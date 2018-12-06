using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class Session
    {
        public static int EntSlotAssigner;
        public static int GetSlot()
        {
            if (++EntSlotAssigner >= EntSlotScaler) EntSlotAssigner = 0;
            return EntSlotAssigner;
        }

        #region WebMonitor
        public void WebMonitor()
        {
            try
            {
                if (Enforced.Debug >= 1 && EntSlotTick) Dsutil1.Sw.Restart();
                _newFrame = false;
                var tick = Tick;
                var shieldList = new List<DefenseShields>(Shields);
                MyAPIGateway.Parallel.For(0, shieldList.Count, x =>
                {
                    var s = shieldList[x];
                    if (_newFrame) return;
                    if (!s.ControlBlockWorking && !s.DsState.State.Lowered)
                    {
                        s.Asleep = true;
                        return;
                    }

                    lock (s.SubLock)
                    {
                        var cleanDistributor = s.MyGridDistributor != null && s.FuncTask.IsComplete && s.MyGridDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects;
                        if (cleanDistributor)
                        {
                            s.GridCurrentPower = s.MyGridDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                            s.GridMaxPower = s.MyGridDistributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);
                        }
                    }

                    if (!IsServer || !s.WasOnline || !ActiveShields.Contains(s))
                    {
                        s.Asleep = true;
                        return;
                    }

                    if (EntSlotTick && RefreshCycle == s.MonitorSlot) MonitorRefreshTasks(s, tick);

                    if (tick < s.LastWokenTick + 400 || s.ShieldComp.GridIsMoving  || s.Missiles.Count > 0)
                    {
                        if (s.ShieldComp.GridIsMoving) s.LastWokenTick = tick;
                        s.Asleep = false;
                        return;
                    }

                    if (!s.PlayerByShield && !s.MoverByShield)
                    {
                        if (s.TicksWithNoActivity++ % EntCleanCycle == 0) s.EntCleanUpTime = true;
                        s.Asleep = true;
                        return;
                    }
                    var monitorList = new List<MyEntity>();
                    var inflatedBox = s.ShieldWorldAabb;
                    var speedBuffer = MaxEntitySpeed / 30;
                    var increasedSize = Vector3D.One * speedBuffer;
                    inflatedBox.Inflate(increasedSize);

                    var intersect = false;
                    MyGamePruningStructure.GetTopmostEntitiesInBox(ref inflatedBox, monitorList, MyEntityQueryType.Dynamic);
                    for (int i = 0; i < monitorList.Count; i++)
                    {
                        var ent = monitorList[i];

                        if (!(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                        if (ent.Physics.IsMoving)
                        {
                            var bObb = MyOrientedBoundingBoxD.CreateFromBoundingBox(ent.PositionComp.WorldAABB);
                            var sObb = MyOrientedBoundingBoxD.CreateFromBoundingBox(inflatedBox);
                            if (sObb.Intersects(ref bObb))
                            {
                                intersect = true;
                                break;
                            }
                        }
                    }

                    if (!intersect)
                    {
                        s.Asleep = true;
                        return;
                    }
                    s.TicksWithNoActivity = 0;
                    s.LastWokenTick = tick;
                    s.Asleep = false;
                });
                if (Enforced.Debug >= 1 && EntSlotTick) Dsutil1.StopWatchReport($"[Monitoring] tick:{tick} - ", -1);
            }
            catch (Exception ex) { Log.Line($"Exception in WebMonitor: {ex}"); }
        }

        private void MonitorRefreshTasks(DefenseShields s, uint tick)
        {

            s.PlayerByShield = false;
            var foundPlayer = false;
            foreach (var character in Characters)
            {
                if (Vector3D.DistanceSquared(character.PositionComp.WorldMatrix.Translation, s.DetectionCenter) < _syncDistSqr)
                {
                    foundPlayer = true;
                    break;
                }
            }
            s.PlayerByShield = foundPlayer;

            if (!s.PlayerByShield)
            {
                s.MoverByShield = false;
                var newMover = false;
                var moverList = new List<MyEntity>();
                var inflatedBox = s.ShieldWorldAabb;
                var increasedSize = Vector3D.One * 3000;
                inflatedBox.Inflate(increasedSize);
                MyGamePruningStructure.GetTopMostEntitiesInBox(ref inflatedBox, moverList, MyEntityQueryType.Dynamic);
                for (int i = 0; i < moverList.Count; i++)
                {
                    var ent = moverList[i];

                    if (!(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                    var entPos = ent.PositionComp.WorldMatrix.Translation;

                    var keyFound = s.EntsByMe.ContainsKey(ent);
                    if (keyFound)
                    {
                        if (!s.EntsByMe[ent].Pos.Equals(entPos, 1e-3))
                        {
                            MoverInfo moverInfo;
                            s.EntsByMe.TryRemove(ent, out moverInfo);
                            s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, tick));
                            newMover = true;
                            break;
                        }
                    }
                    else s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, tick));
                }
                s.MoverByShield = newMover;
            }

            if (tick < s.LastWokenTick + 400)
            {
                s.Asleep = false;
                return;
            }

            if (s.EntCleanUpTime)
            {
                var entsByMeTmp = new List<KeyValuePair<MyEntity, MoverInfo>>();
                entsByMeTmp.AddRange(s.EntsByMe.Where(info => tick - info.Value.CreationTick > EntMaxTickAge * ++s.CleanCycle && !info.Value.Pos.Equals(info.Key.PositionComp.WorldMatrix.Translation, 1e-3)));
                for (int i = 0; i < entsByMeTmp.Count; i++) s.EntsByMe.Remove(entsByMeTmp[i].Key);
                s.EntCleanUpTime = false;
            }
        }
        #endregion

        private void LoadBalancer()
        {
            _newFrame = true;
            Tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick600 = Tick % 600 == 0;
            if (MoreThan600Frames)
            {
                var globalProtCnt = GlobalProtect.Count;
                if (globalProtCnt <= 25) EntSlotScaler = 1;
                else if (globalProtCnt <= 50) EntSlotScaler = 2;
                else if (globalProtCnt <= 75) EntSlotScaler = 3;
                else if (globalProtCnt <= 100) EntSlotScaler = 4;
                else if (globalProtCnt <= 150) EntSlotScaler = 5;
                else if (globalProtCnt <= 200) EntSlotScaler = 6;
                else EntSlotScaler = 9;
            }

            EntSlotTick = Tick % (180 / EntSlotScaler) == 0;

            var shieldsWaking = 0;
            var entsUpdated = 0;
            var entsremoved = 0;
            var entsLostShield = 0;
            if (EntSlotTick)
            {
                if (++RefreshCycle >= EntSlotScaler) RefreshCycle = 0;

                var aa = 0;
                var bb = 0;
                var cc = 0;
                var dd = 0;
                var ee = 0;
                var ff = 0;
                var gg = 0;
                var hh = 0;
                var ii = 0;
                var wrongSlot = 0;

                foreach (var k in GlobalProtect.Values)
                {
                    if (k.RefreshSlot == 0) aa++;
                    else if (k.RefreshSlot == 1) bb++;
                    else if (k.RefreshSlot == 2) cc++;
                    else if (k.RefreshSlot == 3) dd++;
                    else if (k.RefreshSlot == 4) ee++;
                    else if (k.RefreshSlot == 5) ff++;
                    else if (k.RefreshSlot == 6) gg++;
                    else if (k.RefreshSlot == 7) hh++;
                    else if (k.RefreshSlot == 8) ii++;
                }
                GlobalEntTmp.Clear();
                GlobalEntTmp.AddRange(GlobalProtect.Where(info => !MoreThan600Frames || info.Value.RefreshSlot == RefreshCycle && EntSlotTick || info.Value.RefreshSlot > EntSlotScaler - 1));
                for (int i = 0; i < GlobalEntTmp.Count; i++)
                {
                    var ent = GlobalEntTmp[i].Key;
                    var myProtector = GlobalEntTmp[i].Value;
                    var entShields = myProtector.Shields.Keys;

                    var refreshCount = 0;
                    foreach (var s in entShields)
                    {
                        if (!s.ResetEnts(ent, Tick))
                        {
                            myProtector.Shields.Remove(s);
                            entsLostShield++;
                        }
                        else refreshCount++;

                        if (s.Asleep && s.ShieldComp.GetSubGrids.Contains(ent) && (!MoreThan600Frames || s.PlayerByShield || s.MoverByShield || Tick <= s.LastWokenTick + 580))
                        {
                            //Log.Line($"{shieldLost} - {!MoreThan600Frames} - {s.PlayerByShield} - {s.MoverByShield} - {Tick <= s.LastWokenTick + 580}");
                            s.Asleep = false;
                            shieldsWaking++;
                        }
                    }
                    myProtector.Shields.ApplyChanges();

                    if (refreshCount == 0)
                    {
                        GlobalProtect.Remove(ent);
                        entsremoved++;
                    }
                    else entsUpdated++;
                }
                if (Enforced.Debug >= 1) Log.Line($"[NewRefresh] SlotScaler:{EntSlotScaler} - EntsUpdated:{entsUpdated} - ShieldsWaking:{shieldsWaking} - EntsRemoved: {entsremoved} - EntsLostShield:{entsLostShield} - EntInRefreshSlots:({aa} - {bb} - {cc} - {dd} - {ee} - {ff} - {gg} - {hh} - {ii})");
            }
        }

        private void LogicUpdates()
        {
            var y = 0;
            if (EntSlotTick && Enforced.Debug >= 2) Log.Line($"[NearShield] OnlineShields:{Controllers.Count} - ActiveShields:{ActiveShields.Count} - ShieldBlocks: {Shields.Count}");
            var compCount = Controllers.Count;
            if (Enforced.Debug >= 1) Dsutil1.Sw.Restart();
            for (int i = 0; i < compCount; i++)
            {
                var s = Controllers[i];
                if (s.WasOnline && !s.Asleep)
                {
                    if (EntSlotTick && s.LogicSlot == RefreshCycle) s.ProtectMyself();
                    s.WebEntities();
                    y++;
                }
                s.CleanTimes();
                s.DeformEnabled = false;
            }
            if (Enforced.Debug >= 1 && EntSlotTick) Dsutil1.StopWatchReport($"[Protecting] ProtectedEnts:{GlobalProtect.Count} - WakingShields:{y} - CPU:", -1);
            else if (Enforced.Debug >= 1) Dsutil1.Sw.Reset();

            if (SphereOnCamera.Length != compCount) Array.Resize(ref SphereOnCamera, compCount);
        }
    }
}

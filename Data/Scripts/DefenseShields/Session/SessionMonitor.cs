namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::DefenseShields.Support;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Weapons;
    using VRage;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class Session
    {
        private static volatile int _entSlotAssigner;
        internal static int GetSlot()
        {
            if (++_entSlotAssigner >= Instance.EntSlotScaler) _entSlotAssigner = 0;
            return _entSlotAssigner;
        }

        #region WebMonitor
        internal void WebMonitor()
        {
            try
            {
                while (Monitor)
                {
                    _autoResetEvent.WaitOne();
                    if (!Monitor) break;
                    //if (Enforced.Debug >= 3 && EntSlotTick) Dsutil2.Sw.Restart();
                    _newFrame = false;
                    _workData.DoIt(new List<DefenseShields>(FunctionalShields.Keys), Tick);
                    MinScaler = _workData.MinScaler;
                    MyAPIGateway.Parallel.For(0, _workData.ShieldCnt, x =>
                    {
                        var s = _workData.ShieldList[x];
                        var tick = _workData.Tick;
                        if (_newFrame || s.MarkedForClose || !s.Warming) return;
                        var reInforce = s.DsState.State.ReInforce;
                        if (!IsServer)
                        {
                            lock (s.GetCubesLock)
                            {
                                var cleanDistributor = s.MyGridDistributor != null && s.FuncTask.IsComplete && s.MyGridDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects;
                                if (cleanDistributor)
                                {
                                    s.GridCurrentPower = s.MyGridDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                                    s.GridMaxPower = s.MyGridDistributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);
                                }

                                if (reInforce != s.ReInforcedShield)
                                {
                                    //if (Enforced.Debug == 4) Log.Line("Client queuing entFresh for reinforced shield");
                                    foreach (var sub in s.ShieldComp.GetSubGrids) _entRefreshQueue.Enqueue(sub);
                                    s.ReInforcedShield = reInforce;
                                }

                            }
                            s.TicksWithNoActivity = 0;
                            s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }

                        var shieldActive = ActiveShields.ContainsKey(s);
                        if (s.LostPings > 59)
                        {
                            if (shieldActive)
                            {
                                if (Enforced.Debug >= 2) Log.Line("Logic Paused by lost pings");
                                bool value;
                                ActiveShields.TryRemove(s, out value);
                                s.WasPaused = true;
                            }
                            s.Asleep = false;
                            return;
                        }
                        //if (Enforced.Debug >= 2 && s.LostPings > 0) Log.Line($"Lost Logic Pings:{s.LostPings}");
                        if (shieldActive) s.LostPings++;

                        lock (s.GetCubesLock)
                        {
                            var cleanDistributor = s.MyGridDistributor != null && s.FuncTask.IsComplete && s.MyGridDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects;
                            if (cleanDistributor)
                            {
                                s.GridCurrentPower = s.MyGridDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                                s.GridMaxPower = s.MyGridDistributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);
                            }
                        }

                        if (s.Asleep && EmpStore.Count != 0 && Vector3D.DistanceSquared(s.DetectionCenter, EmpWork.EpiCenter) <= SyncDistSqr)
                        {
                            s.TicksWithNoActivity = 0;
                            s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }

                        if (!shieldActive && s.LostPings > 59)
                        {
                            s.Asleep = true;
                            return;
                        }

                        List<MyEntity> monitorList = null;
                        var newSub = false;
                        if (!reInforce) monitorList = new List<MyEntity>();
                        if (EntSlotTick && RefreshCycle == s.MonitorSlot) MonitorRefreshTasks(x, ref monitorList, reInforce, ref newSub);

                        if (reInforce) return;

                        if (tick < s.LastWokenTick + 400 || s.ShieldComp.GridIsMoving || s.Missiles.Count > 0)
                        {
                            if (s.ShieldComp.GridIsMoving) s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }
                        if (!s.PlayerByShield && !s.MoverByShield && !s.NewEntByShield)
                        {
                            if (s.TicksWithNoActivity++ % EntCleanCycle == 0) s.EntCleanUpTime = true;
                            if (tick > 1200 && !s.WasPaused)
                            {
                                if (Enforced.Debug >= 2) Log.Line($"Logic Paused by monitor");
                                bool value;
                                ActiveShields.TryRemove(s, out value);
                                s.WasPaused = true;
                                s.Asleep = false;
                                s.TicksWithNoActivity = 0;
                                s.LastWokenTick = tick;
                            }
                            else s.Asleep = true;
                            return;
                        }

                        var intersect = false;
                        if (!(EntSlotTick && RefreshCycle == s.MonitorSlot)) MyGamePruningStructure.GetTopmostEntitiesInBox(ref s.WebBox, monitorList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < monitorList.Count; i++)
                        {
                            var ent = monitorList[i];

                            if (ent.Physics == null || !(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                            if (ent.Physics.IsMoving)
                            {
                                if (s.WebBox.Intersects(ent.PositionComp.WorldAABB))
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

                    if (_workData.Tick % 180 == 0 && _workData.Tick > 1199)
                    {
                        _entRefreshTmpList.Clear();
                        _entRefreshTmpList.AddRange(_globalEntTmp.Where(info => _workData.Tick - 540 > info.Value));
                        foreach (var dict in _entRefreshTmpList)
                        {
                            var ent = dict.Key;
                            _entRefreshQueue.Enqueue(ent);
                            uint value;
                            _globalEntTmp.TryRemove(ent, out value);
                        }
                    }
                    //if (Enforced.Debug >= 3 && EntSlotTick) Dsutil2.StopWatchReport("monitor", -1);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in WebMonitor: {ex}"); }
        }

        internal void MonitorRefreshTasks(int x, ref List<MyEntity> monitorList, bool reInforce, ref bool newSub)
        {
            var s = _workData.ShieldList[x];

            if (reInforce)
            {
                HashSet<MyCubeGrid> subs;
                lock (s.GetCubesLock) subs = new HashSet<MyCubeGrid>(s.ShieldComp.GetSubGrids);
                var newMode = !s.ReInforcedShield;
                if (!newMode) return;
                foreach (var sub in subs)
                {
                    //if (Enforced.Debug >= 2) Log.Line("Server queuing entFresh for reinforced shield");

                    if (!_globalEntTmp.ContainsKey(sub)) newSub = true;
                    _entRefreshQueue.Enqueue(sub);
                    if (!s.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                }

                s.ReInforcedShield = true;
                s.TicksWithNoActivity = 0;
                s.LastWokenTick = _workData.Tick;
                s.Asleep = false;
            }
            else
            {
                var newMode = false;
                if (s.ReInforcedShield)
                {
                    HashSet<MyCubeGrid> subs;
                    lock (s.GetCubesLock) subs = new HashSet<MyCubeGrid>(s.ShieldComp.GetSubGrids); 
                    foreach (var sub in subs)
                    {
                        _entRefreshQueue.Enqueue(sub);
                        if (!s.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                    }
                    //if (Enforced.Debug >= 2) Log.Line($"found Reinforce");
                    s.ReInforcedShield = false;
                    s.TicksWithNoActivity = 0;
                    s.LastWokenTick = _workData.Tick;
                    s.Asleep = false;
                    newMode = true;
                }

                if (!newMode)
                {
                    // var testMat = s.DetectMatrixOutside;
                    // var shape1 = new Sphere(Vector3D.Zero, 1.0).Transformed(testMat);
                    var foundNewEnt = false;
                    var disableVoxels = Enforced.DisableVoxelSupport == 1 || s.ShieldComp.Modulator == null || s.ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
                    MyGamePruningStructure.GetTopmostEntitiesInBox(ref s.WebBox, monitorList);
                    foreach (var ent in monitorList)
                    {
                        var voxel = ent as MyVoxelBase;
                        if (ent == null || ent.MarkedForClose || (voxel == null && (ent.Physics == null || ent.DefinitionId == null)) || (!s.GridIsMobile && voxel != null) || (disableVoxels && voxel != null) || (voxel != null && voxel != voxel.RootVoxel))
                        {
                            continue;
                        }

                        if (ent is IMyFloatingObject || ent is IMyEngineerToolBase || !s.WebSphere.Intersects(ent.PositionComp.WorldVolume)) continue;

                        // var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
                        // if (halfExtents.X < 1) halfExtents.X = 10;
                        // if (halfExtents.Y < 1) halfExtents.Y = 10;
                        // if (halfExtents.Z < 1) halfExtents.Z = 10;
                        // var shape2 = new Box(-halfExtents, halfExtents).Transformed(ent.WorldMatrix);
                        // var test = Gjk.Intersects(ref shape1, ref shape2);
                        // Log.Line($"{ent.DebugName} - {test}");
                        if (CustomCollision.NewObbPointsInShield(ent, s.DetectMatrixOutsideInv) > 0)
                        {
                            if (!s.WasPaused && !_globalEntTmp.ContainsKey(ent))
                            {
                                foundNewEnt = true;
                                s.Asleep = false;
                                //if (Enforced.Debug >= 2) Log.Line($"New entity");
                            }

                            if (!s.WasPaused) _globalEntTmp[ent] = _workData.Tick;
                        }
                        s.NewEntByShield = foundNewEnt;
                    }
                    if (!s.NewEntByShield)
                    {
                        var foundPlayer = false;
                        foreach (var player in Players.Values)
                        {
                            var character = player.Character;
                            if (character == null) continue;

                            if (Vector3D.DistanceSquared(character.PositionComp.WorldMatrix.Translation, s.DetectionCenter) < SyncDistSqr)
                            {
                                foundPlayer = true;
                                break;
                            }
                        }
                        s.PlayerByShield = foundPlayer;
                    }
                    if (!s.PlayerByShield)
                    {
                        s.MoverByShield = false;
                        var newMover = false;
                        var moverList = new List<MyEntity>();

                        MyGamePruningStructure.GetTopMostEntitiesInBox(ref s.ShieldBox3K, moverList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < moverList.Count; i++)
                        {
                            var ent = moverList[i];

                            if (!(ent.Physics == null || ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                            var entPos = ent.PositionComp.WorldMatrix.Translation;

                            var keyFound = s.EntsByMe.ContainsKey(ent);
                            if (keyFound)
                            {
                                if (!s.EntsByMe[ent].Pos.Equals(entPos, 1e-3))
                                {
                                    //if (Enforced.Debug >= 2) Log.Line($"[Moved] Ent:{ent.DebugName}");
                                    MoverInfo moverInfo;
                                    s.EntsByMe.TryRemove(ent, out moverInfo);
                                    s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                                    newMover = true;
                                    break;
                                }
                            }
                            else s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                        }
                        s.MoverByShield = newMover;
                    }

                    if (_workData.Tick < s.LastWokenTick + 400)
                    {
                        s.Asleep = false;
                        return;
                    }
                }

                if (s.EntCleanUpTime)
                {
                    s.EntCleanUpTime = false;
                    if (!s.EntsByMe.IsEmpty)
                    {
                        var entsByMeTmp = new List<KeyValuePair<MyEntity, MoverInfo>>();
                        entsByMeTmp.AddRange(s.EntsByMe.Where(info => !info.Key.InScene || _workData.Tick - info.Value.CreationTick > EntMaxTickAge));
                        for (int i = 0; i < entsByMeTmp.Count; i++)
                        {
                            MoverInfo mInfo;
                            s.EntsByMe.TryRemove(entsByMeTmp[i].Key, out mInfo);
                        }
                    }
                }
            }
        }
        #endregion

        private void LoadBalancer()
        {
            var shieldsWaking = 0;
            var entsUpdated = 0;
            var entsremoved = 0;
            var entsLostShield = 0;

            if (++RefreshCycle >= EntSlotScaler) RefreshCycle = 0;
            MyEntity ent;
            while (_entRefreshQueue.TryDequeue(out ent))
            {
                MyProtectors myProtector;
                if (!GlobalProtect.TryGetValue(ent, out myProtector)) continue;

                var entShields = myProtector.Shields;
                var refreshCount = 0;
                DefenseShields iShield = null;
                var removeIShield = false;
                foreach (var s in entShields)
                {
                    if (s.WasPaused) continue;
                    if (s.DsState.State.ReInforce && s.ShieldComp.GetSubGrids.Contains(ent))
                    {
                        //if (Enforced.Debug == 4) Log.Line("_entRefreshQueue adding Reinfroced");
                        iShield = s;
                        refreshCount++;
                    }
                    else if (!ent.InScene || !s.ResetEnts(ent, Tick))
                    {
                        myProtector.Shields.Remove(s);
                        entsLostShield++;
                    }
                    else refreshCount++;

                    if (iShield == null && myProtector.IntegrityShield == s)
                    {
                        removeIShield = true;
                        myProtector.IntegrityShield = null;
                    }

                    var detectedStates = s.PlayerByShield || s.MoverByShield || Tick <= s.LastWokenTick + 580 || iShield != null || removeIShield;
                    if (ScalerChanged || detectedStates)
                    {
                        s.Asleep = false;
                        shieldsWaking++;
                    }
                }

                if (iShield != null)
                {
                    myProtector.Shields.Remove(iShield);
                    myProtector.IntegrityShield = iShield;
                }

                myProtector.Shields.ApplyChanges();

                if (refreshCount == 0)
                {
                    GlobalProtect.Remove(ent);
                    ProtSets.Return(myProtector);
                    entsremoved++;
                }
                else entsUpdated++;
            }
            /*
            if (Enforced.Debug >= 2 || (Tick1800 && Enforced.Debug == 1))
            {
                for (int i = 0; i < SlotCnt.Length; i++) SlotCnt[i] = 0;
                foreach (var pair in GlobalProtect) SlotCnt[pair.Value.RefreshSlot]++;
            }
            if (Enforced.Debug >= 2 || (Enforced.Debug == 1 && Tick1800))
            {
                Log.Line($"[NewRefresh] SlotScaler:{EntSlotScaler} - EntsUpdated:{entsUpdated} - ShieldsWaking:{shieldsWaking} - EntsRemoved: {entsremoved} - EntsLostShield:{entsLostShield} - EntInRefreshSlots:({SlotCnt[0]} - {SlotCnt[1]} - {SlotCnt[2]} - {SlotCnt[3]} - {SlotCnt[4]} - {SlotCnt[5]} - {SlotCnt[6]} - {SlotCnt[7]} - {SlotCnt[8]}) \n" +
                                                $"                                     ProtectedEnts:{GlobalProtect.Count} - ActiveShields:{ActiveShields.Count} - FunctionalShields:{FunctionalShields.Count} - AllControllerBlocks:{Controllers.Count}");
            }
            */
        }

        private void LogicUpdates()
        {
            //if (Enforced.Debug >= 3 && EntSlotTick) Dsutil1.Sw.Restart();
            foreach (var s in ActiveShields.Keys)
            {
                if (!s.WasOnline || s.Asleep) continue;

                if (s.DsState.State.ReInforce)
                {
                    s.DeformEnabled = true;
                    s.ProtectSubs(Tick);
                    continue;
                }
                if (Tick20 && Tick - s.EffectsCleanTick < 41) s.ResetDamageEffects();
                if (Tick600) s.CleanWebEnts();

                s.WebEntities();
            }

            if (!Dispatched && WebWrapperOn)
            {
                MyAPIGateway.Parallel.Start(WebDispatch, WebDispatchDone);
                Dispatched = true;
                WebWrapperOn = false;
            }

            //if (Enforced.Debug >= 3 && EntSlotTick) Dsutil1.StopWatchReport("[LogicUpdate] - CPU:", -1);
            //else if (Enforced.Debug >= 3) Dsutil1.Sw.Reset();
        }

        private void WebDispatch()
        {
            DefenseShields shield;
            while (WebWrapper.TryDequeue(out shield))
            {
                if (shield == null || shield.MarkedForClose) continue;
                if (!shield.VoxelsToIntersect.IsEmpty) MyAPIGateway.Parallel.Start(shield.VoxelIntersect);
                if (!shield.WebEnts.IsEmpty) MyAPIGateway.Parallel.ForEach(shield.WebEnts, shield.EntIntersectSelector);
            }
        }

        private void WebDispatchDone()
        {
            Dispatched = false;
        }

        private void Scale()
        {
            if (Tick < 600) return;
            var oldScaler = EntSlotScaler;
            var globalProtCnt = GlobalProtect.Count;

            if (globalProtCnt <= 25) EntSlotScaler = 1;
            else if (globalProtCnt <= 50) EntSlotScaler = 2;
            else if (globalProtCnt <= 75) EntSlotScaler = 3;
            else if (globalProtCnt <= 100) EntSlotScaler = 4;
            else if (globalProtCnt <= 150) EntSlotScaler = 5;
            else if (globalProtCnt <= 200) EntSlotScaler = 6;
            else EntSlotScaler = 9;

            if (EntSlotScaler < MinScaler) EntSlotScaler = MinScaler;

            if (oldScaler != EntSlotScaler)
            {
                GlobalProtect.Clear();
                ProtSets.Clean();
                foreach (var s in FunctionalShields.Keys)
                {
                    s.AssignSlots();
                    s.Asleep = false;
                }
                foreach (var c in Controllers)
                {
                    if (FunctionalShields.ContainsKey(c)) continue;
                    c.AssignSlots();
                    c.Asleep = false;
                }
                ScalerChanged = true;
            }
            else ScalerChanged = false;
        }
    }
}

namespace DefenseSystems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Weapons;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class Session
    {
        #region WebMonitor
        internal void WebMonitor()
        {
            try
            {
                while (Monitor)
                {
                    _autoResetEvent.WaitOne();
                    if (!Monitor) break;
                    _newFrame = false;
                    _workData.DoIt(new List<Controllers>(FunctionalShields.Keys), Tick);
                    MinScaler = _workData.MinScaler;
                    MyAPIGateway.Parallel.For(0, _workData.ShieldCnt, x =>
                    {
                        var c = _workData.ShieldList[x];
                        var b = c.Bus;
                        var f = b.Field;

                        var tick = _workData.Tick;
                        var notBubble = c.State.Value.ProtectMode > 0;
                        var notField = c.State.Value.ProtectMode != 2;

                        if (_newFrame || c.MarkedForClose || !notField && !f.Warming) return;
                        if (!IsServer)
                        {
                            if (notBubble != c.NotBubble)
                            {
                                lock (b.SubLock) foreach (var sub in b.SubGrids) _entRefreshQueue.Enqueue(sub);
                                c.NotBubble = notBubble;
                            }

                            if (EntSlotTick && RefreshCycle == c.MonitorSlot)
                            {
                                List<MyEntity> monitorListClient = null;
                                var newSubClient = false;
                                if (!notBubble) monitorListClient = new List<MyEntity>();
                                MonitorRefreshTasks(x, ref monitorListClient, notBubble, ref newSubClient);
                            }
                            c.TicksWithNoActivity = 0;
                            c.LastWokenTick = tick;
                            c.Asleep = false;
                            return;
                        }

                        bool shieldActive;
                        lock (ActiveProtection) shieldActive = ActiveProtection.Contains(c);

                        if (c.LostPings > 59)
                        {
                            if (shieldActive)
                            {
                                if (Enforced.Debug >= 2) Log.Line("Logic Paused by lost pings");
                                lock (ActiveProtection) ActiveProtection.Remove(c);
                                c.WasPaused = true;
                            }
                            c.Asleep = false;
                            return;
                        }
                        if (Enforced.Debug >= 2 && c.LostPings > 0) Log.Line($"Lost Logic Pings:{c.LostPings}");
                        if (shieldActive) c.LostPings++;

                        if (c.Asleep && EmpStore.Count != 0 && Vector3D.DistanceSquared(f.DetectionCenter, EmpWork.EpiCenter) <= SyncDistSqr)
                        {
                            c.TicksWithNoActivity = 0;
                            c.LastWokenTick = tick;
                            c.Asleep = false;
                            return;
                        }

                        if (!shieldActive && c.LostPings > 59)
                        {
                            c.Asleep = true;
                            return;
                        }

                        List<MyEntity> monitorList = null;
                        var newSub = false;
                        if (!notBubble) monitorList = new List<MyEntity>();
                        if (EntSlotTick && RefreshCycle == c.MonitorSlot) MonitorRefreshTasks(x, ref monitorList, notBubble, ref newSub);

                        if (notBubble) return;
                        if (tick < c.LastWokenTick + 400 || f.Missiles.Count > 0)
                        {
                            c.Asleep = false;
                            return;
                        }

                        if (f.ShieldIsMobile && b.Spine.Physics.IsMoving)
                        {
                            c.LastWokenTick = tick;
                            c.Asleep = false;
                            return;
                        }

                        if (!c.PlayerByShield && !c.MoverByShield && !c.NewEntByShield)
                        {
                            if (c.TicksWithNoActivity++ % EntCleanCycle == 0) c.EntCleanUpTime = true;
                            if (shieldActive && !c.WasPaused && tick > 1200)
                            {
                                if (Enforced.Debug >= 2) Log.Line($"Logic Paused by monitor");
                                lock (ActiveProtection) ActiveProtection.Remove(c);
                                c.WasPaused = true;
                                c.Asleep = false;
                                c.TicksWithNoActivity = 0;
                                c.LastWokenTick = tick;
                            }
                            else c.Asleep = true;
                            return;
                        }

                        var intersect = false;
                        if (!(EntSlotTick && RefreshCycle == c.MonitorSlot)) MyGamePruningStructure.GetTopmostEntitiesInBox(ref f.WebBox, monitorList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < monitorList.Count; i++)
                        {
                            var ent = monitorList[i];

                            if (ent.Physics == null || !(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                            if (ent.Physics.IsMoving)
                            {
                                if (f.WebBox.Intersects(ent.PositionComp.WorldAABB))
                                {
                                    intersect = true;
                                    break;
                                }
                            }
                        }

                        if (!intersect)
                        {
                            c.Asleep = true;
                            return;
                        }
                        c.TicksWithNoActivity = 0;
                        c.LastWokenTick = tick;
                        c.Asleep = false;
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
                }
            }
            catch (Exception ex) { Log.Line($"Exception in WebMonitor: {ex}"); }
        }

        internal void MonitorRefreshTasks(int x, ref List<MyEntity> monitorList, bool reInforce, ref bool newSub)
        {
            var c = _workData.ShieldList[x];
            var b = c.Bus;
            var f = b.Field;

            if (reInforce)
            {
                HashSet<MyCubeGrid> subs;
                lock (b.SubLock) subs = new HashSet<MyCubeGrid>(b.SubGrids);
                var newMode = !c.NotBubble;
                if (!newMode) return;
                foreach (var sub in subs)
                {
                    if (!_globalEntTmp.ContainsKey(sub)) newSub = true;
                    _entRefreshQueue.Enqueue(sub);
                    if (!c.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                }

                c.NotBubble = true;
                c.TicksWithNoActivity = 0;
                c.LastWokenTick = _workData.Tick;
                c.Asleep = false;
            }
            else
            {
                var newMode = false;
                if (c.NotBubble)
                {
                    HashSet<MyCubeGrid> subs;
                    lock (b.SubLock) subs = new HashSet<MyCubeGrid>(b.SubGrids); 
                    foreach (var sub in subs)
                    {
                        _entRefreshQueue.Enqueue(sub);
                        if (!c.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                    }
                    //if (Enforced.Debug >= 2) Log.Line($"found Reinforce");
                    c.NotBubble = false;
                    c.TicksWithNoActivity = 0;
                    c.LastWokenTick = _workData.Tick;
                    c.Asleep = false;
                    newMode = true;
                }

                if (!newMode)
                {
                    // var testMat = s.DetectMatrixOutside;
                    // var shape1 = new Sphere(Vector3D.Zero, 1.0).Transformed(testMat);
                    var foundNewEnt = false;
                    var disableVoxels = Enforced.DisableVoxelSupport == 1 || b.ActiveModulator == null || b.ActiveModulator.ModSet.Settings.ModulateVoxels;
                    MyGamePruningStructure.GetTopmostEntitiesInBox(ref f.WebBox, monitorList);
                    if (!c.WasPaused)
                    {
                        foreach (var ent in monitorList)
                        {
                            var voxel = ent as MyVoxelBase;
                            if (ent == null || ent.MarkedForClose || (voxel == null && (ent.Physics == null || ent.DefinitionId == null)) || (!f.ShieldIsMobile && voxel != null) || (disableVoxels && voxel != null) || (voxel != null && voxel != voxel.RootVoxel))
                            {
                                continue;
                            }

                            if (ent is IMyFloatingObject || ent is IMyEngineerToolBase || !f.WebSphere.Intersects(ent.PositionComp.WorldVolume)) continue;

                            // var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
                            // if (halfExtents.X < 1) halfExtents.X = 10;
                            // if (halfExtents.Y < 1) halfExtents.Y = 10;
                            // if (halfExtents.Z < 1) halfExtents.Z = 10;
                            // var shape2 = new Box(-halfExtents, halfExtents).Transformed(ent.WorldMatrix);
                            // var test = Gjk.Intersects(ref shape1, ref shape2);
                            // Log.Line($"{ent.DebugName} - {test}");
                            if (CustomCollision.NewObbPointsInShield(ent, f.DetectMatrixOutsideInv) > 0)
                            {
                                if (!_globalEntTmp.ContainsKey(ent))
                                {
                                    foundNewEnt = true;
                                    c.Asleep = false;
                                }

                                _globalEntTmp[ent] = _workData.Tick;
                            }
                            c.NewEntByShield = foundNewEnt;
                        }
                    }
                    else c.NewEntByShield = false;

                    if (!c.NewEntByShield)
                    {
                        var foundPlayer = false;
                        foreach (var player in Players.Values)
                        {
                            var character = player.Character;
                            if (character == null) continue;

                            if (Vector3D.DistanceSquared(character.PositionComp.WorldMatrix.Translation, f.DetectionCenter) < SyncDistSqr)
                            {
                                foundPlayer = true;
                                break;
                            }
                        }
                        c.PlayerByShield = foundPlayer;
                    }
                    if (!c.PlayerByShield)
                    {
                        c.MoverByShield = false;
                        var newMover = false;
                        var moverList = new List<MyEntity>();

                        MyGamePruningStructure.GetTopMostEntitiesInBox(ref f.ShieldBox3K, moverList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < moverList.Count; i++)
                        {
                            var ent = moverList[i];

                            var meteor = ent as IMyMeteor;
                            if (meteor != null)
                            {
                                if (CustomCollision.FutureIntersect(f, ent, f.DetectMatrixOutside, f.DetectMatrixOutsideInv))
                                {
                                    if (Enforced.Debug >= 2) Log.Line($"[Future Intersecting Meteor] distance from shieldCenter: {Vector3D.Distance((Vector3D) f.DetectionCenter, ent.WorldMatrix.Translation)} - waking:");
                                    newMover = true;
                                    break;
                                }
                                continue;
                            }

                            if (!(ent.Physics == null || ent is MyCubeGrid || ent is IMyCharacter)) continue;
                            var entPos = ent.PositionComp.WorldAABB.Center;

                            var keyFound = c.EntsByMe.ContainsKey(ent);
                            if (keyFound)
                            {
                                if (!c.EntsByMe[ent].Pos.Equals(entPos, 1e-3))
                                {
                                    MoverInfo moverInfo;
                                    c.EntsByMe.TryRemove(ent, out moverInfo);
                                    c.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                                    if (moverInfo.CreationTick == _workData.Tick - 1)
                                    {
                                        if (Enforced.Debug >= 3 && c.WasPaused) Log.Line($"[Moved] Ent:{ent.DebugName} - howMuch:{Vector3D.Distance(entPos, c.EntsByMe[ent].Pos)} - ControllerId [{c.Controller.EntityId}]");
                                        newMover = true;
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                if (Enforced.Debug >= 3) Log.Line($"[NewMover] Ent:{ent.DebugName} - ControllerId [{c.Controller.EntityId}]");
                                c.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                            }
                        }
                        c.MoverByShield = newMover;
                    }

                    if (_workData.Tick < c.LastWokenTick + 400)
                    {
                        c.Asleep = false;
                        return;
                    }
                }

                if (c.EntCleanUpTime)
                {
                    c.EntCleanUpTime = false;
                    if (!c.EntsByMe.IsEmpty)
                    {
                        var entsByMeTmp = new List<KeyValuePair<MyEntity, MoverInfo>>();
                        entsByMeTmp.AddRange(c.EntsByMe.Where(info => !info.Key.InScene || _workData.Tick - info.Value.CreationTick > EntMaxTickAge));
                        for (int i = 0; i < entsByMeTmp.Count; i++)
                        {
                            MoverInfo mInfo;
                            c.EntsByMe.TryRemove(entsByMeTmp[i].Key, out mInfo);
                        }
                    }
                }
            }
        }
        #endregion

        #region Timings / LoadBalancer
        private void Timings()
        {
            _newFrame = true;
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick1800 && AuthorPlayerId != 0) AuthorDebug();
            if (LogStats && (IsServer && LogServer || !IsServer && !LogServer))
            {
                Perf.Ticker(Tick, LogTime, LogFullReport, LogColumn);
            }
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10)
                    {
                        _eCount = 0;
                        _previousEntId = -1;
                    }
                }
            }
            if (!GameLoaded && Tick > 100)
            {
                if (FirstLoop && Tick > 100)
                {
                    if (!WarHeadLoaded && WarTerminalReset != null)
                    {
                        WarTerminalReset.ShowInTerminal = true;
                        WarTerminalReset = null;
                        WarHeadLoaded = true;
                    }

                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        UtilsStatic.GetDefinitons();
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);
                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    _bTapi.Init();
                }
            }
            if (EmpWork.EventRunning && EmpWork.Computed) EmpWork.EventComplete();

            if (Tick20)
            {
                Scale();
                EntSlotTick = Tick % (180 / EntSlotScaler) == 0;
                if (EntSlotTick) LoadBalancer();
            }
            else EntSlotTick = false;
        }

        internal static int GetSlot()
        {
            if (++_entSlotAssigner >= Instance.EntSlotScaler) _entSlotAssigner = 0;
            return _entSlotAssigner;
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
                foreach (var c in AllControllers)
                {
                    if (FunctionalShields.ContainsKey(c)) continue;
                    c.AssignSlots();
                    c.Asleep = false;
                }
                ScalerChanged = true;
            }
            else ScalerChanged = false;
        }

        private void LoadBalancer()
        {

            if (++RefreshCycle >= EntSlotScaler) RefreshCycle = 0;
            MyEntity ent;
            while (_entRefreshQueue.TryDequeue(out ent))
            {
                MyProtectors myProtector;
                if (!GlobalProtect.TryGetValue(ent, out myProtector)) continue;

                var entShields = myProtector.Controllers;
                var refreshCount = 0;
                Controllers notBubble = null;
                var removeIShield = false;
                foreach (var c in entShields)
                {
                    if (c.WasPaused) continue;
                    if (c.State.Value.ProtectMode > 0 && c.Bus.SubGrids.Contains(ent))
                    {
                        notBubble = c;
                        refreshCount++;
                    }
                    else if (!ent.InScene || !c.Bus.Field.ResetEnts(ent, Tick))
                    {
                        myProtector.Controllers.Remove(c);
                    }
                    else refreshCount++;

                    if (notBubble == null && myProtector.NotBubble == c)
                    {
                        removeIShield = true;
                        myProtector.NotBubble = null;
                    }

                    var detectedStates = c.PlayerByShield || c.MoverByShield || Tick <= c.LastWokenTick + 580 || notBubble != null || removeIShield;
                    if (ScalerChanged || detectedStates)
                    {
                        c.Asleep = false;
                    }
                }

                if (notBubble != null)
                {
                    myProtector.Controllers.Remove(notBubble);
                    myProtector.NotBubble = notBubble;
                }

                myProtector.Controllers.ApplyChanges();

                if (refreshCount == 0)
                {
                    GlobalProtect.Remove(ent);
                    ProtSets.Return(myProtector);
                }
            }
        }
        #endregion

        #region LogicUpdates
        private void LogicUpdates()
        {
            if (!Dispatched)
            {
                lock (ActiveProtection)
                {
                    if (LogStats)
                    {
                        Perf.Active(ActiveProtection.Count);
                        Perf.Paused(AllControllers.Count - FunctionalShields.Count);
                        Perf.Emitters(Emitters.Count);
                        Perf.Modulators(Modulators.Count);
                        Perf.Displays(Displays.Count);
                        Perf.Enhancers(Enhancers.Count);
                        Perf.O2Generators(O2Generators.Count);
                        Perf.Protected(GlobalProtect.Count);
                    }

                    foreach (var s in ActiveProtection)
                    {
                        if (s.Asleep)
                        {
                            if (LogStats) Perf.Asleep();
                            continue;
                        }
                        if (LogStats) Perf.Awake();
                        var protMode = s.State.Value.ProtectMode;
                        if (protMode > 0)
                        {
                            if (protMode == 1) s.Bus.Field.DeformEnabled = true;
                            s.ProtectSubs(Tick);
                            continue;
                        }

                        if (!DedicatedServer && Tick20 && s.Bus.EffectsDirty) s.Bus.ResetDamageEffects();
                        if (Tick600) s.Bus.Field.CleanWebEnts();
                        s.Bus.Field.WebEntities();
                    }
                }
                if (WebWrapperOn)
                {
                    Dispatched = true;
                    MyAPIGateway.Parallel.Start(WebDispatch, WebDispatchDone);
                    WebWrapperOn = false;
                }
            }
        }

        private void WebDispatch()
        {
            Fields field;
            while (WebWrapper.TryDequeue(out field))
            {
                if (field == null) continue;
                if (!field.VoxelsToIntersect.IsEmpty) MyAPIGateway.Parallel.Start(field.VoxelIntersect);
                if (!field.WebEnts.IsEmpty) MyAPIGateway.Parallel.ForEach(field.WebEnts, field.EntIntersectSelector);
            }
        }

        private void WebDispatchDone()
        {
            Dispatched = false;
        }
        #endregion
    }
}

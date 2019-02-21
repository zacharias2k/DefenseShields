namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using Support;
    using Sandbox.Definitions;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;
    using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
    using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, int.MinValue)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
                IsServer = MyAPIGateway.Multiplayer.IsServer;
                DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

                var env = MyDefinitionManager.Static.EnvironmentDefinition;
                if (env.LargeShipMaxSpeed > MaxEntitySpeed) MaxEntitySpeed = env.LargeShipMaxSpeed;
                else if (env.SmallShipMaxSpeed > MaxEntitySpeed) MaxEntitySpeed = env.SmallShipMaxSpeed;

                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started: Server:{IsServer} - Dedicated:{DedicatedServer} - MpActive:{MpActive}");

                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, ReceivedPacket);

                if (!MpActive) Players.TryAdd(MyAPIGateway.Session.Player.IdentityId, MyAPIGateway.Session.Player);
                MyEntities.OnEntityRemove += OnEntityRemove;

                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
                if (!DedicatedServer)
                {
                    MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
                }

                if (IsServer)
                {
                    Log.Line("LoadConf - Session: This is a server");
                    UtilsStatic.PrepConfigFile();
                    UtilsStatic.ReadConfigFile();
                }

                if (MpActive)
                {
                    SyncDist = MyAPIGateway.Session.SessionSettings.SyncDistance;
                    SyncDistSqr = SyncDist * SyncDist;
                    SyncBufferedDistSqr = SyncDistSqr + 250000;
                    if (Enforced.Debug >= 2) Log.Line($"SyncDistSqr:{SyncDistSqr} - SyncBufferedDistSqr:{SyncBufferedDistSqr} - DistNorm:{SyncDist}");
                }
                else
                {
                    SyncDist = MyAPIGateway.Session.SessionSettings.ViewDistance;
                    SyncDistSqr = SyncDist * SyncDist;
                    SyncBufferedDistSqr = SyncDistSqr + 250000;
                    if (Enforced.Debug >= 2) Log.Line($"SyncDistSqr:{SyncDistSqr} - SyncBufferedDistSqr:{SyncBufferedDistSqr} - DistNorm:{SyncDist}");
                }
                MyAPIGateway.Parallel.StartBackground(WebMonitor);
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void Draw()
        {
            if (DedicatedServer) return;
            try
            {
                var compCount = Controllers.Count;
                if (compCount == 0) return;

                if (SphereOnCamera.Length != compCount) Array.Resize(ref SphereOnCamera, compCount);

                if (_count == 0 && _lCount == 0) OnCountThrottle = false;
                var onCount = 0;
                for (int i = 0; i < compCount; i++)
                {
                    var s = Controllers[i];
                    if (s.WasPaused || s.DsState.State.Suspended) continue;

                    if (s.KineticCoolDown > -1)
                    {
                        s.KineticCoolDown++;
                        if (s.KineticCoolDown == 9) s.KineticCoolDown = -1;
                    }

                    if (s.EnergyCoolDown > -1)
                    {
                        s.EnergyCoolDown++;
                        if (s.EnergyCoolDown == 9) s.EnergyCoolDown = -1;
                    }

                    if (s.WebCoolDown > -1)
                    {
                        s.WebCoolDown++;
                        if (s.WebCoolDown == 6) s.WebCoolDown = -1;
                    }

                    if (!s.WarmedUp || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking) continue;
                    var sp = new BoundingSphereD(s.DetectionCenter, s.BoundingRange);
                    if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp))
                    {
                        SphereOnCamera[i] = false;
                        continue;
                    }
                    SphereOnCamera[i] = true;
                    if (!s.Icosphere.ImpactsFinished) onCount++;
                }

                if (onCount >= OnCount)
                {
                    OnCount = onCount;
                    OnCountThrottle = true;
                }
                else if (!OnCountThrottle && _count == 59 && _lCount == 9) OnCount = onCount;

                for (int i = 0; i < compCount; i++)
                {
                    var s = Controllers[i];
                    var drawSuspended = s.WasPaused || s.DsState.State.Suspended || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking || !s.WarmedUp;

                    if (drawSuspended) continue;

                    if (s.DsState.State.Online)
                    {
                        if (SphereOnCamera[i]) s.Draw(OnCount, SphereOnCamera[i]);
                        else if (s.Icosphere.ImpactsFinished)
                        {
                            if (s.WorldImpactPosition != Vector3D.NegativeInfinity)
                            {
                                s.Draw(OnCount, true);
                                s.Icosphere.ImpactPosState = Vector3D.NegativeInfinity;
                            }
                        }
                        else s.Icosphere.StepEffects();
                    }
                    else if (s.IsWorking && SphereOnCamera[i]) s.DrawShieldDownIcon();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();

                if (!ThreadEvents.IsEmpty)
                {
                    IThreadEvent tEvent;
                    while (ThreadEvents.TryDequeue(out tEvent)) tEvent.Execute();
                }

                LogicUpdates();

                if (EmpStore.Count != 0 && !EmpDispatched)
                {
                    EmpDispatched = true;   
                    PrepEmpBlast();
                    if (EmpWork.EventRunning) MyAPIGateway.Parallel.Start(ComputeEmpBlast, EmpCallBack);
                    else EmpDispatched = false;
                }

                if (_warEffect && Tick20) WarEffect();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            _autoResetEvent.Set();
        }
        #endregion

        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Monitor = false;
            Instance = null;
            HudComp = null;
            Enforced = null;
            _autoResetEvent.Set();
            _autoResetEvent = null;

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PACKET_ID, ReceivedPacket);

            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;

            MyEntities.OnEntityRemove -= OnEntityRemove;

            if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControls;

            //Terminate();
            Log.Line("Logging stopped.");
            Log.Close();
        }
        #endregion

        private void Timings()
        {
            _newFrame = true;
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;

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
                    if (!IsServer) Players.TryAdd(MyAPIGateway.Session.Player.IdentityId, MyAPIGateway.Session.Player);
                }
                GameLoaded = true;
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

        #region EMP
        private void PrepEmpBlast()
        {
            var stackCount = 0;
            var warHeadSize = 0;
            var warHeadYield = 0d;
            var epiCenter = Vector3D.Zero;

            WarHeadBlast empChild;
            while (EmpStore.TryDequeue(out empChild))
            {
                if (empChild.CustomData.Contains("@EMP"))
                {
                    stackCount++;
                    warHeadSize = empChild.WarSize;
                    warHeadYield = empChild.Yield;
                    epiCenter += empChild.Position;
                }
            }

            if (stackCount == 0)
            {
                EmpWork.EventComplete();
                return;
            }
            epiCenter /= stackCount;
            var rangeCap = MathHelper.Clamp(stackCount * warHeadYield, warHeadYield, SyncDist);

            _warHeadGridHits.Clear();
            _pruneWarGrids.Clear();

            var sphere = new BoundingSphereD(epiCenter, rangeCap);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, _pruneWarGrids);

            foreach (var ent in _pruneWarGrids)
            {
                var grid = ent as MyCubeGrid;
                if (grid != null)
                {
                    ShieldGridComponent sComp;
                    grid.Components.TryGet(out sComp);
                    if (sComp?.DefenseShields != null && sComp.DefenseShields.WasOnline) continue;

                    var gridCenter = grid.PositionComp.WorldVolume.Center;
                    var testDir = Vector3D.Normalize(gridCenter - epiCenter);
                    var impactPos = gridCenter + (testDir * -grid.PositionComp.WorldVolume.Radius);

                    IHitInfo hitInfo;
                    MyAPIGateway.Physics.CastRay(epiCenter, impactPos, out hitInfo, CollisionLayers.DefaultCollisionLayer);
                    if (hitInfo?.HitEntity == null) _warHeadGridHits.Add(grid);
                }
            }

            EmpWork.StoreEmpBlast(epiCenter, warHeadSize, warHeadYield, stackCount, rangeCap);
        }

        private void ComputeEmpBlast()
        {
            var epiCenter = EmpWork.EpiCenter;
            var rangeCap = EmpWork.RangeCap;
            var dirYield = EmpWork.DirYield;
            const double BlockInflate = 1.25;

            GetFilteredItems(epiCenter, rangeCap, dirYield);

            foreach (var cube in _warHeadCubeHits)
            {
                WarHeadHit warHit;
                var foundSphere = _warHeadGridShapes.TryGetValue(cube.CubeGrid, out warHit);
                if (foundSphere && warHit.Sphere.Contains(cube.PositionComp.WorldAABB.Center) != ContainmentType.Disjoint)
                {
                    var clearance = cube.CubeGrid.GridSize * BlockInflate;
                    var testDir = Vector3D.Normalize(epiCenter - cube.PositionComp.WorldAABB.Center);
                    var testPos = cube.PositionComp.WorldAABB.Center + (testDir * clearance);
                    var hit = cube.CubeGrid.RayCastBlocks(epiCenter, testPos);

                    if (hit == null)
                    {
                        BlockState blockState;
                        uint endTick;

                        var cubeId = cube.EntityId;
                        var oldState = _warEffectCubes.TryGetValue(cubeId, out blockState);

                        if (oldState) endTick = blockState.Endtick + (Tick + (warHit.Duration + 1));
                        else endTick = Tick + (warHit.Duration + 1);
                        var startTick = (((Tick + 1) / 20) * 20) + 20;

                        _warEffectCubes[cube.EntityId] = new BlockState(cube, startTick, endTick);
                    }
                    else if (cube.SlimBlock == cube.CubeGrid.GetCubeBlock(hit.Value))
                    {
                        BlockState blockState;
                        uint endTick;

                        var cubeId = cube.EntityId;
                        var oldState = _warEffectCubes.TryGetValue(cubeId, out blockState);

                        if (oldState) endTick = blockState.Endtick + (Tick + (warHit.Duration + 1));
                        else endTick = Tick + (warHit.Duration + 1);
                        var startTick = (((Tick + 1) / 20) * 20) + 20;

                        _warEffectCubes[cube.EntityId] = new BlockState(cube, startTick, endTick);
                    }
                }
            }
            EmpWork.ComputeComplete();
        }

        private void GetFilteredItems(Vector3D epiCenter, double rangeCap, double dirYield)
        {
            _warHeadCubeHits.Clear();
            _warHeadGridShapes.Clear();
            var myCubeList = new List<MyEntity>();
            foreach (var grid in _warHeadGridHits)
            {
                var invSqrDist = UtilsStatic.InverseSqrDist(epiCenter, grid.PositionComp.WorldAABB.Center, rangeCap);
                var damage = (uint)(dirYield * invSqrDist);
                var gridAabb = grid.PositionComp.WorldAABB;
                var sphere = CustomCollision.NewObbClosestTriCorners(grid, epiCenter);

                grid.Hierarchy.QueryAABB(ref gridAabb, myCubeList);
                _warHeadGridShapes.Add(grid, new WarHeadHit(sphere, damage));
            }

            for (int i = 0; i < myCubeList.Count; i++)
            {
                var myEntity = myCubeList[i];
                var myCube = myEntity as MyCubeBlock;

                if (myCube == null || myCube.MarkedForClose) continue;
                if ((myCube is IMyThrust || myCube is IMyUserControllableGun || myCube is IMyUpgradeModule) && myCube.IsFunctional && myCube.IsWorking)
                {
                    _warHeadCubeHits.Add(myCube);
                }
            }
            if (Enforced.Debug >= 2) Log.Line($"[ComputeEmpBlast] AllFat:{myCubeList.Count} - TrimmedFat:{_warHeadCubeHits.Count}");
        }

        private void EmpCallBack()
        {
            if (!DedicatedServer) EmpDrawExplosion();
            EmpDispatched = false;
            if (!_warEffectCubes.IsEmpty) _warEffect = true;
        }

        private void EmpDrawExplosion()
        {
            _effect?.Stop();
            var epiCenter = EmpWork.EpiCenter;
            var rangeCap = EmpWork.RangeCap;
            var radius = (float)(rangeCap * 0.01);
            var scale = 7f;

            if (radius < 7) scale = radius;

            var matrix = MatrixD.CreateTranslation(epiCenter);
            MyParticlesManager.TryCreateParticleEffect(6666, out _effect, ref matrix, ref epiCenter, 0, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect == null)
            {
                EmpWork.EmpDrawComplete();
                return;
            }

            if (Enforced.Debug >= 2) Log.Line($"[EmpDraw] scale:{scale} - radius:{radius} - rangeCap:{rangeCap}");

            _effect.UserRadiusMultiplier = radius;
            _effect.UserEmitterScale = scale;
            _effect.UserColorMultiplier = new Vector4(255, 255, 255, 10);
            _effect.Play();
            EmpWork.EmpDrawComplete();
        }

        private void WarEffect()
        {
            foreach (var item in _warEffectCubes)
            {

                var cubeid = item.Key;
                var blockInfo = item.Value;
                var startTick = blockInfo.StartTick;
                var tick = Tick;

                var functBlock = blockInfo.FunctBlock;
                if (functBlock == null || functBlock.MarkedForClose)
                {
                    _warEffectPurge.Enqueue(cubeid);
                    continue;
                }

                if (tick <= startTick)
                {
                    if (tick < startTick) continue;
                    functBlock.Enabled = false;
                    functBlock.EnabledChanged += ForceDisable;
                }

                if (tick < blockInfo.Endtick)
                {
                    if (Tick60) functBlock.SetDamageEffect(true);
                }
                else
                {
                    functBlock.EnabledChanged -= ForceDisable;
                    functBlock.Enabled = blockInfo.EnableState;
                    functBlock.SetDamageEffect(false);
                    _warEffectPurge.Enqueue(cubeid);
                }
            }

            while (_warEffectPurge.Count != 0)
            {
                BlockState value;
                _warEffectCubes.TryRemove(_warEffectPurge.Dequeue(), out value);
            }

            if (_warEffectCubes.IsEmpty) _warEffect = false;
        }

        private void ForceDisable(IMyTerminalBlock myTerminalBlock)
        {
            ((IMyFunctionalBlock)myTerminalBlock).Enabled = false;
        }
        #endregion

        #region Events
        private void OnEntityRemove(MyEntity myEntity)
        {
            var warhead = myEntity as IMyWarhead;
            if (warhead != null)
            {
                if (warhead.IsWorking && !warhead.IsFunctional && (warhead.IsArmed || (warhead.DetonationTime <= 0 && warhead.IsCountingDown)) && warhead.CustomData.Length != 0)
                {
                    var blastRatio = warhead.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 1 : 5;
                    var epicCenter = warhead.PositionComp.WorldAABB.Center;

                    if (Enforced.Debug >= 2 && EmpStore.Count == 0) Log.Line($"====================================================================== [WarHead EventStart]");
                    EmpStore.Enqueue(new WarHeadBlast(blastRatio, epicCenter, warhead.CustomData));
                }
            }
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id))
                {
                    if (Enforced.Debug >= 3) Log.Line($"Player id({id}) already exists");
                    return;
                }
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                if (Enforced.Debug >= 3) Log.Line($"Removed player, new playerCount:{Players.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                if (Enforced.Debug >= 3) Log.Line($"Added player: {player.DisplayName}, new playerCount:{Players.Count}");
            }
            return false;
        }
        #endregion
    }
}

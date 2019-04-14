namespace DefenseSystems
{
    using System;
    using Support;
    using Sandbox.Definitions;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRageMath;
    using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, int.MinValue)]
    public partial class Session : MySessionComponentBase
    {
        #region BeforeStart
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
                //MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(int.MaxValue, AfterDamage);

                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, ReceivedPacket);

                //if (!DedicatedServer && IsServer) Players.TryAdd(MyAPIGateway.Session.Player.IdentityId, MyAPIGateway.Session.Player);
                if (!DedicatedServer && IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);
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

                if (!IsServer) RequestEnforcement(MyAPIGateway.Multiplayer.MyId);
                foreach (var mod in MyAPIGateway.Session.Mods)
                    if (mod.PublishedFileId == 540003236) ThyaImages = true;
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }
        #endregion

        #region Draw
        public override void Draw()
        {
            if (DedicatedServer) return;
            try
            {
                var compCount = AllControllers.Count;
                if (compCount == 0) return;

                if (SphereOnCamera.Length != compCount) Array.Resize(ref SphereOnCamera, compCount);

                if (_count == 0 && _lCount == 0) OnCountThrottle = false;
                var onCount = 0;
                for (int i = 0; i < compCount; i++)
                {
                    var c = AllControllers[i];
                    if (c.State.Value.Suspended || c.Bus?.Field == null) continue;
                    var b = c.Bus;
                    var f = b.Field;

                    if (f.KineticCoolDown > -1)
                    {
                        f.KineticCoolDown++;
                        if (f.KineticCoolDown == 6) f.KineticCoolDown = -1;
                    }

                    if (f.EnergyCoolDown > -1)
                    {
                        f.EnergyCoolDown++;
                        if (f.EnergyCoolDown == 9) f.EnergyCoolDown = -1;
                    }

                    if (!c.WarmedUp || c.State.Value.Lowered || c.State.Value.Sleeping || c.State.Value.Suspended || !c.State.Value.EmitterLos) continue;

                    var sp = new BoundingSphereD(f.DetectionCenter, f.BoundingRange);
                    if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp))
                    {
                        SphereOnCamera[i] = false;
                        continue;
                    }
                    SphereOnCamera[i] = true;
                    if (!f.Icosphere.ImpactsFinished) onCount++;
                }

                if (onCount >= OnCount)
                {
                    OnCount = onCount;
                    OnCountThrottle = true;
                }
                else if (!OnCountThrottle && _count == 59 && _lCount == 9) OnCount = onCount;

                for (int i = 0; i < compCount; i++)
                {
                    var c = AllControllers[i];
                    if (c.Bus?.Field == null) continue;
                    var b = c.Bus;
                    var f = b.Field;
                    var drawSuspended = !c.WarmedUp || c.State.Value.Lowered || c.State.Value.Sleeping || c.State.Value.Suspended || !c.State.Value.EmitterLos;

                    if (drawSuspended) continue;

                    if (c.State.Value.Online)
                    {
                        if (SphereOnCamera[i]) f.Draw(OnCount, SphereOnCamera[i]);
                        else if (f.Icosphere.ImpactsFinished)
                        {
                            if (f.WorldImpactPosition != Vector3D.NegativeInfinity)
                            {
                                f.Draw(OnCount, true);
                                f.Icosphere.ImpactPosState = Vector3D.NegativeInfinity;
                            }
                        }
                        else f.Icosphere.StepEffects();
                    }
                    else if (c.WarmedUp && SphereOnCamera[i]) f.DrawShieldDownIcon();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                Timings();
                if (!ThreadEvents.IsEmpty)
                {
                    if (LogStats) Perf.ThreadEvents(ThreadEvents.Count);
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
            lock (ActiveProtection)
                foreach (var c in ActiveProtection)
                {
                    var b = c.Bus;
                    var f = b.Field;
                    if (f.ShieldIsMobile && !c.Asleep && c.State.Value.ProtectMode != 2) f.MobileUpdate();
                }
            _autoResetEvent.Set();
        }
        #endregion

        #region Data
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
    }
}

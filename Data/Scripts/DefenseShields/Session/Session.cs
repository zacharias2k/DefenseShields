using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using DefenseShields.Support;
using Sandbox.Definitions;
using VRageMath;
using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

namespace DefenseShields
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public partial class Session : MySessionComponentBase
    {
        #region Simulation / Init
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
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEnforce, EnforcementReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdControllerState, ControllerStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdControllerSettings, ControllerSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdModulatorSettings, ModulatorSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdModulatorState, ModulatorStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEnhancerState, EnhancerStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdO2GeneratorState, O2GeneratorStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEmitterState, EmitterStateReceived);

                if (!MpActive)
                {
                    Players.Add(MyAPIGateway.Session.Player);
                    Characters.Add(MyAPIGateway.Session.Player.Character);
                }
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;


                if (!DedicatedServer)
                {
                    MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
                }

                if (IsServer)
                {
                    Log.Line($"LoadConf - Session: This is a server");
                    UtilsStatic.PrepConfigFile();
                    UtilsStatic.ReadConfigFile();
                }

                _syncDistSqr = MyAPIGateway.Session.SessionSettings.SyncDistance;
                _syncDistSqr += 500;
                _syncDistSqr *= _syncDistSqr;

                if (Enforced.Debug >= 1) Log.Line($"SyncDistSqr:{_syncDistSqr} - DistNorm:{Math.Sqrt(_syncDistSqr)}");
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
                if (Controllers.Count == 0) return;
                if (_count == 0 && _lCount == 0) OnCountThrottle = false;
                var onCount = 0;
                for (int i = 0; i < Controllers.Count; i++)
                {
                    var s = Controllers[i];
                    if (s.BulletCoolDown > -1)
                    {
                        s.BulletCoolDown++;
                        if (s.BulletCoolDown == 9) s.BulletCoolDown = -1;
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

                for (int i = 0; i < Controllers.Count; i++)
                {
                    var s = Controllers[i];
                    if (!s.WarmedUp || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking) continue;
                    if (s.DsState.State.Online && SphereOnCamera[i]) s.Draw(OnCount, SphereOnCamera[i]);
                    else
                    {
                        if (s.DsState.State.Online)
                        {
                            if (!s.Icosphere.ImpactsFinished) s.Icosphere.StepEffects();
                        }
                        else if (s.IsWorking && SphereOnCamera[i]) s.DrawShieldDownIcon();
                    }
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
                LoadBalancer();
                LogicUpdates();
                Timings();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            if (Shields.Count > 0) MyAPIGateway.Parallel.StartBackground(WebMonitor);
        }
        #endregion

        #region Events
        private void PlayerConnected(long l)
        {
            var ent = MyAPIGateway.Entities.GetEntityById(l);
            var character = ent as IMyCharacter;
            var player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
            if (character != null) Characters.Add(character);
            if (player != null) Players.Add(player);
        }

        private void PlayerDisconnected(long l)
        {
            var ent = MyAPIGateway.Entities.GetEntityById(l);
            var character = ent as IMyCharacter;
            var player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
            if (character != null) Characters.Add(character);
            if (player != null) Players.Remove(player);
        }
        #endregion

        #region Misc
        private void Timings()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    MoreThan600Frames = true;
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10)
                    {
                        _eCount = 0;
                    }
                }
            }
            if (!DefinitionsLoaded && Tick > 100)
            {
                DefinitionsLoaded = true;
                UtilsStatic.GetDefinitons();
            }
        }

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
            Instance = null;
            HudComp = null;
            Enforced = null;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEnforce, EnforcementReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdControllerState, ControllerStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdControllerSettings, ControllerSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdModulatorSettings, ModulatorSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdModulatorState, ModulatorStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEnhancerState, EnhancerStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdO2GeneratorState, O2GeneratorStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEmitterState, EmitterStateReceived);

            MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;

            if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControls;

            //Terminate();
            Log.Line("Logging stopped.");
            Log.Close();
        }
        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        internal bool SessionInit;
        public bool ControlsLoaded { get; set; }
        public bool Enabled = true;
        internal bool CustomDataReset = true;
        internal MyStringId Password = MyStringId.GetOrCompute("Password");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Set the shield modulation password");

        public static readonly bool MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
        public static readonly bool IsServer = MyAPIGateway.Multiplayer.IsServer;
        public static readonly bool DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

        private int _count = -1;
        private int _longLoop;
        private int _extendedLoop;

        public const ushort PACKET_ID_SETTINGS = 62520; // network
        public const ushort PACKET_ID_ENFORCE = 62521; // network
        public const ushort PACKET_ID_MODULATOR = 62522; // network
        private const long WORKSHOP_ID = 1365616918;
        public readonly Guid SettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");
        public readonly Guid ModulatorGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811509");

        public string disabledBy = null;

        public static Session Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(5);
        private DSUtils _dsutil1 = new DSUtils();

        private readonly Dictionary<IMyEntity, int> _voxelDamageCounter = new Dictionary<IMyEntity, int>();
        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public readonly List<Modulators> Modulators = new List<Modulators>();

        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public static DefenseShieldsEnforcement Enforced = new DefenseShieldsEnforcement();


        public void Init()
        {
            try
            {
                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started");
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID_SETTINGS, PacketSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID_ENFORCE, PacketEnforcementReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID_MODULATOR, ModulatorSettingsReceived);
                MyAPIGateway.Utilities.RegisterMessageHandler(WORKSHOP_ID, ModMessageHandler);
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomDataToPassword;

                if (DedicatedServer || IsServer)
                {
                    Log.Line($"This is a server, loading config");
                    DsUtilsStatic.PrepConfigFile();
                    DsUtilsStatic.ReadConfigFile();
                }
                SessionInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in SessionInit: {ex}"); }
        }

        public override void Draw()
        {
            if (DedicatedServer) return;
            if (Enforced.Debug == 1 && _extendedLoop == 0 & _longLoop == 0 && _count == 0) Log.Line($"Shields in the world: {Components.Count.ToString()}");
            try
            {
                if (!SessionInit || Components.Count == 0) return;
                var sphereOnCamera = new bool[Components.Count];
                var onCount = 0;
                for (int i = 0; i < Components.Count; i++)
                {
                    var s = Components[i];
                    if (s.HardDisable || !s.ShieldActive || !(s.AnimateInit && s.MainInit)) continue;
                    var sp = new BoundingSphereD(s.Entity.GetPosition(), s.Range);
                    if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp)) continue;
                    sphereOnCamera[i] = true;
                    onCount++;
                }
                for (int i = 0; i < Components.Count; i++) if (Components[i].ShieldActive && !Components[i].HardDisable) Components[i].Draw(onCount, sphereOnCamera[i]);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                for (int i = 0; i < Components.Count; i++) Components[i].DeformEnabled = false;

                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10)
                    {
                        _longLoop = 0;
                        _extendedLoop++;
                        if (_extendedLoop == 10) _extendedLoop = 0;
                    }
                }

                if (!SessionInit)
                {
                    if (IsServer && DedicatedServer) Init();
                    else if (MyAPIGateway.Session.Player != null) Init();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                if (Components.Count == 0 || info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind || info.Type == MyDamageType.Environment) return;

                var player = target as IMyCharacter;
                if (player != null)
                {
                    foreach (var shield in Components)
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);

                        if (shield.ShieldActive 
                            && shield.FriendlyCache.Contains(player) 
                            && (hostileEnt == null  || !shield.FriendlyCache.Contains(hostileEnt))) info.Amount = 0f;
                    }
                    return;
                }

                var block = target as IMySlimBlock;
                if (block == null) return;
                var blockGrid = (MyCubeGrid)block.CubeGrid;

                foreach (var shield in Components)
                {
                    if (shield.ShieldActive && (shield.Shield.CubeGrid == blockGrid || shield.FriendlyCache.Contains(blockGrid)))
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (shield.FriendlyCache.Contains(hostileEnt) || hostileEnt == shield.Shield.CubeGrid)
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        if (hostileEnt != null && block.FatBlock == shield.Shield && (info.Type.String.Equals("DSdamage") || info.Type.String.Equals("DSheal") || info.Type.String.Equals("DSbypass")))
                        {
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            shield.WorldImpactPosition = shield._shield.Render.ColorMaskHsv;
                            continue;
                        }

                        if (shield.DeformEnabled) continue;

                        if (hostileEnt != null && shield.Absorb < 1 && shield.BulletCoolDown == -1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity)
                        {
                            Vector3D blockPos;
                            block.ComputeWorldCenter(out blockPos);
                            var vertPos = CustomCollision.ClosestVert(shield.PhysicsOutside, blockPos);
                            shield.WorldImpactPosition = vertPos;
                            shield.ImpactSize = 5;
                        }

                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }

        #region Network sync
        private static void PacketSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<PacketData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.SETTINGS:
                        {
                            if (data.Settings == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; settings are null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"Packet Settings Packet received:- data:\n{data.Settings}");
                            data.Settings.Buffer = logic.ShieldBuffer;
                            logic.UpdateSettings(data.Settings);
                            logic.SaveSettings();
                            logic.ServerUpdate = true;

                            if (IsServer)
                                ShieldSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketSettingsReceived: {ex}"); }
        }

        private static void PacketEnforcementReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<EnforceData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.ENFORCE:
                        {
                            if (data.Enforce == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; Enforce is null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"PacketReceived(); Enforce - Server:\n{data.Enforce}");
                            if (!(DedicatedServer || IsServer))
                            {
                                logic.UpdateEnforcement(data.Enforce);
                                logic.SaveSettings();
                                logic.EnforceUpdate = true;
                            }

                            if (DedicatedServer || IsServer)
                            {
                                PacketizeEnforcements(logic.Shield);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketEnforcementReceived: {ex}"); }
        }

        private static void ModulatorSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<ModulatorData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.MODULATOR:
                        {
                            if (data.Settings == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; settings are null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"Packet received:\n{data.Settings}");
                            logic.UpdateSettings(data.Settings);
                            logic.SaveSettings();
                            logic.ServerUpdate = true;

                            if (IsServer)
                                ModulatorSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorSettingsReceived: {ex}"); }
        }

        public static void PacketizeEnforcements(IMyCubeBlock block)
        {
            var data = new EnforceData(MyAPIGateway.Multiplayer.MyId, block.EntityId, Enforced);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ClientEnforcement(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeModulatorSettings(IMyCubeBlock block, ModulatorSettings settings)
        {
            var data = new ModulatorData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeShieldSettings(IMyCubeBlock block, DefenseShieldsModSettings settings)
        {
            var data = new PacketData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ShieldSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void ShieldSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 1000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID_SETTINGS, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ClientEnforcement(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 1000; 
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && (Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq) || id == sender)
                    MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID_ENFORCE, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ModulatorSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 1000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID_MODULATOR, bytes, p.SteamUserId);
            }
            players.Clear();
        }
        #endregion

        private void ModMessageHandler(object obj)
        {
            try
            {
                if (obj is MyTuple<bool, string>)
                {
                    var data = (MyTuple<bool, string>)obj;
                    Enabled = data.Item1;

                    if (Enabled)
                    {
                        disabledBy = null;
                    }
                    else
                    {
                        disabledBy = data.Item2;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModMessageHandler: {ex}"); }
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void CustomDataToPassword(IMyTerminalBlock block, List<IMyTerminalControl> myTerminalControls)
        {
            try
            {
                if (block.BlockDefinition.SubtypeId == "LargeShieldModulator"  || block.BlockDefinition.SubtypeId == "SmallShieldModulator" 
                    || block.BlockDefinition.SubtypeId == "DefenseShieldsST" || block.BlockDefinition.SubtypeId == "DefenseShieldsSS" 
                    || block.BlockDefinition.SubtypeId == "DefenseShieldsLS")
                    SetCustomDataToPassword(myTerminalControls);
                else if (!CustomDataReset) ResetCustomData(myTerminalControls);
            }
            catch (Exception ex) { Log.Line($"Exception in CustomDataToPassword: {ex}"); }
        }

        private void SetCustomDataToPassword(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = Password;
            ((IMyTerminalControlTitleTooltip)customData).Tooltip = PasswordTooltip;
            customData.RedrawControl();
            CustomDataReset = false;
        }

        private void ResetCustomData(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = MySpaceTexts.Terminal_CustomData;
            ((IMyTerminalControlTitleTooltip)customData).Tooltip = MySpaceTexts.Terminal_CustomDataTooltip;
            customData.RedrawControl();
            CustomDataReset = true;
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
    #endregion
}

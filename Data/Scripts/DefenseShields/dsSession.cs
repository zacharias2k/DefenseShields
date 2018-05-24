using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public static float Nerf;
        public static int BaseScaler;

        internal bool SessionInit;
        public bool ControlsLoaded { get; set; }
        private bool _resetVoxelColliders;
        public bool Enabled = true;

        private int _voxelTrigger;
        private int _count;

        public const ushort PACKET_ID_SETTINGS = 62520; // network
        public const ushort PACKET_ID_ENFORCE = 62521; // network

        private const long WORKSHOP_ID = 1365616918;
        public string disabledBy = null;

        public readonly Guid SettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");

        public static DefenseShieldsBase Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(5);
        private DSUtils _dsutil1 = new DSUtils();
        private DSUtils _dsutil2 = new DSUtils();
        private DSUtils _dsutil3 = new DSUtils();

        private readonly Dictionary<IMyEntity, int> _voxelDamageCounter = new Dictionary<IMyEntity, int>();
        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();


        public void Init()
        {
            try
            {
                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started");
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID_SETTINGS, PacketSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID_ENFORCE, PacketNerfReceived);
                MyAPIGateway.Utilities.RegisterMessageHandler(WORKSHOP_ID, ModMessageHandler);
                var nerfExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShieldsNerf.cfg");
                var baseScaleExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShieldsBaseScale.cfg");

                if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Multiplayer.IsServer)
                {
                    if (nerfExists)
                    {
                        var getNerf = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShieldsNerf.cfg");
                        float nerf;
                        float.TryParse(getNerf.ReadToEnd(), out nerf);
                        DefenseShields.ServerEnforcedValues.Nerf = nerf;
                        Nerf = nerf;
                        Log.Line($"writting nerf to Values: {nerf} - {DefenseShields.ServerEnforcedValues.Nerf}");
                    }
                    else
                    {
                        var noNerf = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShieldsNerf.cfg");
                        noNerf.Write("00.0000");
                        noNerf.Close();
                        var getNerf = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShieldsNerf.cfg");
                        float nerf;
                        float.TryParse(getNerf.ReadToEnd(), out nerf);
                        DefenseShields.ServerEnforcedValues.Nerf = nerf;
                        Nerf = nerf;
                        Log.Line($"writting nerf to Values: {nerf} - {DefenseShields.ServerEnforcedValues.Nerf}");

                    }

                    if (baseScaleExists)
                    {
                        var getBaseScaler = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShieldsBaseScale.cfg");
                        int baseScale;
                        int.TryParse(getBaseScaler.ReadToEnd(), out baseScale);
                        DefenseShields.ServerEnforcedValues.BaseScaler = baseScale;
                        BaseScaler = baseScale;
                    }
                    else
                    {
                        var noBaseScaler = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShieldsBaseScale.cfg");
                        noBaseScaler.Write("30");
                        noBaseScaler.Close();
                        var getBaseScaler = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShieldsBaseScale.cfg");
                        int baseScale;
                        int.TryParse(getBaseScaler.ReadToEnd(), out baseScale);
                        DefenseShields.ServerEnforcedValues.BaseScaler = baseScale;
                        BaseScaler = baseScale;
                    }
                }
                SessionInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in SessionInit: {ex}"); }
        }

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
                        Log.Line($"Wing logic turned off by mod {data.Item2}");
                        disabledBy = null;
                    }
                    else
                    {
                        Log.Line($"Wing logic turned off by mod {data.Item2}.");
                        disabledBy = data.Item2;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModMessageHandler: {ex}"); }
        }

        public override void Draw()
        {
            //_dsutil1.Sw.Start();
            if (MyAPIGateway.Utilities.IsDedicated) return;
            if (_count == 0) Log.Line($"Shields in the world: {Components.Count.ToString()}");
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
                //_dsutil1.StopWatchReport("draw", -1);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
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
            Log.Line("Logging stopped.");
            Log.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!SessionInit)
                {
                    if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
                    else if (MyAPIGateway.Session.Player != null) Init();
                }
                else
                {
                    if (_count++ == 3600)
                    {
                        _count = 0;
                        if (_voxelDamageCounter.Count != 0) _voxelDamageCounter.Clear();
                    }
                    _voxelTrigger = 0;
                    _resetVoxelColliders = false;
                    foreach (var voxel in _voxelDamageCounter.Values)
                        if (voxel > 40) _resetVoxelColliders = true;
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

                        if (hostileEnt != null && (hostileEnt == shield.Shield.CubeGrid) || shield.FriendlyCache.Contains(hostileEnt))
                        {
                            continue;
                        }

                        if ((hostileEnt is MyVoxelBase) && _voxelTrigger == 0 && (!MyAPIGateway.Utilities.IsDedicated))
                        {
                            var voxel = (MyVoxelBase)hostileEnt;
                            info.Amount = 0f;

                            if (_resetVoxelColliders)
                            {
                                var safeplace = MyAPIGateway.Entities.FindFreePlace(shield.Shield.CubeGrid.WorldVolume.Center, (float)shield.Shield.CubeGrid.WorldVolume.Radius * 5);
                                if (safeplace != null)
                                {
                                    shield.Shield.CubeGrid.Physics.ClearSpeed();
                                    shield.Shield.CubeGrid.SetPosition((Vector3D)safeplace);
                                    _voxelDamageCounter.Clear();
                                }
                            }
                            if (!_voxelDamageCounter.ContainsKey(voxel)) _voxelDamageCounter.Add(voxel, 1);
                            else _voxelDamageCounter[voxel]++;
                            _voxelTrigger = 1;
                        }

                        if (info.Type == MyDamageType.Deformation)
                        {
                            info.Amount = 0f;
                            info.IsDeformation = false;
                            continue;
                        }

                        if (hostileEnt == null && info.Type == MyDamageType.Explosion)
                        {
                            info.Amount = 0f;
                            continue;
                        }

                        if (info.Type.String.Equals("DSdamage") || info.Type.String.Equals("DSheal") || info.Type.String.Equals("DSbypass"))
                        {
                            //Log.Line($"Amount:{info.Amount.ToString()} - Type:{info.Type.ToString()} - Block:{block.BlockDefinition.GetType().Name} - Attacker:{hostileEnt?.DebugName}");
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            shield.WorldImpactPosition = shield._shield.Render.ColorMaskHsv;
                            continue;
                        }

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

                            //Log.Line($"PacketReceived(); Settings; {(MyAPIGateway.Multiplayer.IsServer ? " Relaying to clients;" : "")} Valid!\n{logic.Settings}");

                            //if (MyAPIGateway.Utilities.IsDedicated) data.Settings.Nerf = ForceServerNerf;
                            Log.Line($"Packet received: {MyAPIGateway.Multiplayer.IsServer} - data:{data.Settings.Nerf} - {data.Settings.BaseScaler}");
                            logic.UpdateSettings(data.Settings);
                            logic.SaveSettings();
                            logic.ServerUpdate = true;

                            if (MyAPIGateway.Multiplayer.IsServer)
                                RelayToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Line($"Invalid packet data!{e}");
            }
        }

        private static void PacketNerfReceived(byte[] bytes)
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

                            Log.Line($"PacketReceived(); Enforce - Server: {MyAPIGateway.Multiplayer.IsServer} Valid packet!\n{DefenseShields.ServerEnforcedValues}");
                            if (!(MyAPIGateway.Utilities.IsDedicated))
                            {
                                Log.Line($"Client updating and saving enforcement");
                                logic.UpdateEnforcement(data.Enforce);
                                logic.SaveSettings();
                                logic.EnforceUpdate = true;
                            }

                            if (MyAPIGateway.Utilities.IsDedicated)
                            {
                                RelayEnforcementToClients(logic.Shield);
                            }
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Line($"Invalid packet data!{e}");
            }
        }

        public static void RelayEnforcementToClients(IMyCubeBlock block)
        {
            Log.Line($"RelayEnforcementToClients - Nerf:{DefenseShields.ServerEnforcedValues.Nerf} - Base:{DefenseShields.ServerEnforcedValues.BaseScaler}");
            var data = new EnforceData(MyAPIGateway.Multiplayer.MyId, block.EntityId, DefenseShields.ServerEnforcedValues);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ForceClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void RelaySettingsToClients(IMyCubeBlock block, DefenseShieldsModSettings settings)
        {
            //Log.Line("RelaySettingsToClients");

            var data = new PacketData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            RelayToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void RelayToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            //Log.Line("RelayToClients");

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

        public static void ForceClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            Log.Line("Forcing clients");
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

                if (id != localSteamId && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID_ENFORCE, bytes, p.SteamUserId);
            }
            players.Clear();
        }
        #endregion
    }
    #endregion
}

using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public bool IsInit;
        public bool ControlsLoaded;

        public const ushort PACKET_ID = 62520; // network
        public readonly Guid SETTINGS_GUID = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");

        public static DefenseShieldsBase Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(6);
        private DSUtils _dsutil1 = new DSUtils();
        private DSUtils _dsutil2 = new DSUtils();
        private DSUtils _dsutil3 = new DSUtils();

        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        //public List<DefenseShields> Shields = new List<DefenseShields>();

        public override void Draw()
        {
            //_dsutil1.Sw.Start();
            var sphereOnCamera = new bool[Components.Count];
            var onCount = 0;
            for (int i = 0; i < Components.Count; i++)
            {
                var s = Components[i];
                var sp = new BoundingSphereD(s.Entity.GetPosition(), s._range);
                if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp)) continue;
                sphereOnCamera[i] = true;
                onCount++;
            }
            for (int i = 0; i < Components.Count; i++) Components[i].Draw(onCount, sphereOnCamera[i]);
            //_dsutil1.StopWatchReport("draw", -1);

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
            if (IsInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public void Init() 
        {
            Log.Init("debugdevelop.log");
            Log.Line($" Logging Started");
            //MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, PacketReceived);
            IsInit = true;
        }

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            var block = target as IMySlimBlock;
            if (block == null) return;

            if (Components.Count == 0 || (info.Type != MyDamageType.Bullet && info.Type != MyDamageType.Deformation)) return;
            foreach (var shield in Components)
            {
                if (!shield.Block.IsWorking || !shield.Initialized) continue;
                if (block.CubeGrid == shield.Block.CubeGrid) info.Amount = 0f;
            }
        }

        #region Network sync
        private static void PacketReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<PacketData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed || !(ent is IMyOreDetector))
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

                            Log.Line($"PacketReceived(); Settings; {(MyAPIGateway.Multiplayer.IsServer ? " Relaying to clients;" : "")}Valid!\n{logic.Settings}");

                            logic.UpdateSettings(data.Settings);
                            logic.SaveSettings();

                            if (MyAPIGateway.Multiplayer.IsServer)
                                RelayToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        //break;
                    //case PacketType.REMOVE:
                        //logic.RemoveBlueprints_Receiver(bytes, data.Sender);
                        //break;
                    //case PacketType.RECEIVED_BP:
                        //logic.PlayerReceivedBP(data.Sender);
                        break;
                    case PacketType.USE_THIS_AS_IS:
                        logic.UseThisShip_Receiver(false);
                        break;
                    case PacketType.USE_THIS_FIX:
                        logic.UseThisShip_Receiver(true);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Line($"Invalid packet data!{e}");
            }
        }

        public static void RelaySettingsToClients(IMyCubeBlock block, DefenseShieldsModSettings settings)
        {
            Log.Line("RelaySettingsToClients(block,settings)");

            var data = new PacketData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            RelayToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void RelayToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            Log.Line("RelayToClients(syncPos,bytes,sender)");

            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.ViewDistance;
            distSq += 1000; // some safety padding
            distSq *= distSq;

            MyAPIGateway.Players.GetPlayers(null, (p) =>
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID, bytes, p.SteamUserId);

                return false; // avoid adding to the null list
            });
        }
        #endregion
    }
    #endregion
}

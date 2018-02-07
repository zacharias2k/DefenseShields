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
        //public int I;

        // test
        public const ushort PACKET_ID = 62520; // network
        public readonly Guid SETTINGS_GUID = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");
        //public readonly Guid BLUEPRINT_GUID = new Guid("E973AD49-F3F4-41B9-811B-2B114E6EE0F9");
        //

        public static DefenseShieldsBase Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(4);

        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public List<DefenseShields> Shields = new List<DefenseShields>(); 

        //private readonly MyStringId _faceId = MyStringId.GetOrCompute("Build new");


        public override void Draw()
        {
            /*
            if (I < 60)
            {
                I++;
                for (var j = 0; j < 32768; j++) MyTransparentGeometry.AddTriangleBillboard(Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, _faceId, 0, Vector3D.Zero);
            }
            */
            foreach (var s in Components)
            {
                s.Draw();
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
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(PACKET_ID, PacketReceived);
            IsInit = true;
        }

        public void CheckDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // fix
            {
            }

            if (Shields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            var generator = Shields[0];
            var ent = block as IMyEntity;
            var slimBlock = block as IMySlimBlock;
            if (slimBlock != null) ent = slimBlock.CubeGrid;
            var dude = block as IMyCharacter;
            if (dude != null) ent = dude;
            if (ent == null) return;
            var isProtected = false;
            foreach (var shield in Shields)
                if (shield.InHash.Contains(ent))
                {
                    isProtected = true;
                    generator = shield;
                }
            if (!isProtected) return;
            IMyEntity attacker;
            if (!MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out attacker)) return;
            if (generator.InHash.Contains(attacker)) return;
            info.Amount = 0f;
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
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed || !(ent is IMyProjector))
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not projector"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; projector doesn't have the gamelogic component!");
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
                        break;
                    case PacketType.REMOVE:
                        logic.RemoveBlueprints_Receiver(bytes, data.Sender);
                        break;
                    case PacketType.RECEIVED_BP:
                        logic.PlayerReceivedBP(data.Sender);
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

        public static void RelaySettingsToClients(IMyCubeBlock block, ProjectorPreviewModSettings settings)
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

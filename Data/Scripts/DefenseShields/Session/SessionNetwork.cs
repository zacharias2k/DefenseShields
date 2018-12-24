using System;
using DefenseShields.Support;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class Session
    {
        #region Network sync
        private static void EnforcementReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEnforce>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"EnforceData Received; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Enforce:
                        {
                            if (data.Enforce == null) return;

                            if (Enforced.Debug >= 3) Log.Line($"EnforceData Received; Enforce - Server:\n{data.Enforce}");
                            if (!MyAPIGateway.Multiplayer.IsServer)
                            {
                                Enforcements.SaveEnforcement(logic.Shield, data.Enforce);
                                EnforceInit = true;
                                if (Enforced.Debug >= 3) Log.Line($"client accepted enforcement");
                                if (Enforced.Debug >= 3) Log.Line($"Client EnforceInit Complete with enforcements:\n{data.Enforce}");
                            }
                            else PacketizeEnforcements(logic.Shield, data.Enforce.SenderId);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketEnforcementReceived: {ex}"); }
        }

        private static void ControllerStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataControllerState>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    if (Enforced.Debug >= 3) Log.Line($"Data State null");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    if (Enforced.Debug >= 3) Log.Line($"State PacketReceived; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null)
                {
                    if (Enforced.Debug >= 3) Log.Line($"Logic State null");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.Controllerstate:
                        {
                            if (data.State == null)
                            {
                                if (Enforced.Debug >= 3) Log.Line($"Packet State null");
                                return;
                            }

                            if (Enforced.Debug >= 5) Log.Line($"Packet State Packet received data:\n{data.State}");

                            if (MyAPIGateway.Multiplayer.IsServer) ControllerStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketStatsReceived: {ex}"); }
        }


        private static void ControllerSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataControllerSettings>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Controler PacketReceived; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Controllersettings:
                        {
                            if (data.Settings == null) return;

                            logic.UpdateSettings(data.Settings);
                            if (MyAPIGateway.Multiplayer.IsServer) ControllerSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            if (Enforced.Debug >= 3) Log.Line($"Packet Settings Packet received:- data:\n{data.Settings}");
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketSettingsReceived: {ex}"); }
        }

        private static void ModulatorSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataModulatorSettings>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Modulator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Modulatorsettings:
                        {
                            if (data.Settings == null) return;

                            logic.UpdateSettings(data.Settings);
                            if (MyAPIGateway.Multiplayer.IsServer) ModulatorSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            if (Enforced.Debug >= 3) Log.Line($"Modulator received:\n{data.Settings}");
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorSettingsReceived: {ex}"); }
        }

        private static void ModulatorStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataModulatorState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Modulator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Modulatorstate:
                        {
                            if (data.State == null) return;

                            if (Enforced.Debug >= 3) Log.Line($"Modulator received:\n{data.State}");

                            if (MyAPIGateway.Multiplayer.IsServer) ModulatorStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorStateReceived: {ex}"); }
        }

        private static void O2GeneratorStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataO2GeneratorState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"O2Generator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<O2Generators>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.O2Generatorstate:
                        {
                            if (data.State == null) return;

                            if (Enforced.Debug >= 3) Log.Line($"O2Generator received:\n{data.State}");

                            if (MyAPIGateway.Multiplayer.IsServer) O2GeneratorStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in O2GeneratorStateReceived: {ex}"); }
        }

        private static void EnhancerStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEnhancerState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Enhancer PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Enhancers>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Enhancerstate:
                        {
                            if (data.State == null) return;

                            if (Enforced.Debug >= 3) Log.Line($"Enhancer received:\n{data.State}");

                            if (MyAPIGateway.Multiplayer.IsServer) EnhancerStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in EnhancerStateReceived: {ex}"); }
        }

        private static void EmitterStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEmitterState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Emitter PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Emitters>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Emitterstate:
                        {
                            if (data.State == null) return;
                            if (Enforced.Debug >= 3) Log.Line($"Emitter received:\n{data.State}");
                            if (MyAPIGateway.Multiplayer.IsServer) EmitterStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in EmitterStateReceived: {ex}"); }
        }

        public static void PacketizeEnforcements(IMyCubeBlock block, ulong senderId)
        {
            var data = new DataEnforce(MyAPIGateway.Multiplayer.MyId, block.EntityId, Enforced);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnforce, bytes, senderId);
        }

        public static void PacketizeControllerState(IMyCubeBlock block, ProtoControllerState state)
        {
            var data = new DataControllerState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ControllerStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeControllerSettings(IMyCubeBlock block, ProtoControllerSettings settings)
        {
            var data = new DataControllerSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ControllerSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeModulatorSettings(IMyCubeBlock block, ProtoModulatorSettings settings)
        {
            var data = new DataModulatorSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeModulatorState(IMyCubeBlock block, ProtoModulatorState state)
        {
            var data = new DataModulatorState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizePlanetShieldSettings(IMyCubeBlock block, ProtoPlanetShieldSettings settings)
        {
            var data = new DataPlanetShieldSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            PlanetShieldSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizePlanetShieldState(IMyCubeBlock block, ProtoPlanetShieldState state)
        {
            var data = new DataPlanetShieldState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            PlanetShieldStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeO2GeneratorState(IMyCubeBlock block, ProtoO2GeneratorState state)
        {
            var data = new DataO2GeneratorState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            O2GeneratorStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeEnhancerState(IMyCubeBlock block, ProtoEnhancerState state)
        {
            var data = new DataEnhancerState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            EnhancerStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeEmitterState(IMyCubeBlock block, ProtoEmitterState state)
        {
            var data = new DataEmitterState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            EmitterStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void ControllerStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerState, bytes, p.SteamUserId);
            }
        }

        public static void ControllerSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerSettings, bytes, p.SteamUserId);
            }
        }

        public static void ModulatorSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorSettings, bytes, p.SteamUserId);
            }
        }

        public static void ModulatorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorState, bytes, p.SteamUserId);
                }
            }
        }

        public static void PlanetShieldSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldSettings, bytes, p.SteamUserId);
            }
        }

        public static void PlanetShieldStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender &&
                    Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldState, bytes, p.SteamUserId);
                }
            }
        }

        public static void O2GeneratorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdO2GeneratorState, bytes, p.SteamUserId);
            }
        }

        public static void EnhancerStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnhancerState, bytes, p.SteamUserId);
            }
        }

        public static void EmitterStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            foreach (var p in Players.Values)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEmitterState, bytes, p.SteamUserId);
            }
        }
        #endregion

    }
}

using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class O2GeneratorState
    {
        internal ProtoO2GeneratorState State = new ProtoO2GeneratorState();
        internal readonly IMyFunctionalBlock O2Generator;
        internal O2GeneratorState(IMyFunctionalBlock o2Generator)
        {
            O2Generator = o2Generator;
        }

        public void StorageInit()
        {
            if (O2Generator.Storage == null)
            {
                O2Generator.Storage = new MyModStorageComponent {[Session.Instance.EmitterStateGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && O2Generator.Storage == null) O2Generator.Storage = new MyModStorageComponent();
            else if (O2Generator.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            O2Generator.Storage[Session.Instance.O2GeneratorStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (O2Generator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (O2Generator.Storage.TryGetValue(Session.Instance.O2GeneratorStateGuid, out rawData))
            {
                ProtoO2GeneratorState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoO2GeneratorState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - EmitterId [{O2Generator.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{O2Generator.EntityId}]: network state update for modulator");
                Session.PacketizeO2GeneratorState(O2Generator, State); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{O2Generator.EntityId}]: sent network state update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataO2GeneratorState(MyAPIGateway.Multiplayer.MyId, O2Generator.EntityId, State));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdO2GeneratorState, bytes);
            }
        }
        #endregion
    }
}

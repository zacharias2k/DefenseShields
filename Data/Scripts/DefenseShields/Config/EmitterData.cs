using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class EmitterState
    {
        internal ProtoEmitterState State = new ProtoEmitterState();
        internal readonly IMyFunctionalBlock Emitter;
        internal EmitterState(IMyFunctionalBlock emitter)
        {
            Emitter = emitter;
        }

        public void StorageInit()
        {
            if (Emitter.Storage == null)
            {
                Emitter.Storage = new MyModStorageComponent {[Session.Instance.EmitterStateGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && Emitter.Storage == null) Emitter.Storage = new MyModStorageComponent();
            else if (Emitter.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Emitter.Storage[Session.Instance.EmitterStateGuid] = Convert.ToBase64String(binary);
        }


        public bool LoadState()
        {
            if (Emitter.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Emitter.Storage.TryGetValue(Session.Instance.EmitterStateGuid, out rawData))
            {
                ProtoEmitterState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoEmitterState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - EmitterId [{Emitter.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Emitter.EntityId}]: network state update for emitter");
                Session.PacketizeEmitterState(Emitter, State); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Emitter.EntityId}]: sent network state update for emitter");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataEmitterState(MyAPIGateway.Multiplayer.MyId, Emitter.EntityId, State));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdEmitterState, bytes);
            }
        }
        #endregion
    }
}

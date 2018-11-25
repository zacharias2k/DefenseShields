using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class EnhancerState
    {
        internal ProtoEnhancerState State = new ProtoEnhancerState();
        internal readonly IMyFunctionalBlock Enhancer;
        internal EnhancerState(IMyFunctionalBlock enhancer)
        {
            Enhancer = enhancer;
        }

        public void StorageInit()
        {
            if (Enhancer.Storage == null)
            {
                Enhancer.Storage = new MyModStorageComponent {[Session.Instance.EnhancerStateGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && Enhancer.Storage == null) Enhancer.Storage = new MyModStorageComponent();
            else if (Enhancer.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Enhancer.Storage[Session.Instance.EnhancerStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Enhancer.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Enhancer.Storage.TryGetValue(Session.Instance.EnhancerStateGuid, out rawData))
            {
                ProtoEnhancerState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoEnhancerState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug >= 2) Log.Line($"Loaded - EnhancerId [{Enhancer.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug >= 2) Log.Line($"ServRelay - EnhancerId [{Enhancer.EntityId}]: network state update for Enhancer");
                Session.PacketizeEnhancerState(Enhancer, State); // update clients with server's settings
            }
        }
        #endregion
    }
}

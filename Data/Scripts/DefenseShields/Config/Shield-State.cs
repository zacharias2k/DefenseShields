using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class ShieldStatus
    {
        internal ShieldState State = new ShieldState();
        internal readonly IMyFunctionalBlock Field;
        internal ShieldStatus(IMyFunctionalBlock field)
        {
            Field = field;
        }

        public void SaveState()
        {
            if (Field.Storage == null) Field.Storage = new MyModStorageComponent();
            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Field.Storage[Session.Instance.ShieldGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Field.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Field.Storage.TryGetValue(Session.Instance.ShieldGuid, out rawData))
            {
                ShieldState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ShieldState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - ShieldId [{Field.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - ShieldId [{Field.EntityId}]: network state update for shield");
            Session.PacketizeState(Field, State); // update clients with server's state
        }
    }
}

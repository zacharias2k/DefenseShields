namespace DefenseSystems
{
    using System;
    using Support;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class ControllerState
    {
        internal readonly IMyFunctionalBlock Controller;

        internal ControllerState(IMyFunctionalBlock controller)
        {
            Controller = controller;
        }

        internal ControllerStateValues Value { get; set; } = new ControllerStateValues();

        internal void StorageInit()
        {
            Controller.Storage = new MyModStorageComponent {[Session.Instance.ControllerSettingsGuid] = ""};
        }

        internal void SaveState(bool createStorage = false)
        {
            if (createStorage && Controller.Storage == null) Controller.Storage = new MyModStorageComponent();
            else if (Controller.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Controller.Storage[Session.Instance.ControllerStateGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadState()
        {
            if (Controller.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Controller.Storage.TryGetValue(Session.Instance.ControllerStateGuid, out rawData))
            {
                var base64 = Convert.FromBase64String(rawData);
                var loadedValues = MyAPIGateway.Utilities.SerializeFromBinary<ControllerStateValues>(base64);

                if (loadedValues != null)
                {
                    Value = loadedValues;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - ControllerId [{Controller.EntityId}]");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {
            Value.MId++;
            Session.Instance.PacketizeToClientsInRange(Controller, new DataControllerState(Controller.EntityId, Value)); // update clients with server's state
        }
    }

    internal class ControllerSettings
    {
        internal readonly IMyFunctionalBlock Controller;

        internal ControllerSettings(IMyFunctionalBlock controller)
        {
            Controller = controller;
        }

        internal ControllerSettingsValues Value { get; set; } = new ControllerSettingsValues();

        internal void SaveSettings(bool createStorage = false)
        {
            if (createStorage && Controller.Storage == null) Controller.Storage = new MyModStorageComponent();
            else if (Controller.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Controller.Storage[Session.Instance.ControllerSettingsGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadSettings()
        {
            if (Controller.Storage == null) return false;

            string rawData;
            var loadedSomething = false;

            if (Controller.Storage.TryGetValue(Session.Instance.ControllerSettingsGuid, out rawData))
            {
                ControllerSettingsValues loadedValues;

                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedValues = MyAPIGateway.Utilities.SerializeFromBinary<ControllerSettingsValues>(base64);
                }
                catch (Exception e)
                {
                    loadedValues = null;
                    Log.Line($"Load - ControllerId [{Controller.EntityId}]: - Error loading settings!\n{e}");
                }

                if (loadedValues != null)
                {
                    Value = loadedValues;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - ControllerId [{Controller.EntityId}]");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {
            Value.MId++;
            if (Session.Instance.IsServer)
            {
                Session.Instance.PacketizeToClientsInRange(Controller, new DataControllerSettings(Controller.EntityId, Value)); 
            }
            else 
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataControllerSettings(Controller.EntityId, Value));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
            }
        }
    }
}

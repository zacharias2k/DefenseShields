namespace DefenseShields
{
    using System;
    using global::DefenseShields.Support;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class ControllerState
    {
        internal readonly IMyFunctionalBlock Shield;

        internal ControllerState(IMyFunctionalBlock shield)
        {
            Shield = shield;
        }

        internal ProtoControllerState State { get; set; } = new ProtoControllerState();

        internal void StorageInit()
        {
            Shield.Storage = new MyModStorageComponent {[Session.Instance.ControllerSettingsGuid] = ""};
        }

        internal void SaveState(bool createStorage = false)
        {
            if (createStorage && Shield.Storage == null) Shield.Storage = new MyModStorageComponent();
            else if (Shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Shield.Storage[Session.Instance.ControllerStateGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadState()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.ControllerStateGuid, out rawData))
            {
                var base64 = Convert.FromBase64String(rawData);
                var loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoControllerState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - ShieldId [{Shield.EntityId}]");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {
            if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - ShieldId [{Shield.EntityId}]: network state update for shield");
            Session.Instance.PacketizeControllerState(Shield, State); // update clients with server's state
        }
    }

    internal class ControllerSettings
    {
        internal readonly IMyFunctionalBlock Shield;

        internal ControllerSettings(IMyFunctionalBlock shield)
        {
            Shield = shield;
        }

        internal ProtoControllerSettings Settings { get; set; } = new ProtoControllerSettings();

        internal void SaveSettings(bool createStorage = false)
        {
            if (createStorage && Shield.Storage == null) Shield.Storage = new MyModStorageComponent();
            else if (Shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Settings);
            Shield.Storage[Session.Instance.ControllerSettingsGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadSettings()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            var loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.ControllerSettingsGuid, out rawData))
            {
                ProtoControllerSettings loadedSettings;

                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<ProtoControllerSettings>(base64);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Load - ShieldId [{Shield.EntityId}]: - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - ShieldId [{Shield.EntityId}]");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {
            if (Session.Instance.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                Session.Instance.PacketizeControllerSettings(Shield, Settings); 
            }
            else 
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ClientRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataControllerSettings(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdControllerSettings, bytes);
            }
        }
    }
}

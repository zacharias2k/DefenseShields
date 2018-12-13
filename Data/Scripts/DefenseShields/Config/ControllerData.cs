using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class ControllerState
    {
        internal ProtoControllerState State = new ProtoControllerState();
        internal readonly IMyFunctionalBlock Shield;
        internal ControllerState(IMyFunctionalBlock shield)
        {
            Shield = shield;
        }

        public void StorageInit()
        {
            Shield.Storage = new MyModStorageComponent {[Session.Instance.ControllerSettingsGuid] = ""};
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && Shield.Storage == null) Shield.Storage = new MyModStorageComponent();
            else if (Shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Shield.Storage[Session.Instance.ControllerStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.ControllerStateGuid, out rawData))
            {
                ProtoControllerState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoControllerState>(base64);

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
            Session.PacketizeControllerState(Shield, State); // update clients with server's state
        }
    }

    public class ControllerSettings
    {
        internal ProtoControllerSettings Settings = new ProtoControllerSettings();
        internal readonly IMyFunctionalBlock Shield;
        internal ControllerSettings(IMyFunctionalBlock shield)
        {
            Shield = shield;
        }

        public void SaveSettings(bool createStorage = false)
        {
            
            if (createStorage && Shield.Storage == null) Shield.Storage = new MyModStorageComponent();
            else if (Shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Settings);
            Shield.Storage[Session.Instance.ControllerSettingsGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadSettings()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.ControllerSettingsGuid, out rawData))
            {
                ProtoControllerSettings loadedSettings = null;

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

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                Session.PacketizeControllerSettings(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ClientRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataControllerSettings(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdControllerSettings, bytes);
            }
        }
    }
}

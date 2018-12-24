using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class ModulatorState
    {
        internal ProtoModulatorState State = new ProtoModulatorState();
        internal readonly IMyFunctionalBlock Modulator;
        internal ModulatorState(IMyFunctionalBlock modulator)
        {
            Modulator = modulator;
        }

        public void StorageInit()
        {
            if (Modulator.Storage == null)
            {
                Modulator.Storage = new MyModStorageComponent {[Session.Instance.ModulatorSettingsGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && Modulator.Storage == null) Modulator.Storage = new MyModStorageComponent();
            else if (Modulator.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            Modulator.Storage[Session.Instance.ModulatorStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Modulator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Modulator.Storage.TryGetValue(Session.Instance.ModulatorStateGuid, out rawData))
            {
                ProtoModulatorState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoModulatorState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - ModulatorId [{Modulator.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.Instance.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - Online:{State.Online} Link:{State.Link} - Backup:{State.Backup} - ModulatorId [{Modulator.EntityId}]: network state update for modulator");
                Session.PacketizeModulatorState(Modulator, State); // update clients with server's settings
            }
        }
        #endregion
    }

    public class ModulatorSettings
    {
        internal ProtoModulatorSettings Settings = new ProtoModulatorSettings();
        internal readonly IMyFunctionalBlock Modulator;
        internal ModulatorSettings(IMyFunctionalBlock modulator)
        {
            Modulator = modulator;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (createStorage && Modulator.Storage == null) Modulator.Storage = new MyModStorageComponent();
            else if (Modulator.Storage == null) return;

            Modulator.Storage[Session.Instance.ModulatorSettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Modulator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Modulator.Storage.TryGetValue(Session.Instance.ModulatorSettingsGuid, out rawData))
            {
                ProtoModulatorSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ProtoModulatorSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"ModulatorId:{Modulator.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.Instance.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - ModulatorId [{Modulator.EntityId}]: network settings update for modulator");
                Session.PacketizeModulatorSettings(Modulator, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ClientRelay - ModulatorId [{Modulator.EntityId}]: sent network settings update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataModulatorSettings(MyAPIGateway.Multiplayer.MyId, Modulator.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdModulatorSettings, bytes);
            }
        }
        #endregion
    }
}

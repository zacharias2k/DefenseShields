using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class PlanetShieldState
    {
        internal ProtoPlanetShieldState State = new ProtoPlanetShieldState();
        internal readonly IMyFunctionalBlock PlanetShield;
        internal PlanetShieldState(IMyFunctionalBlock planetShield)
        {
            PlanetShield = planetShield;
        }

        public void StorageInit()
        {
            if (PlanetShield.Storage == null)
            {
                PlanetShield.Storage = new MyModStorageComponent {[Session.Instance.ModulatorSettingsGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && PlanetShield.Storage == null) PlanetShield.Storage = new MyModStorageComponent();
            else if (PlanetShield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            PlanetShield.Storage[Session.Instance.PlanetShieldStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (PlanetShield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (PlanetShield.Storage.TryGetValue(Session.Instance.PlanetShieldStateGuid, out rawData))
            {
                ProtoPlanetShieldState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoPlanetShieldState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"Loaded - PlanetShieldId [{PlanetShield.EntityId}]:\n{State.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Session.Instance.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - Online:{State.Online} - Backup:{State.Backup} - PlanetShieldId [{PlanetShield.EntityId}]: network state update for PlanetShield");
                Session.Instance.PacketizePlanetShieldState(PlanetShield, State); // update clients with server's settings
            }
        }
        #endregion
    }

    public class PlanetShieldSettings
    {
        internal ProtoPlanetShieldSettings Settings = new ProtoPlanetShieldSettings();
        internal readonly IMyFunctionalBlock PlanetShield;
        internal PlanetShieldSettings(IMyFunctionalBlock planetShield)
        {
            PlanetShield = planetShield;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (createStorage && PlanetShield.Storage == null) PlanetShield.Storage = new MyModStorageComponent();
            else if (PlanetShield.Storage == null) return;

            PlanetShield.Storage[Session.Instance.PlanetShieldSettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (PlanetShield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (PlanetShield.Storage.TryGetValue(Session.Instance.PlanetShieldSettingsGuid, out rawData))
            {
                ProtoPlanetShieldSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ProtoPlanetShieldSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"PlanetShieldId:{PlanetShield.EntityId.ToString()} - Error loading settings!\n{e}");
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
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - PlanetShieldId [{PlanetShield.EntityId}]: network settings update for PlanetShield");
                Session.Instance.PacketizePlanetShieldSettings(PlanetShield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ClientRelay - PlanetShieldId [{PlanetShield.EntityId}]: sent network settings update for PlanetShield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataPlanetShieldSettings(MyAPIGateway.Multiplayer.MyId, PlanetShield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdPlanetShieldSettings, bytes);
            }
        }
        #endregion
    }
}

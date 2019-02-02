namespace DefenseShields
{
    using System;
    using Support;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class PlanetShieldState
    {
        internal PlanetShieldStateValues State = new PlanetShieldStateValues();
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
                PlanetShieldStateValues loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<PlanetShieldStateValues>(base64);

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
                Session.Instance.PacketizeToClientsInRange(PlanetShield, new DataPlanetShieldState(PlanetShield.EntityId, State)); // update clients with server's state
            }
        }
        #endregion
    }

    public class PlanetShieldSettings
    {
        internal PlanetShieldSettingsValues Settings = new PlanetShieldSettingsValues();
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
                PlanetShieldSettingsValues loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<PlanetShieldSettingsValues>(rawData);
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
                Session.Instance.PacketizeToClientsInRange(PlanetShield, new DataPlanetShieldSettings(PlanetShield.EntityId, Settings)); // update clients with server's settings
            }
            else // client, send settings to server
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataPlanetShieldSettings(PlanetShield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
            }
        }
        #endregion
    }
}

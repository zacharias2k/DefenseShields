using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class ModulatorSettings
    {
        internal ModulatorBlockSettings Settings = new ModulatorBlockSettings();
        internal readonly IMyFunctionalBlock Modulator;
        internal ModulatorSettings(IMyFunctionalBlock modulator)
        {
            Modulator = modulator;
        }

        public void SaveSettings()
        {
            if (Modulator.Storage == null)
            {
                Modulator.Storage = new MyModStorageComponent();
            }
            Modulator.Storage[Session.Instance.ModulatorGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Modulator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Modulator.Storage.TryGetValue(Session.Instance.ModulatorGuid, out rawData))
            {
                ModulatorBlockSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ModulatorBlockSettings>(rawData);
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

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Modulator.EntityId}]: network settings update for modulator");
                Session.PacketizeModulatorSettings(Modulator, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Modulator.EntityId}]: sent network settings update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ModulatorData(MyAPIGateway.Multiplayer.MyId, Modulator.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdModulator, bytes);
            }
        }
        #endregion
    }
}

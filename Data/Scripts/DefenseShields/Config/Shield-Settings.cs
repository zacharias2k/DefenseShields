using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class DefenseShieldsSettings
    {
        internal DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        internal readonly IMyFunctionalBlock Shield;
        internal DefenseShieldsSettings(IMyFunctionalBlock shield)
        {
            Shield = shield;
        }

        public void SaveSettings()
        {
            if (Shield.Storage == null)
            {
                Shield.Storage = new MyModStorageComponent();
            }
            Shield.Storage[Session.Instance.SettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.SettingsGuid, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Load - EmitterId [{Shield.EntityId}]: - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - EmitterId [{Shield.EntityId}]:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Shield.EntityId}]: network settings update for shield");
                Session.PacketizeShieldSettings(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelat - EmitterId [{Shield.EntityId}]: network settings update for shield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new PacketData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdSettings, bytes);
            }
        }
    }
}

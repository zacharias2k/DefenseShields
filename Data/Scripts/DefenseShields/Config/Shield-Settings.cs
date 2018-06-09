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
                Log.Line($"ShieldId:{Shield.EntityId.ToString()} - Storage = null");
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
                    Log.Line($"ShieldId:{Shield.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded settings:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for shield {Shield.EntityId}");
                Session.PacketizeShieldSettings(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for shield {Shield.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new PacketData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_SETTINGS, bytes);
            }
        }
    }
}

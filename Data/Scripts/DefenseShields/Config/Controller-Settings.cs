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
            if (Shield.Storage == null) Shield.Storage = new MyModStorageComponent();
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Settings);
            Shield.Storage[Session.Instance.SettingsGuid] = Convert.ToBase64String(binary);
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
                    if (rawData.IndexOf('<', 0, 10) != -1)
                    {
                        loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                    }
                    else
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<DefenseShieldsModSettings>(base64);
                    }

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
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - ShieldId [{Shield.EntityId}]:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                Session.PacketizeShieldSettings(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - ShieldId [{Shield.EntityId}]: network settings update for shield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new PacketData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdSettings, bytes);
            }
        }
    }
}

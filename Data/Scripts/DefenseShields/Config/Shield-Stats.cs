using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class ShieldStatus
    {
        internal ShieldStats Stats = new ShieldStats();
        internal readonly IMyFunctionalBlock Field;
        internal ShieldStatus(IMyFunctionalBlock field)
        {
            Field = field;
        }

        public void SaveSettings()
        {
            if (Field.Storage == null)
            {
                Field.Storage = new MyModStorageComponent();
            }
            Field.Storage[Session.Instance.SettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Stats);
        }

        public bool LoadSettings()
        {
            if (Field.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Field.Storage.TryGetValue(Session.Instance.SettingsGuid, out rawData))
            {
                ShieldStats loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ShieldStats>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Load - ShieldId [{Field.EntityId}]: - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Stats = loadedSettings;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - ShieldId [{Field.EntityId}]:\n{Stats.ToString()}");
            }
            return loadedSomething;
        }

        internal void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - ShieldId [{Field.EntityId}]: network settings update for shield");
                Session.PacketizeStats(Field, Stats); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - ShieldId [{Field.EntityId}]: network settings update for shield");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new StatsData(MyAPIGateway.Multiplayer.MyId, Field.EntityId, Stats));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdStats, bytes);
            }
        }
    }
}

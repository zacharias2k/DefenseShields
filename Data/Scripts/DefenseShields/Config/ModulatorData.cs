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

        public void SaveState()
        {
            if (Modulator.Storage == null)
            {
                Modulator.Storage = new MyModStorageComponent();
            }
            Modulator.Storage[Session.Instance.ModulatorStateGuid] = MyAPIGateway.Utilities.SerializeToXML(State);
        }

        public bool LoadState()
        {
            if (Modulator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Modulator.Storage.TryGetValue(Session.Instance.ModulatorStateGuid, out rawData))
            {
                ProtoModulatorState loadedState = null;

                try
                {
                    loadedState = MyAPIGateway.Utilities.SerializeFromXML<ProtoModulatorState>(rawData);
                }
                catch (Exception e)
                {
                    loadedState = null;
                    Log.Line($"ModulatorId:{Modulator.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedState != null)
                {
                    State = loadedState;
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
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Modulator.EntityId}]: network state update for modulator");
                Session.PacketizeModulatorState(Modulator, State); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Modulator.EntityId}]: sent network state update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataModulatorState(MyAPIGateway.Multiplayer.MyId, Modulator.EntityId, State));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdModulatorState, bytes);
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

        public void SaveSettings()
        {
            if (Modulator.Storage == null)
            {
                Modulator.Storage = new MyModStorageComponent();
            }
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

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Modulator.EntityId}]: network settings update for modulator");
                Session.PacketizeModulatorSettings(Modulator, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Modulator.EntityId}]: sent network settings update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataModulatorSettings(MyAPIGateway.Multiplayer.MyId, Modulator.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdModulatorSettings, bytes);
            }
        }
        #endregion
    }
}

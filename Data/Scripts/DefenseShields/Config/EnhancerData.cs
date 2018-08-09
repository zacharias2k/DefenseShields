using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class EnhancerState
    {
        internal ProtoEnhancerState State = new ProtoEnhancerState();
        internal readonly IMyFunctionalBlock Enhancer;
        internal EnhancerState(IMyFunctionalBlock enhancer)
        {
            Enhancer = enhancer;
        }

        public void SaveState()
        {
            if (Enhancer.Storage == null)
            {
                Enhancer.Storage = new MyModStorageComponent();
            }
            Enhancer.Storage[Session.Instance.EnhancerStateGuid] = MyAPIGateway.Utilities.SerializeToXML(State);
        }

        public bool LoadState()
        {
            if (Enhancer.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Enhancer.Storage.TryGetValue(Session.Instance.EnhancerStateGuid, out rawData))
            {
                ProtoEnhancerState loadedState = null;

                try
                {
                    loadedState = MyAPIGateway.Utilities.SerializeFromXML<ProtoEnhancerState>(rawData);
                }
                catch (Exception e)
                {
                    loadedState = null;
                    Log.Line($"ModulatorId:{Enhancer.EntityId.ToString()} - Error loading state!\n{e}");
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
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Enhancer.EntityId}]: network state update for modulator");
                Session.PacketizeEnhancerState(Enhancer, State); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Enhancer.EntityId}]: sent network state update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataEnhancerState(MyAPIGateway.Multiplayer.MyId, Enhancer.EntityId, State));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdEnhancerState, bytes);
            }
        }
        #endregion
    }
    /*
    public class EnhancerSettings
    {
        internal ProtoEnhancerSettings Settings = new ProtoEnhancerSettings();
        internal readonly IMyFunctionalBlock Enhancer;
        internal EnhancerSettings(IMyFunctionalBlock enhancer)
        {
            Enhancer = enhancer;
        }

        public void SaveSettings()
        {
            if (Enhancer.Storage == null)
            {
                Enhancer.Storage = new MyModStorageComponent();
            }
            Enhancer.Storage[Session.Instance.EnhancerSettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Enhancer.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Enhancer.Storage.TryGetValue(Session.Instance.ModulatorSettingsGuid, out rawData))
            {
                ProtoEnhancerSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ProtoEnhancerSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"ModulatorId:{Enhancer.EntityId.ToString()} - Error loading settings!\n{e}");
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
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{Enhancer.EntityId}]: network settings update for modulator");
                Session.PacketizeEnhancerSettings(Enhancer, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{Enhancer.EntityId}]: sent network settings update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataEnhancerSettings(MyAPIGateway.Multiplayer.MyId, Enhancer.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdEnhancerSettings, bytes);
            }
        }
        #endregion
    }
    */
}

using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public class O2GeneratorState
    {
        internal ProtoO2GeneratorState State = new ProtoO2GeneratorState();
        internal readonly IMyFunctionalBlock O2Generator;
        internal O2GeneratorState(IMyFunctionalBlock o2Generator)
        {
            O2Generator = o2Generator;
        }

        public void SaveState()
        {
            if (O2Generator.Storage == null)
            {
                O2Generator.Storage = new MyModStorageComponent();
            }
            O2Generator.Storage[Session.Instance.O2GeneratorStateGuid] = MyAPIGateway.Utilities.SerializeToXML(State);
        }

        public bool LoadState()
        {
            if (O2Generator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (O2Generator.Storage.TryGetValue(Session.Instance.O2GeneratorStateGuid, out rawData))
            {
                ProtoO2GeneratorState loadedState = null;

                try
                {
                    loadedState = MyAPIGateway.Utilities.SerializeFromXML<ProtoO2GeneratorState>(rawData);
                }
                catch (Exception e)
                {
                    loadedState = null;
                    Log.Line($"ModulatorId:{O2Generator.EntityId.ToString()} - Error loading state!\n{e}");
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
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - EmitterId [{O2Generator.EntityId}]: network state update for modulator");
                Session.PacketizeO2GeneratorState(O2Generator, State); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - EmitterId [{O2Generator.EntityId}]: sent network state update for modulator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataO2GeneratorState(MyAPIGateway.Multiplayer.MyId, O2Generator.EntityId, State));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdO2GeneratorState, bytes);
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

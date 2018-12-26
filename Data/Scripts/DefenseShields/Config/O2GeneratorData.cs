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

        public void StorageInit()
        {
            if (O2Generator.Storage == null)
            {
                O2Generator.Storage = new MyModStorageComponent {[Session.Instance.O2GeneratorStateGuid] = ""};
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (createStorage && O2Generator.Storage == null) O2Generator.Storage = new MyModStorageComponent();
            else if (O2Generator.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(State);
            O2Generator.Storage[Session.Instance.O2GeneratorStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (O2Generator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (O2Generator.Storage.TryGetValue(Session.Instance.O2GeneratorStateGuid, out rawData))
            {
                ProtoO2GeneratorState loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<ProtoO2GeneratorState>(base64);

                if (loadedState != null)
                {
                    State = loadedState;
                    loadedSomething = true;
                }

                if (Session.Enforced.Debug == 1) Log.Line($"Loaded - O2GeneratorId [{O2Generator.EntityId}]:\n{State.ToString()}");
            }

            return loadedSomething;
        }

        #region Network

        public void NetworkUpdate()
        {

            if (Session.Instance.IsServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ServRelay - O2GeneratorId [{O2Generator.EntityId}]: network state update for O2Generator");
                Session.Instance.PacketizeO2GeneratorState(O2Generator, State); // update clients with server's settings
            }
        }

        #endregion
    }

    public class O2GeneratorSettings
    {
        internal ProtoO2GeneratorSettings Settings = new ProtoO2GeneratorSettings();
        internal readonly IMyFunctionalBlock O2Generator;

        internal O2GeneratorSettings(IMyFunctionalBlock o2Generator)
        {
            O2Generator = o2Generator;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (createStorage && O2Generator.Storage == null) O2Generator.Storage = new MyModStorageComponent();
            else if (O2Generator.Storage == null) return;

            O2Generator.Storage[Session.Instance.O2GeneratorSettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (O2Generator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (O2Generator.Storage.TryGetValue(Session.Instance.O2GeneratorSettingsGuid, out rawData))
            {
                ProtoO2GeneratorSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ProtoO2GeneratorSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"O2GeneratorId:{O2Generator.EntityId.ToString()} - Error loading settings!\n{e}");
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
                if (Session.Enforced.Debug == 1) Log.Line($"ServRelay - O2GeneratorId [{O2Generator.EntityId}]: network settings update for O2Generator");
                Session.Instance.PacketizeO2GeneratorSettings(O2Generator, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientRelay - O2GeneratorId [{O2Generator.EntityId}]: sent network settings update for O2Generator");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataO2GeneratorSettings(MyAPIGateway.Multiplayer.MyId, O2Generator.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdO2GeneratorSettings, bytes);
            }
        }

        #endregion
    }
}

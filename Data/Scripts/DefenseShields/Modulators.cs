using System;
using System.Collections.Generic;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldModulator", "SmallShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {

        public bool ServerUpdate;
        private bool _hierarchyChanged;
        private bool _hierarchyDelayed;

        private uint _tick;
        private int _count = -1;
        private int _lCount;

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        public MyModStorageComponentBase Storage { get; set; }
        internal ModulatorSettings Settings = new ModulatorSettings();
        internal ModulatorGridComponent MGridComponent;

        private IMyUpgradeModule Modulator => (IMyUpgradeModule)Entity;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateGrids;

        internal DSUtils Dsutil1 = new DSUtils();

        public Modulators()
        {
            MGridComponent = new ModulatorGridComponent(this);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                Modulator.CubeGrid.Components.Add(MGridComponent);
                Session.Instance.Modulators.Add(this);
                if (!_modulators.ContainsKey(Entity.EntityId)) _modulators.Add(Entity.EntityId, this);
                CreateUi();
                StorageSetup();
                ((MyCubeGrid) Modulator.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Modulator.Storage;
            LoadSettings();
            UpdateSettings(Settings, false);
        }

        private void HierarchyChanged(IMyCubeGrid myCubeGrid)
        {
            if (_hierarchyChanged)
            {
                _hierarchyDelayed = true;
                return;
            }
            _hierarchyChanged = true;
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Modulator.CubeGrid, GridLinkTypeEnum.Logical);
            MGridComponent.GetSubGrids.Clear();
            for (int i = 0; i < gotGroups.Count; i++) MGridComponent.GetSubGrids.Add(gotGroups[i]);
        }

        public override void UpdateBeforeSimulation100()
        {
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

            _hierarchyChanged = false;

            if (_hierarchyDelayed)
            {
                _hierarchyDelayed = false;
                HierarchyChanged(Modulator.CubeGrid);
            }

            if (ServerUpdate) SyncControlsServer();
            SyncControlsClient();

            if (Modulator.CustomData != MGridComponent.ModulationPassword)
            {
                MGridComponent.ModulationPassword = Modulator.CustomData;
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Updating modulator password");
            }
        }

        #region Create UI
        private void CreateUi()
        {
            Session.Instance.ControlsLoaded = true;
            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Modulator, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Modulator, "AllowGrids", "Grids may pass", false);
        }
        #endregion

        #region Settings
        public bool Enabled
        {
            get { return Settings.Enabled; }
            set { Settings.Enabled = value; }
        }

        public bool ModulateVoxels
        {
            get { return Settings.ModulateVoxels; }
            set { Settings.ModulateVoxels = value; }
        }

        public bool ModulateGrids
        {
            get { return Settings.ModulateGrids; }
            set { Settings.ModulateGrids = value; }
        }

        public void UpdateSettings(ModulatorSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            MGridComponent.Enabled = newSettings.Enabled;
            ModulateVoxels = newSettings.ModulateVoxels;
            MGridComponent.Voxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;
            MGridComponent.Grids = newSettings.ModulateGrids;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for modulator");
        }

        public void SaveSettings()
        {
            if (Modulator.Storage == null)
            {
                Log.Line($"ModulatorId:{Modulator.EntityId.ToString()} - Storage = null");
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
                ModulatorSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ModulatorSettings>(rawData);
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

        private void SyncControlsServer()
        {
            if (Modulator != null && !Modulator.Enabled.Equals(Settings.Enabled))
            {
                Enabled = Settings.Enabled;
                MGridComponent.Enabled = Settings.Enabled;
            }

            if (_modulateVoxels != null && !_modulateVoxels.Getter(Modulator).Equals(Settings.ModulateVoxels))
            {
                _modulateVoxels.Setter(Modulator, Settings.ModulateVoxels);
                MGridComponent.Voxels = Settings.ModulateVoxels;
            }

            if (_modulateGrids != null && !_modulateGrids.Getter(Modulator).Equals(Settings.ModulateGrids))
            {
                _modulateGrids.Setter(Modulator, Settings.ModulateGrids);
                MGridComponent.Grids = Settings.ModulateGrids;
            }

            ServerUpdate = false;
            SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer (modulator)");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!Enabled.Equals(Enabled) 
                || !_modulateVoxels.Getter(Modulator).Equals(ModulateVoxels)
                || !_modulateGrids.Getter(Modulator).Equals(ModulateGrids))
            {
                needsSync = true;
                Enabled = Settings.Enabled;
                MGridComponent.Enabled = Settings.Enabled;
                ModulateVoxels = _modulateVoxels.Getter(Modulator);
                MGridComponent.Voxels = _modulateVoxels.Getter(Modulator);
                ModulateGrids = _modulateGrids.Getter(Modulator);
                MGridComponent.Grids = _modulateGrids.Getter(Modulator);
            }

            if (needsSync)
            {
                NetworkUpdate();
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Needed sync for modulator");
            }
        }
        #endregion

        #region Network
        private void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for modulator {Modulator.EntityId}");
                Session.PacketizeModulatorSettings(Modulator, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for modulator {Modulator.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ModulatorData(MyAPIGateway.Multiplayer.MyId, Modulator.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_MODULATOR, bytes);
            }
        }
        #endregion
        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                Modulator?.CubeGrid.Components.Remove(typeof(ModulatorGridComponent), this);
                Session.Instance.Modulators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_modulators.ContainsKey(Entity.EntityId)) _modulators.Remove(Entity.EntityId);
                if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
    }
}

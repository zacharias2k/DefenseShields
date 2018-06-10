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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldController", "SmallShieldController")]
    public class Controllers : MyGameLogicComponent
    {

        public bool ServerUpdate;
        private bool _hierarchyChanged;
        private bool _hierarchyDelayed;

        private uint _tick;
        private int _count = -1;
        private int _lCount;

        private readonly Dictionary<long, Controllers> _controllers = new Dictionary<long, Controllers>();

        public MyModStorageComponentBase Storage { get; set; }
        internal ControllerSettings Settings = new ControllerSettings();
        internal ControllerGridComponent CGridComponent;

        private IMyUpgradeModule Controller => (IMyUpgradeModule)Entity;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateGrids;

        internal DSUtils Dsutil1 = new DSUtils();

        public Controllers()
        {
            CGridComponent = new ControllerGridComponent(this);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                Controller.CubeGrid.Components.Add(CGridComponent);
                Session.Instance.Controllers.Add(this);
                if (!_controllers.ContainsKey(Entity.EntityId)) _controllers.Add(Entity.EntityId, this);
                CreateUi();
                StorageSetup();
                ((MyCubeGrid)Controller.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Controller.Storage;
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
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Controller.CubeGrid, GridLinkTypeEnum.Logical);
            CGridComponent.GetSubGrids.Clear();
            for (int i = 0; i < gotGroups.Count; i++) CGridComponent.GetSubGrids.Add(gotGroups[i]);
        }

        public override void UpdateBeforeSimulation100()
        {
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

            _hierarchyChanged = false;

            if (_hierarchyDelayed)
            {
                _hierarchyDelayed = false;
                HierarchyChanged(Controller.CubeGrid);
            }

            if (ServerUpdate) SyncControlsServer();
            SyncControlsClient();

            if (Controller.CustomData != CGridComponent.ModulationPassword)
            {
                CGridComponent.ModulationPassword = Controller.CustomData;
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Updating modulator password");
            }
        }

        #region Create UI
        private void CreateUi()
        {
            Session.Instance.ControlsLoaded = true;
            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Controller, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Controller, "AllowGrids", "Grids may pass", false);
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

        public void UpdateSettings(ControllerSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            CGridComponent.Enabled = newSettings.Enabled;
            ModulateVoxels = newSettings.ModulateVoxels;
            CGridComponent.Voxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;
            CGridComponent.Grids = newSettings.ModulateGrids;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for modulator");
        }

        public void SaveSettings()
        {
            if (Controller.Storage == null)
            {
                Log.Line($"ModulatorId:{Controller.EntityId.ToString()} - Storage = null");
                Controller.Storage = new MyModStorageComponent();
            }
            Controller.Storage[Session.Instance.ModulatorGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Controller.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Controller.Storage.TryGetValue(Session.Instance.ModulatorGuid, out rawData))
            {
                ControllerSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ControllerSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"ModulatorId:{Controller.EntityId.ToString()} - Error loading settings!\n{e}");
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
            if (Controller != null && !Controller.Enabled.Equals(Settings.Enabled))
            {
                Enabled = Settings.Enabled;
                CGridComponent.Enabled = Settings.Enabled;
            }

            if (_modulateVoxels != null && !_modulateVoxels.Getter(Controller).Equals(Settings.ModulateVoxels))
            {
                _modulateVoxels.Setter(Controller, Settings.ModulateVoxels);
                CGridComponent.Voxels = Settings.ModulateVoxels;
            }

            if (_modulateGrids != null && !_modulateGrids.Getter(Controller).Equals(Settings.ModulateGrids))
            {
                _modulateGrids.Setter(Controller, Settings.ModulateGrids);
                CGridComponent.Grids = Settings.ModulateGrids;
            }

            ServerUpdate = false;
            SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer (modulator)");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!Enabled.Equals(Enabled) 
                || !_modulateVoxels.Getter(Controller).Equals(ModulateVoxels)
                || !_modulateGrids.Getter(Controller).Equals(ModulateGrids))
            {
                needsSync = true;
                Enabled = Settings.Enabled;
                CGridComponent.Enabled = Settings.Enabled;
                ModulateVoxels = _modulateVoxels.Getter(Controller);
                CGridComponent.Voxels = _modulateVoxels.Getter(Controller);
                ModulateGrids = _modulateGrids.Getter(Controller);
                CGridComponent.Grids = _modulateGrids.Getter(Controller);
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
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for modulator {Controller.EntityId}");
                Session.PacketizeControllerSettings(Controller, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for modulator {Controller.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ControllerData(MyAPIGateway.Multiplayer.MyId, Controller.EntityId, Settings));
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
                Controller?.CubeGrid.Components.Remove(typeof(ControllerGridComponent), this);
                Session.Instance.Controllers.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_controllers.ContainsKey(Entity.EntityId)) _controllers.Remove(Entity.EntityId);
                if (Session.Instance.Controllers.Contains(this)) Session.Instance.Controllers.Remove(this);
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

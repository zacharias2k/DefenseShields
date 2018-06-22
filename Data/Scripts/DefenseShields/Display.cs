using System;
using System.Collections.Generic;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false, "DSControlLCD")]
    public class Displays : MyGameLogicComponent
    {
        public bool ServerUpdate;

        private uint _tick;
        private int _count = -1;
        private int _lCount;

        private readonly Dictionary<long, Displays> _displays = new Dictionary<long, Displays>();

        public MyModStorageComponentBase Storage { get; set; }
        internal DisplaySettings Settings = new DisplaySettings();
        internal ShieldGridComponent ShieldComp;

        private IMyTextPanel Display => (IMyTextPanel)Entity;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyTextPanel> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyTextPanel> _modulateGrids;

        internal DSUtils Dsutil1 = new DSUtils();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                Session.Instance.Displays.Add(this);
                if (!_displays.ContainsKey(Entity.EntityId)) _displays.Add(Entity.EntityId, this);
                CreateUi();
                StorageSetup();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Display.Storage;
            LoadSettings();
            UpdateSettings(Settings, false);
        }

        public override void UpdateBeforeSimulation100()
        {
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            if (ShieldComp == null) Display.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp?.DefenseShields?.Shield == null || !ShieldComp.ShieldActive) return;
            Display.WritePublicText(ShieldComp.DefenseShields.Shield.CustomInfo);
            //if (ServerUpdate) SyncControlsServer();
            //SyncControlsClient();
        }

        #region Create UI
        private void CreateUi()
        {
            //if (Session.Instance.DisplayControlsLoaded) return; // Fix get existing control
            Session.Instance.DisplayControlsLoaded = true;
            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyTextPanel>(Display, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyTextPanel>(Display, "AllowGrids", "Grids may pass", false);
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

        public void UpdateSettings(DisplaySettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            ModulateVoxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for Display");
        }

        public void SaveSettings()
        {
            if (Display.Storage == null)
            {
                Log.Line($"DisplayId:{Display.EntityId.ToString()} - Storage = null");
                Display.Storage = new MyModStorageComponent();
            }
            Display.Storage[Session.Instance.DisplayGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Display.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Display.Storage.TryGetValue(Session.Instance.DisplayGuid, out rawData))
            {
                DisplaySettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DisplaySettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"DisplayId:{Display.EntityId.ToString()} - Error loading settings!\n{e}");
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
            if (Display != null && !Display.Enabled.Equals(Settings.Enabled))
            {
                Enabled = Settings.Enabled;
            }

            if (_modulateVoxels != null && !_modulateVoxels.Getter(Display).Equals(Settings.ModulateVoxels))
            {
                _modulateVoxels.Setter(Display, Settings.ModulateVoxels);
            }

            if (_modulateGrids != null && !_modulateGrids.Getter(Display).Equals(Settings.ModulateGrids))
            {
                _modulateGrids.Setter(Display, Settings.ModulateGrids);
            }

            ServerUpdate = false;
            SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer (display)");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!Enabled.Equals(Enabled) 
                || !_modulateVoxels.Getter(Display).Equals(ModulateVoxels)
                || !_modulateGrids.Getter(Display).Equals(ModulateGrids))
            {
                needsSync = true;
                Enabled = Settings.Enabled;
                ModulateVoxels = _modulateVoxels.Getter(Display);
                ModulateGrids = _modulateGrids.Getter(Display);
            }

            if (needsSync)
            {
                NetworkUpdate();
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Needed sync for display");
            }
        }
        #endregion

        #region Network
        private void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for display {Display.EntityId}");
                Session.PacketizeDisplaySettings(Display, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for display {Display.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DisplayData(MyAPIGateway.Multiplayer.MyId, Display.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_DISPLAY, bytes);
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
                Session.Instance.Displays.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_displays.ContainsKey(Entity.EntityId)) _displays.Remove(Entity.EntityId);
                if (Session.Instance.Displays.Contains(this)) Session.Instance.Displays.Remove(this);
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

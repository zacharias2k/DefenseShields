using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "LargeShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {
        private float _power = 0.05f;
        internal bool MainInit;
        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        public MyModStorageComponentBase Storage { get; set; }

        internal ModulatorSettings Settings = new ModulatorSettings();

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        public IMyOreDetector Modulator => (IMyOreDetector)Entity;

        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _modulateGrids;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                Entity.Components.TryGet(out Sink);

                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                if (!_modulators.ContainsKey(Entity.EntityId)) _modulators.Add(Entity.EntityId, this);
                DefenseShieldsBase.Instance.Modulators.Add(this);
                Modulator.CubeGrid.Components.Add(new ModulatorGridComponent(this));
                Modulator.BroadcastUsingAntennas = false;
                StorageSetup();
                CreateUi();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Modulator.Storage;
            LoadSettings();
            UpdateSettings(Settings, false);
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!MainInit)
            {
                MainInit = true;
            }
            Log.Line($"{Modulator.IsWorking} - {Modulator.BroadcastUsingAntennas}");
        }

        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void RemoveOreUi()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;
        }

        private void CreateUi()
        {
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Modulator, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Modulator, "AllowGrids", "Grids may pass", false);
        }
        #endregion

        public bool Enabled
        {
            get { return Settings.Enabled; }
            set { Settings.Enabled = value; }
        }

        public bool ShieldIdleVisible
        {
            get { return Settings.IdleInvisible; }
            set { Settings.IdleInvisible = value; }
        }

        public bool ShieldActiveVisible
        {
            get { return Settings.ActiveInvisible; }
            set { Settings.ActiveInvisible = value; }
        }

        public void UpdateSettings(ModulatorSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            ShieldIdleVisible = newSettings.IdleInvisible;
            ShieldActiveVisible = newSettings.ActiveInvisible;
        }

        public void SaveSettings()
        {
            if (Modulator.Storage == null)
            {
                Log.Line($"ShieldId:{Modulator.EntityId.ToString()} - Storage = null");
                Modulator.Storage = new MyModStorageComponent();
            }
            Modulator.Storage[DefenseShieldsBase.Instance.ModulatorGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Modulator.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Modulator.Storage.TryGetValue(DefenseShieldsBase.Instance.ModulatorGuid, out rawData))
            {
                ModulatorSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ModulatorSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"ShieldId:{Modulator.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                //Log.Line($"Loaded settings:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
    }
}

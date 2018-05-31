using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {
        private float _power = 0.05f;
        internal bool MainInit;
        internal bool CustomDataReset = true;
        internal MyStringId Password = MyStringId.GetOrCompute("Password");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Set the shield modulation password");

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        public MyModStorageComponentBase Storage { get; set; }

        internal ModulatorSettings Settings = new ModulatorSettings();
        internal ModulatorGridComponent MGridComponent;

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        private IMyUpgradeModule Modulator => (IMyUpgradeModule)Entity;

        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateGrids;

        public Modulators()
        {
            MGridComponent = new ModulatorGridComponent(this);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                Entity.Components.TryGet(out Sink);
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
                Modulator.OnClose += OnClose;
                Modulator.CubeGrid.Components.Add(MGridComponent);
                Session.Instance.Modulators.Add(this);
                if (!_modulators.ContainsKey(Entity.EntityId)) _modulators.Add(Entity.EntityId, this);
                CreateUi();
                StorageSetup();
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomDataToPassword;
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
                MGridComponent.ModulationPassword = Modulator.CustomData;
                MainInit = true;
            }

            if (Modulator.CustomData != MGridComponent.ModulationPassword)
            {
                MGridComponent.ModulationPassword = Modulator.CustomData;
                SaveSettings();
            }
        }

        #region Create UI
        private void CreateUi()
        {
            Session.Instance.ControlsLoaded = true;
            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Modulator, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Modulator, "AllowGrids", "Grids may pass", false);
        }

        private void CustomDataToPassword(IMyTerminalBlock block, List<IMyTerminalControl> myTerminalControls)
        {
            try
            {
                if (block.BlockDefinition.SubtypeId == "LargeShieldModulator" || block.BlockDefinition.SubtypeId == "DefenseShieldsST" 
                    || block.BlockDefinition.SubtypeId == "DefenseShieldsSS" || block.BlockDefinition.SubtypeId == "DefenseShieldsLS")
                    SetCustomDataToPassword(myTerminalControls);
                else if (!CustomDataReset) ResetCustomData(myTerminalControls);
            }
            catch (Exception ex) { Log.Line($"Exception in CustomDataToPassword: {ex}"); }
        }

        private void SetCustomDataToPassword(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip) customData).Title = Password;
            ((IMyTerminalControlTitleTooltip) customData).Tooltip = PasswordTooltip;
            customData.RedrawControl();
            CustomDataReset = false;
        }

        private void ResetCustomData(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = MySpaceTexts.Terminal_CustomData;
            ((IMyTerminalControlTitleTooltip) customData).Tooltip = MySpaceTexts.Terminal_CustomDataTooltip;
            customData.RedrawControl();
            CustomDataReset = true;
        }

        private void OnClose(IMyEntity obj)
        {
            if (Modulator.CubeGrid.Components.Contains(typeof(ModulatorGridComponent))) Modulator.CubeGrid.Components.Add(MGridComponent);
            if (_modulators.ContainsKey(Entity.EntityId)) _modulators.Remove(Entity.EntityId);
            if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
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

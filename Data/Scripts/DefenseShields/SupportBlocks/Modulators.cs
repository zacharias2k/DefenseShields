namespace DefenseShields
{
    using System;
    using System.Text;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Game.ObjectBuilders.Definitions;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using VRage.Utils;
    using VRageMath;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldModulator", "SmallShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {
        internal ModulatorGridComponent ModulatorComp;
        internal ShieldGridComponent ShieldComp;
        internal MyResourceSinkInfo ResourceInfo;

        private readonly MyDefinitionId _gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private MyEntitySubpart _subpartRotor;

        private bool _powered;

        private uint _tick;
        private uint _subTick = 1;

        private float _power = 0.01f;

        private bool _isServer;
        private bool _isDedicated;
        private bool _wasOnline;
        private bool _wasLink;
        private bool _wasBackup;
        private bool _firstRun = true;

        private int _wasModulateDamage;
        private int _count = -1;
        private bool _tock33;
        private bool _tock34;
        private bool _tock60;
        private float _wasModulateEnergy;
        private float _wasModulateKinetic;
        private bool _subDelayed;

        internal int RotationTime { get; set; }
        internal bool MainInit { get; set; }
        internal bool SettingsUpdated { get; set; }
        internal bool ClientUiUpdate { get; set; }
        internal bool ContainerInited { get; set; }
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }
        internal bool EnhancerLink { get; set; }

        internal ModulatorSettings ModSet { get; set; }
        internal ModulatorState ModState { get; set; }
        internal MyResourceSinkComponent Sink { get; set; }
        internal MyCubeGrid MyGrid { get; set; }
        internal MyCubeBlock MyCube { get; set; }
        internal IMyUpgradeModule Modulator { get; set; }

        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                if (!MyAPIGateway.Utilities.IsDedicated) NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                else NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                Modulator = (IMyUpgradeModule)Entity;
                ContainerInited = true;
            }
            if (Entity.InScene) OnAddedToScene();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                StorageSetup();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                MyGrid = (MyCubeGrid)Modulator.CubeGrid;
                MyCube = Modulator as MyCubeBlock;
                RegisterEvents();
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: - ModulatorId [{Modulator.EntityId}]");
                if (!MainInit) return;
                ResetComp();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Modulator.CubeGrid.Physics == null) return;

                _isServer = Session.Instance.IsServer;
                _isDedicated = Session.Instance.DedicatedServer;

                ResetComp();

                Session.Instance.Modulators.Add(this);

                CreateUi();
                ModUi.ComputeDamage(this, ModUi.GetDamage(Modulator));

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                PowerInit();
                Modulator.RefreshCustomInfo();
                StateChange(true);
                if (!Session.Instance.ModAction)
                {
                    Session.Instance.ModAction = true;
                    Session.AppendConditionToAction<IMyUpgradeModule>((a) => Session.Instance.ModActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<Modulators>() != null && Session.Instance.ModActions.Contains(a.Id));
                }
                MainInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = Session.Instance.Tick;
                _tock33 = _tick % 33 == 0;
                _tock34 = _tick % 33 == 0;
                if (_count++ == 59)
                {
                    _count = 0;
                    _tock60 = true;
                }
                else _tock60 = false;

                var wait = _isServer && _count != 0 && ModState.State.Backup;

                MyGrid = MyCube.CubeGrid;
                if (wait || MyGrid?.Physics == null) return;

                Timing();

                if (!ModulatorReady())
                {
                    ModulatorOff();
                    return;
                }
                ModulatorOn();
                if (!_isDedicated && UtilsStatic.DistanceCheck(Modulator, 1000, 1))
                {
                    var blockCam = MyCube.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && IsWorking) BlockMoveAnimation();
                }

                if (_isServer) UpdateStates();
                _firstRun = false;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                _tick = Session.Instance.Tick;
                if (_count++ == 5)
                {
                    _count = 0;
                    _tock60 = true;
                }
                else _tock60 = false;
                _tock33 = _count == 3;
                _tock34 = _count == 4;

                var wait = _isServer && _count != 0 && ModState.State.Backup;

                MyGrid = MyCube.CubeGrid;
                if (wait || MyGrid?.Physics == null) return;
                Timing();

                if (!ModulatorReady())
                {
                    ModulatorOff();
                    return;
                }
                ModulatorOn();

                if (_isServer) UpdateStates();

                _firstRun = false;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation10: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Modulator.Storage != null)
                {
                    ModState.SaveState();
                    ModSet.SaveSettings();
                }
            }
            return false;
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
                if (ShieldComp?.Modulator == this)
                {
                    ShieldComp.Modulator = null;
                }

                if (ModulatorComp?.Modulator == this)
                {
                    ModulatorComp.Modulator = null;
                    ModulatorComp = null;
                }
                RegisterEvents(false);
                IsWorking = false;
                IsFunctional = false;
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void Close()
        {
            try
            {
                if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
                if (ShieldComp?.Modulator == this)
                {
                    ShieldComp.Modulator = null;
                }

                if (ModulatorComp?.Modulator == this)
                {
                    ModulatorComp.Modulator = null;
                    ModulatorComp = null;
                }
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

        internal void UpdateSettings(ModulatorSettingsValues newSettings)
        {
            SettingsUpdated = true;
            ModSet.Settings = newSettings;
            if (Session.Enforced.Debug == 3) Log.Line("UpdateSettings for modulator");
        }

        internal void UpdateState(ModulatorStateValues newState)
        {
            ModState.State = newState;
            if (Session.Enforced.Debug == 3) Log.Line($"UpdateState - ModulatorId [{Modulator.EntityId}]:\n{ModState.State}");
        }

        private void UpdateStates()
        {
            if (_tock60 || _firstRun)
            {
                if (Modulator.CustomData != ModulatorComp.ModulationPassword)
                {
                    ModulatorComp.ModulationPassword = Modulator.CustomData;
                    ModSet.SaveSettings();
                    if (Session.Enforced.Debug == 3) Log.Line("Updating modulator password");
                }
            }
        }

        private void ModulatorOff()
        {
            var stateChange = StateChange();

            if (stateChange)
            {
                if (_isServer)
                {
                    NeedUpdate();
                    StateChange(true);
                    Modulator.RefreshCustomInfo();
                }
                else
                {
                    StateChange(true);
                    Modulator.RefreshCustomInfo();
                }
            }
        }

        private void ModulatorOn()
        {
            if (_isServer && StateChange())
            {
                NeedUpdate();
                StateChange(true);
                Modulator.RefreshCustomInfo();
            }
        }

        private bool ModulatorReady()
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null)
                {
                    if (_isServer) ModState.State.Online = false;
                    return false;
                }
            }
            if (ModulatorComp?.Modulator?.MyGrid != MyGrid) ResetComp();

            if (_isServer)
            {
                if (!BlockWorking())
                {
                    ModState.State.Online = false;
                    return false;
                }
            }
            else
            {
                if (!ModState.State.Online) return false;
                if (_tock60 || _firstRun) ClientCheckForCompLink();
            }
            return true;
        }

        private bool BlockWorking()
        {
            if (_tock60 || _firstRun) _powered = Sink.IsPowerAvailable(_gId, 0.01f);
            if (!IsWorking || !_powered)
            {
                if (!_isDedicated && _tock60)
                {
                    Modulator.RefreshCustomInfo();
                }
                ModState.State.Online = false;
                return false;
            }
            if (ModulatorComp.Modulator == null) ModulatorComp.Modulator = this;
            else if (ModulatorComp.Modulator != this)
            {
                ModState.State.Backup = true;
                ModState.State.Online = false;
                return false;
            }

            ModState.State.Backup = false;

            if (_tock60 || _firstRun) ServerCheckForCompLink();
            ModState.State.Online = true;
            return true;
        }

        private void ServerCheckForCompLink()
        {
            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);

            if (ShieldComp?.DefenseShields == null) return;

            if (ShieldComp?.Modulator != this)
            {
                if (ShieldComp.Modulator != this) ShieldComp.Modulator = this;
                ModState.State.Link = true;
            }

            var wasLink = EnhancerLink;
            if (ModState.State.Link && ShieldComp.Enhancer != null && ShieldComp.Enhancer.IsWorking)
            {
                EnhancerLink = true;
                if (ShieldComp.DefenseShields.IsStatic) ModSet.Settings.EmpEnabled = true;
            }
            else EnhancerLink = false;

            if (!EnhancerLink && EnhancerLink != wasLink)
            {
                ModSet.Settings.ReInforceEnabled = false;
                ModSet.Settings.EmpEnabled = false;
            }
            else if (ModState.State.Link && ShieldComp.DefenseShields.IsStatic) ModSet.Settings.ReInforceEnabled = false;
        }

        private void ClientCheckForCompLink()
        {
            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);

            if (ShieldComp?.DefenseShields == null) return;

            if (ModState.State.Link && ShieldComp?.Modulator != this)
            {
                if (ShieldComp.Modulator != this) ShieldComp.Modulator = this;
            }
            EnhancerLink = ShieldComp.DefenseShields.DsState.State.Enhancer;
        }

        private void Timing()
        {
            if (_tock60 && !_isDedicated && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel && Session.Instance.LastTerminalId == Modulator.EntityId)
            {
                Modulator.RefreshCustomInfo();
                MyCube.UpdateTerminal();
            }

            if (_tock33)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    ModSet.SaveSettings();
                    ModState.SaveState();
                    if (Session.Enforced.Debug == 3) Log.Line($"SettingsUpdated: server:{_isServer} - ModulatorId [{Modulator.EntityId}]");
                }
            }
            else if (_tock34)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    MyCube.UpdateTerminal();
                    Modulator.RefreshCustomInfo();
                    if (!_isServer) ModSet.NetworkUpdate();
                }
            }

            if (_isDedicated || (_subDelayed && _tick > _subTick + 9))
            {
                if (Session.Enforced.Debug == 3) Log.Line($"Delayed tick: {_tick} - hierarchytick: {_subTick}");
                _subDelayed = false;
                HierarchyChanged();
            }
        }

        private bool StateChange(bool update = false)
        {
            if (update)
            {
                _wasOnline = ModState.State.Online;
                _wasLink = ModState.State.Link;
                _wasBackup = ModState.State.Backup;
                _wasModulateDamage = ModState.State.ModulateDamage;
                _wasModulateEnergy = ModState.State.ModulateEnergy;
                _wasModulateKinetic = ModState.State.ModulateKinetic;
                return true;
            }

            var change = _wasOnline != ModState.State.Online || _wasLink != ModState.State.Link || _wasBackup != ModState.State.Backup 
                   || _wasModulateDamage != ModState.State.ModulateDamage || !_wasModulateEnergy.Equals(ModState.State.ModulateEnergy) 
                   || !_wasModulateKinetic.Equals(ModState.State.ModulateKinetic);
            return change;
        }

        private void NeedUpdate()
        {
            ModState.SaveState();
            if (Session.Instance.MpActive) ModState.NetworkUpdate();
        }

        private void ResetComp()
        {
            ModulatorGridComponent comp;
            Modulator.CubeGrid.Components.TryGet(out comp);
            if (comp == null)
            {
                ModulatorComp = new ModulatorGridComponent(this);
                Modulator.CubeGrid.Components.Add(ModulatorComp);
            }
            else Modulator.CubeGrid.Components.TryGet(out ModulatorComp);
        }

        private void PowerPreInit()
        {
            try
            {
                if (Sink == null)
                {
                    Sink = new MyResourceSinkComponent();
                }
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = _gId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Modulator.Enabled;
                if (enableState)
                {
                    Modulator.Enabled = false;
                    Modulator.Enabled = true;
                }
                Sink.Update();
                IsWorking = MyCube.IsWorking;
                if (Session.Enforced.Debug == 3) Log.Line($"PowerInit: ModulatorId [{Modulator.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private void StorageSetup()
        {
            if (ModSet == null) ModSet = new ModulatorSettings(Modulator);
            if (ModState == null) ModState = new ModulatorState(Modulator);
            ModState.StorageInit();

            ModSet.LoadSettings();
            ModState.LoadState();
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (ModulatorComp == null || ModState.State.Backup || ShieldComp?.DefenseShields != null || (!_isDedicated && _tick == _subTick)) return;
                if (!_isDedicated && _subTick > _tick - 9)
                {
                    _subDelayed = true;
                    return;
                }
                _subTick = _tick;
                var gotGroups = MyAPIGateway.GridGroups.GetGroup(Modulator?.CubeGrid, GridLinkTypeEnum.Mechanical);
                ModulatorComp.SubGrids.Clear();
                for (int i = 0; i < gotGroups.Count; i++)
                {
                    var sub = gotGroups[i];
                    ModulatorComp.SubGrids.Add((MyCubeGrid)sub, null); 
                }
            }
            catch (Exception ex) { Log.Line($"Exception in HierarchyChanged: {ex}"); }
        }

        private void CreateUi()
        {
            ModUi.CreateUi(Modulator);
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            stringBuilder.Append("[Online]: " + ModState.State.Online +
                                 "\n[Remodulating Shield]: " + ModState.State.Link +
                                 "\n" +
                                 "\n[Backup Modulator]: " + ModState.State.Backup +
                                 "\n[Energy Protection]: " + ModState.State.ModulateEnergy.ToString("0") + "%" +
                                 "\n[Kinetic Protection]: " + ModState.State.ModulateKinetic.ToString("0") + "%" +
                                 "\n[Emp Protection]: " + ModSet.Settings.EmpEnabled);
        }

        private bool BlockMoveAnimationReset()
        {
            if (!IsFunctional) return false;
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }

            if (!_subpartRotor.Closed) return true;

            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            return true;
        }

        private void BlockMoveAnimation()
        {
            if (!BlockMoveAnimationReset()) return;
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.00625f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                MyGrid.OnHierarchyUpdated += HierarchyChanged;
                Modulator.AppendingCustomInfo += AppendingCustomInfo;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
            }
            else
            {
                MyGrid.OnHierarchyUpdated -= HierarchyChanged;
                Modulator.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsFunctional = myCubeBlock.IsFunctional;
            IsWorking = myCubeBlock.IsWorking;
        }
    }
}

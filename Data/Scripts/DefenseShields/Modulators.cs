using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
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

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldModulator", "SmallShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {

        private bool _subDelayed;
        internal int RotationTime;
        internal bool MainInit;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool ContainerInited;
        private bool _powered;

        private uint _tick;
        private uint _subTick = 1;

        private int _count = -1;
        private int _lCount;

        private float _power = 0.01f;

        private bool _tick60;
        private bool _isServer;
        private bool _isDedicated;
        private bool _wasOnline;
        private bool _wasLink;
        private bool _wasBackup;
        private bool _firstRun = true;
        private int _wasModulateDamage;
        private float _wasModulateEnergy;
        private float _wasModulateKinetic;

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        internal ModulatorGridComponent ModulatorComp;
        internal ShieldGridComponent ShieldComp;
        private MyEntitySubpart _subpartRotor;
        internal ModulatorSettings ModSet;
        internal ModulatorState ModState;
        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        public IMyUpgradeModule Modulator => (IMyUpgradeModule)Entity;
        internal MyCubeGrid MyGrid;

        internal DSUtils Dsutil1 = new DSUtils();

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = Session.Instance.Tick;
                _tick60 = _tick % 60 == 0;
                var wait = _isServer && !_tick60 && ModState.State.Backup;

                MyGrid = Modulator.CubeGrid as MyCubeGrid;
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
                    var blockCam = Modulator.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && Modulator.IsWorking) BlockMoveAnimation();
                }

                if (_isServer)
                {
                    if (ShieldComp?.GetSubGrids != null && !ShieldComp.GetSubGrids.Equals(ModulatorComp.GetSubGrids))
                        ModulatorComp.GetSubGrids = ShieldComp.GetSubGrids;

                    if (_count == 0 || _firstRun)
                    {
                        Modulator.RefreshCustomInfo();
                        if (Modulator.CustomData != ModulatorComp.ModulationPassword)
                        {
                            ModulatorComp.ModulationPassword = Modulator.CustomData;
                            ModSet.SaveSettings();
                            if (Session.Enforced.Debug >= 1) Log.Line($"Updating modulator password");
                        }
                    }
                }
                else if (_count == 0) Modulator.RefreshCustomInfo();
                _firstRun = false;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
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
            if (ModulatorComp == null) ResetComp();

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
                if (_count == 29 || _firstRun) ClientCheckForCompLink();
            }
            return true;
        }

        private bool BlockWorking()
        {
            if (_count <= 0) _powered = Sink.IsPowerAvailable(GId, 0.01f);
            if (!Modulator.IsWorking || !_powered)
            {
                if (!_isDedicated && _count == 29)
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

            if (_count == 59 && _lCount == 9 || _firstRun) ServerCheckForCompLink();
            ModState.State.Online = true;
            return true;
        }

        private void ServerCheckForCompLink()
        {
            MyGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp?.DefenseShields == null) return;

            if (ShieldComp?.Modulator != this)
            {
                if (ShieldComp.Modulator != this) ShieldComp.Modulator = this;
                ModState.State.Link = true;
            }
        }

        private void ClientCheckForCompLink()
        {
            MyGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp?.DefenseShields == null) return;

            if (ModState.State.Link && ShieldComp?.Modulator != this)
            {
                if (ShieldComp.Modulator != this) ShieldComp.Modulator = this;
            }
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }
            if (_count == 29 && !_isDedicated && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Modulator.RefreshCustomInfo();
            }

            if (_count == 33)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    ModSet.SaveSettings();
                    ModState.SaveState();
                    if (Session.Enforced.Debug >= 1) Log.Line($"SettingsUpdated: server:{Session.IsServer} - ModulatorId [{Modulator.EntityId}]");
                }
            }
            else if (_count == 34)
            {
                if (ClientUiUpdate && !_isServer)
                {
                    ClientUiUpdate = false;
                    ModSet.NetworkUpdate();
                }
            }

            if (_subDelayed && _tick > _subTick + 9)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"Delayed tick: {_tick} - hierarchytick: {_subTick}");
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
            if (Session.MpActive) ModState.NetworkUpdate();
        }

        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
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

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Modulator.CubeGrid.Physics == null) return;

                _isServer = Session.IsServer;
                _isDedicated = Session.DedicatedServer;

                ResetComp();

                Session.Instance.Modulators.Add(this);
                _modulators.Add(Entity.EntityId, this);

                CreateUi();
                ModUi.ComputeDamage(this, ModUi.GetDamage(Modulator));

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                PowerInit();
                RegisterEvents();
                Modulator.RefreshCustomInfo();
                StateChange(true);
                MainInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
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

        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                Modulator.AppendingCustomInfo += AppendingCustomInfo;
            }
            else
            {
                ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;
                Modulator.AppendingCustomInfo -= AppendingCustomInfo;
            }
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
                    ResourceTypeId = GId,
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
                if (Session.Enforced.Debug >= 2) Log.Line($"PowerInit: ModulatorId [{Modulator.EntityId}]");
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
                if (_tick == _subTick || ShieldComp?.DefenseShields != null) return;
                if (_subTick > _tick - 9)
                {
                    _subDelayed = true;
                    return;
                }
                _subTick = _tick;
                var gotGroups = MyAPIGateway.GridGroups.GetGroup(Modulator?.CubeGrid, GridLinkTypeEnum.Mechanical);
                ModulatorComp?.GetSubGrids?.Clear();
                for (int i = 0; i < gotGroups.Count; i++)
                {
                    var sub = gotGroups[i];
                    if (sub == null) continue;
                    ModulatorComp?.GetSubGrids?.Add(sub as MyCubeGrid);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in HierarchyChanged: {ex}"); }
        }

        #region Create UI
        private void CreateUi()
        {
            ModUi.CreateUi(Modulator);
        }
        #endregion

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
            if (!Modulator.IsFunctional) return false;
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            if (_subpartRotor.Closed) _subpartRotor.Subparts.Clear();
            return true;
        }

        private void BlockMoveAnimation()
        {
            BlockMoveAnimationReset();
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.00625f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        public void UpdateSettings(ProtoModulatorSettings newSettings)
        {
            SettingsUpdated = true;
            ModSet.Settings = newSettings;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateSettings for modulator");
        }

        public void UpdateState(ProtoModulatorState newState)
        {
            ModState.State = newState;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateState - ModulatorId [{Modulator.EntityId}]:\n{ModState.State}");
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

        public override void OnAddedToScene()
        {
            try
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"OnAddedToScene: - ModulatorId [{Modulator.EntityId}]");
                if (!MainInit) return;
                ResetComp();
                RegisterEvents();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
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
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
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
                RegisterEvents(false);
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
    }
}

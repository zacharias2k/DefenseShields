using System;
using System.Text;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseSystems
{
    public partial class Modulators 
    {
        private void BeforeInit()
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
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _bTime = _isDedicated ? 10 : 1;
            _bInit = true;
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
                if (!_firstSync && _readyToSync) SaveAndSendAll();

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
                if (!ModState.State.Backup || _firstLoop) Session.Instance.BlockTagBackup(Modulator);
                ModState.State.Backup = true;
                ModState.State.Online = false;
                _firstLoop = false;
                return false;
            }

            ModState.State.Backup = false;

            if (_tock60 || _firstRun) ServerCheckForCompLink();
            ModState.State.Online = true;
            _firstLoop = false;
            return true;
        }

        private void ServerCheckForCompLink()
        {
            if (Bus?.Spine != MyGrid) MyGrid.Components.TryGet(out Bus);

            if (Bus?.ActiveController == null) return;

            if (Bus?.ActiveModulator != this)
            {
                if (Bus.ActiveModulator != this)
                {
                    Bus.ActiveModulator = this;
                    Session.Instance.BlockTagActive(Modulator);
                }
                ModState.State.Link = true;
            }

            var wasLink = EnhancerLink;
            if (ModState.State.Link && Bus.ActiveEnhancer != null && Bus.ActiveEnhancer.IsWorking)
            {
                EnhancerLink = true;
                if (Bus.IsStatic) ModSet.Settings.EmpEnabled = true;
            }
            else EnhancerLink = false;

            if (!EnhancerLink && EnhancerLink != wasLink)
            {
                ModSet.Settings.EmpEnabled = false;
            }
        }

        private void ClientCheckForCompLink()
        {
            if (Bus?.Spine != MyGrid) MyGrid.Components.TryGet(out Bus);

            if (Bus?.ActiveController == null) return;

            if (ModState.State.Link && Bus?.ActiveModulator != this)
            {
                if (Bus.ActiveModulator != this) Bus.ActiveModulator = this;
            }
            EnhancerLink = Bus.ActiveController.DsState.State.Enhancer;
        }

        private void Timing()
        {
            if (_tock60 && !_isDedicated)
            {
                TerminalRefresh();
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

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            ModSet.SaveSettings();
            ModState.SaveState();
            ModSet.NetworkUpdate();
            ModState.NetworkUpdate();
            if (Session.Enforced.Debug >= 3) Log.Line($"SaveAndSendAll: ModualtorId [{Modulator.EntityId}]");

        }

        internal void TerminalRefresh(bool update = true)
        {
            Modulator.RefreshCustomInfo();
            if (update && InControlPanel && InThisTerminal)
            {
                var mousePos = MyAPIGateway.Input.GetMousePosition();
                var startPos = new Vector2(800, 700);
                var endPos = new Vector2(1070, 750);
                var match1 = mousePos.Between(ref startPos, ref endPos);
                var match2 = mousePos.Y > 700 && mousePos.Y < 760 && mousePos.X > 810 && mousePos.X < 1070;
                if (!(match1 && match2)) MyCube.UpdateTerminal();
            }
        }

        private bool StateChange(bool update = false)
        {
            if (update)
            {
                _modulatorFailed = ModState.State.Online;
                _wasLink = ModState.State.Link;
                _wasBackup = ModState.State.Backup;
                _wasModulateDamage = ModState.State.ModulateDamage;
                _wasModulateEnergy = ModState.State.ModulateEnergy;
                _wasModulateKinetic = ModState.State.ModulateKinetic;
                return true;
            }

            var change = _modulatorFailed != ModState.State.Online || _wasLink != ModState.State.Link || _wasBackup != ModState.State.Backup
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
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                ModState.State.Backup = false;
                ModState.State.Online = false;
                ModState.State.Link = false;
            }
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (ModulatorComp == null || ModState.State.Backup || Bus?.ActiveController != null || (!_isDedicated && _tick == _subTick)) return;
                if (!_isDedicated && _subTick > _tick - 9)
                {
                    _subDelayed = true;
                    return;
                }
                _subTick = _tick;
                var gotGroups = MyAPIGateway.GridGroups.GetGroup(Modulator?.CubeGrid, GridLinkTypeEnum.Mechanical);
                ModulatorComp.SubGrids.Clear();
                for (int i = 0; i < gotGroups.Count; i++) ModulatorComp.SubGrids.Add(gotGroups[i] as MyCubeGrid);
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

        internal void UpdateSettings(ModulatorSettingsValues newSettings)
        {
            if (newSettings.MId > ModSet.Settings.MId)
            {
                SettingsUpdated = true;
                ModSet.Settings = newSettings;
                if (Session.Enforced.Debug == 3) Log.Line("UpdateSettings for modulator");
            }
        }

        internal void UpdateState(ModulatorStateValues newState)
        {
            if (newState.MId > ModState.State.MId)
            {
                ModState.State = newState;
                if (Session.Enforced.Debug == 3) Log.Line($"UpdateState - ModulatorId [{Modulator.EntityId}]:\n{ModState.State}");
            }
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

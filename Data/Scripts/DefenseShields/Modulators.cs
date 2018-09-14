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

        private bool _wasOnline;
        private bool _wasLink;
        private bool _wasBackup;
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

        internal DSUtils Dsutil1 = new DSUtils();

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Modulator.CubeGrid.Physics == null) return;
                var isServer = Session.IsServer;
                _tick = Session.Instance.Tick;
                Timing();

                if (!ModulatorReady(isServer))
                {
                    ModulatorOff(isServer);
                    return;
                }
                ModulatorOn(isServer);

                if (!Session.DedicatedServer && UtilsStatic.DistanceCheck(Modulator, 1000, 1))
                {
                    var blockCam = Modulator.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && Modulator.IsWorking) BlockMoveAnimation();
                }

                if (isServer)
                {
                    if (ShieldComp?.GetSubGrids != null && !ShieldComp.GetSubGrids.Equals(ModulatorComp.GetSubGrids))
                        ModulatorComp.GetSubGrids = ShieldComp.GetSubGrids;

                    if (_count == 0)
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
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void ModulatorOff(bool isServer)
        {
            var stateChange = StateChange();

            if (stateChange)
            {
                if (isServer)
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

        private void ModulatorOn(bool isServer)
        {
            if (isServer && StateChange())
            {
                NeedUpdate();
                StateChange(true);
                Modulator.RefreshCustomInfo();
            }
        }

        private void Election()
        {
            if (ModulatorComp == null || !Modulator.CubeGrid.Components.Has<ModulatorGridComponent>())
            {
                var girdHasModComp = Modulator.CubeGrid.Components.Has<ModulatorGridComponent>();

                if (girdHasModComp)
                {
                    Modulator.CubeGrid.Components.TryGet(out ModulatorComp);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Election: grid had Comp- ModulatorId [{Modulator.EntityId}]");
                }
                else
                {
                    ModulatorComp = new ModulatorGridComponent(this);
                    Modulator.CubeGrid.Components.Add(ModulatorComp);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Election: grid didn't have Comp - ModulatorId [{Modulator.EntityId}]");
                }
                Modulator.RefreshCustomInfo();
            }
        }

        private bool ModulatorReady(bool isServer)
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null)
                {
                    if (isServer) ModState.State.Online = false;
                    return false;
                }
            }
            if (ModulatorComp == null) Election();

            if (isServer)
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
                if (_count == 29) ClientCheckForCompLink();
            }
            return true;
        }

        private bool BlockWorking()
        {
            if (_count <= 0) _powered = Sink.IsPowerAvailable(GId, 0.01f);
            if (Modulator?.CubeGrid == null || !Modulator.Enabled || !Modulator.IsFunctional || !_powered)
            {
                if (Modulator != null && _count == 29)
                {
                    Modulator.RefreshCustomInfo();
                    Modulator.ShowInToolbarConfig = false;
                    Modulator.ShowInToolbarConfig = true;
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

            if (_count == 59 && _lCount == 9) ServerCheckForCompLink();
            ModState.State.Online = true;
            return true;
        }

        private void ServerCheckForCompLink()
        {
            if (ShieldComp == null || ShieldComp?.Modulator != this)
            {
                if (ShieldComp?.DefenseShields == null)
                {
                    Modulator.CubeGrid.Components.TryGet(out ShieldComp);
                    if (ShieldComp?.DefenseShields == null) return;
                }
                if (ShieldComp.Modulator != this) ShieldComp.Modulator = this;
                ModState.State.Link = true;
            }
        }

        private void ClientCheckForCompLink()
        {
            if (ModState.State.Link && (ShieldComp == null || ShieldComp?.Modulator != this))
            {
                if (ShieldComp?.DefenseShields == null)
                {
                    Modulator.CubeGrid.Components.TryGet(out ShieldComp);
                    if (ShieldComp?.DefenseShields == null) return;
                }
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

            if (_count == 29 && !Session.DedicatedServer && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Modulator.RefreshCustomInfo();
                Modulator.ShowInToolbarConfig = false;
                Modulator.ShowInToolbarConfig = true;
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
                if (ClientUiUpdate && !Session.IsServer)
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

                if (!Modulator.CubeGrid.Components.Has<ModulatorGridComponent>())
                {
                    ModulatorComp = new ModulatorGridComponent(this);
                    Modulator.CubeGrid.Components.Add(ModulatorComp);
                }
                else
                {
                    Modulator.CubeGrid.Components.TryGet(out ModulatorComp);
                    if (ModulatorComp != null && ModulatorComp.Modulator == null) ModulatorComp.Modulator = this;
                }
                Session.Instance.Modulators.Add(this);
                _modulators.Add(Entity.EntityId, this);

                CreateUi();
                ModUi.ComputeDamage(this, ModUi.GetDamage(Modulator));

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                PowerInit();
                ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                Modulator.AppendingCustomInfo += AppendingCustomInfo;
                Modulator.RefreshCustomInfo();
                StateChange(true);
                MainInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
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
                if (Session.Enforced.Debug >= 1) Log.Line($"PowerInit: ModulatorId [{Modulator.EntityId}]");
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
                                 "\n[Kinetic Protection]: " + ModState.State.ModulateKinetic.ToString("0") + "%");
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
            if (Session.IsServer)
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
                if ((MyCubeGrid)Modulator?.CubeGrid != null) ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;

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

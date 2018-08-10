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

        private bool _hierarchyDelayed;
        internal int RotationTime;
        internal bool MainInit;
        internal bool SettingsUpdated;

        private uint _tick;
        private uint _hierarchyTick = 1;

        private int _count = -1;
        private int _lCount;

        private float _power = 0.01f;

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        public MyModStorageComponentBase Storage { get; set; }
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
                _tick = Session.Instance.Tick;
                var isServer = Session.IsServer;

                if (!ModulatorReady(isServer)) return;

                Timing();

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
                        if (ShieldComp == null) Modulator.CubeGrid.Components.TryGet(out ShieldComp);
                        if (ShieldComp == null) return;
                        if (!ModState.State.Online) NeedUpdate();

                        Modulator.RefreshCustomInfo();

                        if (Modulator.CustomData != ModulatorComp.ModulationPassword)
                        {
                            ModulatorComp.ModulationPassword = Modulator.CustomData;
                            ModSet.SaveSettings();
                            if (Session.Enforced.Debug == 1) Log.Line($"Updating modulator password");
                        }
                    }
                }
                else if (_count == 0) Modulator.RefreshCustomInfo();

            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private bool ModulatorReady(bool server)
        {
            if (server)
            {
                if (ModulatorComp == null)
                {
                    Modulator.CubeGrid.Components.Add(new ModulatorGridComponent(this));
                }
                else if (ModulatorComp != null && ModulatorComp.Modulators == null) ModulatorComp.Modulators = this;
                else if (ModulatorComp != null && ModulatorComp.Modulators != this)
                {
                    if (ModState.State.Online)
                    {
                        ModState.State.Online = false;
                        NeedUpdate(true);
                    }
                    return false;
                }

                if (Sink.CurrentInputByType(GId) < 0.01f || Modulator?.CubeGrid == null || !Modulator.Enabled || !Modulator.IsFunctional)
                {
                    if (Modulator != null && _tick % 300 == 0)
                    {
                        Modulator.RefreshCustomInfo();
                        Modulator.ShowInToolbarConfig = false;
                        Modulator.ShowInToolbarConfig = true;
                    }

                    if (ModState.State.Online)
                    {
                        ModState.State.Online = false;
                        NeedUpdate(true);
                    }
                    return false;
                }
            }
            else if (!ModState.State.Online || !Modulator.IsFunctional) return false;

            return _subpartRotor != null || BlockMoveAnimationReset();
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Modulator.RefreshCustomInfo();
                Modulator.ShowInToolbarConfig = false;
                Modulator.ShowInToolbarConfig = true;
            }

            if ((_lCount == 1 || _lCount == 6) && _count == 1)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    ModSet.SaveSettings();
                }
            }

            if (_hierarchyDelayed && _tick > _hierarchyTick + 9)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Delayed tick: {_tick} - hierarchytick: {_hierarchyTick}");
                _hierarchyDelayed = false;
                HierarchyChanged();
            }
        }

        private void NeedUpdate(bool force = false)
        {
            if (!force) ModState.State.Online = true;
            ModState.NetworkUpdate();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
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
                    Modulator.CubeGrid.Components.Add(new ModulatorGridComponent(this));

                Modulator.CubeGrid.Components.TryGet(out ModulatorComp);
                Modulator.CubeGrid.Components.TryGet(out ShieldComp);

                Session.Instance.Modulators.Add(this);
                _modulators.Add(Entity.EntityId, this);

                StorageSetup();
                CreateUi();
                ModUi.ComputeDamage(this, ModUi.GetDamage(Modulator));

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                PowerInit();
                ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                Modulator.AppendingCustomInfo += AppendingCustomInfo;
                Modulator.RefreshCustomInfo();
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
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit complete");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Modulator.Storage;
            if (ModSet == null) ModSet = new ModulatorSettings(Modulator);

            if (ModState == null) ModState = new ModulatorState(Modulator);
            ModSet.LoadSettings();
            ModState.LoadState();
            //ModState.LoadState();
            UpdateSettings(ModSet.Settings);
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (_tick == _hierarchyTick || ShieldComp?.DefenseShields != null) return;
                if (_hierarchyTick > _tick - 9)
                {
                    _hierarchyDelayed = true;
                    return;
                }
                _hierarchyTick = _tick;
                var gotGroups = MyAPIGateway.GridGroups.GetGroup(Modulator?.CubeGrid, GridLinkTypeEnum.Mechanical);
                ModulatorComp?.GetSubGrids?.Clear();
                for (int i = 0; i < gotGroups.Count; i++)
                {
                    var sub = gotGroups[i];
                    if (sub == null) continue;
                    ModulatorComp?.GetSubGrids?.Add(sub);
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
                                 "\n[Remodulating Shield]: " + (ModState.State.Online) +
                                 "\n" +
                                 "\n[Energy Protection]: " + ModState.State.ModulateEnergy.ToString("0") + "%" +
                                 "\n[Kinetic Protection]: " + ModState.State.ModulateKinetic.ToString("0") + "%");
        }

        private bool BlockMoveAnimationReset()
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            _subpartRotor.Subparts.Clear();
            return true;
        }

        private void BlockMoveAnimation()
        {
            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.00625f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        public void UpdateSettings(ProtoModulatorSettings newSettings)
        {
            SettingsUpdated = true;
            ModSet.Settings = newSettings;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for modulator");
        }

        public void UpdateState(ProtoModulatorState newState)
        {
            ModState.State = newState;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateState - ModId [{Modulator.EntityId}]:\n{ModState.State}");
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (Session.Instance.Modulators.Contains(this)) Session.Instance.Modulators.Remove(this);
                if (Modulator.CubeGrid.Components.Has<ModulatorGridComponent>()) Modulator.CubeGrid.Components.Remove<ModulatorGridComponent>();
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
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
    }
}

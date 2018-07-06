using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeShieldModulator", "SmallShieldModulator")]
    public class Modulators : MyGameLogicComponent
    {

        public bool ServerUpdate;
        private bool _hierarchyDelayed;
        internal int RotationTime;
        internal bool MainInit;

        private uint _tick;
        private uint _hierarchyTick = 1;

        private int _count = -1;
        private int _lCount;

        private readonly Dictionary<long, Modulators> _modulators = new Dictionary<long, Modulators>();

        public MyModStorageComponentBase Storage { get; set; }
        internal ModulatorGridComponent ModulatorComp;
        internal ShieldGridComponent ShieldComp;
        private MyEntitySubpart _subpartRotor;
        internal ModulatorSettings ModSet;

        public IMyUpgradeModule Modulator => (IMyUpgradeModule)Entity;

        internal DSUtils Dsutil1 = new DSUtils();

        public Modulators()
        {
            ModulatorComp = new ModulatorGridComponent(this);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                StorageSetup();

                if (!Modulator.CubeGrid.Components.Has<ModulatorGridComponent>())
                    Modulator.CubeGrid.Components.Add(ModulatorComp);
                else Modulator.CubeGrid.Components.TryGet(out ModulatorComp);
                Modulator.CubeGrid.Components.TryGet(out ShieldComp);
                ShieldComp?.DefenseShields?.GetModulationInfo();
                Session.Instance.Modulators.Add(this);
                _modulators.Add(Entity.EntityId, this);
                CreateUi();
                ((MyCubeGrid)Modulator.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                Modulator.AppendingCustomInfo += AppendingCustomInfo;
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Modulator.Storage;
            ModSet = new ModulatorSettings(Modulator);
            ModSet.LoadSettings();
            //ModulatorComp = new ModulatorGridComponent(this);
            //UpdateSettings(Settings, false);
        }

        private void HierarchyChanged(IMyCubeGrid myCubeGrid = null)
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
                for (int i = 0; i < gotGroups.Count; i++) ModulatorComp?.GetSubGrids?.Add(gotGroups[i]);
            }
            catch (Exception ex) { Log.Line($"Exception in HierarchyChanged: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                //Log.Line($"{ModSet.Settings.ModulateVoxels}");
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                Timing();

                if (UtilsStatic.DistanceCheck(Modulator, 1000, 1))
                {
                    var blockCam = Modulator.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && Modulator.IsWorking) BlockMoveAnimation();
                }

                if (ShieldComp?.GetSubGrids != null && !ShieldComp.GetSubGrids.Equals(ModulatorComp.GetSubGrids))
                    ModulatorComp.GetSubGrids = ShieldComp.GetSubGrids;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation100: {ex}"); }
        }

        public override void UpdateBeforeSimulation100()
        {
            try
            {
                if (ShieldComp == null) Modulator.CubeGrid.Components.TryGet(out ShieldComp);
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                Modulator.RefreshCustomInfo();

                if (Modulator.CustomData != ModulatorComp.ModulationPassword)
                {
                    ModulatorComp.ModulationPassword = Modulator.CustomData;
                    ModSet.SaveSettings();
                    if (Session.Enforced.Debug == 1) Log.Line($"Updating modulator password");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation100: {ex}"); }
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

            if (_hierarchyDelayed && _tick > _hierarchyTick + 9)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Delayed tick: {_tick} - hierarchytick: {_hierarchyTick}");
                _hierarchyDelayed = false;
                HierarchyChanged();
            }
        }

        #region Create UI
        private void CreateUi()
        {
            ModUi.CreateUi(Modulator);
        }
        #endregion

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (ModulatorComp.Damage < 100)
            {
                ModulatorComp.Energy = 200 - ModulatorComp.Damage;
                ModulatorComp.Kinetic = ModulatorComp.Damage;
            }
            else if (ModulatorComp.Damage > 100)
            {
                ModulatorComp.Energy = 200 - ModulatorComp.Damage;
                ModulatorComp.Kinetic = ModulatorComp.Damage;
            }
            else
            {
                ModulatorComp.Kinetic = ModulatorComp.Damage;
                ModulatorComp.Energy = ModulatorComp.Damage;
            }
            stringBuilder.Append("\n[Energy Protection_]: " + ModulatorComp.Energy.ToString("0") + "%" +
                                 "\n[Kinetic Protection]: " + ModulatorComp.Kinetic.ToString("0") + "%");
        }

        private void BlockMoveAnimationReset()
        {
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.00625f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        public bool Enabled
        {
            get { return ModSet.Settings.Enabled; }
            set { ModSet.Settings.Enabled = value; }
        }

        public void UpdateSettings(ModulatorBlockSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            ModulatorComp.Enabled = newSettings.Enabled;
            ModulatorComp.Voxels = newSettings.ModulateVoxels;
            ModulatorComp.Grids = newSettings.ModulateGrids;
            ModulatorComp.Damage = newSettings.ModulateDamage;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for modulator");
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

using VRageMath;
using System;
using DefenseSystems.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
namespace DefenseSystems
{


    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DSControlLarge", "DSControlSmall", "DSControlTable")]
    public partial class Controllers : MyGameLogicComponent
    {
        #region Simulation
        public override void OnAddedToContainer()
        {
            if (!_containerInited)
            {
                PowerPreInit();
                Controller = (IMyUpgradeModule)Entity;
                _containerInited = true;
            }

            if (Entity.InScene) OnAddedToScene();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            StorageSetup();
        }

        public override void OnAddedToScene()
        {
            try
            {
                if (!ResetEntity()) return;
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: GridId:{Controller.CubeGrid.EntityId} - ControllerId [{Controller.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (!_bInit) BeforeInit();
                else if (!_aInit) AfterInit();
                else if (_bCount < SyncCount * _bTime)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    if (Bus.ActiveEmitter != null || Bus.ActiveRegen != null)
                    {
                        NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                        if (Bus.ActiveController != null && Bus.ActiveController.Warming) _bCount++;
                    }
                }
                else _readyToSync = true;

            }
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!EntityAlive()) return;
                var fieldMode = DsState.State.ProtectMode != 2;

                var protect = ProtectionOn(fieldMode);
                if (protect != State.Active)
                {
                    if (_tick1800 && Bus.ActiveController == this) Log.Line($"NotActive: {protect} - {Bus.MyResourceDist.SourcesEnabled} - {Bus.MyResourceDist.ResourceStateByType(GId)} - {Bus.MyResourceDist.MaxAvailableResourceByType(GId)} - {_shieldMaintaintPower} - {ShieldCurrentPower} - {_sink.CurrentInputByType(GId)}");
                    if (NotFailed)
                    {
                        if (Session.Enforced.Debug >= 2) Log.Line($"FailState: {protect} - ControllerId [{Controller.EntityId}]");
                        if (fieldMode)
                        {
                            var up = protect != State.Lowered;
                            var awake = protect != State.Sleep;
                            var clear = up && awake;
                            OfflineShield(clear, up, protect);
                        }
                        else
                        {
                            NotFailed = false;
                            ProtChangedState();
                            //other stuff
                        }
                    }
                    else if (DsState.State.Message) ProtChangedState();
                    return;
                }
                if (!_isServer || !DsState.State.Online) return;
                if (_comingOnline) ComingOnline();
                if (_mpActive && (_forceBufferSync || _count == 29))
                {
                    var newPercentColor = UtilsStatic.GetShieldColorFromFloat(DsState.State.ShieldPercent);
                    if (_forceBufferSync || newPercentColor != _oldPercentColor)
                    {
                        ProtChangedState();
                        _oldPercentColor = newPercentColor;
                        _forceBufferSync = false;
                    }
                    else if (_tick1800) ProtChangedState();
                }
                if (Session.Instance.EmpWork.EventRunning) AbsorbEmp();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Controller.Storage != null)
                {
                    DsState.SaveState();
                    DsSet.SaveSettings();
                }
            }
            return false;
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!_allInited) return;
                if (Session.Enforced.Debug >= 3) Log.Line($"OnRemovedFromScene: GridId:{Controller.CubeGrid.EntityId} - ControllerId [{Controller.EntityId}]");

                if (Bus != null && Bus.SubGrids.Contains(LocalGrid))
                {
                    if (Bus.ActiveController == this) OfflineShield(true, false, State.Other, true);
                    Registry.RegisterWithBus(this, LocalGrid, false, Bus, out Bus);
                }

                InitEntities(false);
                IsWorking = false;
                IsFunctional = false;
                _shellPassive?.Render?.RemoveRenderObjects();
                _shellActive?.Render?.RemoveRenderObjects();
                ShieldEnt?.Render?.RemoveRenderObjects();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }

        public override void Close()
        {
            try
            {
                base.Close();
                if (!_allInited) return;
                if (Session.Enforced.Debug >= 3) Log.Line($"Close: ControllerId [{Controller.EntityId}]");

                if (Bus != null && Bus.SubGrids.Contains(LocalGrid))
                {
                    if (Bus.ActiveController == this) OfflineShield(true, false, State.Other, true);
                    Registry.RegisterWithBus(this, LocalGrid, false, Bus, out Bus);
                }

                if (Session.Instance.AllControllers.Contains(this)) Session.Instance.AllControllers.Remove(this);
                bool value1;

                if (Session.Instance.FunctionalShields.ContainsKey(this)) Session.Instance.FunctionalShields.TryRemove(this, out value1);

                Icosphere = null;
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(_ellipsoidOxyProvider);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
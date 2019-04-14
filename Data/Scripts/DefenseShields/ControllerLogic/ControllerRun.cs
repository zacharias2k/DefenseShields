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
                        if (Bus.ActiveController != null && _allInited) _bCount++;
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
                var fieldMode = State.Value.ProtectMode != 2;

                var protect = ProtectionOn(fieldMode);
                if (protect != Status.Active)
                {
                    if (Bus.Tick1800 && Bus.ActiveController == this) Log.Line($"NotActive: {protect} - {Bus.MyResourceDist.SourcesEnabled} - {Bus.MyResourceDist.ResourceStateByType(GId)} - {Bus.MyResourceDist.MaxAvailableResourceByType(GId)} - {SinkCurrentPower} - {Sink.CurrentInputByType(GId)}");
                    if (NotFailed)
                    {
                        if (Session.Enforced.Debug >= 2) Log.Line($"FailState: {protect} - ControllerId [{Controller.EntityId}]");
                        if (fieldMode)
                        {
                            var up = protect != Status.Lowered;
                            var awake = protect != Status.Sleep;
                            var clear = up && awake;
                            Bus.Field.OfflineShield(clear, up, protect);
                        }
                        else
                        {
                            NotFailed = false;
                            ProtChangedState();
                            //other stuff
                        }
                    }
                    else if (State.Value.Message) ProtChangedState();
                    return;
                }
                if (!_isServer || !State.Value.Online) return;
                if (Bus.Starting) ComingOnline();
                if (_mpActive && (Bus.Field.ForceBufferSync || Bus.Count == 29))
                {
                    var newPercentColor = UtilsStatic.GetShieldColorFromFloat(State.Value.ShieldPercent);
                    if (Bus.Field.ForceBufferSync || newPercentColor != _oldPercentColor)
                    {
                        ProtChangedState();
                        _oldPercentColor = newPercentColor;
                        Bus.Field.ForceBufferSync = false;
                    }
                    else if (Bus.Tick1800) ProtChangedState();
                }
                if (Session.Instance.EmpWork.EventRunning) Bus.Field.AbsorbEmp();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Controller.Storage != null)
                {
                    State.SaveState();
                    Set.SaveSettings();
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

                if (Bus?.ActiveController == this) Bus.Field?.OfflineShield(true, false, Status.Other, true);
                Registry.RegisterWithBus(this, LocalGrid, false, Bus, out Bus);
                if (Session.Instance.AllControllers.Contains(this)) Session.Instance.AllControllers.Remove(this);
                bool value1;

                if (Session.Instance.FunctionalShields.ContainsKey(this)) Session.Instance.FunctionalShields.TryRemove(this, out value1);
                //InitEntities(false);
                IsWorking = false;
                IsFunctional = false;
                //Bus.ShellPassive?.Render?.RemoveRenderObjects();
                //Bus.ShellActive?.Render?.RemoveRenderObjects();
                //Bus.ShieldEnt?.Render?.RemoveRenderObjects();
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

                if (Bus != null)
                {
                    if (Bus?.ActiveController == this && Bus.Field != null)
                    {
                        Bus.Field.OfflineShield(true, false, Status.Other, true);
                        Bus.Field.Run(false);
                    }
                }
                Registry.RegisterWithBus(this, LocalGrid, false, Bus, out Bus);

                if (Session.Instance.AllControllers.Contains(this)) Session.Instance.AllControllers.Remove(this);
                bool value1;

                if (Session.Instance.FunctionalShields.ContainsKey(this)) Session.Instance.FunctionalShields.TryRemove(this, out value1);

                //Icosphere = null;
                //InitEntities(false);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
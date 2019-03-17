namespace DefenseShields
{
    using System;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DSControlLarge", "DSControlSmall", "DSControlTable")]
    public partial class DefenseShields : MyGameLogicComponent
    {
        #region Simulation
        public override void OnAddedToContainer()
        {
            if (!_containerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Shield = (IMyUpgradeModule)Entity;
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
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                MyGrid = (MyCubeGrid)Shield.CubeGrid;
                MyCube = Shield as MyCubeBlock;
                RegisterEvents();
                AssignSlots();
                _resetEntity = true;
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Shield.CubeGrid.Physics == null) return;
                _isServer = Session.Instance.IsServer;
                _isDedicated = Session.Instance.DedicatedServer;
                _mpActive = Session.Instance.MpActive;

                PowerInit();
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(_ellipsoidOxyProvider);

                if (_isServer) Enforcements.SaveEnforcement(Shield, Session.Enforced, true);
                else Session.Instance.FunctionalShields[this] = false;

                Session.Instance.Controllers.Add(this);
                if (MyAPIGateway.Session.CreativeMode) CreativeModeWarning();
                if (Session.Enforced.Debug == 3) Log.Line($"UpdateOnceBeforeFrame: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!EntityAlive()) return;
                if (!ShieldOn())
                {
                    if (NotFailed) OfflineShield();
                    else if (DsState.State.Message) ShieldChangeState();
                    if (!_isDedicated && _tick60 && InControlPanel && InThisTerminal) TerminalRefresh();
                    return;
                }

                if (!_isServer || !DsState.State.Online) return;

                if (_comingOnline) ComingOnlineSetup();
                if (_mpActive && (_forceBufferSync || _count == 29))
                {
                    var newPercentColor = UtilsStatic.GetShieldColorFromFloat(DsState.State.ShieldPercent);
                    if (_forceBufferSync || newPercentColor != _oldPercentColor)
                    {
                        ShieldChangeState();
                        _oldPercentColor = newPercentColor;
                        _forceBufferSync = false;
                    }
                    else if (_tick1800) ShieldChangeState();
                }
                if (Session.Instance.EmpWork.EventRunning) AbsorbEmp();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Shield.Storage != null)
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
                if (Session.Enforced.Debug == 3) Log.Line($"OnRemovedFromScene: {ShieldMode} - GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                if (ShieldComp?.DefenseShields == this)
                {
                    FailShield(true);
                    DsState.State.Suspended = true;
                    Shield.RefreshCustomInfo();
                    ShieldComp.DefenseShields = null;
                }
                RegisterEvents(false);
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
                if (Session.Enforced.Debug == 3) Log.Line($"Close: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                if (Session.Instance.Controllers.Contains(this)) Session.Instance.Controllers.Remove(this);
                bool value1;
                if (Session.Instance.FunctionalShields.ContainsKey(this)) Session.Instance.FunctionalShields.TryRemove(this, out value1);
                lock (Session.Instance.ActiveShields) Session.Instance.ActiveShields.Remove(this);
                Icosphere = null;
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(_ellipsoidOxyProvider);

                _power = 0.0001f;
                if (_allInited) _sink.Update();
                if (ShieldComp?.DefenseShields == this) ShieldComp.DefenseShields = null;
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
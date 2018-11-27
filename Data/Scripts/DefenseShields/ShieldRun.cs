using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.Entities.Blocks;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DSControlLarge", "DSControlSmall", "DSControlTable")]
    public partial class DefenseShields : MyGameLogicComponent
    {
        #region Simulation
        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Shield = (IMyUpgradeModule)Entity;
                ContainerInited = true;
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
                if (Session.Enforced.Debug >= 2) Log.Line($"OnAddedToScene: GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                MyGrid = (MyCubeGrid)Shield.CubeGrid;
                MyCube = Shield as MyCubeBlock;
                RegisterEvents();
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
                _isServer = Session.IsServer;
                _isDedicated = Session.DedicatedServer;
                _mpActive = Session.MpActive;

                PowerInit();
                Session.Instance.Shields.Add(this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                if (_isServer) Enforcements.SaveEnforcement(Shield, Session.Enforced, true);
                if (Session.Enforced.Debug >= 2) Log.Line($"UpdateOnceBeforeFrame: ShieldId [{Shield.EntityId}]");
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
                    if (Session.Enforced.Debug >= 1 && WasOnline) Log.Line($"Off: WasOn:{WasOnline} - Online:{DsState.State.Online}({_prevShieldActive}) - Lowered:{DsState.State.Lowered} - Buff:{DsState.State.Buffer} - Sus:{DsState.State.Suspended} - EW:{DsState.State.EmitterWorking} - Perc:{DsState.State.ShieldPercent} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
                    if (WasOnline) OfflineShield();
                    else if (DsState.State.Message) ShieldChangeState();
                    return;
                }
                if (Session.Enforced.Debug >= 1 && Tick180 && !WasOnline) Log.Line($"On: WasOn:{WasOnline} - Online:{DsState.State.Online}({_prevShieldActive}) - Lowered:{DsState.State.Lowered} - Buff:{DsState.State.Buffer} - Sus:{DsState.State.Suspended} - EW:{DsState.State.EmitterWorking} - Perc:{DsState.State.ShieldPercent} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
                if (DsState.State.Online)
                {
                    if (ComingOnline) ComingOnlineSetup();
                    if (_isServer)
                    {
                        var createHeTiming = _count == 6 && (_lCount == 1 || _lCount == 6);
                        if (GridIsMobile && createHeTiming) CreateHalfExtents();
                        if (_syncEnts) SyncThreadedEnts();

                        if (_mpActive && _count == 29)
                        {
                            var newPercentColor = UtilsStatic.GetShieldColorFromFloat(DsState.State.ShieldPercent);
                            if (newPercentColor != _oldPercentColor)
                            {
                                ShieldChangeState();
                                _oldPercentColor = newPercentColor;
                            }
                            else if (_lCount == 7 && _eCount == 7) ShieldChangeState();
                        }
                    }
                    else WebEntitiesClient();
                    if (!_isDedicated && Tick60) HudCheck();

                }
                if (Session.Enforced.Debug >=2) Dsutil1.StopWatchReport($"PerfCon: Online: {DsState.State.Online} - Asleep:{Asleep} - Tick: {Tick} loop: {_lCount}-{_count}", 4);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
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
                if (Session.Enforced.Debug >= 2) Log.Line($"OnRemovedFromScene: {ShieldMode} - GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                if (ShieldComp?.DefenseShields == this)
                {
                    DsState.State.Online = false;
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
                if (Session.Enforced.Debug >= 2) Log.Line($"Close: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                if (Session.Instance.Controllers.Contains(this)) Session.Instance.Controllers.Remove(this);
                if (Session.Instance.ActiveShields.Contains(this)) Session.Instance.ActiveShields.Remove(this);
                if (Session.Instance.Shields.Contains(this)) Session.Instance.Shields.Remove(this);
                Icosphere = null;
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(EllipsoidOxyProvider);

                _power = 0.0001f;
                if (AllInited) Sink.Update();
                if (ShieldComp?.DefenseShields == this) ShieldComp.DefenseShields = null;
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
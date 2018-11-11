using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using DefenseShields.Support;
using Sandbox.Game.Entities;
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
                if (Session.Enforced.Debug >= 1) Log.Line($"OnAddedToScene: GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                RegisterEvents();
                MyCube = Shield as MyCubeBlock;
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

                Session.Instance.Components.Add(this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                if (_isServer) Enforcements.SaveEnforcement(Shield, Session.Enforced, true);
                if (Session.Enforced.Debug >= 1) Log.Line($"UpdateOnceBeforeFrame: ShieldId [{Shield.EntityId}]");
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
                    if (Session.Enforced.Debug >= 1 && WasOnline) Log.Line($"On: WasOn:{WasOnline} - Online:{DsState.State.Online}({_prevShieldActive}) - Lowered:{DsState.State.Lowered} - Buff:{DsState.State.Buffer} - Sus:{DsState.State.Suspended} - EW:{DsState.State.EmitterWorking} - Perc:{DsState.State.ShieldPercent} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
                    if (WasOnline) OfflineShield();
                    else if (DsState.State.Message) ShieldChangeState();
                    return;
                }
                if (Session.Enforced.Debug >= 1 && _tick60 && !WasOnline) Log.Line($"On: WasOn:{WasOnline} - Online:{DsState.State.Online}({_prevShieldActive}) - Lowered:{DsState.State.Lowered} - Buff:{DsState.State.Buffer} - Sus:{DsState.State.Suspended} - EW:{DsState.State.EmitterWorking} - Perc:{DsState.State.ShieldPercent} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
                if (DsState.State.Online)
                {
                    if (ComingOnline) ComingOnlineSetup();
                    if (_isServer)
                    {
                        var createHeTiming = _count == 6 && (_lCount == 1 || _lCount == 6);
                        if (GridIsMobile && createHeTiming) CreateHalfExtents();
                        if (_syncEnts) SyncThreadedEnts();
                        WebEntities();
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

                    if (!_isDedicated && _tick60) HudCheck();
                }
                if (Session.Enforced.Debug >= 1) Dsutil1.StopWatchReport($"PerfCon: Online: {DsState.State.Online} - Tick: {_tick} loop: {_lCount}-{_count}", 4);
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
                if (Session.Enforced.Debug >= 1) Log.Line($"OnRemovedFromScene: {ShieldMode} - GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                if (ShieldComp?.DefenseShields == this)
                {
                    DsState.State.Online = false;
                    DsState.State.Suspended = true;
                    Shield.RefreshCustomInfo();
                    ShieldComp.DefenseShields = null;
                }
                RegisterEvents(false);
                InitEntities(false);
                MyCube = null;
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
                if (Session.Instance.Components.Contains(this)) Session.Instance.Components.Remove(this);
                Icosphere = null;
                RegisterEvents(false);
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(EllipsoidOxyProvider);

                _power = 0.0001f;
                if (AllInited) Sink.Update();
                if (ShieldComp?.DefenseShields == this)
                {
                    ShieldComp.DefenseShields = null;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
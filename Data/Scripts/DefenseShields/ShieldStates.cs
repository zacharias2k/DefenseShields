using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public enum PlayerNotice
        {
            EmitterInit,
            FieldBlocked,
            OverLoad,
            Remodulate,
            NoPower,
            NoLos
        };

        private bool ShieldWaking()
        {
            if (_tick < UnsuspendTick)
            {
                if (!DsState.State.Waking)
                {
                    DsState.State.Waking = true;
                    ShieldChangeState(true);
                }
                _genericDownLoop = 0;
                return true;
            }
            if (UnsuspendTick != uint.MinValue && _tick >= UnsuspendTick)
            {
                _blockEvent = true;
                _shapeEvent = true;
                UnsuspendTick = uint.MinValue;
            }
            else if (_shapeTick != uint.MinValue && _tick >= _shapeTick)
            {
                _shapeEvent = true;
                _shapeTick = uint.MinValue;
            }
            DsState.State.Waking = false;
            return false;
        }

        private bool FieldShapeBlocked()
        {
            ModulatorGridComponent modComp;
            Shield.CubeGrid.Components.TryGet(out modComp);
            if (ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels || Session.Enforced.DisableVoxelSupport == 1) return false;

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            //MobileUpdate();
            //Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(ShieldComp.PhysicsOutsideLow, voxel)) continue;

                Shield.Enabled = false;
                DsState.State.FieldBlocked = true;
                ShieldChangeState(true);
                return true;
            }
            DsState.State.FieldBlocked = false;
            return false;
        }

        private void EmitterEventDetected()
        {
            if (!GridIsMobile)
            {
                UpdateDimensions = true;
                if (UpdateDimensions) RefreshDimensions();
            }
            ShieldComp.EmitterEvent = false;
            if (!ShieldComp.EmittersWorking)
            {
                DsState.State.EmitterWorking = false;
                _genericDownLoop = 0;
                if (Session.Enforced.Debug == 1) Log.Line($"EmitterEvent: detected an emitter event and no emitter is working, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                return;
            }
            DsState.State.EmitterWorking = true;
        }

        private void FailureConditions()
        {
            if (_overLoadLoop == 0 || _reModulationLoop == 0 || _genericDownLoop == 0)
            {
                if (DsState.State.Online)
                {
                    DsState.State.Online = false;
                    if (_overLoadLoop != -1) DsState.State.Overload = true;
                    if (_reModulationLoop != -1) DsState.State.Remodulate = true;
                    OfflineShield();
                    ShieldChangeState(true);
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                     DsState.State.Remodulate = false;
                    _reModulationLoop = -1;
                    ShieldChangeState(false);
                }
            }

            if (_genericDownLoop > -1)
            {
                _genericDownLoop++;
                if (_genericDownLoop == GenericDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_genericDownLoop == GenericDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        DsState.State.EmitterWorking = false;
                        _genericDownLoop = 0;
                    }
                    else
                    {
                        DsState.State.EmitterWorking = true;
                        _genericDownLoop = -1;
                        ShieldChangeState(false);
                    }
                }
            }

            if (_overLoadLoop > -1)
            {
                _overLoadLoop++;
                if (_overLoadLoop == ShieldDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_overLoadLoop == ShieldDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        _genericDownLoop = 0;
                    }
                    else
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                        var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
                        var nerfer = nerf ? Session.Enforced.Nerf : 1f;
                        DsState.State.Buffer = (_shieldMaxBuffer / 25) * nerfer; // replace this with something that scales based on charge rate
                        ShieldChangeState(false);
                    }
                }
            }
        }

        private void OfflineShield()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Offline count: {_offlineCnt} - resetting all - was: Buffer:{DsState.State.Buffer} - Absorb:{Absorb} - Percent:{ShieldComp.ShieldPercent} - O2:{DsState.State.IncreaseO2ByFPercent} - Lowered:{DsState.State.Lowered}");

                if (!_power.Equals(0.0001f)) _power = 0.001f;
                Sink.Update();
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                ResetShape(true, true);
                ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                CleanUp(0);
                CleanUp(1);
                CleanUp(3);
                CleanUp(4);

                _currentHeatStep = 0;
                _accumulatedHeat = 0;
                _heatCycle = -1;
                Absorb = 0f;
                DsState.State.Buffer = 0f;
                ShieldComp.ShieldPercent = 0f;
                DsState.State.IncreaseO2ByFPercent = 0f;
                DsSet.Settings.ShieldActive = false;
                PrevShieldActive = false;
                DsState.State.Lowered = false;
                DsState.State.Online = false;
                DsState.State.Heat = 0;

                if (!Session.DedicatedServer) ShellVisibility(true);
            }

            Shield.RefreshCustomInfo();
            Shield.ShowInToolbarConfig = false;
            Shield.ShowInToolbarConfig = true;

            if (Session.Enforced.Debug == 1) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {_shieldCurrentPower} - gridMax: {_gridMaxPower} - currentPower: {_gridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private bool ShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsSet.Settings.ShieldActive)
            {
                Timing(false);
                if (!DsState.State.Lowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!Session.DedicatedServer) ShellVisibility(true);
                    DsState.State.Lowered = true;
                    DsSet.SaveSettings();
                    DsSet.NetworkUpdate();
                }
                PowerOnline();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!Shield.IsWorking || !ShieldComp.EmittersWorking)
                {
                    _genericDownLoop = 0;
                    return false;
                }

                if (GridIsMobile && _lCount == 0 && _count == 0)
                {
                    _updateMobileShape = true;
                    MobileUpdate();
                }
                else if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }
            if (DsState.State.Lowered && DsSet.Settings.ShieldActive && Shield.IsWorking)
            {
                if (!Session.DedicatedServer) ShellVisibility();
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Lowered = false;
                DsSet.SaveSettings();
                DsSet.NetworkUpdate();
            }
            return false;
        }

        private bool ClientShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsSet.Settings.ShieldActive)
            {
                Timing(false);
                if (!_clientLowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientLowered = true;
                }
                PowerOnline();

                if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }
            if (_clientLowered) ShellVisibility();
            return false;
        }

        private bool ShieldSleeping()
        {
            if (ShieldComp.EmittersSuspended)
            {
                if (!DsState.State.Sleeping)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!Session.DedicatedServer) ShellVisibility(true);
                    DsState.State.Sleeping = true;
                    ShieldChangeState(false);
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 1) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Sleeping = true;
                return DsState.State.Sleeping;
            }

            if (DsState.State.Sleeping)
            {
                DsState.State.Sleeping = false;
                if (!Session.DedicatedServer) ShellVisibility();
                _blockChanged = true;
                _functionalChanged = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Sleeping = false;
                Shield.RefreshCustomInfo();
                ShieldChangeState(false);
                if (Session.Enforced.Debug == 1) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            DsState.State.Sleeping = false;
            return DsState.State.Sleeping;
        }

        private void Election()
        {
            if (ShieldComp == null || !Shield.CubeGrid.Components.Has<ShieldGridComponent>())
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Election: ShieldComp is null, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                var girdHasShieldComp = Shield.CubeGrid.Components.Has<ShieldGridComponent>();

                if (girdHasShieldComp)
                {
                    Shield.CubeGrid.Components.TryGet(out ShieldComp);
                    if (Session.Enforced.Debug == 1) Log.Line($"Election: grid had Comp, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                else
                {
                    Shield.CubeGrid.Components.Add(ShieldComp);
                    if (Session.Enforced.Debug == 1) Log.Line($"Election: grid didn't have Comp, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                ShieldMode = ShieldType.Unknown;
                Shield.RefreshCustomInfo();
                if (ShieldComp != null) ShieldComp.DefaultO2 = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(Shield.PositionComp.WorldVolume.Center);
            }
            if (Session.Enforced.Debug == 1) Log.Line($"Election: controller election was held, new mode is: {ShieldMode} - ShieldId [{Shield.EntityId}]");
        }

        private bool Suspend()
        {
            var isStatic = Shield.CubeGrid.IsStatic;
            var primeMode = ShieldMode == ShieldType.Station && isStatic && ShieldComp.StationEmitter == null;
            var betaMode = ShieldMode != ShieldType.Station && !isStatic && ShieldComp.ShipEmitter == null;

            if (ShieldMode != ShieldType.Station && isStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Station && !isStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Unknown) InitSuspend();
            else if (ShieldComp.DefenseShields != this || primeMode || betaMode) InitSuspend(true);
            else if (!GridOwnsController()) InitSuspend(true);
            else
            {
                if (DsState.State.Suspended)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Suspend: controller unsuspending - ShieldId [{Shield.EntityId}]");
                    DsState.State.Suspended = false;
                    ShieldComp.GetLinkedGrids.Clear();
                    ShieldComp.GetSubGrids.Clear();
                    _blockChanged = true;
                    _functionalChanged = true;
                    ResetShape(false, true);
                    ResetShape(false, false);
                    if (Session.Enforced.Debug == 1) Log.Line($"Suspend: controller mode was: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                    SetShieldType(false);
                    if (Session.Enforced.Debug == 1) Log.Line($"Suspend: controller mode is now: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                    if (!Session.DedicatedServer) ShellVisibility(true);
                    Icosphere.ShellActive = null;
                    GetModulationInfo();
                    _currentHeatStep = 0;
                    _accumulatedHeat = 0;
                    _heatCycle = -1;
                    UnsuspendTick = _tick + 1800;
                    _updateRender = true;
                    DsState.State.Suspended = false;
                    DsState.State.Heat = 0;
                    ShieldChangeState(false);
                    if (Session.Enforced.Debug == 1) Log.Line($"Unsuspended: CM:{ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{BoundingRange} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Suspended = false;
            }

            if (DsState.State.Suspended) SetShieldType(true);
            return DsState.State.Suspended;
        }

        private void InitSuspend(bool cleanEnts = false)
        {
            if (!DsState.State.Suspended)
            {
                if (cleanEnts) InitEntities(false);

                DsState.State.Suspended = true;
                Shield.RefreshCustomInfo();
            }
            if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
            DsState.State.Suspended = true;
        }

        private bool GridOwnsController()
        {
            if (Shield.CubeGrid.BigOwners.Count == 0)
            {
                DsState.State.ControllerGridAccess = false;
                return DsState.State.ControllerGridAccess;
            }

            var controlToGridRelataion = ((MyCubeBlock)Shield).GetUserRelationToOwner(Shield.CubeGrid.BigOwners[0]);
            var faction = MyRelationsBetweenPlayerAndBlock.FactionShare;
            var owner = MyRelationsBetweenPlayerAndBlock.Owner;
            DsState.State.InFaction = controlToGridRelataion == faction;
            DsState.State.IsOwner = controlToGridRelataion == owner;

            if (controlToGridRelataion != owner && controlToGridRelataion != faction)
            {
                if (DsState.State.ControllerGridAccess)
                {
                    DsState.State.ControllerGridAccess = false;
                    Shield.RefreshCustomInfo();
                }
                DsState.State.ControllerGridAccess = false;
                return DsState.State.ControllerGridAccess;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
            }
            DsState.State.ControllerGridAccess = true;
            return DsState.State.ControllerGridAccess;
        }

        private void PlayerMessages(PlayerNotice notice)
        {
            var realPlayerIds = new HashSet<long>();
            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    UtilsStatic.GetRealPlayers(Shield.CubeGrid.PositionComp.WorldAABB.Center, (float)Shield.CubeGrid.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- emitter is initializing and connecting to controller, startup in 30 seconds!", 4816, "Red");
                    break;
                case PlayerNotice.FieldBlocked:
                    UtilsStatic.GetRealPlayers(Shield.CubeGrid.PositionComp.WorldAABB.Center, (float)Shield.CubeGrid.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("The shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case PlayerNotice.OverLoad:
                    UtilsStatic.GetRealPlayers(Shield.CubeGrid.PositionComp.WorldAABB.Center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.Remodulate:
                    UtilsStatic.GetRealPlayers(Shield.CubeGrid.PositionComp.WorldAABB.Center, (float)Shield.CubeGrid.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield remodremodulating, restarting in 5 seconds.", 4800, "White");
                    break;
                case PlayerNotice.NoLos:
                    break;
                case PlayerNotice.NoPower:
                    UtilsStatic.GetRealPlayers(Shield.CubeGrid.PositionComp.WorldAABB.Center, (float)Shield.CubeGrid.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }

        }

        private void BroadcastMessage()
        {
            if (DsState.State.NoPower) PlayerMessages(PlayerNotice.NoPower);
            else if (DsState.State.Overload) PlayerMessages(PlayerNotice.OverLoad);
            else if (DsState.State.FieldBlocked) PlayerMessages(PlayerNotice.FieldBlocked);
            else if (DsState.State.Waking) PlayerMessages(PlayerNotice.EmitterInit);
            else if (DsState.State.Remodulate) PlayerMessages(PlayerNotice.Remodulate);
        }

        private bool ClientOfflineStates()
        {
            var isStatic = Shield.CubeGrid.IsStatic;
            var primeMode = ShieldMode == ShieldType.Station && isStatic && ShieldComp.StationEmitter == null;
            var betaMode = ShieldMode != ShieldType.Station && !isStatic && ShieldComp.ShipEmitter == null;

            if (DsState.State.Message)
            {
                BroadcastMessage();
                DsState.State.Message = false;
            }

            if (ShieldComp.DefenseShields != this || primeMode || betaMode)
            {
                if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
                if (_clientOn)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientOn = false;
                    Shield.RefreshCustomInfo();
                }
                return true;
            }

            var offline = DsState.State.Suspended || !DsState.State.Online || DsState.State.Sleeping || !DsState.State.ControllerGridAccess
                          || !DsState.State.EmitterWorking || DsState.State.Remodulate || DsState.State.Waking;
            if (offline)
            {
                if (_clientOn)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientOn = false;
                    Shield.RefreshCustomInfo();
                }
                return true;
            }

            if (!_clientOn) ShellVisibility();
            return false;
        }

        private bool WarmUpSequence()
        {
            if (Warming) return true;
            if (Starting)
            {
                Session.Instance.ControllerBlockCache[Shield.SlimBlock] = this;
                Warming = true;
                return true;
            }
            if (!Session.IsServer && (!DsState.State.EmitterWorking || !DsState.State.Online || DsState.State.NoPower || !DsState.State.ControllerGridAccess) || !PowerOnline())
            {
                if (_delayedClientWarmTick == 0)
                {
                    if (GridIsMobile) MobileUpdate();
                    _delayedClientWarmTick = _tick + 600;
                }
                if (_tick >= _delayedClientWarmTick)
                {
                    _blockChanged = true;
                    _functionalChanged = true;

                    ResetShape(false, true);
                    ResetShape(false, false);
                    _delayedClientWarmTick = _tick + 600;
                }
                return false;
            }
            HadPowerBefore = true;
            _blockChanged = true;
            _functionalChanged = true;
            ResetShape(false, true);
            ResetShape(false, false);
            _oldGridHalfExtents = DsState.State.GridHalfExtents;
            _oldEllipsoidAdjust = DsState.State.EllipsoidAdjust;
            GetModulationInfo();

            Starting = true;
            ControlBlockWorking = AllInited && Shield.IsWorking && Shield.IsFunctional;
            if (Session.Enforced.Debug == 1) Log.Line($"Warming: buffer:{DsState.State.Buffer} - BlockWorking:{ControlBlockWorking} - Active:{DsState.State.Online} - ShieldId [{Shield.EntityId}]");
            return false;
        }

        public void UpdateSettings(ProtoControllerSettings newSettings)
        {
            var newShape = newSettings.ExtendFit != DsSet.Settings.ExtendFit || newSettings.FortifyShield != DsSet.Settings.FortifyShield || newSettings.SphereFit != DsSet.Settings.SphereFit;
            DsSet.Settings = newSettings;
            SettingsUpdated = true;
            if (newShape) FitChanged = true;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }

        public void UpdateState(ProtoControllerState newState)
        {
            DsState.State = newState;
            if (!MainInit) return;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateState - ShieldId [{Shield.EntityId}]:\n{newState}");
        }
    }
}

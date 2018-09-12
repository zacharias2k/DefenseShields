using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
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

        private void ShieldChangeState()
        {
            if (!WarmedUp) return;
            if (Session.Enforced.Debug >= 2) Log.Line($"ShieldChangeState: Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
            else if (Session.Enforced.Debug >= 1 && DsState.State.Message) Log.Line($"ShieldChangeState: Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");

            DsState.SaveState();
            if (Session.MpActive) DsState.NetworkUpdate();
            if (!Session.DedicatedServer)
            {
                BroadcastMessage();
                Shield.RefreshCustomInfo();
            }
            DsState.State.Message = false;
        }

        private bool ShieldOn(bool server)
        {
            _tick = Session.Instance.Tick;
            if (server)
            {
                if (!ControllerFunctional() || ShieldWaking())
                {
                    if (ShieldDown()) return false;
                    if (!PrevShieldActive) return false;
                    PrevShieldActive = false;
                    return false;
                }
                var powerState = PowerOnline();
                if (_tick % 120 == 0)
                {
                    GetModulationInfo();
                    GetEnhancernInfo();
                }
                if (ShieldDown()) return false;
                SetShieldServerStatus(powerState);
                Timing(true);

                if (!DsState.State.Online || ComingOnline && (!GridOwnsController() || GridIsMobile && FieldShapeBlocked()))
                {
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
                    ShieldDown();
                    return false;
                }
            }
            else
            {
                if (!PostInit()) return false;

                if (_blockChanged) BlockMonitor();

                if (ClientOfflineStates() || !WarmUpSequence() || ClientShieldLowered()) return false;
                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();
                PowerOnline();
                SetShieldClientStatus();
                Timing(true);
            }
            _clientOn = true;
            _clientLowered = false;
            return true;
        }

        private bool ControllerFunctional()
        {
            if (!PostInit()) return false;

            if (_blockChanged) BlockMonitor();

            if (_tick >= _losCheckTick) LosCheck();
            if (Suspend() || !Warming && !WarmUpSequence() || ShieldSleeping() || ShieldLowered()) return false;

            if (ShieldComp.EmitterEvent) EmitterEventDetected();

            ControlBlockWorking = Shield.IsWorking && Shield.IsFunctional;
            if (!ControlBlockWorking || !ShieldComp.EmittersWorking)
            {
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                return false;
            }

            if (ControlBlockWorking)
            {
                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();
            }

            return ControlBlockWorking;
        }

        private void SetShieldServerStatus(bool powerState)
        {
            DsSet.Settings.ShieldActive = ControlBlockWorking && powerState;
            if (!PrevShieldActive && DsSet.Settings.ShieldActive) ComingOnline = true;
            else if (ComingOnline && PrevShieldActive && DsSet.Settings.ShieldActive) ComingOnline = false;

            PrevShieldActive = DsSet.Settings.ShieldActive;
            DsState.State.Online = DsSet.Settings.ShieldActive;

            if (!GridIsMobile && (ComingOnline || ShieldComp.O2Updated))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
                ShieldComp.O2Updated = false;
            }
        }

        private void SetShieldClientStatus()
        {
            if (!PrevShieldActive && DsState.State.Online) ComingOnline = true;
            else if (ComingOnline && PrevShieldActive && DsState.State.Online) ComingOnline = false;

            PrevShieldActive = DsState.State.Online;
            if (!GridIsMobile && (ComingOnline || !DsState.State.IncreaseO2ByFPercent.Equals(EllipsoidOxyProvider.O2Level)))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
            }
        }

        private bool ShieldDown()
        {
            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1)
            {
                FailureConditions();
                return true;
            }
            return false;
        }

        private void LosCheck()
        {
            _losCheckTick = uint.MaxValue;
            ShieldComp.CheckEmitters = true;
        }

        private bool ShieldWaking()
        {
            if (_tick < UnsuspendTick)
            {
                if (!DsState.State.Waking)
                {
                    DsState.State.Waking = true;
                    DsState.State.Message = true;
                }
                if (_genericDownLoop == -1) _genericDownLoop = 0;
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
            MyGrid.Components.TryGet(out modComp);
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
                DsState.State.Message = true;
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
                if (GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los) DsState.State.Message = true;
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) DsState.State.Message = true;
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                if (Session.Enforced.Debug >= 1) Log.Line($"EmitterEvent: detected an emitter event and no emitter is working, shield mode: {ShieldMode} - Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
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
                    if (_overLoadLoop != -1)
                    {
                        DsState.State.Overload = true;
                        DsState.State.Message = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        DsState.State.Remodulate = true;
                        DsState.State.Message = true;
                    }
                    ShieldFailed();
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                     DsState.State.Remodulate = false;
                    _reModulationLoop = -1;
                }
            }

            if (_genericDownLoop > -1)
            {
                _genericDownLoop++;
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
                    }
                }
            }
        }

        private void ShieldFailed()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"Offline count: {_offlineCnt} - resetting all - was: Buffer:{DsState.State.Buffer} - Absorb:{Absorb} - Percent:{DsState.State.ShieldPercent} - O2:{DsState.State.IncreaseO2ByFPercent} - Lowered:{DsState.State.Lowered}");

                _power = 0.001f;
                Sink.Update();
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                ResetShape(true, true);
                CleanUp(0);
                CleanUp(1);
                CleanUp(3);
                CleanUp(4);

                _currentHeatStep = 0;
                _accumulatedHeat = 0;
                _heatCycle = -1;
                Absorb = 0f;
                DsState.State.Buffer = 0f;
                DsState.State.ShieldPercent = 0f;
                DsState.State.IncreaseO2ByFPercent = 0f;
                DsSet.Settings.ShieldActive = false;
                PrevShieldActive = false;
                DsState.State.Online = false;
                DsState.State.Heat = 0;

                if (!Session.DedicatedServer) ShellVisibility(true);
            }

            Shield.RefreshCustomInfo();
            Shield.ShowInToolbarConfig = false;
            Shield.ShowInToolbarConfig = true;

            if (Session.Enforced.Debug >= 1) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {_shieldCurrentPower} - gridMax: {_gridMaxPower} - currentPower: {_gridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private bool ShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsSet.Settings.ShieldActive)
            {
                Timing(false);
                if (!DsState.State.Lowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!Session.DedicatedServer) ShellVisibility(true);
                    DsState.State.Lowered = true;
                }
                PowerOnline();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!Shield.IsWorking || !ShieldComp.EmittersWorking)
                {
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
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

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!Session.DedicatedServer) ShellVisibility(true);
                    DsState.State.Sleeping = true;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug >= 1) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Sleeping = true;
                return true;
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
                if (Session.Enforced.Debug >= 1) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            DsState.State.Sleeping = false;
            return false;
        }

        private void Election()
        {
            if (ShieldComp == null || !Shield.CubeGrid.Components.Has<ShieldGridComponent>())
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"Election: ShieldComp is null, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                var girdHasShieldComp = Shield.CubeGrid.Components.Has<ShieldGridComponent>();

                if (girdHasShieldComp)
                {
                    Shield.CubeGrid.Components.TryGet(out ShieldComp);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Election: grid had Comp, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                else
                {
                    Shield.CubeGrid.Components.Add(ShieldComp);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Election: grid didn't have Comp, mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                ShieldMode = ShieldType.Unknown;
                Shield.RefreshCustomInfo();
                if (ShieldComp != null) ShieldComp.DefaultO2 = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(Shield.PositionComp.WorldVolume.Center);
            }
            if (Session.Enforced.Debug >= 1) Log.Line($"Election: controller election was held, new mode is: {ShieldMode} - ShieldId [{Shield.EntityId}]");
        }

        private bool Suspend()
        {
            var isStatic = MyGrid.IsStatic;
            var primeMode = ShieldMode == ShieldType.Station && isStatic && ShieldComp.StationEmitter == null;
            var betaMode = ShieldMode != ShieldType.Station && !isStatic && ShieldComp.ShipEmitter == null;

            if (ShieldMode != ShieldType.Station && isStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Station && !isStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Unknown) InitSuspend();
            else if (ShieldComp.DefenseShields != this || primeMode || betaMode) InitSuspend(true);
            else if (!GridOwnsController() || SlaveControllerLink(isStatic)) InitSuspend(true);
            else
            {
                if (DsState.State.Suspended)
                {
                    if (Session.Enforced.Debug >= 1) Log.Line($"Suspend: controller unsuspending - ShieldId [{Shield.EntityId}]");
                    DsState.State.Suspended = false;
                    ShieldComp.GetLinkedGrids.Clear();
                    ShieldComp.GetSubGrids.Clear();
                    _blockChanged = true;
                    _functionalChanged = true;
                    ResetShape(false, true);
                    ResetShape(false, false);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Suspend: controller mode was: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                    SetShieldType(false);
                    if (Session.Enforced.Debug >= 1) Log.Line($"Suspend: controller mode is now: {ShieldMode} - ShieldId [{Shield.EntityId}]");
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
                    if (Session.Enforced.Debug >= 1) Log.Line($"Unsuspended: CM:{ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{BoundingRange} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Suspended = false;
            }

            if (DsState.State.Suspended) SetShieldType(true);
            return DsState.State.Suspended;
        }

        private void InitSuspend(bool cleanEnts = false)
        {
            SetShieldType(true);
            if (!DsState.State.Suspended)
            {
                if (cleanEnts) InitEntities(false);

                DsState.State.Suspended = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug >= 1) Log.Line($"Suspended: controller mode is: {ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ShieldId [{Shield.EntityId}]");
            }
            if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
            DsState.State.Suspended = true;
        }

        private bool GridOwnsController()
        {
            var notTime = _tick != 0 && _tick % 600 != 0;

            if (notTime && !DsState.State.ControllerGridAccess) return false;
            if (notTime) return true;
            UpdateSubGrids(true);
            if (MyGrid.BigOwners.Count == 0)
            {
                DsState.State.ControllerGridAccess = false;
                return false;
            }

            var controlToGridRelataion = ((MyCubeBlock)Shield).GetUserRelationToOwner(MyGrid.BigOwners[0]);
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
                    if (Session.Enforced.Debug >= 1) Log.Line($"GridOwner: controller is not owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.ControllerGridAccess = false;
                return false;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug >= 1) Log.Line($"GridOwner: controller is owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            DsState.State.ControllerGridAccess = true;
            return true;
        }

        private bool SlaveControllerLink(bool isStatic)
        {
            var notTime = _tick != 0 && _tick % 120 != 0;

            if (notTime && _slaveLink) return true;
            if (notTime || isStatic) return false;
            var mySize = MyGrid.PositionComp.WorldVolume.Radius;
            var myEntityId = MyGrid.EntityId;
            //if (MyGrid.GridSizeEnum == MyCubeSize.Small) Log.Line($"SlaveControllerLink: size:{mySize} - tick:{_tick} - maxPower:{_gridMaxPower} - avail:{_gridAvailablePower} - LinkCnt:{ShieldComp.GetLinkedGrids.Count} - SubCnt:{ShieldComp.GetSubGrids.Count}");
            foreach (var grid in ShieldComp.GetLinkedGrids)
            {
                if (grid == MyGrid) continue;
                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null && shieldComponent.DefenseShields.WasOnline)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var otherMaxPower = dsComp.MyGrid.PositionComp.WorldVolume.Radius;
                    var otherEntityId = dsComp.MyGrid.EntityId;
                    if (mySize < otherMaxPower || mySize.Equals(otherEntityId) && myEntityId < otherEntityId)
                    {
                        _slaveLink = true;
                        return true;
                    }
                }
            }
            _slaveLink = false;
            return false;
        }

        private void PlayerMessages(PlayerNotice notice)
        {
            var realPlayerIds = new HashSet<long>();
            var center = GridIsMobile ? MyGrid.PositionComp.WorldVolume.Center : ShieldComp.StationEmitter.Emitter.WorldVolume.Center;
            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- emitter is initializing and connecting to controller, startup in 30 seconds!", 4816, "Red");
                    break;
                case PlayerNotice.FieldBlocked:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("The shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case PlayerNotice.OverLoad:
                    UtilsStatic.GetRealPlayers(center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.Remodulate:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield remodremodulating, restarting in 5 seconds.", 4800, "White");
                    break;
                case PlayerNotice.NoLos:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Emitter does not have line of sight, shield offline", 8000, "Red");
                    break;
                case PlayerNotice.NoPower:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }

        }

        private void BroadcastMessage()
        {
            if (!DsState.State.EmitterWorking && !DsState.State.Waking)
            {
                if (GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los) PlayerMessages(PlayerNotice.NoLos);
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) PlayerMessages(PlayerNotice.NoLos);
            }
            else if (DsState.State.NoPower) PlayerMessages(PlayerNotice.NoPower);
            else if (DsState.State.Overload) PlayerMessages(PlayerNotice.OverLoad);
            else if (DsState.State.FieldBlocked) PlayerMessages(PlayerNotice.FieldBlocked);
            else if (DsState.State.Waking) PlayerMessages(PlayerNotice.EmitterInit);
            else if (DsState.State.Remodulate) PlayerMessages(PlayerNotice.Remodulate);
        }

        private bool ClientOfflineStates()
        {
            var isStatic = MyGrid.IsStatic;
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
                PrevShieldActive = false;
                return true;
            }

            var offline = DsState.State.Suspended || !DsState.State.Online || DsState.State.Sleeping || !DsState.State.ControllerGridAccess
                          || !DsState.State.EmitterWorking || DsState.State.Remodulate || DsState.State.Waking || DsState.State.Overload;
            if (offline)
            {
                if (_clientOn)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientOn = false;
                    Shield.RefreshCustomInfo();
                }
                PrevShieldActive = false;
                return true;
            }

            if (!_clientOn)
            {
                ShellVisibility();
                Shield.RefreshCustomInfo();
            }
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

            if (Session.IsServer)
            {
                HadPowerBefore = true;
                ControlBlockWorking = AllInited && Shield.IsWorking && Shield.IsFunctional;
                DsState.State.Overload = false;
                DsState.State.NoPower = false;
                DsState.State.Remodulate = false;
                DsState.State.Heat = 0;
            }
            WarmingInit();
            return false;
        }

        private void WarmingInit()
        {
            _blockChanged = true;
            _functionalChanged = true;

            ResetShape(false, true);
            ResetShape(false, false);
            _oldGridHalfExtents = DsState.State.GridHalfExtents;
            _oldEllipsoidAdjust = DsState.State.EllipsoidAdjust;
            GetModulationInfo();
            GetEnhancernInfo();
            Starting = true;
            if (Session.Enforced.Debug >= 1) Log.Line($"Warming: Server:{Session.IsServer} - buffer:{DsState.State.Buffer} - BlockWorking:{Session.IsServer && ControlBlockWorking || !Session.IsServer && Shield.IsWorking && Shield.IsFunctional} - Active:{DsState.State.Online} - ShieldId [{Shield.EntityId}]");

        }

        public void UpdateSettings(ProtoControllerSettings newSettings)
        {
            var newShape = newSettings.ExtendFit != DsSet.Settings.ExtendFit || newSettings.FortifyShield != DsSet.Settings.FortifyShield || newSettings.SphereFit != DsSet.Settings.SphereFit;
            DsSet.Settings = newSettings;
            SettingsUpdated = true;
            if (newShape) FitChanged = true;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateSettings - server:{Session.IsServer} - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }

        public void UpdateState(ProtoControllerState newState)
        {
            DsState.State = newState;
            if (Session.Enforced.Debug == 2) Log.Line($"UpdateState - server:{Session.IsServer} - ShieldId [{Shield.EntityId}]:\n{newState}");
        }
    }
}

namespace DefenseShields
{
    using System.Collections.Generic;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class DefenseShields
    {
        public enum PlayerNotice
        {
            EmitterInit,
            FieldBlocked,
            OverLoad,
            EmpOverLoad,
            Remodulate,
            NoPower,
            NoLos
        }

        internal void UpdateSettings(ControllerSettingsValues newSettings)
        {
            var newShape = newSettings.ExtendFit != DsSet.Settings.ExtendFit || newSettings.FortifyShield != DsSet.Settings.FortifyShield || newSettings.SphereFit != DsSet.Settings.SphereFit;
            DsSet.Settings = newSettings;
            SettingsUpdated = true;
            if (newShape) FitChanged = true;
        }

        internal void UpdateState(ControllerStateValues newState)
        {
            if (!_isServer)
            {
                if (!newState.EllipsoidAdjust.Equals(DsState.State.EllipsoidAdjust) || !newState.ShieldFudge.Equals(DsState.State.ShieldFudge) ||
                    !newState.GridHalfExtents.Equals(DsState.State.GridHalfExtents))
                {
                    _updateMobileShape = true;
                }
            }
            DsState.State = newState;
            _clientNotReady = false;
        }

        private void ShieldChangeState()
        {
            if (!WarmedUp && !DsState.State.Message) return;
            if (Session.Instance.MpActive)
            {
                if (Session.Enforced.Debug == 4) Log.Line($"ServerUpdate: Broadcast:{DsState.State.Message} - Percent:{DsState.State.ShieldPercent} - HeatLvl:{DsState.State.Heat} - ShieldCharge:{DsState.State.Charge} - EmpProt:{DsState.State.EmpProtection} - ShieldId [{Shield.EntityId}]");
                DsState.NetworkUpdate();
            }
            if (!_isDedicated && DsState.State.Message)
            {
                BroadcastMessage();
                if (Session.Enforced.Debug == 3) Log.Line("ShieldChangeState");
                Shield.RefreshCustomInfo();
            }
            DsState.State.Message = false;
            DsState.SaveState();
        }

        private bool EntityAlive()
        {
            _tick = Session.Instance.Tick;
            _tick60 = Session.Instance.Tick60;
            _tick180 = Session.Instance.Tick180;
            _tick600 = Session.Instance.Tick600;
            _tick1800 = Session.Instance.Tick1800;

            if (WasPaused && (PlayerByShield || MoverByShield || NewEntByShield || LostPings > 59)) UnPauseLogic();
            LostPings = 0;

            var wait = _isServer && !_tick60 && DsState.State.Suspended;

            MyGrid = MyCube.CubeGrid;
            if (MyGrid?.Physics == null) return false;

            if (_resetEntity) ResetEntity();

            if (wait || (!_allInited && !PostInit())) return false;

            if (_tick1800 && Session.Enforced.Debug > 0)
            {
                if (Shield.CustomName == "DEBUG")
                {
                    if (_tick <= 1800) Shield.CustomName = "DEBUGAUTODISABLED";
                    else UserDebug();
                }
            }

            IsStatic = MyGrid.IsStatic;

            if (!Warming) WarmUpSequence();

            if (_subUpdate && _tick >= _subTick) HierarchyUpdate();
            if (_blockEvent && _tick >= _funcTick) BlockChanged(true);
            if (_mpActive)
            {
                if (_isServer)
                {
                    if (_tick - 1 > _lastSendDamageTick) ShieldHitReset(ShieldHit.Amount > 0 && ShieldHit.HitPos != Vector3D.Zero);
                    if (ShieldHitsToSend.Count != 0) SendShieldHits();
                    if (!_isDedicated && ShieldHits.Count != 0) AbsorbClientShieldHits();
                }
                else if (ShieldHits.Count != 0) AbsorbClientShieldHits();
            }
            return true;
        }

        private void UnPauseLogic()
        {
            if (Session.Enforced.Debug >= 2) Log.Line($"[Logic Resumed] Player:{PlayerByShield} - Mover:{MoverByShield} - NewEnt:{NewEntByShield} - Lost:{LostPings > 59} - LastWoken:{LastWokenTick} - ASleep:{Asleep} - TicksNoActivity:{TicksWithNoActivity}");
            TicksWithNoActivity = 0;
            LastWokenTick = _tick;
            Asleep = false;
            PlayerByShield = true;
            lock (Session.Instance.ActiveShields) Session.Instance.ActiveShields.Add(this);
            WasPaused = false;
        }

        private bool ShieldOn()
        {
            if (_isServer)
            {
                if (!ControllerFunctional() || ShieldWaking())
                {
                    if (ShieldFailing()) return false;
                    _prevShieldActive = false;
                    return false;
                }
                var powerState = PowerOnline();

                if (_tick60)
                {
                    GetModulationInfo();
                    GetEnhancernInfo();
                }

                if (ClientUiUpdate || SettingsUpdated) UpdateSettings();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();

                if (ShieldFailing(powerState))
                {
                    _prevShieldActive = false;
                    return false;
                }
                SetShieldServerStatus(powerState);
                Timing();
                if (!DsState.State.Online || _comingOnline && GridIsMobile && FieldShapeBlocked())
                {
                    _prevShieldActive = false;
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
                    ShieldFailing();
                    return false;
                }
            }
            else
            {
                if (_blockChanged) BlockMonitor();
                SetShieldClientStatus();
                if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
                if (ClientOfflineStates() || ClientShieldLowered()) return false;
                if (UpdateDimensions) RefreshDimensions();
                PowerOnline();
                Timing();
                _clientOn = true;
                _clientLowered = false;
            }

            return true;
        }

        private bool ControllerFunctional()
        {
            if (_blockChanged) BlockMonitor();

            if (_tick >= LosCheckTick) LosCheck();
            if (Suspend() || ShieldSleeping() || ShieldLowered())
            {
                ControlBlockWorking = false;
                return false;
            }

            ControlBlockWorking = IsWorking && IsFunctional;

            if (!ControlBlockWorking)
            {
                return false;
            }
            if (ControlBlockWorking)
            {
                if (UpdateDimensions) RefreshDimensions();
            }
            return ControlBlockWorking;
        }

        private void EmitterEventDetected()
        {
            ShieldComp.EmitterEvent = false;

            if (!GridIsMobile)
            {
                UpdateDimensions = true;
                if (UpdateDimensions) RefreshDimensions();
            }

            if (!ShieldComp.EmittersWorking)
            {
                if (!WarmedUp)
                {
                    MyGrid.Physics.ForceActivate();
                    if (Session.Enforced.Debug == 2) Log.Line($"EmitterStartupFailure: Asleep:{Asleep} - MaxPower:{GridMaxPower} - {ShieldSphere.Radius} - ControlWork:{ControlBlockWorking}");
                    LosCheckTick = Session.Instance.Tick + 1800;
                    return;
                }
                DsState.State.EmitterWorking = false;
                if (GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los) DsState.State.Message = true;
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) DsState.State.Message = true;
                if (Session.Enforced.Debug == 2) Log.Line($"EmitterEvent: no emitter is working, shield mode: {ShieldMode} - WarmedUp:{WarmedUp} - MaxPower:{GridMaxPower} - ControlWorking:{ControlBlockWorking} - Radius:{ShieldSphere.Radius} - Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
                return;
            }
            DsState.State.EmitterWorking = true;
        }

        private void ComingOnlineSetup()
        {
            if (!_isDedicated) ShellVisibility();
            ShieldEnt.Render.Visible = true;
            _updateRender = true;
            _comingOnline = false;
            LastWokenTick = _tick;
            NotFailed = true;
            WasActive = true;
            WarmedUp = true;

            if (_isServer)
            {
                CleanAll();
                _offlineCnt = -1;
                ShieldChangeState();
                if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: ComingOnlineSetup - ShieldId [{Shield.EntityId}]");
            }
            else
            {
                UpdateSubGrids(true);
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: ComingOnlineSetup - ShieldId [{Shield.EntityId}]");
            }
            lock (Session.Instance.ActiveShields) Session.Instance.ActiveShields.Add(this);
        }

        private void OfflineShield()
        {
            WorldImpactPosition = Vector3D.NegativeInfinity;
            NotFailed = false;
            WasActive = false;
            EnergyHit = false;
            _power = 0.001f;
            _sink.Update();

            ShieldEnt.Render.Visible = false;
            if (_isServer)
            {
                if (!DsState.State.Lowered && !DsState.State.Sleeping)
                {
                    DsState.State.ShieldPercent = 0f;
                    DsState.State.Charge = 0f;
                }
                ShieldChangeState();
            }
            else
            {
                UpdateSubGrids(true);
                Shield.RefreshCustomInfo();
            }
            lock (Session.Instance.ActiveShields) Session.Instance.ActiveShields.Remove(this);

            if (Session.Enforced.Debug == 4) Log.Line($"StateUpdate: ShieldOff - ShieldId [{Shield.EntityId}]");
        }

        private bool ShieldFailing(bool powerState = true)
        {
            if ((!ControlBlockWorking || !powerState || !ShieldComp.EmittersWorking) && _genericDownLoop == -1)
            {
                _genericDownLoop = 0;
            }

            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1 || _empOverLoadLoop > -1)
            {
                FailureConditions();
                return true;
            }
            return false;
        }

        private void FailureConditions()
        {
            if (!WarmedUp && _genericDownLoop != -1)
            {
                _genericDownLoop++;
                if (_genericDownLoop == GenericDownCount) _genericDownLoop = -1;
                return;
            }

            if (_overLoadLoop == 0 || _empOverLoadLoop == 0 || _reModulationLoop == 0 || _genericDownLoop == 0)
            {
                _prevShieldActive = false;
                if (DsState.State.Online)
                {
                    DsState.State.Online = false;
                    if (_overLoadLoop != -1)
                    {
                        DsState.State.Overload = true;
                        DsState.State.Message = true;
                    }

                    if (_empOverLoadLoop != -1)
                    {
                        DsState.State.EmpOverLoad = true;
                        DsState.State.Message = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        DsState.State.Remodulate = true;
                        DsState.State.Message = true;
                    }
                    FailShield();
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
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                        var recharged = _shieldChargeRate * ShieldDownCount / 60;
                        DsState.State.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.10f, ShieldMaxCharge * 0.25f);
                    }
                }
            }

            if (_empOverLoadLoop > -1)
            {
                _empOverLoadLoop++;
                if (_empOverLoadLoop == EmpDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_empOverLoadLoop == EmpDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                        var recharged = _shieldChargeRate * EmpDownCount / 60;
                        DsState.State.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.25f, ShieldMaxCharge * 0.62f);
                    }
                }
            }
        }

        private void FailShield()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                _power = 0.001f;
                _sink.Update();
                ShieldCurrentPower = _sink.CurrentInputByType(GId);
                ResetShape(true, true);
                CleanWebEnts();

                _currentHeatStep = 0;
                _accumulatedHeat = 0;
                _heatCycle = -1;
                Absorb = 0f;
                DsState.State.Charge = 0f;
                DsState.State.ShieldPercent = 0f;
                DsState.State.IncreaseO2ByFPercent = 0f;

                DsState.State.Heat = 0;

                if (!_isDedicated) ShellVisibility(true);
            }

            _prevShieldActive = false;
            DsState.State.Online = false;

            if (!_isDedicated)
            {
                Shield.RefreshCustomInfo();
                ((MyCubeBlock)Shield).UpdateTerminal();
            }
            if (Session.Enforced.Debug == 3) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {ShieldCurrentPower} - gridMax: {GridMaxPower} - currentPower: {GridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private void LosCheck()
        {
            LosCheckTick = uint.MaxValue;
            ShieldComp.CheckEmitters = true;
            FitChanged = true;
            _adjustShape = true;
        }

        private void SetShieldServerStatus(bool powerState)
        {
            DsState.State.Online = ControlBlockWorking && powerState;
            _comingOnline = !_prevShieldActive && DsState.State.Online;

            _prevShieldActive = DsState.State.Online;

            if (!GridIsMobile && (_comingOnline || ShieldComp.O2Updated))
            {
                _ellipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
                ShieldComp.O2Updated = false;
            }
        }

        private void SetShieldClientStatus()
        {
            _comingOnline = !_prevShieldActive && DsState.State.Online;

            _prevShieldActive = DsState.State.Online;
            if (!GridIsMobile && (_comingOnline || !DsState.State.IncreaseO2ByFPercent.Equals(_ellipsoidOxyProvider.O2Level)))
            {
                _ellipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
            }
        }

        private bool ShieldWaking()
        {
            if (_tick < UnsuspendTick)
            {
                if (!DsState.State.Waking)
                {
                    DsState.State.Waking = true;
                    DsState.State.Message = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"Waking: ShieldId [{Shield.EntityId}]");
                }
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                return true;
            }
            if (UnsuspendTick != uint.MinValue && _tick >= UnsuspendTick)
            {
                ResetShape(false);
                _updateRender = true;
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
            Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(ShieldComp.PhysicsOutsideLow, voxel)) continue;

                Shield.Enabled = false;
                DsState.State.FieldBlocked = true;
                DsState.State.Message = true;
                if (Session.Enforced.Debug == 3) Log.Line($"Field blocked: - ShieldId [{Shield.EntityId}]");
                return true;
            }
            DsState.State.FieldBlocked = false;
            return false;
        }

        private bool ShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsState.State.Online)
            {
                Timing();
                if (!DsState.State.Lowered)
                {
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    DsState.State.Lowered = true;
                }
                PowerOnline();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!IsWorking || !ShieldComp.EmittersWorking)
                {
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
                    return false;
                }

                if (GridIsMobile && _lCount == 0 && _count == 0)
                {
                    _updateMobileShape = true;
                }
                else if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }
            if (DsState.State.Lowered && DsState.State.Online && IsWorking)
            {
                if (!_isDedicated) ShellVisibility();
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Lowered = false;
            }
            return false;
        }

        private bool ClientShieldLowered()
        {
            if (WarmedUp && DsState.State.Lowered)
            {
                Timing();
                if (!_clientLowered)
                {
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientLowered = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"Lowered: shield lowered - ShieldId [{Shield.EntityId}]");
                }
                PowerOnline();

                if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }

            if (_clientLowered)
            {
                ShellVisibility();
                _prevShieldActive = false;
            }
            return false;
        }

        private bool ShieldSleeping()
        {
            if (ShieldComp.EmittersSuspended || SlaveControllerLink())
            {
                if (!DsState.State.Sleeping)
                {
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    DsState.State.Sleeping = true;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 4) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Sleeping = true;
                return true;
            }

            if (DsState.State.Sleeping)
            {
                DsState.State.Sleeping = false;
                if (!_isDedicated) ShellVisibility();
                _blockChanged = true;
                _functionalChanged = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Sleeping = false;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 4) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            DsState.State.Sleeping = false;
            return false;
        }

        private bool SlaveControllerLink()
        {
            var notTime = _tick % 120 != 0;
            if (notTime && _slaveLink) return true;
            if (IsStatic || (notTime && _count != -1)) return false;
            var mySize = MyGrid.PositionComp.WorldAABB.Size.Volume;
            var myEntityId = MyGrid.EntityId;
            foreach (var grid in ShieldComp.LinkedGrids.Keys)
            {
                if (grid == MyGrid) continue;
                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                var ds = shieldComponent?.DefenseShields;
                if (ds?.ShieldComp != null && ds.NotFailed && ds.IsWorking)
                {
                    var otherSize = ds.MyGrid.PositionComp.WorldAABB.Size.Volume;
                    var otherEntityId = ds.MyGrid.EntityId;
                    if ((!IsStatic && ds.IsStatic) || mySize < otherSize || (mySize.Equals(otherEntityId) && myEntityId < otherEntityId))
                    {
                        _slaveLink = true;
                        return true;
                    }
                }
            }
            _slaveLink = false;
            return false;
        }

        private void ResetComp()
        {
            ShieldGridComponent comp;
            Shield.CubeGrid.Components.TryGet(out comp);
            if (comp == null)
            {
                ShieldComp = new ShieldGridComponent(this);
                Shield.CubeGrid.Components.Add(ShieldComp);
            }
            else Shield.CubeGrid.Components.TryGet(out ShieldComp);
        }

        private bool Suspend()
        {
            var primeMode = ShieldMode == ShieldType.Station && IsStatic && ShieldComp.StationEmitter == null;
            var betaMode = ShieldMode != ShieldType.Station && !IsStatic && ShieldComp.ShipEmitter == null;
            if (ShieldMode != ShieldType.Station && IsStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Station && !IsStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Unknown) InitSuspend();
            else if (ShieldComp.DefenseShields != this || primeMode || betaMode) InitSuspend(true);
            else if (!DsState.State.ControllerGridAccess) InitSuspend(true);
            else
            {
                if (DsState.State.Suspended)
                {
                    if (Session.Enforced.Debug == 3) Log.Line($"Suspend: controller unsuspending - ShieldId [{Shield.EntityId}]");
                    DsState.State.Suspended = false;
                    DsState.State.Heat = 0;
                    UnsuspendTick = _tick + 1800;

                    _currentHeatStep = 0;
                    _accumulatedHeat = 0;
                    _heatCycle = -1;

                    UpdateEntity();
                    GetEnhancernInfo();
                    GetModulationInfo();
                    if (Session.Enforced.Debug == 3) Log.Line($"Unsuspended: CM:{ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{BoundingRange} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Suspended = false;
            }
            if (DsState.State.Suspended) SetShieldType(true);

            if (DsState.State.Suspended != WasSuspended)
            {
                if (DsState.State.Suspended)
                {
                    bool value;
                    Session.Instance.FunctionalShields.TryRemove(this, out value);
                }
                else Session.Instance.FunctionalShields[this] = false;
            }
            WasSuspended = DsState.State.Suspended;

            return DsState.State.Suspended;
        }

        private void InitSuspend(bool cleanEnts = false)
        {
            SetShieldType(true);
            if (!DsState.State.Suspended)
            {
                if (cleanEnts) InitEntities(false);
                DsState.State.Suspended = true;
                FailShield();
                if (Session.Enforced.Debug == 3) Log.Line($"Suspended: controller mode is: {ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ShieldId [{Shield.EntityId}]");
            }
            if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
            DsState.State.Suspended = true;
        }

        private void GridOwnsController()
        {
            if (MyGrid.BigOwners.Count == 0)
            {
                DsState.State.ControllerGridAccess = false;
                return;
            }

            _gridOwnerId = MyGrid.BigOwners[0];
            _controllerOwnerId = MyCube.OwnerId;

            if (_controllerOwnerId == 0) MyCube.ChangeOwner(_gridOwnerId, MyOwnershipShareModeEnum.Faction);

            var controlToGridRelataion = MyCube.GetUserRelationToOwner(_gridOwnerId);
            DsState.State.InFaction = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.FactionShare;
            DsState.State.IsOwner = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.Owner;

            if (controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.Owner && controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.FactionShare)
            {
                if (DsState.State.ControllerGridAccess)
                {
                    DsState.State.ControllerGridAccess = false;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is not owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.ControllerGridAccess = false;
                return;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            DsState.State.ControllerGridAccess = true;
            return;
        }

        private void UpdateEntity()
        {
            ShieldComp.LinkedGrids.Clear();
            ShieldComp.SubGrids.Clear();
            _blockChanged = true;
            _functionalChanged = true;
            ResetShape(false, true);
            ResetShape(false);
            SetShieldType(false);
            if (!_isDedicated) ShellVisibility(true);
            if (Session.Enforced.Debug == 2) Log.Line($"UpdateEntity: sEnt:{ShieldEnt == null} - sPassive:{_shellPassive == null} - controller mode is: {ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ShieldId [{Shield.EntityId}]");
            Icosphere.ShellActive = null;
            _updateRender = true;
        }

        private void PlayerMessages(PlayerNotice notice)
        {
            double radius;
            if (notice == PlayerNotice.EmpOverLoad || notice == PlayerNotice.OverLoad) radius = 500;
            else radius = ShieldSphere.Radius * 2;

            var center = GridIsMobile ? MyGrid.PositionComp.WorldVolume.Center : OffsetEmitterWMatrix.Translation;
            var sphere = new BoundingSphereD(center, radius);
            var sendMessage = false;
            IMyPlayer targetPlayer = null;
            foreach (var player in Session.Instance.Players.Values)
            {
                if (player.IdentityId != MyAPIGateway.Session.Player.IdentityId) continue;
                if (!sphere.Intersects(player.Character.WorldVolume)) continue;
                sendMessage = true;
                targetPlayer = player;
                break;
            }

            if (sendMessage && !DsSet.Settings.NoWarningSounds) BroadcastSound(targetPlayer, notice);

            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield is reinitializing and checking LOS, attempting startup in 30 seconds!", 4816);
                    break;
                case PlayerNotice.FieldBlocked:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + "-- the shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case PlayerNotice.OverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.EmpOverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield was EMPed, restarting in 60 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.Remodulate:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield remodulating, restarting in 5 seconds.", 4800);
                    break;
                case PlayerNotice.NoLos:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Emitter does not have line of sight, shield offline", 8000, "Red");
                    break;
                case PlayerNotice.NoPower:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }
        }

        private void BroadcastSound(IMyPlayer player, PlayerNotice notice)
        {
            var soundEmitter = Session.Instance.SoundEmitter;
            soundEmitter.Entity = (MyEntity)player.Character;

            MySoundPair pair = null;
            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    pair = new MySoundPair("Arc_reinitializing");
                    break;
                case PlayerNotice.FieldBlocked:
                    pair = new MySoundPair("Arc_solidbody");
                    break;
                case PlayerNotice.OverLoad:
                    pair = new MySoundPair("Arc_overloaded");
                    break;
                case PlayerNotice.EmpOverLoad:
                    pair = new MySoundPair("Arc_EMP");
                    break;
                case PlayerNotice.Remodulate:
                    pair = new MySoundPair("Arc_remodulating");
                    break;
                case PlayerNotice.NoLos:
                    pair = new MySoundPair("Arc_noLOS");
                    break;
                case PlayerNotice.NoPower:
                    pair = new MySoundPair("Arc_insufficientpower");
                    break;
            }
            if (soundEmitter.Entity != null && pair != null) soundEmitter.PlaySingleSound(pair, true);
        }

        private void BroadcastMessage(bool forceNoPower = false)
        {
            if (Session.Enforced.Debug == 3) Log.Line($"Broadcasting message to local playerId{Session.Instance.Players.Count} - Server:{_isServer} - Dedicated:{_isDedicated} - Id:{MyAPIGateway.Multiplayer.MyId}");

            var checkMobLos = GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los;
            if (!DsState.State.EmitterWorking && (!DsState.State.Waking || (checkMobLos && _genericDownLoop > -1) || (checkMobLos && !_isServer)))
            {
                if (checkMobLos) PlayerMessages(PlayerNotice.NoLos);
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) PlayerMessages(PlayerNotice.NoLos);
            }
            else if (DsState.State.NoPower || forceNoPower) PlayerMessages(PlayerNotice.NoPower);
            else if (DsState.State.Overload) PlayerMessages(PlayerNotice.OverLoad);
            else if (DsState.State.EmpOverLoad) PlayerMessages(PlayerNotice.EmpOverLoad);
            else if (DsState.State.FieldBlocked) PlayerMessages(PlayerNotice.FieldBlocked);
            else if (DsState.State.Waking) PlayerMessages(PlayerNotice.EmitterInit);
            else if (DsState.State.Remodulate) PlayerMessages(PlayerNotice.Remodulate);
        }

        private bool ClientOfflineStates()
        {
            var message = DsState.State.Message;
            if (message)
            {
                if (Session.Enforced.Debug == 3) Log.Line("ClientOffline: Broadcasting message");
                BroadcastMessage();
                DsState.State.Message = false;
            }

            if (ShieldComp.DefenseShields != this && DsState.State.Online && !DsState.State.Suspended)
            {
                ShieldComp.DefenseShields = this;
                _prevShieldActive = false;
            }

            var offline = DsState.State.Suspended || !DsState.State.Online || DsState.State.Sleeping || !DsState.State.ControllerGridAccess
                          || !DsState.State.EmitterWorking || DsState.State.Remodulate || DsState.State.Waking || DsState.State.Overload;
            if (offline)
            {
                if (_clientOn)
                {
                    if (!message && GridMaxPower <= 0) BroadcastMessage(true);
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientOn = false;
                    Shield.RefreshCustomInfo();
                }
                _prevShieldActive = false;
                return true;
            }

            if (!_clientOn)
            {
                ShellVisibility();
                Shield.RefreshCustomInfo();
            }
            return false;
        }
    }
}

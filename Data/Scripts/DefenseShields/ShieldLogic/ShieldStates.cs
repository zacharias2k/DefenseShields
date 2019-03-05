namespace DefenseShields
{
    using Support;
    using Sandbox.Game.Entities;
    using VRageMath;

    public partial class DefenseShields
    {
        internal void UpdateSettings(ControllerSettingsValues newSettings)
        {
            if (!_isServer && MyGrid != null && Session.Enforced.Debug == 3) Log.Line($"{MyGrid.DebugName} received settings packet");
            var newShape = newSettings.ExtendFit != DsSet.Settings.ExtendFit || newSettings.FortifyShield != DsSet.Settings.FortifyShield || newSettings.SphereFit != DsSet.Settings.SphereFit;
            DsSet.Settings = newSettings;
            SettingsUpdated = true;
            if (newShape) FitChanged = true;
        }

        internal void UpdateState(ControllerStateValues newState)
        {
            if (!_isServer)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"{MyGrid.DebugName} received state packet: old:{DsState.State.ActiveEmitterId} - new:{newState.ActiveEmitterId}");
                if (!newState.EllipsoidAdjust.Equals(DsState.State.EllipsoidAdjust) || !newState.ShieldFudge.Equals(DsState.State.ShieldFudge) ||
                    !newState.GridHalfExtents.Equals(DsState.State.GridHalfExtents))
                {
                    _updateMobileShape = true;
                }
            }
            DsState.State = newState;
            _clientNotReady = false;
        }

        private void UpdateSettings()
        {
            if (_tick % 33 == 0)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    DsSet.SaveSettings();
                    ResetShape(false);
                }
            }
            else if (_tick % 34 == 0)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    if (!_isServer) DsSet.NetworkUpdate();
                }
            }
        }

        private void ShieldChangeState()
        {
            if (Session.Instance.MpActive)
            {
                DsState.NetworkUpdate();
                if (_isServer) TerminalRefresh(false);
            }
            if (!_isDedicated && DsState.State.Message) BroadcastMessage();

            DsState.State.Message = false;
            DsState.SaveState();
        }

        private bool EntityAlive()
        {
            _tick = Session.Instance.Tick;
            _tick20 = Session.Instance.Tick20;
            _tick60 = Session.Instance.Tick60;
            _tick180 = Session.Instance.Tick180;
            _tick600 = Session.Instance.Tick600;
            _tick1800 = Session.Instance.Tick1800;
            if (_count++ == 59) _count = 0;

            if (WasPaused && (PlayerByShield || MoverByShield || NewEntByShield || LostPings > 59)) UnPauseLogic();
            LostPings = 0;

            var wait = _isServer && !_tick60 && DsState.State.Suspended;

            MyGrid = MyCube.CubeGrid;
            if (MyGrid?.Physics == null) return false;

            if (_resetEntity) ResetEntity();

            if (wait || (!_allInited && !PostInit())) return false;

            if (_tick1800 && Session.Enforced.Debug > 0) Debug();

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

                if (_tick >= LosCheckTick) LosCheck();
                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (ShieldFailing(powerState))
                {
                    _prevShieldActive = false;
                    return false;
                }
                SetShieldServerStatus(powerState);

                if (_shapeEvent || FitChanged) CheckExtents();
                if (_adjustShape) AdjustShape(true);

                StepDamageState();
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

                StepDamageState();

                _clientOn = true;
                _clientLowered = false;
            }

            return true;
        }

        private bool ControllerFunctional()
        {
            if (_blockChanged) BlockMonitor();

            if (Suspend() || ShieldSleeping() || ShieldLowered())
            {
                ControlBlockWorking = false;
                return false;
            }

            if (UpdateDimensions) RefreshDimensions();

            return true;
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

        private void ComingOnlineSetup()
        {
            if (!_isDedicated) ShellVisibility();
            ShieldEnt.Render.Visible = true;
            _updateRender = true;
            _comingOnline = false;
            _firstLoop = false;
            LastWokenTick = _tick;
            NotFailed = true;
            WasActive = true;
            WarmedUp = true;

            if (_isServer)
            {
                CleanWebEnts();
                ResetDamageEffects();
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

        private bool ShieldFailing(bool powerState = true)
        {

            if ((!ControlBlockWorking || !powerState || !DsState.State.EmitterLos) && _genericDownLoop == -1)
            {
                if (!WarmedUp) return true;
                _genericDownLoop = 0;
            }

            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1 || _empOverLoadLoop > -1)
            {
                FailureConditions();
                return true;
            }
            return false;
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

        private bool ShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsState.State.Online)
            {
                if (_shapeEvent || FitChanged) CheckExtents();
                if (_adjustShape) AdjustShape(true);

                StepDamageState();

                if (!DsState.State.Lowered)
                {
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    DsState.State.Lowered = true;
                }
                PowerOnline();
                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!IsWorking || !DsState.State.EmitterLos)
                {
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
                    return false;
                }

                if (_tick600)
                {
                    if (GridIsMobile)_updateMobileShape = true;
                    else RefreshDimensions();
                }

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
                StepDamageState();
                if (!_clientLowered)
                {
                    if (!GridIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientLowered = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"Lowered: shield lowered - ShieldId [{Shield.EntityId}]");
                }
                PowerOnline();

                if (_tick600) RefreshDimensions();
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
            if (!IsFunctional)
            {
                if (ShieldComp.DefenseShields == this)
                {
                    DsState.State.Suspended = true;
                    ShieldComp.DefenseShields = null;
                }
            }
            else if (ShieldMode != ShieldType.Station && IsStatic) InitSuspend();
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
                    Session.Instance.BlockTagActive(Shield);
                    if (Session.Enforced.Debug == 3) Log.Line($"Unsuspended: CM:{ShieldMode} - EW:{DsState.State.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - Range:{BoundingRange} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Suspended = false;
            }
            if (DsState.State.Suspended) SetShieldType(true);

            if (DsState.State.Suspended != WasSuspended)
            {
                if (DsState.State.Suspended)
                {
                    bool value;
                    Session.Instance.BlockTagBackup(Shield);
                    Session.Instance.FunctionalShields.TryRemove(this, out value);
                }
                else
                {
                    Session.Instance.BlockTagActive(Shield);
                    Session.Instance.FunctionalShields[this] = false;
                }
            }

            WasSuspended = DsState.State.Suspended;
            ControlBlockWorking = IsWorking && IsFunctional;

            return WasSuspended || !ControlBlockWorking;
        }

        private void InitSuspend(bool cleanEnts = false)
        {
            SetShieldType(true);
            if (!DsState.State.Suspended)
            {
                if (cleanEnts) InitEntities(false);
                DsState.State.Suspended = true;
                Session.Instance.BlockTagBackup(Shield);
                FailShield();
            }
            if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
            DsState.State.Suspended = true;
        }

        private bool ClientOfflineStates()
        {
            var message = DsState.State.Message;
            if (message) BroadcastMessage();

            if (ShieldComp.DefenseShields != this && DsState.State.Online && !DsState.State.Suspended)
            {
                ShieldComp.DefenseShields = this;
                _prevShieldActive = false;
            }

            var offline = DsState.State.Suspended || !DsState.State.Online || DsState.State.Sleeping || !DsState.State.ControllerGridAccess
                          || !DsState.State.EmitterLos || DsState.State.Remodulate || DsState.State.Waking || DsState.State.Overload;
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

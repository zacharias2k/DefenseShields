using Sandbox.Game;
using VRageMath;
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRage.Utils;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DSControlLarge", "DSControlSmall", "DSControlTable")]
    public partial class DefenseShields : MyGameLogicComponent
    {
        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Dsutil1.Sw.Restart();
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (!BlockFunctional()) return;
                if (GridIsMobile) MobileUpdate();
                else _shapeAdjusted = false;
                if (UpdateDimensions) RefreshDimensions();
                if (_tick == _unsuspendTick) _blocksChanged = true;
                else if (_tick < _unsuspendTick) return;
                if (FitChanged || (_lCount == 1 || _lCount == 6) && _count == 1 && _blocksChanged) BlockChanged(true);
                SetShieldStatus();
                Timing(true);

                if (ShieldComp.ShieldActive)
                {
                    if (_count == 6 && (_lCount == 1 || _lCount == 6) && GridIsMobile && ShieldComp.GetSubGrids.Count > 1) CreateHalfExtents();
                    if (_lCount % 2 != 0 && _count == 20)
                    {
                        GetModulationInfo();
                        if (_reModulationLoop > -1) return;
                    }
                    if (ComingOnline)
                    {
                        if (!GridOwnsController()) return;
                        if (ComingOnline && GridIsMobile && FieldShapeBlocked()) return;
                        ComingOnline = false;

                        if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);

                        _offlineCnt = -1;
                        _shellActive.Render.UpdateRenderObject(true);
                        _shellActive.Render.UpdateRenderObject(false);
                        SyncThreadedEnts(true);
                        if (!WarmedUp) 
                        {
                            WarmedUp = true;
                            if (Session.Enforced.Debug == 1) Log.Line($"Warmup: ShieldId [{Shield.EntityId}]");
                            return;
                        }
                        DsSet.NetworkUpdate();
                        DsSet.SaveSettings();
                    }
                    SyncThreadedEnts();
                    _enablePhysics = false;
                    WebEntities();
                    if (_tick % 60 != 0 && !Session.DedicatedServer) HudCheck();
                }
                else
                {
                    SyncThreadedEnts();
                }
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"PerfCon: Active: {ShieldComp.ShieldActive} - Tick: {_tick} loop: {_lCount}-{_count}", 4);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private bool BlockFunctional()
        {
            if (!AllInited)
            {
                PostInit();
                return false;
            }
            if (_blockChanged) BlockMonitor();

            if (Suspend() || !WarmUpSequence() || ShieldSleeping() || ShieldLowered()) return false;
            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1)
            {
                FailureConditions();
                return false;
            }

            if (ShieldComp.EmitterEvent) EmitterEventDetected();

            if (!Shield.IsWorking || !Shield.IsFunctional || !ShieldComp.EmittersWorking)
            {
                _genericDownLoop = 0;
                return false;
            }

            return ControlBlockWorking = Shield.IsWorking && Shield.IsFunctional; 
        }

        private void Timing(bool cleanUp)
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10) _eCount = 0;
                }
            }
            // damage counter hack - tempoary
            if (_damageReadOut > 0 && _damageCounter > _damageReadOut) _damageCounter = _damageReadOut;
            else if (_damageCounter < _damageReadOut) _damageCounter = _damageReadOut;
            else if (_damageCounter > 1) _damageCounter = _damageCounter * .9835f;
            else _damageCounter = 0f;
            //
            if (_hierarchyDelayed && _tick > _hierarchyTick + 9)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"HierarchyWasDelayed: this:{_tick} - delayedTick: {_hierarchyTick} - ShieldId [{Shield.EntityId}]");
                _hierarchyDelayed = false;
                HierarchyChanged();
            }

            if (_count == 29)
            {
                Shield.RefreshCustomInfo();
                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                {
                    Shield.ShowInToolbarConfig = false;
                    Shield.ShowInToolbarConfig = true;
                }
                _damageReadOut = 0;
            }
            if (_eCount == 0 && _lCount == 0 && _count == 0) _randomCount = _random.Next(0, 10);

            if (cleanUp)
            {
                if (_staleGrids.Count != 0) CleanUp(0);
                if (_lCount == 9 && _count == 58) CleanUp(1);
                if (_effectsCleanup && (_count == 1 || _count == 21 || _count == 41)) CleanUp(2);
                if (_eCount == 0 && _lCount == _randomCount && _count == 15 && (Session.DedicatedServer || Session.IsServer)) DsSet.SaveSettings();

                if ((_lCount * 60 + _count + 1) % 150 == 0)
                {
                    CleanUp(3);
                    CleanUp(4);
                }
            }
        }

        private void SetShieldStatus()
        {
            ShieldComp.ShieldActive = ControlBlockWorking && !ShieldOffline && PowerOnline();

            if (!PrevShieldActive && ShieldComp.ShieldActive) ComingOnline = true;
            else if (ComingOnline && PrevShieldActive && ShieldComp.ShieldActive) ComingOnline = false;

            PrevShieldActive = ShieldComp.ShieldActive;

            if (!GridIsMobile && (ComingOnline || ShieldComp.O2Updated))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, ShieldComp.IncreaseO2ByFPercent);
                ShieldComp.O2Updated = false;
            }
        }

        private void BlockChanged(bool backGround)
        {
            //_oldEllipsoidAdjust = _ellipsoidAdjust;
            FitChanged = false;

            if (GridIsMobile)
            {
                CreateHalfExtents();
                if (_shapeAdjusted) _shapeLoaded = true;
                else if (_shapeLoaded && backGround) MyAPIGateway.Parallel.StartBackground(GetShapeAdjust);
                else if (_shapeLoaded) GetShapeAdjust();
            }

            if (_blocksChanged)
            {
                var check = !ShieldWasSleeping && !Suspended;
                if (Session.Enforced.Debug == 1) Log.Line($"BlockChanged: check:{check} + functional:{_functionalsChanged} - Sleeping:{ShieldWasSleeping} - Suspend:{Suspended} - ShieldId [{Shield.EntityId}]");
                if (!check) return;

                ShieldComp.CheckEmitters = true;
                if (_functionalsChanged)
                {
                    if (backGround) _backGround = MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
                    else BackGroundChecks();
                    _functionalsChanged = false;
                }
                _blocksChanged = false;
            }
        }

        public void UpdateBlockCount()
        {
            var blockCnt = 0;
            foreach (var subGrid in ShieldComp.GetSubGrids)
            {
                if (subGrid == null) continue;
                blockCnt += ((MyCubeGrid)subGrid).BlocksCount;
            }

            if (!_blocksChanged) _blocksChanged = blockCnt != _oldBlockCount;
            _oldBlockCount = blockCnt;
        }

        private void BackGroundChecks()
        {
            lock (_powerSources) _powerSources.Clear();
            lock (_functionalBlocks) _functionalBlocks.Clear();
            //lock (_batterySources) _batterySources.Clear();

            foreach (var grid in ShieldComp.GetLinkedGrids)
            {
                var mechanical = ShieldComp.GetSubGrids.Contains(grid);

                foreach (var block in ((MyCubeGrid)grid).GetFatBlocks())
                {
                    lock (_functionalBlocks) if (block.IsFunctional && mechanical) _functionalBlocks.Add(block);
                    var source = block.Components.Get<MyResourceSourceComponent>();
                    if (source == null) continue;
                    foreach (var type in source.ResourceTypes)
                    {
                        if (type != MyResourceDistributorComponent.ElectricityId) continue;
                        lock (_powerSources) _powerSources.Add(source);
                        //if (source.Entity is IMyBatteryBlock) lock (_batterySources) _batterySources.Add(source);
                        break;
                    }
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"PowerCount: {_powerSources.Count.ToString()} - ShieldId [{Shield.EntityId}]");
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
                _genericDownLoop = 0;
                if (Session.Enforced.Debug == 1) Log.Line($"EmitterEvent: detected an emitter event and no emitter is working, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
        }

        private void FailureConditions()
        {
            if (_overLoadLoop == 0 || _reModulationLoop == 0)
            {
                if (!ShieldOffline) OfflineShield();
                ShieldComp.CheckEmitters = true;
                var realPlayerIds = new HashSet<long>();
                UtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                foreach (var id in realPlayerIds)
                {
                    if (_overLoadLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red", id);
                    if (_reModulationLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield remodulating, restarting in 5 seconds.", 4800, "White", id);
                }

            }
            else if (_genericDownLoop == 0 && !ShieldOffline) OfflineShield();

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    ShieldOffline = false;
                    _reModulationLoop = -1;
                    return;
                }
                return;
            }

            if (_genericDownLoop > -1)
            {
                _genericDownLoop++;
                if (_genericDownLoop == GenericDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        ShieldComp.CheckEmitters = true;
                        _genericDownLoop = 0;
                    }
                    else
                    {
                        ShieldOffline = false;
                        _genericDownLoop = -1;
                    }
                    return;
                }
                return;
            }

            _overLoadLoop++;
            if (_overLoadLoop == ShieldDownCount)
            {
                if (!ShieldComp.EmittersWorking)
                {
                    ShieldComp.CheckEmitters = true;
                    _genericDownLoop = 0;
                }
                else
                {
                    ShieldOffline = false;
                    _overLoadLoop = -1;
                }
                var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
                var nerfer = nerf ? Session.Enforced.Nerf : 1f;
                ShieldBuffer = (_shieldMaxBuffer / 25) * nerfer; // replace this with something that scales based on charge rate
            }
        }

        private void OfflineShield()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Offline count: {_offlineCnt} - resetting all - was: Buffer:{ShieldBuffer} - Absorb:{Absorb} - Percent:{ShieldComp.ShieldPercent} - O2:{ShieldComp.IncreaseO2ByFPercent} - Lowered:{ShieldWasLowered}");

                if (!_power.Equals(0.0001f)) _power = 0.001f;
                Sink.Update();
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                _blockChanged = true;
                _functionalChanged = true;
                _shapeLoaded = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) MobileUpdate();
                else
                {
                    UpdateDimensions = true;
                    if (UpdateDimensions) RefreshDimensions();
                }

                ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                CleanUp(0);
                CleanUp(1);
                CleanUp(3);
                CleanUp(4);
            }
            Absorb = 0f;
            ShieldBuffer = 0f;
            ShieldComp.ShieldPercent = 0f;
            ShieldComp.IncreaseO2ByFPercent = 0f;
            Shield.RefreshCustomInfo();
            Shield.ShowInToolbarConfig = false;
            Shield.ShowInToolbarConfig = true;
            ShieldComp.ShieldActive = false;
            PrevShieldActive = false;
            ShieldWasLowered = false;
            _shellPassive.Render.UpdateRenderObject(false);
            _shellActive.Render.UpdateRenderObject(false);
            ShieldOffline = true;
            if (Session.Enforced.Debug == 1) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {_shieldCurrentPower} - gridMax: {_gridMaxPower} - currentPower: {_gridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private bool ShieldLowered()
        {
            if (!ShieldComp.RaiseShield && WarmedUp && ShieldComp.ShieldActive)
            {
                Timing(false);
                if (!ShieldWasLowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    ShieldComp.IncreaseO2ByFPercent = 0f;
                    _shellPassive.Render.UpdateRenderObject(false);
                    _shellActive.Render.UpdateRenderObject(false);
                    DsSet.NetworkUpdate();
                    DsSet.SaveSettings();
                    ShieldWasLowered = true;
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
                    _createMobileShape = true;
                    MobileUpdate();
                }
                else if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }
            if (ShieldWasLowered && ShieldComp.ShieldActive && Shield.IsWorking)
            {
                if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);
                if (GridIsMobile) _createMobileShape = true;
                else UpdateDimensions = true;

                _shellActive.Render.UpdateRenderObject(false);
                DsSet.NetworkUpdate();
                DsSet.SaveSettings();
                ShieldWasLowered = false;
            }
            return false;
        }

        private bool ShieldSleeping()
        {
            if (ShieldComp.EmittersSuspended)
            {
                if (!ShieldWasSleeping)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    ShieldComp.IncreaseO2ByFPercent = 0f;
                    _shellPassive.Render.UpdateRenderObject(false);
                    _shellActive.Render.UpdateRenderObject(false);
                    DsSet.NetworkUpdate();
                    DsSet.SaveSettings();
                    ShieldWasSleeping = true;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 1) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }

                ShieldWasSleeping = true;
                return ShieldWasSleeping;
            }

            if (ShieldWasSleeping)
            {
                ShieldWasSleeping = false;
                if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);
                _shapeLoaded = true;
                _blockChanged = true;
                _functionalChanged = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) _createMobileShape = true;
                else UpdateDimensions = true;

                _shellActive.Render.UpdateRenderObject(false);
                DsSet.NetworkUpdate();
                DsSet.SaveSettings();
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 1) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            ShieldWasSleeping = false;
            return ShieldWasSleeping;
        }
        #endregion

        #region Block Power Logic
        private bool PowerOnline()
        {
            UpdateGridPower();
            CalculatePowerCharge();
            _power = _shieldConsumptionRate + _shieldMaintaintPower;
            if (WarmedUp && HadPowerBefore && _shieldConsumptionRate.Equals(0f) && ShieldBuffer.Equals(0.01f) && _genericDownLoop == -1)
            {
                //if (Session.Enforced.Debug == 1) Log.Line($"power failing: {_shieldConsumptionRate} - {ShieldBuffer}");
                _power = 0.0001f;
                _genericDownLoop = 0;
                return false;
            }
            if (_power < 0.0001f) _power = 0.001f;
            if (WarmedUp && (_power < _shieldCurrentPower || _count == 28)) Sink.Update();

            if (WarmedUp && Absorb > 0)
            {
                _damageCounter += Absorb;
                _damageReadOut += Absorb;
                _effectsCleanup = true;
                ShieldBuffer -= (Absorb / Session.Enforced.Efficiency);
            }
            else if (WarmedUp && Absorb < 0) ShieldBuffer += (Absorb / Session.Enforced.Efficiency);

            if (WarmedUp && ShieldBuffer < 0)
            {
                //DsSet.NetworkUpdate();
                _overLoadLoop = 0;
            }
            Absorb = 0f;
            return true;
        }

        private void UpdateGridPower()
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;
            _gridAvailablePower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentPower = 0;
            var eId = MyResourceDistributorComponent.ElectricityId;
            lock (_powerSources)
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    var source = _powerSources[i];

                    if (!source.HasCapacityRemaining || !source.Enabled || !source.ProductionEnabled) continue;
                    if (source.Entity is IMyBatteryBlock)
                    {
                        _batteryMaxPower += source.MaxOutputByType(eId);
                        _batteryCurrentPower += source.CurrentOutputByType(eId);
                    }
                    else
                    {
                        _gridMaxPower += source.MaxOutputByType(eId);
                        _gridCurrentPower += source.CurrentOutputByType(eId);
                    }
                }

            if (UseBatteries)
            {
                _gridMaxPower += _batteryMaxPower;
                _gridCurrentPower += _batteryCurrentPower;
            }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            _shieldCurrentPower = Sink.CurrentInputByType(GId);
        }

        private void UpdateGridPowerNew()
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;
            _gridAvailablePower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentPower = 0;
            if (!UseBatteries)
            {
                lock (_batterySources)
                    for (int i = 0; i < _batterySources.Count; i++)
                    {
                        var source = _batterySources[i];
                        if (!source.HasCapacityRemainingByType(GId) || !source.Enabled || !source.ProductionEnabled) continue;
                        if (source.Entity is IMyBatteryBlock)
                        {
                            _batteryMaxPower += source.MaxOutputByType(GId);
                            _batteryCurrentPower += source.CurrentOutputByType(GId);
                        }
                    }
            }

            //_gridMaxPower = MyGridSystem.MaxAvailableResourceByType(GId);
            //_gridCurrentPower = MyGridSystem.TotalRequiredInputByType(GId);

            if (!UseBatteries)
            {
                _gridMaxPower -= _batteryMaxPower;
                _gridCurrentPower -= _batteryCurrentPower;
            }

            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            _shieldCurrentPower = Sink.CurrentInputByType(GId);
        }


        private void CalculatePowerCharge()
        {
            var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
            var rawNerf = nerf ? Session.Enforced.Nerf : 1f;
            var nerfer = rawNerf / _shieldRatio;
            var shieldVol = _detectMatrixOutside.Scale.Volume;
            var powerForShield = 0f;
            const float ratio = 1.25f;
            var percent = Rate * ratio;
            var shieldMaintainPercent = 1 / percent;
            shieldMaintainPercent = shieldMaintainPercent * (ShieldComp.ShieldPercent * 0.01f);
            if (!ShieldComp.RaiseShield) shieldMaintainPercent = shieldMaintainPercent * 0.5f;
            _shieldMaintaintPower = _gridMaxPower * shieldMaintainPercent;
            var fPercent = (percent / ratio) / 100;
            _sizeScaler = (shieldVol / _ellipsoidSurfaceArea) / 2.40063050674088;
            var bufferScaler = 100 / percent * Session.Enforced.BaseScaler / (float)_sizeScaler * nerfer;

            var cleanPower = _gridAvailablePower + _shieldCurrentPower;
            _otherPower = _gridMaxPower - cleanPower;
            powerForShield = (cleanPower * fPercent) - _shieldMaintaintPower;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxChargeRate = rawMaxChargeRate;
            _shieldMaxBuffer = _gridMaxPower * bufferScaler;
            if (_sizeScaler < 1)
            {
                if (ShieldBuffer + _shieldMaxChargeRate * nerfer < _shieldMaxBuffer)
                {
                    _shieldChargeRate = _shieldMaxChargeRate * nerfer;
                    _shieldConsumptionRate = _shieldMaxChargeRate;
                }
                else if (_shieldMaxBuffer - ShieldBuffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - ShieldBuffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
           
            else if (ShieldBuffer + _shieldMaxChargeRate / (_sizeScaler / nerfer) < _shieldMaxBuffer)
            {
                _shieldChargeRate = _shieldMaxChargeRate / ((float)_sizeScaler / nerfer);
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                if (_shieldMaxBuffer - ShieldBuffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - ShieldBuffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
            _powerNeeded = _shieldMaintaintPower + _shieldConsumptionRate + _otherPower;
            //if (ShieldMode == ShieldType.Station) Log.Line($"(Other:{_otherPower} - {powerForShield}])- Clean:{cleanPower} - NEW:{cleanPower * fPercent} - OLD:{_shieldCurrentPower} - MAX:{_gridMaxPower} - CON:{_shieldConsumptionRate}");

            //if (ShieldMode == ShieldType.Station) Log.CleanLine($"[{_tick}] PN:{_powerNeeded} - OP:{_otherPower} - MAINT:{_shieldMaintaintPower} - GAVAIL:{_gridAvailablePower} - GMAX:{_gridMaxPower} - PFS:{powerForShield} - C:{_shieldConsumptionRate} - MAXC:{_shieldMaxChargeRate}");
            if (WarmedUp && _count != -2)
            {
                if (ShieldBuffer < _shieldMaxBuffer) ShieldComp.ShieldPercent = (ShieldBuffer / _shieldMaxBuffer) * 100;
                else if (ShieldBuffer <= 1) ShieldComp.ShieldPercent = 0f;
                else ShieldComp.ShieldPercent = 100f;
            }

            if (WarmedUp && (ShieldBuffer > _shieldMaxBuffer)) ShieldBuffer = _shieldMaxBuffer;
            var roundedGridMax = Math.Round(_gridMaxPower, 1);
            if (WarmedUp && (_powerNeeded > roundedGridMax || powerForShield <= 0))
            {
                if (!ShieldComp.ShieldActive)
                {
                    //Log.Line($"Already offline, don't drain - {rawMaxChargeRate} - maint:{_shieldMaintaintPower} - neededPower:{_powerNeeded} - max:{_gridMaxPower} - other:{_otherPower}");
                    ShieldBuffer = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return;
                }
                _powerLossLoop++;
                if (!ShieldPowerLoss)
                {
                    ShieldPowerLoss = true;
                    var realPlayerIds = new HashSet<long>();
                    UtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, (float)BoundingRange, realPlayerIds);
                    foreach (var id in realPlayerIds)
                    {
                        MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red", id);
                    }
                }

                var shieldLoss = ShieldBuffer * (_powerLossLoop * 0.00008333333f);
                ShieldBuffer = ShieldBuffer - shieldLoss;
                if (ShieldBuffer < 0.01f) ShieldBuffer = 0.01f;

                //Log.Line($"ShieldLoss: {shieldLoss} - Online: {ShieldComp.ShieldActive} - obuffer:{ShieldBuffer} - nbuffer:{ShieldBuffer - shieldLoss} - rawCharge:{rawMaxChargeRate} - neededPower:{_powerNeeded} - max:{_gridMaxPower} - other:{_otherPower}");
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
                return;
            }
            _powerLossLoop = 0;
            if (ShieldPowerLoss)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    ShieldPowerLoss = false;
                    _powerNoticeLoop = 0;
                }
            }

            if (WarmedUp && (ShieldBuffer < _shieldMaxBuffer && _count == 29)) ShieldBuffer += _shieldChargeRate;
            else if (WarmedUp && (ShieldBuffer.Equals(_shieldMaxBuffer)))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }
        }
        #endregion

        #region Field Check
        private bool FieldShapeBlocked()
        {
            if (ModulateVoxels || Session.Enforced.DisableVoxelSupport == 1) return false;

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            MobileUpdate();
            Icosphere.ReturnPhysicsVerts(_detectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(ShieldComp.PhysicsOutsideLow, voxel)) continue;

                Shield.Enabled = false;
                MyVisualScriptLogicProvider.ShowNotification("The shield's field cannot form when in contact with a solid body", 6720, "Blue", Shield.OwnerId);
                return true;
            }
            return false;
        }
        #endregion

        #region Shield Shape
        public void CreateHalfExtents()
        {
            var myAabb = Shield.CubeGrid.PositionComp.LocalAABB;
            var shieldGrid = Shield.CubeGrid;
            var expandedAabb = myAabb;
            foreach (var grid in ShieldComp.GetSubGrids)
            {
                if (grid != null && grid != shieldGrid)
                {
                    var shieldMatrix = shieldGrid.WorldMatrixNormalizedInv;
                    var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                    var gOriBBoxD = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, gQuaternion);
                    gOriBBoxD.Transform(shieldMatrix);
                    expandedAabb.Include(gOriBBoxD.GetAABB());
                }
            }

            if (SphereFit || FortifyShield)
            {
                var extend = ExtendFit ? 2 : 1;
                var fortify = FortifyShield ? 3 : 1;
                var size = expandedAabb.HalfExtents.Max() * fortify;
                var scaler = 4;
                if (shieldGrid.GridSizeEnum == MyCubeSize.Small && !ExtendFit) scaler = 5;
                var vectorSize = new Vector3D(size, size, size);
                var fudge = shieldGrid.GridSize * scaler * extend;
                var extentsDiff = _gridHalfExtents.LengthSquared() - vectorSize.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || _gridHalfExtents == Vector3D.Zero || !fudge.Equals(_shieldFudge)) _gridHalfExtents = vectorSize;
                _shieldFudge = fudge;
            }
            else 
            {
                _shieldFudge = 0f;
                var extentsDiff = _gridHalfExtents.LengthSquared() - expandedAabb.HalfExtents.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || _gridHalfExtents == Vector3D.Zero) _gridHalfExtents = expandedAabb.HalfExtents;
            }
        }

        private void GetShapeAdjust()
        {
            if (SphereFit || FortifyShield) _ellipsoidAdjust = 1f;
            else if (!ExtendFit) _ellipsoidAdjust = UtilsStatic.CreateNormalFit(Shield, _gridHalfExtents);
            else _ellipsoidAdjust = UtilsStatic.CreateExtendedFit(Shield, _gridHalfExtents);
        }

        private void MobileUpdate()
        {
            ShieldComp.ShieldVelocitySqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
            _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
            if (ShieldComp.ShieldVelocitySqr > 0.00001 || _sAvelSqr > 0.00001 || ComingOnline)
            {
                ShieldComp.GridIsMoving = true;
                if (FortifyShield && Math.Sqrt(ShieldComp.ShieldVelocitySqr) > 15)
                {
                    FitChanged = true;
                    FortifyShield = false;
                }
            }
            else ShieldComp.GridIsMoving = false;

            _shapeAdjusted = !_ellipsoidAdjust.Equals(_oldEllipsoidAdjust) || !_gridHalfExtents.Equals(_oldGridHalfExtents);
            _entityChanged = Shield.CubeGrid.Physics.IsMoving || ComingOnline || _shapeAdjusted || _createMobileShape;
            _oldGridHalfExtents = _gridHalfExtents;
            _oldEllipsoidAdjust = _ellipsoidAdjust;
            if (_entityChanged || BoundingRange <= 0) CreateShieldShape();
        }

        private void CreateShieldShape()
        {
            if (GridIsMobile)
            {
                _createMobileShape = false;
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_shapeAdjusted) CreateMobileShape();
                DetectionMatrix = _shieldShapeMatrix * _shieldGridMatrix;
                DetectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
                SOriBBoxD = new MyOrientedBoundingBoxD(DetectionCenter, ShieldSize, _sQuaternion);
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                ShieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.AbsMax());
                EllipsoidSa.Update(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z);
            }
            else
            {
                var emitter = ShieldComp.StationEmitter.Emitter;
                _shieldGridMatrix = emitter.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(Width, Height, Depth));
                _shieldShapeMatrix = MatrixD.Rescale(emitter.LocalMatrix, new Vector3D(Width, Height, Depth));
                ShieldSize = DetectionMatrix.Scale;
                DetectionCenter = emitter.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(emitter.CubeGrid.WorldMatrix);
                SOriBBoxD = new MyOrientedBoundingBoxD(DetectionCenter, ShieldSize, _sQuaternion);
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                ShieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.AbsMax());
                EllipsoidSa.Update(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z);
            }
            BoundingRange = ShieldSize.AbsMax();
            _ellipsoidSurfaceArea = EllipsoidSa.Surface;
            ShieldComp.ShieldVolume = _detectMatrixOutside.Scale.Volume;
            if (!ShieldWasLowered) SetShieldShape();
        }

        private void CreateMobileShape()
        {
            var shieldSize = _gridHalfExtents * _ellipsoidAdjust + _shieldFudge;
            ShieldSize = shieldSize;
            var mobileMatrix = MatrixD.CreateScale(shieldSize);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            _shellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
            _shellActive.PositionComp.LocalMatrix = Matrix.Zero;
            ShieldEnt.PositionComp.LocalMatrix = Matrix.Zero;

            _shellPassive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            _shellActive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            ShieldEnt.PositionComp.LocalMatrix = _shieldShapeMatrix;
            ShieldEnt.PositionComp.LocalAABB = _shieldAabb;

            MatrixD matrix;
            if (!GridIsMobile)
            {
                matrix = _shieldShapeMatrix * ShieldComp.StationEmitter.Emitter.WorldMatrix;
                ShieldEnt.PositionComp.SetWorldMatrix(matrix);
                ShieldEnt.PositionComp.SetPosition(DetectionCenter);
            }
            else
            {
                matrix = _shieldShapeMatrix * Shield.WorldMatrix;
                ShieldEnt.PositionComp.SetWorldMatrix(matrix);
                ShieldEnt.PositionComp.SetPosition(DetectionCenter);
            }
        }

        private void RefreshDimensions()
        {
            UpdateDimensions = false;
            CreateShieldShape();
            Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
            _shapeAdjusted = true;
        }
        #endregion

        #region Shield Draw
        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            var renderId = Shield.CubeGrid.Render.GetRenderObjectID();
            var config = MyAPIGateway.Session.Config;
            var drawIcon = !enemy && SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible;
            if (drawIcon)
            {
                UpdateIcon();
            }

            var passiveVisible = !ShieldPassiveHide || enemy;
            var activeVisible = !ShieldActiveHide || enemy;
            CalcualteVisibility(passiveVisible, activeVisible);

            var impactPos = WorldImpactPosition;
            _localImpactPosition = Vector3D.NegativeInfinity;
            if (impactPos != Vector3D.NegativeInfinity && BulletCoolDown < 0)
            {
                BulletCoolDown = 0;
                HitParticleStart();
                var cubeBlockLocalMatrix = Shield.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                _localImpactPosition = localPosition;
            }
            WorldImpactPosition = Vector3D.NegativeInfinity;

            if (Shield.IsWorking)
            {
                var prevlod = _prevLod;
                var lod = CalculateLod(_onCount);
                if (_shapeAdjusted || _updateRender || lod != prevlod) Icosphere.CalculateTransform(_shieldShapeMatrix, lod);
                _updateRender = false;
                Icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _shellPassive, _shellActive, prevlod, ShieldComp.ShieldPercent, passiveVisible, activeVisible);
            }
            if (sphereOnCamera && Shield.IsWorking) Icosphere.Draw(renderId);
        }

        private int CalculateLod(int onCount)
        {
            var lod = 4;

            if (onCount > 20) lod = 2;
            else if (onCount > 10) lod = 3;

            _prevLod = lod;
            return lod;
        }

        private void HitParticleStart()
        {
            var pos = WorldImpactPosition;
            var matrix = MatrixD.CreateTranslation(pos);

            MyParticlesManager.TryCreateParticleEffect(6667, out _effect, ref matrix, ref pos, _shieldEntRendId, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect == null) return;
            var playerDist = Vector3D.Distance(MyAPIGateway.Session.Player.GetPosition(), pos);
            var radius = playerDist * 0.15d;
            var scale = (playerDist + playerDist * 0.001) / playerDist * 0.03;
            if (ImpactSize < 150)
            {
                scale = scale * 0.3;
                radius = radius * 9;
            }
            else if (ImpactSize > 12000) scale = 0.1;
            else if (ImpactSize > 3600) scale = scale * (ImpactSize / 3600);
            //Log.Line($"D:{playerDist} - R:{radius} - S:{scale} - I:{ImpactSize}");
            _effect.UserRadiusMultiplier = (float)radius;
            _effect.UserEmitterScale = (float) scale;
            _effect.Play();
        }

        public void HitParticleStop()
        {
            if (_effect == null) return;
            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private void CalcualteVisibility(bool passiveVisible, bool activeVisible)
        {
            if (WorldImpactPosition != Vector3D.NegativeInfinity) HitCoolDown = -10;
            else if (HitCoolDown > -11) HitCoolDown++;
            if (HitCoolDown > 59) HitCoolDown = -11;
            var passiveSet = !passiveVisible && !_hideShield && HitCoolDown == -11;
            var passiveReset = passiveVisible && _hideShield || _hideShield && !passiveVisible && !activeVisible && _hideShield && HitCoolDown == -10;
            var passiveFade = HitCoolDown > -1 && !passiveVisible && !activeVisible;
            var fadeReset = !passiveFade && !activeVisible && HitCoolDown != -11;

            if (fadeReset)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = 0f;
                _shellPassive.Render.UpdateRenderObject(true);
            }

            if (passiveFade)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = (HitCoolDown + 1) * 0.0166666666667f;
                _shellPassive.Render.UpdateRenderObject(true);
            }
            else if (passiveSet)
            {
                _hideShield = true;
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = 0f;
            }
            else if (passiveReset)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _hideShield = false;
                _shellPassive.Render.Transparency = 0f;
                _shellPassive.Render.UpdateRenderObject(true);
            }
        }

        private void HudCheck()
        {
            var playerEnt = MyAPIGateway.Session.ControlledObject?.Entity;
            if (playerEnt?.Parent != null) playerEnt = playerEnt.Parent;
            if (playerEnt == null || ShieldComp.ShieldActive && !FriendlyCache.Contains(playerEnt) || !ShieldComp.ShieldActive && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
            {
                if (Session.HudComp != this) return;

                Session.HudComp = null;
                Session.HudShieldDist = double.MaxValue;
                return;
            }

            var distFromShield = Vector3D.DistanceSquared(playerEnt.WorldVolume.Center, DetectionCenter);
            if (Session.HudComp != this && distFromShield <= Session.HudShieldDist)
            {
                Session.HudShieldDist = distFromShield;
                Session.HudComp = this;
            }
        }

        private void UpdateIcon()
        {
            var position = new Vector3D(_shieldIconPos.X, _shieldIconPos.Y, 0);
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;

            var cameraWorldMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            const double scaler = 0.08;
            scale = scaler * scale;

            var icon2FSelect = GetIconMeterfloat();

            var icon1 = GetHudIcon1FromFloat(ShieldComp.ShieldPercent);
            var icon2 = GetHudIcon2FromFloat(icon2FSelect);
            var showIcon2 = !ShieldOffline && ShieldComp.ShieldActive;
            Color color;
            var p = ShieldComp.ShieldPercent;
            if (p > 0 && p < 10 && _lCount % 2 == 0) color = Color.Red;
            else color = Color.White;
            MyTransparentGeometry.AddBillboardOriented(icon1, color, origin, left, up, (float)scale, BlendTypeEnum.LDR); // LDR for mptest, SDR for public
            if (showIcon2 && icon2 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon2, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
        }

        private float GetIconMeterfloat()
        {
            var dps = 1f;
            if (_damageCounter > 1) dps = _damageCounter / Session.Enforced.Efficiency;

            var healing = _shieldChargeRate / Session.Enforced.Efficiency - dps;
            var damage = dps - _shieldChargeRate;

            if (healing > 0 && _damageCounter > 1) return healing;
            else return -damage;
        }

        public static MyStringId GetHudIcon1FromFloat(float percent)
        {
            if (percent >= 99) return HudIconHealth100;
            if (percent >= 90) return HudIconHealth90;
            if (percent >= 80) return HudIconHealth80;
            if (percent >= 70) return HudIconHealth70;
            if (percent >= 60) return HudIconHealth60;
            if (percent >= 50) return HudIconHealth50;
            if (percent >= 40) return HudIconHealth40;
            if (percent >= 30) return HudIconHealth30;
            if (percent >= 20) return HudIconHealth20;
            if (percent > 0) return HudIconHealth10;
            return HudIconOffline;
        }

        public static MyStringId GetHudIcon2FromFloat(float fState)
        {
            if (fState > 0)
            {
                if (fState <= 1) return HudIconHeal100;
                if (fState <= 10) return HudIconHeal90;
                if (fState <= 20) return HudIconHeal80;
                if (fState <= 30) return HudIconHeal70;
                if (fState <= 40) return HudIconHeal60;
                if (fState <= 50) return HudIconHeal50;
                if (fState <= 60) return HudIconHeal40;
                if (fState <= 70) return HudIconHeal30;
                if (fState <= 80) return HudIconHeal20;
                if (fState <= 90) return HudIconHeal10;
                if (fState > 90) return MyStringId.NullOrEmpty;
            }

            if (fState <= -99) return HudIconDps100;
            if (fState <= -90) return HudIconDps90;
            if (fState <= -80) return HudIconDps80;
            if (fState <= -70) return HudIconDps70;
            if (fState <= -60) return HudIconDps60;
            if (fState <= -50) return HudIconDps50;
            if (fState <= -40) return HudIconDps40;
            if (fState <= -30) return HudIconDps30;
            if (fState <= -20) return HudIconDps20;
            if (fState < -10) return HudIconDps10;
            return MyStringId.NullOrEmpty;
        }

        public void DrawShieldDownIcon()
        {
            if (_tick % 60 != 0 && !Session.DedicatedServer) HudCheck();
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            if (!enemy && SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible) UpdateIcon();
        }

        private string GetShieldStatus()
        {
            if (!ControllerGridAccess) return "Invalid Owner";
            if (Suspended || ShieldMode == ShieldType.Unknown) return "Controller Standby";
            if (ShieldWasSleeping) return "Docked";
            if (!Shield.IsWorking || !Shield.IsFunctional) return "Controller Failure";
            if (ShieldComp.EmitterMode < 0 || !ShieldComp.EmittersWorking) return "Emitter Failure";
            if (ShieldOffline && !_overLoadLoop.Equals(-1)) return "Overloaded";
            if (ShieldOffline && _power.Equals(0.0001f)) return "Insufficient Power";
            if (!ShieldComp.RaiseShield && !ShieldOffline) return "Shield Down";
            return ShieldOffline ? "Offline" : "Shield Up";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var secToFull = 0;
            var shieldPercent = ShieldOffline ? 0f : 100f;
            if (ShieldBuffer < _shieldMaxBuffer) shieldPercent = (ShieldBuffer / _shieldMaxBuffer) * 100;
            if (_shieldChargeRate > 0)
            {
                var toMax = _shieldMaxBuffer - ShieldBuffer;
                var secs = toMax / _shieldChargeRate;
                if (secs.Equals(1)) secToFull = 0;
                else secToFull = (int)(secs);
            }

            var shieldPowerNeeds = _powerNeeded;
            var powerUsage = shieldPowerNeeds;
            var otherPower = _otherPower;
            var gridMaxPower = _gridMaxPower;
            if (!UseBatteries)
            {
                powerUsage = powerUsage + _batteryCurrentPower;
                otherPower = _otherPower + _batteryCurrentPower;
                gridMaxPower = gridMaxPower + _batteryMaxPower;
            }

            var status = GetShieldStatus();
            if (status == "Shield Up" || status == "Shield Down")
            {
                stringBuilder.Append("[" + status + "] MaxHP: " + (_shieldMaxBuffer * Session.Enforced.Efficiency).ToString("N0") +
                                     "\n" +
                                     "\n[Shield HP__]: " + (ShieldBuffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                     "\n[HP Per Sec_]: " + (_shieldChargeRate * Session.Enforced.Efficiency).ToString("N0") +
                                     "\n[Damage In__]: " + _damageReadOut.ToString("N0") +
                                     "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                     "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                     "\n[Efficiency__]: " + Session.Enforced.Efficiency.ToString("0.0") +
                                     "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                     "\n[Power Usage]: " + powerUsage.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                     "\n[Shield Power]: " + Sink.CurrentInputByType(GId).ToString("0.0") + " Mw");
            }
            else
            {
                stringBuilder.Append("Shield Status [" + status + "]" +
                                     "\n" +
                                     "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                     "\n[Other Power]: " + otherPower.ToString("0.0") + " Mw" +
                                     "\n[HP Stored]: " + (ShieldBuffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                     "\n[Needed Power]: " + shieldPowerNeeds.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                     "\n[Emitter Detected]: " + ShieldComp.EmittersWorking +
                                     "\n" +
                                     "\n[Grid Owns Controller]: "+ IsOwner +
                                     "\n[In Grid's Faction]: "+ InFaction);

            }
        }
        #endregion

        #region Cleanup
        private void CleanUp(int task)
        {
            try
            {
                switch (task)
                {
                    case 0:
                        IMyCubeGrid grid;
                        while (_staleGrids.TryDequeue(out grid)) lock (WebEnts) WebEnts.Remove(grid);
                        break;
                    case 1:
                        lock (WebEnts)
                        {
                            EnemyShields.Clear();
                            _webEntsTmp.AddRange(WebEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1));
                            foreach (var webent in _webEntsTmp) WebEnts.Remove(webent.Key);
                        }
                        break;
                    case 2:
                        if (ShieldComp.ShieldActive && !ShieldWasLowered)
                        {
                            lock (_functionalBlocks)
                            {
                                foreach (var funcBlock in _functionalBlocks)
                                {
                                    if (funcBlock == null) continue;
                                    if (funcBlock.IsFunctional) funcBlock.SetDamageEffect(false);
                                }
                            }
                        }
                        _effectsCleanup = false;

                        break;
                    case 3:
                        {
                            FriendlyCache.Clear();
                            PartlyProtectedCache.Clear();
                            AuthenticatedCache.Clear();
                            foreach (var sub in ShieldComp.GetSubGrids)
                            {
                                if (sub == null) continue;

                                if (!GridIsMobile && ShieldEnt.PositionComp.WorldVolume.Intersects(sub.PositionComp.WorldVolume))
                                {
                                    var cornersInShield = CustomCollision.NotAllCornersInShield(sub, DetectMatrixOutsideInv);
                                    if (cornersInShield != 8) PartlyProtectedCache.Add(sub);
                                    else if (cornersInShield == 8) FriendlyCache.Add(sub);
                                    continue;
                                }
                                FriendlyCache.Add(sub);
                            }
                            FriendlyCache.Add(ShieldEnt);
                        }
                        break;
                    case 4:
                        {
                            IgnoreCache.Clear();
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CleanUp: {ex}"); }
        }

        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            ModulatorGridComponent modComp;
            Shield.CubeGrid.Components.TryGet(out modComp);
            if (modComp != null)
            {
                var reModulate = ModulateVoxels != modComp.ModulateVoxels || ModulateGrids != modComp.ModulateGrids;
                if (Session.Enforced.Debug == 1 && reModulate) Log.Line($"Remodulate: ModComp change - voxels:[was:{ModulateVoxels} is:{modComp.ModulateVoxels}] Grids:[was:{ModulateGrids} is:{modComp.ModulateGrids}] - ShieldId [{Shield.EntityId}]");
                if (reModulate) _reModulationLoop = 0;

                ModulateVoxels = modComp.ModulateVoxels;
                ModulateGrids = modComp.ModulateGrids;

                var energyDamage = modComp.KineticProtection * 0.01f;
                var kineticDamage = modComp.EnergyProtection * 0.01f;
                ModulateEnergy = energyDamage;
                ModulateKinetic = kineticDamage;
            }
            else
            {
                if (Session.Enforced.Debug == 1 && (!ModulateEnergy.Equals(1f) || !ModulateKinetic.Equals(1f))) Log.Line($"Remodulate: no modComp found, value not default (1f): Energy:{ModulateEnergy} - Kinetic:{ModulateKinetic} - ShieldId [{Shield.EntityId}]");

                ModulateEnergy = 1f;
                ModulateKinetic = 1f;
            }
        }

        private void ShieldDoDamage(float damage, long entityId)
        {
            ImpactSize = damage;
            ((IMySlimBlock)((MyCubeBlock)Shield).SlimBlock).DoDamage(damage, MPdamage, true, null, entityId);
        }
        #endregion


        public override void OnAddedToScene()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"OnAddedToScene: - {ShieldMode} - ShieldId [{Shield.EntityId}]");
                if (!AllInited) return;
                //MyGridSystem = UtilsStatic.GetDistributor((MyCubeGrid)Shield.CubeGrid);
                if (Shield.CubeGrid.IsStatic != IsStatic)
                {
                    Election();
                    RegisterEvents();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"OnRemovedFromScene: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                IsStatic = Shield.CubeGrid.IsStatic;
                RegisterEvents(false);
                InitEntities(false);
                //MyGridSystem = null;
                _shellPassive?.Render?.RemoveRenderObjects();
                _shellActive?.Render?.RemoveRenderObjects();
                ShieldEnt?.Render?.RemoveRenderObjects();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnAddedToContainer()
        {
            if (Entity.InScene) OnAddedToScene();
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void Close()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Close: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                if (Session.Instance.Components.Contains(this)) Session.Instance.Components.Remove(this);
                Icosphere = null;
                //MyGridSystem = null;
                RegisterEvents(false);
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(EllipsoidOxyProvider);

                _power = 0.0001f;
                if (AllInited) Sink.Update();
                if (ShieldComp?.DefenseShields == this)
                {
                    ShieldComp.DefenseShields = null;
                    ShieldComp = null;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"MarkForClose: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        #endregion
    }
}
using System;
using Sandbox.ModAPI;
using DefenseShields.Support;
using VRage;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Block Power Logic
        private bool PowerOnline()
        {
            if (!UpdateGridPower()) return false;
            CalculatePowerCharge();
            _power = _shieldConsumptionRate + _shieldMaintaintPower;
            if (!WarmedUp) return true;
            if (_isServer && _hadPowerBefore && _shieldConsumptionRate.Equals(0f) && DsState.State.Buffer.Equals(0.01f) && _genericDownLoop == -1)
            {
                _power = 0.0001f;
                _genericDownLoop = 0;
                return false;
            }
            if (_power < 0.0001f) _power = 0.001f;

            if (_power < _shieldCurrentPower || _count == 28 && !_power.Equals(_shieldCurrentPower)) Sink.Update();
            if (Absorb > 0)
            {
                _damageReadOut += Absorb;
                _effectsCleanup = true;
                DsState.State.Buffer -= Absorb / Session.Enforced.Efficiency;
            }
            else if (Absorb < 0) DsState.State.Buffer += Absorb / Session.Enforced.Efficiency;

            if (_isServer && DsState.State.Buffer < 0)
            {
                DsState.State.Buffer = 0;
                if (!_empOverLoad) _overLoadLoop = 0;
                else _empOverLoadLoop = 0;
            }
            Absorb = 0f;
            return true;
        }

        private bool UpdateGridPower()
        {
            var tempGridMaxPower = _gridMaxPower;
            var dirtyDistributor = FuncTask.IsComplete && MyGridDistributor != null && !_functionalEvent;
            _gridMaxPower = 0;
            _gridCurrentPower = 0;
            _gridAvailablePower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentPower = 0;
            lock (SubLock)
            {
                if (dirtyDistributor)
                {
                    _gridMaxPower += MyGridDistributor.MaxAvailableResourceByType(GId);
                    if (_gridMaxPower <= 0)
                    {
                        var distOnState = MyGridDistributor.SourcesEnabled;
                        var noObjects = distOnState == MyMultipleEnabledEnum.NoObjects;

                        if (noObjects)
                        {
                            if (Session.Enforced.Debug >= 1) Log.Line($"NoObjects: {MyGrid?.DebugName} - Max:{MyGridDistributor?.MaxAvailableResourceByType(GId)} - Status:{MyGridDistributor?.SourcesEnabled} - Sources:{_powerSources.Count}");
                            FallBackPowerCalc();
                            FunctionalChanged(true);
                        }
                    }
                    else
                    {
                        _gridCurrentPower += MyGridDistributor.TotalRequiredInputByType(GId);
                        if (!DsSet.Settings.UseBatteries)
                        {
                            for (int i = 0; i < _batteryBlocks.Count; i++)
                            {
                                var battery = _batteryBlocks[i];
                                if (!battery.IsWorking) continue;
                                var maxOutput = battery.MaxOutput;
                                if (maxOutput <= 0) continue;
                                var currentOutput = battery.CurrentOutput;

                                _gridMaxPower -= maxOutput;
                                _gridCurrentPower -= currentOutput;
                                _batteryMaxPower += maxOutput;
                                _batteryCurrentPower += currentOutput;
                            }
                        }
                    }
                }
                else FallBackPowerCalc();
            }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            if (!_gridMaxPower.Equals(tempGridMaxPower) || _roundedGridMax <= 0) _roundedGridMax = Math.Round(_gridMaxPower, 1);
            _shieldCurrentPower = Sink.CurrentInputByType(GId);
            return _gridMaxPower > 0;
        }

        private void FallBackPowerCalc()
        {
            var rId = GId;
            for (int i = 0; i < _powerSources.Count; i++)
            {
                var source = _powerSources[i];
                if (!source.Enabled || !source.ProductionEnabledByType(rId) || source.Entity is IMyReactor && !source.HasCapacityRemainingByType(rId)) continue;
                if (source.Entity is IMyBatteryBlock)
                {
                    _batteryMaxPower += source.MaxOutputByType(rId);
                    _batteryCurrentPower += source.CurrentOutputByType(rId);
                }
                else
                {
                    _gridMaxPower += source.MaxOutputByType(rId);
                    _gridCurrentPower += source.CurrentOutputByType(rId);
                }
            }

            if (DsSet.Settings.UseBatteries)
            {
                _gridMaxPower += _batteryMaxPower;
                _gridCurrentPower += _batteryCurrentPower;
            }
        }

        private void CalculatePowerCharge()
        {
            var capScaler = Session.Enforced.CapScaler;
            var hpsEfficiency = Session.Enforced.HpsEfficiency;
            var baseScaler = Session.Enforced.BaseScaler;
            var maintenanceCost = Session.Enforced.MaintenanceCost;

            if (hpsEfficiency <= 0) hpsEfficiency = 1f;
            if (baseScaler <= 0) baseScaler = 1;
            if (maintenanceCost <= 0) maintenanceCost = 1f;

            const float ratio = 1.25f;
            var percent = DsSet.Settings.Rate * ratio;
            var shieldMaintainPercent = maintenanceCost / percent;
            var sizeScaler = _shieldVol / (_ellipsoidSurfaceArea * 2.40063050674088);
            var gridIntegrity = DsState.State.GridIntegrity * 0.01f;
            var hpScaler = 1f;
            _sizeScaler = sizeScaler >= 1d ? sizeScaler : 1d;

            float bufferScaler;
            if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) bufferScaler = 100 / percent * baseScaler * _shieldRatio;
            else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;

            var hpBase = _gridMaxPower * bufferScaler;
            if (capScaler > 0 && hpBase > gridIntegrity) hpScaler = gridIntegrity * capScaler / hpBase;

            shieldMaintainPercent = shieldMaintainPercent * DsState.State.EnhancerPowerMulti * (DsState.State.ShieldPercent * 0.01f);
            if (DsState.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            _shieldMaintaintPower = _gridMaxPower * hpScaler * shieldMaintainPercent;

            ShieldMaxBuffer = hpBase * hpScaler;

            //if (_tick600) Log.Line($"gridName:{MyGrid.DebugName} - {hpBase} > {gridIntegrity} ({hpBase > gridIntegrity}) - hpScaler:{hpScaler}");

            var powerForShield = PowerNeeded(percent, ratio, hpsEfficiency);

            if (!WarmedUp) return;

            if (DsState.State.Buffer > ShieldMaxBuffer) DsState.State.Buffer = ShieldMaxBuffer;

            if (PowerLoss(powerForShield)) return;

            ChargeBuffer();
            if (DsState.State.Buffer < ShieldMaxBuffer) DsState.State.ShieldPercent = DsState.State.Buffer / ShieldMaxBuffer * 100;
            else if (DsState.State.Buffer < ShieldMaxBuffer * 0.1) DsState.State.ShieldPercent = 0f;
            else DsState.State.ShieldPercent = 100f;
        }

        private float PowerNeeded(float percent, float ratio, float hpsEfficiency)
        {
            var powerForShield = 0f;
            var fPercent = percent / ratio * 0.01f;

            var cleanPower = _gridAvailablePower + _shieldCurrentPower;
            _otherPower = _gridMaxPower - cleanPower;
            powerForShield = (cleanPower * fPercent) - _shieldMaintaintPower;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxChargeRate = rawMaxChargeRate;
            var chargeSize = _shieldMaxChargeRate * hpsEfficiency / _sizeScaler;
            if (DsState.State.Buffer + chargeSize < ShieldMaxBuffer)
            {
                _shieldChargeRate = (float)chargeSize;
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                var remaining = ShieldMaxBuffer - DsState.State.Buffer;
                var remainingScaled = remaining / chargeSize;
                _shieldConsumptionRate = (float)(remainingScaled * _shieldMaxChargeRate);
                _shieldChargeRate = (float)(chargeSize * remainingScaled);
            }

            _powerNeeded = _shieldMaintaintPower + _shieldConsumptionRate + _otherPower;
            return powerForShield;
        }

        private bool PowerLoss(float powerForShield)
        {
            if (_powerNeeded > _roundedGridMax || powerForShield <= 0)
            {
                if (_isServer && !DsState.State.Online)
                {
                    DsState.State.Buffer = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return true;
                }
                _powerLossLoop++;
                if (_isServer && !DsState.State.NoPower)
                {
                    DsState.State.NoPower = true;
                    DsState.State.Message = true;
                    if (Session.Enforced.Debug >= 1) Log.Line($"StateUpdate: NoPower - forShield:{powerForShield} - rounded:{_roundedGridMax} - max:{_gridMaxPower} - avail{_gridAvailablePower} - sCurr:{_shieldCurrentPower} - count:{_powerSources.Count} - DistEna:{MyGridDistributor.SourcesEnabled} - State:{MyGridDistributor?.ResourceState} - ShieldId [{Shield.EntityId}]");
                    ShieldChangeState();
                }

                var shieldLoss = ShieldMaxBuffer * 0.0016667f;
                DsState.State.Buffer = DsState.State.Buffer - shieldLoss;
                if (DsState.State.Buffer < 0.01f) DsState.State.Buffer = 0.01f;

                if (DsState.State.Buffer < ShieldMaxBuffer) DsState.State.ShieldPercent = DsState.State.Buffer / ShieldMaxBuffer * 100;
                else if (DsState.State.Buffer < ShieldMaxBuffer * 0.1) DsState.State.ShieldPercent = 0f;
                else DsState.State.ShieldPercent = 100f;

                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
                return true;
            }

            _powerLossLoop = 0;

            if (_isServer && DsState.State.NoPower)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    DsState.State.NoPower = false;
                    _powerNoticeLoop = 0;
                    if (Session.Enforced.Debug >= 1) Log.Line($"StateUpdate: PowerRestored - ShieldId [{Shield.EntityId}]");
                    ShieldChangeState();
                }
            }
            return false;
        }

        private void ChargeBuffer()
        {
            var heat = DsState.State.Heat * 0.1;
            if (heat > 10) heat = 10;

            if (heat >= 10) _shieldChargeRate = 0;
            else
            {
                var expChargeReduction = (float)Math.Pow(2, heat);
                _shieldChargeRate = _shieldChargeRate / expChargeReduction;
            }
            if (_count == 29 && DsState.State.Buffer < ShieldMaxBuffer) DsState.State.Buffer += _shieldChargeRate;
            else if (DsState.State.Buffer.Equals(ShieldMaxBuffer))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }
        }

        private void HeatManager()
        {
            var hp = ShieldMaxBuffer * Session.Enforced.Efficiency;
            var oldHeat = DsState.State.Heat;
            if (_damageReadOut > 0 && _heatCycle == -1)
            {
                if (_count == 29) _accumulatedHeat += _damageReadOut;
                _heatCycle = 0;
            }
            else if (_heatCycle > -1)
            {
                if (_count == 29) _accumulatedHeat += _damageReadOut;
                _heatCycle++;
            }

            var empProt = DsState.State.EmpProtection && ShieldMode != ShieldType.Station;
            if (empProt && _heatCycle == 0)
            {
                _empScaleHp = 0.1f;
                _empScaleTime = 10;
            }
            else if (!empProt && _heatCycle == 0)
            {
                _empScaleHp = 1f;
                _empScaleTime = 1;
            }

            var hpLoss = 0.01 * _empScaleHp;
            var nextThreshold = hp * hpLoss * _currentHeatStep;
            var currentThreshold = hp * hpLoss * (_currentHeatStep - 1);
            var scaledOverHeat = OverHeat / _empScaleTime;
            var lastStep = _currentHeatStep == 10;
            var overloadStep = _heatCycle == scaledOverHeat;
            var scaledHeatingSteps = HeatingStep / _empScaleTime;
            var afterOverload = _heatCycle > scaledOverHeat;
            var nextCycle = _heatCycle == _currentHeatStep * scaledHeatingSteps + scaledOverHeat;
            var overload = _accumulatedHeat > hpLoss;
            var pastThreshold = _accumulatedHeat > nextThreshold;
            var metThreshold = _accumulatedHeat > currentThreshold;
            var underThreshold = !pastThreshold && !metThreshold;
            var venting = lastStep && pastThreshold;
            var leftCritical = lastStep && _tick >= _heatVentingTick;
            var backOneCycles = (_currentHeatStep - 1) * scaledHeatingSteps + scaledOverHeat + 1;
            var backTwoCycles = (_currentHeatStep - 2) * scaledHeatingSteps + scaledOverHeat + 1;

            if (overloadStep)
            {
                if (overload)
                {
                    _currentHeatStep = 1;
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug >= 1) Log.Line($"overh - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold:{hpLoss} - ShieldId [{Shield.EntityId}]");
                    _accumulatedHeat = 0;
                }
                else
                {
                    DsState.State.Heat = 0;
                    _currentHeatStep = 0;
                    if (Session.Enforced.Debug >= 1) Log.Line($"under - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{_accumulatedHeat} - threshold:{hpLoss} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                    _heatCycle = -1;
                    _accumulatedHeat = 0;
                }
            }
            else if (nextCycle && afterOverload && !lastStep)
            {
                if (_empScaleTime == 10)
                {
                    if (_accumulatedHeat > 0) _fallbackCycle = 1;
                    else _fallbackCycle++;
                }

                if (pastThreshold)
                {
                    _currentHeatStep++;
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug >= 1) Log.Line($"incre - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold:{currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                    _accumulatedHeat = 0;
                }
                else if (metThreshold)
                {
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug >= 1) Log.Line($"uncha - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backOneCycles} - heat:{_accumulatedHeat} - threshold:{currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                    _heatCycle = backOneCycles;
                    _accumulatedHeat = 0;
                }
                else
                {
                    _heatCycle = backOneCycles;
                    _accumulatedHeat = 0;
                }

                if (empProt && _fallbackCycle == FallBackStep || !empProt && underThreshold)
                {
                    if (_currentHeatStep > 0) _currentHeatStep--;
                    if (_currentHeatStep == 0)
                    {
                        DsState.State.Heat = 0;
                        _currentHeatStep = 0;
                        if (Session.Enforced.Debug >= 1) Log.Line($"nohea - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{_accumulatedHeat} - threshold:{currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                        _heatCycle = -1;
                        _accumulatedHeat = 0;
                        _fallbackCycle = 0;
                    }
                    else
                    {
                        DsState.State.Heat = _currentHeatStep * 10;
                        if (Session.Enforced.Debug >= 1) Log.Line($"decto - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{_accumulatedHeat} - threshold:{currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                        _heatCycle = backTwoCycles;
                        _accumulatedHeat = 0;
                        _fallbackCycle = 0;
                    }
                }
            }
            else if (venting)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"mainc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold: {currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                _heatVentingTick = _tick + CoolingStep;
                _accumulatedHeat = 0;
            }
            else if (leftCritical)
            {
                if (_currentHeatStep >= 10) _currentHeatStep--;
                if (Session.Enforced.Debug >= 1) Log.Line($"leftc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{_accumulatedHeat} - threshold: {currentThreshold} - nThreshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                DsState.State.Heat = _currentHeatStep * 10;
                _heatCycle = backTwoCycles;
                _heatVentingTick = uint.MaxValue;
                _accumulatedHeat = 0;
            }

            if (_heatCycle > HeatingStep * 10 + OverHeat && _tick >= _heatVentingTick)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"HeatCycle over limit, resetting: heatCycle:{_heatCycle} - fallCycle:{_fallbackCycle}");
                _heatCycle = -1;
                _fallbackCycle = 0;
            }

            if (!oldHeat.Equals(DsState.State.Heat))
            {
                if (Session.Enforced.Debug >= 2) Log.Line($"StateUpdate: HeatChange - ShieldId [{Shield.EntityId}]");
                ShieldChangeState();
            }
        }
        #endregion
    }
}
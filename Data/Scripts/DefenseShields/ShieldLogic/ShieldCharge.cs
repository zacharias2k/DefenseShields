namespace DefenseShields
{
    using System;
    using global::DefenseShields.Support;
    using Sandbox.ModAPI;
    using VRage;
    using VRageMath;

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
            if (_power < ShieldCurrentPower || (_count == 28 && !_power.Equals(ShieldCurrentPower))) _sink.Update();
            if (!_isDedicated)
            {
                var hitCnt = ShieldHits.Count;
                if (hitCnt > 0)
                {
                    for (int i = 0; i < hitCnt; i++)
                    {
                        var hit = ShieldHits[i];
                        ImpactSize = 12001;
                        if (Session.Enforced.Debug >= 2) Log.Line($"MpAbsorb: Amount:{hit.Amount} - attacker:{hit.Attacker != null} - dType:{hit.DamageType} - hitPos:{hit.HitPos}");
                        if (hit.HitPos != Vector3D.Zero && WorldImpactPosition == Vector3D.NegativeInfinity) WorldImpactPosition = hit.HitPos;
                        Absorb += hit.Amount;
                    }
                    ShieldHits.Clear();
                }
            }
            if (Absorb > 0)
            {
                _damageReadOut += Absorb;
                EffectsCleanTick = _tick;
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
            var tempGridMaxPower = GridMaxPower;
            var cleanDistributor = FuncTask.IsComplete && MyGridDistributor != null && !_functionalEvent;
            GridAvailablePower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentPower = 0;
            lock (GetCubesLock)
            {
                if (cleanDistributor)
                {
                    if (GridMaxPower <= 0)
                    {
                        var distOnState = MyGridDistributor.SourcesEnabled;
                        var noObjects = distOnState == MyMultipleEnabledEnum.NoObjects;
                        if (noObjects)
                        {
                            if (Session.Enforced.Debug >= 1) Log.Line($"NoObjects: {MyGrid?.DebugName} - Max:{MyGridDistributor?.MaxAvailableResourceByType(GId)} - Status:{MyGridDistributor?.SourcesEnabled} - Sources:{_powerSources.Count}");
                            FallBackPowerCalc();
                            FunctionalChanged(true);
                        }
                        else
                        {
                            GridMaxPower = MyGridDistributor.MaxAvailableResourceByType(GId);
                            GridCurrentPower = MyGridDistributor.TotalRequiredInputByType(GId);
                        }
                    }
                    else
                    {
                        if (!DsSet.Settings.UseBatteries)
                        {
                            for (int i = 0; i < _batteryBlocks.Count; i++)
                            {
                                var battery = _batteryBlocks[i];
                                if (!battery.IsWorking) continue;
                                var maxOutput = battery.MaxOutput;
                                if (maxOutput <= 0) continue;
                                var currentOutput = battery.CurrentOutput;

                                GridMaxPower -= maxOutput;
                                GridCurrentPower -= currentOutput;
                                _batteryMaxPower += maxOutput;
                                _batteryCurrentPower += currentOutput;
                            }
                        }
                    }
                }
                else FallBackPowerCalc();
            }
            GridAvailablePower = GridMaxPower - GridCurrentPower;
            if (!GridMaxPower.Equals(tempGridMaxPower) || _roundedGridMax <= 0) _roundedGridMax = Math.Round(GridMaxPower, 1);
            return GridMaxPower > 0;
        }

        private void FallBackPowerCalc()
        {
            GridMaxPower = 0;
            GridCurrentPower = 0;
            var rId = GId;
            for (int i = 0; i < _powerSources.Count; i++)
            {
                var source = _powerSources[i];
                if (!source.Enabled || !source.ProductionEnabledByType(rId) || (source.Entity is IMyReactor && !source.HasCapacityRemainingByType(rId)))
                    continue;

                if (source.Entity is IMyBatteryBlock)
                {
                    _batteryMaxPower += source.MaxOutputByType(rId);
                    _batteryCurrentPower += source.CurrentOutputByType(rId);
                }
                else
                {
                    GridMaxPower += source.MaxOutputByType(rId);
                    GridCurrentPower += source.CurrentOutputByType(rId);
                }
            }

            if (DsSet.Settings.UseBatteries)
            {
                GridMaxPower += _batteryMaxPower;
                GridCurrentPower += _batteryCurrentPower;
            }
        }

        private void CalculatePowerCharge()
        {
            var capScaler = Session.Enforced.CapScaler;
            var hpsEfficiency = Session.Enforced.HpsEfficiency;
            var baseScaler = Session.Enforced.BaseScaler;
            var maintenanceCost = Session.Enforced.MaintenanceCost;
            var efficiency = Session.Enforced.Efficiency;

            if (hpsEfficiency <= 0) hpsEfficiency = 1f;
            if (baseScaler < 1) baseScaler = 1;
            if (maintenanceCost <= 0) maintenanceCost = 1f;

            var percent = DsSet.Settings.Rate * ChargeRatio;
            var chargePercent = percent / ChargeRatio * ConvToDec;

            var shieldMaintainPercent = maintenanceCost / percent;
            _sizeScaler = _shieldVol / (_ellipsoidSurfaceArea * MagicRatio);

            float bufferScaler;
            if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) bufferScaler = 100 / percent * baseScaler * _shieldRatio;
            else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;

            var hpBase = GridMaxPower * bufferScaler;

            var gridIntegrity = DsState.State.GridIntegrity * (efficiency * ConvToDec) * ConvToDec;
            if (capScaler > 0) gridIntegrity *= capScaler;

            var hpScaler = 1f;
            if (hpBase > gridIntegrity) hpScaler = gridIntegrity / hpBase;

            shieldMaintainPercent = shieldMaintainPercent * DsState.State.EnhancerPowerMulti * (DsState.State.ShieldPercent * ConvToDec);
            if (DsState.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            _shieldMaintaintPower = GridMaxPower * hpScaler * shieldMaintainPercent;

            ShieldMaxBuffer = hpBase * hpScaler;
            var powerForShield = PowerNeeded(chargePercent, hpsEfficiency, hpScaler);
            if (!WarmedUp) return;

            if (DsState.State.Buffer > ShieldMaxBuffer) DsState.State.Buffer = ShieldMaxBuffer;
            if (_isServer)
            {
                var powerLost = powerForShield <= 0 || _powerNeeded > _roundedGridMax || (_roundedGridMax - _powerNeeded) / Math.Abs(_powerNeeded) * 100 < 0.001;
                var serverNoPower = DsState.State.NoPower;
                if (powerLost || serverNoPower)
                {
                    if (PowerLoss(powerForShield, powerLost, serverNoPower))
                    {
                        _powerFail = true;
                        return;
                    }
                }
                else _powerFail = false;
            }

            if (DsState.State.Heat != 0) UpdateHeatState();

            if (_count == 29 && DsState.State.Buffer < ShieldMaxBuffer) DsState.State.Buffer += _shieldChargeRate;
            else if (DsState.State.Buffer.Equals(ShieldMaxBuffer))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }

            if (DsState.State.Buffer < ShieldMaxBuffer) DsState.State.ShieldPercent = DsState.State.Buffer / ShieldMaxBuffer * 100;
            else if (DsState.State.Buffer < ShieldMaxBuffer * 0.1) DsState.State.ShieldPercent = 0f;
            else DsState.State.ShieldPercent = 100f;
        }

        private float PowerNeeded(float chargePercent, float hpsEfficiency, float hpScaler)
        {
            var powerScaler = 1f;
            if (hpScaler < 0.5) powerScaler = hpScaler + hpScaler;

            var cleanPower = GridAvailablePower + ShieldCurrentPower;
            _otherPower = GridMaxPower - cleanPower;
            var powerForShield = ((cleanPower * chargePercent) - _shieldMaintaintPower) * powerScaler;
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

        private bool PowerLoss(float powerForShield, bool powerLost, bool serverNoPower)
        {
            if (powerLost)
            {
                if (!DsState.State.Online)
                {
                    DsState.State.Buffer = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return true;
                }
                if (!DsState.State.NoPower)
                {
                    DsState.State.NoPower = true;
                    DsState.State.Message = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: NoPower - forShield:{powerForShield} - rounded:{_roundedGridMax} - max:{GridMaxPower} - avail{GridAvailablePower} - sCurr:{ShieldCurrentPower} - count:{_powerSources.Count} - DistEna:{MyGridDistributor?.SourcesEnabled} - State:{MyGridDistributor?.ResourceState} - ShieldId [{Shield.EntityId}]");
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
            if (serverNoPower)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    DsState.State.NoPower = false;
                    _powerNoticeLoop = 0;
                    if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: PowerRestored - ShieldId [{Shield.EntityId}]");
                    ShieldChangeState();
                }
            }
            return false;
        }

        private void UpdateHeatState()
        {
            var heat = DsState.State.Heat;
            heat = heat / 10;

            if (heat >= 10) _shieldChargeRate = 0;
            else
            {
                var expChargeReduction = ExpChargeReductions[heat];
                _shieldChargeRate = _shieldChargeRate / expChargeReduction;
            }
        }

        private void HeatManager()
        {
            var hp = ShieldMaxBuffer * Session.Enforced.Efficiency;
            var oldHeat = DsState.State.Heat;
            if (_count == 29 && _damageReadOut > 0 && _heatCycle == -1)
            {
                _accumulatedHeat += _damageReadOut;
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

            var hpLoss = Session.Enforced.HeatScaler * _empScaleHp;
            var nextThreshold = hp * hpLoss * (_currentHeatStep + 1);
            var currentThreshold = hp * hpLoss * _currentHeatStep;
            var scaledOverHeat = OverHeat / _empScaleTime;
            var lastStep = _currentHeatStep == 10;
            var overloadStep = _heatCycle == scaledOverHeat;
            var scaledHeatingSteps = HeatingStep / _empScaleTime;
            var afterOverload = _heatCycle > scaledOverHeat;
            var nextCycle = _heatCycle == (_currentHeatStep * scaledHeatingSteps) + scaledOverHeat;
            var overload = _accumulatedHeat > hp * hpLoss * 2;
            var pastThreshold = _accumulatedHeat > nextThreshold;
            var metThreshold = _accumulatedHeat > currentThreshold;
            var underThreshold = !pastThreshold && !metThreshold;
            var venting = lastStep && pastThreshold;
            var leftCritical = lastStep && _tick >= _heatVentingTick;
            var backOneCycles = ((_currentHeatStep - 1) * scaledHeatingSteps) + scaledOverHeat + 1;
            var backTwoCycles = ((_currentHeatStep - 2) * scaledHeatingSteps) + scaledOverHeat + 1;

            if (overloadStep)
            {
                if (overload)
                {
                    if (Session.Enforced.Debug == 3) Log.Line($"overh - stage:{_currentHeatStep + 1} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold:{hp * hpLoss * 2}[{hp / hp * hpLoss * (_currentHeatStep + 1)}] - nThreshold:{hp * hpLoss * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
                    _currentHeatStep = 1;
                    DsState.State.Heat = _currentHeatStep * 10;
                    _accumulatedHeat = 0;
                }
                else
                {
                    if (Session.Enforced.Debug == 3) Log.Line($"under - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{_accumulatedHeat} - threshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                    DsState.State.Heat = 0;
                    _currentHeatStep = 0;
                    _heatCycle = -1;
                    _accumulatedHeat = 0;
                }
            }
            else if (nextCycle && afterOverload && !lastStep)
            {
                if (_empScaleTime == 10)
                {
                    if (_accumulatedHeat > 0)
                    {
                        _fallbackCycle = 1;
                        _accumulatedHeat = 0;
                    }
                    else _fallbackCycle++;
                }

                if (pastThreshold)
                {
                    if (Session.Enforced.Debug == 4) Log.Line($"incre - stage:{_currentHeatStep + 1} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold:{nextThreshold}[{hp / hp * hpLoss * (_currentHeatStep + 1)}] - nThreshold:{hp * hpLoss * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
                    _currentHeatStep++;
                    DsState.State.Heat = _currentHeatStep * 10;
                    _accumulatedHeat = 0;
                    if (_currentHeatStep == 10) _heatVentingTick = _tick + CoolingStep;
                }
                else if (metThreshold)
                {
                    if (Session.Enforced.Debug == 4) Log.Line($"uncha - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backOneCycles} - heat:{_accumulatedHeat} - threshold:{nextThreshold} - nThreshold:{hp * hpLoss * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
                    DsState.State.Heat = _currentHeatStep * 10;
                    _heatCycle = backOneCycles;
                    _accumulatedHeat = 0;
                }
                else _heatCycle = backOneCycles;

                if ((empProt && _fallbackCycle == FallBackStep) || (!empProt && underThreshold))
                {
                    if (_currentHeatStep == 0)
                    {
                        DsState.State.Heat = 0;
                        _currentHeatStep = 0;
                        if (Session.Enforced.Debug == 4) Log.Line($"nohea - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{_accumulatedHeat} - ShieldId [{Shield.EntityId}]");
                        _heatCycle = -1;
                        _accumulatedHeat = 0;
                        _fallbackCycle = 0;
                    }
                    else
                    {
                        if (Session.Enforced.Debug == 4) Log.Line($"decto - stage:{_currentHeatStep - 1} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{_accumulatedHeat} - threshold:{currentThreshold} - ShieldId [{Shield.EntityId}]");
                        _currentHeatStep--;
                        DsState.State.Heat = _currentHeatStep * 10;
                        _heatCycle = backTwoCycles;
                        _accumulatedHeat = 0;
                        _fallbackCycle = 0;
                    }
                }
            }
            else if (venting)
            {
                if (Session.Enforced.Debug == 4) Log.Line($"mainc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{_accumulatedHeat} - threshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                _heatVentingTick = _tick + CoolingStep;
                _accumulatedHeat = 0;
            }
            else if (leftCritical)
            {
                if (_currentHeatStep >= 10) _currentHeatStep--;
                if (Session.Enforced.Debug == 4) Log.Line($"leftc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{_accumulatedHeat} - threshold:{nextThreshold}[{hp / hp * hpLoss * (_currentHeatStep + 1)}] - nThreshold:{hp * hpLoss * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
                DsState.State.Heat = _currentHeatStep * 10;
                _heatCycle = backTwoCycles;
                _heatVentingTick = uint.MaxValue;
                _accumulatedHeat = 0;
            }

            if (_heatCycle > (HeatingStep * 10) + OverHeat && _tick >= _heatVentingTick)
            {
                if (Session.Enforced.Debug == 4) Log.Line($"HeatCycle over limit, resetting: heatCycle:{_heatCycle} - fallCycle:{_fallbackCycle}");
                _heatCycle = -1;
                _fallbackCycle = 0;
            }

            if (!oldHeat.Equals(DsState.State.Heat))
            {
                if (Session.Enforced.Debug == 4) Log.Line($"StateUpdate: HeatChange - ShieldId [{Shield.EntityId}]");
                ShieldChangeState();
            }
        }
        #endregion
    }
}
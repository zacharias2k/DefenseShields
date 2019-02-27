namespace DefenseShields
{
    using System;
    using Support;
    using Sandbox.ModAPI;
    using VRage;

    public partial class DefenseShields
    {
        #region Block Power Logic
        private bool PowerOnline()
        {
            if (!UpdateGridPower()) return false;
            CalculatePowerCharge();

            if (!WarmedUp) return true;
            if (_isServer && _hadPowerBefore && _shieldConsumptionRate.Equals(0f) && DsState.State.Charge.Equals(0.01f) && _genericDownLoop == -1)
            {
                _genericDownLoop = 0;
                return false;
            }

            _power = _shieldMaxChargeRate > 0 ? _shieldConsumptionRate + _shieldMaintaintPower : 0f;
            if (_power < ShieldCurrentPower && (_power - _shieldMaxChargeRate) >= 0.0001f) //overpower
                _sink.Update();
            else if (_count == 28 && Math.Abs(_power - ShieldCurrentPower) >= 0.0001f)
                _sink.Update();

            if (Absorb > 0)
            {
                _damageReadOut += Absorb;
                EffectsCleanTick = _tick;
                DsState.State.Charge -= Absorb * ConvToWatts;
            }
            else if (Absorb < 0) DsState.State.Charge += Absorb * ConvToWatts;

            if (_isServer && DsState.State.Charge < 0)
            {
                DsState.State.Charge = 0;
                if (!_empOverLoad) _overLoadLoop = 0;
                else _empOverLoadLoop = 0;
            }
            Absorb = 0f;
            return true;
        }

        private bool UpdateGridPower()
        {
            GridAvailablePower = 0;
            GridMaxPower = 0;
            GridCurrentPower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentOutput = 0;
            _batteryCurrentInput = 0;
            lock (SubLock)
            {
                if (MyResourceDist != null && FuncTask.IsComplete && !_functionalEvent)
                {
                    var noObjects = MyResourceDist.SourcesEnabled == MyMultipleEnabledEnum.NoObjects;
                    if (noObjects)
                    {
                        if (Session.Enforced.Debug == 2) Log.Line($"NoObjects: {MyGrid?.DebugName} - Max:{MyResourceDist?.MaxAvailableResourceByType(GId)} - Status:{MyResourceDist?.SourcesEnabled} - Sources:{_powerSources.Count}");
                        FallBackPowerCalc();
                        FunctionalChanged(true);
                    }
                    else
                    {
                        GridMaxPower = MyResourceDist.MaxAvailableResourceByType(GId);
                        GridCurrentPower = MyResourceDist.TotalRequiredInputByType(GId);
                        if (!DsSet.Settings.UseBatteries && _batteryBlocks.Count != 0) CalculateBatteryInput();
                    }
                }
                else FallBackPowerCalc();
            }
            GridAvailablePower = GridMaxPower - GridCurrentPower;

            if (!DsSet.Settings.UseBatteries)
            {
                GridCurrentPower += _batteryCurrentInput;
                GridAvailablePower -= _batteryCurrentInput;
            }

            return GridMaxPower > 0;
        }

        private void FallBackPowerCalc(bool reportOnly = false)
        {
            var batteries = !DsSet.Settings.UseBatteries;
            if (reportOnly)
            {
                var gridMaxPowerReport = 0f;
                var gridCurrentPowerReport = 0f;
                var gridAvailablePowerReport = 0f;
                var batteryMaxPowerReport = 0f;
                var batteryCurrentPowerReport = 0f;
                var batteryCurrentInputreport = 0f;
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    var source = _powerSources[i];

                    var battery = source.Entity as IMyBatteryBlock;
                    if (battery != null && batteries)
                    {
                        //Log.Line($"bMaxO:{battery.MaxOutput} - bCurrO:{battery.CurrentOutput} - bCurrI:{battery.CurrentInput} - Charging:{battery.IsCharging}");
                        if (!battery.IsWorking) continue;
                        var currentInput = battery.CurrentInput;
                        var currentOutput = battery.CurrentOutput;
                        var maxOutput = battery.MaxOutput;
                        if (currentInput > 0)
                        {
                            batteryCurrentInputreport += currentInput;
                            if (battery.IsCharging) batteryCurrentPowerReport -= currentInput;
                            else batteryCurrentPowerReport -= currentInput;
                        }
                        batteryMaxPowerReport += maxOutput;
                        batteryCurrentPowerReport += currentOutput;
                    }
                    else
                    {
                        gridMaxPowerReport += source.MaxOutputByType(GId);
                        gridCurrentPowerReport += source.CurrentOutputByType(GId);
                    }
                }

                gridMaxPowerReport += batteryMaxPowerReport;
                gridCurrentPowerReport += batteryCurrentPowerReport;
                gridAvailablePowerReport = gridMaxPowerReport - gridCurrentPowerReport;

                if (!DsSet.Settings.UseBatteries)
                {
                    gridCurrentPowerReport += batteryCurrentInputreport;
                    gridAvailablePowerReport -= batteryCurrentInputreport;
                }

                Log.Line($"Report: PriGMax:{GridMaxPower}(BetaGMax:{gridMaxPowerReport}) - PriGCurr:{GridCurrentPower}(BetaGCurr:{gridCurrentPowerReport}) - PriGAvail:{GridMaxPower - GridCurrentPower}(BetaGAvail:{gridAvailablePowerReport}) - BatInput:{batteryCurrentInputreport} - SCurr:{ShieldCurrentPower}");
            }
            else
            {
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    var source = _powerSources[i];
                    var battery = source.Entity as IMyBatteryBlock;
                    if (battery != null && batteries)
                    {
                        //Log.Line($"bMaxO:{battery.MaxOutput} - bCurrO:{battery.CurrentOutput} - bCurrI:{battery.CurrentInput} - Charging:{battery.IsCharging}");
                        if (!battery.IsWorking) continue;
                        var currentInput = battery.CurrentInput;
                        var currentOutput = battery.CurrentOutput;
                        var maxOutput = battery.MaxOutput;
                        if (currentInput > 0)
                        {
                            _batteryCurrentInput += currentInput;
                            if (battery.IsCharging) _batteryCurrentOutput -= currentInput;
                            else _batteryCurrentOutput -= currentInput;
                        }
                        _batteryMaxPower += maxOutput;
                        _batteryCurrentOutput += currentOutput;
                    }
                    else
                    {
                        GridMaxPower += source.MaxOutputByType(GId);
                        GridCurrentPower += source.CurrentOutputByType(GId);
                    }
                }
                GridMaxPower += _batteryMaxPower;
                GridCurrentPower += _batteryCurrentOutput;
            }
        }

        private void CalculateBatteryInput()
        {
            for (int i = 0; i < _batteryBlocks.Count; i++)
            {
                var battery = _batteryBlocks[i];
                if (!battery.IsWorking) continue;
                var currentInput = battery.CurrentInput;
                var currentOutput = battery.CurrentOutput;
                var maxOutput = battery.MaxOutput;
                if (currentInput > 0)
                {
                    _batteryCurrentInput += currentInput;
                    if (battery.IsCharging) _batteryCurrentOutput -= currentInput;
                    else _batteryCurrentOutput -= currentInput;
                }
                _batteryMaxPower += maxOutput;
                _batteryCurrentOutput += currentOutput;
            }
        }

        private void CalculatePowerCharge()
        {
            var capScaler = Session.Enforced.CapScaler;
            var hpsEfficiency = Session.Enforced.HpsEfficiency;
            var baseScaler = Session.Enforced.BaseScaler;
            var maintenanceCost = Session.Enforced.MaintenanceCost;

            var percent = DsSet.Settings.Rate * ChargeRatio;

            var chargePercent = DsSet.Settings.Rate * ConvToDec;
            var shieldMaintainPercent = maintenanceCost / percent;
            _sizeScaler = _shieldVol / (_ellipsoidSurfaceArea * MagicRatio);

            float bufferScaler;
            if (ShieldMode == ShieldType.Station)
            {
                if (DsState.State.Enhancer) bufferScaler = 100 / percent * baseScaler * _shieldRatio;
                else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;
            }
            else if (_sizeScaler > 1 && DsSet.Settings.FortifyShield)
            {
                bufferScaler = 100 / percent * baseScaler * _shieldRatio;
            }
            else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;

            var hpBase = GridMaxPower * bufferScaler;

            var gridIntegrity = DsState.State.GridIntegrity * ConvToDec;
            if (capScaler > 0) gridIntegrity *= capScaler;

            if (hpBase > gridIntegrity) _hpScaler = gridIntegrity / hpBase;
            else _hpScaler = 1f;

            shieldMaintainPercent = shieldMaintainPercent * DsState.State.EnhancerPowerMulti * (DsState.State.ShieldPercent * ConvToDec);
            if (DsState.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            _shieldMaintaintPower = GridMaxPower * _hpScaler * shieldMaintainPercent;

            ShieldMaxCharge = hpBase * _hpScaler;
            var powerForShield = PowerNeeded(chargePercent, hpsEfficiency);
            if (!WarmedUp) return;

            if (DsState.State.Charge > ShieldMaxCharge) DsState.State.Charge = ShieldMaxCharge;
            if (_isServer)
            {
                var powerLost = powerForShield <= 0 || _powerNeeded > GridMaxPower || (GridMaxPower - _powerNeeded) / Math.Abs(_powerNeeded) * 100 < 0.001;
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
            else _expChargeReduction = 0;

            if (_count == 29 && DsState.State.Charge < ShieldMaxCharge) DsState.State.Charge += _shieldChargeRate;
            else if (DsState.State.Charge.Equals(ShieldMaxCharge))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }

            if (DsState.State.Charge < ShieldMaxCharge) DsState.State.ShieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
            else if (DsState.State.Charge < ShieldMaxCharge * 0.1) DsState.State.ShieldPercent = 0f;
            else DsState.State.ShieldPercent = 100f;
        }

        private float PowerNeeded(float chargePercent, float hpsEfficiency)
        {
            var powerScaler = 1f;
            if (_hpScaler < 0.5) powerScaler = _hpScaler + _hpScaler;

            var cleanPower = GridAvailablePower + ShieldCurrentPower;
            _otherPower = GridMaxPower - cleanPower;
            var powerForShield = ((cleanPower * chargePercent) - _shieldMaintaintPower) * powerScaler;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxChargeRate = rawMaxChargeRate;
            _shieldPeakRate = _shieldMaxChargeRate * hpsEfficiency / (float)_sizeScaler;
            if (DsState.State.Charge + _shieldPeakRate < ShieldMaxCharge)
            {
                _shieldChargeRate = _shieldPeakRate;
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                var remaining = ShieldMaxCharge - DsState.State.Charge;
                var remainingScaled = remaining / _shieldPeakRate;
                _shieldConsumptionRate = remainingScaled * _shieldMaxChargeRate;
                _shieldChargeRate = _shieldPeakRate * remainingScaled;
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
                    DsState.State.Charge = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return true;
                }
                if (!DsState.State.NoPower)
                {
                    DsState.State.NoPower = true;
                    DsState.State.Message = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: NoPower - forShield:{powerForShield} - rounded:{GridMaxPower} - max:{GridMaxPower} - avail{GridAvailablePower} - sCurr:{ShieldCurrentPower} - count:{_powerSources.Count} - DistEna:{MyResourceDist?.SourcesEnabled} - State:{MyResourceDist?.ResourceState} - ShieldId [{Shield.EntityId}]");
                    ShieldChangeState();
                }

                var shieldLoss = ShieldMaxCharge * 0.0016667f;
                DsState.State.Charge = DsState.State.Charge - shieldLoss;
                if (DsState.State.Charge < 0.01f) DsState.State.Charge = 0.01f;

                if (DsState.State.Charge < ShieldMaxCharge) DsState.State.ShieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
                else if (DsState.State.Charge < ShieldMaxCharge * 0.1) DsState.State.ShieldPercent = 0f;
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
                _expChargeReduction = ExpChargeReductions[heat];
                _shieldChargeRate = _shieldChargeRate / _expChargeReduction;
            }
        }

        private void HeatManager()
        {
            var hp = ShieldMaxCharge * ConvToHp;
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
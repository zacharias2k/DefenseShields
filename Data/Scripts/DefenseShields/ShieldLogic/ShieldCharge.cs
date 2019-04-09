using VRageMath;
using System;
using DefenseSystems.Support;

namespace DefenseSystems
{
    public partial class Controllers
    {
        #region Block Power Logic
        private bool PowerOnline()
        {
            if (!Bus.UpdateSpinePower()) return false;
            CalculatePowerCharge();

            if (!WarmedUp) return true;
            if (_isServer && _shieldConsumptionRate.Equals(0f) && DsState.State.Charge.Equals(0.01f))
            {
                return false;
            }

            _power = _shieldMaxChargeRate > 0 ? _shieldConsumptionRate + _shieldMaintaintPower : 0f;
            if (_power < ShieldCurrentPower && (_power - _shieldMaxChargeRate) >= 0.0001f) //overpower
                _sink.Update();
            else if (_count == 28 && (ShieldCurrentPower <= 0 || Math.Abs(_power - ShieldCurrentPower) >= 0.0001f))
                _sink.Update();

            if (Absorb > 0)
            {
                _damageReadOut += Absorb;
                Bus.EffectsCleanTick = _tick;
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

            ShieldHpBase = Bus.ShieldMaxPower * bufferScaler;

            var gridIntegrity = DsState.State.GridIntegrity * ConvToDec;
            if (capScaler > 0) gridIntegrity *= capScaler;

            if (ShieldHpBase > gridIntegrity) HpScaler = gridIntegrity / ShieldHpBase;
            else HpScaler = 1f;
            shieldMaintainPercent = shieldMaintainPercent * DsState.State.EnhancerPowerMulti * (DsState.State.ShieldPercent * ConvToDec);
            if (DsState.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            _shieldMaintaintPower = Bus.ShieldMaxPower * HpScaler * shieldMaintainPercent;

            ShieldMaxCharge = ShieldHpBase * HpScaler;
            var powerForShield = PowerNeeded(chargePercent, hpsEfficiency);
            if (!WarmedUp) return;

            if (DsState.State.Charge > ShieldMaxCharge) DsState.State.Charge = ShieldMaxCharge;
            if (_isServer)
            {
                var powerLost = powerForShield <= 0 || _powerNeeded > Bus.ShieldMaxPower || (Bus.ShieldMaxPower - _powerNeeded) / Math.Abs(_powerNeeded) * 100 < 0.001;
                var serverNoPower = DsState.State.NoPower;
                if (powerLost || serverNoPower)
                {
                    if (PowerLoss(powerForShield, powerLost, serverNoPower))
                    {
                        _powerFail = true;
                        return;
                    }
                }
                else
                {
                    if (_capacitorLoop != 0 && _tick - _capacitorTick > CapacitorStableCount)
                    {
                        _capacitorLoop = 0;
                    }
                    _powerFail = false;
                }
            }
            if (DsState.State.Heat != 0) UpdateHeatRate();
            else _expChargeReduction = 0;

            if (_count == 29 && DsState.State.Charge < ShieldMaxCharge) DsState.State.Charge += ShieldChargeRate;
            else if (DsState.State.Charge.Equals(ShieldMaxCharge))
            {
                ShieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }

            if (DsState.State.Charge < ShieldMaxCharge) DsState.State.ShieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
            else if (DsState.State.Charge < ShieldMaxCharge * 0.1) DsState.State.ShieldPercent = 0f;
            else DsState.State.ShieldPercent = 100f;
        }

        private float PowerNeeded(float chargePercent, float hpsEfficiency)
        {
            var powerScaler = 1f;
            if (HpScaler < 1) powerScaler = HpScaler;

            var cleanPower = Bus.ShieldAvailablePower + ShieldCurrentPower;
            _otherPower = Bus.ShieldMaxPower - cleanPower;
            var powerForShield = ((cleanPower * chargePercent) - _shieldMaintaintPower) * powerScaler;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            //if (ShieldMode == ShieldType.SmallGrid)Log.Line($"GridAvail:{GridAvailablePower} - Current:{ShieldCurrentPower} - Clean:{cleanPower} - Other:{_otherPower} - powerFor:{powerForShield} - rawCharge:{rawMaxChargeRate}");
            _shieldMaxChargeRate = rawMaxChargeRate;
            _shieldPeakRate = _shieldMaxChargeRate * hpsEfficiency / (float)_sizeScaler;

            if (DsState.State.Charge + _shieldPeakRate < ShieldMaxCharge)
            {
                ShieldChargeRate = _shieldPeakRate;
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                if (_shieldPeakRate > 0)
                {
                    var remaining = MathHelper.Clamp(ShieldMaxCharge - DsState.State.Charge, 0, ShieldMaxCharge);
                    var remainingScaled = remaining / _shieldPeakRate;
                    _shieldConsumptionRate = remainingScaled * _shieldMaxChargeRate;
                    ShieldChargeRate = _shieldPeakRate * remainingScaled;
                }
                else
                {
                    _shieldConsumptionRate = 0;
                    ShieldChargeRate = 0;
                }
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
                    ShieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return true;
                }

                _capacitorTick = _tick;
                _capacitorLoop++;
                if (_capacitorLoop > CapacitorDrainCount)
                {
                    if (Session.Enforced.Debug >= 3 && _tick60) Log.Line($"CapcitorDrained");
                    if (!DsState.State.NoPower)
                    {
                        DsState.State.NoPower = true;
                        DsState.State.Message = true;
                        ShieldChangeState();
                    }

                    var shieldLoss = ShieldMaxCharge * 0.0016667f;
                    DsState.State.Charge = DsState.State.Charge - shieldLoss;
                    if (DsState.State.Charge < 0.01f) DsState.State.Charge = 0.01f;

                    if (DsState.State.Charge < ShieldMaxCharge) DsState.State.ShieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
                    else if (DsState.State.Charge < ShieldMaxCharge * 0.1) DsState.State.ShieldPercent = 0f;
                    else DsState.State.ShieldPercent = 100f;

                    ShieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return true;
                }
            }

            if (serverNoPower)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    DsState.State.NoPower = false;
                    _powerNoticeLoop = 0;
                    if (Session.Enforced.Debug >= 3) Log.Line($"StateUpdate: PowerRestored - ShieldId [{Shield.EntityId}]");
                    ShieldChangeState();
                }
            }
            return false;
        }
        #endregion
    }
}
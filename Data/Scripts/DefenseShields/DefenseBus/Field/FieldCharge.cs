using System;
using DefenseSystems.Support;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        internal void UpdateCharge()
        {
            var a = Bus.ActiveController;
            var set = a.Set;
            var state = a.State;

            var capScaler = Session.Enforced.CapScaler;
            var hpsEfficiency = Session.Enforced.HpsEfficiency;
            var baseScaler = Session.Enforced.BaseScaler;
            var maintenanceCost = Session.Enforced.MaintenanceCost;
            var percent = set.Value.Rate * ChargeRatio;

            var chargePercent = set.Value.Rate * ConvToDec;
            var shieldMaintainPercent = maintenanceCost / percent;
            _sizeScaler = _shieldVol / (_ellipsoidSurfaceArea * MagicRatio);

            float bufferScaler;
            if (Bus.EmitterMode == Bus.EmitterModes.Station)
            {
                if (state.Value.Enhancer) bufferScaler = 100 / percent * baseScaler * _shieldRatio;
                else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;
            }
            else if (_sizeScaler > 1 && set.Value.FortifyShield)
            {
                bufferScaler = 100 / percent * baseScaler * _shieldRatio;
            }
            else bufferScaler = 100 / percent * baseScaler / (float)_sizeScaler * _shieldRatio;

            ShieldHpBase = FieldMaxPower * bufferScaler;

            var spineIntegrity = state.Value.SpineIntegrity * ConvToDec;
            if (capScaler > 0) spineIntegrity *= capScaler;

            if (ShieldHpBase > spineIntegrity) HpScaler = spineIntegrity / ShieldHpBase;
            else HpScaler = 1f;
            shieldMaintainPercent = shieldMaintainPercent * state.Value.EnhancerPowerMulti * (state.Value.ShieldPercent * ConvToDec);
            if (state.Value.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            ShieldMaintaintPower = FieldMaxPower * HpScaler * shieldMaintainPercent;

            ShieldMaxCharge = ShieldHpBase * HpScaler;
            var powerForShield = PowerNeeded(chargePercent, hpsEfficiency);
            if (!Bus.ActiveController.WarmedUp) return;

            if (state.Value.Charge > ShieldMaxCharge) state.Value.Charge = ShieldMaxCharge;
            if (_isServer)
            {
                var powerLost = powerForShield <= 0 || PowerNeeds > FieldMaxPower || (FieldMaxPower - PowerNeeds) / Math.Abs(PowerNeeds) * 100 < 0.001;
                var serverNoPower = state.Value.NoPower;
                if (powerLost || serverNoPower)
                {
                    if (PowerLoss(powerForShield, powerLost, serverNoPower))
                    {
                        PowerFail = true;
                        return;
                    }
                }
                else
                {
                    if (_capacitorLoop != 0 && Bus.Tick - _capacitorTick > CapacitorStableCount)
                    {
                        _capacitorLoop = 0;
                    }
                    PowerFail = false;
                }
            }
            if (state.Value.Heat != 0) UpdateHeatRate();
            else _expChargeReduction = 0;

            if (Bus.Count == 29 && state.Value.Charge < ShieldMaxCharge) state.Value.Charge += ShieldChargeRate;
            else if (state.Value.Charge.Equals(ShieldMaxCharge))
            {
                ShieldChargeRate = 0f;
                ShieldConsumptionRate = 0f;
            }

            if (state.Value.Charge < ShieldMaxCharge) state.Value.ShieldPercent = state.Value.Charge / ShieldMaxCharge * 100;
            else if (state.Value.Charge < ShieldMaxCharge * 0.1) state.Value.ShieldPercent = 0f;
            else state.Value.ShieldPercent = 100f;
        }

        private float PowerNeeded(float chargePercent, float hpsEfficiency)
        {
            var a = Bus.ActiveController;
            var state = a.State;

            var powerScaler = 1f;
            if (HpScaler < 1) powerScaler = HpScaler;

            var cleanPower = FieldAvailablePower + a.SinkCurrentPower;
            _otherPower = FieldMaxPower - cleanPower;
            var powerForShield = ((cleanPower * chargePercent) - ShieldMaintaintPower) * powerScaler;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            //if (ShieldMode == ShieldType.SmallGrid)Log.Line($"GridAvail:{GridAvailablePower} - Current:{ShieldCurrentPower} - Clean:{cleanPower} - Other:{_otherPower} - powerFor:{powerForShield} - rawCharge:{rawMaxChargeRate}");
            ShieldMaxChargeRate = rawMaxChargeRate;
            _shieldPeakRate = ShieldMaxChargeRate * hpsEfficiency / (float)_sizeScaler;

            if (state.Value.Charge + _shieldPeakRate < ShieldMaxCharge)
            {
                ShieldChargeRate = _shieldPeakRate;
                ShieldConsumptionRate = ShieldMaxChargeRate;
            }
            else
            {
                if (_shieldPeakRate > 0)
                {
                    var remaining = MathHelper.Clamp(ShieldMaxCharge - state.Value.Charge, 0, ShieldMaxCharge);
                    var remainingScaled = remaining / _shieldPeakRate;
                    ShieldConsumptionRate = remainingScaled * ShieldMaxChargeRate;
                    ShieldChargeRate = _shieldPeakRate * remainingScaled;
                }
                else
                {
                    ShieldConsumptionRate = 0;
                    ShieldChargeRate = 0;
                }
            }
            PowerNeeds = ShieldMaintaintPower + ShieldConsumptionRate + _otherPower;
            return powerForShield;
        }

        private bool PowerLoss(float powerForShield, bool powerLost, bool serverNoPower)
        {
            var a = Bus.ActiveController;
            var state = a.State;

            if (powerLost)
            {
                if (!state.Value.Online)
                {
                    state.Value.Charge = 0.01f;
                    ShieldChargeRate = 0f;
                    ShieldConsumptionRate = 0f;
                    return true;
                }

                _capacitorTick = Bus.Tick;
                _capacitorLoop++;
                if (_capacitorLoop > CapacitorDrainCount)
                {
                    if (Session.Enforced.Debug >= 3 && Bus.Tick60) Log.Line($"CapcitorDrained");
                    if (!state.Value.NoPower)
                    {
                        state.Value.NoPower = true;
                        state.Value.Message = true;
                        a.ProtChangedState();
                    }

                    var shieldLoss = ShieldMaxCharge * 0.0016667f;
                    state.Value.Charge = state.Value.Charge - shieldLoss;
                    if (state.Value.Charge < 0.01f) state.Value.Charge = 0.01f;

                    if (state.Value.Charge < ShieldMaxCharge) state.Value.ShieldPercent = state.Value.Charge / ShieldMaxCharge * 100;
                    else if (state.Value.Charge < ShieldMaxCharge * 0.1) state.Value.ShieldPercent = 0f;
                    else state.Value.ShieldPercent = 100f;

                    ShieldChargeRate = 0f;
                    ShieldConsumptionRate = 0f;
                    return true;
                }
            }

            if (serverNoPower)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    state.Value.NoPower = false;
                    _powerNoticeLoop = 0;
                    if (Session.Enforced.Debug >= 3) Log.Line($"StateUpdate: PowerRestored - ControllerId [{Bus.ActiveController.Controller.EntityId}]");
                    a.ProtChangedState();
                }
            }
            return false;
        }
    }
}

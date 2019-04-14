using DefenseSystems.Support;
using Sandbox.ModAPI;
using VRage;

namespace DefenseSystems
{
    internal partial class Bus
    {
        internal bool HasPower()
        {
            var a = ActiveController;
            var state = a.State;
            var set = a.Set;

            SpineAvailablePower = 0;
            SpineMaxPower = 0;
            SpineCurrentPower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentOutput = 0;
            _batteryCurrentInput = 0;
            lock (SubLock)
            {
                if (MyResourceDist != null && FuncTask.IsComplete && !FunctionalEvent)
                {
                    var noObjects = MyResourceDist.SourcesEnabled == MyMultipleEnabledEnum.NoObjects;
                    if (noObjects)
                    {
                        if (Session.Enforced.Debug >= 2) Log.Line($"NoObjects: {Spine?.DebugName} - Max:{MyResourceDist?.MaxAvailableResourceByType(GId)} - Status:{MyResourceDist?.SourcesEnabled} - Sources:{_powerSources.Count}");
                        FallBackPowerCalc();
                        FunctionalBlockChanged(true);
                    }
                    else
                    {
                        SpineMaxPower = MyResourceDist.MaxAvailableResourceByType(GId);
                        SpineCurrentPower = MyResourceDist.TotalRequiredInputByType(GId);
                        if (!set.Value.UseBatteries && _batteryBlocks.Count != 0) CalculateBatteryInput();
                    }
                }
                else FallBackPowerCalc();
            }
            SpineAvailablePower = SpineMaxPower - SpineCurrentPower;
            if (!set.Value.UseBatteries)
            {
                SpineCurrentPower += _batteryCurrentInput;
                SpineAvailablePower -= _batteryCurrentInput;
            }
            var reserveScaler = ReserveScaler[set.Value.PowerScale];
            var userPowerCap = set.Value.PowerWatts * reserveScaler;

            if (state.Value.ProtectMode != 2)
            {
                var fieldMax = SpineMaxPower > userPowerCap ? userPowerCap : SpineMaxPower;
                Field.FieldMaxPower = fieldMax;
                Field.FieldAvailablePower = Field.FieldMaxPower - SpineCurrentPower;
                if (Field.FieldMaxPower > 0)
                {
                    Field.UpdateCharge();
                    //if (!WarmedUp) return true;
                    var consume = Field.ShieldConsumptionRate;
                    var outOfPower = consume <= 0f && state.Value.Charge.Equals(0.01f);
                    var powerFault = _isServer && outOfPower;

                    if (powerFault)
                        return false;

                    Field.UpdateField();

                    return true;
                }
                return false;
            }
            Field.FieldMaxPower = 0;
            Field.FieldAvailablePower = 0;

            if (SpineAvailablePower > 0)
            {
                PowerForUse = 0.01f;
                if (!a.SinkCurrentPower.Equals(PowerForUse)) PowerUpdate = true;
                return true;
            }
            return false;
        }

        private void FallBackPowerCalc()
        {
            var batteries = !ActiveController.Set.Value.UseBatteries;
            for (int i = 0; i < _powerSources.Count; i++)
            {
                var source = _powerSources[i];
                var battery = source.Entity as IMyBatteryBlock;
                if (battery != null && batteries)
                {
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
                    SpineMaxPower += source.MaxOutputByType(GId);
                    SpineCurrentPower += source.CurrentOutputByType(GId);
                }
            }
            SpineMaxPower += _batteryMaxPower;
            SpineCurrentPower += _batteryCurrentOutput;
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
    }
}


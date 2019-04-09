using DefenseSystems.Support;
using Sandbox.ModAPI;
using VRage;

namespace DefenseSystems
{
    public partial class Bus
    {

        internal bool UpdateSpinePower()
        {
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
                        if (!ActiveController.DsSet.Settings.UseBatteries && _batteryBlocks.Count != 0) CalculateBatteryInput();
                    }
                }
                else FallBackPowerCalc();
            }
            SpineAvailablePower = SpineMaxPower - SpineCurrentPower;

            if (!ActiveController.DsSet.Settings.UseBatteries)
            {
                SpineCurrentPower += _batteryCurrentInput;
                SpineAvailablePower -= _batteryCurrentInput;
            }
            var reserveScaler = ReserveScaler[ActiveController.DsSet.Settings.PowerScale];
            var userPowerCap = ActiveController.DsSet.Settings.PowerWatts * reserveScaler;
            var shieldMax = SpineMaxPower > userPowerCap ? userPowerCap : SpineMaxPower;
            ShieldMaxPower = shieldMax;
            ShieldAvailablePower = ShieldMaxPower - SpineCurrentPower;
            return ShieldMaxPower > 0;
        }

        private void FallBackPowerCalc()
        {
            var batteries = !ActiveController.DsSet.Settings.UseBatteries;
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


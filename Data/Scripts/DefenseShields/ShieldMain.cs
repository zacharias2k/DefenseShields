using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRageMath;

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
                var isServer = Session.IsServer;
                var isDedicated = Session.DedicatedServer;
                if (!ShieldOn(isServer))
                {
                    if (WasOnline) ShieldOff(isServer);
                    return;
                }

                if (DsState.State.Online)
                {
                    if (ComingOnline) ComingOnlineSetup(isServer, isDedicated);
                    if (!isDedicated && _tick % 60 == 0) HudCheck();

                    if (isServer)
                    {
                        var createHeTiming = _count == 6 && (_lCount == 1 || _lCount == 6);
                        if (GridIsMobile && createHeTiming) CreateHalfExtents();
                        SyncThreadedEnts();
                        WebEntities();
                        var mpActive = Session.MpActive;
                        if (mpActive && _count == 29)
                        {
                            var newPercentColor = UtilsStatic.GetShieldColorFromFloat(ShieldComp.ShieldPercent);
                            if (newPercentColor != _oldPercentColor)
                            {
                                ShieldChangeState(false);
                                _oldPercentColor = newPercentColor;
                            }
                        }
                        else if (mpActive && _count == 29 && _lCount == 7) ShieldChangeState(false);
                    }
                    else WebEntitiesClient();
                }
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"PerfCon: Online: {DsState.State.Online} - Tick: {_tick} loop: {_lCount}-{_count}", 4);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void ShieldOff(bool isServer)
        {
            _power = 0.001f;
            Sink.Update();
            WasOnline = false;
            ShieldEnt.Render.Visible = false;
            ShieldEnt.PositionComp.SetPosition(Vector3D.Zero);
            if (!DsState.State.Lowered && !DsState.State.Sleeping)
            {
                ShieldComp.ShieldPercent = 0f;
                DsState.State.Buffer = 0f;
            }
            if (isServer) ShieldChangeState(false);
            else Shield.RefreshCustomInfo();
        }

        private void ComingOnlineSetup(bool server, bool dedicated)
        {
            if (!dedicated) ShellVisibility();
            ShieldEnt.Render.Visible = true;
            ComingOnline = false;
            WasOnline = true;
            WarmedUp = true;

            if (server)
            {
                SyncThreadedEnts(true);
                _offlineCnt = -1;
                ShieldChangeState(false);
            }
            else Shield.RefreshCustomInfo();
        }

        private void Timing(bool cleanUp)
        {
            var isServer = Session.IsServer;

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

            if (_count == 33)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    DsSet.SaveSettings();
                    ResetShape(false, false);
                    if (Session.Enforced.Debug == 1) Log.Line($"SettingsUpdated - server:{Session.IsServer} - ShieldId [{Shield.EntityId}]");
                }
                if (_blockEvent) BlockChanged(true);
            }
            else if (_count == 34)
            {
                if (ClientUiUpdate && !isServer)
                {
                    ClientUiUpdate = false;
                    DsSet.NetworkUpdate();
                }
            }

            if (isServer && (_shapeEvent || FitChanged)) CheckExtents(true);

            // damage counter hack - tempoary
            if (_damageReadOut > 0 && _damageCounter > _damageReadOut) _damageCounter = _damageReadOut;
            else if (_damageCounter < _damageReadOut) _damageCounter = _damageReadOut;
            else if (_damageCounter > 1) _damageCounter = _damageCounter * .9835f;
            else _damageCounter = 0f;
            //

            if (isServer) HeatManager();

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
            //if (_eCount % 2 == 0 && _lCount == 0 && _count == 0) _randomCount = _random.Next(0, 10);

            if (cleanUp)
            {
                if (_staleGrids.Count != 0) CleanUp(0);
                if (_lCount == 9 && _count == 58) CleanUp(1);
                if (_effectsCleanup && (_count == 1 || _count == 21 || _count == 41)) CleanUp(2);
                //if (_eCount % 2 == 0 && _lCount == _randomCount && _count == 15 && (Session.DedicatedServer || Session.IsServer)) DsSet.SaveSettings();

                if ((_lCount * 60 + _count + 1) % 150 == 0)
                {
                    CleanUp(3);
                    CleanUp(4);
                }
            }
        }

        private void BlockMonitor()
        {
            if (_blockChanged)
            {
                _blockEvent = true;
                _shapeEvent = true;
                if (_blockAdded) _shapeTick = _tick + 300;
                else _shapeTick = _tick + 1800;
            }
            if (_functionalChanged) _functionalEvent = true;

            _functionalAdded = false;
            _functionalRemoved = false;
            _functionalChanged = false;

            _blockChanged = false;
            _blockRemoved = false;
            _blockAdded = false;
        }

        private void BlockChanged(bool backGround)
        {
            if (_blockEvent)
            {
                var check = !DsState.State.Sleeping && !DsState.State.Suspended;
                if (Session.Enforced.Debug == 1) Log.Line($"BlockChanged: check:{check} + functional:{_functionalEvent} - Sleeping:{DsState.State.Sleeping} - Suspend:{DsState.State.Suspended} - ShieldId [{Shield.EntityId}]");
                if (!check) return;

                if (_functionalEvent)
                {
                    if (backGround) MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
                    else BackGroundChecks();
                    _functionalEvent = false;
                }
                _blockEvent = false;
            }
        }

        private void CheckExtents(bool backGround)
        {
            FitChanged = false;
            _shapeEvent = false;
            if (!Session.IsServer) return;
            if (GridIsMobile)
            {
                CreateHalfExtents();
                if (backGround) MyAPIGateway.Parallel.StartBackground(GetShapeAdjust);
                else GetShapeAdjust();
            }
        }

        private void BackGroundChecks()
        {
            lock (_powerSources) _powerSources.Clear();
            lock (_functionalBlocks) _functionalBlocks.Clear();
            lock (_batteryBlocks) _batteryBlocks.Clear();
            foreach (var grid in ShieldComp.GetLinkedGrids)
            {
                var mechanical = ShieldComp.GetSubGrids.Contains(grid);
                if (grid == null) continue;
                foreach (var block in grid.GetFatBlocks())
                {
                    if (MyGridDistributor == null)
                    {
                        var controller = block as MyShipController;
                        if (controller != null)
                        {
                            lock (_lockOnMe) MyGridDistributor = controller.GridResourceDistributor;
                        }
                    }
                    lock (_batteryBlocks)
                    {
                        var battery = block as IMyBatteryBlock;
                        if (battery != null && block.IsFunctional && mechanical) _batteryBlocks.Add(battery);
                    }
                    lock (_functionalBlocks) if (block.IsFunctional && mechanical) _functionalBlocks.Add(block);

                    var source = block.Components.Get<MyResourceSourceComponent>();

                    if (source == null) continue;
                    foreach (var type in source.ResourceTypes)
                    {
                        if (type != MyResourceDistributorComponent.ElectricityId) continue;
                        lock (_powerSources) _powerSources.Add(source);
                        break;
                    }
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"PowerCount: {_powerSources.Count.ToString()} - ShieldId [{Shield.EntityId}]");
        }
        #endregion

        #region Block Power Logic
        private void HeatManager()
        {
            var hp = _shieldMaxBuffer * Session.Enforced.Efficiency;
            var heat = DsState.State.Heat;
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

            var nextThreshold = hp * 0.01 * _currentHeatStep;
            var currentThreshold = hp * 0.01 * (_currentHeatStep - 1);

            if (_heatCycle == OverHeat)
            {
                var threshold = hp * 0.01;
                if (_accumulatedHeat > threshold)
                {
                    _currentHeatStep = 1;
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug == 1) Log.Line($"now overheating - stage:{_currentHeatStep} - heat:{_accumulatedHeat} - threshold:{threshold}");
                    _accumulatedHeat = 0;
                }
                else
                {
                    DsState.State.Heat = 0;
                    _currentHeatStep = 0;
                    _heatCycle = -1;
                    if (Session.Enforced.Debug == 1) Log.Line($"under-threshold - stage:{_currentHeatStep} - heat:{_accumulatedHeat} - threshold:{threshold}");
                    _accumulatedHeat = 0;
                }
            }
            else if (_currentHeatStep < HeatSteps && _heatCycle == _currentHeatStep * HeatingStep + OverHeat)
            {
                if (_accumulatedHeat > nextThreshold)
                {
                    _currentHeatStep++;
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug == 1) Log.Line($"increased to - stage ({_currentHeatStep}): heat:{_accumulatedHeat} - threshold:{nextThreshold}");
                    _accumulatedHeat = 0;
                }
                else if (_accumulatedHeat > currentThreshold)
                {
                    DsState.State.Heat = _currentHeatStep * 10;
                    if (Session.Enforced.Debug == 1) Log.Line($"heat unchanged - stage:{_currentHeatStep} - heat:{_accumulatedHeat} - threshold:{currentThreshold}");
                    _heatCycle = (_currentHeatStep - 1) * HeatingStep + OverHeat + 1;
                    _accumulatedHeat = 0;
                }
                else
                {
                    if (_currentHeatStep > 0) _currentHeatStep--;
                    if (_currentHeatStep == 0)
                    {
                        DsState.State.Heat = 0;
                        _currentHeatStep = 0;
                        _heatCycle = -1;
                        if (Session.Enforced.Debug == 1) Log.Line($"no longer overheating ({_currentHeatStep}): heat:{_accumulatedHeat} - threshold:{nextThreshold}");
                        _accumulatedHeat = 0;
                    }
                    else
                    {
                        DsState.State.Heat = _currentHeatStep * 10;
                        _heatCycle = (_currentHeatStep - 1) * HeatingStep + OverHeat + 1;
                        if (Session.Enforced.Debug == 1) Log.Line($"decreased to - stage ({_currentHeatStep}): heat:{_accumulatedHeat} - threshold:{nextThreshold}");
                        _accumulatedHeat = 0;
                    }
                }
            }
            else if (_heatCycle >= HeatSteps * HeatingStep + OverHeat && _accumulatedHeat > nextThreshold)
            {
                _heatVentingTick = _tick + CoolingStep;
                _accumulatedHeat = 0;
            }
            else if (_tick >= _heatVentingTick)
            {
                if (_currentHeatStep >= 10) _currentHeatStep--;
                if (Session.Enforced.Debug == 1) Log.Line($"left critical - stage ({_currentHeatStep}): heat:{_accumulatedHeat} - threshold: {nextThreshold}");
                DsState.State.Heat = _currentHeatStep * 10;
                _heatCycle = (_currentHeatStep - 1) * HeatingStep + OverHeat + 1;
                _heatVentingTick = uint.MaxValue;
            }

            if (!heat.Equals(DsState.State.Heat)) ShieldChangeState(false);
        }

        private bool PowerOnline()
        {
            var isServer = Session.IsServer;
            if (MyGridDistributor != null)
            {   
                if (!UpdateGridPower() && !isServer) return false;
            }
            else if (!UpdateGridPower(true) && !isServer) return false;
            //if (!UpdateGridPower() && !isServer) return false;

            CalculatePowerCharge();
            _power = _shieldConsumptionRate + _shieldMaintaintPower;
            if (isServer && WarmedUp && HadPowerBefore && _shieldConsumptionRate.Equals(0f) && DsState.State.Buffer.Equals(0.01f) && _genericDownLoop == -1)
            {
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
                DsState.State.Buffer -= (Absorb / Session.Enforced.Efficiency);
            }
            else if (WarmedUp && Absorb < 0) DsState.State.Buffer += (Absorb / Session.Enforced.Efficiency);

            if (isServer && WarmedUp && DsState.State.Buffer < 0)
            {
                _overLoadLoop = 0;
            }
            Absorb = 0f;
            return true;
        }

        private bool UpdateGridPower(bool fallBack = false)
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;
            _gridAvailablePower = 0;
            _batteryMaxPower = 0;
            _batteryCurrentPower = 0;
            var eId = MyResourceDistributorComponent.ElectricityId;
            if (fallBack)
            {
                lock (_powerSources)
                {
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
                }

                if (DsSet.Settings.UseBatteries)
                {
                    _gridMaxPower += _batteryMaxPower;
                    _gridCurrentPower += _batteryCurrentPower;
                }
            }
            else
            {
                _gridMaxPower += MyGridDistributor.MaxAvailableResourceByType(eId);
                _gridCurrentPower += MyGridDistributor.TotalRequiredInputByType(eId);
                if (!DsSet.Settings.UseBatteries)
                {
                    lock (_batteryBlocks)
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
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            _shieldCurrentPower = Sink.CurrentInputByType(GId);
            return _gridMaxPower > 0;
        }

        private void CalculatePowerCharge()
        {
            var isServer = Session.IsServer;

            var heat = DsState.State.Heat * 0.1;
            if (heat > 10) heat = 10;

            var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
            var rawNerf = nerf ? Session.Enforced.Nerf : 1f;
            var nerfer = rawNerf / _shieldRatio;
            var shieldVol = DetectMatrixOutside.Scale.Volume;
            var powerForShield = 0f;
            const float ratio = 1.25f;
            var percent = DsSet.Settings.Rate * ratio;
            var shieldMaintainPercent = 1 / percent;
            shieldMaintainPercent = shieldMaintainPercent * DsState.State.EnhancerPowerMulti * (ShieldComp.ShieldPercent * 0.01f);
            if (DsState.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.25f;
            _shieldMaintaintPower = _gridMaxPower * shieldMaintainPercent;
            var fPercent = percent / ratio / 100;
            _sizeScaler = shieldVol / _ellipsoidSurfaceArea / 2.40063050674088;

            float bufferScaler;
            if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) bufferScaler = 100 / percent * Session.Enforced.BaseScaler * nerfer;
            else bufferScaler = 100 / percent * Session.Enforced.BaseScaler / (float)_sizeScaler * nerfer;

            var cleanPower = _gridAvailablePower + _shieldCurrentPower;
            _otherPower = _gridMaxPower - cleanPower;
            powerForShield = (cleanPower * fPercent) - _shieldMaintaintPower;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxChargeRate = rawMaxChargeRate;
            _shieldMaxBuffer = _gridMaxPower * bufferScaler;
            if (_sizeScaler < 1)
            {
                if (DsState.State.Buffer + _shieldMaxChargeRate * nerfer < _shieldMaxBuffer)
                {
                    _shieldChargeRate = _shieldMaxChargeRate * nerfer;
                    _shieldConsumptionRate = _shieldMaxChargeRate;
                }
                else if (_shieldMaxBuffer - DsState.State.Buffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - DsState.State.Buffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
           
            else if (DsState.State.Buffer + _shieldMaxChargeRate / (_sizeScaler / nerfer) < _shieldMaxBuffer)
            {
                _shieldChargeRate = _shieldMaxChargeRate / ((float)_sizeScaler / nerfer);
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                if (_shieldMaxBuffer - DsState.State.Buffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - DsState.State.Buffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
            _powerNeeded = _shieldMaintaintPower + _shieldConsumptionRate + _otherPower;
            if (!WarmedUp) return;

            if (DsState.State.Buffer < _shieldMaxBuffer) ShieldComp.ShieldPercent = (DsState.State.Buffer / _shieldMaxBuffer) * 100;
            else if (DsState.State.Buffer <= 1) ShieldComp.ShieldPercent = 0f;
            else ShieldComp.ShieldPercent = 100f;

            if ( DsState.State.Buffer > _shieldMaxBuffer) DsState.State.Buffer = _shieldMaxBuffer;
            var roundedGridMax = Math.Round(_gridMaxPower, 1);
            if (_powerNeeded > roundedGridMax || powerForShield <= 0)
            {
                if (!DsState.State.Online)
                {
                    DsState.State.Buffer = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return;
                }
                _powerLossLoop++;
                if (isServer && !DsState.State.NoPower)
                {
                    DsState.State.NoPower = true;
                    ShieldChangeState(true);
                }

                var shieldLoss = DsState.State.Buffer * (_powerLossLoop * 0.00008333333f);
                DsState.State.Buffer = DsState.State.Buffer - shieldLoss;
                if (DsState.State.Buffer < 0.01f) DsState.State.Buffer = 0.01f;

                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
                return;
            }
            _powerLossLoop = 0;
            if (isServer && DsState.State.NoPower)
            {
                _powerNoticeLoop++;
                if (_powerNoticeLoop >= PowerNoticeCount)
                {
                    DsState.State.NoPower = false;
                    _powerNoticeLoop = 0;
                    ShieldChangeState(false);
                }
            }


            if (heat <= 0) {}
            else if (heat >= 10)
            {
                _shieldChargeRate = 0;
                if (Session.Enforced.Debug == 1 && _count == 0) Log.Line($"Cycle:{_heatCycle} - cRate:{_shieldChargeRate} - Reduction:Infinity - Heat:{heat} - HeatDamage:{_accumulatedHeat}");
            }
            else
            {
                var expChargeReduction = (float)Math.Pow(2, heat);
                _shieldChargeRate = _shieldChargeRate / expChargeReduction;
                if (Session.Enforced.Debug == 1 && _count == 0) Log.Line($"Cycle:{_heatCycle} - cRate:{_shieldChargeRate} - Reduction:{_shieldChargeRate / expChargeReduction} - Exp:{expChargeReduction} - Heat:{heat} - HeatDamage:{_accumulatedHeat}");
            }

            if (DsState.State.Buffer < _shieldMaxBuffer && _count == 29) DsState.State.Buffer += _shieldChargeRate;
            else if (DsState.State.Buffer.Equals(_shieldMaxBuffer))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }
        }
        #endregion

        #region Checks / Terminal
        private string GetShieldStatus()
        {
            if (!DsState.State.Online && (!Shield.IsWorking || !Shield.IsFunctional)) return "[Controller Failure]";
            if (!DsState.State.Online && DsState.State.NoPower) return "[Insufficient Power]";
            if (!DsState.State.Online && DsState.State.Overload) return "[Overloaded]";
            if (!DsState.State.ControllerGridAccess) return "[Invalid Owner]";
            if (DsState.State.Waking) return "[Coming Online]";
            if (DsState.State.Suspended || DsState.State.Mode == 4) return "[Controller Standby]";
            if (DsState.State.Sleeping) return "[Docked]";
            if (!DsState.State.EmitterWorking) return "[Emitter Failure]";
            if (!DsState.State.Online) return "[Shield Down]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !DsState.State.Online ? 0f : 100f;
                var heatPercent = (int)DsState.State.Heat;

                if (DsState.State.Buffer < _shieldMaxBuffer) shieldPercent = (DsState.State.Buffer / _shieldMaxBuffer) * 100;
                if (DsState.State.Heat < _shieldMaxBuffer) shieldPercent = (DsState.State.Buffer / _shieldMaxBuffer) * 100;
                if (_shieldChargeRate > 0)
                {
                    var toMax = _shieldMaxBuffer - DsState.State.Buffer;
                    var secs = toMax / _shieldChargeRate;
                    if (secs.Equals(1)) secToFull = 0;
                    else secToFull = (int)(secs);
                }

                var shieldPowerNeeds = _powerNeeded;
                var powerUsage = shieldPowerNeeds;
                var otherPower = _otherPower;
                var gridMaxPower = _gridMaxPower;
                if (!DsSet.Settings.UseBatteries)
                {
                    powerUsage = powerUsage + _batteryCurrentPower;
                    otherPower = _otherPower + _batteryCurrentPower;
                    gridMaxPower = gridMaxPower + _batteryMaxPower;
                }

                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]")
                {
                    stringBuilder.Append(status + " MaxHP: " + (_shieldMaxBuffer * Session.Enforced.Efficiency).ToString("N0") +
                                         "\n" +
                                         "\n[Shield HP__]: " + (DsState.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[HP Per Sec_]: " + (_shieldChargeRate * Session.Enforced.Efficiency).ToString("N0") +
                                         "\n[Damage In__]: " + _damageReadOut.ToString("N0") +
                                         "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                         "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                         "\n[Over Heated]: " + heatPercent.ToString("0") + "%" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Power Usage]: " + powerUsage.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                         "\n[Shield Power]: " + Sink.CurrentInputByType(GId).ToString("0.0") + " Mw");
                }
                else
                {
                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Other Power]: " + otherPower.ToString("0.0") + " Mw" +
                                         "\n[HP Stored]: " + (DsState.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Needed Power]: " + shieldPowerNeeds.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                         "\n[Emitter Detected]: " + DsState.State.EmitterWorking +
                                         "\n" +
                                         "\n[Grid Owns Controller]: " + DsState.State.IsOwner +
                                         "\n[In Grid's Faction]: " + DsState.State.InFaction);

                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }

        private void UpdateSubGrids(bool force = false)
        {
            var checkGroups = Shield.IsWorking && Shield.IsFunctional && DsState.State.Online;
            if (Session.Enforced.Debug == 1) Log.Line($"SubCheckGroups: check:{checkGroups} - SW:{Shield.IsWorking} - SF:{Shield.IsFunctional} - Offline:{DsState.State.Online} - ShieldId [{Shield.EntityId}]");
            if (!checkGroups && !force) return;
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Physical);
            if (gotGroups.Count == ShieldComp.GetLinkedGrids.Count) return;
            if (Session.Enforced.Debug == 1) Log.Line($"SubGroupCnt: subCountChanged:{ShieldComp.GetLinkedGrids.Count != gotGroups.Count} - old:{ShieldComp.GetLinkedGrids.Count} - new:{gotGroups.Count} - ShieldId [{Shield.EntityId}]");

            lock (ShieldComp.GetSubGrids) ShieldComp.GetSubGrids.Clear();
            lock (ShieldComp.GetLinkedGrids) ShieldComp.GetLinkedGrids.Clear();
            var c = 0;
            for (int i = 0; i < gotGroups.Count; i++)
            {
                var sub = gotGroups[i];
                if (sub == null) continue;

                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Mechanical)) lock (ShieldComp.GetSubGrids) ShieldComp.GetSubGrids.Add(sub as MyCubeGrid);
                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Physical)) lock (ShieldComp.GetLinkedGrids) ShieldComp.GetLinkedGrids.Add(sub as MyCubeGrid);
            }
            _blockChanged = true;
            _functionalChanged = true;
        }

        private void ShieldDoDamage(float damage, long entityId)
        {
            ImpactSize = damage;
            Shield.SlimBlock.DoDamage(damage, MPdamage, true, null, entityId);
        }
        #endregion

        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            var update = false;
            if (ShieldComp.Modulator != null)
            {
                if (!DsState.State.ModulateEnergy.Equals(ShieldComp.Modulator.ModState.State.ModulateEnergy * 0.01f) || !DsState.State.ModulateKinetic.Equals(ShieldComp.Modulator.ModState.State.ModulateKinetic * 0.01f)) update = true;
                DsState.State.ModulateEnergy = ShieldComp.Modulator.ModState.State.ModulateEnergy * 0.01f;
                DsState.State.ModulateKinetic = ShieldComp.Modulator.ModState.State.ModulateKinetic * 0.01f;
                if (update) ShieldChangeState(false);
            }
            else
            {
                if (!DsState.State.ModulateEnergy.Equals(1f) || !DsState.State.ModulateKinetic.Equals(1f)) update = true;
                DsState.State.ModulateEnergy = 1f;
                DsState.State.ModulateKinetic = 1f;
                if (update) ShieldChangeState(false);

            }
        }

        public void GetEnhancernInfo()
        {
            var update = false;
            if (ShieldComp.Enhancer != null && ShieldComp.Enhancer.EnhState.State.Online)
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(2) || !DsState.State.EnhancerProtMulti.Equals(1000) || !DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 2;
                DsState.State.EnhancerProtMulti = 1000;
                DsState.State.Enhancer = true;
                if (update) ShieldChangeState(false);
            }
            else
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(1) || !DsState.State.EnhancerProtMulti.Equals(1) || DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 1;
                DsState.State.EnhancerProtMulti = 1;
                DsState.State.Enhancer = false;
                if (update) ShieldChangeState(false);
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
                        MyCubeGrid grid;
                        while (_staleGrids.TryDequeue(out grid))
                        {
                            EntIntersectInfo gridRemoved;
                            WebEnts.TryRemove(grid, out gridRemoved);
                        }
                        break;
                    case 1:
                        EnemyShields.Clear();
                        _webEntsTmp.AddRange(WebEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1));
                        foreach (var webent in _webEntsTmp)
                        {
                            EntIntersectInfo gridRemoved;
                            WebEnts.TryRemove(webent.Key, out gridRemoved);
                        }
                        break;
                    case 2:
                        if (DsState.State.Online && !DsState.State.Lowered)
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
        #endregion
    }
}
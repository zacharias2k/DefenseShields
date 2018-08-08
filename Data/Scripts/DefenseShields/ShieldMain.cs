using System;
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

                if (!ShieldReady(Session.IsServer)) return;

                if (DsStatus.State.Online)
                {
                    if (ComingOnline) ComingOnlineSetup(Session.IsServer, Session.DedicatedServer);
                    if (!Session.DedicatedServer && _tick % 60 == 0) HudCheck();

                    if (Session.IsServer)
                    {
                        var createHeTiming = _count == 6 && (_lCount == 1 || _lCount == 6);
                        if (GridIsMobile && createHeTiming) CreateHalfExtents();
                        SyncThreadedEnts();
                        WebEntities();
                    }
                    else
                    {
                        SyncThreadedEnts(true);
                        WebEntities();
                    }
                    WarmedUp = true;
                }
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"PerfCon: Online: {DsStatus.State.Online} - Tick: {_tick} loop: {_lCount}-{_count}", 4);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void ComingOnlineSetup(bool server, bool dedicated)
        {
            ComingOnline = false;
            SyncThreadedEnts(true);

            if (server) _offlineCnt = -1;
            if (!dedicated) ShellVisibility();
        }

        private void ShieldChangeState()
        {
            if (Session.IsServer && Session.MpActive) DsStatus.NetworkUpdate();
            else UpdateState(DsStatus.State);
        }

        private bool ShieldReady(bool server)
        {
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            if (server)
            {
                if (!ControllerFunctional() || ShieldWaking())
                {
                    if (PrevShieldActive) ShieldChangeState();
                    return false;
                }

                var powerState = PowerOnline();

                if (_tick % 120 == 0)
                {
                    GetModulationInfo();
                    GetEnhancernInfo();
                }
                if (ShieldDown()) return false;
                SetShieldServerStatus(powerState);
                Timing(true);

                if (ComingOnline && (!GridOwnsController() || GridIsMobile && FieldShapeBlocked()))
                {
                    _genericDownLoop = 0;
                    ShieldDown();
                    return false;
                }

                if (ComingOnline) ShieldChangeState();
            }
            else
            {
                if (!AllInited)
                {
                    PostInit();
                    return false;
                }
                if (_blockChanged) BlockMonitor();
                if (!WarmUpSequence()) return false;

                if (!DsStatus.State.Online) return false;

                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();

                PowerOnline();
                SetShieldClientStatus();
                Timing(true);
            }
            return true;
        }

        private bool ControllerFunctional()
        {
            if (!AllInited)
            {
                PostInit();
                return false;
            }
            if (_blockChanged) BlockMonitor();

            if (Suspend() || !WarmUpSequence() || ShieldSleeping() || ShieldLowered()) return false;

            if (ShieldComp.EmitterEvent) EmitterEventDetected();

            if (!Shield.IsWorking || !Shield.IsFunctional || !ShieldComp.EmittersWorking)
            {
                _genericDownLoop = 0;
                return false;
            }

            ControlBlockWorking = Shield.IsWorking && Shield.IsFunctional;

            if (ControlBlockWorking)
            {
                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();
            }

            return ControlBlockWorking; 
        }

        private void SetShieldServerStatus(bool powerState)
        {
            DsSet.Settings.ShieldActive = ControlBlockWorking && powerState;

            if (!PrevShieldActive && DsSet.Settings.ShieldActive) ComingOnline = true;
            else if (ComingOnline && PrevShieldActive && DsSet.Settings.ShieldActive) ComingOnline = false;

            PrevShieldActive = DsSet.Settings.ShieldActive;
            DsStatus.State.Online = DsSet.Settings.ShieldActive;

            if (!GridIsMobile && (ComingOnline || ShieldComp.O2Updated))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsStatus.State.IncreaseO2ByFPercent);
                ShieldComp.O2Updated = false;
            }
        }

        private void SetShieldClientStatus()
        {
            if (!PrevShieldActive && DsStatus.State.Online) ComingOnline = true;
            else if (ComingOnline && PrevShieldActive && DsStatus.State.Online) ComingOnline = false;

            PrevShieldActive = DsStatus.State.Online;

            /*
            if (!GridIsMobile && (ComingOnline || !DsStatus.State.IncreaseO2ByFPercent.Equals(EllipsoidOxyProvider.O2Level)))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsStatus.State.IncreaseO2ByFPercent);
            }
            */
        }

        private bool ShieldDown()
        {
            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1)
            {
                FailureConditions();
                return true;
            }
            return false;
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

            if ((_lCount == 1 || _lCount == 6) && _count == 1 && _blockEvent) BlockChanged(true);
            if (Session.IsServer && (_shapeEvent || FitChanged)) CheckExtents(true);

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
                var check = !DsStatus.State.Sleeping && !DsStatus.State.Suspended;
                if (Session.Enforced.Debug == 1) Log.Line($"BlockChanged: check:{check} + functional:{_functionalEvent} - Sleeping:{DsStatus.State.Sleeping} - Suspend:{DsStatus.State.Suspended} - ShieldId [{Shield.EntityId}]");
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

        public void UpdateBlockCount()
        {
            var blockCnt = 0;
            foreach (var subGrid in ShieldComp.GetSubGrids)
            {
                if (subGrid == null) continue;
                blockCnt += ((MyCubeGrid)subGrid).BlocksCount;
            }

            if (!_blockEvent) _blockEvent = blockCnt != _oldBlockCount;
            _oldBlockCount = blockCnt;
        }

        private void CheckExtents(bool backGround)
        {
            FitChanged = false;
            _shapeEvent = false;
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
                        break;
                    }
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"PowerCount: {_powerSources.Count.ToString()} - ShieldId [{Shield.EntityId}]");
        }
        #endregion

        #region Block Power Logic
        private bool PowerOnline()
        {
            UpdateGridPower();
            CalculatePowerCharge();
            _power = _shieldConsumptionRate + _shieldMaintaintPower;
            if (WarmedUp && HadPowerBefore && _shieldConsumptionRate.Equals(0f) && DsStatus.State.Buffer.Equals(0.01f) && _genericDownLoop == -1)
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
                DsStatus.State.Buffer -= (Absorb / Session.Enforced.Efficiency);
            }
            else if (WarmedUp && Absorb < 0) DsStatus.State.Buffer += (Absorb / Session.Enforced.Efficiency);

            if (WarmedUp && DsStatus.State.Buffer < 0)
            {
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

            if (DsSet.Settings.UseBatteries)
            {
                _gridMaxPower += _batteryMaxPower;
                _gridCurrentPower += _batteryCurrentPower;
            }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            _shieldCurrentPower = Sink.CurrentInputByType(GId);
        }

        private void CalculatePowerCharge()
        {
            var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
            var rawNerf = nerf ? Session.Enforced.Nerf : 1f;
            var nerfer = rawNerf / _shieldRatio;
            var shieldVol = DetectMatrixOutside.Scale.Volume;
            var powerForShield = 0f;
            const float ratio = 1.25f;
            var percent = DsSet.Settings.Rate * ratio;
            var shieldMaintainPercent = 1 / percent;
            shieldMaintainPercent = (shieldMaintainPercent * DsStatus.State.EnhancerPowerMulti) * (ShieldComp.ShieldPercent * 0.01f);
            if (DsStatus.State.Lowered) shieldMaintainPercent = shieldMaintainPercent * 0.5f;
            _shieldMaintaintPower = _gridMaxPower * shieldMaintainPercent;
            var fPercent = (percent / ratio) / 100;
            _sizeScaler = (shieldVol / _ellipsoidSurfaceArea) / 2.40063050674088;

            float bufferScaler;
            if (ShieldMode == ShieldType.Station && DsStatus.State.Enhancer) bufferScaler = 100 / percent * Session.Enforced.BaseScaler * nerfer;
            else bufferScaler = 100 / percent * Session.Enforced.BaseScaler / (float)_sizeScaler * nerfer;

            var cleanPower = _gridAvailablePower + _shieldCurrentPower;
            _otherPower = _gridMaxPower - cleanPower;
            powerForShield = (cleanPower * fPercent) - _shieldMaintaintPower;
            var rawMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxChargeRate = rawMaxChargeRate;
            _shieldMaxBuffer = _gridMaxPower * bufferScaler;
            if (_sizeScaler < 1)
            {
                if (DsStatus.State.Buffer + _shieldMaxChargeRate * nerfer < _shieldMaxBuffer)
                {
                    _shieldChargeRate = _shieldMaxChargeRate * nerfer;
                    _shieldConsumptionRate = _shieldMaxChargeRate;
                }
                else if (_shieldMaxBuffer - DsStatus.State.Buffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - DsStatus.State.Buffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
           
            else if (DsStatus.State.Buffer + _shieldMaxChargeRate / (_sizeScaler / nerfer) < _shieldMaxBuffer)
            {
                _shieldChargeRate = _shieldMaxChargeRate / ((float)_sizeScaler / nerfer);
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                if (_shieldMaxBuffer - DsStatus.State.Buffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - DsStatus.State.Buffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldConsumptionRate = 0f;
            }
            _powerNeeded = _shieldMaintaintPower + _shieldConsumptionRate + _otherPower;
            if (WarmedUp && _count != -2)
            {
                if (DsStatus.State.Buffer < _shieldMaxBuffer) ShieldComp.ShieldPercent = (DsStatus.State.Buffer / _shieldMaxBuffer) * 100;
                else if (DsStatus.State.Buffer <= 1) ShieldComp.ShieldPercent = 0f;
                else ShieldComp.ShieldPercent = 100f;
            }

            if (WarmedUp && (DsStatus.State.Buffer > _shieldMaxBuffer)) DsStatus.State.Buffer = _shieldMaxBuffer;
            var roundedGridMax = Math.Round(_gridMaxPower, 1);
            if (WarmedUp && (_powerNeeded > roundedGridMax || powerForShield <= 0))
            {
                if (DsStatus.State.Online)
                {
                    DsStatus.State.Buffer = 0.01f;
                    _shieldChargeRate = 0f;
                    _shieldConsumptionRate = 0f;
                    return;
                }
                _powerLossLoop++;
                if (!ShieldPowerLoss)
                {
                    ShieldPowerLoss = true;
                    if (!Session.DedicatedServer) PlayerMessages(PlayerNotice.NoPower);
                }

                var shieldLoss = DsStatus.State.Buffer * (_powerLossLoop * 0.00008333333f);
                DsStatus.State.Buffer = DsStatus.State.Buffer - shieldLoss;
                if (DsStatus.State.Buffer < 0.01f) DsStatus.State.Buffer = 0.01f;

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

            if (WarmedUp && (DsStatus.State.Buffer < _shieldMaxBuffer && _count == 29)) DsStatus.State.Buffer += _shieldChargeRate;
            else if (WarmedUp && (DsStatus.State.Buffer.Equals(_shieldMaxBuffer)))
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
            }
        }
        #endregion

        #region Checks / Terminal
        private string GetShieldStatus()
        {
            if (!Shield.IsWorking || !Shield.IsFunctional) return "[Controller Failure]";
            if (!DsStatus.State.Online && _power.Equals(0.0001f)) return "[Insufficient Power]";
            if (!DsStatus.State.Online && !_overLoadLoop.Equals(-1)) return "[Overloaded]";
            if (!DsStatus.State.ControllerGridAccess) return "[Invalid Owner]";
            if (DsStatus.State.Suspended || ShieldMode == ShieldType.Unknown) return "[Controller Standby]";
            if (DsStatus.State.Sleeping) return "[Docked]";
            if (ShieldComp.EmitterMode < 0 || !ShieldComp.EmittersWorking) return "[Emitter Failure]";
            if (UnsuspendTick != uint.MinValue) return "[Controller Starting]";
            if (!DsStatus.State.Online) return "[Shield Down]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !DsStatus.State.Online ? 0f : 100f;
                if (DsStatus.State.Buffer < _shieldMaxBuffer) shieldPercent = (DsStatus.State.Buffer / _shieldMaxBuffer) * 100;
                if (_shieldChargeRate > 0)
                {
                    var toMax = _shieldMaxBuffer - DsStatus.State.Buffer;
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
                                         "\n[Shield HP__]: " + (DsStatus.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
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
                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Other Power]: " + otherPower.ToString("0.0") + " Mw" +
                                         "\n[HP Stored]: " + (DsStatus.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Needed Power]: " + shieldPowerNeeds.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                         "\n[Emitter Detected]: " + ShieldComp.EmittersWorking +
                                         "\n" +
                                         "\n[Grid Owns Controller]: " + DsStatus.State.IsOwner +
                                         "\n[In Grid's Faction]: " + DsStatus.State.InFaction);

                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
        #endregion

        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            Shield.CubeGrid.Components.TryGet(out ModComp);
            if (ModComp != null)
            {
                var energyDamage = ModComp.KineticProtection * 0.01f;
                var kineticDamage = ModComp.EnergyProtection * 0.01f;
                DsStatus.State.ModulateEnergy = energyDamage;
                DsStatus.State.ModulateKinetic = kineticDamage;
            }
            else
            {
                if (Session.Enforced.Debug == 1 && (!DsStatus.State.ModulateEnergy.Equals(1f) || !DsStatus.State.ModulateKinetic.Equals(1f))) Log.Line($"Remodulate: no modComp found, value not default (1f): Energy:{DsStatus.State.ModulateEnergy} - Kinetic:{DsStatus.State.ModulateKinetic} - ShieldId [{Shield.EntityId}]");

                DsStatus.State.ModulateEnergy = 1f;
                DsStatus.State.ModulateKinetic = 1f;
            }
        }

        public void GetEnhancernInfo()
        {
            if (ShieldComp.Enhancer != null && ShieldComp.Enhancer.Online)
            {
                DsStatus.State.EnhancerPowerMulti = 2;
                DsStatus.State.EnhancerProtMulti = 1000;
                DsStatus.State.Enhancer = true;
            }
            else
            {
                DsStatus.State.EnhancerPowerMulti = 1;
                DsStatus.State.EnhancerProtMulti = 1;
                DsStatus.State.Enhancer = false;
            }
        }

        private void ShieldDoDamage(float damage, long entityId)
        {
            ImpactSize = damage;
            ((IMySlimBlock)((MyCubeBlock)Shield).SlimBlock).DoDamage(damage, MPdamage, true, null, entityId);
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
                        if (DsStatus.State.Online && !DsStatus.State.Lowered)
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

        public override void OnAddedToScene()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"OnAddedToScene: - {ShieldMode} - ShieldId [{Shield.EntityId}]");
                if (!AllInited) return;
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
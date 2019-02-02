namespace DefenseShields
{
    using System.Linq;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using VRage;
    using VRage.Game.ModAPI;

    public partial class DefenseShields
    {
        public void ResetDamageEffects()
        {
            if (DsState.State.Online && !DsState.State.Lowered)
            {
                lock (GetCubesLock)
                {
                    foreach (var funcBlock in _functionalBlocks)
                    {
                        if (funcBlock == null) continue;
                        if (funcBlock.IsFunctional) funcBlock.SetDamageEffect(false);
                    }
                }
            }
        }

        public void CleanWebEnts()
        {
            AuthenticatedCache.Clear();
            EnemyShields.Clear();
            IgnoreCache.Clear();

            _porotectEntsTmp.Clear();
            _porotectEntsTmp.AddRange(ProtectedEntCache.Where(info => _tick - info.Value.LastTick > 180));
            foreach (var protectedEnt in _porotectEntsTmp) ProtectedEntCache.Remove(protectedEnt.Key);

            _webEntsTmp.Clear();
            _webEntsTmp.AddRange(WebEnts.Where(info => _tick - info.Value.LastTick > 180));
            foreach (var webent in _webEntsTmp)
            {
                EntIntersectInfo removedEnt;
                WebEnts.TryRemove(webent.Key, out removedEnt);
            }
        }

        private void Timing()
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

            if (_isServer)
            {
                if (_shapeEvent || FitChanged) CheckExtents();
                if (_adjustShape) AdjustShape(true);
                HeatManager();
            }

            if (_count == 29)
            {
                if (!_isDedicated)
                {
                    Shield.RefreshCustomInfo();
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel && Session.Instance.LastTerminalId == Shield.EntityId)
                        MyCube.UpdateTerminal();
                }
                _runningDamage = _dpsAvg.Add((int)_damageReadOut);
                _runningHeal = _hpsAvg.Add((int)(_shieldChargeRate * ConvToHp));
                _damageReadOut = 0;
            }

        }

        private void UpdateSettings()
        {
            if (_tick % 33 == 0)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    DsSet.SaveSettings();
                    ResetShape(false);
                    if (Session.Enforced.Debug == 3) Log.Line($"SettingsUpdated: server:{_isServer} - ShieldId [{Shield.EntityId}]");
                }
            }
            else if (_tick % 34 == 0)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    if (!_isServer) DsSet.NetworkUpdate();
                }
            }
        }

        private void BlockMonitor()
        {
            if (_blockChanged)
            {
                _blockEvent = true;
                _shapeEvent = true;
                LosCheckTick = _tick + 1800;
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
                var notReady = !FuncTask.IsComplete || DsState.State.Sleeping || DsState.State.Suspended;
                if (notReady) return;
                if (Session.Enforced.Debug == 3) Log.Line($"BlockChanged: functional:{_functionalEvent} - funcComplete:{FuncTask.IsComplete} - Sleeping:{DsState.State.Sleeping} - Suspend:{DsState.State.Suspended} - ShieldId [{Shield.EntityId}]");
                if (_functionalEvent) FunctionalChanged(backGround);
                _blockEvent = false;
                _funcTick = _tick + 60;
            }
        }

        private void FunctionalChanged(bool backGround)
        {
            if (backGround) FuncTask = MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
            else BackGroundChecks();
            _functionalEvent = false;
        }

        private void BackGroundChecks()
        {
            var gridDistNeedUpdate = _updateGridDistributor || MyGridDistributor?.SourcesEnabled == MyMultipleEnabledEnum.NoObjects;
            _updateGridDistributor = false;
            lock (GetCubesLock)
            {
                _powerSources.Clear();
                _functionalBlocks.Clear();
                _batteryBlocks.Clear();

                foreach (var grid in ShieldComp.GetLinkedGrids)
                {
                    var mechanical = ShieldComp.GetSubGrids.Contains(grid);
                    if (grid == null) continue;
                    foreach (var block in grid.GetFatBlocks())
                    {
                        if (mechanical)
                        {
                            if (gridDistNeedUpdate)
                            {
                                var controller = block as MyShipController;
                                if (controller != null)
                                {
                                    var distributor = controller.GridResourceDistributor;
                                    if (distributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects)
                                    {
                                        if (Session.Enforced.Debug == 3) Log.Line($"Found MyGridDistributor from type:{block.BlockDefinition} - ShieldId [{Shield.EntityId}]");
                                        MyGridDistributor = controller.GridResourceDistributor;
                                        gridDistNeedUpdate = false;
                                    }
                                }
                            }

                            _functionalBlocks.Add(block);

                            var battery = block as IMyBatteryBlock;
                            if (battery != null) _batteryBlocks.Add(battery);
                        }

                        var source = block.Components.Get<MyResourceSourceComponent>();
                        if (source == null) continue;

                        foreach (var type in source.ResourceTypes)
                        {
                            if (type != MyResourceDistributorComponent.ElectricityId) continue;
                            _powerSources.Add(source);
                            break;
                        }
                    }
                }
            }
        }

        #region Checks
        private void HierarchyUpdate()
        {
            var checkGroups = MyCube.IsWorking && MyCube.IsFunctional && (DsState.State.Online || DsState.State.NoPower || DsState.State.Sleeping || DsState.State.Waking);
            if (Session.Enforced.Debug == 3) Log.Line($"SubCheckGroups: check:{checkGroups} - SW:{Shield.IsWorking} - SF:{Shield.IsFunctional} - Online:{DsState.State.Online} - Power:{!DsState.State.NoPower} - Sleep:{DsState.State.Sleeping} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
            if (checkGroups)
            {
                _subTick = _tick + 10;
                UpdateSubGrids();
                if (Session.Enforced.Debug == 3) Log.Line($"HierarchyWasDelayed: this:{_tick} - delayedTick: {_subTick} - ShieldId [{Shield.EntityId}]");
            }
        }

        private void UpdateSubGrids()
        {
            _subUpdate = false;

            var gotGroups = MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Physical);
            if (gotGroups.Count == ShieldComp.GetLinkedGrids.Count) return;
            if (Session.Enforced.Debug == 3 && ShieldComp.GetLinkedGrids.Count != 0) Log.Line($"SubGroupCnt: subCountChanged:{ShieldComp.GetLinkedGrids.Count != gotGroups.Count} - old:{ShieldComp.GetLinkedGrids.Count} - new:{gotGroups.Count} - ShieldId [{Shield.EntityId}]");

            lock (GetCubesLock)
            {
                ShieldComp.GetSubGrids.Clear();
                ShieldComp.GetLinkedGrids.Clear();
                for (int i = 0; i < gotGroups.Count; i++)
                {
                    var sub = gotGroups[i];
                    if (sub == null) continue;
                    if (MyAPIGateway.GridGroups.HasConnection(MyGrid, sub, GridLinkTypeEnum.Mechanical)) ShieldComp.GetSubGrids.Add(sub as MyCubeGrid);
                    ShieldComp.GetLinkedGrids.Add(sub as MyCubeGrid);
                }
            }
            _blockChanged = true;
            _functionalChanged = true;
            _updateGridDistributor = true;
        }
        #endregion

        private void CleanAll()
        {
            CleanWebEnts();
            ResetDamageEffects();
            SyncThreadedEnts(true);
        }
    }
}
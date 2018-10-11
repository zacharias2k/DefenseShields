using System;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRage;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Main
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

            if (_count == 33)
            {
                if (SettingsUpdated)
                {
                    SettingsUpdated = false;
                    DsSet.SaveSettings();
                    ResetShape(false, false);
                    if (Session.Enforced.Debug >= 1) Log.Line($"SettingsUpdated: server:{Session.IsServer} - ShieldId [{Shield.EntityId}]");
                }
            }
            else if (_count == 34)
            {
                if (ClientUiUpdate && !_isServer)
                {
                    ClientUiUpdate = false;
                    DsSet.NetworkUpdate();
                }
            }

            if (_isServer && (_shapeEvent || FitChanged)) CheckExtents(true);

            if (_isServer) HeatManager();

            if (_count == 29)
            {
                Shield.RefreshCustomInfo();
                if (!_isDedicated)
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                    {
                        Shield.ShowInToolbarConfig = false;
                        Shield.ShowInToolbarConfig = true;
                    }
                }
                _runningDamage = DpsAvg.Add((int) _damageReadOut);
                _damageReadOut = 0;
            }

            if (cleanUp)
            {
                if (_staleGrids.Count != 0) CleanUp(0);
                if (_lCount == 9 && _count == 58) CleanUp(1);
                if (_effectsCleanup && (_count == 1 || _count == 21 || _count == 41)) CleanUp(2);

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
                if (Session.Enforced.Debug >= 2) Log.Line($"BlockChanged: functional:{_functionalEvent} - funcComplete:{FuncTask.IsComplete} - Sleeping:{DsState.State.Sleeping} - Suspend:{DsState.State.Suspended} - ShieldId [{Shield.EntityId}]");
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

        private void CheckExtents(bool backGround)
        {
            FitChanged = false;
            _shapeEvent = false;
            if (!_isServer) return;
            if (GridIsMobile)
            {
                CreateHalfExtents();
                if (backGround) MyAPIGateway.Parallel.StartBackground(GetShapeAdjust);
                else GetShapeAdjust();
            }
        }

        private void BackGroundChecks()
        {
            var gridDistNeedUpdate = _updateGridDistributor || MyGridDistributor?.SourcesEnabled == MyMultipleEnabledEnum.NoObjects;
            _updateGridDistributor = false;
             
            lock (SubLock)
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
                        if (mechanical && gridDistNeedUpdate)
                        {
                            var controller = block as MyShipController;
                            if (controller != null)
                            {
                                var distributor = controller.GridResourceDistributor;
                                if (distributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects)
                                {
                                    if (Session.Enforced.Debug >= 1) Log.Line($"Found MyGridDistributor: ShieldId [{Shield.EntityId}]");
                                    MyGridDistributor = controller.GridResourceDistributor;
                                    gridDistNeedUpdate = false;
                                }
                            }
                        }
                        var battery = block as IMyBatteryBlock;
                        if (battery != null && block.IsFunctional && mechanical) _batteryBlocks.Add(battery);
                        if (block.IsFunctional && mechanical) _functionalBlocks.Add(block);

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
        #endregion

        #region Checks
        private void HierarchyUpdate()
        {
            var checkGroups = Shield.IsWorking && Shield.IsFunctional && (DsState.State.Online || DsState.State.NoPower || DsState.State.Sleeping || DsState.State.Waking);
            if (Session.Enforced.Debug >= 2) Log.Line($"SubCheckGroups: check:{checkGroups} - SW:{Shield.IsWorking} - SF:{Shield.IsFunctional} - Online:{DsState.State.Online} - Power:{!DsState.State.NoPower} - Sleep:{DsState.State.Sleeping} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
            if (checkGroups)
            {
                _subTick = _tick + 10;
                UpdateSubGrids();
                if (Session.Enforced.Debug >= 2) Log.Line($"HierarchyWasDelayed: this:{_tick} - delayedTick: {_subTick} - ShieldId [{Shield.EntityId}]");
            }
        }

        private void UpdateSubGrids()
        {
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Physical);
            if (gotGroups.Count == ShieldComp.GetLinkedGrids.Count) return;
            if (Session.Enforced.Debug >= 1) Log.Line($"SubGroupCnt: subCountChanged:{ShieldComp.GetLinkedGrids.Count != gotGroups.Count} - old:{ShieldComp.GetLinkedGrids.Count} - new:{gotGroups.Count} - ShieldId [{Shield.EntityId}]");

            lock (SubLock)
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
            _subUpdate = false;
        }
        #endregion

        #region Cleanup
        private void CleanAll()
        {
            CleanUp(0);
            CleanUp(1);
            CleanUp(2);
            CleanUp(3);
            CleanUp(4);
            CleanUp(5);
            SyncThreadedEnts(true);
        }

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
                            lock (SubLock)
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
                    case 5:
                    {
                        WebEnts.Clear();
                    }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CleanUp: {ex}"); }
        }
        #endregion
    }
}
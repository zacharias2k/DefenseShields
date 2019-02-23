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
                lock (DirtyCubeBlocks)
                {
                    var remove = false;
                    foreach (var keyPair in DirtyCubeBlocks)
                    {
                        var dirtyBlock = keyPair.Key;
                        var lastHitTick = keyPair.Value;
                        if (_tick >= lastHitTick + 10)
                        {
                            dirtyBlock.SetDamageEffect(false);
                            DirtyCubeBlocks.Remove(dirtyBlock);
                            remove = true;
                        }
                    }
                    if (remove) DirtyCubeBlocks.ApplyRemovals();
                    if (DirtyCubeBlocks.Keys.Count == 0) EffectsDirty = false;
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
                _shapeEvent = true;
                LosCheckTick = _tick + 1800;
                if (_blockAdded) _shapeTick = _tick + 300;
                else _shapeTick = _tick + 1800;
            }
            _blockChanged = false;
            _blockAdded = false;
        }

        #region Checks
        private void HierarchyUpdate()
        {
            var checkGroups = IsWorking && IsFunctional && (DsState.State.Online || DsState.State.NoPower || DsState.State.Sleeping || DsState.State.Waking);
            if (checkGroups)
            {
                _subTick = uint.MinValue;
                _subUpdate = false;
                UpdateSubGrids();
            }
        }

        private void UpdateSubGrids(bool force = false)
        {
            if (Session.Enforced.Debug == 2) Log.Line($"UpdateSubGrids: Su:{DsState.State.Suspended}({WasSuspended}) - SW:{Shield.IsWorking} - SF:{Shield.IsFunctional} - Online:{DsState.State.Online} - Power:{!DsState.State.NoPower} - Sleep:{DsState.State.Sleeping} - Wake:{DsState.State.Waking} - ShieldId [{Shield.EntityId}]");
            var newLinkGrop = MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Physical);
            var newLinkGropCnt = newLinkGrop.Count;
            lock (SubUpdateLock)
            {
                ShieldComp.RemSubs.Clear();
                foreach (var sub in ShieldComp.LinkedGrids.Keys) ShieldComp.RemSubs.Add(sub);
            }

            lock (SubLock)
            {
                if (newLinkGropCnt == ShieldComp.LinkedGrids.Count && !force) return;
                ShieldComp.SubGrids.Clear();
                ShieldComp.LinkedGrids.Clear();
                for (int i = 0; i < newLinkGropCnt; i++)
                {
                    var sub = (MyCubeGrid)newLinkGrop[i];
                    var mechSub = false;
                    if (MyAPIGateway.GridGroups.HasConnection(MyGrid, sub, GridLinkTypeEnum.Mechanical))
                    {
                        mechSub = true;
                        ShieldComp.SubGrids.Add(sub);
                    }
                    ShieldComp.LinkedGrids[sub] = new SubGridInfo(sub, sub == MyGrid, mechSub);
                }
            }

            lock (SubUpdateLock)
            {
                ShieldComp.AddSubs.Clear();
                foreach (var sub in ShieldComp.LinkedGrids.Keys)
                {
                    ShieldComp.AddSubs.Add(sub);
                    ShieldComp.NewTmp1.Add(sub);
                }

                ShieldComp.NewTmp1.IntersectWith(ShieldComp.RemSubs);
                ShieldComp.RemSubs.ExceptWith(ShieldComp.AddSubs);
                ShieldComp.AddSubs.ExceptWith(ShieldComp.NewTmp1);
                ShieldComp.NewTmp1.Clear();
                if (ShieldComp.AddSubs.Count != 0 || ShieldComp.RemSubs.Count != 0) MyAPIGateway.Parallel.StartBackground(SubChangePreEvents, SubChangeCallback);
                else SetSubFlags();
            }
        }
        #endregion

        private void SubChangePreEvents()
        {
            lock (SubUpdateLock)
            {
                foreach (var addSub in ShieldComp.AddSubs)
                {
                    RegisterGridEvents(true, addSub);
                    var gridIntegrity = GridIntegrity(addSub);
                    AddSubGridInfo.Enqueue(new SubGridComputedInfo(addSub, gridIntegrity));
                    BlockSets.TryAdd(addSub, new BlockSets());
                    bool mechSub;
                    lock (SubLock) mechSub = ShieldComp.SubGrids.Contains(addSub);
                    UpdateSubBlockCollections(addSub, mechSub);
                    Log.Line($"SubAdd: Integrity:{gridIntegrity} - newTotal:{DsState.State.GridIntegrity}");
                }
                ShieldComp.AddSubs.Clear();

                foreach (var remSub in ShieldComp.RemSubs)
                {
                    RegisterGridEvents(false, remSub);
                    GridIntegrity(remSub, true);
                    Log.Line($"SubRemove: newTotal:{DsState.State.GridIntegrity}");
                }
                ShieldComp.RemSubs.Clear();
            }
        }

        private void UpdateSubBlockCollections(MyCubeGrid sub, bool mechSub, bool remove = false)
        {

            if (remove)
            {
                BlockSets value;
                BlockSets.TryRemove(sub, out value);
                return;
            }

            foreach (var block in sub.GetFatBlocks())
            {
                if (mechSub)
                {
                    var controller = block as MyShipController;
                    if (controller != null) BlockSets[sub].ShipControllers.Add(controller);
                }

                var source = block.Components.Get<MyResourceSourceComponent>();
                if (source != null)
                {
                    if (source.ResourceTypes[0] != GId) continue;

                    var battery = block as IMyBatteryBlock;
                    if (battery != null)
                    {
                        BlockSets[sub].Batteries.Add(new BatteryInfo(source));
                    }
                    BlockSets[sub].Sources.Add(source);
                }
            }
        }

        private void SubChangeCallback()
        {
            lock (SubLock)
            {
                SubGridComputedInfo info;
                while (AddSubGridInfo.TryDequeue(out info))
                {
                    ShieldComp.LinkedGrids[info.Grid].Integrity = info.Integrity;
                }
            }

            if (MyResourceDist == null || MyResourceDist.SourcesEnabled == MyMultipleEnabledEnum.NoObjects) _checkForDistributor = true;

            SetSubFlags();
        }

        private bool GetDistributor()
        {
            var gotDistributor = false;
            foreach (var set in BlockSets.Values)
            {
                foreach (var controller in set.ShipControllers)
                {
                    var distributor = controller.GridResourceDistributor;
                    if (distributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects)
                    {
                        if (Session.Enforced.Debug == 2) Log.Line($"Found MyResourceDist from type - ShieldId [{Shield.EntityId}]");
                        MyResourceDist = controller.GridResourceDistributor;
                        gotDistributor = true;
                        break;
                    }
                }
            }

            if (!gotDistributor) MyResourceDist = null;

            Log.Line($"GetDistributor: {gotDistributor}");
            _checkForDistributor = false;
            return gotDistributor;
        }

        private void SetSubFlags()
        {
            _blockChanged = true;
            _subTick = _tick + 10;
        }

        private void CleanAll()
        {
            CleanWebEnts();
            ResetDamageEffects();
        }
    }
}
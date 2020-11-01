﻿using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    using System;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using Sandbox.Definitions;
    using Sandbox.Game.EntityComponents;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DSControlLarge", "DSControlSmall", "DSControlTable", "NPCControlSB", "NPCControlLB")]
    public partial class DefenseShields : MyGameLogicComponent
    {
        private void OnFatBlockAdded(MyCubeBlock block)
        {
            lock (SubLock)
            {
                if (!_isDedicated)
                {
                    _functionalBlocks.Add(block);

                    var display = block as IMyTextPanel;
                    if (display != null)
                    {
                        _displayBlocks.Add(display);
                    }
                }

                var battery = block as IMyBatteryBlock;
                if (battery != null)
                {
                    _batteryBlocks.Add(battery);
                }

                var powerBlock = block as IMyPowerProducer;
                if (powerBlock != null)
                {
                    var source = powerBlock.Components.Get<MyResourceSourceComponent>();
                    if (source != null)
                    {
                        foreach (var type in source.ResourceTypes)
                        {
                            if (type != MyResourceDistributorComponent.ElectricityId)
                            {
                                _powerSources.Add(source);
                                break;
                            }
                        }
                    }
                }
            }
        }
        private void OnFatBlockRemoved(MyCubeBlock block)
        {
            lock (SubLock)
            {
                if (!_isDedicated)
                {
                    _functionalBlocks.Remove(block);

                    var display = block as IMyTextPanel;
                    if (display != null)
                    {
                        _displayBlocks.Remove(display);
                    }
                }

                var battery = block as IMyBatteryBlock;
                if (battery != null)
                {
                    _batteryBlocks.Remove(battery);
                }

                var powerBlock = block as IMyPowerProducer;
                if (powerBlock != null)
                {
                    var source = powerBlock.Components.Get<MyResourceSourceComponent>();
                    if (source != null)
                    {
                        foreach (var type in source.ResourceTypes)
                        {
                            if (type != MyResourceDistributorComponent.ElectricityId)
                            {
                                _powerSources.Remove(source);
                                break;
                            }
                        }
                    }
                }
            }
        }

        #region Simulation
        public override void OnAddedToContainer()
        {
            if (!_containerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Shield = (IMyUpgradeModule)Entity;
                _containerInited = true;
            }

            if (Entity.InScene) OnAddedToScene();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            StorageSetup();
        }

        public override void OnAddedToScene()
        {
            try
            {
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");
                MyGrid = (MyCubeGrid)Shield.CubeGrid;
                MyCube = Shield as MyCubeBlock;
                RegisterEvents();
                AssignSlots();
                _resetEntity = true;

                MyGrid.OnFatBlockAdded += OnFatBlockAdded;
                MyGrid.OnFatBlockRemoved += OnFatBlockRemoved;

                GridMaxPower = 0;
                foreach (var block in MyGrid.GetFatBlocks())
                {
                    OnFatBlockAdded(block);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (!_bInit) BeforeInit();
                else if (_bCount < SyncCount * _bTime)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    if (ShieldComp?.DefenseShields != null && ShieldComp.DefenseShields.Warming) _bCount++;
                }
                else _readyToSync = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!EntityAlive()) return;
                var shield = ShieldOn();
                if (shield != State.Active)
                {
                    if (NotFailed)
                    {
                        if (Session.Enforced.Debug >= 2) Log.Line($"FailState: {shield} - ShieldId [{Shield.EntityId}]");
                        var up = shield != State.Lowered;
                        var awake = shield != State.Sleep;
                        var clear = up && awake;
                        OfflineShield(clear, up);
                    }
                    else if (DsState.State.Message) ShieldChangeState();
                    return;
                }

                if (!_isServer || !DsState.State.Online) return;
                if (_comingOnline) ComingOnlineSetup();
                if (_mpActive && (_forceBufferSync || _count == 29))
                {
                    var newPercentColor = UtilsStatic.GetShieldColorFromFloat(DsState.State.ShieldPercent);
                    if (_forceBufferSync || newPercentColor != _oldPercentColor)
                    {
                        ShieldChangeState();
                        _oldPercentColor = newPercentColor;
                        _forceBufferSync = false;
                    }
                    else if (_tick180) ShieldChangeState();
                }
                if (Session.Instance.EmpWork.EventRunning) AbsorbEmp();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Shield.Storage != null)
                {
                    DsState.SaveState();
                    DsSet.SaveSettings();
                }
            }
            return false;
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!_allInited) return;
                if (Session.Enforced.Debug >= 3) Log.Line($"OnRemovedFromScene: {ShieldMode} - GridId:{Shield.CubeGrid.EntityId} - ShieldId [{Shield.EntityId}]");

                if (ShieldComp?.DefenseShields == this)
                {
                    OfflineShield(true, false, true);
                    ShieldComp.DefenseShields = null;
                }

                RegisterEvents(false);
                InitEntities(false);
                IsWorking = false;
                IsFunctional = false;
                _shellPassive?.Render?.RemoveRenderObjects();
                _shellActive?.Render?.RemoveRenderObjects();
                ShieldEnt?.Render?.RemoveRenderObjects();

                MyGrid.OnFatBlockAdded -= OnFatBlockAdded;
                MyGrid.OnFatBlockRemoved -= OnFatBlockRemoved;

                foreach (var block in MyGrid.GetFatBlocks())
                {
                    OnFatBlockRemoved(block);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }

        public override void Close()
        {
            try
            {
                base.Close();
                if (!_allInited) return;
                if (Session.Enforced.Debug >= 3) Log.Line($"Close: {ShieldMode} - ShieldId [{Shield.EntityId}]");

                if (ShieldComp?.DefenseShields == this)
                {
                    OfflineShield(true, false, true);
                    ShieldComp.DefenseShields = null;
                }

                if (Session.Instance.Controllers.Contains(this)) Session.Instance.Controllers.Remove(this);
                bool value1;

                if (Session.Instance.FunctionalShields.ContainsKey(this)) Session.Instance.FunctionalShields.TryRemove(this, out value1);

                Icosphere = null;
                InitEntities(false);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(_ellipsoidOxyProvider);
                if (_sink != null)
                {
                    _resourceInfo = new MyResourceSinkInfo
                    {
                        ResourceTypeId = GId,
                        MaxRequiredInput = 0f,
                        RequiredInputFunc = null
                    };
                    _sink.Init(MyStringHash.GetOrCompute("Defense"), _resourceInfo);
                    _sink = null;
                }

                ShieldComp = null;
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }
        #endregion
    }
}
namespace DefenseShields
{
    using System;
    using System.Text;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.EntityComponents;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Weapons;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;

    public partial class DefenseShields
    {
        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    ((MyCubeGrid)Shield.CubeGrid).OnBlockOwnershipChanged += OwnerChanged;
                    MyEntities.OnEntityAdd += OnEntityAdd;
                    MyEntities.OnEntityRemove += OnEntityRemove;
                }

                OnShieldDetectedEvent += OnShieldDetected;

                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                RegisterGridEvents();
                Shield.AppendingCustomInfo += AppendingCustomInfo;
                _sink.CurrentInputChanged += CurrentInputChanged;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

            }
            else
            {
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    ((MyCubeGrid)Shield.CubeGrid).OnBlockOwnershipChanged -= OwnerChanged;
                    MyEntities.OnEntityAdd -= OnEntityAdd;
                    MyEntities.OnEntityRemove -= OnEntityRemove;
                }

                OnShieldDetectedEvent -= OnShieldDetected;

                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;
                RegisterGridEvents(false);
                Shield.AppendingCustomInfo -= AppendingCustomInfo;
                _sink.CurrentInputChanged -= CurrentInputChanged;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void RegisterGridEvents(bool register = true, MyCubeGrid grid = null)
        {
            if (grid == null) grid = MyGrid;
            if (register)
            {
                grid.OnBlockAdded += BlockAdded;
                grid.OnBlockRemoved += BlockRemoved;
                grid.OnFatBlockAdded += FatBlockAdded;
                grid.OnFatBlockRemoved += FatBlockRemoved;
                grid.OnGridSplit += GridSplit;
            }
            else
            {
                grid.OnBlockAdded -= BlockAdded;
                grid.OnBlockRemoved -= BlockRemoved;
                grid.OnFatBlockAdded -= FatBlockAdded;
                grid.OnFatBlockRemoved -= FatBlockRemoved;
                grid.OnGridSplit -= GridSplit;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsWorking = myCubeBlock.IsWorking;
            IsFunctional = myCubeBlock.IsFunctional;
        }

        private void OwnerChanged(MyCubeGrid myCubeGrid)
        {
            try
            {
                if (MyCube == null || MyGrid == null || MyCube.OwnerId == _controllerOwnerId && MyGrid.BigOwners.Count != 0 && MyGrid.BigOwners[0] == _gridOwnerId) return;
                GridOwnsController();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OwnerChanged: {ex}"); }
        }

        private void OnEntityAdd(MyEntity myEntity)
        {
            try
            {
                if (myEntity?.Physics == null || myEntity is MyVoxelBase || myEntity is MyFloatingObject || myEntity is IMyCharacter || myEntity is IMyEngineerToolBase || !myEntity.InScene || myEntity.MarkedForClose || myEntity.IsPreview) return;
                if (DsState.State.ReInforce) return;
                var isMissile = myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile);
                if (!isMissile && !(myEntity is MyCubeGrid)) return;

                var aabb = myEntity.PositionComp.WorldAABB;
                if (!ShieldBox3K.Intersects(ref aabb)) return;
                Asleep = false;
                if (isMissile) Missiles.Add(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityAdd: {ex}"); }
        }

        private void OnEntityRemove(MyEntity myEntity)
        {
            try
            {
                if (myEntity?.Physics == null || myEntity is MyVoxelBase || myEntity is MyFloatingObject || myEntity is IMyCharacter || myEntity is IMyEngineerToolBase || !myEntity.InScene || myEntity.MarkedForClose || myEntity.IsPreview) return;

                if (DsState.State.ReInforce) return;

                if (!(myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile))) return;

                Missiles.Remove(myEntity);
                FriendlyMissileCache.Remove(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityRemove: {ex}"); }
        }

        private void GridSplit(MyCubeGrid oldGrid, MyCubeGrid newGrid)
        {
            newGrid.RecalculateOwners();
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            _subUpdate = true;
        }

        private void BlockAdded(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockAdded = true;
                _blockChanged = true;
                if (_isServer) DsState.State.GridIntegrity += mySlimBlock.MaxIntegrity;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemoved(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockChanged = true;
                if (_isServer) DsState.State.GridIntegrity -= mySlimBlock.MaxIntegrity;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

        private void FatBlockAdded(MyCubeBlock myCubeBlock)
        {
            try
            {
                var controller = myCubeBlock as MyShipController;
                if (controller != null)
                {
                    BlockSets[myCubeBlock.CubeGrid].ShipControllers.Add(controller);
                    _checkForDistributor = true;
                    return;
                }

                var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                if (source != null)
                {
                    if (source.ResourceTypes[0] != GId) return;

                    var battery = myCubeBlock as IMyBatteryBlock;
                    if (battery != null)
                    {
                        BlockSets[myCubeBlock.CubeGrid].Batteries.Add(new BatteryInfo(source));
                    }

                    BlockSets[myCubeBlock.CubeGrid].Sources.Add(source);
                    _updatePowerSources = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                var controller = myCubeBlock as MyShipController;

                if (controller != null)
                {
                    BlockSets[myCubeBlock.CubeGrid].ShipControllers.Remove(controller);
                    _checkForDistributor = true;
                    return;
                }
                var source = myCubeBlock.Components.Get<MyResourceSourceComponent>();
                if (source != null)
                {
                    if (source.ResourceTypes[0] != GId) return;

                    var battery = myCubeBlock as IMyBatteryBlock;
                    if (battery != null)
                    {
                        BlockSets[myCubeBlock.CubeGrid].Batteries.Remove(new BatteryInfo(source));
                    }

                    BlockSets[myCubeBlock.CubeGrid].Sources.Remove(source);
                    _updatePowerSources = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private void OnShieldDetected(DefenseShields detector, DefenseShields detected) 
        {

        }

        private string GetShieldStatus()
        {
            if (!DsState.State.Online && (!MyCube.IsWorking || !MyCube.IsFunctional)) return "[Controller Failure]";
            if (!DsState.State.Online && DsState.State.NoPower) return "[Insufficient Power]";
            if (!DsState.State.Online && DsState.State.Overload) return "[Overloaded]";
            if (!DsState.State.ControllerGridAccess) return "[Invalid Owner]";
            if (DsState.State.Waking) return "[Coming Online]";
            if (DsState.State.Suspended || DsState.State.Mode == 4) return "[Controller Standby]";
            if (DsState.State.Lowered) return "[Shield Down]";
            if (!DsState.State.EmitterWorking) return "[Emitter Failure]";
            if (DsState.State.Sleeping) return "[Suspended]";
            if (!DsState.State.Online) return "[Shield Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !DsState.State.Online ? 0f : 100f;

                if (DsState.State.Charge < ShieldMaxCharge) shieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
                if (_shieldChargeRate > 0)
                {
                    var toMax = ShieldMaxCharge - DsState.State.Charge;
                    var secs = toMax / _shieldChargeRate;
                    if (secs.Equals(1)) secToFull = 0;
                    else secToFull = (int)secs;
                }

                var shieldPowerNeeds = _powerNeeded;
                var powerUsage = shieldPowerNeeds;
                var otherPower = _otherPower;
                var gridMaxPower = GridMaxPower;
                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]")
                {
                    stringBuilder.Append(status + " MaxHP: " + (ShieldMaxCharge * ConvToHp).ToString("N0") +
                                         "\n" +
                                         "\n[Shield HP__]: " + (DsState.State.Charge * ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[HP Per Sec_]: " + (_shieldChargeRate * ConvToHp).ToString("N0") +
                                         "\n[Damage In__]: " + _damageReadOut.ToString("N0") +
                                         "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                         "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                         "\n[Over Heated]: " + DsState.State.Heat.ToString("0") + "%" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Power Usage]: " + powerUsage.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ")Mw" +
                                         "\n[Shield Power]: " + _sink.CurrentInputByType(GId).ToString("0.0") + " Mw");
                }
                else
                {
                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Other Power]: " + otherPower.ToString("0.0") + " Mw" +
                                         "\n[HP Stored]: " + (DsState.State.Charge * ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Needed Power]: " + shieldPowerNeeds.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                         "\n[Emitter Working]: " + DsState.State.EmitterWorking +
                                         "\n[Ship Emitter]: " + (ShieldComp?.ShipEmitter != null) +
                                         "\n[Station Emitter]: " + (ShieldComp?.StationEmitter != null) +
                                         "\n[Grid Owns Controller]: " + DsState.State.IsOwner +
                                         "\n[In Grid's Faction]: " + DsState.State.InFaction);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}

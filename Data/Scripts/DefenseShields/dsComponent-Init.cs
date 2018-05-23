using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Startup Logic
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                //Log.Line($"Starting Init for {Entity.EntityId.ToString()}");
                Entity.Components.TryGet(out Sink);
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.RemoveType(ref ResourceInfo.ResourceTypeId);
                Sink.Init(MyStringHash.GetOrCompute("Defense"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);

                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!DefinitionsLoaded && AnimateInit && _tick > 300)
                {
                    DefinitionsLoaded = true;
                    GetDefinitons();
                }
                if (AnimateInit && MainInit || !Shield.IsFunctional) return;

                HardDisable = false || (Shield.EntityId != ThereCanBeOnlyOne() || (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" && !Shield.CubeGrid.Physics.IsStatic)
                                                                               || (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic));
                NoPower = false;
                if (!HardDisable) AddResourceSourceComponent();

                if (HardDisable)
                {
                    var realPlayerIds = new List<long>();
                    DsUtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds)
                    {
                        if (!_startupWarning && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" &&
                            !Shield.CubeGrid.Physics.IsStatic)
                        {
                            MyVisualScriptLogicProvider.ShowNotification("Station shields only allowed on stations", 5000, "Red", id);
                            _startupWarning = true;
                        }
                        else if (!_startupWarning && Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic)
                        {
                            MyVisualScriptLogicProvider.ShowNotification("Large Ship Shields only allowed on ships, not stations", 5000, "Red", id);
                            _startupWarning = true;
                        }
                        else if (!_startupWarning && NoPower)
                        {
                            MyVisualScriptLogicProvider.ShowNotification("Insufficent power to bring Shield online", 5000, "Red", id);
                            _startupWarning = true;
                        }
                        else if (!_startupWarning)
                        {
                            MyVisualScriptLogicProvider.ShowNotification("Only one generator per grid in this version", 5000, "Red", id);
                            _startupWarning = true;
                        }
                    }
                    return;
                }

                if (Icosphere == null)
                {
                    //Log.Line($"_icosphere Null!");
                    Icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere);
                    if (!DefenseShieldsBase.Instance.Components.Contains(this)) DefenseShieldsBase.Instance.Components.Add(this);
                    if (!Shield.CubeGrid.Components.Has<ShieldGridComponent>()) Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));
                }

                if (!MainInit && Shield.IsFunctional)
                {
                    //Log.Line($"Initting {Shield.BlockDefinition.SubtypeId} - tick:{_tick.ToString()}");
                    if (Shield.CubeGrid.Physics.IsStatic) GridIsMobile = false;
                    else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                    MyEntity parent;
                    if (GridIsMobile) parent = (MyEntity)Shield.CubeGrid;
                    else parent = (MyEntity)Shield.CubeGrid;

                    CreateUi();

                    _shellPassive = Spawn.EmptyEntity("dShellPassive", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\Cubes\\ShieldPassive_LOD0.mwm", parent, true);
                    _shellPassive.Render.CastShadows = false;
                    _shellPassive.IsPreview = true;
                    _shellPassive.Render.Visible = true;
                    _shellPassive.Render.RemoveRenderObjects();
                    _shellPassive.Render.UpdateRenderObject(true);
                    _shellPassive.Save = false;

                    _shellActive = Spawn.EmptyEntity("dShellActive", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\Cubes\\ShieldActiveH_LOD3.mwm", parent, true);
                    _shellActive.Render.CastShadows = false;
                    _shellActive.IsPreview = true;
                    _shellActive.Render.Visible = true;
                    _shellActive.Render.RemoveRenderObjects();
                    _shellActive.Render.UpdateRenderObject(true);
                    _shellActive.Render.UpdateRenderObject(false);
                    _shellActive.Save = false;
                    _shellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Black, 0.01f);

                    _shield = Spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
                    //_shield = Spawn.SpawnBlock("dShield", $"{Shield.EntityId}", true, false, false, false, true, Shield.OwnerId);
                    _shield.Render.CastShadows = false;
                    _shield.Render.RemoveRenderObjects();
                    _shield.Render.UpdateRenderObject(true);
                    _shield.Render.Visible = true;
                    _shield.Save = false;

                    Shield.AppendingCustomInfo += AppendingCustomInfo;
                    Shield.RefreshCustomInfo();

                    MainInit = true;
                }

                if (AnimateInit || !MainInit || !Shield.IsFunctional) return;

                if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                {
                    _blocksChanged = true;
                    Log.Line($"ShieldId:{Shield.EntityId.ToString()} - {Shield.BlockDefinition.SubtypeId} is functional - tick:{_tick.ToString()}");
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);

                    ServerUpdate = true;
                    _updateDimensions = true;
                    Storage = Shield.Storage;
                    LoadSettings();

                    if (!MyAPIGateway.Utilities.IsDedicated) BlockParticleCreate();
                    if (GridIsMobile) MobileUpdate();
                    else RefreshDimensions();

                    Icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);

                    AnimateInit = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation100: {ex}"); }
        }

        private void AddResourceSourceComponent()
        {
            try
            {
                if (!Sink.IsPowerAvailable(GId, _power))
                {
                    //Log.Line($"ShieldId:{Shield.EntityId.ToString()} - no power to init resourceSink: {_power.ToString()}");
                    NoPower = true;
                    HardDisable = true;
                    return;
                }
                HardDisable = false;
                NoPower = false;
                UpdateGridPower();
                CalculatePowerCharge();
                SetPower();
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private long ThereCanBeOnlyOne()
        {
            var gridStatic = Shield.CubeGrid.Physics.IsStatic;
            var shieldBlocks = new List<MyCubeBlock>();
            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                if (block == null) continue;

                if (block.BlockDefinition.BlockPairName.Equals("DefenseShield") || block.BlockDefinition.BlockPairName.Equals("StationShield"))
                {
                    if (gridStatic && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                    else if (!gridStatic && (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS"))
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                }
            }
            var shieldDistFromCenter = double.MinValue;
            var shieldId = long.MinValue;
            foreach (var shield in shieldBlocks)
            {
                if (shield == null) continue;
                if (gridStatic && shield.BlockDefinition.BlockPairName.Equals("DefenseShield")) continue;
                if (!gridStatic && shield.BlockDefinition.BlockPairName.Equals("StationShield")) continue;

                var dist = Vector3D.DistanceSquared(shield.PositionComp.WorldVolume.Center, Shield.CubeGrid.WorldVolume.Center);
                if (dist > shieldDistFromCenter)
                {
                    shieldDistFromCenter = dist;
                    shieldId = shield.EntityId;
                }
            }
            return shieldId;
        }

        private void GetDefinitons()
        {
            try
            {
                var defintions = MyDefinitionManager.Static.GetAllDefinitions();
                foreach (var def in defintions)
                {
                    if (!(def is MyAmmoMagazineDefinition)) continue;
                    var ammoDef = def as MyAmmoMagazineDefinition;
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(ammoDef.AmmoDefinitionId);
                    if (!(ammo is MyMissileAmmoDefinition)) continue;
                    var shot = ammo as MyMissileAmmoDefinition;
                    if (_ammoInfo.ContainsKey(shot.MissileModelName)) continue;
                    _ammoInfo.Add(shot.MissileModelName, new AmmoInfo(shot.IsExplosive, shot.MissileExplosionDamage, shot.MissileExplosionRadius, shot.DesiredSpeed, shot.MissileMass, shot.BackkickForce));
                }
            }
            catch (Exception ex) { Log.Line($"Exception in GetAmmoDefinitions: {ex}"); }
        }
        #endregion

        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void RemoveOreUi()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;
        }

        private void CreateUi()
        {
            //Log.Line($"Create UI - Tick:{_tick.ToString()}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _chargeSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ChargeRate", "Shield Charge Rate", 20, 95, 50);
            _hidePassiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HidePassive", "Hide idle shield state", false);
            _hideActiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HideActive", "Hide active shield state", false);

            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") return;

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 30, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 300, 100);
        }
        #endregion
    }
}

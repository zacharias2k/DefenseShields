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
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

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

        private void CheckShieldLineOfSight()
        {
            if (GridIsMobile)
            {
                MobileUpdate();
                Icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);
            }
            else RefreshDimensions();

            var testDist = 0d;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") testDist = 4.5d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") testDist = 0.8d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") testDist = 8.0d;

            var testDir = _subpartRotor.PositionComp.WorldVolume.Center - Shield.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = Shield.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;

            MyAPIGateway.Parallel.For(0, PhysicsOutside.Length, i =>
            {
                var hit = Shield.CubeGrid.RayCastBlocks(testPos, PhysicsOutside[i]);
                if (hit.HasValue)
                {
                    _blocksLos.Add(i);
                    return;
                }
                _noBlocksLos.Add(i);
            });
            if (GridIsMobile)
            {
                MyAPIGateway.Parallel.For(0, _noBlocksLos.Count, i =>
                {
                    const int filter = CollisionLayers.VoxelCollisionLayer;
                    IHitInfo hitInfo;
                    var hit = MyAPIGateway.Physics.CastRay(testPos, PhysicsOutside[_noBlocksLos[i]], out hitInfo, filter);
                    if (hit) _blocksLos.Add(_noBlocksLos[i]);
                });
            }

            for (int i = 0; i < PhysicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
            _shieldLineOfSight = _blocksLos.Count < 500;
            Log.Line($"ShieldId:{Shield.EntityId.ToString()} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {_shieldLineOfSight.ToString()}");
        }

        private void DrawHelper()
        {
            var lineDist = 0d;
            const float lineWidth = 0.025f;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") lineDist = 5.0d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") lineDist = 3d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") lineDist = 7.5d;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = PhysicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = PhysicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("The shield emitter DOES NOT have a CLEAR ENOUGH LINE OF SIGHT to the shield, SHUTTING DOWN.", 960, "Red", Shield.OwnerId);
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("Blue means clear line of sight, black means blocked......................................................................", 960, "Red", Shield.OwnerId);
        }

        private bool FieldShapeBlocked()
        {
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            MobileUpdate();
            Icosphere.ReturnPhysicsVerts(_detectMatrixOutside, PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null) continue;

                if (!CustomCollision.VoxelContact(Shield.CubeGrid, PhysicsOutsideLow, voxel, new MyStorageData(), _detectMatrixOutside)) continue;

                Shield.Enabled = false;
                MyVisualScriptLogicProvider.ShowNotification("The shield's field cannot form when in contact with a solid body", 6720, "Blue", Shield.OwnerId);
                return true;
            }
            return false;
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

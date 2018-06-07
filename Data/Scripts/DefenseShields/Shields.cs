using Sandbox.Game;
using VRageMath;
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRage.Voxels;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;


namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "DefenseShieldsLS", "DefenseShieldsSS", "DefenseShieldsST")]
    public partial class DefenseShields : MyGameLogicComponent
    {
        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Dsutil1.Sw.Restart();
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (!BlockFunctional()) return;

                if (ServerUpdate) SyncControlsServer();
                SyncControlsClient();

                if (GridIsMobile) MobileUpdate();
                if (_updateDimensions) RefreshDimensions();
                if (_fitChanged || (_longLoop == 0 && _count == 0 && _blocksChanged))
                {
                    _oldEllipsoidAdjust = _ellipsoidAdjust;
                    _fitChanged = false;

                    if (GridIsMobile)
                    {
                        CreateHalfExtents();
                        if (_shapeAdjusted) _shapeLoaded = true;
                        else if (_shapeLoaded) MyAPIGateway.Parallel.StartBackground(GetShapeAdjust);
                    }

                    if (_blocksChanged)
                    {
                        MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
                        CheckShieldLineOfSight();
                        _blocksChanged = false;
                    } 
                }
                if (_shieldLineOfSight == false && !Session.DedicatedServer) DrawHelper();

                ShieldActive = BlockWorking && _shieldLineOfSight;
                if (_prevShieldActive == false && ShieldActive) _shieldStarting = true;
                else if (_shieldStarting && _prevShieldActive && ShieldActive) _shieldStarting = false;
                _prevShieldActive = ShieldActive;

                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10) _longLoop = 0;
                }

                if (_staleGrids.Count != 0) CleanUp(0);
                if (_longLoop == 9 && _count == 58) CleanUp(1);
                if (_effectsCleanup && (_count == 1 || _count == 21 || _count == 41)) CleanUp(2);
                if (_longLoop % 2 == 0 && _count == 5) CleanUp(3);            
                if (_longLoop == 5 && _count == 10) CleanUp(4);
                if (_longLoop == 6 && _count == 15 && (Session.DedicatedServer || Session.IsServer)) SaveSettings();

                UpdateGridPower();
                CalculatePowerCharge();
                SetPower();

                if (_count == 29)
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                    {
                        Shield.ShowInToolbarConfig = false;
                        Shield.ShowInToolbarConfig = true;
                    }
                    else if (_longLoop == 0 || _longLoop == 5)
                    {
                        Shield.RefreshCustomInfo();
                    }
                    _shieldDps = 0f;
                }
                if (ShieldActive)
                {
                    if (_longLoop % 2 != 0 && _count == 20)
                    {
                        GetModulationInfo();
                        if (_reModulationLoop > -1) return;
                    }
                    if (_shieldStarting)
                    {
                        if (_shieldStarting && GridIsMobile && FieldShapeBlocked()) return;
                        if (!(_hidePassiveCheckBox.Getter(Shield).Equals(true))) _shellPassive.Render.UpdateRenderObject(true);

                        _shellActive.Render.UpdateRenderObject(true);
                        _shellActive.Render.UpdateRenderObject(false);
                        _shield.Render.Visible = true;
                        _shield.Render.UpdateRenderObject(true);
                        SyncThreadedEnts(true);
                        if (!GridIsMobile) EllipsoidOxyProvider.UpdateMatrix(_detectMatrixOutsideInv);
                        if (!_warmedUp) 
                        {
                            _warmedUp = true;
                            if (Session.Enforced.Debug == 1) Log.Line($"Warmup complete");
                            return;
                        }
                    }
                    if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
                    if ((!Session.DedicatedServer) && UtilsStatic.Distance(Shield, 1000, Range))
                    {
                        if (_shieldMoving || _shieldStarting) BlockParticleUpdate();
                        var blockCam = Shield.PositionComp.WorldVolume;
                        if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam))
                        {
                            if (_blockParticleStopped) BlockParticleStart();
                            _blockParticleStopped = false;
                            BlockMoveAnimation();

                            if (_animationLoop++ == 599) _animationLoop = 0;
                        }
                    }
                    SyncThreadedEnts();
                    _enablePhysics = false;
                    WebEntities();
                }
                else
                {
                    SyncThreadedEnts();
                    if (!_blockParticleStopped) BlockParticleStop();
                }
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"MainLoop: ShieldId:{Shield.EntityId.ToString()} - Active: {ShieldActive} - Tick: {_tick} loop: {_longLoop}-{_count}", 4);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public void CreateHalfExtents()
        {
            var myAabb = Shield.CubeGrid.PositionComp.LocalAABB;
            var shieldGrid = Shield.CubeGrid;
            var expandedAabb = myAabb;
            foreach (var grid in _subGrids)
            {
                if (grid != shieldGrid)
                {
                    var shieldMatrix = shieldGrid.WorldMatrixNormalizedInv;
                    var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                    var gOriBBoxD = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, gQuaternion);
                    gOriBBoxD.Transform(shieldMatrix);
                    expandedAabb.Include(gOriBBoxD.GetAABB());
                }
            }

            _expandedAabb = expandedAabb;
            _shieldFudge = 0f;
            if (SphereFit || FortifyShield)
            {
                var extend = ExtendFit ? 2 : 1;
                var fortify = FortifyShield ? 3 : 1;

                var size = expandedAabb.HalfExtents.Max() * fortify;
                _gridHalfExtents = new Vector3D(size, size, size);
                _shieldFudge = (shieldGrid.GridSize * 4 * extend);
            }
            else _gridHalfExtents = expandedAabb.HalfExtents;
        }

        private void GetShapeAdjust()
        {
            if (SphereFit || FortifyShield) _ellipsoidAdjust = 1f;
            else if (!ExtendFit) _ellipsoidAdjust = UtilsStatic.CreateNormalFit(Shield, _gridHalfExtents);
            else _ellipsoidAdjust = UtilsStatic.CreateExtendedFit(Shield, _gridHalfExtents);
        }

        public int BlockCount()
        {
            var blockCnt = 0;
            for (int i = 0; i < _subGrids.Count; i++)
            {
                blockCnt += ((MyCubeGrid)_subGrids[i]).BlocksCount;
            }
            return blockCnt;
        }

        private void BackGroundChecks()
        {
            lock (_powerSources) _powerSources.Clear();
            lock (_functionalBlocks) _functionalBlocks.Clear();

            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                lock (_functionalBlocks) if (block.IsFunctional) _functionalBlocks.Add(block);
                var source = block.Components.Get<MyResourceSourceComponent>();
                if (source == null) continue;
                foreach (var type in source.ResourceTypes)
                {
                    if (type != MyResourceDistributorComponent.ElectricityId) continue;
                    lock (_powerSources) _powerSources.Add(source);
                    break;
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - powerCnt: {_powerSources.Count.ToString()}");
        }

        private bool ConnectCheck()
        {
            if (!Shield.Enabled) return true;

            var subId = Shield.BlockDefinition.SubtypeId;
            var myGrid = Shield.CubeGrid;
            var myGridIsSub = false;

            _subGrids = MyAPIGateway.GridGroups.GetGroup(myGrid, GridLinkTypeEnum.Logical);
            if (_subGrids.Count <= 1) return false;
            CreateHalfExtents();

            if (subId == "DefenseShieldsSS")
            {
                foreach (var grid in _subGrids)
                {
                    if (grid != myGrid && grid.GridSizeEnum == MyCubeSize.Large)
                    {
                        if (myGrid.PositionComp.WorldAABB.Volume < grid.PositionComp.WorldAABB.Volume) myGridIsSub = true;
                    }
                }
            }
            else if (subId == "DefenseShieldsLS")
            {
                foreach (var grid in _subGrids)
                {
                    if (grid != myGrid && grid.GridSizeEnum == MyCubeSize.Large)
                    {
                        if (myGrid.PositionComp.WorldAABB.Volume < grid.PositionComp.WorldAABB.Volume) myGridIsSub = true;
                    }
                }
            }
            //if (subId != "DefenseShieldsST" && _expandedAabb.Volume() > myGrid.PositionComp.LocalAABB.Volume() * 3) myGridIsSub = true;
            if (myGridIsSub)
            {
                var realPlayerIds = new List<long>();
                UtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                foreach (var id in realPlayerIds)
                {
                    MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- primary grid is connected to much larger body, powering shield down.", 4800, "White", id);
                }
                Shield.Enabled = false;
            }
            return myGridIsSub;
        }

        private bool BlockFunctional()
        {
            if (!AllInited) return false;

            if (Range.Equals(0)) // populate matrices and prep for smooth init.
            {
                _subGrids = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Logical);
                var blockCnt = BlockCount();
                if (!_blocksChanged) _blocksChanged = blockCnt != _oldBlockCount;
                _oldBlockCount = blockCnt;

                if (GridIsMobile)
                {
                    CreateHalfExtents();
                    GetShapeAdjust();
                    MobileUpdate();
                }
                else
                {
                    _updateDimensions = true;
                    RefreshDimensions();
                }

                Icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);
                SyncControlsClient();

                BackGroundChecks();
                UpdateGridPower();
                GetModulationInfo();
                CheckShieldLineOfSight();
                _shapeAdjusted = false;
                _blocksChanged = false;
                BlockWorking = MainInit && AnimateInit && Shield.IsWorking && Shield.IsFunctional;
                if (Session.Enforced.Debug == 1) Log.Line($"range warmup enforced:\n{Session.Enforced}");
                if (Session.Enforced.Debug == 1) Log.Line($"range warmup buffer:{ShieldBuffer} - BlockWorking:{BlockWorking} - LoS:{_shieldLineOfSight}");

                return BlockWorking;
            }

            if (_longLoop == 4 && _count == 4 && Shield.Enabled && ConnectCheck()) return false;

            var shieldPowerUsed = Sink.CurrentInputByType(GId);

            if (((MyCubeGrid)Shield.CubeGrid).GetFatBlocks().Count < 2 && ShieldActive && !Session.MpActive)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Shield going critical");
                MyVisualScriptLogicProvider.CreateExplosion(Shield.PositionComp.WorldVolume.Center, (float)Shield.PositionComp.WorldVolume.Radius * 1.25f, 2500);
                return false;
            }

            if (!Shield.IsWorking && Shield.Enabled && Shield.IsFunctional && shieldPowerUsed > 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"fixing shield state power: {_power.ToString()}");
                Shield.Enabled = false;
                Shield.Enabled = true;
                return true;
            }

            if ((!Shield.IsWorking || !Shield.IsFunctional || _shieldDownLoop > -1) || _reModulationLoop > -1)
            {
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                UpdateGridPower();
                if (!GridIsMobile) EllipsoidOxyProvider.UpdateMatrix(MatrixD.Zero);
                BlockParticleStop();
                ShieldActive = false;
                BlockWorking = false;
                _prevShieldActive = false;
                _shellPassive.Render.UpdateRenderObject(false);
                _shellActive.Render.UpdateRenderObject(false);
                _shield.Render.Visible = false;
                _shield.Render.UpdateRenderObject(false);
                Absorb = 0;
                ShieldBuffer = 0;
                _shieldChargeRate = 0;
                _shieldMaxChargeRate = 0;
                if (_shieldDownLoop > -1 || _reModulationLoop > -1)
                {
                    _power = _gridMaxPower * _shieldMaintaintPower;
                    if (_power < 0 || float.IsNaN(_power)) _power = 0.0001f; // temporary definitely 100% will fix this to do - Find ThE NaN!
                    Sink.Update();

                    if (_shieldDownLoop == 0 || _reModulationLoop == 0)
                    {
                        var realPlayerIds = new List<long>();
                        UtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                        foreach (var id in realPlayerIds)
                        {
                            if (_shieldDownLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 19200, "Red", id);
                            if (_reModulationLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield remodulating, restarting in 5 seconds.", 4800, "White", id);
                        }

                        CleanUp(0);
                        CleanUp(1);
                        CleanUp(3);
                        CleanUp(4);
                    }

                    if (_reModulationLoop > -1)
                    {
                        _reModulationLoop++;
                        if (_reModulationLoop == 300)
                        {
                            _reModulationLoop = -1;
                            return false;
                        }
                        return false;
                    }

                    _shieldDownLoop++;
                    if (_shieldDownLoop == 1200)
                    {
                        _shieldDownLoop = -1;
                        var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
                        var nerfer = nerf ? Session.Enforced.Nerf : 1f;
                        ShieldBuffer = (_shieldMaxBuffer / 25) * nerfer; // replace this with something that scales based on charge rate
                    }
                    return false;
                }
                _power = 0.0001f;
                Sink.Update();
                return false;
            }

            if (_longLoop == 4 && _count == 4) _subGrids = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Logical);
            var blockCount = BlockCount();
            if (!_blocksChanged) _blocksChanged = blockCount != _oldBlockCount;
            _oldBlockCount = blockCount;

            BlockWorking = MainInit && AnimateInit && Shield.IsWorking && Shield.IsFunctional;
            return BlockWorking;
        }
        #endregion

        #region Field Check
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
            if (Session.Enforced.Debug == 1) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {_shieldLineOfSight.ToString()}");
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
            if (ModulateVoxels) return false;

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
        #endregion

        #region Shield Shape
        private void MobileUpdate()
        {
            _sVelSqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
            _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
            if (_sVelSqr > 0.00001 || _sAvelSqr > 0.00001 || _shieldStarting) _shieldMoving = true;
            else _shieldMoving = false;

            _shapeAdjusted = !_ellipsoidAdjust.Equals(_oldEllipsoidAdjust) || !_gridHalfExtents.Equals(_oldGridHalfExtents);
            _oldGridHalfExtents = _gridHalfExtents;
            _oldEllipsoidAdjust = _ellipsoidAdjust;
            _entityChanged = Shield.CubeGrid.Physics.IsMoving || _shieldStarting;
            if (_entityChanged || Range <= 0 || _shieldStarting) CreateShieldShape();
        }

        private void CreateShieldShape()
        {
            if (GridIsMobile)
            {
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_shapeAdjusted) CreateMobileShape();
                //DsDebugDraw.DrawSingleVec(_detectionCenter, 10f, Color.Blue);

                //_detectionCenter = Vector3D.Transform(_expandedAabb.Center, Shield.CubeGrid.PositionComp.WorldMatrix);
                //var newDir = Vector3D.TransformNormal(_expandedAabb.HalfExtents, Shield.CubeGrid.PositionComp.WorldMatrix);
                //_expandedMatrix = MatrixD.CreateFromTransformScale(_sQuaternion, _detectionCenter, newDir);
                //DetectionMatrix = _shieldShapeMatrix * _expandedMatrix;
                DetectionMatrix = _shieldShapeMatrix * _shieldGridMatrix;
                _detectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
                _sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, ShieldSize, _sQuaternion);
                //_sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, _expandedAabb.HalfExtents, _sQuaternion);

                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.AbsMax());
                EllipsoidSa.Update(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z);
            }
            else
            {
                _shieldGridMatrix = Shield.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(Width, Height, Depth));
                _shieldShapeMatrix = MatrixD.Rescale(Shield.LocalMatrix, new Vector3D(Width, Height, Depth));
                ShieldSize = DetectionMatrix.Scale;
                _detectionCenter = Shield.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
                _sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, ShieldSize, _sQuaternion);
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.AbsMax());
                EllipsoidSa.Update(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z);
            }
            Range = ShieldSize.AbsMax() + 5f;
            _ellipsoidSurfaceArea = EllipsoidSa.Surface;
            SetShieldShape();
        }

        private void CreateMobileShape()
        {

            var shieldSize = _gridHalfExtents * _ellipsoidAdjust + _shieldFudge;
            ShieldSize = shieldSize;
            var mobileMatrix = MatrixD.CreateScale(shieldSize);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            //mobileMatrix.Translation = _expandedAabb.Center;
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            _shellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
            _shellActive.PositionComp.LocalMatrix = Matrix.Zero;
            _shield.PositionComp.LocalMatrix = Matrix.Zero;

            _shellPassive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            _shellActive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            _shield.PositionComp.LocalMatrix = _shieldShapeMatrix;
            _shield.PositionComp.LocalAABB = _shieldAabb;

            var matrix = _shieldShapeMatrix * Shield.WorldMatrix;
            _shield.PositionComp.SetWorldMatrix(matrix);
            _shield.PositionComp.SetPosition(_detectionCenter);

            if (!GridIsMobile) EllipsoidOxyProvider.UpdateMatrix(_detectMatrixOutsideInv);
        }

        private void RefreshDimensions()
        {

            if (!_updateDimensions) return;
            _updateDimensions = false;
            CreateShieldShape();
            Icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);
            _entityChanged = true;
        }
        #endregion

        #region Block Power Logic
        private void UpdateGridPower()
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;

            lock (_powerSources)
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    var source = _powerSources[i];
                    if (!source.Enabled || !source.ProductionEnabled) continue;
                    _gridMaxPower += source.MaxOutput;
                    _gridCurrentPower += source.CurrentOutput;
                }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
            if (_gridCurrentPower <= 0) Shield.Enabled = false;
        }

        private void CalculatePowerCharge()
        {
            var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
            var rawNerf = nerf ? Session.Enforced.Nerf : 1f;
            var nerfer = rawNerf / _shieldRatio;
            var shieldVol = _detectMatrixOutside.Scale.Volume;
            var powerForShield = 0f;
            const float ratio = 1.25f;
            var rate = _chargeSlider?.Getter(Shield) ?? 20f;
            var percent = rate * ratio;
            var shieldMaintainCost = 1 / percent;
            _shieldMaintaintPower = shieldMaintainCost;
            var fPercent = (percent / ratio) / 100;
            _sizeScaler = (shieldVol / _ellipsoidSurfaceArea) / 2.40063050674088;

            if (ShieldBuffer > 0 && _shieldCurrentPower < 0.00000000001f) // is this even needed anymore?
            {
                Log.Line($"ShieldId:{Shield.EntityId.ToString()} - if u see this it is needed");
                if (ShieldBuffer > _gridMaxPower * shieldMaintainCost) ShieldBuffer -= _gridMaxPower * shieldMaintainCost;
                else ShieldBuffer = 0f;
            }

            _shieldCurrentPower = Sink.CurrentInputByType(GId);

            var otherPower = _gridMaxPower - _gridAvailablePower - _shieldCurrentPower;
            var cleanPower = _gridMaxPower - otherPower;
            powerForShield = (cleanPower * fPercent);

            _shieldMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxBuffer = ((_gridMaxPower * (100 / percent) * Session.Enforced.BaseScaler) / (float)_sizeScaler) * nerfer;
            if (_sizeScaler < 1)
            {
                if (ShieldBuffer + (_shieldMaxChargeRate * nerfer) < _shieldMaxBuffer) _shieldChargeRate = (_shieldMaxChargeRate * nerfer);
                else if (_shieldMaxBuffer - ShieldBuffer > 0) _shieldChargeRate = _shieldMaxBuffer - ShieldBuffer;
                else _shieldMaxChargeRate = 0f;
                _shieldConsumptionRate = _shieldChargeRate;
            }
            else if (ShieldBuffer + (_shieldMaxChargeRate / (_sizeScaler / nerfer)) < _shieldMaxBuffer)
            {
                _shieldChargeRate = _shieldMaxChargeRate / ((float)_sizeScaler / nerfer);
                _shieldConsumptionRate = _shieldMaxChargeRate;
            }
            else
            {
                if (_shieldMaxBuffer - ShieldBuffer > 0)
                {
                    _shieldChargeRate = _shieldMaxBuffer - ShieldBuffer;
                    _shieldConsumptionRate = _shieldChargeRate;
                }
                else _shieldMaxChargeRate = 0f;
            }

            if (_shieldMaxChargeRate < 0.001f)
            {
                _shieldChargeRate = 0f;
                _shieldConsumptionRate = 0f;
                if (ShieldBuffer > _shieldMaxBuffer)  ShieldBuffer = _shieldMaxBuffer;
                return;
            }

            if (ShieldBuffer < _shieldMaxBuffer && _count == 29)
            {
                ShieldBuffer += _shieldChargeRate;
            }
            if (_count == 29)
            {
                _shieldPercent = 100f;
                if (ShieldBuffer < _shieldMaxBuffer) _shieldPercent = (ShieldBuffer / _shieldMaxBuffer) * 100;
                else _shieldPercent = 100f;
            }
        }

        private double PowerCalculation(IMyEntity breaching)
        {
            var bPhysics = breaching.Physics;
            var sPhysics = Shield.CubeGrid.Physics;

            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = sPhysics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = sPhysics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = bPhysics.LinearVelocity;
            var linearImpulse = bPhysics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }

        private void SetPower()
        {
            _power = _shieldConsumptionRate + _gridMaxPower * _shieldMaintaintPower;
            if (_power <= 0 || float.IsNaN(_power)) _power = 0.0001f; // temporary definitely 100% will fix this to do - Find ThE NaN!
            Sink.Update();

            _shieldCurrentPower = Sink.CurrentInputByType(GId);
            if (Absorb > 0)
            {
                _shieldDps += Absorb;
                _effectsCleanup = true;
                ShieldBuffer -= (Absorb / Session.Enforced.Efficiency);
            }
            else if (Absorb < 0) ShieldBuffer += (Absorb / Session.Enforced.Efficiency);

            if (ShieldBuffer < 0)
            {
                _shieldDownLoop = 0;
            }
            else if (ShieldBuffer > _shieldMaxBuffer) ShieldBuffer = _shieldMaxBuffer;

            Absorb = 0f;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shieldPercent = 100f;
            var secToFull = 0;
            if (ShieldBuffer < _shieldMaxBuffer) shieldPercent = (ShieldBuffer / _shieldMaxBuffer) * 100;
            if (_shieldChargeRate > 0) secToFull = (int) ((_shieldMaxBuffer - ShieldBuffer) / _shieldChargeRate);
            stringBuilder.Append("[Shield Status] MaxHP: " + (_shieldMaxBuffer * Session.Enforced.Efficiency).ToString("N0") +
                                 "\n" +
                                 "\n[Shield HP__]: " + (ShieldBuffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                 "\n[HP Per Sec_]: " + (_shieldChargeRate * Session.Enforced.Efficiency).ToString("N0") +
                                 "\n[DPS_______]: " + (_shieldDps).ToString("N0") +
                                 "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                 "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                 "\n[Efficiency__]: " + Session.Enforced.Efficiency.ToString("0.0") +
                                 "\n[Maintenance]: " + (_gridMaxPower * _shieldMaintaintPower).ToString("0.0") + " Mw" +
                                 "\n[Availabile]: " + _gridAvailablePower.ToString("0.0") + " Mw" +
                                 "\n[Current__]: " + Sink.CurrentInputByType(GId).ToString("0.0"));
        }
        #endregion

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            const float ssTranslation = 0.00032f;
            const float lsTranslation = 0.0018f;
            const float stTranslation = 0.005f;

            _time -= 1;
            if (_animationLoop == 0) _time2 = 0;
            if (_animationLoop < 299) _time2 += 1;
            else _time2 -= 1;
            if (_count == 0) _emissiveIntensity = 2;
            if (_count < 30) _emissiveIntensity += 1;
            else _emissiveIntensity -= 1;
                
            var temp1 = MatrixD.CreateRotationY(0.05f * _time);

            MatrixD temp2;
            if (GridIsMobile && Shield.CubeGrid.GridSizeEnum == MyCubeSize.Large) temp2 = MatrixD.CreateTranslation(0, lsTranslation * _time2, 0);
            else if (GridIsMobile) temp2 = MatrixD.CreateTranslation(0, ssTranslation * _time2, 0);
            else temp2 = MatrixD.CreateTranslation(0, stTranslation * _time2, 0);

            _subpartRotor.PositionComp.LocalMatrix = temp1 * temp2;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }

        private void BlockParticleCreate()
        {
            var scale = 0f;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") scale = 10f;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") scale = 2.5f;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") scale = 20f;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] == null)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Particle #{i.ToString()} is null, creating - tick:{_tick.ToString()}");
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    if (_effects[i] == null) continue;
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = scale;
                    _effects[i].UserEmitterScale = 1f;
                }

                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
                    _effects[i].Stop();
                    _blockParticleStopped = true;
                }
            }
        }

        private void BlockParticleUpdate()
        {
            var testDist = 0d;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") testDist = 1.5d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") testDist = 1.25d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") testDist = 3.5d;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - Shield.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = Shield.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = Shield.PositionComp.WorldMatrix;
            predictedMatrix.Translation = spawnPos;
            if (_sVelSqr > 4000) predictedMatrix.Translation = spawnPos + Shield.CubeGrid.Physics.GetVelocityAtPoint(Shield.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            for (int i = 0; i < _effects.Length; i++)
                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = predictedMatrix;
                }
        }

        private void BlockParticleStop()
        {
            _blockParticleStopped = true;
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                {
                    _effects[i].Stop();
                    _effects[i].Close(false, true);
                }
            }

        }

        private void BlockParticleStart()
        {
            var scale = 0f;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") scale = 10f;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") scale = 2.5f;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") scale = 20f;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (!_effects[i].IsStopped) continue;

                MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                _effects[i].UserScale = 1f;
                _effects[i].UserRadiusMultiplier = scale;
                _effects[i].UserEmitterScale = 1f;
                BlockParticleUpdate();
            }
        }
        #endregion
      
        #region Shield Draw
        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            _enemy = enemy;

            var passiveVisible = !(_hidePassiveCheckBox.Getter(Shield).Equals(true) && !enemy);
            var activeVisible = !(_hideActiveCheckBox.Getter(Shield).Equals(true) && !enemy);

            if (!passiveVisible && !_hideShield)
            {
                _hideShield = true;
                _shellPassive.Render.UpdateRenderObject(false);
            }
            else if (passiveVisible && _hideShield)
            {
                _hideShield = false;
                _shellPassive.Render.UpdateRenderObject(true);
            }

            if (BulletCoolDown > -1) BulletCoolDown++;
            if (BulletCoolDown > 9) BulletCoolDown = -1;
            if (EntityCoolDown > -1) EntityCoolDown++;
            if (EntityCoolDown > 9) EntityCoolDown = -1;

            var impactPos = WorldImpactPosition;
            _localImpactPosition = Vector3D.NegativeInfinity;
            if (impactPos != Vector3D.NegativeInfinity & ((BulletCoolDown == -1 && EntityCoolDown == -1)))
            {
                if (EntityCoolDown == -1 && ImpactSize > 5) EntityCoolDown = 0;
                BulletCoolDown = 0;

                var cubeBlockLocalMatrix = Shield.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                _localImpactPosition = localPosition;
            }
            WorldImpactPosition = Vector3D.NegativeInfinity;

            if (Shield.IsWorking)
            {
                var prevlod = _prevLod;
                var lod = CalculateLod(_onCount);
                if (_shapeAdjusted || lod != prevlod) Icosphere.CalculateTransform(_shieldShapeMatrix, lod);
                Icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _shellPassive, _shellActive, prevlod, _shieldPercent, passiveVisible, activeVisible);
                _entityChanged = false;
            }
            if (sphereOnCamera && Shield.IsWorking) Icosphere.Draw(GetRenderId());
        }

        private int CalculateLod(int onCount)
        {
            var lod = 4;

            if (onCount > 20) lod = 2;
            else if (onCount > 10) lod = 3;

            _prevLod = lod;
            return lod;
        }

        private uint GetRenderId()
        {
            return Shield.CubeGrid.Render.GetRenderObjectID();
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
                        while (_staleGrids.TryDequeue(out grid)) lock (_webEnts) _webEnts.Remove(grid);
                        break;
                    case 1:
                        lock (_webEnts)
                        {
                            _webEntsTmp.AddRange(_webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1));
                            foreach (var webent in _webEntsTmp) _webEnts.Remove(webent.Key);
                        }
                        break;
                    case 2:
                        lock (_functionalBlocks)
                        {
                            foreach (var funcBlock in _functionalBlocks) funcBlock.SetDamageEffect(false);
                            _effectsCleanup = false;
                        }
                        break;
                    case 3:
                        {
                            FriendlyCache.Clear();
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

        #region Shield Support Blocks
        private void GetModulationInfo()
        {

            ModulatorGridComponent modComp;
            Shield.CubeGrid.Components.TryGet(out modComp);
            if (modComp != null)
            {
                var reModulate = ModulateVoxels != modComp.ModulateVoxels || ModulateGrids != modComp.ModulateGrids;
                if (reModulate) _reModulationLoop = 0;

                ModulateVoxels = modComp.ModulateVoxels;
                ModulateGrids = modComp.ModulateGrids;
            }
            else
            {
                ModulateVoxels = false;
                ModulateGrids = false;
            }
        }
        #endregion


        public override void OnAddedToScene()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                _power = 0f;
                if (MainInit) Sink.Update();
                Icosphere = null;
                _shield?.Close();
                _shellPassive?.Close();
                _shellActive?.Close();
                BlockParticleStop();
                Shield?.CubeGrid.Components.Remove(typeof(ShieldGridComponent), this);
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(EllipsoidOxyProvider);
                Session.Instance.Components.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (Session.Instance.Components.Contains(this)) Session.Instance.Components.Remove(this);
                _power = 0f;
                Icosphere = null;
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(EllipsoidOxyProvider);
                if (MainInit) Sink.Update();
                BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        #endregion
    }
}
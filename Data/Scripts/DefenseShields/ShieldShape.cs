using System;
using DefenseShields.Support;
using VRage.Game;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Shield Shape
        public void ResetShape(bool background, bool newShape = false)
        {
            if (Session.Enforced.Debug == 1) Log.Line($"ResetShape: newShape {newShape} - Offline:{ShieldOffline} - offCnt:{_offlineCnt} - blockChanged:{_blockEvent} - functional:{_functionalEvent} - Sleeping:{ShieldWasSleeping} - Suspend:{Suspended} - EWorking:{ShieldComp.EmittersWorking} - ELoS:{ShieldComp.EmittersLos} - ShieldId [{Shield.EntityId}]");

            if (newShape)
            {
                UpdateSubGrids(true);
                BlockMonitor();
                if (_blockEvent) BlockChanged(background);
                if (_shapeEvent) CheckExtents(background);
                return;
            }

            if (GridIsMobile)
            {
                _updateMobileShape = true;
                MobileUpdate();
            }
            else
            {
                UpdateDimensions = true;
                if (UpdateDimensions) RefreshDimensions();
            }
            Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
        }

        public void CreateHalfExtents()
        {
            var myAabb = Shield.CubeGrid.PositionComp.LocalAABB;
            var shieldGrid = Shield.CubeGrid;
            var expandedAabb = myAabb;
            foreach (var grid in ShieldComp.GetSubGrids)
            {
                if (grid != null && grid != shieldGrid)
                {
                    var shieldMatrix = shieldGrid.WorldMatrixNormalizedInv;
                    var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                    var gOriBBoxD = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, gQuaternion);
                    gOriBBoxD.Transform(shieldMatrix);
                    expandedAabb.Include(gOriBBoxD.GetAABB());
                }
            }

            if (SphereFit || FortifyShield)
            {
                var extend = ExtendFit ? 2 : 1;
                var fortify = FortifyShield ? 3 : 1;
                var size = expandedAabb.HalfExtents.Max() * fortify;
                var scaler = 4;
                if (shieldGrid.GridSizeEnum == MyCubeSize.Small && !ExtendFit) scaler = 5;
                var vectorSize = new Vector3D(size, size, size);
                var fudge = shieldGrid.GridSize * scaler * extend;
                var extentsDiff = _gridHalfExtents.LengthSquared() - vectorSize.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || _gridHalfExtents == Vector3D.Zero || !fudge.Equals(_shieldFudge)) _gridHalfExtents = vectorSize;
                _shieldFudge = fudge;
            }
            else
            {
                _shieldFudge = 0f;
                var extentsDiff = _gridHalfExtents.LengthSquared() - expandedAabb.HalfExtents.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || _gridHalfExtents == Vector3D.Zero) _gridHalfExtents = expandedAabb.HalfExtents;
            }
        }

        private void GetShapeAdjust()
        {
            if (SphereFit || FortifyShield) _ellipsoidAdjust = 1f;
            else if (!ExtendFit) _ellipsoidAdjust = UtilsStatic.CreateNormalFit(Shield, _gridHalfExtents);
            else _ellipsoidAdjust = UtilsStatic.CreateExtendedFit(Shield, _gridHalfExtents);
        }

        private void MobileUpdate()
        {
            ShieldComp.ShieldVelocitySqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
            _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
            if (ShieldComp.ShieldVelocitySqr > 0.00001 || _sAvelSqr > 0.00001 || ComingOnline)
            {
                ShieldComp.GridIsMoving = true;
                if (FortifyShield && Math.Sqrt(ShieldComp.ShieldVelocitySqr) > 15)
                {
                    FitChanged = true;
                    FortifyShield = false;
                }
            }
            else ShieldComp.GridIsMoving = false;

            _shapeChanged = !_ellipsoidAdjust.Equals(_oldEllipsoidAdjust) || !_gridHalfExtents.Equals(_oldGridHalfExtents) || _updateMobileShape;
            _entityChanged = Shield.CubeGrid.Physics.IsMoving || ComingOnline || _shapeChanged;
            _oldGridHalfExtents = _gridHalfExtents;
            _oldEllipsoidAdjust = _ellipsoidAdjust;
            if (_entityChanged || BoundingRange <= 0) CreateShieldShape();
        }

        private void CreateShieldShape()
        {
            if (GridIsMobile)
            {
                _updateMobileShape = false;
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_shapeChanged) CreateMobileShape();
                DetectionMatrix = _shieldShapeMatrix * _shieldGridMatrix;
                DetectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
            }
            else
            {
                var emitter = ShieldComp.StationEmitter.Emitter;
                _shieldGridMatrix = emitter.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(Width, Height, Depth));
                _shieldShapeMatrix = MatrixD.Rescale(emitter.LocalMatrix, new Vector3D(Width, Height, Depth));
                ShieldSize = DetectionMatrix.Scale;
                DetectionCenter = emitter.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(emitter.CubeGrid.WorldMatrix);
            }

            SOriBBoxD = new MyOrientedBoundingBoxD(DetectionCenter, ShieldSize, _sQuaternion);
            _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
            ShieldSphere = new BoundingSphereD(Shield.PositionComp.LocalAABB.Center, ShieldSize.AbsMax()) { Center = DetectionCenter };

            if (_shapeChanged)
            {
                EllipsoidSa.Update(DetectMatrixOutside.Scale.X, DetectMatrixOutside.Scale.Y, DetectMatrixOutside.Scale.Z);
                BoundingRange = ShieldSize.AbsMax();
                _ellipsoidSurfaceArea = EllipsoidSa.Surface;
                ShieldComp.ShieldVolume = DetectMatrixOutside.Scale.Volume;
                ShieldComp.CheckEmitters = true;
                if (Session.Enforced.Debug == 1) Log.Line($"CreateShape: shapeChanged - GridMobile:{GridIsMobile} - ShieldId [{Shield.EntityId}]");
            }
            if (!ShieldWasLowered) SetShieldShape();
        }

        private void CreateMobileShape()
        {
            var shieldSize = _gridHalfExtents * _ellipsoidAdjust + _shieldFudge;
            ShieldSize = shieldSize;
            var mobileMatrix = MatrixD.CreateScale(shieldSize);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            _shellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
            _shellActive.PositionComp.LocalMatrix = Matrix.Zero;
            ShieldEnt.PositionComp.LocalMatrix = Matrix.Zero;

            _shellPassive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            _shellActive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            ShieldEnt.PositionComp.LocalMatrix = _shieldShapeMatrix;
            ShieldEnt.PositionComp.LocalAABB = _shieldAabb;

            MatrixD matrix;
            if (!GridIsMobile)
            {
                matrix = _shieldShapeMatrix * ShieldComp.StationEmitter.Emitter.WorldMatrix;
                ShieldEnt.PositionComp.SetWorldMatrix(matrix);
                ShieldEnt.PositionComp.SetPosition(DetectionCenter);
            }
            else
            {
                matrix = _shieldShapeMatrix * Shield.WorldMatrix;
                ShieldEnt.PositionComp.SetWorldMatrix(matrix);
                ShieldEnt.PositionComp.SetPosition(DetectionCenter);
            }
        }

        private void RefreshDimensions()
        {
            UpdateDimensions = false;
            CreateShieldShape();
            Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
            _shapeChanged = true;
        }
        #endregion
    }
}

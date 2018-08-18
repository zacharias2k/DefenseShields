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
            if (Session.Enforced.Debug >= 2) Log.Line($"ResetShape: Mobile:{GridIsMobile} - Mode:{ShieldMode} - newShape:{newShape} - Offline:{!DsState.State.Online} - offCnt:{_offlineCnt} - blockChanged:{_blockEvent} - functional:{_functionalEvent} - Sleeping:{DsState.State.Sleeping} - Suspend:{DsState.State.Suspended} - EWorking:{ShieldComp.EmittersWorking} - ShieldId [{Shield.EntityId}]");

            if (newShape)
            {
                UpdateSubGrids(true);
                BlockMonitor();
                if (_blockEvent) BlockChanged(background);
                if (_shapeEvent) CheckExtents(background);
                if (GridIsMobile) _updateMobileShape = true;
                return;
            }

            if (GridIsMobile)
            {
                //_updateMobileShape = true;
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
            if (ShieldComp.GetSubGrids.Count > 1)
            {
                foreach (var grid in ShieldComp.GetSubGrids)
                {
                    if (grid == null || grid == shieldGrid) continue;
                    var shieldMatrix = shieldGrid.WorldMatrixNormalizedInv;
                    var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                    var gOriBBoxD = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, gQuaternion);
                    gOriBBoxD.Transform(shieldMatrix);
                    expandedAabb.Include(gOriBBoxD.GetAABB());
                }
            }

            if (DsSet.Settings.SphereFit || DsSet.Settings.FortifyShield)
            {
                var extend = DsSet.Settings.ExtendFit ? 2 : 1;
                var fortify = DsSet.Settings.FortifyShield ? 3 : 1;
                var size = expandedAabb.HalfExtents.Max() * fortify;
                var scaler = 4;
                if (shieldGrid.GridSizeEnum == MyCubeSize.Small && !DsSet.Settings.ExtendFit) scaler = 5;
                var vectorSize = new Vector3D(size, size, size);
                var fudge = shieldGrid.GridSize * scaler * extend;
                var extentsDiff = DsState.State.GridHalfExtents.LengthSquared() - vectorSize.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || DsState.State.GridHalfExtents == Vector3D.Zero || !fudge.Equals(DsState.State.ShieldFudge)) DsState.State.GridHalfExtents = vectorSize;
                DsState.State.ShieldFudge = fudge;
            }
            else
            {
                DsState.State.ShieldFudge = 0f;
                var extentsDiff = DsState.State.GridHalfExtents.LengthSquared() - expandedAabb.HalfExtents.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || DsState.State.GridHalfExtents == Vector3D.Zero) DsState.State.GridHalfExtents = expandedAabb.HalfExtents;
            }
        }

        private void GetShapeAdjust()
        {
            if (DsSet.Settings.SphereFit || DsSet.Settings.FortifyShield) DsState.State.EllipsoidAdjust = 1f;
            else if (!DsSet.Settings.ExtendFit) DsState.State.EllipsoidAdjust = UtilsStatic.CreateNormalFit(Shield, DsState.State.GridHalfExtents);
            else DsState.State.EllipsoidAdjust = UtilsStatic.CreateExtendedFit(Shield, DsState.State.GridHalfExtents);
        }

        private void MobileUpdate()
        {
            ShieldComp.ShieldVelocitySqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
            _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
            if (ShieldComp.ShieldVelocitySqr > 0.00001 || _sAvelSqr > 0.00001 || ComingOnline)
            {
                ShieldComp.GridIsMoving = true;
                if (DsSet.Settings.FortifyShield && Math.Sqrt(ShieldComp.ShieldVelocitySqr) > 15)
                {
                    FitChanged = true;
                    DsSet.Settings.FortifyShield = false;
                }
            }
            else ShieldComp.GridIsMoving = false;

            _shapeChanged = !DsState.State.EllipsoidAdjust.Equals(_oldEllipsoidAdjust) || !DsState.State.GridHalfExtents.Equals(_oldGridHalfExtents) || !DsState.State.ShieldFudge.Equals(_oldShieldFudge) || _updateMobileShape;
            _entityChanged = Shield.CubeGrid.Physics.IsMoving || ComingOnline || _shapeChanged;
            _oldGridHalfExtents = DsState.State.GridHalfExtents;
            _oldEllipsoidAdjust = DsState.State.EllipsoidAdjust;
            _oldShieldFudge = DsState.State.ShieldFudge;
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
                ShieldSphere = new BoundingSphereD(Shield.PositionComp.LocalAABB.Center, ShieldSize.AbsMax()) { Center = DetectionCenter };
            }
            else
            {
                var emitter = ShieldComp.StationEmitter.Emitter;
                _shieldGridMatrix = emitter.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(DsSet.Settings.Width, DsSet.Settings.Height, DsSet.Settings.Depth));
                _shieldShapeMatrix = MatrixD.Rescale(emitter.LocalMatrix, new Vector3D(DsSet.Settings.Width, DsSet.Settings.Height, DsSet.Settings.Depth));
                ShieldSize = DetectionMatrix.Scale;
                DetectionCenter = emitter.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(emitter.CubeGrid.WorldMatrix);
                ShieldSphere = new BoundingSphereD(emitter.PositionComp.LocalAABB.Center, ShieldSize.AbsMax()) { Center = DetectionCenter };
            }

            SOriBBoxD = new MyOrientedBoundingBoxD(DetectionCenter, ShieldSize, _sQuaternion);
            _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);

            if (_shapeChanged)
            {
                EllipsoidSa.Update(DetectMatrixOutside.Scale.X, DetectMatrixOutside.Scale.Y, DetectMatrixOutside.Scale.Z);
                BoundingRange = ShieldSize.AbsMax();
                _ellipsoidSurfaceArea = EllipsoidSa.Surface;
                if (Session.IsServer)
                {
                    ShieldChangeState(false);
                    ShieldComp.ShieldVolume = DetectMatrixOutside.Scale.Volume;
                    ShieldComp.CheckEmitters = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"CreateShape: shapeChanged - GridMobile:{GridIsMobile} - ShieldId [{Shield.EntityId}]");
            }
            if (!DsState.State.Lowered) SetShieldShape();
        }

        private void CreateMobileShape()
        {
            var shieldSize = DsState.State.GridHalfExtents * DsState.State.EllipsoidAdjust + DsState.State.ShieldFudge;
            ShieldSize = shieldSize;
            var mobileMatrix = MatrixD.CreateScale(shieldSize);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            if (!Session.DedicatedServer)
            {
                _shellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
                _shellActive.PositionComp.LocalMatrix = Matrix.Zero;
                _shellPassive.PositionComp.LocalMatrix = _shieldShapeMatrix;
                _shellActive.PositionComp.LocalMatrix = _shieldShapeMatrix;
            }

            ShieldEnt.PositionComp.LocalMatrix = Matrix.Zero;
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
            _shapeChanged = true;
            CreateShieldShape();
            Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
        }
        #endregion
    }
}

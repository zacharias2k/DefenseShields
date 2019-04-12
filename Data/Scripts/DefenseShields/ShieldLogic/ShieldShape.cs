using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace DefenseSystems
{
    using System;
    using Support;
    using VRage.Game;
    using VRageMath;

    public partial class Controllers
    {
        #region Shield Shape
        public void ResetShape(bool background, bool newShape = false)
        {
            if (DsState.State.ProtectMode == 2) return;

            if (Session.Enforced.Debug == 3) Log.Line($"ResetShape: Mobile:{ShieldIsMobile} - Mode:{ShieldMode}/{DsState.State.Mode} - newShape:{newShape} - Offline:{!DsState.State.Online} - Sleeping:{DsState.State.Sleeping} - Suspend:{DsState.State.Suspended} - ELos:{Bus.EmitterLos} - ControllerId [{Controller.EntityId}]");

            if (newShape)
            {
                Bus.SubGridDetect(LocalGrid, true);
                Bus.BlockMonitor();
                if (ShapeEvent) CheckExtents();
                if (ShieldIsMobile) _updateMobileShape = true;
                return;
            }

            if (ShieldIsMobile) MobileUpdate();
            else
            {
                UpdateDimensions = true;
                if (UpdateDimensions) RefreshDimensions();
            }
        }

        public void MobileUpdate()
        {
            var checkForNewCenter = Bus.Spine.PositionComp.WorldVolume.Center;
            if (!checkForNewCenter.Equals(MyGridCenter, 1e-4))
            {
                Bus.GridIsMoving = true;
                MyGridCenter = checkForNewCenter;
            }
            else
            {
                Bus.GridIsMoving = false;
            }

            if (Bus.GridIsMoving || _comingOnline)
            {
                if (DsSet.Settings.FortifyShield && Bus.Spine.Physics.LinearVelocity.Length() > 15)
                {
                    FitChanged = true;
                    DsSet.Settings.FortifyShield = false;
                }
            }

            _shapeChanged = _halfExtentsChanged || !DsState.State.EllipsoidAdjust.Equals(_oldEllipsoidAdjust) || !DsState.State.ShieldFudge.Equals(_oldShieldFudge) || _updateMobileShape;
            _entityChanged = Bus.GridIsMoving || _comingOnline || _shapeChanged;

            _halfExtentsChanged = false;
            _oldEllipsoidAdjust = DsState.State.EllipsoidAdjust;
            _oldShieldFudge = DsState.State.ShieldFudge;
            if (_entityChanged || BoundingRange <= 0) CreateShieldShape();
            if (_tick300) CreateHalfExtents();

            if (Session.Instance.LogStats)
            {
                if (Bus.GridIsMoving) Session.Instance.Perf.Moving();
                if (_shapeChanged) Session.Instance.Perf.ShapeChanged();
            }
        }

        public void RefreshDimensions()
        {
            UpdateDimensions = false;
            _shapeChanged = true;
            CreateShieldShape();
        }

        public void CreateHalfExtents()
        {
            _oldGridHalfExtents = DsState.State.GridHalfExtents;
            var myAabb = Bus.Spine.PositionComp.LocalAABB;
            var shieldGrid = Bus.Spine;
            var expandedAabb = myAabb;
            if (Bus.SubGrids.Count > 1)
            {
                foreach (var grid in Bus.SubGrids)
                {
                    if (grid == shieldGrid) continue;
                    var shieldMatrix = shieldGrid.PositionComp.WorldMatrixNormalizedInv;
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
                var blockHalfSize = Bus.Spine.GridSize * 0.5;
                DsState.State.ShieldFudge = 0f;
                var extentsDiff = DsState.State.GridHalfExtents.LengthSquared() - expandedAabb.HalfExtents.LengthSquared();
                var overThreshold = extentsDiff < -blockHalfSize || extentsDiff > blockHalfSize;
                if (overThreshold || DsState.State.GridHalfExtents == Vector3D.Zero) DsState.State.GridHalfExtents = expandedAabb.HalfExtents;
            }
            _halfExtentsChanged = !DsState.State.GridHalfExtents.Equals(_oldGridHalfExtents);
            if (_halfExtentsChanged || SettingsUpdated)
            {
                AdjustShape = true;
            }
        }

        private void ReAdjustShape(bool backGround)
        {
            if (backGround) GetShapeAdjust();
            else GetShapeAdjust();
            AdjustShape = false;
        }

        private void GetShapeAdjust()
        {
            if (DsSet.Settings.SphereFit || DsSet.Settings.FortifyShield) DsState.State.EllipsoidAdjust = 1f;
            else if (!DsSet.Settings.ExtendFit) DsState.State.EllipsoidAdjust = UtilsStatic.CreateNormalFit(Controller, DsState.State.GridHalfExtents);
            else DsState.State.EllipsoidAdjust = UtilsStatic.CreateExtendedFit(Controller, DsState.State.GridHalfExtents);
        }

        private void CheckExtents()
        {
            FitChanged = false;
            ShapeEvent = false;
            if (!_isServer || !ShieldIsMobile) return;
            CreateHalfExtents();
        }

        internal void CreateShieldShape()
        {
            if (ShieldIsMobile)
            {
                _updateMobileShape = false;
                if (_shapeChanged) CreateMobileShape();
                DetectionMatrix = ShieldShapeMatrix * Bus.Spine.WorldMatrix;
                DetectionCenter = MyGridCenter;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Bus.Spine.WorldMatrix);
                ShieldSphere.Center = DetectionCenter;
                ShieldSphere.Radius = ShieldSize.AbsMax();
            }
            else
            {
                IMyUpgradeModule emitter;
                if (_isServer) emitter = Bus.ActiveEmitter.Emitter;
                else emitter = (IMyUpgradeModule)MyEntities.GetEntityById(DsState.State.ActiveEmitterId, true);

                if (emitter == null)
                {
                    UpdateDimensions = true;
                    return;
                }

                var width = DsSet.Settings.Width;
                var height = DsSet.Settings.Height;
                var depth = DsSet.Settings.Depth;

                var wOffset = DsSet.Settings.ShieldOffset.X;
                var hOffset = DsSet.Settings.ShieldOffset.Y;
                var dOffset = DsSet.Settings.ShieldOffset.Z;

                var blockGridPosMeters = new Vector3D(emitter.Position) * LocalGrid.GridSize;
                var localOffsetMeters = new Vector3D(wOffset, hOffset, dOffset) * LocalGrid.GridSize; 
                var localOffsetPosMeters = localOffsetMeters + blockGridPosMeters; 
                var emitterCenter = emitter.PositionComp.GetPosition();
                var offsetLMatrix = Matrix.CreateWorld(localOffsetPosMeters, Vector3D.Forward, Vector3D.Up);

                var worldOffset = Vector3D.TransformNormal(localOffsetMeters, LocalGrid.WorldMatrix); 
                var translationInWorldSpace = emitterCenter + worldOffset;

                OffsetEmitterWMatrix = MatrixD.CreateWorld(translationInWorldSpace, LocalGrid.WorldMatrix.Forward, LocalGrid.WorldMatrix.Up);

                DetectionCenter = OffsetEmitterWMatrix.Translation;

                var halfDistToCenter = 600 - Vector3D.Distance(DetectionCenter, emitterCenter);
                var vectorScale = new Vector3D(MathHelper.Clamp(width, 30, halfDistToCenter), MathHelper.Clamp(height, 30, halfDistToCenter), MathHelper.Clamp(depth, 30, halfDistToCenter));

                DetectionMatrix = MatrixD.Rescale(OffsetEmitterWMatrix, vectorScale);
                ShieldShapeMatrix = MatrixD.Rescale(offsetLMatrix, vectorScale);

                ShieldSize = DetectionMatrix.Scale;

                _sQuaternion = Quaternion.CreateFromRotationMatrix(OffsetEmitterWMatrix);
                ShieldSphere.Center = DetectionCenter;
                ShieldSphere.Radius = ShieldSize.AbsMax();
            }

            ShieldSphere3K.Center = DetectionCenter;
            WebSphere.Center = DetectionCenter;

            SOriBBoxD.Center = DetectionCenter;
            SOriBBoxD.Orientation = _sQuaternion;
            if (_shapeChanged)
            {
                SOriBBoxD.HalfExtent = ShieldSize;
                ShieldAabbScaled.Min = ShieldSize;
                ShieldAabbScaled.Max = -ShieldSize;
                _ellipsoidSa.Update(DetectMatrixOutside.Scale.X, DetectMatrixOutside.Scale.Y, DetectMatrixOutside.Scale.Z);
                BoundingRange = ShieldSize.AbsMax();

                ShieldSphere3K.Radius = BoundingRange + 3000;
                WebSphere.Radius = BoundingRange + 7;

                _ellipsoidSurfaceArea = _ellipsoidSa.Surface;
                EllipsoidVolume = 1.333333 * Math.PI * DetectMatrixOutside.Scale.X * DetectMatrixOutside.Scale.Y * DetectMatrixOutside.Scale.Z;
                _shieldVol = DetectMatrixOutside.Scale.Volume;
                if (_isServer)
                {
                    ProtChangedState();
                    Bus.ShieldVolume = DetectMatrixOutside.Scale.Volume;
                }
            }

            if (_shapeChanged)
            {
                if (!_isDedicated)
                {
                    _shellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
                    _shellActive.PositionComp.LocalMatrix = Matrix.Zero;
                    _shellPassive.PositionComp.LocalMatrix = ShieldShapeMatrix;
                    _shellActive.PositionComp.LocalMatrix = ShieldShapeMatrix;
                }
                ShieldEnt.PositionComp.LocalMatrix = Matrix.Zero;
                ShieldEnt.PositionComp.LocalMatrix = ShieldShapeMatrix;
                ShieldEnt.PositionComp.LocalAABB = ShieldAabbScaled;
            }
            ShieldEnt.PositionComp.SetPosition(DetectionCenter);

            BoundingBoxD.CreateFromSphere(ref WebSphere, out WebBox);
            BoundingBoxD.CreateFromSphere(ref ShieldSphere3K, out ShieldBox3K);
        }

        private void CreateMobileShape()
        {
            ShieldSize = (DsState.State.GridHalfExtents * DsState.State.EllipsoidAdjust) + DsState.State.ShieldFudge;
            var mobileMatrix = MatrixD.Rescale(MatrixD.Identity, ShieldSize);
            mobileMatrix.Translation = Bus.Spine.PositionComp.LocalVolume.Center;
            ShieldShapeMatrix = mobileMatrix;
        }
        #endregion
    }
}

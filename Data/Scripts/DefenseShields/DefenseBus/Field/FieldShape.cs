using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        #region Shield Shape

        public void ShapeCheck(bool blockAdded)
        {
            var tick = Session.Instance.Tick;
            ShapeEvent = true;

            LosCheckTick = tick + 1800;
            if (blockAdded) ShapeTick = tick + 300;
            else ShapeTick = tick + 1800;
        }

        public void ResetShape(bool background, bool newShape = false)
        {
            var a = Bus.ActiveController;
            if (a.State.Value.ProtectMode == 2) return;

            if (newShape)
            {
                Bus.SubGridDetect(Bus.Spine, true);
                Bus.BlockMonitor();
                if (ShapeEvent) CheckExtents();
                if (ShieldIsMobile) UpdateMobileShape = true;
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
            var a = Bus.ActiveController;
            var checkForNewCenter = Bus.Spine.PositionComp.WorldVolume.Center;
            if (!checkForNewCenter.Equals(MyGridCenter, 1e-4))
            {
                Bus.SpineIsMoving = true;
                MyGridCenter = checkForNewCenter;
            }
            else
            {
                Bus.SpineIsMoving = false;
            }

            if (Bus.SpineIsMoving || UpdateMobileShape)
            {
                if (a.Set.Value.FortifyShield && Bus.Spine.Physics.LinearVelocity.Length() > 15)
                {
                    FitChanged = true;
                    a.Set.Value.FortifyShield = false;
                }
            }

            _shapeChanged = _halfExtentsChanged || !a.State.Value.EllipsoidAdjust.Equals(_oldEllipsoidAdjust) || !a.State.Value.ShieldFudge.Equals(_oldShieldFudge) || UpdateMobileShape;
            _entityChanged = Bus.SpineIsMoving || UpdateMobileShape || _shapeChanged;

            _halfExtentsChanged = false;
            _oldEllipsoidAdjust = a.State.Value.EllipsoidAdjust;
            _oldShieldFudge = a.State.Value.ShieldFudge;
            if (_entityChanged || BoundingRange <= 0) CreateShieldShape();
            if (Session.Instance.Tick300) CreateHalfExtents();

            if (Session.Instance.LogStats)
            {
                if (Bus.SpineIsMoving) Session.Instance.Perf.Moving();
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
            var a = Bus.ActiveController;
            _oldGridHalfExtents = a.State.Value.GridHalfExtents;
            var myAabb = Bus.Spine.PositionComp.LocalAABB;
            var expandedAabb = myAabb;
            if (Bus.SubGrids.Count > 1)
            {
                foreach (var grid in Bus.SubGrids)
                {
                    if (grid == Bus.Spine) continue;
                    var shieldMatrix = Bus.Spine.PositionComp.WorldMatrixNormalizedInv;
                    var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                    var gOriBBoxD = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, gQuaternion);
                    gOriBBoxD.Transform(shieldMatrix);
                    expandedAabb.Include(gOriBBoxD.GetAABB());
                }
            }

            if (a.Set.Value.SphereFit || a.Set.Value.FortifyShield)
            {
                var extend = a.Set.Value.ExtendFit ? 2 : 1;
                var fortify = a.Set.Value.FortifyShield ? 3 : 1;
                var size = expandedAabb.HalfExtents.Max() * fortify;
                var scaler = 4;
                if (Bus.Spine.GridSizeEnum == MyCubeSize.Small && !a.Set.Value.ExtendFit) scaler = 5;
                var vectorSize = new Vector3D(size, size, size);
                var fudge = Bus.Spine.GridSize * scaler * extend;
                var extentsDiff = a.State.Value.GridHalfExtents.LengthSquared() - vectorSize.LengthSquared();
                if (extentsDiff < -1 || extentsDiff > 1 || a.State.Value.GridHalfExtents == Vector3D.Zero || !fudge.Equals(a.State.Value.ShieldFudge)) a.State.Value.GridHalfExtents = vectorSize;
                a.State.Value.ShieldFudge = fudge;
            }
            else
            {
                var blockHalfSize = Bus.Spine.GridSize * 0.5;
                a.State.Value.ShieldFudge = 0f;
                var extentsDiff = a.State.Value.GridHalfExtents.LengthSquared() - expandedAabb.HalfExtents.LengthSquared();
                var overThreshold = extentsDiff < -blockHalfSize || extentsDiff > blockHalfSize;
                if (overThreshold || a.State.Value.GridHalfExtents == Vector3D.Zero) a.State.Value.GridHalfExtents = expandedAabb.HalfExtents;
            }
            _halfExtentsChanged = !a.State.Value.GridHalfExtents.Equals(_oldGridHalfExtents);
            if (_halfExtentsChanged)
            {
                AdjustShape = true;
            }
        }

        internal void ReAdjustShape(bool backGround)
        {
            if (backGround) GetShapeAdjust();
            else GetShapeAdjust();
            AdjustShape = false;
        }

        private void GetShapeAdjust()
        {
            var a = Bus.ActiveController;
            if (a.Set.Value.SphereFit || a.Set.Value.FortifyShield) a.State.Value.EllipsoidAdjust = 1f;
            else if (!a.Set.Value.ExtendFit) a.State.Value.EllipsoidAdjust = UtilsStatic.CreateNormalFit(a.Controller, a.State.Value.GridHalfExtents);
            else a.State.Value.EllipsoidAdjust = UtilsStatic.CreateExtendedFit(a.Controller, a.State.Value.GridHalfExtents);
        }

        internal void CheckExtents()
        {
            FitChanged = false;
            ShapeEvent = false;
            if (!_isServer || !ShieldIsMobile) return;
            CreateHalfExtents();
        }

        internal void CreateShieldShape()
        {
            var a = Bus.ActiveController;
            if (ShieldIsMobile)
            {
                UpdateMobileShape = false;
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
                else emitter = (IMyUpgradeModule)MyEntities.GetEntityById(a.State.Value.ActiveEmitterId, true);

                if (emitter == null)
                {
                    UpdateDimensions = true;
                    return;
                }

                var width = a.Set.Value.Width;
                var height = a.Set.Value.Height;
                var depth = a.Set.Value.Depth;

                var wOffset = a.Set.Value.ShieldOffset.X;
                var hOffset = a.Set.Value.ShieldOffset.Y;
                var dOffset = a.Set.Value.ShieldOffset.Z;

                var blockGridPosMeters = new Vector3D(emitter.Position) * Bus.Spine.GridSize;
                var localOffsetMeters = new Vector3D(wOffset, hOffset, dOffset) * Bus.Spine.GridSize;
                var localOffsetPosMeters = localOffsetMeters + blockGridPosMeters;
                var emitterCenter = emitter.PositionComp.GetPosition();
                var offsetLMatrix = Matrix.CreateWorld(localOffsetPosMeters, Vector3D.Forward, Vector3D.Up);

                var worldOffset = Vector3D.TransformNormal(localOffsetMeters, Bus.Spine.WorldMatrix);
                var translationInWorldSpace = emitterCenter + worldOffset;

                OffsetEmitterWMatrix = MatrixD.CreateWorld(translationInWorldSpace, Bus.Spine.WorldMatrix.Forward, Bus.Spine.WorldMatrix.Up);

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
                    Bus.ActiveController.ProtChangedState();
                    ShieldVolume = DetectMatrixOutside.Scale.Volume;
                }
            }

            if (_shapeChanged)
            {
                if (!_isDedicated)
                {
                    ShellPassive.PositionComp.LocalMatrix = Matrix.Zero;  // Bug - Cannot just change X coord, so I reset first.
                    ShellActive.PositionComp.LocalMatrix = Matrix.Zero;
                    ShellPassive.PositionComp.LocalMatrix = ShieldShapeMatrix;
                    ShellActive.PositionComp.LocalMatrix = ShieldShapeMatrix;
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
            var c = Bus.ActiveController;
            ShieldSize = (c.State.Value.GridHalfExtents * c.State.Value.EllipsoidAdjust) + c.State.Value.ShieldFudge;
            var mobileMatrix = MatrixD.Rescale(MatrixD.Identity, ShieldSize);
            mobileMatrix.Translation = Bus.Spine.PositionComp.LocalVolume.Center;
            ShieldShapeMatrix = mobileMatrix;
        }
        #endregion
    }
}

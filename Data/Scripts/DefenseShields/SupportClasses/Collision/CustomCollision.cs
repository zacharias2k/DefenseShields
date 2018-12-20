using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields.Support
{
    internal static class CustomCollision
    {
        private static readonly Vector3D[] GridCorners = new Vector3D[8];
        private static readonly Vector3D[] GridPoints = new Vector3D[9];

        public static Vector3D? MissileIntersect(DefenseShields ds, MyEntity missile, MatrixD detectMatrix, MatrixD detectMatrixInv)
        {
            var missileVel = missile.Physics.LinearVelocity;
            var missileCenter = missile.PositionComp.WorldVolume.Center;
            const float gameSteps = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 2;
            var velStepSize = missileVel * gameSteps;
            var futureCenter = missileCenter + velStepSize;
            var testDir = Vector3D.Normalize(missileCenter - futureCenter);
            var ellipsoid = IntersectEllipsoid(ds.DetectMatrixOutsideInv, ds.DetectionMatrix, new RayD(futureCenter, -testDir));
            if (ellipsoid == null || ellipsoid > 0) return null;
            return futureCenter; 
        }

        /*
        public static Vector3D? MissileIntersect(DefenseShields ds, MyEntity missile, MatrixD detectMatrix, MatrixD detectMatrixInv)
        {
            var missileVel = missile.Physics.LinearVelocity;
            var velStepSize = missileVel * (MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 2);
            var missileCenter = missile.PositionComp.WorldVolume.Center;
            var inflatedSphere = new BoundingSphereD(missileCenter, velStepSize.Length());
            var wDir = detectMatrix.Translation - inflatedSphere.Center;
            var wLen = wDir.Length();
            var wTest = inflatedSphere.Center + wDir / wLen * Math.Min(wLen, inflatedSphere.Radius);
            var intersect = Vector3D.Transform(wTest, detectMatrixInv).LengthSquared() <= 1;
            Vector3D? hitPos = null;

            if (intersect)
            {
                const float gameSecond = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 60;
                var line = new LineD(missileCenter + -missileVel * gameSecond, missileCenter + missileVel * gameSecond);
                var obbIntersect = ds.SOriBBoxD.Intersects(ref line);
                if (obbIntersect.HasValue)
                {
                    var testDir = line.From - line.To;
                    testDir.Normalize();
                    hitPos = line.From + testDir * -obbIntersect.Value;
                }
            }
            return hitPos;
        }
        */
        public static bool MissileNoIntersect(MyEntity missile, MatrixD detectMatrix, MatrixD detectMatrixInv)
        {
            var missileVel = missile.Physics.LinearVelocity;
            var missileCenter = missile.PositionComp.WorldVolume.Center;
            var leaving = Vector3D.Transform(missileCenter + -missileVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 2, detectMatrixInv).LengthSquared() <= 1;
            return leaving;
        }

        private static BoundingSphereD _normSphere = new BoundingSphereD(Vector3.Zero, 1f);
        private static RayD _kRay = new RayD(Vector3D.Zero, Vector3D.Forward);
        public static float? IntersectEllipsoid(MatrixD ellipsoidMatrixInv, MatrixD ellipsoidMatrix, RayD ray)
        {
            var krayPos = Vector3D.Transform(ray.Position, ellipsoidMatrixInv);
            var krayDir = Vector3D.Normalize(Vector3D.TransformNormal(ray.Direction, ellipsoidMatrixInv));

            _kRay.Direction = krayDir;
            _kRay.Position = krayPos;
            var nullDist = _normSphere.Intersects(_kRay);
            if (!nullDist.HasValue) return null;

            var hitPos = krayPos + krayDir * -nullDist.Value;
            var worldHitPos = Vector3D.Transform(hitPos, ellipsoidMatrix);
            return Vector3.Distance(worldHitPos, ray.Position);
        }

        public static bool RayIntersectsTriangle(Vector3D rayOrigin, Vector3D rayVector, Vector3D v0, Vector3D v1, Vector3D v2, Vector3D outIntersectionPoint)
        {
            const double epsilon = 0.0000001;
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var h = rayVector.Cross(edge2);
            var a = edge1.Dot(h);
            if (a > -epsilon && a < epsilon) return false;

            var f = 1 / a;
            var s = rayOrigin - v0;
            var u = f * (s.Dot(h));
            if (u < 0.0 || u > 1.0) return false;

            var q = s.Cross(edge1);
            var v = f * rayVector.Dot(q);
            if (v < 0.0 || u + v > 1.0) return false;
            
            // At this stage we can compute t to find out where the intersection point is on the line.
            var t = f * edge2.Dot(q);
            if (t > epsilon) // ray intersection
            {
                outIntersectionPoint = rayOrigin + rayVector * t;
                return true;
            }
            // This means that there is a line intersection but not a ray intersection.
            return false;
        }

        public static void ShieldX2PointsInside(Vector3D[] shield1Verts, MatrixD shield1MatrixInv, Vector3D[] shield2Verts, MatrixD shield2MatrixInv, List<Vector3D> insidePoints)
        {
            for (int i = 0; i < 642; i++) if (Vector3D.Transform(shield1Verts[i], shield2MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield1Verts[i]); 
            for (int i = 0; i < 642; i++) if (Vector3D.Transform(shield2Verts[i], shield1MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield2Verts[i]);
        }

        public static void ClientShieldX2PointsInside(Vector3D[] shield1Verts, MatrixD shield1MatrixInv, Vector3D[] shield2Verts, MatrixD shield2MatrixInv, List<Vector3D> insidePoints)
        {
            for (int i = 0; i < 162; i++) if (Vector3D.Transform(shield1Verts[i], shield2MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield1Verts[i]);
            for (int i = 0; i < 162; i++) if (Vector3D.Transform(shield2Verts[i], shield1MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield2Verts[i]);
        }

        public static void VoxelCollision(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase, MyOrientedBoundingBoxD bOriBBoxD)
        {
            var sVel = shieldGrid.Physics.LinearVelocity;
            var sVelSqr = sVel.LengthSquared();
            var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
            var voxelSphere = voxelBase.PositionComp.WorldVolume;
            //var obbSphere = new BoundingSphereD(bOriBBoxD.Center, bOriBBoxD.HalfExtent.Max());
            var lerpedVerts = new Vector3D[642];
            var shieldGridMass = shieldGrid.Physics.Mass;
            for (int i = 0; i < 642; i++)
            {
                var newVert = Vector3D.Lerp(physicsVerts[i], bOriBBoxD.Center, -0.1d);
                lerpedVerts[i] = newVert;
            }

            var voxelHitVecs = new List<Vector3D>();
            const int filter = CollisionLayers.VoxelCollisionLayer;
            if ((sVelSqr > 0.00001 || sAvelSqr > 0.00001)) //&& voxelMap.GetIntersectionWithSphere(ref obbSphere))
            {
                var obbSphereTest = bOriBBoxD.Intersects(ref voxelSphere);
                if (!obbSphereTest) return;
                for (int i = 0; i < 642; i++)
                {
                    IHitInfo hit = null;
                    var from = physicsVerts[i];
                    var to = lerpedVerts[i];
                    //var dir = to - from;
                    //if (sAvelSqr < 1e-4f && Vector3D.Dot(dir, sVel) < 0) continue;
                    MyAPIGateway.Physics.CastRay(from, to, out hit, filter);
                    if (hit?.HitEntity is MyVoxelBase) voxelHitVecs.Add(hit.Position);
                    //DsDebugDraw.DrawLineToVec(from, to, Color.Black);
                }
            }
            for (int i = 0; i < voxelHitVecs.Count; i++) shieldGrid.Physics.ApplyImpulse((bOriBBoxD.Center - voxelHitVecs[i]) * shieldGridMass / 100, voxelHitVecs[i]);
        }

        public static void SmallIntersect(EntIntersectInfo entInfo, MyConcurrentQueue<IMySlimBlock> fewDmgBlocks, MyConcurrentQueue<IMySlimBlock> destroyedBlocks, MyConcurrentQueue<MyAddForceData> force, MyConcurrentQueue<MyImpulseData> impulse, MyCubeGrid grid, MatrixD matrix, MatrixD matrixInv)
        {
            try
            {
                var contactPoint = ContactPointOutside(grid, matrix);
                if (!(Vector3D.Transform(contactPoint, matrixInv).LengthSquared() <= 1)) return;

                var getBlocks = new List<IMySlimBlock>();
                (grid as IMyCubeGrid).GetBlocks(getBlocks);
                Vector3D[] blockPoints = new Vector3D[9];
                var collisionAvg = Vector3D.Zero;
                var c3 = 0;
                var damage = 0f;
                for (int i = 0; i < getBlocks.Count; i++)
                {
                    var block = getBlocks[i];
                    if (block.IsDestroyed)
                    {
                        destroyedBlocks.Enqueue(block);
                        continue;
                    }

                    BoundingBoxD blockBox;
                    block.GetWorldBoundingBox(out blockBox);
                    blockBox.GetCorners(blockPoints);
                    blockPoints[8] = blockBox.Center;
                    for (int j = 8; j > -1; j--)
                    {
                        var point = blockPoints[j];
                        if (Vector3.Transform(point, matrixInv).LengthSquared() > 1) continue;
                        c3++;
                        collisionAvg += point;
                        fewDmgBlocks.Enqueue(block);
                        break;
                    }
                }

                if (collisionAvg != Vector3D.Zero)
                {
                    collisionAvg /= c3;
                    entInfo.ContactPoint = collisionAvg;
                    var mass = grid.GetCurrentMass();

                    var transformInv = matrixInv;
                    var normalMat = MatrixD.Transpose(transformInv);
                    var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                    var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                    var gridLinearVel = grid.Physics.LinearVelocity;
                    var gridLinearLen = gridLinearVel.Length();

                    var forceData = new MyAddForceData { MyGrid = grid, Force = (grid.PositionComp.WorldAABB.Center - matrix.Translation) * (mass * gridLinearLen), MaxSpeed = MathHelper.Clamp(gridLinearLen, 10, gridLinearLen * 0.5f)};
                    var impulseData = new MyImpulseData { MyGrid = grid, Direction = mass * 0.015 * -Vector3D.Dot(gridLinearVel, surfaceNormal) * surfaceNormal, Position = collisionAvg };
                    force.Enqueue(forceData);
                    impulse.Enqueue(impulseData);
                    entInfo.Damage = mass * 0.1f;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SmallIntersect: {ex}"); }
        }

        public static void ClientSmallIntersect(EntIntersectInfo entInfo, MyCubeGrid grid, MatrixD matrix, MatrixD matrixInv, MyConcurrentQueue<MyCubeGrid> eject)
        {
            try
            {
                if (grid == null) return;
                var contactPoint = ContactPointOutside(grid, matrix);
                if (!(Vector3D.Transform(contactPoint, matrixInv).LengthSquared() <= 1)) return;
                entInfo.ContactPoint = contactPoint;

                var approching = Vector3.Dot(grid.Physics.LinearVelocity, grid.PositionComp.WorldVolume.Center - contactPoint) < 0;
                if (approching) eject.Enqueue(grid);
            }
            catch (Exception ex) { Log.Line($"Exception in ClientSmallIntersect: {ex}"); }
        }

        public static bool Intersecting(IMyCubeGrid breaching, IMyCubeGrid shield, Vector3D[] physicsVerts, Vector3D breachingPos)
        {
            var shieldPos = ClosestVert(physicsVerts, breachingPos);
            var gridVel = breaching.Physics.LinearVelocity;
            var gridCenter = breaching.PositionComp.WorldVolume.Center;
            var shieldVel = shield.Physics.LinearVelocity;
            var shieldCenter = shield.PositionComp.WorldVolume.Center;
            var gApproching = Vector3.Dot(gridVel, gridCenter - shieldPos) < 0;
            var sApproching = Vector3.Dot(shieldVel, shieldCenter - breachingPos) < 0;
            return gApproching || sApproching;
        }

        public static Vector3D ContactPointOutside(MyEntity breaching, MatrixD matrix)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = matrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        public static bool SphereTouchOutside(MyEntity breaching, MatrixD matrix, MatrixD detectMatrixInv)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = matrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var closestPointOnSphere = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius + 1));

            var intersect = Vector3D.Transform(closestPointOnSphere, detectMatrixInv).LengthSquared() <= 1;
            return intersect;
        }

        public static bool PointInShield(Vector3D entCenter, MatrixD matrixInv)
        {
            return Vector3D.Transform(entCenter, matrixInv).LengthSquared() <= 1;
        }


        public static bool VoxelContact(Vector3D[] physicsVerts, MyVoxelBase voxelBase)
        {
            try
            {
                if (voxelBase.Closed) return false;
                var planet = voxelBase as MyPlanet;
                var map = voxelBase as MyVoxelMap;
                var isPlanet = voxelBase is MyPlanet;
                if (isPlanet)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var hit = planet.DoOverlapSphereTest(0.1f, from);
                        if (hit) return true;
                    }
                }
                else
                {
                    for (int i = 0; i < 162; i++)
                    {
                        if (map == null) continue;
                        var from = physicsVerts[i];
                        var hit = map.DoOverlapSphereTest(0.1f, from);
                        if (hit) return true;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }
            return false;
        }

        public static Vector3D VoxelCollisionSphere(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase, MyOrientedBoundingBoxD sOriBBoxD, MatrixD detectMatrix)
        {
            var collisionAvg = Vector3D.Zero;

            try
            {
                var sVel = shieldGrid.Physics.LinearVelocity;
                var sVelSqr = sVel.LengthSquared();
                var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
                var voxelSphere = voxelBase.RootVoxel.PositionComp.WorldVolume;
                var voxelHitVecs = new List<Vector3D>();
                if (sVelSqr > 0.00001 || sAvelSqr > 0.00001)
                {
                    var obbSphereTest = sOriBBoxD.Intersects(ref voxelSphere);
                    if (!obbSphereTest || voxelBase.Closed) return Vector3D.NegativeInfinity;
                    var planet = voxelBase as MyPlanet;
                    var map = voxelBase as MyVoxelMap;
                    var isPlanet = voxelBase is MyPlanet;
                    if (isPlanet)
                    {
                        for (int i = 0; i < 162; i++)
                        {
                            var from = physicsVerts[i];
                            var hit = planet.RootVoxel.DoOverlapSphereTest(0.1f, from);
                            if (hit) voxelHitVecs.Add(from);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 162; i++)
                        {
                            if (map == null) continue;
                            var from = physicsVerts[i];
                            var hit = map.RootVoxel.DoOverlapSphereTest(0.1f, from);
                            if (hit) voxelHitVecs.Add(from);
                        }
                    }
                }

                if (voxelHitVecs.Count == 0) return Vector3D.NegativeInfinity;
                var sPhysics = shieldGrid.Physics;
                var lSpeed = sPhysics.LinearVelocity.Length();
                var aSpeed = sPhysics.AngularVelocity.Length() * 20;
                var speed = 0f;
                speed = lSpeed > aSpeed ? lSpeed : aSpeed;
                Vector3D collisionAdd = Vector3D.Zero;
                for (int i = 0; i < voxelHitVecs.Count; i++)
                {
                    var point = voxelHitVecs[i];
                    collisionAdd += point;
                }
                collisionAvg = collisionAdd / voxelHitVecs.Count;

                shieldGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * (shieldGrid as MyCubeGrid)?.GetCurrentMass() * speed, null, Vector3D.Zero, MathHelper.Clamp(speed, 1f, 20f));
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }

            return collisionAvg;
        }

        public static void ClosestCornerInShield(Vector3D[] gridCorners, MatrixD matrixInv, ref Vector3D cloestPoint)
        {
            var minValue1 = double.MaxValue;

            for (int i = 0; i < 8; i++)
            {
                var point = gridCorners[i];
                var pointInside = Vector3D.Transform(point, matrixInv).LengthSquared();
                if (!(pointInside <= 1) || !(pointInside < minValue1)) continue;
                minValue1 = pointInside;
                cloestPoint = point;
            }
        }

        public static int CornerOrCenterInShield(MyEntity ent, MatrixD matrixInv, Vector3D[] corners, bool firstMatch = false)
        {

            var c = 0;
            if (Vector3D.Transform(ent.PositionComp.WorldAABB.Center, matrixInv).LengthSquared() <= 1) c++;
            if (firstMatch && c > 0) return c;

            ent.PositionComp.WorldAABB.GetCorners(corners);
            for (int i = 0; i < 8; i++)
            {
                if (Vector3D.Transform(corners[i], matrixInv).LengthSquared() <= 1) c++;
                if (firstMatch && c > 0) return c;
            }
            return c;
        }

        public static int CornersInShield(MyEntity ent, MatrixD matrixInv)
        {
            var entCorners = ent.PositionComp.WorldAABB.GetCorners();
            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                var pointInside = Vector3D.Transform(entCorners[i], matrixInv).LengthSquared() <= 2;
                if (pointInside) c++;
            }
            return c;
        }

        public static int NotAllCornersInShield(IMyCubeGrid grid, MatrixD matrixInv)
        {
            var gridCorners = grid.PositionComp.WorldAABB.GetCorners();
            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                var pointInside = Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1;
                if (pointInside) c++;
                else if (c != 0) break;
            }
            return c;
        }

        public static Vector3D ClosestPointInShield(MyOrientedBoundingBoxD bOriBBoxD, MatrixD matrixInv)
        {
            var webentPoints = new Vector3D[15];
            bOriBBoxD.GetCorners(webentPoints, 0);

            webentPoints[8] = bOriBBoxD.Center;
            webentPoints[9] = (webentPoints[0] + webentPoints[5]) / 2;
            webentPoints[10] = (webentPoints[3] + webentPoints[7]) / 2;
            webentPoints[11] = (webentPoints[0] + webentPoints[7]) / 2;
            webentPoints[12] = (webentPoints[1] + webentPoints[6]) / 2;
            webentPoints[13] = (webentPoints[4] + webentPoints[7]) / 2;
            webentPoints[14] = (webentPoints[0] + webentPoints[2]) / 2;

            var minValue1 = double.MaxValue;
            var closestPoint = Vector3D.NegativeInfinity;
            for (int i = 0; i < 15; i++)
            {
                var point = webentPoints[i];
                var pointInside = Vector3D.Transform(point, matrixInv).LengthSquared();
                if (!(pointInside <= 1) || !(pointInside < minValue1)) continue;
                minValue1 = pointInside;
                closestPoint = point;
            }
            return closestPoint;
        }

        public static bool AllCornersInShield(MyOrientedBoundingBoxD bOriBBoxD, MatrixD matrixInv)
        {
            var gridCorners = new Vector3D[8];
            bOriBBoxD.GetCorners(gridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c == 8;
        }

        public static bool AnyCornerInShield(MyOrientedBoundingBoxD bOriBBoxD, MatrixD matrixInv)
        {
            var gridCorners = new Vector3D[8];
            bOriBBoxD.GetCorners(gridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c > 0;
        }

        public static int ObbPointsInShield(MyEntity ent, MatrixD matrixInv)
        {
            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(GridPoints, 0);
            GridPoints[8] = obb.Center;
            var c = 0;
            for (int i = 0; i < 9; i++)
                if (Vector3D.Transform(GridPoints[i], matrixInv).LengthSquared() <= 1) c++;
            return c;
        }

        public static int ObbCornersInShield(MyEntity ent, MatrixD matrixInv)
        {
            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(GridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(GridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c;
        }

        public static bool AllObbCornersInShield(MyEntity ent, MatrixD matrixInv)
        {
            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(GridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(GridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c == 8;
        }

        public static bool AnyObbCornerInShield(MyEntity ent, MatrixD matrixInv)
        {
            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);
            obb.GetCorners(GridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(GridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c > 0;
        }

        public static bool AllAabbInShield(BoundingBoxD gridAabb, MatrixD matrixInv)
        {
            gridAabb.GetCorners(GridCorners);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(GridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c == 8;
        }

        public static void IntersectSmallBox(int[] closestFace, Vector3D[] physicsVerts, BoundingBoxD bWorldAabb, List<Vector3D> intersections)
        {
            for (int i = 0, j = 0; i < closestFace.Length; i += 3, j++)
            {
                var v0 = physicsVerts[closestFace[i]];
                var v1 = physicsVerts[closestFace[i + 1]];
                var v2 = physicsVerts[closestFace[i + 2]];
                var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);
                if (!test1) continue;
                intersections.Add(v0); 
                intersections.Add(v1);
                intersections.Add(v2);
            }
        }

        public static Vector3D ClosestVert(Vector3D[] physicsVerts, Vector3D pos)
        {
            var minValue1 = double.MaxValue;
            var closestVert = Vector3D.NegativeInfinity;


            for (int p = 0; p < physicsVerts.Length; p++)
            {
                var vert = physicsVerts[p];
                var range = vert - pos;
                var test = (range.X * range.X + range.Y * range.Y + range.Z * range.Z);
                if (test < minValue1)
                {
                    minValue1 = test;
                    closestVert = vert;
                }
            }
            return closestVert;
        }

        public static int ClosestVertNum(Vector3D[] physicsVerts, Vector3D pos)
        {
            var minValue1 = double.MaxValue;
            var closestVertNum = int.MaxValue;


            for (int p = 0; p < physicsVerts.Length; p++)
            {
                var vert = physicsVerts[p];
                var range = vert - pos;
                var test = (range.X * range.X + range.Y * range.Y + range.Z * range.Z);
                if (test < minValue1)
                {
                    minValue1 = test;
                    closestVertNum = p;
                }
            }
            return closestVertNum;
        }

        public static int GetClosestTri(Vector3D[] physicsOutside, Vector3D pos)
        {
            var triDist1 = double.MaxValue;
            var triNum = 0;
            //var ttri = new Triangle3d(Vector3D.Zero, Vector3D.Zero, Vector3D.Zero);
            //var odistTri = new DistPoint3Triangle3(pos, ttri);

            for (int i = 0; i < physicsOutside.Length; i += 3)
            {
                var ov0 = physicsOutside[i];
                var ov1 = physicsOutside[i + 1];
                var ov2 = physicsOutside[i + 2];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(pos, otri);
                odistTri.Update(pos, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    triNum = i;
                }
            }
            //Log.Line($"tri: {triNum}");
            return triNum;
        }
    }
}

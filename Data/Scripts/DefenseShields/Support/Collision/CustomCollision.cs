using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Voxels;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields.Support
{
    internal static class CustomCollision
    {
        public static bool VoxelContact(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase, MyStorageData tempStorage, MatrixD detectMatrix)
        {
            try
            {
                if (voxelBase.Closed) return false;
                var leftBottomCorner = voxelBase.PositionLeftBottomCorner;
                var storageMin = voxelBase.StorageMin;
                var map = voxelBase as IMyVoxelMap;
                var storage = (IMyStorage)voxelBase.Storage;
                const float radius = 0.01f;
                for (int i = 0; i < 162; i++)
                {
                    var from = physicsVerts[i];
                    var hit = DoOverlapSphereTest(from, radius, tempStorage, map, storage, leftBottomCorner, storageMin);
                    if (hit) return true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }

            return false;
        }

        public static Vector3D VoxelCollisionEllipsoid(IMyCubeGrid shieldGrid, MatrixD matrixInv, MyVoxelBase voxelBase, MyOrientedBoundingBoxD sOriBBoxD, MyStorageData tempStorage)
        {

            var sVel = shieldGrid.Physics.LinearVelocity;
            var sVelSqr = sVel.LengthSquared();
            var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
            var voxelSphere = voxelBase.RootVoxel.PositionComp.WorldVolume;
            var voxelHitVecs = new List<Vector3D>();
            if ((sVelSqr > 0.00001 || sAvelSqr > 0.00001))
            {
                var obbSphereTest = sOriBBoxD.Intersects(ref voxelSphere);
                if (!obbSphereTest || voxelBase.Closed) return Vector3D.NegativeInfinity;
                var leftBottomCorner = voxelBase.PositionLeftBottomCorner;
                var storageMin = voxelBase.StorageMin;
                var map = voxelBase as IMyVoxelMap;
                var storage = (IMyStorage)voxelBase.Storage;
                var shieldAabb = sOriBBoxD.GetAABB();
                //var dsutil = new DSUtils();
                //dsutil.Sw.Start();
                EllipsoidTest(matrixInv, shieldAabb, tempStorage, map, storage, leftBottomCorner, storageMin, voxelHitVecs);
                //dsutil.StopWatchReport("ellipsoid", -1);
            }

            if (voxelHitVecs.Count == 0) return Vector3D.NegativeInfinity;

            var sPhysics = shieldGrid.Physics;
            var speed = sPhysics.LinearVelocity.Length();
            var collisionAvg = Vector3D.Zero;

            for (int i = 0; i < voxelHitVecs.Count; i++)
            {
                var point = voxelHitVecs[i];
                collisionAvg += point;

                shieldGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(point - sPhysics.CenterOfMassWorld) * sPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(speed, 1f, 20f));
            }
            return collisionAvg;
        }

        public static void EllipsoidTest(MatrixD matrixInv, BoundingBoxD box, MyStorageData tempStorage, IMyVoxelMap myVoxelMap, IMyStorage storage, Vector3D leftBottomCorner, Vector3I storageMin, List<Vector3D> voxelHits)
        {
            Vector3I minCorner, maxCorner;
            {
                var boxMin = box.Min - MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                var boxMax = box.Max + MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                MyVoxelCoordSystems.WorldPositionToVoxelCoord(leftBottomCorner, ref boxMin, out minCorner);
                MyVoxelCoordSystems.WorldPositionToVoxelCoord(leftBottomCorner, ref boxMax, out maxCorner);
            }

            minCorner += storageMin;
            maxCorner += storageMin;

            if (myVoxelMap != null)
            {
                myVoxelMap.ClampVoxelCoord(ref minCorner);
                myVoxelMap.ClampVoxelCoord(ref maxCorner);
            }

            var flag = MyVoxelRequestFlags.AdviseCache;
            tempStorage.Resize(minCorner, maxCorner);
            storage.ReadRange(tempStorage, MyStorageDataTypeFlags.Content, 0, minCorner, maxCorner, ref flag);

            Vector3I tempVoxelCoord, cache;
            for (tempVoxelCoord.Z = minCorner.Z, cache.Z = 0; tempVoxelCoord.Z <= maxCorner.Z; tempVoxelCoord.Z++, cache.Z++)
            {
                for (tempVoxelCoord.Y = minCorner.Y, cache.Y = 0; tempVoxelCoord.Y <= maxCorner.Y; tempVoxelCoord.Y++, cache.Y++)
                {
                    for (tempVoxelCoord.X = minCorner.X, cache.X = 0; tempVoxelCoord.X <= maxCorner.X; tempVoxelCoord.X++, cache.X++)
                    {
                        var voxelContent = tempStorage.Content(ref cache);

                        if (voxelContent < MyVoxelConstants.VOXEL_ISO_LEVEL) continue;

                        Vector3D voxelPosition;
                        MyVoxelCoordSystems.VoxelCoordToWorldPosition(leftBottomCorner - storageMin * MyVoxelConstants.VOXEL_SIZE_IN_METRES, ref tempVoxelCoord, out voxelPosition);

                        var newDistanceToVoxel = Vector3D.Transform(voxelPosition, matrixInv).LengthSquared() <= 1;

                        if (newDistanceToVoxel) voxelHits.Add(voxelPosition);
                    }
                }
            }
        }

        public static Vector3D VoxelCollisionSphere(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase, MyOrientedBoundingBoxD sOriBBoxD, MyStorageData tempStorage, MatrixD detectMatrix)
        {
            var collisionAvg = Vector3D.Zero;

            try
            {
                var sVel = shieldGrid.Physics.LinearVelocity;
                var sVelSqr = sVel.LengthSquared();
                var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
                var voxelSphere = voxelBase.RootVoxel.PositionComp.WorldVolume;
                var voxelHitVecs = new List<Vector3D>();
                if ((sVelSqr > 0.00001 || sAvelSqr > 0.00001))
                {
                    var obbSphereTest = sOriBBoxD.Intersects(ref voxelSphere);
                    if (!obbSphereTest || voxelBase.Closed) return Vector3D.NegativeInfinity;
                    var leftBottomCorner = voxelBase.PositionLeftBottomCorner;
                    var storageMin = voxelBase.StorageMin;
                    var map = voxelBase as IMyVoxelMap;
                    var storage = (IMyStorage)voxelBase.Storage;
                    const float radius = 0.01f;
                    //var dsutil = new DSUtils();
                    //dsutil.Sw.Start();
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var hit = DoOverlapSphereTest(from, radius, tempStorage, map, storage, leftBottomCorner, storageMin);

                        if (hit) voxelHitVecs.Add(from);
                    }
                    //dsutil.StopWatchReport("sphereVoxel", -1);
                }

                if (voxelHitVecs.Count == 0) return Vector3D.NegativeInfinity;
                var sPhysics = shieldGrid.Physics;
                var lSpeed = sPhysics.LinearVelocity.Length();
                var aSpeed = sPhysics.AngularVelocity.Length() * 20;
                var speed = 0f;
                speed = lSpeed > aSpeed ? lSpeed : aSpeed;

                for (int i = 0; i < voxelHitVecs.Count; i++)
                {
                    var point = voxelHitVecs[i];
                    collisionAvg += point;

                    shieldGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(point - sPhysics.CenterOfMassWorld) * sPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(speed, 1f, 20f));
                }
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }

            return collisionAvg;
        } 

        public static bool DoOverlapSphereTest(Vector3D center, float radius, MyStorageData tempStorage, IMyVoxelMap myVoxelMap, IMyStorage storage, Vector3D leftBottomCorner, Vector3I storageMin)
        {
            try
            {
                if (storage.Closed || storage.MarkedForClose) return false;

                var body0Pos = center;
                BoundingSphereD newSphere;
                newSphere.Center = body0Pos;
                newSphere.Radius = radius;

                Vector3I minCorner, maxCorner;
                {
                    var sphereMin = newSphere.Center - newSphere.Radius - MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                    var sphereMax = newSphere.Center + newSphere.Radius + MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                    MyVoxelCoordSystems.WorldPositionToVoxelCoord(leftBottomCorner, ref sphereMin, out minCorner);
                    MyVoxelCoordSystems.WorldPositionToVoxelCoord(leftBottomCorner, ref sphereMax, out maxCorner);
                }

                minCorner += storageMin;
                maxCorner += storageMin;

                if (myVoxelMap != null)
                {
                    myVoxelMap.ClampVoxelCoord(ref minCorner);
                    myVoxelMap.ClampVoxelCoord(ref maxCorner);
                }

                var flag = MyVoxelRequestFlags.AdviseCache;
                tempStorage.Clear(MyStorageDataTypeEnum.Content, 0); // did this fix index out of bounds error?
                tempStorage.Resize(minCorner, maxCorner);
                storage.ReadRange(tempStorage, MyStorageDataTypeFlags.Content, 0, minCorner, maxCorner, ref flag);
                try
                {
                    Vector3I tempVoxelCoord, cache;
                    for (tempVoxelCoord.Z = minCorner.Z, cache.Z = 0; tempVoxelCoord.Z <= maxCorner.Z; tempVoxelCoord.Z++, cache.Z++)
                    {
                        for (tempVoxelCoord.Y = minCorner.Y, cache.Y = 0; tempVoxelCoord.Y <= maxCorner.Y; tempVoxelCoord.Y++, cache.Y++)
                        {
                            for (tempVoxelCoord.X = minCorner.X, cache.X = 0; tempVoxelCoord.X <= maxCorner.X; tempVoxelCoord.X++, cache.X++)
                            {
                                byte voxelContent = tempStorage.Content(ref cache);

                                if (voxelContent < MyVoxelConstants.VOXEL_ISO_LEVEL) continue;

                                Vector3D voxelPosition;
                                MyVoxelCoordSystems.VoxelCoordToWorldPosition(leftBottomCorner - storageMin * MyVoxelConstants.VOXEL_SIZE_IN_METRES, ref tempVoxelCoord, out voxelPosition);

                                var voxelSize = voxelContent / MyVoxelConstants.VOXEL_CONTENT_FULL_FLOAT * MyVoxelConstants.VOXEL_RADIUS;

                                var newDistanceToVoxel = Vector3.DistanceSquared(voxelPosition, newSphere.Center) - voxelSize;
                                if (newDistanceToVoxel < newSphere.Radius) return true;
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in DoOverlapSphereTest for loops: {ex}"); }
            }
            catch (Exception ex) { Log.Line($"Exception in DoOverlapSphereTest: {ex}"); }

            return false;
        }

        public static void GetIntersect(Vector3D center, double radius, IMyCubeGrid query, List<IMySlimBlock> result)
        {
            Vector3I gc = query.WorldToGridInteger(center);
            double rc = radius / query.GridSize;
            query.GetBlocks(result, s => DistanceSquared(gc, s.Position) < rc);
        }

        public static double DistanceSquared(Vector3I value1, Vector3I value2)
        {
            int num1 = value1.X - value2.X;
            int num2 = value1.Y - value2.Y;
            int num3 = value1.Z - value2.Z;
            return num1 * num1 + num2 * num2 + num3 * num3;
        }

        public static double? IntersectRayEllipsoid(Vector3D rayPos, Vector3D rayDir)
        {
            MatrixD T = MatrixD.CreateTranslation(Vector3D.Zero);
            MatrixD S = MatrixD.CreateScale(Vector3D.One);
            MatrixD R = MatrixD.CreateFromQuaternion(Quaternion.Zero);

            MatrixD ellipsoidMatrix = MatrixD.Multiply(MatrixD.Multiply(T, R), S);

            MatrixD inverseEllipsoidMatrix = MatrixD.Invert(ellipsoidMatrix);

            Vector3D krayPos = Vector3D.Transform(rayPos, inverseEllipsoidMatrix);
            Vector3D krayDir = Vector3D.Transform(rayDir, inverseEllipsoidMatrix);


            //MyAPIGateway.Utilities.ShowNotification("" + rayPos + " " + rayDir, 66);
            //MyAPIGateway.Utilities.ShowNotification("" + krayPos + " " + krayDir, 66);
            krayDir.Normalize();

            BoundingSphereD sphere = new BoundingSphereD(Vector3.Zero, 1d);

            RayD kRay = new RayD(krayPos, krayDir);

            double? hitMult = sphere.Intersects(kRay);

            return hitMult;
        }

        public static void MeshCollisionStaticSphere(IMyCubeBlock block, BoundingSphereD shieldSphere, Vector3D[] physicsVerts, MatrixD matrix, MyOrientedBoundingBoxD sOriBBoxD)
        {
            var meshHitVecs = new List<Vector3D>();

            var obbSphereTest = sOriBBoxD.Intersects(ref shieldSphere);
            if (!obbSphereTest) return;
            for (int i = 0; i < 642; i++)
            {
                var from = physicsVerts[i];
                var hit = PointInShield(from, matrix);
                if (hit) meshHitVecs.Add(from);
                //if (hit) Log.Line($"we have a hit");
                //DsDebugDraw.DrawLineToVec(from, shieldSphere.Center, Color.Black);
            }
        }

        public static void MeshCollisionSphere(IMyCubeGrid shieldGrid, BoundingSphereD shieldSphere, Vector3D[] physicsVerts, MatrixD matrix, MyOrientedBoundingBoxD sOriBBoxD)
        {
            var sVel = shieldGrid.Physics.LinearVelocity;
            var sVelSqr = sVel.LengthSquared();
            var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
            var shieldGridMass = shieldGrid.Physics.Mass;

            var meshHitVecs = new List<Vector3D>();
            if ((sVelSqr > 0.00001 || sAvelSqr > 0.00001))
            {
                var obbSphereTest = sOriBBoxD.Intersects(ref shieldSphere);
                if (!obbSphereTest) return;
                for (int i = 0; i < 642; i++)
                {
                    var from = physicsVerts[i];
                    var hit = PointInShield(from, matrix);
                    if (hit) meshHitVecs.Add(from);
                    //if (hit) Log.Line($"we have a hit");
                    //DsDebugDraw.DrawLineToVec(from, shieldSphere.Center, Color.Black);
                }
            }
            for (int i = 0; i < meshHitVecs.Count; i++) shieldGrid.Physics.ApplyImpulse((sOriBBoxD.Center - meshHitVecs[i]) * shieldGridMass / 250, meshHitVecs[i]);
        }

        public static void ShieldX2PointsInside(Vector3D[] shield1Verts, MatrixD shield1MatrixInv, Vector3D[] shield2Verts, MatrixD shield2MatrixInv, List<Vector3D> insidePoints)
        {
            for (int i = 0; i < 642; i++) if (Vector3D.Transform(shield1Verts[i], shield2MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield1Verts[i]); 
            for (int i = 0; i < 642; i++) if (Vector3D.Transform(shield2Verts[i], shield1MatrixInv).LengthSquared() <= 1) insidePoints.Add(shield2Verts[i]);
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

        public static void SmallIntersect(EntIntersectInfo entInfo, MyConcurrentQueue<IMySlimBlock> fewDmgBlocks, IMyCubeGrid grid, MatrixD matrix, MatrixD matrixInv)
        {
            var contactPoint = ContactPointOutside(grid, matrix);
            if (!(Vector3D.Transform(contactPoint, matrixInv).LengthSquared() <= 1)) return;
            entInfo.ContactPoint = contactPoint;

            var approching = Vector3.Dot(grid.Physics.LinearVelocity, grid.PositionComp.WorldVolume.Center - contactPoint) < 0;
            if (approching) grid.Physics.LinearVelocity = grid.Physics.LinearVelocity * -0.25f;

            var dmgblockCnt = fewDmgBlocks.Count;
            if (dmgblockCnt == 25) return;
            var getBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(getBlocks);
            var damage = 0f;
            for (int i = 0; i < getBlocks.Count && i < 25 - dmgblockCnt; i++)
            {
                var block = getBlocks[i];
                damage += block.Mass;
                fewDmgBlocks.Enqueue(block);
            }
            entInfo.Damage = damage;
        }

        public static Vector3D EjectDirection(IMyCubeGrid grid, Vector3D[] physicsOutside, int[][] vertTris, MyOrientedBoundingBoxD obb, MatrixD matrixInv)
        {
            var targetPos = ClosestPointInShield(obb, matrixInv);
            var gridVel = grid.Physics.LinearVelocity;
            var gridCenter = grid.PositionComp.WorldVolume.Center;
            var approching = Vector3.Dot(gridVel, gridCenter - targetPos) < 0;
            if (approching) grid.Physics.LinearVelocity = gridVel * -0.1f;
            else return Vector3D.NegativeInfinity;
            var rangedVerts = new int[3];

            VertRangeFullCheck(physicsOutside, gridCenter, rangedVerts);

            var closestFace0 = vertTris[rangedVerts[0]];
            var closestFace1 = vertTris[rangedVerts[1]];
            var closestFace2 = vertTris[rangedVerts[2]];

            var center = GetClosestTriCenter(physicsOutside, closestFace0, closestFace1, closestFace2, gridCenter);
            return center;
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

        public static Vector3D ContactPointOutside(IMyEntity breaching, MatrixD matrix)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = matrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        public static bool PointInShield(Vector3D entCenter, MatrixD matrixInv)
        {
            return Vector3D.Transform(entCenter, matrixInv).LengthSquared() <= 1;
        }

        public static bool ExtendFitPoints(Vector3D entCenter, MatrixD matrixInv)
        {
            return Vector3D.Transform(entCenter, matrixInv).LengthSquared() <= 0.90;
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

        public static bool AnyCornerNotInShield(IMyCubeGrid grid, MatrixD matrixInv)
        {

            var gridCorners = grid.PositionComp.WorldAABB.GetCorners();
            for (int i = 0; i < 8; i++)
            {
                var point = gridCorners[i];
                var pointInside = Vector3D.Transform(point, matrixInv).LengthSquared();
                if (pointInside >= 1) return true;
            }
            return false;
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
        public static bool AllAabbInShield(BoundingBoxD gridAabb, MatrixD matrixInv)
        {
            var gridCorners = new Vector3D[8];
            gridAabb.GetCorners(gridCorners);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c == 8;
        }

        public static bool CheckFirstFace(int[] firstFace, int secondVertNum)
        {
            for (int i = 0; i < firstFace.Length; i++)
            {
                if (firstFace[i] == secondVertNum) return false;
            }
            return true;
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

        public static List<Vector3D> IntersectSmallBoxFaces(int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D[] physicsVerts, BoundingBoxD bWorldAabb, bool secondFace, bool thirdFace)
        {
            var boxedTriangles = new List<Vector3D>();
            for (int i = 0, j = 0; i < closestFace0.Length; i += 3, j++)
            {
                var v0 = physicsVerts[closestFace0[i]];
                var v1 = physicsVerts[closestFace0[i + 1]];
                var v2 = physicsVerts[closestFace0[i + 2]];
                var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                if (!test1) continue;
                boxedTriangles.Add(v0);
                boxedTriangles.Add(v1);
                boxedTriangles.Add(v2);
            }
            if (boxedTriangles.Count == 0 && secondFace)
            {
                for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
                {
                    var v0 = physicsVerts[closestFace1[i]];
                    var v1 = physicsVerts[closestFace1[i + 1]];
                    var v2 = physicsVerts[closestFace1[i + 2]];

                    var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                    if (!test1) continue;
                    boxedTriangles.Add(v0);
                    boxedTriangles.Add(v1);
                    boxedTriangles.Add(v2);
                }
            }
            if (boxedTriangles.Count == 0 && thirdFace)
            {
                for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
                {
                    var v0 = physicsVerts[closestFace2[i]];
                    var v1 = physicsVerts[closestFace2[i + 1]];
                    var v2 = physicsVerts[closestFace2[i + 2]];

                    var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                    if (!test1) continue;
                    boxedTriangles.Add(v0);
                    boxedTriangles.Add(v1);
                    boxedTriangles.Add(v2);
                }
            }
            return boxedTriangles;
        }

        public static bool GetClosestInOutTri(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace, Vector3D bWorldCenter)
        {
            var closestTri1 = -1;
            var triDist1 = double.MaxValue;

            for (int i = 0; i < closestFace.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace[i]];
                var ov1 = physicsOutside[closestFace[i + 1]];
                var ov2 = physicsOutside[closestFace[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                }
            }

            var iv0 = physicsInside[closestFace[closestTri1]];
            var iv1 = physicsInside[closestFace[closestTri1 + 1]];
            var iv2 = physicsInside[closestFace[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);
            return triDist1 > idistTri.GetSquared();
        }
       
        public static void GetClosestTriAndFace(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter, int[] faceTri)
        {
            var closestTri1 = -1;
            var closestFace = -1;

            var triDist1 = double.MaxValue;

            for (int i = 0; i < closestFace0.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace0[i]];
                var ov1 = physicsOutside[closestFace0[i + 1]];
                var ov2 = physicsOutside[closestFace0[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 0;
                }
            }

            for (int i = 0; i < closestFace1.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace1[i]];
                var ov1 = physicsOutside[closestFace1[i + 1]];
                var ov2 = physicsOutside[closestFace1[i + 2]];

                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 1;
                }
            }

            for (int i = 0; i < closestFace2.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace2[i]];
                var ov1 = physicsOutside[closestFace2[i + 1]];
                var ov2 = physicsOutside[closestFace2[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 2;
                }
            }

            int[] face;
            switch (closestFace)
            {
                case 0:
                    face = closestFace0;
                    break;
                case 1:
                    face = closestFace1;
                    break;
                default:
                    face = closestFace2;
                    break;
            }

            faceTri[0] = closestFace;
            faceTri[1] = face[closestTri1];
            faceTri[2] = face[closestTri1 + 1];
            faceTri[3] = face[closestTri1 + 2];

        }

        public static Vector3D GetClosestTriCenter(Vector3D[] physicsOutside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter)
        {
            var closestTri1 = -1;
            var closestFace = -1;

            var triDist1 = double.MaxValue;

            for (int i = 0; i < closestFace0.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace0[i]];
                var ov1 = physicsOutside[closestFace0[i + 1]];
                var ov2 = physicsOutside[closestFace0[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 0;
                }
            }

            for (int i = 0; i < closestFace1.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace1[i]];
                var ov1 = physicsOutside[closestFace1[i + 1]];
                var ov2 = physicsOutside[closestFace1[i + 2]];

                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 1;
                }
            }

            for (int i = 0; i < closestFace2.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace2[i]];
                var ov1 = physicsOutside[closestFace2[i + 1]];
                var ov2 = physicsOutside[closestFace2[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 2;
                }
            }

            int[] face;
            switch (closestFace)
            {
                case 0:
                    face = closestFace0;
                    break;
                case 1:
                    face = closestFace1;
                    break;
                default:
                    face = closestFace2;
                    break;
            }

            var center = (physicsOutside[face[closestTri1]] + physicsOutside[face[closestTri1 +1]] + physicsOutside[face[closestTri1 + 2]]) / 3;

            return center;
        }

        public static List<Vector3D> ContainPointObb(Vector3D[] physicsVerts, MyOrientedBoundingBoxD bOriBBoxD, BoundingSphereD tSphere)
        {
            var containedPoints = new List<Vector3D>();
            foreach (var vert in physicsVerts)
            {
                var vec = vert;
                if (tSphere.Contains(vec) == ContainmentType.Disjoint) continue;
                if (bOriBBoxD.Contains(ref vec))
                {
                    containedPoints.Add(vec);
                }
            }
            return containedPoints;
        }

        public static void GetAllClosestInOutTri(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter, int[] faceInsideTri)
        {
            var closestTri1 = -1;
            var closestFace = -1;

            var triDist1 = double.MaxValue;

            for (int i = 0; i < closestFace0.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace0[i]];
                var ov1 = physicsOutside[closestFace0[i + 1]];
                var ov2 = physicsOutside[closestFace0[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 0;
                }
            }

            for (int i = 0; i < closestFace1.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace1[i]];
                var ov1 = physicsOutside[closestFace1[i + 1]];
                var ov2 = physicsOutside[closestFace1[i + 2]];

                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 1;
                }
            }

            for (int i = 0; i < closestFace2.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace2[i]];
                var ov1 = physicsOutside[closestFace2[i + 1]];
                var ov2 = physicsOutside[closestFace2[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 2;
                }
            }

            int[] face;
            switch (closestFace)
            {
                case 0:
                    face = closestFace0;
                    break;
                case 1:
                    face = closestFace1;
                    break;
                default:
                    face = closestFace2;
                    break;
            }

            faceInsideTri[2] = face[closestTri1];
            faceInsideTri[3] = face[closestTri1 + 1];
            faceInsideTri[4] = face[closestTri1 + 2];

            var iv0 = physicsInside[face[closestTri1]];
            var iv1 = physicsInside[face[closestTri1 + 1]];
            var iv2 = physicsInside[face[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);

            faceInsideTri[0] = closestFace;
            if (triDist1 > idistTri.GetSquared()) faceInsideTri[1] = 1;
        }

        public static void VertRangeFullCheck(Vector3D[] physicsVerts, Vector3D bWorldCenter, int[] rangedVerts)
        {
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;


            var minNum1 = -2;
            var minNum2 = -2;
            var minNum3 = -2;


            for (int p = 0; p < physicsVerts.Length; p++)
            {
                var vert = physicsVerts[p];
                var range = vert - bWorldCenter;
                var test = (range.X * range.X + range.Y * range.Y + range.Z * range.Z);
                //var test = Vector3D.DistanceSquared(vert, bWorldCenter);
                if (test < minValue3)
                {
                    if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = minValue1;
                        minNum2 = minNum1;
                        minValue1 = test;
                        minNum1 = p;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = test;
                        minNum2 = p;
                    }
                    else
                    {
                        minValue3 = test;
                        minNum3 = p;
                    }
                }
            }
            rangedVerts[0] = minNum1;
            rangedVerts[1] = minNum2;
            rangedVerts[2] = minNum3;
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

        public static void Get3ClosestVerts(Vector3D[] physicsVerts, Vector3D bWorldCenter, Vector3D[] rangedVerts)
        {
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;

            var minVert1 = Vector3D.NegativeInfinity;
            var minVert2 = Vector3D.NegativeInfinity;
            var minVert3 = Vector3D.NegativeInfinity;

            for (int p = 0; p < physicsVerts.Length; p++)
            {
                var vert = physicsVerts[p];
                var range = vert - bWorldCenter;
                var test = (range.X * range.X + range.Y * range.Y + range.Z * range.Z);
                //var test = Vector3D.DistanceSquared(vert, bWorldCenter);
                if (test < minValue3)
                {
                    if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        minVert3 = minVert2;
                        minValue2 = minValue1;
                        minVert2 = minVert1;
                        minValue1 = test;
                        minVert1 = vert;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        minVert3 = minVert2;
                        minValue2 = test;
                        minVert2 = vert;
                    }
                    else
                    {
                        minValue3 = test;
                        minVert3 = vert;
                    }
                }
            }
            rangedVerts[0] = minVert1;
            rangedVerts[1] = minVert2;
            rangedVerts[2] = minVert3;
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

        public static void GetClosestTriInFace(Vector3D[] physicsOutside, int[] closestFace, Vector3D pos, Vector3D[] closestTri)
        {
            var closestTri1 = -1;
            var triDist1 = double.MaxValue;

            for (int i = 0; i < closestFace.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace[i]];
                var ov1 = physicsOutside[closestFace[i + 1]];
                var ov2 = physicsOutside[closestFace[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(pos, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                }
            }
            closestTri[0] = physicsOutside[closestTri1];
            closestTri[1] = physicsOutside[closestTri1 + 1];
            closestTri[2] = physicsOutside[closestTri1 + 2];
        }
    }
}

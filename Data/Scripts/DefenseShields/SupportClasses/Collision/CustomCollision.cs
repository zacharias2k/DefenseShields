namespace DefenseShields.Support
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Voxels;

    using VRageMath;
    using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

    internal class CustomCollision
    {
        public static Vector3D? MissileIntersect(DefenseShields ds, MyEntity missile, MatrixD detectMatrix, MatrixD detectMatrixInv)
        {
            var missileVel = missile.Physics.LinearVelocity;
            var missileCenter = missile.PositionComp.WorldVolume.Center;
            var velStepSize = missileVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 2;
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
            var leaving = Vector3D.Transform(missileCenter + (-missileVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS * 2), detectMatrixInv).LengthSquared() <= 1;
            return leaving;
        }

        public static float? IntersectEllipsoid(MatrixD ellipsoidMatrixInv, MatrixD ellipsoidMatrix, RayD ray)
        {
            var _normSphere = new BoundingSphereD(Vector3.Zero, 1f);
            var _kRay = new RayD(Vector3D.Zero, Vector3D.Forward);

            var krayPos = Vector3D.Transform(ray.Position, ellipsoidMatrixInv);
            var krayDir = Vector3D.Normalize(Vector3D.TransformNormal(ray.Direction, ellipsoidMatrixInv));

            _kRay.Direction = krayDir;
            _kRay.Position = krayPos;
            var nullDist = _normSphere.Intersects(_kRay);
            if (!nullDist.HasValue) return null;

            var hitPos = krayPos + (krayDir * -nullDist.Value);
            var worldHitPos = Vector3D.Transform(hitPos, ellipsoidMatrix);
            return Vector3.Distance(worldHitPos, ray.Position);
        }

        public static bool RayIntersectsTriangle(Vector3D rayOrigin, Vector3D rayVector, Vector3D v0, Vector3D v1, Vector3D v2, Vector3D outIntersectionPoint)
        {
            const double Epsilon = 0.0000001;
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var h = rayVector.Cross(edge2);
            var a = edge1.Dot(h);
            if (a > -Epsilon && a < Epsilon) return false;

            var f = 1 / a;
            var s = rayOrigin - v0;
            var u = f * s.Dot(h);
            if (u < 0.0 || u > 1.0) return false;

            var q = s.Cross(edge1);
            var v = f * rayVector.Dot(q);
            if (v < 0.0 || u + v > 1.0) return false;
            
            var t = f * edge2.Dot(q);
            if (t > Epsilon) 
            {
                // outIntersectionPoint = rayOrigin + rayVector * t;
                return true;
            }
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

        public static void VoxelCollision(MyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase)
        {
            var tmpList = new List<IHitInfo>();
            for (int i = 0; i < 642; i++)
            {
                var from = physicsVerts[i];
                var dir = Vector3D.Normalize(shieldGrid.PositionComp.WorldAABB.Center - from);
                var to = from + (dir * 0.01f);
                MyAPIGateway.Physics.CastRayParallel(ref from, ref to, tmpList, CollisionLayers.VoxelCollisionLayer, VoxelCollisionCallback);
                //MyAPIGateway.Physics.CastRay(from, to, out hit, CollisionLayers.VoxelCollisionLayer);
                //if (hit?.HitEntity is MyVoxelBase)
                    //shieldGrid.Physics.ApplyImpulse((shieldCenter - hit.Position) * shieldGridMass / 100, hit.Position);
            }
        }

        public static void VoxelCollisionCallback(List<IHitInfo> hitInfos)
        {
        }

        /*
        public static bool PosInVoxel(MyVoxelBase voxelBase, Vector3D pos, MyStorageData cache)
        {
            if (voxelBase.Storage.Closed) return false;
            //cache.Clear(MyStorageDataTypeEnum.Content, 0);
            //cache.Resize(Vector3I.One);
            Vector3I voxelCoord;
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelBase.RootVoxel.PositionLeftBottomCorner, ref pos, out voxelCoord);
            var flag = MyVoxelRequestFlags.EmptyContent;
            voxelBase.RootVoxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, voxelCoord, voxelCoord, ref flag);
            if (cache.Content(ref Vector3I.Zero) != (byte)0)
            {
                return true;
            }
            return false;
        }
        */

        public static bool VoxelContact(Vector3D[] physicsVerts, MyVoxelBase voxelBase)
        {
            try
            {
                if (voxelBase.RootVoxel.MarkedForClose || voxelBase.RootVoxel.Storage.Closed) return false;
                var planet = voxelBase as MyPlanet;
                var map = voxelBase as MyVoxelMap;

                if (planet != null)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var localPosition = (Vector3)(from - planet.PositionLeftBottomCorner);
                        var v = localPosition / 1f;
                        Vector3I voxelCoord;
                        Vector3I.Floor(ref v, out voxelCoord);

                        var hit = new VoxelHit();
                        planet.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);

                        if (hit.HasHit) return true;
                    }
                }
                else if (map != null)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var localPosition = (Vector3)(from - map.PositionLeftBottomCorner);
                        var v = localPosition / 1f;
                        Vector3I voxelCoord;
                        Vector3I.Floor(ref v, out voxelCoord);

                        var hit = new VoxelHit();
                        map.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);

                        if (hit.HasHit) return true;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelContact: {ex}"); }

            return false;
        }

        /*
        public static bool VoxelContact(Vector3D[] physicsVerts, MyVoxelBase voxelBase, MyStorageData cache)
        {
            try
            {
                if (voxelBase.RootVoxel.MarkedForClose || voxelBase.RootVoxel.Storage.Closed) return false;
                var planet = voxelBase as MyPlanet;
                var map = voxelBase as MyVoxelMap;
                var isPlanet = voxelBase is MyPlanet;
                if (isPlanet)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var hit = PosInVoxel(planet, physicsVerts[i], cache);
                        //var hit = planet.DoOverlapSphereTest(0.1f, from);
                        if (hit) return true;
                    }
                }
                else
                {
                    for (int i = 0; i < 162; i++)
                    {
                        if (map == null) continue;
                        //var hit = map.DoOverlapSphereTest(0.1f, from);
                        var hit = PosInVoxel(map, physicsVerts[i], cache);
                        if (hit) return true;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }
            return false;
        }
        */
        public static Vector3D? VoxelEllipsoidCheck(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, MyVoxelBase voxelBase)
        {
            var collisionAvg = Vector3D.Zero;
            try
            {
                if (voxelBase.RootVoxel.MarkedForClose || voxelBase.RootVoxel.Storage.Closed) return null;
                var planet = voxelBase as MyPlanet;
                var map = voxelBase as MyVoxelMap;

                var collision = Vector3D.Zero;
                var collisionCnt = 0;
                
                if (planet != null)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var localPosition = (Vector3)(from - planet.PositionLeftBottomCorner);
                        var v = localPosition / 1f;
                        Vector3I voxelCoord;
                        Vector3I.Floor(ref v, out voxelCoord);

                        var hit = new VoxelHit();
                        planet.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);

                        if (hit.HasHit)
                        {
                            collision += from;
                            collisionCnt++;
                        }
                    }
                }
                else if (map != null)
                {
                    for (int i = 0; i < 162; i++)
                    {
                        var from = physicsVerts[i];
                        var localPosition = (Vector3)(from - map.PositionLeftBottomCorner);
                        var v = localPosition / 1f;
                        Vector3I voxelCoord;
                        Vector3I.Floor(ref v, out voxelCoord);

                        var hit = new VoxelHit();
                        map.Storage.ExecuteOperationFast(ref hit, MyStorageDataTypeFlags.Content, ref voxelCoord, ref voxelCoord, notifyRangeChanged: false);

                        if (hit.HasHit)
                        {
                            collision += from;
                            collisionCnt++;
                        }
                    }
                }

                if (collisionCnt == 0) return null;
                var sPhysics = shieldGrid.Physics;
                var lSpeed = sPhysics.LinearVelocity.Length();
                var aSpeed = sPhysics.AngularVelocity.Length() * 20;
                var speed = 0f;
                speed = lSpeed > aSpeed ? lSpeed : aSpeed;

                collisionAvg = collision / collisionCnt;

                shieldGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * ((MyCubeGrid)shieldGrid).GetCurrentMass() * speed, null, Vector3D.Zero, MathHelper.Clamp(speed, 1f, 20f));
            }
            catch (Exception ex) { Log.Line($"Exception in VoxelCollisionSphere: {ex}"); }

            return collisionAvg;
        }

        public static void SmallIntersect(EntIntersectInfo entInfo, ConcurrentQueue<IMySlimBlock> fewDmgBlocks, ConcurrentQueue<IMySlimBlock> destroyedBlocks, ConcurrentQueue<MyAddForceData> force, ConcurrentQueue<MyImpulseData> impulse, MyCubeGrid grid, MatrixD matrix, MatrixD matrixInv)
        {
            try
            {
                var contactPoint = ContactPointOutside(grid, matrix);
                if (!(Vector3D.Transform(contactPoint, matrixInv).LengthSquared() <= 1)) return;

                var getBlocks = new List<IMySlimBlock>();
                (grid as IMyCubeGrid).GetBlocks(getBlocks);
                var blockPoints = new Vector3D[9];
                var collisionAvg = Vector3D.Zero;
                var c3 = 0;
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

                    var forceData = new MyAddForceData { MyGrid = grid, Force = (grid.PositionComp.WorldAABB.Center - matrix.Translation) * (mass * gridLinearLen), MaxSpeed = MathHelper.Clamp(gridLinearLen, 10, gridLinearLen * 0.5f) };
                    var impulseData = new MyImpulseData { MyGrid = grid, Direction = mass * 0.015 * -Vector3D.Dot(gridLinearVel, surfaceNormal) * surfaceNormal, Position = collisionAvg };
                    force.Enqueue(forceData);
                    impulse.Enqueue(impulseData);
                    entInfo.Damage = mass * 0.5f;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SmallIntersect: {ex}"); }
        }

        public static void ClientSmallIntersect(EntIntersectInfo entInfo, MyCubeGrid grid, MatrixD matrix, MatrixD matrixInv, ConcurrentQueue<MyCubeGrid> eject)
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

        public static int EntCornersInShield(MyEntity ent, MatrixD matrixInv, Vector3D[] entCorners)
        {
            var entAabb = ent.PositionComp.WorldAABB;
            entAabb.GetCorners(entCorners);

            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                var pointInside = Vector3D.Transform(entCorners[i], matrixInv).LengthSquared() <= 2;
                if (pointInside) c++;
            }
            return c;
        }

        public static int NotAllCornersInShield(MyCubeGrid grid, MatrixD matrixInv, Vector3D[] gridCorners)
        {
            var gridAabb = grid.PositionComp.WorldAABB;
            gridAabb.GetCorners(gridCorners);

            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                var pointInside = Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1;
                if (pointInside) c++;
                else if (c != 0) break;
            }
            return c;
        }

        public static bool AllAabbInShield(BoundingBoxD gridAabb, MatrixD matrixInv, Vector3D[] gridCorners = null)
        {
            if (gridCorners == null) gridCorners = new Vector3D[8];

            gridAabb.GetCorners(gridCorners);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c == 8;
        }

        public static bool ObbCornersInShield(MyOrientedBoundingBoxD bOriBBoxD, MatrixD matrixInv, Vector3D[] gridCorners, bool anyCorner = false)
        {
            bOriBBoxD.GetCorners(gridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1)
                {
                    if (anyCorner) return true;
                    c++;
                }
            }
            return c == 8;
        }

        public static int NewObbPointsInShield(MyEntity ent, MatrixD matrixInv, Vector3D[] gridPoints = null)
        {
            if (gridPoints == null) gridPoints = new Vector3D[9];

            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(gridPoints, 0);
            gridPoints[8] = obb.Center;
            var c = 0;
            for (int i = 0; i < 9; i++)
                if (Vector3D.Transform(gridPoints[i], matrixInv).LengthSquared() <= 1) c++;
            return c;
        }

        public static int NewObbCornersInShield(MyEntity ent, MatrixD matrixInv, Vector3D[] gridCorners = null)
        {
            if (gridCorners == null) gridCorners = new Vector3D[8];

            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(gridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1) c++;
            return c;
        }

        public static bool NewAllObbCornersInShield(MyEntity ent, MatrixD matrixInv, bool anyCorner, Vector3D[] gridCorners = null)
        {
            if (gridCorners == null) gridCorners = new Vector3D[8];

            var quaternion = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
            var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
            var gridCenter = ent.PositionComp.WorldAABB.Center;
            var obb = new MyOrientedBoundingBoxD(gridCenter, halfExtents, quaternion);

            obb.GetCorners(gridCorners, 0);
            var c = 0;
            for (int i = 0; i < 8; i++)
            {
                if (Vector3D.Transform(gridCorners[i], matrixInv).LengthSquared() <= 1)
                {
                    if (anyCorner) return true;
                    c++;
                }
            }
            return c == 8;
        }

        public static void IntersectSmallBox(int[] closestFace, Vector3D[] physicsVerts, BoundingBoxD bWorldAabb, List<Vector3D> intersections)
        {
            for (int i = 0; i < closestFace.Length; i += 3)
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
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);
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
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);
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
            return triNum;
        }
    }
}

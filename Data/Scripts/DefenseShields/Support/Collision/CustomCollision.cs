using System;
using System.Collections.Generic;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.WorldEnvironment.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields.Support
{
    internal static class CustomCollision
    {
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

        public static void VoxelCollisionSphere(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, IMyVoxelMap voxelMap, MyOrientedBoundingBoxD bOriBBoxD)
        {
            var sVel = shieldGrid.Physics.LinearVelocity;
            var sVelSqr = sVel.LengthSquared();
            var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
            var voxelSphere = voxelMap.WorldVolume;
            //var obbSphere = new BoundingSphereD(bOriBBoxD.Center, bOriBBoxD.HalfExtent.Max());
            //var lerpedVerts = new Vector3D[642];
            var shieldGridMass = shieldGrid.Physics.Mass;
            /*
            for (int i = 0; i < 642; i++)
            {
                var newVert = Vector3D.Lerp(physicsVerts[i], bOriBBoxD.Center, -0.1d);
                lerpedVerts[i] = newVert;
            }
            */

            var voxelHitVecs = new List<Vector3D>();
            if ((sVelSqr > 0.00001 || sAvelSqr > 0.00001))// && voxelMap.GetIntersectionWithSphere(ref obbSphere))
            {
                var myvoxelmap = (MyVoxelBase)voxelMap;
                var obbSphereTest = bOriBBoxD.Intersects(ref voxelSphere);
                if (!obbSphereTest) return;
                for (int i = 0; i < 642; i++)
                {
                    var from = physicsVerts[i];
                    //var to = lerpedVerts[i];
                    //var dir = to - from;
                    //if (sAvelSqr < 1e-4f && Vector3D.Dot(dir, sVel) < 0) continue;
                    var hit = myvoxelmap.DoOverlapSphereTest(0.5f, from);
                    if (hit) voxelHitVecs.Add(from);
                    //DsDebugDraw.DrawSingleVec(from, 1f, Color.Red);
                }
            }
            for (int i = 0; i < voxelHitVecs.Count; i++) shieldGrid.Physics.ApplyImpulse((bOriBBoxD.Center - voxelHitVecs[i]) * shieldGridMass / 250, voxelHitVecs[i]);
        }


        public static void VoxelCollision(IMyCubeGrid shieldGrid, Vector3D[] physicsVerts, IMyVoxelMap voxelMap, MyOrientedBoundingBoxD bOriBBoxD)
        {
            var sVel = shieldGrid.Physics.LinearVelocity;
            var sVelSqr = sVel.LengthSquared();
            var sAvelSqr = shieldGrid.Physics.AngularVelocity.LengthSquared();
            var voxelSphere = voxelMap.WorldVolume;
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
                //var myvoxelmap = (MyVoxelBase)voxelMap;
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
                    if (hit?.HitEntity is IMyVoxelMap) voxelHitVecs.Add(hit.Position);
                    //DsDebugDraw.DrawLineToVec(from, to, Color.Black);
                }
            }
            for (int i = 0; i < voxelHitVecs.Count; i++) shieldGrid.Physics.ApplyImpulse((bOriBBoxD.Center - voxelHitVecs[i]) * shieldGridMass / 100, voxelHitVecs[i]);
        }

        public static Vector3D SmallIntersect(MyConcurrentList<IMySlimBlock> dmgBlocks, IMyCubeGrid grid, MatrixD matrix, MatrixD matrixInv)
        {
            var contactPoint = ContactPointOutside(grid, matrix);
            if (!(Vector3D.Transform(contactPoint, matrixInv).LengthSquared() <= 1)) return Vector3D.NegativeInfinity;

            var approching = Vector3.Dot(grid.Physics.LinearVelocity, grid.PositionComp.WorldVolume.Center - contactPoint) < 0;
            if (approching) grid.Physics.LinearVelocity = grid.Physics.LinearVelocity * -0.25f;
            var getBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(getBlocks);
            lock (dmgBlocks)
                for (int i = 0; i < getBlocks.Count; i++)
                {
                    var block = getBlocks[i];
                    dmgBlocks.Add(block);
                    if (dmgBlocks.Count >= 25) break;
                }
            return contactPoint;
        }

        public static Vector3D EjectDirection(IMyCubeGrid grid, Vector3D[] physicsOutside, int[][] vertTris, MyOrientedBoundingBoxD obb, MatrixD matrixInv)
        {
            var targetPos = ClosestPointInShield(obb, matrixInv);
            var gridVel = grid.Physics.LinearVelocity;
            var gridCenter = grid.PositionComp.WorldVolume.Center;
            var approching = Vector3.Dot(gridVel, gridCenter - targetPos) < 0;
            if (approching) grid.Physics.LinearVelocity = gridVel * -0.25f;
            else return Vector3D.NegativeInfinity;
            var rangedVerts = new int[3];

            VertRangeFullCheck(physicsOutside, gridCenter, rangedVerts);

            var closestFace0 = vertTris[rangedVerts[0]];
            var closestFace1 = vertTris[rangedVerts[1]];
            var closestFace2 = vertTris[rangedVerts[2]];

            var center = GetClosestTriCenter(physicsOutside, closestFace0, closestFace1, closestFace2, gridCenter);
            return center;
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

        public static void SmallIntersectDebugDraw(Vector3D[] physicsOutside, int face, int[][] vertLines, int[] rangedVert, Vector3D bWorldCenter, List<Vector3D> intersections)
        {
            //DrawNums(_physicsOutside,zone, Color.AntiqueWhite);
            DsDebugDraw.DrawLineToNum(physicsOutside, rangedVert[0], bWorldCenter, Color.Red);
            DsDebugDraw.DrawLineToNum(physicsOutside, rangedVert[1], bWorldCenter, Color.Green);
            DsDebugDraw.DrawLineToNum(physicsOutside, rangedVert[2], bWorldCenter, Color.Gold);

            int[] closestLineFace;
            switch (face)
            {
                case 0:
                    closestLineFace = vertLines[rangedVert[0]];
                    break;
                case 1:
                    closestLineFace = vertLines[rangedVert[1]];
                    break;
                default:
                    closestLineFace = vertLines[rangedVert[2]];
                    break;
            }

            var c1 = Color.Black;
            var c2 = Color.Black;
            //if (checkBackupFace1) c1 = Color.Green;
            //if (checkBackupFace2) c2 = Color.Gold;
            c1 = Color.Green;
            c2 = Color.Gold;

            DsDebugDraw.DrawLineNums(physicsOutside, closestLineFace, Color.Red);
            //DrawLineNums(_physicsOutside, closestLineFace1, c1);
            //DrawLineNums(_physicsOutside, closestLineFace2, c2);

            DsDebugDraw.DrawTriVertList(intersections);

            //DrawLineToNum(_physicsOutside, rootVerts, bWorldCenter, Color.HotPink);
            //DrawLineToNum(_physicsOutside, rootVerts[1], bWorldCenter, Color.Green);
            //DrawLineToNum(_physicsOutside, rootVerts[2], bWorldCenter, Color.Gold);
        }
    }
}

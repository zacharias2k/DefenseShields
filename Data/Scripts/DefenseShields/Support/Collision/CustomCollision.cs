using System.Collections.Generic;
using VRageMath;

namespace DefenseShields.Support
{
    internal static class CustomCollision
    {
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

        public static int[] GetAllClosestInOutTriBackup(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter, int[] triNums0, int[] triNums1, int[] triNums2)
        {
            var closestTri1 = -1;
            var closestFace = -1;
            var status = new int[2];
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
            for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
            {
                var skip = false;
                for (int f = 0; f < triNums0.Length; f++)
                {
                    if (triNums0[f] != triNums1[j]) continue;
                    skip = true;
                    break;
                }

                if (skip) continue;
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

            for (int i = 0, j = 0; i < closestFace2.Length; i += 3, j++)
            {
                var skip = false;
                for (int f = 0; f < triNums0.Length; f++)
                {
                    if (triNums0[f] != triNums2[j]) continue;
                    skip = true;
                    break;
                }
                if (skip) continue;
                for (int f = 0; f < triNums1.Length; f++)
                {
                    if (triNums1[f] != triNums2[j]) continue;
                    skip = true;
                    break;
                }
                if (skip) continue;

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

            var iv0 = physicsInside[face[closestTri1]];
            var iv1 = physicsInside[face[closestTri1 + 1]];
            var iv2 = physicsInside[face[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);

            status[0] = closestFace;
            if (triDist1 > idistTri.GetSquared()) status[1] = 1;
            return status;
        }

        public static int[] GetAllClosestInOutTri(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter)
        {
            var closestTri1 = -1;
            var closestFace = -1;
            var status = new int[5];

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

            status[2] = face[closestTri1];
            status[3] = face[closestTri1 + 1];
            status[4] = face[closestTri1 + 2];

            var iv0 = physicsInside[face[closestTri1]];
            var iv1 = physicsInside[face[closestTri1 + 1]];
            var iv2 = physicsInside[face[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);

            status[0] = closestFace;
            if (triDist1 > idistTri.GetSquared()) status[1] = 1;

            return status;
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

        public static int[] VertRangeFullCheck(Vector3D[] physicsVerts, Vector3D bWorldCenter)
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
            return new[] { minNum1, minNum2, minNum3 };
        }
    }
}

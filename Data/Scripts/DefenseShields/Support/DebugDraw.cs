using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace DefenseShields.Support
{
    public static class DsDebugDraw
    {
        #region Debug and Utils
        public static int GetVertNum(Vector3D[] physicsVerts, Vector3D vec)
        {
            var pmatch = false;
            var pNum = -1;
            foreach (var pvert in physicsVerts)
            {
                pNum++;
                if (vec == pvert) pmatch = true;
                if (pmatch) return pNum;
            }
            return pNum;
        }

        public static void FindRoots(Vector3D[] physicsVerts, Vector3D[] rootVerts)
        {
            for (int i = 0, j = 0; i < physicsVerts.Length; i++, j++)
            {
                var vec = physicsVerts[i];
                foreach (var magic in rootVerts)
                {
                    for (int num = 0; num < 12; num++)
                    {
                        if (vec == magic && rootVerts[num] == vec) Log.Line($"Found root {num} at index: {i}");
                    }

                }
            }
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

        public static bool[] GetZonesContaingNum(Vector3D[] physicsVerts, Vector3D[] rootVerts, int[][] rZone, int[][] sZone, int[][] mZone, Color[] zColors, int locateVertNum, int size, bool draw = false)
        {
            // 1 = p3SmallZones, 2 = p3MediumZones, 3 = p3LargeZones, 4 = p3LargestZones
            var root = rZone;
            var small = sZone;
            var medium = mZone;
            var zone = size == 1 ? small : medium;
            if (size == 0) zone = root;
            var zMatch = new bool[12];

            for (int i = 0; i < zone.Length; i++)
            {
                foreach (var vertNum in zone[i])
                {
                    if (vertNum == locateVertNum) zMatch[i] = true;
                }
            }
            if (draw)
            {
                var c = 0;
                var j = 0;
                foreach (var z in zMatch)
                {
                    if (z)
                    {
                        DrawNums(physicsVerts, rootVerts, zColors, zone[c]);
                    }
                    c++;
                }
            }

            return zMatch;
        }

        public static int[] FindClosestZoneToVec(Vector3D[] physicsVerts, int[][] rZone, int[][] sZone, int[][] mZone, Vector3D locateVec, int size)
        {
            // 1 = p3SmallZones, 2 = p3MediumZones, 3 = p3LargeZones, 4 = p3LargestZones
            var root = rZone;
            var small = sZone;
            var medium = mZone;
            var zone = size == 1 ? small : medium;
            if (size == 0) zone = root;

            var zoneNum = -1;
            var tempNum = -1;
            var tempVec = Vector3D.Zero;
            double pNumDistance = 9999999999999999999;

            for (int i = 0; i < physicsVerts.Length; i++)
            {
                var v = physicsVerts[i];
                if (v != locateVec) continue;
                tempVec = v;
                tempNum = i;
            }
            var c = 0;
            foreach (int[] numArray in zone)
            {
                foreach (var vertNum in numArray)
                {
                    if (vertNum != tempNum) continue;
                    var distCheck = Vector3D.DistanceSquared(locateVec, tempVec);
                    if (!(distCheck < pNumDistance)) continue;
                    pNumDistance = distCheck;
                    zoneNum = c;
                }
                c++;
            }
            return zone[zoneNum];
        }

        public static void DrawTriNumArray(Vector3D[] physicsVerts, int[] array)
        {
            var lineId = MyStringId.GetOrCompute("Square");
            var c = Color.Red.ToVector4();

            for (int i = 0; i < array.Length; i += 3)
            {
                var vn0 = array[i];
                var vn1 = array[i + 1];
                var vn2 = array[i + 2];

                var v0 = physicsVerts[vn0];
                var v1 = physicsVerts[vn1];
                var v2 = physicsVerts[vn2];

                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v0, v2, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v1, v2, lineId, ref c, 0.25f);

            }
        }

        public static void DrawTriVertList(List<Vector3D> list)
        {
            var lineId = MyStringId.GetOrCompute("Square");
            var c = Color.DarkViolet.ToVector4();
            for (int i = 0; i < list.Count; i += 3)
            {
                var v0 = list[i];
                var v1 = list[i + 1];
                var v2 = list[i + 2];

                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v0, v2, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v1, v2, lineId, ref c, 0.25f);

            }
        }

        public static void DrawLineNums(Vector3D[] physicsVerts, int[] lineArray, Color color)
        {
            var c = color.ToVector4();
            var lineId = MyStringId.GetOrCompute("Square");

            for (int i = 0; i < lineArray.Length; i += 2)
            {
                var v0 = physicsVerts[lineArray[i]];
                var v1 = physicsVerts[lineArray[i + 1]];
                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
            }
        }

        public static void DrawLineToNum(Vector3D[] physicsVerts, int num, Vector3D fromVec, Color color)
        {
            var c = color.ToVector4();
            var lineId = MyStringId.GetOrCompute("Square");

            var v0 = physicsVerts[num];
            var v1 = fromVec;
            MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
        }

        public static void DrawLineToVec(Vector3D toVec, Vector3D fromVec, Color color, float lineWidth)
        {
            var c = color.ToVector4();
            var lineId = MyStringId.GetOrCompute("Square");

            var v0 = toVec;
            var v1 = fromVec;
            MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, lineWidth);
        }
        public static void DrawRootVerts(Vector3D[] rootVerts, Color[] zoneColors)
        {
            var i = 0;
            foreach (var root in rootVerts)
            {
                var rootColor = zoneColors[i];
                DrawVertCollection(root, 5, rootColor, 20);
                i++;
            }
        }

        private static void DrawVerts(Vector3D[] list, Vector3D[] rootVerts, Color[] zoneColors, Color color = default(Color))
        {
            var i = 0;
            foreach (var vec in list)
            {
                var rootColor = zoneColors[i];
                if (vec == rootVerts[i]) color = rootColor;
                DrawVertCollection(vec, 5, color, 8);
                i++;
            }
        }

        private static void DrawNums(Vector3D[] physicsVerts, Vector3D[] rootVerts, Color[] zoneColors, int[] list, Color color = default(Color))
        {
            foreach (var num in list)
            {
                var i = 0;
                foreach (var root in rootVerts)
                {
                    var rootColor = zoneColors[i];
                    if (physicsVerts[num] == root) color = rootColor;
                    i++;
                }
                DrawVertCollection(physicsVerts[num], 5, color, 8);
            }
        }

        public static void DrawSingleNum(Vector3D[] physicsVerts, int num)
        {
            //Log.Line($"magic: {magic}");
            var c = Color.Black;
            DrawVertCollection(physicsVerts[num], 7, c, 20);
        }

        public static void DrawBox(MyOrientedBoundingBoxD obb, Color color, bool shield, MatrixD matrix = default(MatrixD))
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromTransformScale(obb.Orientation, obb.Center, Vector3D.One);
            //if (shield) wm = wm * _shieldGridMatrix;
            //else wm = wm * matrix;
            //wm = wm * Block.WorldMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, MySimpleObjectRasterizer.Solid, 1);
        }

        public static void DrawObb(MatrixD matrix, MyOrientedBoundingBoxD obb, Color color)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref box, ref color, MySimpleObjectRasterizer.Solid, 1);
        }

        public static void DrawBox3(MatrixD matrix, BoundingBoxD box, Color color)
        {
            MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref box, ref color, MySimpleObjectRasterizer.Solid, 1, 8f, MyStringId.GetOrCompute("square"));
        }


        public static void DrawSingleVec(Vector3D vec, float size, Color color)
        {
            DrawVertCollection(vec, size, color, 20);
        }

        public static void DrawVertCollection(Vector3D collision, double radius, Color color, int lineWidth = 1)
        {
            var posMatCenterScaled = MatrixD.CreateTranslation(collision);
            var posMatScaler = MatrixD.Rescale(posMatCenterScaled, radius);
            var rangeGridResourceId = MyStringId.GetOrCompute("Build new");
            MySimpleObjectDraw.DrawTransparentSphere(ref posMatScaler, 1f, ref color, MySimpleObjectRasterizer.Solid, lineWidth, null, rangeGridResourceId, -1, -1);
        }

        public static void DrawSphere(BoundingSphereD sphere, Color color)
        {
            var rangeGridResourceId = MyStringId.GetOrCompute("Build new");
            var radius = sphere.Radius;
            var transMatrix = MatrixD.CreateTranslation(sphere.Center);
            //var wm = MatrixD.Rescale(transMatrix, radius);

            MySimpleObjectDraw.DrawTransparentSphere(ref transMatrix, (float)radius, ref color, MySimpleObjectRasterizer.Solid, 20, null, rangeGridResourceId, -1, -1);
        }
        #endregion
    }
}

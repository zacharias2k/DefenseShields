using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DefenseShields.Support
{
    public class StructureBuilder
    {
        /*
        private readonly int[] _rootNums0 = { 0, 1, 2, 4, 7, 11, 16, 19, 25, 28, 34, 37 };
        private readonly int[] _rootNums1 = { 0, 3, 6, 15, 27, 42, 63, 75, 99, 111, 135, 147 };
        private readonly int[] _rootNums2 = { 0, 12, 24, 60, 108, 168, 252, 300, 396, 444, 540, 588 };
        private readonly int[] _rootNums3 = { 0, 48, 96, 240, 432, 672, 1008, 1200, 1584, 1776, 2160, 2352 };

        private readonly int[] _icoVerts0 =
        {
            0, 1, 2, 4, 7, 11, 16, 19, 25, 28, 34, 37
        };

        private readonly int[] _icoVerts1 =
        {
            0, 1, 2, 3, 4, 6, 13, 15, 16, 25, 27, 28, 38, 40, 42, 52, 61, 63, 64, 73, 75, 76, 85, 97, 99, 100, 109, 111,
            112, 121, 133, 135, 136, 145, 147, 148, 157, 160, 172, 194, 206, 208
        };

        private readonly int[] _icoVerts2 =
        {
            0, 1, 2, 3, 4, 6, 12, 13, 14, 15, 16, 24, 25, 26, 28, 49, 51, 52, 60, 61, 62, 63, 64, 74, 76, 97, 99, 100,
            108, 109, 110, 111, 112, 122, 124, 146, 148, 150, 157, 159, 160, 168, 169, 170, 172, 196, 205, 207, 208,
            218, 220, 241, 243, 244, 252, 253, 254, 255, 256, 266, 268, 289, 291, 292, 300, 301, 302, 303, 304, 314,
            316, 337, 339, 340, 350, 352, 364, 385, 387, 388, 396, 397, 398, 399, 400, 410, 412, 433, 435, 436, 444,
            445, 446, 447, 448, 458, 460, 481, 483, 484, 494, 496, 508, 529, 531, 532, 540, 541, 542, 543, 544, 554,
            556, 577, 579, 580, 588, 589, 590, 591, 592, 602, 604, 625, 627, 628, 637, 638, 639, 640, 650, 652, 676,
            685, 687, 688, 698, 700, 724, 736, 748, 770, 772, 774, 784, 793, 796, 818, 820, 822, 829, 831, 832, 841,
            842, 844, 868, 880, 892, 916, 928, 940
        };
        */
        private readonly int[] _icoVerts3 =
        {
            0, 1, 2, 3, 4, 6, 12, 13, 14, 15, 16, 24, 25, 26, 28, 48, 49, 50, 51, 52, 54, 60, 61, 62, 63, 64, 73, 74,
            76, 96, 97, 98, 99, 100, 102, 109, 110, 111, 112, 121, 122, 124, 148, 160, 172, 193, 195, 196, 204, 205,
            206, 207, 208, 218, 220, 240, 241, 242, 243, 244, 246, 252, 253, 254, 255, 256, 265, 266, 268, 290, 292,
            294, 301, 303, 304, 313, 314, 316, 340, 352, 364, 385, 387, 388, 396, 397, 398, 399, 400, 410, 412, 432,
            433, 434, 435, 436, 438, 444, 445, 446, 447, 448, 457, 458, 460, 482, 484, 486, 493, 495, 496, 505, 506,
            508, 532, 544, 556, 578, 580, 582, 589, 591, 592, 600, 601, 602, 604, 625, 627, 628, 636, 637, 638, 639,
            640, 650, 652, 672, 673, 674, 675, 676, 678, 685, 686, 687, 688, 697, 698, 700, 724, 736, 748, 772, 781,
            783, 784, 794, 796, 817, 819, 820, 828, 829, 830, 831, 832, 842, 844, 866, 868, 870, 877, 879, 880, 889,
            890, 892, 916, 928, 940, 961, 963, 964, 972, 973, 974, 975, 976, 986, 988, 1008, 1009, 1010, 1011, 1012,
            1014, 1020, 1021, 1022, 1023, 1024, 1033, 1034, 1036, 1058, 1060, 1062, 1069, 1071, 1072, 1081, 1082, 1084,
            1108, 1120, 1132, 1153, 1155, 1156, 1164, 1165, 1166, 1167, 1168, 1178, 1180, 1200, 1201, 1202, 1203, 1204,
            1206, 1212, 1213, 1214, 1215, 1216, 1225, 1226, 1228, 1250, 1252, 1254, 1261, 1263, 1264, 1273, 1274, 1276,
            1300, 1312, 1324, 1345, 1347, 1348, 1356, 1357, 1358, 1359, 1360, 1370, 1372, 1394, 1396, 1398, 1405, 1407,
            1408, 1417, 1418, 1420, 1444, 1453, 1455, 1456, 1466, 1468, 1492, 1504,
            1516, 1537, 1539, 1540, 1548, 1549, 1550, 1551, 1552, 1562, 1564, 1584, 1585, 1586, 1587, 1588, 1590, 1596,
            1597, 1598, 1599, 1600, 1609, 1610, 1612, 1634, 1636, 1638, 1645, 1647, 1648, 1657, 1658, 1660, 1684, 1696,
            1708, 1729, 1731, 1732, 1740, 1741, 1742, 1743, 1744, 1754, 1756, 1776, 1777, 1778, 1779, 1780, 1782, 1788,
            1789, 1790, 1791, 1792, 1801, 1802, 1804, 1826, 1828, 1830, 1837, 1839, 1840, 1849, 1850, 1852, 1876, 1888,
            1900, 1921, 1923, 1924, 1932, 1933, 1934, 1935, 1936, 1946, 1948, 1970, 1972, 1974, 1981, 1983, 1984, 1993,
            1994, 1996, 2020, 2029, 2031, 2032, 2042, 2044, 2068, 2080, 2092, 2113, 2115, 2116, 2124, 2125, 2126, 2127,
            2128, 2138, 2140, 2160, 2161, 2162, 2163, 2164, 2166, 2172, 2173, 2174, 2175, 2176, 2185, 2186, 2188, 2210,
            2212, 2214, 2221, 2223, 2224, 2233, 2234, 2236, 2260, 2272, 2284, 2305, 2307, 2308, 2316, 2317, 2318, 2319,
            2320, 2330, 2332, 2352, 2353, 2354, 2355, 2356, 2358, 2364, 2365, 2366, 2367, 2368, 2377, 2378, 2380, 2402,
            2404, 2406, 2413, 2415, 2416, 2425, 2426, 2428, 2452, 2464, 2476, 2497, 2499, 2500, 2508, 2509, 2510, 2511,
            2512, 2522, 2524, 2545, 2546, 2547, 2548, 2550, 2556, 2557, 2558, 2559, 2560, 2569, 2570, 2572, 2594, 2596,
            2598, 2605, 2607, 2608, 2617, 2618, 2620, 2644, 2656, 2668, 2692, 2701, 2703, 2704, 2714, 2716, 2737, 2739,
            2740, 2748, 2749, 2750, 2751, 2752, 2762, 2764, 2786, 2788, 2790, 2797, 2799, 2800, 2809, 2810, 2812, 2836,
            2848, 2860, 2884, 2893, 2895, 2896, 2906, 2908, 2932, 2941, 2943, 2944, 2954, 2956, 2980, 2989, 2991, 2992,
            3002, 3004, 3028, 3040, 3052, 3074, 3076, 3078, 3085, 3087, 3088, 3096, 3097, 3098, 3100, 3124, 3133, 3135,
            3136, 3146, 3148, 3169, 3171, 3172, 3181, 3182, 3183, 3184, 3194, 3196, 3220, 3232, 3244, 3266, 3268, 3270,
            3277, 3279, 3280, 3288, 3289, 3290, 3292, 3313, 3315, 3316, 3324, 3325, 3326, 3327, 3328, 3338, 3340, 3361,
            3362, 3363, 3364, 3366, 3373, 3374, 3375, 3376, 3385, 3386, 3388, 3412, 3424, 3436, 3460, 3469, 3471, 3472,
            3482, 3484, 3508, 3517, 3519, 3520, 3530, 3532, 3556, 3565, 3567, 3568, 3578, 3580, 3604, 3616, 3628, 3652,
            3661, 3663, 3664, 3674, 3676, 3700, 3709, 3711, 3712, 3722, 3724, 3748, 3757, 3759, 3760, 3770, 3772, 3796,
            3808, 3820,
        };

        //private readonly DefenseShields _ds = new DefenseShields();
        private readonly HashSet<Vector3D> _z0 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z1 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z2 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z3 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z4 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z5 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z6 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z7 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z8 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z9 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z10 = new HashSet<Vector3D>();
        private readonly HashSet<Vector3D> _z11 = new HashSet<Vector3D>();

        private readonly List<LineD> _vecPrunedLinesList = new List<LineD>();
        private readonly HashSet<LineD> _vecPrunedLinesHash = new HashSet<LineD>();

        public void BuildTriNums(Vector3D[] shieldTris, Vector3D[] physicsVerts)
        {
            Log.CleanLine($"public int[][] p3TriNums = new int[1280][]");
            Log.CleanLine($"{{");

            var closestTrisNums = new HashSet<int>();
            foreach (var v in physicsVerts)
            {
                closestTrisNums.Clear();
                for (int x = 0, j = 0; x < shieldTris.Length; x += 3, j++)
                {
                    var v0 = shieldTris[x];
                    var v1 = shieldTris[x + 1];
                    var v2 = shieldTris[x + 2];
                    if (v0 == v || v1 == v || v2 == v)
                    {
                        closestTrisNums.Add(j);
                    }
                }
                var len = closestTrisNums.Count;
                var c = 0;
                Log.Chars($"new[] {{");
                foreach (var tri in closestTrisNums)
                {
                    if (c < len -1) Log.Chars($"{tri},");
                    if (c == len - 1) Log.Chars($"{tri}");
                    c++;
                }
                Log.CleanLine($"}},");
            }
            Log.CleanLine($"}};");
        }

        private void PrintTrisNumPerVert(HashSet<int> ClosestTrisNums, Vector3D[] shieldTris, Vector3D[] physicsVerts, bool last)
        {
            var c = 0;

            Log.CleanLine($"\n  new[] {{");
            foreach (var tri in ClosestTrisNums)
            {
                c++;
                Log.Chars($"{tri},");
            }
            Log.CleanLine($"}}\n");
            /*
            for (int i = 0, j = 0; i < numFiveClosestTris.Count; i++, j++)
            {
                c++;
                var vn0 = numFiveClosestTris[i];
                var vn1 = numFiveClosestTris[i + 1];
                var vn2 = numFiveClosestTris[i + 2];
                var v0 = shieldTris[vn0];
                var v1 = shieldTris[vn1];
                var v2 = shieldTris[vn2];

                if (c < numFiveClosestTris.Count - 3)
                    Log.Chars($"{GetVertNum(v0, physicsVerts)}, {GetVertNum(v1, physicsVerts)}, {GetVertNum(v2, physicsVerts)}, ");
                if (c == numFiveClosestTris.Count - 3)
                    Log.Chars($"{GetVertNum(v0, physicsVerts)}, {GetVertNum(v1, physicsVerts)}, {GetVertNum(v2, physicsVerts)}");
            }
            if (last == false) Log.Chars($" }},\n");
            if (last) Log.Chars($" }}\n");
            if (last) Log.CleanLine($"}};");
            */
        }

        public void BuildBase(Vector3D[] shieldTris, Vector3D[] rootVecs, Vector3D[] physicsVerts, bool buildLines, bool buildTris, bool buildVertZones, bool buildByVerts)
        {
            //_ds._buildOnce = true;
            if (!buildTris && !(buildLines | buildVertZones)) return;
            var numFiveClosestTris = new List<int>();
            var vecPrunedLines = new HashSet<LineD>();
            var numL1VertsToRoots = new HashSet<Vector3D>();
            var numL2VertsToRoots = new HashSet<Vector3D>();
            var numL3VertsToRoots = new HashSet<Vector3D>();
            var numL4VertsToRoots = new HashSet<Vector3D>();
            var numL5VertsToRoots = new HashSet<Vector3D>();
            var numL6VertsToRoots = new HashSet<Vector3D>();
            var numL7VertsToRoots = new HashSet<Vector3D>();
            var numL8VertsToRoots = new HashSet<Vector3D>();
            var numL9VertsToRoots = new HashSet<Vector3D>();
            var numL10VertsToRoots = new HashSet<Vector3D>();
            var numL11VertsToRoots = new HashSet<Vector3D>();

            var last = false;
            var c = 0;

            if (buildVertZones) Log.CleanLine($"public int[][] p3SmallZones = new int[12][]");
            if (buildLines) Log.CleanLine($"public int[][] p3VertLines = new int[642][]");
            if (buildTris) Log.CleanLine($"public int[][] p3VertTris = new int[642][]");
            Log.CleanLine($"{{");

            if (buildVertZones)
            {
                var count = 0;
                foreach (var l1 in rootVecs)
                {
                    numL1VertsToRoots.Clear();
                    numL2VertsToRoots.Clear();
                    numL3VertsToRoots.Clear();
                    numL4VertsToRoots.Clear();
                    numL5VertsToRoots.Clear();
                    numL6VertsToRoots.Clear();
                    numL7VertsToRoots.Clear();
                    numL8VertsToRoots.Clear();
                    numL9VertsToRoots.Clear();
                    numL10VertsToRoots.Clear();
                    numL11VertsToRoots.Clear();

                    for (int i = 0; i < shieldTris.Length; i += 3)
                    {
                        var v0 = shieldTris[i];
                        var v1 = shieldTris[i + 1];
                        var v2 = shieldTris[i + 2];

                        if (v0 == l1 || v1 == l1 || v2 == l1)
                        {
                            numL1VertsToRoots.Add(l1);
                            numL1VertsToRoots.Add(v0);
                            numL1VertsToRoots.Add(v1);
                            numL1VertsToRoots.Add(v2);
                        }
                    }
                    foreach (var l in numL1VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);

                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL2VertsToRoots.Add(l);
                                numL2VertsToRoots.Add(v0);
                                numL2VertsToRoots.Add(v1);
                                numL2VertsToRoots.Add(v2);
                            }
                        }
                    }

                    foreach (var l in numL2VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);

                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL3VertsToRoots.Add(l);
                                numL3VertsToRoots.Add(v0);
                                numL3VertsToRoots.Add(v1);
                                numL3VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL3VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL4VertsToRoots.Add(l);
                                numL4VertsToRoots.Add(v0);
                                numL4VertsToRoots.Add(v1);
                                numL4VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL4VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL5VertsToRoots.Add(l);
                                numL5VertsToRoots.Add(v0);
                                numL5VertsToRoots.Add(v1);
                                numL5VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL5VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL6VertsToRoots.Add(l);
                                numL6VertsToRoots.Add(v0);
                                numL6VertsToRoots.Add(v1);
                                numL6VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL6VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL7VertsToRoots.Add(l);
                                numL7VertsToRoots.Add(v0);
                                numL7VertsToRoots.Add(v1);
                                numL7VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL7VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL8VertsToRoots.Add(l);
                                numL8VertsToRoots.Add(v0);
                                numL8VertsToRoots.Add(v1);
                                numL8VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL8VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL9VertsToRoots.Add(l);
                                numL9VertsToRoots.Add(v0);
                                numL9VertsToRoots.Add(v1);
                                numL9VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL9VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL10VertsToRoots.Add(l);
                                numL10VertsToRoots.Add(v0);
                                numL10VertsToRoots.Add(v1);
                                numL10VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL10VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                        for (int i = 0; i < shieldTris.Length; i += 3)
                        {
                            var v0 = shieldTris[i];
                            var v1 = shieldTris[i + 1];
                            var v2 = shieldTris[i + 2];

                            if (v0 == l || v1 == l || v2 == l)
                            {
                                numL11VertsToRoots.Add(l);
                                numL11VertsToRoots.Add(v0);
                                numL11VertsToRoots.Add(v1);
                                numL11VertsToRoots.Add(v2);
                            }
                        }
                    }
                    foreach (var l in numL11VertsToRoots)
                    {
                        if (count == 0) _z0.Add(l);
                        if (count == 1) _z1.Add(l);
                        if (count == 2) _z2.Add(l);
                        if (count == 3) _z3.Add(l);
                        if (count == 4) _z4.Add(l);
                        if (count == 5) _z5.Add(l);
                        if (count == 6) _z6.Add(l);
                        if (count == 7) _z7.Add(l);
                        if (count == 8) _z8.Add(l);
                        if (count == 9) _z9.Add(l);
                        if (count == 10) _z10.Add(l);
                        if (count == 11) _z11.Add(l);
                    }
                    count++;
                }
                var pNum = 0;
                for (int i = 0; i < 12; i++)
                {
                    switch (pNum)
                    {
                        case 0:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z0)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 1:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z1)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 2:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z2)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 3:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z3)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 4:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z4)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 5:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z5)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 6:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z6)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 7:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z7)
                            {
                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, count);
                            break;
                        }
                        case 8:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z8)
                            {

                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, pNum);
                            break;
                        }
                        case 9:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z9)
                            {

                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, pNum);
                            break;
                        }
                        case 10:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z10)
                            {

                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, pNum);
                            break;
                        }
                        case 11:
                        {
                            var tempZoneList = new List<int>();

                            foreach (var vec in _z11)
                            {

                                var vNum = GetVertNum(vec, physicsVerts);
                                tempZoneList.Add(vNum);
                            }
                            PrintZoneNumArray(tempZoneList, last, pNum);
                            break;
                        }
                    }
                    pNum++;
                }
                Log.CleanLine($"}};");
            }

            if (buildTris || buildLines)
            {
                foreach (var v in physicsVerts)
                {
                    c++;
                    if (c == physicsVerts.Length) last = true;
                    numFiveClosestTris.Clear();
                    vecPrunedLines.Clear();
                    var vert = v;
                    for (int x = 0; x < shieldTris.Length; x += 3)
                    {
                        var v0 = shieldTris[x];
                        var v1 = shieldTris[x + 1];
                        var v2 = shieldTris[x + 2];
                        if (v0 == vert || v1 == vert || v2 == vert)
                        {
                            numFiveClosestTris.Add(x);
                            numFiveClosestTris.Add(x + 1);
                            numFiveClosestTris.Add(x + 2);
                        }
                    }
                    if (buildTris) PrintTriNumArray(numFiveClosestTris, shieldTris, physicsVerts, buildByVerts, last);

                    if (!buildLines) continue;
                    for (int i = 0; i < numFiveClosestTris.Count; i += 3)
                    {
                        var tri00 = numFiveClosestTris[i];
                        var tri01 = numFiveClosestTris[i + 1];
                        var tri02 = numFiveClosestTris[i + 2];

                        var tri0Line1 = new LineD(shieldTris[tri00], shieldTris[tri01]);
                        var tri0Line2 = new LineD(shieldTris[tri00], shieldTris[tri02]);
                        var tri0Line3 = new LineD(shieldTris[tri01], shieldTris[tri02]);

                        var etri0Line1 = new LineD(shieldTris[tri01], shieldTris[tri00]);
                        var etri0Line2 = new LineD(shieldTris[tri02], shieldTris[tri00]);
                        var etri0Line3 = new LineD(shieldTris[tri02], shieldTris[tri01]);

                        if (!vecPrunedLines.Contains(etri0Line1)) vecPrunedLines.Add(tri0Line1);
                        if (!vecPrunedLines.Contains(etri0Line2)) vecPrunedLines.Add(tri0Line2);
                        if (!vecPrunedLines.Contains(etri0Line3)) vecPrunedLines.Add(tri0Line3);
                    }
                    PrintLineNumArray(vecPrunedLines, shieldTris, physicsVerts, buildByVerts, last);
                }
                Log.Line($"List: {_vecPrunedLinesList.Count} - Hash: {_vecPrunedLinesHash.Count}");
            }
        }

        private int GetVertNum(Vector3D vec, Vector3D[] physicsVerts)
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

        private void PrintZoneNumArray(List<int> zoneList, bool last, int zone)
        {
            Log.Chars($"new int[] {{ ");
            var lNum = 0;
            for (int i = 0; i < zoneList.Count; i++)
            {
                var num = zoneList[i];
                if (i < zoneList.Count - 1) Log.Chars($"{num}, ");
                if (i == zoneList.Count - 1) Log.Chars($"{num}");
                if (lNum == 24)
                {
                    Log.Chars("\n");
                    lNum = 0;
                }
                lNum++;
            }
            Log.Chars($" }},\n");
        }

        private void PrintLineNumArray(HashSet<LineD> prunedLinesHash, Vector3D[] shieldTris, Vector3D[] physicsVerts, bool buildByVerts, bool last)
        {
            Log.Chars($"new int[] {{ ");
            var c = 0;
            foreach (var lines in prunedLinesHash)
            {
                var from = lines.From;
                var to = lines.To;

                if (buildByVerts)
                {
                    if (c < prunedLinesHash.Count - 1) Log.Chars($"{GetVertNum(from, physicsVerts)}, {GetVertNum(to, physicsVerts)}, ");
                    if (c == prunedLinesHash.Count - 1) Log.Chars($"{GetVertNum(from, physicsVerts)}, {GetVertNum(to, physicsVerts)}");
                    _vecPrunedLinesList.Add(lines);
                    _vecPrunedLinesHash.Add(lines);
                }
                else
                {
                    var fromNum = -1;
                    var toNum = -1;

                    for (int i = 0; i < _icoVerts3.Length; i++)
                    {
                        var num = _icoVerts3[i];
                        var vec = shieldTris[num];
                        if (from == vec) fromNum = num;
                        if (to == vec) toNum = num;
                    }

                    if (c < prunedLinesHash.Count - 1) Log.Chars($"{fromNum}, {toNum}, ");
                    if (c == prunedLinesHash.Count - 1) Log.Chars($"{fromNum}, {toNum}");
                    _vecPrunedLinesList.Add(lines);
                    _vecPrunedLinesHash.Add(lines);
                }
                c++;
            }
            if (last == false) Log.Chars($" }},\n");
            if (last) Log.Chars($" }}\n");
            if (last) Log.CleanLine($"}};");

        }

        private void PrintTriNumArray(List<int> numFiveClosestTris, Vector3D[] shieldTris, Vector3D[] physicsVerts, bool buildByVerts, bool last)
        {
            Log.Chars($"new int[] {{ ");
            var c = 0;
            for (int i = 0; i < numFiveClosestTris.Count; i += 3)
            {
                c++;
                var vn0 = numFiveClosestTris[i];
                var vn1 = numFiveClosestTris[i + 1];
                var vn2 = numFiveClosestTris[i + 2];
                if (buildByVerts)
                {
                    var v0 = shieldTris[vn0];
                    var v1 = shieldTris[vn1];
                    var v2 = shieldTris[vn2];

                    if (c < numFiveClosestTris.Count - 3)
                        Log.Chars($"{GetVertNum(v0, physicsVerts)}, {GetVertNum(v1, physicsVerts)}, {GetVertNum(v2, physicsVerts)}, ");
                    if (c == numFiveClosestTris.Count - 3)
                        Log.Chars($"{GetVertNum(v0, physicsVerts)}, {GetVertNum(v1, physicsVerts)}, {GetVertNum(v2, physicsVerts)}");
                }
                else
                {
                    if (c < numFiveClosestTris.Count - 3) Log.Chars($"{vn0}, {vn1}, {vn2}, ");
                    if (c == numFiveClosestTris.Count - 3) Log.Chars($"{vn0}, {vn1}, {vn2}");
                }
            }
            if (last == false) Log.Chars($" }},\n");
            if (last) Log.Chars($" }}\n");
            if (last) Log.CleanLine($"}};");
        }
    }
}

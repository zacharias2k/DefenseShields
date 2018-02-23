using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
using ProtoBuf;
using System;
using Sandbox.ModAPI.Weapons;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Entity;
using System.Linq;
using DefenseShields.Control;
using VRage.Collections;
using Sandbox.Game.Entities.Character.Components;
using DefenseShields.Support;
using ParallelTasks;
using Sandbox.Game.Entities;
using VRage;
using VRageRender;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "StationDefenseShield")]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private readonly int[] _magicNums0 = { 0, 1, 2, 4, 7, 11, 16, 19, 25, 28, 34, 37 };
        private readonly int[] _magicNums1 = { 0, 3, 6, 15, 27, 42, 63, 75, 99, 111, 135, 147 };
        private readonly int[] _magicNums2 = { 0, 12, 24, 60, 108, 168, 252, 300, 396, 444, 540, 588 };
        private readonly int[] _magicNums3 = { 0, 48, 96, 240, 432, 672, 1008, 1200, 1584, 1776, 2160, 2352 };

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

        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;
        private const float InOutSpace = 15f;

        private float _power = 0.0001f;
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;
        private float _impactSize;

        private const int PhysicsLod = 3;

        private int _count = -1;
        private int _testingCown = -200;
        private int _explodeCount;
        private int _time;
        private int _playertime;
        private int _prevLod;

        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _initialized;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _closegrids;
        private bool _playerkill;
        private bool _gridIsMobile;
        private bool _explode;
        private bool buildOnce;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _detectionCenter;
        private Vector3D _shieldSize;

        private Vector3D[] _shieldTris;
        private Vector3D[] _magicVecs = new Vector3D[12];
        private Vector3D[] _physicsVerts = new Vector3D[642];

        private Vector3D[] m0 = new Vector3D[5];
        private Vector3D[] m1 = new Vector3D[5];
        private Vector3D[] m2 = new Vector3D[5];
        private Vector3D[] m3 = new Vector3D[5];
        private Vector3D[] m4 = new Vector3D[5];
        private Vector3D[] m5 = new Vector3D[5];
        private Vector3D[] m6 = new Vector3D[5];
        private Vector3D[] m7 = new Vector3D[5];
        private Vector3D[] m8 = new Vector3D[5];
        private Vector3D[] m9 = new Vector3D[5];
        private Vector3D[] m10 = new Vector3D[5];
        private Vector3D[] m11 = new Vector3D[5];



        private double[] _shieldRanged;

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;
        private MatrixD _mobileMatrix;

        private BoundingBox _oldGridAabb;

        private IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;

        private MyEntitySubpart _subpartRotor;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;

        private MyResourceSinkComponent _sink;
        private readonly MyDefinitionId _powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash { get; } = new MyConcurrentHashSet<IMyEntity>();
        private MyConcurrentHashSet<IMySlimBlock> DmgBlocks { get; } = new MyConcurrentHashSet<IMySlimBlock>();

        private List<IMyCubeGrid> GridIsColliding = new List<IMyCubeGrid>();

        public static HashSet<IMyEntity> DestroyPlayerHash { get; } = new HashSet<IMyEntity>();
        public readonly HashSet<LineD> vecPrunedLinesTest = new HashSet<LineD>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private MatrixD DetectionMatrix
        {
            get { return _detectionMatrix; }
            set
            {
                _detectionMatrix = value;
                _detectionMatrixInv = MatrixD.Invert(value);
            }
        }

        public MyResourceSinkComponent Sink { get { return _sink; } set { _sink = value; } }

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; } 
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        #endregion

        // temp
        private bool needsMatrixUpdate = false;
        public DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        private bool blocksNeedRefresh = false;
        public const float MIN_SCALE = 15f; // Scale slider min/max
        public const float MAX_SCALE = 300f;
        public float LargestGridLength = 2.5f;
        public static MyModStorageComponent Storage { get; set; }
        private HashSet<ulong> playersToReceive = null;
        // 

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out _sink);
            _sink.SetRequiredInputFuncByType(_powerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            //DSUtils.Sw.Start();
            try
            {
                if (_count++ == 59) _count = 0;
                if (_explode && _explodeCount++ == 14) _explodeCount = 0;
                if (_explodeCount == 0 && _explode) _explode = false;

                if (_count <= 0)
                {
                    if (!_initialized)
                    {
                        _count = -1;
                        //InHashBuilder();
                        return;
                    }
                    //InHashBuilder();
                }
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && _initialized && Block.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_count == 0) _enablePhysics = false;
                if (_enablePhysics == false) QuickWebCheck();
                if (_gridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); 
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation));
                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = entAngularVelocity || entLinVel || _gridChanged;
                    //if (_entityChanged || _gridChanged) Log.Line($"Entity Change Loop ec:{_entityChanged} gc:{_gridChanged} vel:{entLinVel} avel:{entAngularVelocity}");
                    if (_entityChanged || _range <= 0) CreateShieldMatrices();
                }
                if (Block.CubeGrid.Physics.IsStatic) _entityChanged = RefreshDimensions();
                if (_enablePhysics && _initialized && Block.IsWorking)
                {
                    BuildPhysicsArrays();
                }
                if (!_initialized || !Block.IsWorking) return;
                //GridKillField();
                DamageGrids();
                //if (_enablePhysics) MyAPIGateway.Parallel.StartBackground(WebEntities);
                //if (_enablePhysics) WebEntities();

                if (_playerwebbed && _enablePhysics) PlayerEffects();
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
            //DSUtils.StopWatchReport("Main loop", -1);
        }

        private void BuildPhysicsArrays()
        {
            _shieldTris = _icosphere.CalculatePhysics(DetectionMatrix, PhysicsLod);

            //if (_count == 0) DSUtils.Sw.Start();
            /*
            var i = 0;
            foreach (var vertNum in _icoVerts3)
            {
                var vert = _shieldTris[vertNum];
                _physicsVerts[i] = vert;
                i++;
                //if (_count == 0) Log.Line($"i: {i} num: {vertNum} - vert: {vert}");
            }
            */
            //if (_count == 0) Log.Line($"{_icoVerts3.Length}");
            var test = _icosphere.CalculatePhysics(DetectionMatrix, 3);
            /*

            var n = 0;
            foreach (var num in _magicNums1)
            {
                //Log.Line($"Number Order {num}");
                _magicVecs[n] = test[num];
                //Log.Line($"_magicVecsLen {_magicVecs.Length}");
                //Log.Line($"num: {num} - {findMagic[n]} - {findMagic.Length} - {_shieldTris[num]}");
                //if (_count == 0) Log.Line($"magic {num} - {test[num]}");
                n++;
            }
            */
            /*
            for (int i = 0, j = 0; i < _shieldTris.Length; i++, j++)
            {
                var vec = _shieldTris[i];
                foreach (var magic in _magicVecs)
                {
                    for (int num = 0; num < 12; num++)
                    {
                        if (_count == 0 && vec == magic && _magicVecs[num] == vec) Log.Line($"Found Magic root {num} at index: {i}");
                    }

                }
            }
            */
            /*
            var verts = new Vector3D[642];
            var nums = new int[642];

            var vc = 0;
            var vn = 0;
            var num = 0;
            for (int i = 0, j = 0; i < test.Length; i++, j++)
            {
                var vec = test[i];
                var e = true;
                foreach (var v in verts)
                {
                    if (v == vec) e = false;
                }
                if (e) verts[vc] = vec;
                if (e) vc++;
                if (e) nums[vn] = i;
                if (e) vn++;

            }
            foreach (var v in verts)
            {
               //Log.Line($"{num}:{nums[num]}:{v}");
               //Log.Chars($"{nums[num]},");
                num++;
            }
            */
            var vecBufferNums = new HashSet<int>();
            var vecBufferNumList = new List<int>();
            var vecPrunedLines = new HashSet<LineD>();
            var vecPrunedNums = new HashSet<MyTuple<int, int>>();
            DrawLineD(vecPrunedLinesTest);

            if (_count == 0 && buildOnce == false)
            {
                buildOnce = true;
                foreach (var v in _icoVerts2)
                {
                    vecBufferNums.Clear();
                    vecBufferNumList.Clear();
                    vecPrunedLines.Clear();
                    vecPrunedNums.Clear();

                    var vert = test[v];
                    for (int x = 0; x < test.Length; x += 3)
                    {
                        var v0 = test[x];
                        var v1 = test[x + 1];
                        var v2 = test[x + 2];
                        if (v0 == vert || v1 == vert || v2 == vert)
                        {
                            vecBufferNums.Add(x);
                            vecBufferNums.Add(x + 1);
                            vecBufferNums.Add(x + 2);
                        }
                    }



                    //Log.Line($" VECBUFFER {vecBufferNums.Count} - {v} \n");
                    foreach (var Num in vecBufferNums)
                    {
                        vecBufferNumList.Add(Num);
                    }
                    //Log.Line($" VECBUFFERLIST {vecBufferNumList.Count} - {v}");



                    for (int i = 0; i < vecBufferNumList.Count; i += 3)
                    {
                        var tri00 = vecBufferNumList[i];
                        var tri01 = vecBufferNumList[i+1];
                        var tri02 = vecBufferNumList[i+2];

                        var tri0Line1 = new LineD(test[tri00], test[tri01]);
                        var tri0Line2 = new LineD(test[tri00], test[tri02]);
                        var tri0Line3 = new LineD(test[tri01], test[tri02]);


                        vecPrunedLines.Add(tri0Line1);
                        vecPrunedLines.Add(tri0Line2);
                        vecPrunedLines.Add(tri0Line3);
                    }

                    int num = 0;
                    foreach (var prune in vecPrunedLines)
                    {
                        var from = prune.From;
                        var to = prune.To;
                        int toNum = -1;
                        int fromNum = -1;
                        for (int i = 0; i < test.Length; i +=3)
                        {
                            var vn0 = test[i];
                            var vn1 = test[i + 1];
                            var vn2 = test[i + 2];

                            if (from == vn0) fromNum = i;
                            if (from == vn1) fromNum = i + 1;
                            if (from == vn2) fromNum = i + 2;

                            if (to == vn0) toNum = i;
                            if (to == vn1) toNum = i + 1;
                            if (to == vn2) toNum = i + 2;

                            var icoVert = test[v];
                            if ((from == vn0 || from == vn1 || from == vn2) && (to == vn0 || to == vn1 || to == vn2) &&
                                (icoVert == vn0 || icoVert == vn1 || icoVert == vn2))
                            {
                                MyTuple<int, int> fromToNum = new MyTuple<int, int>(fromNum, toNum);
                                MyTuple<int, int> toFromNum = new MyTuple<int, int>(toNum, fromNum);
                                MyTuple<int, int> icoVertFromNum = new MyTuple<int, int>(v, fromNum);
                                MyTuple<int, int> icoVertToNum = new MyTuple<int, int>(v, toNum);
                                MyTuple<int, int> fromToIcoVert = new MyTuple<int, int>(fromNum, v);
                                MyTuple<int, int> toIcoVert = new MyTuple<int, int>(toNum, v);

                                if (!(vecPrunedNums.Contains(fromToNum) || vecPrunedNums.Contains(toFromNum))) vecPrunedNums.Add(fromToNum);
                                if (!(vecPrunedNums.Contains(fromToNum) || vecPrunedNums.Contains(toFromNum))) vecPrunedNums.Add(toFromNum);
                                if (!(vecPrunedNums.Contains(fromToNum) || vecPrunedNums.Contains(fromToIcoVert))) vecPrunedNums.Add(fromToIcoVert);
                                if (!(vecPrunedNums.Contains(toFromNum) || vecPrunedNums.Contains(fromToIcoVert))) vecPrunedNums.Add(toIcoVert);

                                //Log.Line($"from: {fromNum} - to: {toNum} - v: {v}");
                                //Log.Chars($"{{}}");
                                num++;
                                //if (i == 0) Log.Chars($"arr[{num}] = new int[{vecPrunedNums.Count}] {{");
                            }
                        }

                        //Log.Line($"{vecPrunedNums.Count}");
                        foreach (var pNum in vecPrunedNums)
                        {
                            //Log.Line($"num: {v} -- {pNum.Item1} - {pNum.Item2}");

                            var tri0Line1 = new LineD(test[pNum.Item1], test[pNum.Item2]);

                            vecPrunedLinesTest.Add(tri0Line1);

                        }
                    }
                    //DrawLineD(vecPrunedLines);

                    //Log.Line($"pLines: {vecPrunedLines.Count} - vecBuff: {vecBufferNums.Count} - vecBuffList: {vecBufferNumList.Count} - vNum:{v}");
                    /*
                    foreach (var vec in vecBuffer)
                    {
                        Log.Chars($"{vec},");
                    }
                    Log.Chars($"\n");
                    */
                }
                Log.Line($"{vecPrunedLinesTest.Count}");
            }


            /*
                var c0 = 0;
                var c1 = 0;
                var c2 = 0;
                var c3 = 0;
                var c4 = 0;
                var c5 = 0;
                var c6 = 0;
                var c7 = 0;
                var c8 = 0;
                var c9 = 0;
                var c10 = 0;
                var c11 = 0;
                if (buildOnce == false)
                {
                    for (int i = 0, j = 0; i < test.Length; i += 3, j++)
                    {
                        if (c0 == 25) continue;
                        if (c1 == 25) continue;
                        if (c2 == 25) continue;
                        if (c3 == 25) continue;
                        if (c4 == 25) continue;
                        if (c5 == 25) continue;
                        if (c6 == 25) continue;
                        if (c7 == 25) continue;
                        if (c8 == 25) continue;
                        if (c9 == 25) continue;
                        if (c10 == 25) continue;
                        if (c11 == 25) continue;

                        var v0 = test[i];
                        var v1 = test[i + 1];
                        var v2 = test[i + 2];
                        var mNum = -1;

                        foreach (var magicVec in _magicVecs)
                        {
                            if (magicVec == _magicVecs[0]) mNum = 0;
                            if (magicVec == _magicVecs[1]) mNum = 1;
                            if (magicVec == _magicVecs[2]) mNum = 2;
                            if (magicVec == _magicVecs[3]) mNum = 3;
                            if (magicVec == _magicVecs[4]) mNum = 4;
                            if (magicVec == _magicVecs[5]) mNum = 5;
                            if (magicVec == _magicVecs[6]) mNum = 6;
                            if (magicVec == _magicVecs[7]) mNum = 7;
                            if (magicVec == _magicVecs[8]) mNum = 8;
                            if (magicVec == _magicVecs[9]) mNum = 9;
                            if (magicVec == _magicVecs[10]) mNum = 10;
                            if (magicVec == _magicVecs[11]) mNum = 11;

                            if (v0 == _magicVecs[mNum] || v1 == _magicVecs[mNum] || v2 == _magicVecs[mNum])
                            {
                                var e0 = true;
                                var e1 = true;
                                var e2 = true;

                                if (mNum == 0)
                                {
                                    foreach (var m in m0)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m0[c0] = v0;
                                    if (e0) c0++;
                                    if (e1) m0[c0] = v1;
                                    if (e1) c0++;
                                    if (e2) m0[c0] = v2;
                                    if (e2) c0++;

                                    //if (_count == 0) Log.Line($"v0: {v0} v0: {e0} - l:{c0} - n:{mNum} - root:{v0 == _magicVecs[mNum]}");
                                    //if (_count == 0) Log.Line($"v1: {v1} v1: {e1} - l:{c0} - n:{mNum} - root:{v1 == _magicVecs[mNum]}");
                                    //if (_count == 0) Log.Line($"v2: {v2} v2: {e2} - l:{c0} - n:{mNum} - root:{v2 == _magicVecs[mNum]}");

                                }
                                if (mNum == 1)
                                {
                                    foreach (var m in m1)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m1[c1] = v0;
                                    if (e0) c1++;
                                    if (e1) m1[c1] = v1;
                                    if (e1) c1++;
                                    if (e2) m1[c1] = v2;
                                    if (e2) c1++;
                                }
                                if (mNum == 2)
                                {
                                    foreach (var m in m2)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m2[c2] = v0;
                                    if (e0) c2++;
                                    if (e1) m2[c2] = v1;
                                    if (e1) c2++;
                                    if (e2) m2[c2] = v2;
                                    if (e2) c2++;
                                }
                                if (mNum == 3)
                                {
                                    foreach (var m in m3)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m3[c3] = v0;
                                    if (e0) c3++;
                                    if (e1) m3[c3] = v1;
                                    if (e1) c3++;
                                    if (e2) m3[c3] = v2;
                                    if (e2) c3++;
                                }
                                if (mNum == 4)
                                {
                                    foreach (var m in m4)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m4[c4] = v0;
                                    if (e0) c4++;
                                    if (e1) m4[c4] = v1;
                                    if (e1) c4++;
                                    if (e2) m4[c4] = v2;
                                    if (e2) c4++;
                                }
                                if (mNum == 5)
                                {
                                    foreach (var m in m5)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m5[c5] = v0;
                                    if (e0) c5++;
                                    if (e1) m5[c5] = v1;
                                    if (e1) c5++;
                                    if (e2) m5[c5] = v2;
                                    if (e2) c5++;
                                }
                                if (mNum == 6)
                                {
                                    foreach (var m in m6)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m6[c6] = v0;
                                    if (e0) c6++;
                                    if (e1) m6[c6] = v1;
                                    if (e1) c6++;
                                    if (e2) m6[c6] = v2;
                                    if (e2) c6++;
                                }
                                if (mNum == 7)
                                {
                                    foreach (var m in m7)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m7[c7] = v0;
                                    if (e0) c7++;
                                    if (e1) m7[c7] = v1;
                                    if (e1) c7++;
                                    if (e2) m7[c7] = v2;
                                    if (e2) c7++;
                                }
                                if (mNum == 8)
                                {
                                    foreach (var m in m8)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m8[c8] = v0;
                                    if (e0) c8++;
                                    if (e1) m8[c8] = v1;
                                    if (e1) c8++;
                                    if (e2) m8[c8] = v2;
                                    if (e2) c8++;
                                }
                                if (mNum == 9)
                                {
                                    foreach (var m in m9)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m9[c9] = v0;
                                    if (e0) c9++;
                                    if (e1) m9[c9] = v1;
                                    if (e1) c9++;
                                    if (e2) m9[c9] = v2;
                                    if (e2) c9++;
                                }
                                if (mNum == 10)
                                {
                                    foreach (var m in m10)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m10[c10] = v0;
                                    if (e0) c10++;
                                    if (e1) m10[c10] = v1;
                                    if (e1) c10++;
                                    if (e2) m10[c10] = v2;
                                    if (e2) c10++;
                                }
                                if (mNum == 11)
                                {
                                    foreach (var m in m11)
                                    {
                                        if (m == v0 || v0 == _magicVecs[mNum]) e0 = false;
                                        if (m == v1 || v1 == _magicVecs[mNum]) e1 = false;
                                        if (m == v2 || v2 == _magicVecs[mNum]) e2 = false;
                                    }
                                    if (e0) m11[c11] = v0;
                                    if (e0) c11++;
                                    if (e1) m11[c11] = v1;
                                    if (e1) c11++;
                                    if (e2) m11[c11] = v2;
                                    if (e2) c11++;
                                }
                            }
                        }
                        buildOnce = false;
                    }
                }

                DrawMagicVerts();
                ReportVertArrays();
                DrawVertLines();
                */
            //if (_count == 0) DSUtils.StopWatchReport("compile verts", -1);
        }

        private void DrawMagicVerts()
        {
            foreach (var magic in _magicVecs)
            {
                //Log.Line($"magic: {magic}");
                var c = Color.Red;
                if (magic == _magicVecs[0]) c = Color.Blue;
                DrawCollisionCenter(magic, 5, c);
            }
        }

        private void DrawLineD(HashSet<LineD> lineD)
        {
            foreach (var line in lineD)
            {
                var from = line.From;
                var to = line.To;
                var color = Vector4.Zero;
                MySimpleObjectDraw.DrawLine(from, to, MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
        }
        private void DrawVertLines()
        {
            var color = Vector4.Zero;
            for (int i = 0; i < m0.Length; i++)
            {
                if (m0[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[0], m0[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m1.Length; i++)
            {
                if (m1[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[1], m1[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m2.Length; i++)
            {
                if (m2[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[2], m2[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m3.Length; i++)
            {
                if (m3[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[3], m3[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m4.Length; i++)
            {
                if (m4[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[4], m4[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m5.Length; i++)
            {
                if (m5[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[5], m5[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m6.Length; i++)
            {
                if (m6[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[6], m6[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m7.Length; i++)
            {
                if (m7[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[7], m7[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m8.Length; i++)
            {
                if (m8[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[8], m8[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m9.Length; i++)
            {
                if (m9[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[9], m9[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m10.Length; i++)
            {
                if (m10[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[10], m10[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
            for (int i = 0; i < m11.Length; i++)
            {
                if (m11[i] != Vector3D.Zero) MySimpleObjectDraw.DrawLine(_magicVecs[11], m11[i], MyStringId.GetOrCompute(""), ref color, 0.25f);
            }
        }

        private void ReportVertArrays()
        {
            var mc0 = 0;
            foreach (var m in m0)
            {
                bool magic = m == _magicVecs[0];
                if (_count == 0) Log.Line($"m0: {mc0}:{m} - magic:{magic} - {_magicVecs[0]}");
                mc0++;
            }

            var mc1 = 0;
            foreach (var m in m1)
            {
                bool magic = m == _magicVecs[1];
                if (_count == 0) Log.Line($"m1: {mc1}:{m} - magic:{magic}");
                mc1++;
            }

            var mc2 = 0;
            foreach (var m in m2)
            {
                bool magic = m == _magicVecs[2];
                if (_count == 0) Log.Line($"m2: {mc2}:{m} - magic:{magic}");
                mc2++;
            }

            var mc3 = 0;
            foreach (var m in m3)
            {
                bool magic = m == _magicVecs[3];
                if (_count == 0) Log.Line($"m3: {mc3}:{m} - magic:{magic}");
                mc3++;
            }
            var mc4 = 0;
            foreach (var m in m4)
            {
                bool magic = m == _magicVecs[4];
                if (_count == 0) Log.Line($"m4: {mc4}:{m} - magic:{magic}");
                mc4++;
            }
            var mc5 = 0;
            foreach (var m in m5)
            {
                bool magic = m == _magicVecs[5];
                if (_count == 0) Log.Line($"m5: {mc5}:{m} - magic:{magic}");
                mc5++;
            }
            var mc6 = 0;
            foreach (var m in m6)
            {
                bool magic = m == _magicVecs[6];
                if (_count == 0) Log.Line($"m6: {mc6}:{m} - magic:{magic}");
                mc6++;
            }
            var mc7 = 0;
            foreach (var m in m7)
            {
                bool magic = m == _magicVecs[7];
                if (_count == 0) Log.Line($"m7: {mc7}:{m} - magic:{magic}");
                mc7++;
            }
            var mc8 = 0;
            foreach (var m in m8)
            {
                bool magic = m == _magicVecs[8];
                if (_count == 0) Log.Line($"m8: {mc8}:{m} - magic:{magic}");
                mc8++;
            }
            var mc9 = 0;
            foreach (var m in m9)
            {
                bool magic = m == _magicVecs[9];
                if (_count == 0) Log.Line($"m9: {mc9}:{m} - magic:{magic}");
                mc9++;
            }
            var mc10 = 0;
            foreach (var m in m10)
            {
                bool magic = m == _magicVecs[10];
                if (_count == 0) Log.Line($"m10: {mc10}:{m} - magic:{magic}");
                mc10++;
            }
            var mc11 = 0;
            foreach (var m in m11)
            {
                bool magic = m == _magicVecs[11];
                if (_count == 0) Log.Line($"m11: {mc11}:{m} - magic:{magic}");
                mc11++;
            }
        }
        public override void UpdateBeforeSimulation100()
        {
            if (_initialized) return;
            Log.Line($"Initting entity");
            if (Block.CubeGrid.Physics.IsStatic) _gridIsMobile = false;
            else if (!Block.CubeGrid.Physics.IsStatic) _gridIsMobile = true;

            CreateUi();
            Block.AppendingCustomInfo += AppendingCustomInfo;
            Block.RefreshCustomInfo();
            _absorb = 150f;

            _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
            _shield.Render.Visible = false;

            DefenseShieldsBase.Instance.Shields.Add(this);
            _initialized = true;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_animInit) return;
                if (Block.BlockDefinition.SubtypeId == "StationDefenseShield")
                {
                    if (!Block.IsFunctional) return;
                    BlockAnimationInit();
                    Log.Line($" BlockAnimation {_count}");
                    _animInit = true;
                }
                else
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation: {ex}"); }
        }
        #endregion

        #region Block Power and Entity Config Logic
        private float CalcRequiredPower()
        {
            if (!_initialized || !Block.IsWorking) return _power;
            if (_absorb >= 0.1)
            {
                _absorb = _absorb - _recharge;
                _recharge = _absorb / 10f;
            }
            else if (_absorb < 0.1f)
            {
                _recharge = 0f;
                _absorb = 0f;
            }
            var radius = GetRadius();
            var sustaincost = radius * 0.001f;
            _power = _recharge + sustaincost;

            return _power;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            if (!_gridIsMobile)RefreshDimensions();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");
        }

        private bool RefreshDimensions()
        {
            var width = _widthSlider.Getter(Block);
            var height = _heightSlider.Getter(Block);
            var depth = _depthSlider.Getter(Block);
            var oWidth = _width;
            var oHeight = _height;
            var oDepth = _depth;
            _width = width;
            _height = height;
            _depth = depth;
            var changed = (int)oWidth != (int)width || (int)oHeight != (int)height || (int)oDepth != (int)depth;
            if (!changed) return false;
            CreateShieldMatrices();
            return true;
        }

        private float GetRadius()
        {
            float radius;
            if (_gridIsMobile)
            {
                var p = (float)_shieldShapeMatrix.Scale.Sum / 3 / 2;
                radius = p * p * 4 * (float)Math.PI;
                return radius;
            }
            var r = (_width + _height + _depth) / 3 / 2;
            var r2 = r * r;
            var r3 = r2 * 4;
            radius = r3 * (float)Math.PI;

            return radius;
        }
        #endregion

        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void RemoveOreUi()
        {
            List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;
        }

        private void CreateUi()
        {
            Log.Line($"Create UI - c:{_count}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "WidthSlider", "Shield Size Width", 10, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "HeightSlider", "Shield Size Height", 10, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "DepthSlider", "Shield Size Depth", 10, 300, 100);
        }
        #endregion

        #region Block Animation
        private void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {_count}");
            _subpartRotor.Subparts.Clear();
            _subpartsArms.Clear();
            _subpartsReflectors.Clear();
            BlockAnimationInit();
        }

        private void BlockAnimationInit()
        {
            try
            {
                _animStep = 0f;

                _matrixArmsOff = new List<Matrix>();
                _matrixArmsOn = new List<Matrix>();
                _matrixReflectorsOff = new List<Matrix>();
                _matrixReflectorsOn = new List<Matrix>();

                Entity.TryGetSubpart("Rotor", out _subpartRotor);

                for (var i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    _subpartRotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    _matrixArmsOff.Add(temp1.PositionComp.LocalMatrix);
                    var temp2 = temp1.PositionComp.LocalMatrix.GetOrientation();
                    switch (i)
                    {
                        case 1:
                        case 5:
                            temp2 *= Matrix.CreateRotationZ(0.98f);
                            break;
                        case 2:
                        case 6:
                            temp2 *= Matrix.CreateRotationX(-0.98f);
                            break;
                        case 3:
                        case 7:
                            temp2 *= Matrix.CreateRotationZ(-0.98f);
                            break;
                        case 4:
                        case 8:
                            temp2 *= Matrix.CreateRotationX(0.98f);
                            break;
                    }
                    temp2.Translation = temp1.PositionComp.LocalMatrix.Translation;
                    _matrixArmsOn.Add(temp2);
                    _subpartsArms.Add(temp1);
                }

                for (var i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    _subpartsArms[i].TryGetSubpart("Reflector", out temp3);
                    _subpartsReflectors.Add(temp3);
                    _matrixReflectorsOff.Add(temp3.PositionComp.LocalMatrix);
                    var temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    _matrixReflectorsOn.Add(temp4);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockAnimation: {ex}"); }
        }

        private void BlockAnimation()
        {
            if (Block.Enabled && Block.IsFunctional && Block.IsWorking)
            {
                _subpartRotor.SetEmissiveParts("Emissive", Color.White, 1);
                _time += 1;
                var temp1 = Matrix.CreateRotationY(0.1f * _time);
                temp1.Translation = _subpartRotor.PositionComp.LocalMatrix.Translation;
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {
                //_subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
                if (_animStep > 0f)
                {
                    _animStep -= 0.05f;
                }
            }
            for (var i = 0; i < 8; i++)
            {
                if (i < 4)
                {
                    _subpartsReflectors[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixReflectorsOff[i], _matrixReflectorsOn[i], _animStep);
                }
                _subpartsArms[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixArmsOff[i], _matrixArmsOn[i], _animStep);
            }
        }
        #endregion

        #region Shield Draw
        private Task? _prepareDraw = null;
        public void Draw()
        {
            try
            {
                if (!_initialized) return;

                SetShieldShapeMatrix();
                var drawShapeChanged = _entityChanged;

                var prevlod = _prevLod;
                var lod = CalculateLod();
                var shield = _shield;
                var impactPos = _worldImpactPosition;

                var referenceWorldPosition = _shieldGridMatrix.Translation; 
                var worldDirection = impactPos - referenceWorldPosition; 
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(_shieldGridMatrix));
                if (impactPos != Vector3D.NegativeInfinity) impactPos = localPosition;
                //if (impactpos != Vector3D.NegativeInfinity) impactpos = Vector3D.Transform(impactpos, Block.CubeGrid.WorldMatrixInvScaled);
                _worldImpactPosition = Vector3D.NegativeInfinity;

                var impactSize = _impactSize;

                var shapeMatrix = _shieldShapeMatrix;
                var enemy = IsEnemy(null);
                var renderId = GetRenderId();
                //var shapeMatrix = DetectionMatrix;
                //uint renderId = 0;

                var sp = new BoundingSphereD(Entity.GetPosition(), _range);
                var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);
                //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{sphereOnCamera} - RenderID {renderId}");
                if (_prepareDraw.HasValue && !_prepareDraw.Value.IsComplete) _prepareDraw.Value.Wait();
                if (_prepareDraw.HasValue && _prepareDraw.Value.IsComplete && sphereOnCamera && Block.IsWorking) _icosphere.Draw(renderId);
                if (Block.IsWorking || drawShapeChanged) _prepareDraw = MyAPIGateway.Parallel.Start(() => PrepareSphere(drawShapeChanged, sphereOnCamera, enemy, lod, prevlod, impactPos, impactSize, shapeMatrix, shield));

            }
            catch (Exception ex) { Log.Line($"Exception in Entity Draw: {ex}"); }
        }

        private void PrepareSphere(bool drawShapeChanged, bool sphereOnCamera, bool enemy, int lod, int prevlod, Vector3D impactPos, float impactSize, MatrixD shapeMatrix,  IMyEntity shield)
        {
            if (drawShapeChanged || lod != prevlod) _icosphere.CalculateTransform(shapeMatrix, lod);
            _icosphere.CalculateColor(shapeMatrix, impactPos, impactSize, drawShapeChanged, enemy, sphereOnCamera, shield);
        }

        public void DrawBox(MyOrientedBoundingBoxD obb, Color color, bool matrix)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromTransformScale(obb.Orientation, obb.Center, Vector3D.One);
            if (matrix) wm = wm * _shieldGridMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, MySimpleObjectRasterizer.Solid, 1, 1f, null, null, true);
        }
        #endregion

        #region Shield Draw Prep
        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Block.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + _range) * (x + _range);
            return range;
        }

        private int CalculateLod()
        {
            int lod;

            if (Distance(650)) lod = 3;
            else if (Distance(2250)) lod = 3;
            else if (Distance(4500)) lod = 2;
            else if (Distance(15000)) lod = 1;
            else if (Distance(25000)) lod = 1;
            else lod = 1;

            _prevLod = lod;
            return lod;
        }

        private void CreateShieldMatrices()
        {
            if (_gridIsMobile)
            {
                _shieldGridMatrix = Block.CubeGrid.WorldMatrix;
                CreateMobileShape();
                var mobileMatrix = _mobileMatrix;
                DetectionMatrix = mobileMatrix * _shieldGridMatrix;
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;

                //Log.Line($"mobile dims {_range} - {_width} - {_height} - {_depth} - changed: {_entityChanged}");
            }
            else
            {
                _shieldGridMatrix = Block.WorldMatrix;
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                //Log.Line($"static dims {_range} - {_width} - {_height} - {_depth}");
            }
        }


        private void CreateMobileShape()
        {
            if (!_gridChanged) return;

            var gridHalfExtents = Block.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const float ellipsoidAdjust = (float)MathHelper.Sqrt2;
            var buffer = 5f;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            _shieldSize = shieldSize;
            //var gridLocalCenter = Block.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize); //* MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Block.CubeGrid.PositionComp.LocalVolume.Center;
            _mobileMatrix = mobileMatrix;
        }

        private void SetShieldShapeMatrix()
        {
            if (Block.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Block.LocalMatrix, new Vector3D(_width, _height, _depth));
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            if (!_entityChanged || Block.CubeGrid.Physics.IsStatic) return;
            CreateMobileShape();
            var mobileMatrix = _mobileMatrix;

            _shieldShapeMatrix = mobileMatrix;
            _shield.SetWorldMatrix(_shieldShapeMatrix);
        }

        private bool IsEnemy(IMyEntity enemy)
        {
            if (enemy != null)
            {
                if (enemy is IMyCharacter)
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(enemy).IdentityId;
                    var playerrelationship = Block.GetUserRelationToOwner(dude);
                    return playerrelationship != MyRelationsBetweenPlayerAndBlock.Owner && playerrelationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                }
                if (enemy is IMyCubeGrid)
                {
                    var grid = enemy as IMyCubeGrid;
                    var owners = grid.BigOwners;
                    if (owners.Count > 0)
                    {
                        var relationship = Block.GetUserRelationToOwner(owners[0]);
                        return relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                    }
                }
            }
            var relations = Block.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            return relations != MyRelationsBetweenPlayerAndBlock.Owner && relations != MyRelationsBetweenPlayerAndBlock.FactionShare;
        }

        private uint GetRenderId()
        {
            var renderId = _gridIsMobile ? Block.CubeGrid.Render.GetRenderObjectID() : Block.Render.GetRenderObjectID();
            return renderId;
        }
        #endregion

        #region Detect Intersection
        private Vector3D ContactPoint(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = DetectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        private Vector3D ContactPointObb(IMyEntity breaching)
        {
            var collision = new Vector3D(Vector3D.NegativeInfinity);
            var shieldTris = _shieldTris;
            var locCenterSphere = new BoundingSphereD();

            var dWorldAabb =  Block.CubeGrid.WorldAABB;

            var bLocalAabb = breaching.PositionComp.LocalAABB;
            var bWorldAabb = breaching.PositionComp.WorldAABB;
            var bWorldCenter = bWorldAabb.Center;


            var lodScaler = (int)Math.Pow(2, PhysicsLod);
            var gridScaler = (float)(((DetectionMatrix.Scale.X + DetectionMatrix.Scale.Y + DetectionMatrix.Scale.Z) / 3 / lodScaler) * 1.33) / bLocalAabb.Extents.Min();
            var bLength = bLocalAabb.Size.Max() / 2 + 2;
            var bLengthSqr = bLength * bLength;

            var reSized = bLocalAabb.Extents.Min() * gridScaler;
            var reSizedSqr = reSized * reSized;

            if (gridScaler > 1)
            {
                //var rangedVectors = IntersectRangeCheck(shieldTris, bWorldCenter, reSizedSqr);
                var rangedVerts = VertRangeCheck(_physicsVerts, shieldTris, bWorldCenter, reSizedSqr);
                Log.Line($"{rangedVerts.Count}");
                var boxedTriangles = IntersectTriBox(rangedVerts, bWorldAabb);
                var obbLines = IntersectLineObb(rangedVerts, bLocalAabb, breaching.PositionComp.WorldMatrix);
                if (obbLines.Count > 0)
                {
                    var pointCollectionScaled = new Vector3D[obbLines.Count];
                    obbLines.CopyTo(pointCollectionScaled);
                    locCenterSphere = BoundingSphereD.CreateFromPoints(pointCollectionScaled);


                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
                    _worldImpactPosition = collision;
                    DrawCollisionCenter(collision, locCenterSphere.Radius, Color.Blue); // testing
                }

                if (boxedTriangles.Count > 0)
                {
                    //DSUtils.Sw.Start();
                    var pointCollectionScaled = new Vector3D[boxedTriangles.Count];
                    boxedTriangles.CopyTo(pointCollectionScaled);
                    locCenterSphere = BoundingSphereD.CreateFromPoints(pointCollectionScaled);

                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
                    _worldImpactPosition = collision;
                   // DSUtils.StopWatchReport($"Small Grid Collision 3", -1);
                    DrawCollisionCenter(collision, locCenterSphere.Radius, Color.Blue); // testing
                }
                if (rangedVerts.Count > 0) Log.Line($"total triangles: {_shieldTris.Length / 3} - Ranged {rangedVerts.Count / 3} - BoxTri Check: {boxedTriangles.Count / 3}");
            }
            else 
            {

                foreach (var magic in _magicVecs)
                {
                    //Log.Line($"magic: {magic}");
                    DrawCollisionCenter(magic, 5, Color.Red);
                }

                var rangedVectors = IntersectRangeCheck(shieldTris, bWorldCenter, bLengthSqr);
                var boxedVectors = IntersectVecBox(rangedVectors, bWorldAabb);
                var obbLines = IntersectLineObb(boxedVectors, bLocalAabb, breaching.PositionComp.WorldMatrix);
                if (obbLines.Count > 0)
                {
                    var pointCollectionScaled = new Vector3D[obbLines.Count];
                    obbLines.CopyTo(pointCollectionScaled);
                    locCenterSphere = BoundingSphereD.CreateFromPoints(pointCollectionScaled);


                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
                    _worldImpactPosition = collision;
                    DrawCollisionCenter(collision, locCenterSphere.Radius, Color.Blue); // testing
                }
                if (rangedVectors.Count > 0) Log.Line($"total triangles: {_shieldTris.Length / 3} - Ranged {rangedVectors.Count / 3} - Box Check: {boxedVectors.Count / 3} - Obb Collision {obbLines.Count / 3}");
            }
            var grid = breaching as IMyCubeGrid;
            if (grid == null) return collision;

            try
            {
                var getBlocks = grid.GetBlocksInsideSphere(ref locCenterSphere);
                lock (DmgBlocks)
                {
                    foreach (var block in getBlocks)
                    {
                        DmgBlocks.Add(block);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}"); }
            return collision;
        }

        private static List<Vector3D> IntersectRangeCheck(Vector3D[] shieldTris, Vector3D bWorldCenter, float reSizedSqr)
        {
            //DSUtils.Sw.Start();
            var rangedVectors = new List<Vector3D>();


            for (int i = 0, j = 1; i < shieldTris.Length; i += 3, j++)
            {
                var v0 = shieldTris[i];
                var v1 = shieldTris[i + 1];
                var v2 = shieldTris[i + 2];
                var test1 = (float)Vector3D.DistanceSquared(v0, bWorldCenter);
                var test2 = (float)Vector3D.DistanceSquared(v1, bWorldCenter);
                var test3 = (float)Vector3D.DistanceSquared(v2, bWorldCenter);

                if (test1 < reSizedSqr && test2 < reSizedSqr && test3 < reSizedSqr)
                {
                    //Log.Line($"vec {j} success");
                    rangedVectors.Add(v0);
                    rangedVectors.Add(v1);
                    rangedVectors.Add(v2);
                }
            }
            //DSUtils.StopWatchReport($"Small Grid Collision 1", -1);
            return rangedVectors;
        }

        private static List<Vector3D> VertRangeCheck(Vector3D[] physicsVerts3, Vector3D[] shieldTris, Vector3D bWorldCenter, float reSizedSqr)
        {
            var rangedVerts = new List<Vector3D>();
            var vertBuffer = new List<Vector3D>();
            for (int i = 0; i < physicsVerts3.Length; i++)
            {
                var vert = physicsVerts3[i];
                var test1 = (float)Vector3D.DistanceSquared(vert, bWorldCenter);

                if (test1 < reSizedSqr)
                {
                    vertBuffer.Add(vert);
                    if (i < physicsVerts3.Length - 1) continue;
                    //Log.Line($"vec {j} success");
                }
                if (i == physicsVerts3.Length - 1)
                {
                    DSUtils.Sw.Start();
                    for (int j = 0; j < shieldTris.Length; j += 3)
                    {
                        var v0 = shieldTris[j];
                        var v1 = shieldTris[j + 1];
                        var v2 = shieldTris[j + 2];
                        for (int v = 0; v < vertBuffer.Count; v++)
                        {
                            var vbuff = vertBuffer[v];
                            if (v0 == vbuff || v1 == vbuff || v2 == vbuff)
                            {
                                rangedVerts.Add(v0);
                                rangedVerts.Add(v1);
                                rangedVerts.Add(v2);
                            }
                        }
                    }
                    DSUtils.StopWatchReport($"Small Grid Collision 1", -1);
                }
            }
            return rangedVerts;
        }


        private List<Vector3D> IntersectTriBox(List<Vector3D> rangedVectors, BoundingBoxD bWorldAabb)
        {
            //DSUtils.Sw.Start();
            var boxedTriangles = new List<Vector3D>();

            for (int i = 0, j = 0; i < rangedVectors.Count; i += 3, j++)
            {
                var v0 = rangedVectors[i];
                var v1 = rangedVectors[i + 1];
                var v2 = rangedVectors[i + 2];
                var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                if (!test1) continue;
                boxedTriangles.Add(v0);
                boxedTriangles.Add(v1);
                boxedTriangles.Add(v2);
            }
            //if (_count == 0) Log.Line($"gridScaler: {gridScaler} - Blength: {bLength} - bsqr {bLength * bLength} - bMin: {bLocalAABB.Extents.Min()}");
            //if (rangedVectors.Count > 0) Log.Line($"total triangles: {_shieldTris.Length / 3} - Ranged {rangedVectors.Count / 3} - Box Check: {boxedVectors.Count / 3}");
            //DSUtils.StopWatchReport($"Small Grid Collision 2", -1);
            return boxedTriangles;
        }

        private List<Vector3D> IntersectVecBox(List<Vector3D> rangedVectors, BoundingBoxD bWorldAabb)
        {
            var boxedVectors = new List<Vector3D>();

            for (int i = 0, j = 0; i < rangedVectors.Count; i += 3, j++)
            {
                var v0 = rangedVectors[i];
                var v1 = rangedVectors[i + 1];
                var v2 = rangedVectors[i + 2];
                var test1 = bWorldAabb.Contains(v0);
                var test2 = bWorldAabb.Contains(v1);
                var test3 = bWorldAabb.Contains(v2);

                if (test1 == ContainmentType.Contains && test2 == ContainmentType.Contains && test3 == ContainmentType.Contains)
                {
                    boxedVectors.Add(v0);
                    boxedVectors.Add(v1);
                    boxedVectors.Add(v2);
                }
            }
            return boxedVectors;
        }

        private List<Vector3D> IntersectLineObb(List<Vector3D> boxedVectors, BoundingBox bLocalAabb, MatrixD matrix)
        {
            var obbLines = new List<Vector3D>();
            var bOriBBoxD = new MyOrientedBoundingBoxD(bLocalAabb, matrix);

            for (var i = 0; i < boxedVectors.Count; i += 3)
            {
                var line1 = boxedVectors[i];
                var line2 = boxedVectors[i + 1];
                var line3 = boxedVectors[i + 2];
                var lineTest1 = new LineD(line1, line2);
                var lineTest2 = new LineD(line2, line3);
                var lineTest3 = new LineD(line3, line1);
                //if (lineTest1.Length > 19.45 || lineTest2.Length > 19.45 || lineTest3.Length > 19.45)Log.Line($"{lineTest3.Length} - {lineTest2.Length} - {lineTest1.Length}");
                //if (lineTest1.Length > 0 || lineTest2.Length > 0 || lineTest3.Length > 0)Log.Line($"{lineTest3.Length} - {lineTest2.Length} - {lineTest1.Length}");
                if (bOriBBoxD.Intersects(ref lineTest1).HasValue || bOriBBoxD.Intersects(ref lineTest2).HasValue || bOriBBoxD.Intersects(ref lineTest3).HasValue)
                {
                    obbLines.Add(line1);
                    obbLines.Add(line2);
                    obbLines.Add(line3);
                }
            }
            return obbLines;
        }

        private void DrawCollisionCenter(Vector3D collision, double radius, Color color)
        {
            var posMatCenterScaled = MatrixD.CreateTranslation(collision);
            var posMatScaler = MatrixD.Rescale(posMatCenterScaled, radius);
            var rangeGridResourceId = MyStringId.GetOrCompute("Build new");
            MySimpleObjectDraw.DrawTransparentSphere(ref posMatScaler, 1f, ref color, MySimpleObjectRasterizer.Solid, 20, null, rangeGridResourceId, 0.25f, -1);
        }

        private double ContainmentField(IMyEntity breaching, IMyEntity field, Vector3D intersect)
        {
            //var direction = Vector3D.Normalize(grid.Center() - grid.Center);
            //Vector3D velocity = grid.Physics.LinearVelocity;
            //if (Vector3D.IsZero(velocity)) velocity += direction;
            //
            //Vector3D forceDir = Vector3D.Reflect(Vector3D.Normalize(velocity), direction);
            //grid.Physics.SetSpeeds(velocity * forceDir, grid.Physics.AngularVelocity);
            //var dist = Vector3D.Distance(grid.GetPosition(), websphere.Center);
            //
            //var d = grid.Physics.CenterOfMass - thingRepellingYou;
            //var v = d * repulsionVelocity / d.Length();
            //grid.Physics.AddForce((v - grid.Physics.LinearVelocity) * grid.Physics.Mass / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);

            /*
            // local velocity of dest
            var velTarget = field.Physics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var distanceFromTargetCom = breaching.Physics.CenterOfMassWorld - field.Physics.CenterOfMassWorld;

            var accelLinear = field.Physics.LinearAcceleration;
            var omegaVector = field.Physics.AngularVelocity + field.Physics.AngularAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var omegaSquared = omegaVector.LengthSquared();
            // omega^2 * r == a
            var accelRotational = omegaSquared * -distanceFromTargetCom;
            var accelTarget = accelLinear + accelRotational;

            var velTargetNext = velTarget + accelTarget * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = breaching.Physics.LinearVelocity;// + modify.Physics.LinearAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

            var linearImpulse = breaching.Physics.Mass * (velTargetNext - velModifyNext);

            // Angular matching.
            // (dAA*dt + dAV) == (mAA*dt + mAV + tensorInverse*mAI)
            var avelModifyNext = breaching.Physics.AngularVelocity + breaching.Physics.AngularAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var angularDV = omegaVector - avelModifyNext;
            //var angularImpulse = Vector3.Zero;
            var angularImpulse = Vector3.TransformNormal(angularDV, breaching.Physics.RigidBody.InertiaTensor); //not accessible :/

            // based on the large grid, small ion thruster.
            const double wattsPerNewton = (3.36e6 / 288000);
            // based on the large grid gyro
            const double wattsPerNewtonMeter = (0.00003 / 3.36e7);
            // (W/N) * (N*s) + (W/(N*m))*(N*m*s) == W
            var powerCorrectionInJoules = (wattsPerNewton * linearImpulse.Length()) + (wattsPerNewtonMeter * angularImpulse.Length());
            breaching.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, linearImpulse, breaching.Physics.CenterOfMassWorld, angularImpulse);
            if (recoil) field.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -linearImpulse, field.Physics.CenterOfMassWorld, -angularImpulse);

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            */

            // Calculate Power

            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = field.Physics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = field.Physics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = breaching.Physics.LinearVelocity;
            var linearImpulse = breaching.Physics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            // ApplyImpulse
            //var contactPoint = ContactPoint(breaching);
            var contactPoint = intersect;

            var transformInv = MatrixD.Invert(DetectionMatrix);
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(contactPoint, transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            var bmass = -breaching.Physics.Mass;
            var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
            var expelForce = (bmass); /// Math.Pow(cpDist, 2);
            //if (expelForce < -9999000000f || bmass >= -67f) expelForce = -9999000000f;

            var worldPosition = breaching.WorldMatrix.Translation;
            var worldDirection = contactPoint - worldPosition;

            //breaching.Physics.ApplyImpulse(worldDirection * (expelForce / 2), contactPoint);
            //if (_gridIsMobile) Block.CubeGrid.Physics.ApplyImpulse(Vector3D.Negate(worldDirection) * (expelForce / 2), contactPoint);

            //if (cpDist > 0.987f) breaching.Physics.ApplyImpulse((breaching.Physics.Mass / 500) * -0.055f * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, contactPoint);
            //Log.Line($"cpDist:{cpDist} pow:{expelForce} bmass:{bmass} adjbmass{bmass / 50}");

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }

        private int[] ReOrderVectors(int random, int max)
        {
            var index = 0;
            var ordered = new int[max];
            ordered[index++] = random - 0 % max;
            if (random + max / 2 > max) ordered[max - 1] = random - max / 2 % max;
            else ordered[max - 1] = random + max / 2;
            for (int x = 1; index < max - 1; x++)
            {
                var sub = random - x % max;
                var add = random + x % max;
                
                if (sub >= 0) ordered[index++] = random - x % max;
                else if (sub < 0) ordered[index++] = max + (random - x % max);
                if (add >= max) ordered[index++] = 0 + (random + x % max - max);
                else ordered[index++] = random + x % max;
            }
            return ordered;
        }
        

        private void DamageGrids()
        {
            try
            {
                lock (DmgBlocks)
                {
                    foreach (var block in DmgBlocks)
                    {
                        //block.DoDamage(100f, MyDamageType.Fire, true, null, Block.EntityId);
                    }
                    DmgBlocks.Clear();
                }
                //if (_count == 0) Log.Line($"Block Count {DmgBlocks.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in DamgeGrids: {ex}"); }
        }

        private Vector3D Intersect(IMyEntity ent, bool impactcheck)
        {
            var contactpoint = ContactPointObb(ent);

            if (contactpoint != Vector3D.NegativeInfinity) 
            {
                //Log.Line($"GridIsColliding {GridIsColliding.Count} - check {impactcheck} - containsEnt {GridIsColliding.Contains(ent as IMyCubeGrid)}");
                _impactSize = ent.Physics.Mass;
                if (impactcheck && !GridIsColliding.Contains(ent as IMyCubeGrid))
                {
                    //Log.Line($"ContactPoint to WorldImpact: {contactpoint}");
                    _worldImpactPosition = contactpoint;
                }
                if (impactcheck && ent is IMyCubeGrid && !GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Add(ent as IMyCubeGrid);
                //if (impactcheck && _worldImpactPosition != Vector3D.NegativeInfinity) Log.Line($"intersect true: {ent} - ImpactSize: {_impactSize} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()} - _worldImpactPosition: {_worldImpactPosition}");
                return contactpoint;
            }
            //if (impactcheck) Log.Line($"intersect false: {ent.GetFriendlyName()} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
            if (ent is IMyCubeGrid && GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Remove(ent as IMyCubeGrid);
            return Vector3D.NegativeInfinity;
        }
        #endregion

        private void GridKillField()
        {
            try
            {
                var bigkillSphere = new BoundingSphereD(_detectionCenter, _range);
                var killList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref bigkillSphere);
                if (killList.Count == 0) return;
                MyAPIGateway.Parallel.ForEach(killList, killent =>
                {
                    var grid = killent as IMyCubeGrid;
                    if (grid == null || grid == Block.CubeGrid || !IsEnemy(killent) || Intersect(killent, false) == Vector3D.NegativeInfinity) return;

                    var contactPoint = ContactPoint(killent);
                    var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
                    //var worldPosition = killent.WorldVolume.Center;
                    //var worldDirection = contactPoint - worldPosition;
                    //var worldDirection = worldPosition - contactPoint;


                    var killSphere = new BoundingSphereD(contactPoint, 5f);
                    if (cpDist > 0.95f && _explode == false && _explodeCount == 0)
                    {
                        //Log.Line($"EXPLOSION! - dist:{cpDist}");
                        _explode = true;
                        MyVisualScriptLogicProvider.CreateExplosion(killSphere.Center, (float) killSphere.Radius, 20000);
                    }

                    if (!(cpDist <= 0.99)) return;
                    //Log.Line($"DoDamage - dist:{cpDist}");
                    var killBlocks = grid.GetBlocksInsideSphere(ref killSphere);
                    MyAPIGateway.Parallel.ForEach(killBlocks, block =>
                    {
                        block.DoDamage(99999f, MyDamageType.Fire, true, null, Block.EntityId);
                    });
                });

            } catch (Exception ex) { Log.Line($"Exception in GridKillField: {ex}"); }
        }

        #region Build inside HashSet
        private void InHashBuilder()
        {
            /*
            var insphere = new BoundingSphereD(_detectionCenter, _range - InOutSpace);
            var inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (!(inent is IMyCubeGrid) && (!(inent is IMyCharacter) || Intersect(inent, false) == Vector3D.NegativeInfinity)) return;
                lock (InHash)
                {
                    if (inent is IMyCubeGrid && IsEnemy(inent)) return;
                    InHash.Add(inent);
                }
            });
            */
        }
        #endregion

        #region Web and dispatch all intersecting entities
        private void QuickWebCheck()
        {
            var qWebsphere = new BoundingSphereD(_detectionCenter, _range);
            var qWebList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref qWebsphere);
            foreach (var webent in qWebList)
            {
                if (webent == null || webent is IMyFloatingObject || webent is IMyEngineerToolBase || webent == Block.CubeGrid) return;
                if (Block.CubeGrid.Physics.IsStatic && webent is IMyVoxelBase) continue;
                _enablePhysics = true;
                break;
            }
        }

        private void WebEntities()
        {
            //DSUtils.Sw.Start();
            var websphere = new BoundingSphereD(_detectionCenter, _range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (Intersect(webent, true) != Vector3D.NegativeInfinity)
                    {
                        _absorb += Shotdmg;
                        Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {webent} in loop {_count}");
                        if (webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo")) MyVisualScriptLogicProvider.CreateExplosion(webent.GetPosition(), 0, 0);
                        webent.Close();
                    }
                    return;
                }
                if (webent is IMyCharacter && (_count == 2 || _count == 17 || _count == 32 || _count == 47) && IsEnemy(webent) && Intersect(webent, true) != Vector3D.NegativeInfinity)
                {
                    Log.Line($"Enemy Player Intersected");
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid != null && grid != Block.CubeGrid && IsEnemy(webent))
                {
                    var intersect = Intersect(grid, true);
                    if (intersect != Vector3D.NegativeInfinity)
                    {
                        ContainmentField(grid, Block.CubeGrid, intersect);
                    }
                    return;
                }
                //Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
            //DSUtils.StopWatchReport("Web", -1);
        }
        #endregion

        #region player effects
        private void PlayerEffects()
        {
            var rnd = new Random();
            foreach (var playerent in InHash)
            {
                if (!(playerent is IMyCharacter)) continue;
                try
                {
                    var playerid = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                    var relationship = Block.GetUserRelationToOwner(playerid);
                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        var character = playerent as IMyCharacter;

                        var npcname = character.ToString();
                        //Log.Line($"playerEffect: Enemy {character} detected at loop {Count} - relationship: {relationship}");
                        if (npcname.Equals("Space_Wolf"))
                        {
                            Log.Line($"playerEffect: Killing {character}");
                            character.Kill();
                            return;
                        }
                        if (character.EnabledDamping) character.SwitchDamping();
                        if (character.SuitEnergyLevel > 0.5f) MyVisualScriptLogicProvider.SetPlayersEnergyLevel(playerid, 0.49f);
                        if (character.EnabledThrusts)
                        {
                            _playertime++;
                            var explodeRollChance = rnd.Next(0 - _playertime, _playertime);
                            if (explodeRollChance > 666)
                            {
                                _playertime = 0;
                                var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
                                var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
                                if (playerGasLevel > 0.01f)
                                {
                                    character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hydrogenId, (playerGasLevel * -0.0001f) + .002f);
                                    MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
                                    character.DoDamage(50f, MyDamageType.Fire, true);
                                    var vel = character.Physics.LinearVelocity;
                                    if (vel == new Vector3D(0, 0, 0))
                                    {
                                        vel = MyUtils.GetRandomVector3Normalized();
                                    }
                                    var speedDir = Vector3D.Normalize(vel);
                                    var randomSpeed = rnd.Next(10, 20);
                                    var additionalSpeed = vel + speedDir * randomSpeed;
                                    character.Physics.LinearVelocity = additionalSpeed;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in playerEffects: {ex}"); }
            }
            _playerwebbed = false;
        }
        #endregion

        #region Cleanup
        public override void Close()
        {
            try
            {
                DefenseShieldsBase.Instance.Shields.RemoveAt(DefenseShieldsBase.Instance.Shields.IndexOf(this));
            }
            catch { }
            base.Close();
        }

        public override void MarkForClose()
        {
            try { }
            catch { }
            base.MarkForClose();
        }
        #endregion

        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            ShieldVisable = newSettings.Enabled;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
        }

        public void SaveSettings()
        {
            if (DefenseShields.Storage == null)
                DefenseShields.Storage = new MyModStorageComponent();

            DefenseShields.Storage[DefenseShieldsBase.Instance.SETTINGS_GUID] = MyAPIGateway.Utilities.SerializeToXML(Settings);

            Log.Line("SaveSettings()");
        }

        private bool LoadSettings()
        {
            Log.Line("LoadSettings");

            if (DefenseShields.Storage == null)
                return false;

            string rawData;
            bool loadedSomething = false;

            if (DefenseShields.Storage.TryGetValue(DefenseShieldsBase.Instance.SETTINGS_GUID, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }

                Log.Line($"  Loaded settings:\n{Settings.ToString()}");
            }

            return loadedSomething;
        }

        public bool ShieldVisable
        {
            get { return Settings.Enabled; }
            set
            {
                Settings.Enabled = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public float Width
        {
            get { return Settings.Width; }
            set
            {
                Settings.Width = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Height
        {
            get { return Settings.Height; }
            set
            {
                Settings.Height = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Depth
        {
            get { return Settings.Depth; }
            set
            {
                Settings.Depth = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        private void RefreshControls(bool refreshRemoveButton = false, bool refeshCustomInfo = false)
        {
        }

        public void UseThisShip_Receiver(bool fix)
        {
            Log.Line($"UseThisShip_Receiver({fix})");

            //UseThisShip_Internal(fix);
        }
    }
}
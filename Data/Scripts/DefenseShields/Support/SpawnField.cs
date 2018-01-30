using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.VisualScripting;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static VRageMath.MathHelper;

namespace DefenseShields.Support
{
    class Spawn
    {
        #region Cube+subparts Class
        public class Utils
        {
            //Shell Entities
            public static IMyEntity Sphere(string displayName, string model)
            {
                try
                {
                    var ent = new MyEntity();
                    ent.Init(new StringBuilder(displayName), model, null, null, null);
                    MyAPIGateway.Entities.AddEntity(ent);
                    return ent;
                }
                catch (Exception ex)
                {
                    Log.Line($"Exception in Spawn shell entities");
                    Log.Line($"{ex}");
                    return null;
                }

            }
            //SPAWN METHOD
            public static IMyEntity SpawnShield(string subtypeId, string name = "", bool isVisible = true, bool hasPhysics = false, bool isStatic = false, bool toSave = false, bool destructible = false, long ownerId = 0)
            {
                try
                {
                    CubeGridBuilder.Name = name;
                    CubeGridBuilder.CubeBlocks[0].SubtypeName = subtypeId;
                    CubeGridBuilder.CreatePhysics = hasPhysics;
                    CubeGridBuilder.IsStatic = isStatic;
                    CubeGridBuilder.DestructibleBlocks = destructible;
                    var ent = MyAPIGateway.Entities.CreateFromObjectBuilder(CubeGridBuilder);

                    ent.Flags &= ~EntityFlags.Save;
                    ent.Visible = isVisible;
                    MyAPIGateway.Entities.AddEntity(ent, true);

                    return ent;
                }
                catch (Exception ex)
                {
                    Log.Line($"Exception in Spawn");
                    Log.Line($"{ex}");
                    return null;
                }
            }

            private static readonly SerializableBlockOrientation EntityOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up);

            //OBJECTBUILDERS
            private static readonly MyObjectBuilder_CubeGrid CubeGridBuilder = new MyObjectBuilder_CubeGrid()
            {

                EntityId = 0,
                GridSizeEnum = MyCubeSize.Large,
                IsStatic = true,
                Skeleton = new List<BoneInfo>(),
                LinearVelocity = Vector3.Zero,
                AngularVelocity = Vector3.Zero,
                ConveyorLines = new List<MyObjectBuilder_ConveyorLine>(),
                BlockGroups = new List<MyObjectBuilder_BlockGroup>(),
                Handbrake = false,
                XMirroxPlane = null,
                YMirroxPlane = null,
                ZMirroxPlane = null,
                PersistentFlags = MyPersistentEntityFlags2.InScene,
                Name = "ArtificialCubeGrid",
                DisplayName = "FieldEffect",
                CreatePhysics = false,
                DestructibleBlocks = true,
                PositionAndOrientation = new MyPositionAndOrientation(Vector3D.Zero, Vector3D.Forward, Vector3D.Up),

                CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
                {
                    new MyObjectBuilder_CubeBlock()
                    {
                        EntityId = 0,
                        BlockOrientation = EntityOrientation,
                        SubtypeName = "",
                        Name = "",
                        Min = Vector3I.Zero,
                        Owner = 0,
                        ShareMode = MyOwnershipShareModeEnum.None,
                        DeformationRatio = 0,
                    }
                }
            };
        }
        #endregion
    }

    public class Icosphere 
    {
    
        public readonly Vector3[] _vertexBuffer;

        public readonly int[][] _indexBuffer;


        public Icosphere(int lods)
        {
            const float X = 0.525731112119133606f;
            const float Z = 0.850650808352039932f;
            const float Y = 0;
            Vector3[] data =
            {
                /*
                new Vector3(0.000000f, 0.000000f, -1.000000f), new Vector3(0.723600f, -0.525720f, -0.447215f),
                new Vector3(-0.276385f, -0.850640f, -0.447215f), new Vector3(0.723600f, 0.525720f, -0.447215f),
                new Vector3(-0.894425f, 0.000000f, -0.447215f), new Vector3(-0.276385d, 0.850640f, -0.447215f),
                new Vector3(0.894425f, 0.000000f, 0.447215f), new Vector3(0.276385f, -0.850640f, 0.447215f),
                new Vector3(-0.723600f, -0.525720f, 0.447215f), new Vector3(-0.723600f, 0.525720f, 0.447215f),
                new Vector3(0.276385f, 0.850640f, 0.447215f), new Vector3(0.000000f, 0.000000f, 1.000000f)
                */
                new Vector3(-X, Y, Z), new Vector3(X, Y, Z), new Vector3(-X, Y, -Z), new Vector3(X, Y, -Z),
                new Vector3(Y, Z, X), new Vector3(Y, Z, -X), new Vector3(Y, -Z, X), new Vector3(Y, -Z, -X),
                new Vector3(Z, X, Y), new Vector3(-Z, X, Y), new Vector3(Z, -X, Y), new Vector3(-Z, -X, Y)
            };
            List<Vector3> points = new List<Vector3>(12 * (1 << (lods - 1)));
            points.AddRange(data);
            var index = new int[lods][];
            index[0] = new int[]
            {
                /*
                0, 1, 2, 1, 0, 3, 0, 2, 4, 0, 4, 5, 0, 5, 3, 1, 3, 6, 2, 1, 7,
                4, 2, 8, 5, 4, 9, 3, 5, 10, 1, 6, 7, 2, 7, 8, 4, 8, 9, 5, 9, 10,
                3, 10, 6, 7, 6, 11, 8, 7, 11, 9, 8, 11, 10, 9, 11, 6, 10, 11
                */
                0, 4, 1, 0, 9, 4, 9, 5, 4, 4, 5, 8, 4, 8, 1,
                8, 10, 1, 8, 3, 10, 5, 3, 8, 5, 2, 3, 2, 7, 3, 7, 10, 3, 7,
                6, 10, 7, 11, 6, 11, 0, 6, 0, 1, 6, 6, 1, 10, 9, 0, 11, 9,
                11, 2, 9, 2, 5, 7, 2, 11
            };
            for (var i = 1; i < lods; i++)
                index[i] = Subdivide(points, index[i - 1]);

            _indexBuffer = index;
            _vertexBuffer = points.ToArray();
        }
        private static int SubdividedAddress(IList<Vector3> pts, IDictionary<string, int> assoc, int a, int b)
        {
            string key = a < b ? (a + "_" + b) : (b + "_" + a);
            int res;
            if (assoc.TryGetValue(key, out res))
                return res;
            var np = pts[a] + pts[b];
            np.Normalize();
            pts.Add(np);
            assoc.Add(key, pts.Count - 1);
            return pts.Count - 1;
        }

        private static int[] Subdivide(IList<Vector3> vbuffer, IReadOnlyList<int> prevLod)
        {
            Dictionary<string, int> assoc = new Dictionary<string, int>();
            int[] res = new int[prevLod.Count * 4];
            int rI = 0;
            for (int i = 0; i < prevLod.Count; i += 3)
            {
                int v1 = prevLod[i];
                int v2 = prevLod[i + 1];
                int v3 = prevLod[i + 2];
                int v12 = SubdividedAddress(vbuffer, assoc, v1, v2);
                int v23 = SubdividedAddress(vbuffer, assoc, v2, v3);
                int v31 = SubdividedAddress(vbuffer, assoc, v3, v1);

                res[rI++] = v1;
                res[rI++] = v12;
                res[rI++] = v31;

                res[rI++] = v2;
                res[rI++] = v23;
                res[rI++] = v12;

                res[rI++] = v3;
                res[rI++] = v31;
                res[rI++] = v23;

                res[rI++] = v12;
                res[rI++] = v23;
                res[rI++] = v31;
            }

            return res;
        }

        public static long VertsForLod(int lod)
        {
            var shift = lod * 2;
            var k = (1L << shift) - 1;
            return 12 + 30 * (k & 0x5555555555555555L);
        }

        public class Instance
        {
            private readonly Icosphere _backing;

            private Vector3D[] _impactPos = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private Vector3D[] _localImpacts = { Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private Vector3D[] _preCalcNormLclPos;
            private Vector3D[] _vertexBuffer;
            private Vector3D[] _normalBuffer;
            private Vector4[] _triColorBuffer;

            private Vector3D _impactPosState;
            private MatrixD _matrix;

            private static readonly Random Random = new Random();

            public static DefenseShieldsBase Sphere { get; }

            private readonly SortedList<double, int> _faceLocSlist = new SortedList<double, int>();
            private readonly SortedList<double, int> _glichSlist = new SortedList<double, int>();

            private int[] _impactCount = new int[5];

            private int _mainLoop;
            private int _impactDrawStep;
            private int _modelCount;
            private int _glitchCount;
            private int _glitchStep;
            private int _chargeCount;
            private int _pulseCount;
            private int _pulse = 40;
            private int _prevLod;
            private int _lod;
            //private int _lod2;

            private const int GlitchSteps = 320;
            private const int ImpactSteps = 80;
            private const int ModelSteps = 22;
            private const int ImpactChargeSteps = 120;

            private double _firstHitFaceLoc1X;
            private double _lastHitFaceLoc1X;
            private double _firstFaceLoc1X;
            private double _lastFaceLoc1X;
            private double _firstFaceLoc2X;
            private double _lastFaceLoc2x;
            private double _firstFaceLoc4x;
            private double _lastFaceLoc4x;
            private double _firstFaceLoc6X;
            private double _lastFaceLoc6X;

            private double[] _impactLocSarray;
            private double[] _glitchLocSarray;

            private Vector4 _hitColor;
            private Vector4 _lineColor;
            private Vector4 _waveColor;
            private Vector4 _wavePassedColor;
            private Vector4 _waveComingColor;
            private Vector4 _glitchColor;
            private Vector4 _pulseColor;
            private Vector4 _chargeColor;
            private Vector4 _maxColor;
            private Vector4 _currentColor;
            private Vector4 _test1Color;
            private Vector4 _test2Color;

            private bool _impactCountFinished = true;
            private bool _charged = true;
            private bool _enemy;
            private bool _impact;

            private IMyEntity _shield;

            private readonly MyStringId _faceId1 = MyStringId.GetOrCompute("CustomIdle");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId2 = MyStringId.GetOrCompute("SunDisk");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01

            private readonly MyStringId _lineId = MyStringId.GetOrCompute("Square");

            Stopwatch sw = new Stopwatch();

            public void StopWatchReport(string message)
            {
                long ticks = sw.ElapsedTicks;
                double ns = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
                double ms = ns / 1000000.0;
                double s = ms / 1000;

                Log.Line($"{message} - ns:{ns} ms:{ms} s:{s}");
            }

            public Instance(Icosphere backing)
            {
                _backing = backing;
            }


            public void CalculateTransform(MatrixD matrix, int lod)
            {
                //Log.Line($"Start CalculateTransform");
                _lod = lod;
                var count = checked((int)VertsForLod(lod));
                Array.Resize(ref _vertexBuffer, count);
                Array.Resize(ref _normalBuffer, count);

                var normalMatrix = MatrixD.Transpose(MatrixD.Invert(matrix.GetOrientation()));

                for (var i = 0; i < count; i++)
                    Vector3D.Transform(ref _backing._vertexBuffer[i], ref matrix, out _vertexBuffer[i]);

                for (var i = 0; i < count; i++)
                    Vector3D.TransformNormal(ref _backing._vertexBuffer[i], ref normalMatrix, out _normalBuffer[i]);

                var ib = _backing._indexBuffer[_lod];
                Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
            }

            public void CalculateColor(MatrixD matrix, Vector3D impactPos, bool entChanged, bool enemy, IMyEntity shield)
            {
                _test1Color = Color.FromNonPremultiplied(0, 0, 0, 200);
                _test2Color = Color.FromNonPremultiplied(0, 0, 0, 200);
                //sw.Start();
                //Log.Line($"Start CalculateColor1");
                _shield = shield;
                _enemy = enemy;
                _matrix = matrix;
                _impactPosState = impactPos;
                if (impactPos == Vector3D.NegativeInfinity) _impact = false;
                else ComputeImpacts();
                StepEffects();
                InitColors();
                //Log.Line($"Impact True? {_impact} - Count? {_impactCount[4]} -Finished? {_impactCountFinished} - charged? {_charged}- chargeCount {_chargeCount}");
                //Log.Line($"impactPos {impactPos} - State {_impactPosState}");
                if (_impactCount[4] != 0 ) MyAPIGateway.Parallel.Start(Models);

                var ib = _backing._indexBuffer[_lod];
                Array.Resize(ref _triColorBuffer, ib.Length / 3);

                //Log.Line($"PreCalLeng: {_preCalcNormLclPos.Length} - Lod:{_lod}");
                if (entChanged || _prevLod != _lod)
                {
                    Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
                }
                for (int i = 0, j = 0; i < ib.Length; i += 3, j++)
                {
                    var i0 = ib[i];
                    var i1 = ib[i + 1];
                    var i2 = ib[i + 2];

                    var v0 = _vertexBuffer[i0];
                    var v1 = _vertexBuffer[i1];
                    var v2 = _vertexBuffer[i2];

                    if (entChanged || _prevLod != _lod)
                    {
                        var lclPos = (v0 + v1 + v2) / 3 - matrix.Translation;
                        var normlclPos = Vector3D.Normalize(lclPos);
                        _preCalcNormLclPos[j] = normlclPos;
                    }
                    if (_impactCount[4] != 0 || _glitchCount != 0)
                    {
                        //Log.Line($"impactCount and Glitch: {_impactCount[4]} - {_glitchCount}");
                        var pDotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _localImpacts[4]);
                        var primeImpactFactor = Math.Acos(pDotOfNormLclImpact);

                        const float pWaveMultiplier = Pi / ImpactSteps;
                        var pWavePosition = pWaveMultiplier * _impactCount[4];
                        var pRelativeToWavefront = Math.Abs(primeImpactFactor - pWavePosition);
                        //Log.Line($"primeImpactFactor: {primeImpactFactor} - Relative: {pRelativeToWavefront} - pWavePosition: {pWavePosition} - pWaveMultipler: {pWaveMultiplier} - _impactCount[4]: {_impactCount[4]}");
                        if (pWavePosition > primeImpactFactor) 
                        {
                            _triColorBuffer[j] = _test2Color;
                            continue;
                        }
                        //Log.Line($" color: {_currentColor} - _impactCountFished? {_impactCountFinished}");
                        if (!_impactCountFinished)
                        {
                            for (var s = 3; s >= 0; s--)
                            {
                                if (_localImpacts[s] == Vector3D.NegativeInfinity) continue;
                                var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _localImpacts[s]);
                                var impactFactor = Math.Acos(dotOfNormLclImpact);

                                _impactCount[s] = _impactCount[s] + 1;

                                const float waveMultiplier = Pi / ImpactSteps;
                                var wavePosition = waveMultiplier * _impactCount[s];
                                var relativeToWavefront = Math.Abs(impactFactor - wavePosition);
                                //Log.Line($"{relativeToWavefront}");
                                if (wavePosition > impactFactor)
                                {
                                    _triColorBuffer[j] = _waveColor;
                                }
                            }
                        }

                        //if (impactFactor < wavePosition && relativeToWavefront > 0.1 && relativeToWavefront < 0.15 && _impactCount != 0) _currentColor = _wavePassedColor;
                        //else if (_chargeCount != 0) _currentColor = _chargeColor;
                        //else _currentColor = _pulseColor;
                        //var trianglesRelativeToWavefront = (int)Math.Round(Math.Abs(impactFactor - wavePosition) / (Math.PI / (5 << _lod)));
                    }
                    //if (_impactCount[4] == 0 && !_charged) _triColorBuffer[j] = _waveColor;
                    else if (_impactCount[4] == 0) _triColorBuffer[j] = _test1Color;
                }
                _prevLod = _lod;

                /*
                sw.Stop();
                StopWatchReport("Icosphere CalcColor");
                sw.Reset();
                */
            }

            public void Draw(uint renderId)
            {
                //sw.Start();
                try
                {

                    MyStringId faceMaterial;
                    //Log.Line($"Start Draw");
                    var ib = _backing._indexBuffer[_lod];
                    //Log.Line($"after loading LOD");
                    for (int i = 0, j = 0; i < ib.Length; i += 3, j++)
                    {
                        var i0 = ib[i];
                        var i1 = ib[i + 1];
                        var i2 = ib[i + 2];

                        var v0 = _vertexBuffer[i0];
                        var v1 = _vertexBuffer[i1];
                        var v2 = _vertexBuffer[i2];

                        var n0 = _normalBuffer[i0];
                        var n1 = _normalBuffer[i1];
                        var n2 = _normalBuffer[i2];
                        var color = _triColorBuffer[j];
                        if (color == _pulseColor) faceMaterial = _faceId2;
                        else if (color == _currentColor) faceMaterial = _faceId2;
                        else if (color == _test1Color) faceMaterial = _faceId1;
                        else if (color == _test2Color) faceMaterial = _faceId2;
                        else if (color == _waveColor) faceMaterial = _faceId2;
                        else if (color == _waveComingColor) faceMaterial = _faceId2;
                        else if (color == _wavePassedColor) faceMaterial = _faceId2;
                        else if (color == _chargeColor) faceMaterial = _faceId2;
                        else faceMaterial = _faceId2;
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, n0, n1, n2, Vector2.Zero, new Vector2(0.5f, 0), new Vector2(0.5f), faceMaterial, renderId, (v0 + v1 + v2) / 3, color);
                    }
                }
                catch (Exception ex)
                {
                    Log.Line($" Exception in IcoSphere Draw - renderID {renderId}");
                    Log.Line($" {ex}");
                }
                /*
                sw.Stop();
                StopWatchReport("Icosphere Draw");
                sw.Reset();
                */
            }

            public void ComputeImpacts()
            {
                _impact = true;
                for (var i = 4; i >= 0; i--)
                {
                    if (_impactPos[i] != Vector3D.NegativeInfinity) continue;
                    _impactPos[i] = _impactPosState;
                    Log.Line($"Store impact position: {_impactPos[i]} in slot: {i}");
                    break;
                }
                //if (_impactCountFinished) localImpact = _impactPos[0] - matrix.Translation;
                for (int i = 4; i >= 0; i--)
                {
                    if (_impactPos[i] == Vector3D.NegativeInfinity) break;
                    Log.Line($"_localImpact assign and normalize impact: {_impactPos[i]} in slot: {i}");

                    _localImpacts[i] = _impactPos[i] - _matrix.Translation;
                    _localImpacts[i].Normalize();
                }
            }

            public void StepEffects()
            {
                _mainLoop++;

                if (_mainLoop == 61) _mainLoop = 0;
                if (_impactCount[4] != 0) _impactCount[4] = _impactCount[4] + 1;
                if (_glitchCount != 0) _glitchCount++;
                if (_chargeCount != 0) _chargeCount++;

                var rndNum1 = Random.Next(30, 69);
                /*
                if (_impactCount[4] == 0 && _glitchCount == 0 && _pulseCount == 239 && _pulseCount == rndNum1)
                {
                    _glitchCount = 1;
                    Log.Line($"Random Pulse: {_pulse}");
                }
                */
                if (_impact)
                {
                    if (_impactCount[4] != 0) _impactCountFinished = false;
                    if (_impactCount[4] == 0) _impactCount[4] = 1;
                    _glitchStep = 0;
                    _glitchCount = 0;
                    _chargeCount = 0;
                    _impactDrawStep = 0;
                    _charged = false;
                    _pulseCount = 0;
                    _pulse = 40;
                }
                if (_impactCount[4] == ImpactSteps + 1)
                {
                    _impactCountFinished = true;
                    _impactDrawStep = 0;
                    _chargeCount = 1;
                    _impactCount[0] = 0;
                    _impactCount[1] = 0;
                    _impactCount[2] = 0;
                    _impactCount[3] = 0;
                    _impactCount[4] = 0;
                    _impactPos[0] = Vector3D.NegativeInfinity;
                    _impactPos[1] = Vector3D.NegativeInfinity;
                    _impactPos[2] = Vector3D.NegativeInfinity;
                    _impactPos[3] = Vector3D.NegativeInfinity;
                    _impactPos[4] = Vector3D.NegativeInfinity;
                }
                if (_glitchCount == GlitchSteps + 1)
                {
                    _glitchCount = 0;
                    _glitchStep = 0;
                }
                if (_chargeCount == ImpactChargeSteps + 1)
                {
                    _charged = true;
                    _chargeCount = 0;
                    Array.Clear(_triColorBuffer, 0, _triColorBuffer.Length);
                }
            }

            public void Models()
            {               
                try
                {
                    var modPath = DefenseShieldsBase.Instance.ModPath();
                    if (_impactCount[4] == 1) _modelCount = 0;
                    var n = _modelCount;
                    if (_impactCount[4] % 2 == 1)
                    {
                        _shield.Render.Visible = true;
                        ((MyEntity)_shield).RefreshModels($"{modPath}\\Models\\LargeField{n}.mwm", null);
                        _shield.SetEmissiveParts("CWShield", Color.GhostWhite, 1);
                        _shield.SetEmissiveParts("CWShield.001", Color.GhostWhite, 1);
                        _shield.SetEmissiveParts("CWShield.002", Color.GhostWhite, 1);
                        _shield.SetEmissiveParts("CWShield.003", Color.GhostWhite, 1);
                        _shield.SetEmissiveParts("CWShield.004", Color.GhostWhite, 1);
                        _shield.Render.RemoveRenderObjects();
                        _shield.Render.UpdateRenderObject(true);
                        //Log.Line($"c:{_modelCount} - Asset:{_shield.Model.AssetName} - Vis:{_shield.Render.Visible}");
                        _modelCount++;
                    }
                    else _shield.Render.Visible = false;
                    if (_modelCount == 16) 
                    {
                        _modelCount = 0;
                        _shield.Render.Visible = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Line($" Exception in Models");
                    Log.Line($" {ex}");
                }
            }

            public void InitColors()
            {
                var cv1 = 0;
                var cv2 = 0;
                var cv3 = 0;
                var cv4 = 0;
                if (_enemy) cv1 = 75;
                else cv2 = 75;
                if (cv1 != 0) cv3 = cv1;
                if (cv2 != 0) cv4 = cv2;
                var rndNum1 = Random.Next(15, 27);
                var colorRnd1 = Random.Next(0, 15);
                var colorRnd2 = Random.Next(8, 255);
                var rndNum3 = Random.Next(55, 63);
                var rndNum4 = Random.Next(40, 120);

                //currentColor
                _currentColor = Color.FromNonPremultiplied(0, 0, 100, 16);

                //maxColor
                var vmaxColor = Color.FromNonPremultiplied(0, 0, 1, 1);
                _maxColor = vmaxColor;
                //waveColor
                var vwaveColor = Color.FromNonPremultiplied(cv3, 0, cv4, rndNum1 - 5);
                _waveColor = vwaveColor;

                //wavePassedColor
                var vwavePassedColor = Color.FromNonPremultiplied(0, 0, 12, colorRnd1);
                if (_impactCount[4] % 10 == 0)
                {
                    vwavePassedColor = Color.FromNonPremultiplied(0, 0, rndNum1, rndNum1 - 5);
                }
                _wavePassedColor = vwavePassedColor;

                //waveComingColor
                var vwaveComingColor = Color.FromNonPremultiplied(cv1, 0, cv2, 16);
                _waveComingColor = vwaveComingColor;

                //hitColor
                var vhitColor = Color.FromNonPremultiplied(0, 0, colorRnd2, rndNum1);
                _hitColor = vhitColor;

                //lineColor
                var vlineColor = Color.FromNonPremultiplied(cv1, 0, cv2, 32);
                _lineColor = vlineColor;

                //pulseColor
                if (_charged)
                {
                    if (_pulseCount < 60 && _pulseCount % 4 == 0)
                    {
                        _pulse -= 1;
                    }
                    else if (_pulseCount >= 60 && _pulseCount % 4 == 0)
                    {
                        _pulse += 1;
                    }
                    //Log.Line($"Pulse: {_pulse} Count: {_pulseCount}");
                    if (_pulseCount != 119) _pulseCount++;
                    else _pulseCount = 0;
                }

                var pulseColor1 = Color.FromNonPremultiplied(_pulse, 0, 0, 16);
                var pulseColor2 = Color.FromNonPremultiplied(0, 0, 100, 16);
                var vglitchColor = Color.FromNonPremultiplied(0, 0, rndNum4, rndNum1 - 5);
                _glitchColor = vglitchColor;
                if (_pulseCount == 119 && _pulseCount == rndNum3 && _glitchStep == 0)
                {
                    //_glitchCount = 1;
                    Log.Line($"Random Pulse: {_pulse}");
                }
                var vpulseColor = _enemy ? pulseColor1 : pulseColor2;
                _pulseColor = vpulseColor;

                //chargeColor
                var rndNum2 = Random.Next(1, 9);
                _chargeColor = Color.FromNonPremultiplied(0, 0, 0, 16 + _chargeCount / 6);
                if (_chargeCount % rndNum2 == 0)
                {
                    _chargeColor = Color.FromNonPremultiplied(0, 0, 0, 16 + _chargeCount / 8);
                }
            }

            private void LodNormalization()
            {
                Log.Line($"Previous lod was {_prevLod} current lod is {_lod}");
                var ixNew = Sphere.Icosphere._indexBuffer[_lod];
                var ixLenNew = ixNew.Length;
                var ixPrev = Sphere.Icosphere._indexBuffer[_prevLod];
                var ixLenPrev = ixPrev.Length;
                var gDivNew = MyMaths.FaceDivder(GlitchSteps, ixLenNew / 3);
                var iDivNew = MyMaths.FaceDivder(ImpactSteps, ixLenNew / 3);
                var gDivPrev = MyMaths.FaceDivder(GlitchSteps, ixLenPrev / 3);
                var iDivPrev = MyMaths.FaceDivder(ImpactSteps, ixLenPrev / 3);

                if ((gDivNew < 1 || iDivNew < 1) && (gDivPrev >= 1 || iDivPrev >= 1) || (gDivPrev < 1 || iDivPrev < 1) && (gDivNew >= 1 || iDivNew >= 1))
                {
                    Log.Line($"Lod change passed threshold requires renormalization");
                    // temp fix
                    _glitchStep = 0;
                    _glitchCount = 0;
                    _impactCount[4] = 0;
                    _impactDrawStep = 0;
                    //
                }
            }

            public void BuildCollections(MatrixD matrix, Vector3D ImpactPos)
            {
                var lodChange = (_impactCount[4] != 0 || _glitchCount != 0) && _lod != _prevLod;
                if (lodChange) LodNormalization();

                var ix = _backing._indexBuffer[_lod];
                var ixLen = ix.Length;
                _prevLod = _lod;

                if (_glitchCount != 0)
                {
                    var faceDiv = MyMaths.FaceDivder(GlitchSteps, ixLen / 3);
                    if (faceDiv != 0 && MyMaths.Mod(_glitchCount, faceDiv) >= 0)
                    {
                        _glitchStep++;
                        if (_glitchStep == 1 || lodChange)
                        {
                            _glichSlist.Clear();
                            var f = faceDiv / 2;
                            if (faceDiv <= 1) f = 1;

                            var glitchRndNum1 = Random.Next(0, 9999999);
                            var glitchRndNum2 = Random.Next(0, 9999999);
                            var glitchRndNum3 = Random.Next(0, 9999999);
                            var zeroPos = new Vector3D(glitchRndNum1, glitchRndNum2, glitchRndNum3);
                            var localImpact = Vector3D.Transform(zeroPos, MatrixD.Invert(matrix));
                            localImpact.Normalize();
                            _glitchLocSarray = new double[ixLen / 3 / f];
                            var j = 0;
                            for (var i = 0; i < ixLen - 2; i += 3 * f, j++)
                            {
                                var i0 = ix[i];
                                var i1 = ix[i + 1];
                                var i2 = ix[i + 2];

                                //Log.Line($"{ixLen} - {ixLen / 3} - {faceDiv} - {i} - {_lod} - {_prevLod} - {firstFace6X} - {lastFace6X} - {_glitchCount}");

                                var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                                fnorm.Normalize();

                                var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                                _glitchLocSarray[j] = impactFactor;
                                //_glichSlist.Add(impactFactor, i);
                                if (i == 0 || _glitchLocSarray.Length == ixLen / 3 || _glitchLocSarray.Length == ixLen / 3 * faceDiv || _glitchLocSarray.Length == ixLen / 3 / f) Log.Line($"g:{i} - f:{f} - ixLen:{ixLen} - dbLen:{_glitchLocSarray.Length}");
                            }
                        }
                        if (faceDiv <= 1)
                        {
                            var firstFace6X = _glitchStep - 1;
                            var lastFace6X = firstFace6X;
                            if (lodChange || _glitchStep == 1 || _glitchStep == GlitchSteps / faceDiv * -1 || _glitchStep == GlitchSteps / faceDiv) Log.Line($"g1 - s:{_glitchStep} - Div:{faceDiv} - 1:{firstFace6X} - 2:{lastFace6X} - dbLen:{_glitchLocSarray.Length}");
                            //Log.Line($"g1 - s:{_glitchStep} - Div:{faceDiv} - 1:{firstFace6X} - 2:{lastFace6X} - dbLen:{_glichSlist.Count}");
                            _firstFaceLoc1X = _glitchLocSarray[firstFace6X];
                            _lastFaceLoc1X = _glitchLocSarray[lastFace6X];
                        }
                        else
                        {
                            var firstFace6X = _glitchStep * 2 - 2;
                            var lastFace6X = firstFace6X + 1;
                            if (lodChange || _glitchStep == 1 || _glitchStep == GlitchSteps) Log.Line($"g1 - s:{_glitchStep} - Div:{faceDiv} - 1:{firstFace6X} - 2:{lastFace6X} - dbLen:{_glitchLocSarray.Length}");
                            //Log.Line($"g1 - s:{_glitchStep} - Div:{faceDiv} - 1:{firstFace6X} - 2:{lastFace6X} - dbLen:{_glichSlist.Count}");
                            _firstFaceLoc1X = _glitchLocSarray[firstFace6X];
                            _lastFaceLoc1X = _glitchLocSarray[lastFace6X];
                        }
                    }
                }

                if (_impactCount[4] != 0)
                {
                    var faceDiv = MyMaths.FaceDivder(ImpactSteps, ixLen / 3);
                    if (faceDiv != 0 && MyMaths.Mod(_impactCount[4], faceDiv) >= 0)
                    {
                        _impactDrawStep++;
                        if (_impactDrawStep == 1 || lodChange)
                        {
                            _faceLocSlist.Clear();
                            var f = faceDiv / 2;
                            if (faceDiv <= 1) f = 1;
                            for (var i = 0; i < ixLen - 2; i += 3 * f)
                            {
                                var i0 = ix[i];
                                var i1 = ix[i + 1];
                                var i2 = ix[i + 2];
                                var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                                fnorm.Normalize();
                                var localImpact = Vector3D.Transform(ImpactPos, MatrixD.Invert(matrix));
                                localImpact.Normalize();
                                var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                                _faceLocSlist.Add(impactFactor, i);
                                if (i == 0 || _faceLocSlist.Count == ixLen / 3 || _faceLocSlist.Count == ixLen / 3 * faceDiv || _faceLocSlist.Count == ixLen / 3 / f) Log.Line($"i:{i} - f:{f} - ixLen:{ixLen} - dbLen:{_faceLocSlist.Count}");
                            }
                        }
                        if (faceDiv <= 1)
                        {
                            var firstFace1X = _impactDrawStep - 1;
                            var lastFace1X = firstFace1X;
                            if (lodChange || _impactDrawStep == 1 || _impactDrawStep == ImpactSteps / faceDiv * -1 || _impactDrawStep == ImpactSteps / faceDiv) Log.Line($"i1 - s:{_impactDrawStep} - Div:{faceDiv} - 1:{firstFace1X} - 2:{lastFace1X} - dbLen:{_faceLocSlist.Count}");
                            //Log.Line($"i1 - s:{_impactDrawStep} - Div:{faceDiv} - 1:{firstFace1X} - 2:{lastFace1X} - dbLen:{_faceLocSlist.Count}");
                            _firstFaceLoc1X = _faceLocSlist.ElementAt(firstFace1X).Key;
                            _lastFaceLoc1X = _faceLocSlist.ElementAt(lastFace1X).Key;
                            if (_impactDrawStep == 1)
                            {
                                _firstHitFaceLoc1X = _faceLocSlist.ElementAt(firstFace1X).Key;
                                _lastHitFaceLoc1X = _faceLocSlist.ElementAt(lastFace1X).Key;
                            }
                        }
                        else
                        {
                            var firstFace1X = _impactDrawStep * 2 - 2;
                            var lastFace1X = firstFace1X + 1;
                            if (lodChange || _impactDrawStep == 1 || _impactDrawStep == ImpactSteps) Log.Line($"i1 - s:{_impactDrawStep} - Div:{faceDiv} - 1:{firstFace1X} - 2:{lastFace1X} - dbLen:{_faceLocSlist.Count}");
                            //Log.Line($"i2 - s:{_impactDrawStep} - Div:{faceDiv} - 1:{firstFace1X} - 2:{lastFace1X} - dbLen:{_faceLocSlist.Count}");
                            _firstFaceLoc1X = _faceLocSlist.ElementAt(firstFace1X).Key;
                            _lastFaceLoc1X = _faceLocSlist.ElementAt(lastFace1X).Key;
                            if (_impactDrawStep == 1)
                            {
                                _firstHitFaceLoc1X = _faceLocSlist.ElementAt(firstFace1X).Key;
                                _lastHitFaceLoc1X = _faceLocSlist.ElementAt(lastFace1X).Key;
                            }
                        }
                    }
                }
            }
        }

        //
        // Code
        //
        // vec3 localSpherePositionOfImpact;
        //    foreach (vec3 triangleCom in triangles) {
        //    var surfDistance = Math.acos(dot(triangleCom, localSpherePositionOfImpact));
        // }
        //
        //
        // surfDistance will be the distance, along the surface, between the impact point and the triangle
        // Equinox - It won't distort properly for anything that isn't a sphere
        // localSpherePositionOfImpact = a direction
        // triangleCom is another direction
        // Dot product is the cosine of the angle between them
        // Acos gives you that angle in radians
        // Multiplying by the sphere radius(1 for the unit sphere in question) gives the arc length
        // Compared to sorting a list containing every single triangle?
        // Probably a factor of log(n) where n is the triangle count
        // So maybe 10x or so
        // Equinox in that example what is triangles?
        // all the triangles to get rendered
        // So you'd do that calculation right before AddTriangleBillboard

        /*
        if (faceMaterial.HasValue && _lod > 2)
            if (_impactCount != 0)
                if (impactFactor <= _lastHitFaceLoc1x)

                if (impactFactor >= _firstFaceLoc1x && impactFactor <= _lastFaceLoc1x)

                if (impactFactor < _lastFaceLoc1x)

                if (impactFactor > _lastFaceLoc1x)
            else if (_chargeCount != 0)
            else if (_glitchCount != 0)
        */
        //var localImpact = Vector3D.Transform(_impactPos, MatrixD.Invert(matrix));
        //localImpact.Normalize();

        //var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
        //fnorm.Normalize();
        //var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
        //var matrixTranslation = matrix.Translation;
    }
}

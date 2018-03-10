using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static VRageMath.MathHelper;

namespace DefenseShields.Support
{
    #region Spawn
    class Spawn
    {
        //Shell Entities
        public IMyEntity EmptyEntity(string displayName, string model)
        {
            try
            {
                var ent = new MyEntity();
                ent.Init(new StringBuilder(displayName), model, null, null, null);
                MyAPIGateway.Entities.AddEntity(ent);
                return ent;
            }
            catch (Exception ex) { Log.Line($"Exception in EmptyEntity: {ex}"); return null; }
        }
    }
    #endregion

    public class Icosphere 
    {
    
        public readonly Vector3[] VertexBuffer;

        public readonly int[][] IndexBuffer;


        public Icosphere(int lods)
        {
            const float x = 0.525731112119133606f;
            const float z = 0.850650808352039932f;
            const float y = 0;
            Vector3[] data =
            {
                new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(-x, y, -z), new Vector3(x, y, -z),
                new Vector3(y, z, x), new Vector3(y, z, -x), new Vector3(y, -z, x), new Vector3(y, -z, -x),
                new Vector3(z, x, y), new Vector3(-z, x, y), new Vector3(z, -x, y), new Vector3(-z, -x, y)
            };
            List<Vector3> points = new List<Vector3>(12 * (1 << (lods - 1)));
            points.AddRange(data);
            var index = new int[lods][];
            index[0] = new int[]
            {
                0, 4, 1, 0, 9, 4, 9, 5, 4, 4, 5, 8, 4, 8, 1,
                8, 10, 1, 8, 3, 10, 5, 3, 8, 5, 2, 3, 2, 7, 3, 7, 10, 3, 7,
                6, 10, 7, 11, 6, 11, 0, 6, 0, 1, 6, 6, 1, 10, 9, 0, 11, 9,
                11, 2, 9, 2, 5, 7, 2, 11
            };
            for (var i = 1; i < lods; i++)
                index[i] = Subdivide(points, index[i - 1]);

            IndexBuffer = index;
            VertexBuffer = points.ToArray();
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

            private readonly Vector3D[] _impactPos = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private readonly Vector3D[] _localImpacts = { Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private Vector3D[] _preCalcNormLclPos;
            private Vector3D[] _vertexBuffer;
            private Vector3D[] _physicsBuffer;

            private Vector3D[] _normalBuffer;
            private Vector4[] _triColorBuffer;

            private Vector3D _impactPosState;
            private MatrixD _matrix;

            private static readonly Random Random = new Random();

            private readonly int[] _impactCount = {0, 0, 0, 0, 0};

            private int _mainLoop;
            private int _impactDrawStep;
            private int _modelCount;
            private int _glitchCount;
            private int _glitchStep;
            private int _chargeCount;
            private int _pulseCount;
            private int _pulse = 40;
            private int _lod;

            private const int GlitchSteps = 320;
            private const int ImpactSteps = 80;
            private const int ImpactChargeSteps = 120;

            private Vector4 _hitColor;
            private Vector4 _waveColor;
            private Vector4 _wavePassedColor;
            private Vector4 _waveComingColor;
            private Vector4 _glitchColor;
            private Vector4 _pulseColor;
            private Vector4 _chargeColor;
            private Vector4 _maxColor;
            private Vector4 _defaultColor;

            private bool _impactsFinished = true;
            private bool _charged = true;
            private bool _enemy;
            private bool _impact;
            private bool _effectsDone;

            private IMyEntity _shield;

            private readonly MyStringId _faceId1 = MyStringId.GetOrCompute("CustomIdle");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId2 = MyStringId.GetOrCompute("SunDisk");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId3 = MyStringId.GetOrCompute("Glass");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId4 = MyStringId.GetOrCompute("CockpitGlassInside");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01


            public Instance(Icosphere backing)
            {
                _backing = backing;
            }

            private int[,] ComputeVertexToTris(int lod)
            {
                var data = new int[_vertexBuffer.Length, 6];
                var ib = _backing.IndexBuffer[lod];
                for (var i = 0; i < ib.Length; i += 3)
                {
                    AddInlineList(data, ib[i], i);
                    AddInlineList(data, ib[i], i);
                    AddInlineList(data, ib[i], i);
                }

                return data;
            }

            private static void AddInlineList(int[,] list, int index, int val)
            {
                var capacity = list.GetLength(1);
                var count = -list[index, capacity - 1];
                if (count < 0)
                    throw new Exception("Failed to add to list.  Overflow");
                list[index, count] = val;
                count++;
                if (count < capacity)
                    list[index, capacity - 1] = -count;
            }

            public void CalculateTransform(MatrixD matrix, int lod)
            {
                _lod = lod;
                var count = checked((int)VertsForLod(lod));
                Array.Resize(ref _vertexBuffer, count);
                Array.Resize(ref _normalBuffer, count);

                var normalMatrix = MatrixD.Transpose(MatrixD.Invert(matrix.GetOrientation()));
                for (var i = 0; i < count; i++)
                    Vector3D.Transform(ref _backing.VertexBuffer[i], ref matrix, out _vertexBuffer[i]);

                for (var i = 0; i < count; i++)
                    Vector3D.TransformNormal(ref _backing.VertexBuffer[i], ref normalMatrix, out _normalBuffer[i]);

                var ib = _backing.IndexBuffer[_lod];
                Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
            }

            public Vector3D[] ReturnPhysicsVerts(MatrixD matrix, int lod)
            {
                Vector3D[] physicsArray;
                switch (lod)
                {
                    case 0:
                        physicsArray = new Vector3D[12];
                        break;
                    case 1:
                        physicsArray = new Vector3D[42];
                        break;
                    case 2:
                        physicsArray = new Vector3D[162];
                        break;
                    case 3:
                        physicsArray = new Vector3D[642];
                        break;
                    default:
                        physicsArray = new Vector3D[2562];
                        break;
                }

                for (var i = 0; i < physicsArray.Length; i++)
                    Vector3D.Transform(ref _backing.VertexBuffer[i], ref matrix, out physicsArray[i]);

                return physicsArray;
            }

            public void ComputeEffects(MatrixD matrix, Vector3D impactPos, float impactSize, bool entChanged, bool enemy, bool sphereOnCamera, IMyEntity shield, int prevLod)
            {
                _shield = shield;
                _enemy = enemy;
                _matrix = matrix;
                _impactPosState = impactPos;

                if (impactPos == Vector3D.NegativeInfinity) _impact = false;
                else ComputeImpacts();

                if (impactSize <= 10) impactSize = (int)4;
                else impactSize = (int)1;
                var impactSpeed = 2;
                if (impactSize < 4) impactSpeed = 1;

                if (entChanged || prevLod != _lod)
                {
                    Log.Line($"ComputeEffects - entChanged: {entChanged} - lod: {_lod} - prevlod: {prevLod}");
                    var ib = _backing.IndexBuffer[_lod];
                    Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
                    Array.Resize(ref _triColorBuffer, ib.Length / 3);
                }

                StepEffects();
                InitColors();
                if (_impactsFinished && !(entChanged || prevLod != _lod)) return;

                Log.Line($"impacts: {_impactCount[4]} {_impactCount[3]} {_impactCount[2]} {_impactCount[1]} {_impactCount[0]}");

                if (_impactCount[4] != 0) MyAPIGateway.Parallel.Start(Models);
                ColorAssignments(entChanged, impactSize, impactSpeed, prevLod);
                // vec3 localSpherePositionOfImpact;
                //    foreach (vec3 triangleCom in triangles) {
                //    var surfDistance = Math.acos(dot(triangleCom, localSpherePositionOfImpact));
                // }
                // surfDistance will be the distance, along the surface, between the impact point and the triangle
                // Equinox - It won't distort properly for anything that isn't a sphere
                // localSpherePositionOfImpact = a direction
                // triangleCom is another direction
                // Dot product is the cosine of the angle between them
                // Acos gives you that angle in radians
                // Multiplying by the sphere radius(1 for the unit sphere in question) gives the arc length.
            }

            private void ColorAssignments(bool entChanged, float impactSize, int impactSpeed, int prevLod)
            {
                var ib = _backing.IndexBuffer[_lod];

                for (int i = 0, j = 0; i < ib.Length; i += 3, j++)
                {
                    var i0 = ib[i];
                    var i1 = ib[i + 1];
                    var i2 = ib[i + 2];

                    var v0 = _vertexBuffer[i0];
                    var v1 = _vertexBuffer[i1];
                    var v2 = _vertexBuffer[i2];

                    if (entChanged || prevLod != _lod)
                    {
                        Log.Line($"colorAssignments - entChanged: {entChanged} - lod: {_lod} - prevlod: {prevLod}");
                        var lclPos = (v0 + v1 + v2) / 3 - _matrix.Translation;
                        var normlclPos = Vector3D.Normalize(lclPos);
                        _preCalcNormLclPos[j] = normlclPos;
                        for (int c = 0; c < _triColorBuffer.Length; c++)
                            _triColorBuffer[c] = _defaultColor;
                    }
                    if (!_impactsFinished || _glitchCount != 0)
                    {
                        for (int s = 4; s > -1; s--)
                        {
                            if (_localImpacts[s] == Vector3D.NegativeInfinity) continue;
                            var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _localImpacts[s]);
                            var impactFactor = Math.Acos(dotOfNormLclImpact);

                            var waveMultiplier = Pi / ImpactSteps / impactSize;
                            var wavePosition = waveMultiplier * _impactCount[s];
                            var relativeToWavefront = Math.Abs(impactFactor - wavePosition);
                            if (relativeToWavefront < .03)
                            {
                                // within 1/180th of wavefront
                                _triColorBuffer[j] = _defaultColor;
                            }
                            else if (impactFactor < wavePosition && relativeToWavefront > 0.1 && relativeToWavefront < 0.15)
                            {
                                _triColorBuffer[j] = _waveColor;
                            }
                        }
                    }
                    else if (_impactCount[4] == 0) _triColorBuffer[j] = _defaultColor;
                }
            }

            public void Draw(uint renderId)
            {
                try
                {
                    var faceMaterial = _faceId2;
                    var ib = _backing.IndexBuffer[_lod];
                    var v21 = new Vector2(0.5f, 0);
                    var v22 = new Vector2(0.5f);
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
                        if (color == _defaultColor) faceMaterial = _faceId1;
                        else if (color == _waveColor) faceMaterial = _faceId2;
                        //else if (color == _pulseColor) faceMaterial = _faceId1;
                        //else if (color == _test1Color) faceMaterial = _faceId4;
                        //else if (color == _test2Color) faceMaterial = _faceId4;
                        //else if (color == _waveComingColor) faceMaterial = _faceId1;
                        //else if (color == _wavePassedColor) faceMaterial = _faceId1;
                        //else if (color == _chargeColor) faceMaterial = _faceId1;
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, n0, n1, n2, Vector2.Zero, v21, v22, faceMaterial, renderId, (v0 + v1 + v2) / 3, color);
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in IcoSphere Draw - renderId {renderId}: {ex}"); }
            }

            private void ComputeImpacts()
            {
                _impact = true;
                for (var i = 4; i >= 0; i--)
                {
                    if (_impactPos[i] != Vector3D.NegativeInfinity) continue;
                    _impactPos[i] = _impactPosState;
                    break;
                }
                for (int i = 4; i >= 0; i--)
                {
                    if (_impactPos[i] == Vector3D.NegativeInfinity) break;

                    _localImpacts[i] = _impactPos[i] - _matrix.Translation;
                    _localImpacts[i].Normalize();
                }
            }

            private void StepEffects()
            {
                _mainLoop++;

                if (_mainLoop == 61) _mainLoop = 0;
                //if (_impactCount[4] != 0) _impactCount[4] = _impactCount[4] + 1;
                if (_glitchCount != 0) _glitchCount++;
                if (_chargeCount != 0) _chargeCount++;

                var rndNum1 = Random.Next(30, 69);
                if (_impactCount[4] == 0 && _glitchCount == 0 && _pulseCount == 239 && _pulseCount == rndNum1)
                {
                    //_glitchCount = 1;
                    //Log.Line($"Random Pulse: {_pulse}");
                }
                if (_impact)
                {
                    _impactsFinished = false;
                    _glitchStep = 0;
                    _glitchCount = 0;
                    _chargeCount = 0;
                    _impactDrawStep = 0;
                    _charged = false;
                    _pulseCount = 0;
                    _pulse = 40;
                }
                if (!_impactsFinished)
                {
                    for (int i = 0; i < _impactCount.Length; i++)
                        if (_impactPos[i] != Vector3D.NegativeInfinity) _impactCount[i] += 1;

                    if (_impactCount[0] == ImpactSteps + 1)
                    {
                        _impactCount[0] = 0;
                        _impactPos[0] = Vector3D.NegativeInfinity;
                    }
                    if (_impactCount[1] == ImpactSteps + 1)
                    {
                        _impactCount[1] = 0;
                        _impactPos[1] = Vector3D.NegativeInfinity;
                    }
                    if (_impactCount[2] == ImpactSteps + 1)
                    {
                        _impactCount[2] = 0;
                        _impactPos[2] = Vector3D.NegativeInfinity;
                    }
                    if (_impactCount[3] == ImpactSteps + 1)
                    {
                        _impactCount[3] = 0;
                        _impactPos[3] = Vector3D.NegativeInfinity;
                    }
                    if (_impactCount[4] == ImpactSteps + 1)
                    {
                        _impactCount[4] = 0;
                        _impactPos[4] = Vector3D.NegativeInfinity;
                    }
                    if (_impactCount[0] == 0 && _impactCount[1] == 0 && _impactCount[2] == 0 && _impactCount[3] == 0 && _impactCount[4] == 0)
                    {
                        _impactsFinished = true;
                        _impactDrawStep = 0;
                        _chargeCount = 1;
                        for (int i = 0; i < _triColorBuffer.Length; i++)
                            _triColorBuffer[i] = _defaultColor;
                    }
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
                }
                if (_glitchCount == 0 && _impactCount[4] == 0 && _charged && !_impact) _effectsDone = true;
                else _effectsDone = false;
            }

            private void Models()
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
                        _shield.Render.RemoveRenderObjects();
                        _shield.Render.UpdateRenderObject(true);
                        if (n < 3)_shield.SetEmissiveParts("CWShield", Color.DarkViolet, 1);
                        if (n >= 3 && n < 6) _shield.SetEmissiveParts("CWShield.001", Color.DarkViolet, 1);
                        if (n >= 6 && n < 9) _shield.SetEmissiveParts("CWShield.002", Color.DarkViolet, 1);
                        if (n >= 9 && n < 12) _shield.SetEmissiveParts("CWShield.003", Color.DarkViolet, 1);
                        if (n >= 12 && n < 15) _shield.SetEmissiveParts("CWShield.004", Color.DarkViolet, 1);
                        if (n == 15) _shield.SetEmissiveParts("CWShield.005", Color.DarkViolet, 1);

                        _modelCount++;
                        if (_modelCount == 16) _modelCount = 0;
                    }
                    else _shield.Render.Visible = false;
                    if (_impactCount[4] == ImpactSteps) 
                    {
                        _modelCount = 0;
                        _shield.Render.Visible = false;
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in Models: {ex}"); }
            }

            private void InitColors()
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
                _defaultColor = Color.FromNonPremultiplied(0, 0, 0, 32);

                //waveColor
                //var vwaveColor = Color.FromNonPremultiplied(cv3, 0, cv4, rndNum1 - 5);
                var vwaveColor = Color.FromNonPremultiplied(0, 0, 0, 200);
                _waveColor = vwaveColor;

                //maxColor
                var vmaxColor = Color.FromNonPremultiplied(0, 0, 1, 1);
                _maxColor = vmaxColor;

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
        }
    }
}

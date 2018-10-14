using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using static VRageMath.MathHelper;

namespace DefenseShields.Support
{
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
            string key = a < b ? (a.ToString() + "_" + b.ToString()) : (b.ToString() + "_" + a.ToString());
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

            public double[] SideDistArray = {
                0, 0, 0, 0, 0, 0
            };

            public string[] SideNameArray = {
                "ShieldLeft", "ShieldRight", "ShieldTop", "ShieldBottom", "ShieldFront", "ShieldBack"
            };

            public MyEntitySubpart[] SidePartArray = {
                null, null, null, null, null, null
            };

            private readonly Vector3D[] _impactPos = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private readonly Vector3D[] _localImpacts = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private Vector3D[] _preCalcNormLclPos;
            private Vector3D[] _vertexBuffer;
            private Vector3D[] _physicsBuffer;

            private Vector3D[] _normalBuffer;
            private int[] _triColorBuffer;

            private Vector3D _impactPosState;
            private Vector3D _refreshPoint;
            private MatrixD _matrix;
            private Vector2 _v20 = new Vector2(.5f);
            private Vector2 _v21 = new Vector2(0.25f);
            private Vector2 _v22 = new Vector2(0.25f);

            private const string ShieldEmissiveAlpha = "ShieldEmissiveAlpha";

            private static readonly Random Random = new Random();

            private readonly int[] _impactCnt = new int[6];
            private readonly int[] _sideLoops = new int[6];
            private readonly List<int> _hitFaces = new List<int>();

            private int _mainLoop = -1;
            private int _lCount;
            private int _longerLoop;
            private int _refreshDrawStep;
            private int _lod;

            private const int SideSteps = 60;
            private const int ImpactSteps = 60;
            private const int RefreshSteps = 30;

            private Color _activeColor = Color.Transparent;
            private readonly Vector4 _waveColor = Color.FromNonPremultiplied(0, 0, 0, 84);
            private readonly Vector4 _refreshColor = Color.FromNonPremultiplied(255, 255, 255, 255);
            public bool ImpactsFinished = true;
            private bool _impact;
            private bool _refresh;
            private bool _active;

            public MyEntity ShellActive;

            private readonly MyStringId _faceCharge = MyStringId.GetOrCompute("Charge");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceWave = MyStringId.GetOrCompute("GlassOutside");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private MyStringId _faceMaterial;

            private DSUtils _dsutil1 = new DSUtils();

            public Instance(Icosphere backing)
            {
                _backing = backing;
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

            public Vector3D[] CalculatePhysics(MatrixD matrix, int lod)
            {
                var count = checked((int)VertsForLod(lod));
                Array.Resize(ref _physicsBuffer, count);

                for (var i = 0; i < count; i++)
                    Vector3D.Transform(ref _backing.VertexBuffer[i], ref matrix, out _physicsBuffer[i]);

                var ib = _backing.IndexBuffer[lod];
                var vecs = new Vector3D[ib.Length];
                for (int i = 0; i < ib.Length; i += 3)
                {
                    var i0 = ib[i];
                    var i1 = ib[i + 1];
                    var i2 = ib[i + 2];
                    var v0 = _physicsBuffer[i0];
                    var v1 = _physicsBuffer[i1];
                    var v2 = _physicsBuffer[i2];

                    vecs[i] = v0;
                    vecs[i + 1] = v1;
                    vecs[i + 2] = v2;
                }
                return vecs;
            }

            public void ReturnPhysicsVerts(MatrixD matrix, Vector3D[] physicsArray)
            {
                for (var i = 0; i < physicsArray.Length; i++)
                {
                    var num1 = (_backing.VertexBuffer[i].X * matrix.M11 + _backing.VertexBuffer[i].Y * matrix.M21 + _backing.VertexBuffer[i].Z * matrix.M31) + matrix.M41;
                    var num2 = (_backing.VertexBuffer[i].X * matrix.M12 + _backing.VertexBuffer[i].Y * matrix.M22 + _backing.VertexBuffer[i].Z * matrix.M32) + matrix.M42;
                    var num3 = (_backing.VertexBuffer[i].X * matrix.M13 + _backing.VertexBuffer[i].Y * matrix.M23 + _backing.VertexBuffer[i].Z * matrix.M33) + matrix.M43;
                    var num4 = 1 / ((((_backing.VertexBuffer[i].X * matrix.M14) + (_backing.VertexBuffer[i].Y * matrix.M24)) + (_backing.VertexBuffer[i].Z * matrix.M34)) + matrix.M44);
                    Vector3D vector3;
                    vector3.X = num1 * num4;
                    vector3.Y = num2 * num4;
                    vector3.Z = num3 * num4;
                    physicsArray[i] = vector3;
                }
            }

            public void ComputeEffects(MatrixD matrix, Vector3D impactPos, MyEntity shellPassive, MyEntity shellActive, int prevLod, float shieldPercent, bool activeVisible, bool refreshAnim)
            {
                if (ShellActive == null) ComputeSides(shellActive);

                var newActiveColor = UtilsStatic.GetShieldColorFromFloat(shieldPercent);
                _activeColor = newActiveColor;

                _matrix = matrix;
                _impactPosState = impactPos;
                _active = activeVisible;

                if (prevLod != _lod)
                {
                    var ib = _backing.IndexBuffer[_lod];
                    Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
                    Array.Resize(ref _triColorBuffer, ib.Length / 3);
                }

                StepEffects();

                if (refreshAnim && _refresh && ImpactsFinished && prevLod == _lod) RefreshColorAssignments(prevLod);
                if (ImpactsFinished && prevLod == _lod) return;

                ImpactColorAssignments(prevLod);
                //_dsutil2.StopWatchReport("colorcalc", 1);
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

            private void ImpactColorAssignments(int prevLod)
            {
                try
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

                        if (prevLod != _lod)
                        {
                            var lclPos = (v0 + v1 + v2) / 3 - _matrix.Translation;
                            var normlclPos = Vector3D.Normalize(lclPos);
                            _preCalcNormLclPos[j] = normlclPos;
                            for (int c = 0; c < _triColorBuffer.Length; c++)
                                _triColorBuffer[c] = 0;
                        }
                        if (!ImpactsFinished)
                        {
                            for (int s = 0; s < 6; s++)
                            {
                                // basically the same as for a sphere: offset by radius, except the radius will depend on the axis
                                // if you already have the mesh generated, it's easy to get the vector from point - origin
                                // when you have the vector, save the magnitude as the length (radius at that point), then normalize the vector 
                                // so it's length is 1, then multiply by length + wave offset you would need the original vertex points for each iteration
                                var smallImpact = ImpactSteps / 3;

                                if (_localImpacts[s] == Vector3D.NegativeInfinity || _impactCnt[s] > smallImpact + 1) continue;
                                var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _localImpacts[s]);
                                var impactFactor = (-0.69813170079773212 * dotOfNormLclImpact * dotOfNormLclImpact - 0.87266462599716477) * dotOfNormLclImpact + 1.5707963267948966;
                                var waveMultiplier = Pi / ImpactSteps;
                                var wavePosition = waveMultiplier * _impactCnt[s];
                                var relativeToWavefront = Math.Abs(impactFactor - wavePosition);
                                if (impactFactor < wavePosition && relativeToWavefront >= 0 && relativeToWavefront < 0.25)
                                {
                                    if (_impactCnt[s] != smallImpact + 1) _triColorBuffer[j] = 1;
                                    else _triColorBuffer[j] = 0;
                                    break;
                                }

                                if (impactFactor < wavePosition && relativeToWavefront >= -0.25 && relativeToWavefront < 0 || relativeToWavefront > 0.25 && relativeToWavefront <= 0.5)
                                {
                                    _triColorBuffer[j] = 0;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in ImpactColorAssignments {ex}"); }
            }

            private void RefreshColorAssignments(int prevLod)
            {
                try
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

                        if (prevLod != _lod)
                        {
                            var lclPos = (v0 + v1 + v2) / 3 - _matrix.Translation;
                            var normlclPos = Vector3D.Normalize(lclPos);
                            _preCalcNormLclPos[j] = normlclPos;
                            for (int c = 0; c < _triColorBuffer.Length; c++)
                                _triColorBuffer[c] = 0;
                        }

                        var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _refreshPoint);
                        var impactFactor = (-0.69813170079773212 * dotOfNormLclImpact * dotOfNormLclImpact - 0.87266462599716477) * dotOfNormLclImpact + 1.5707963267948966;
                        var waveMultiplier = Pi / RefreshSteps;
                        var wavePosition = waveMultiplier * _refreshDrawStep;
                        var relativeToWavefront = Math.Abs(impactFactor - wavePosition);
                        if (relativeToWavefront < .05) _triColorBuffer[j] = 2;
                        else _triColorBuffer[j] = 0;
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in ChargeColorAssignments {ex}"); }
            }

            private void ComputeImpacts()
            {
                _impact = true;
                for (int i = 0; i < _impactPos.Length; i++)
                {
                    if (_impactPos[i] == Vector3D.NegativeInfinity)
                    {
                        _impactPos[i] = _impactPosState;
                        _localImpacts[i] = _impactPos[i] - _matrix.Translation;
                        _localImpacts[i].Normalize();
                        break;
                    }
                }
            }

            public void ComputeSides(MyEntity shellActive)
            {
                if (shellActive == null) return;
                shellActive.TryGetSubpart("ShieldLeft", out SidePartArray[0]);
                shellActive.TryGetSubpart("ShieldRight", out SidePartArray[1]);
                shellActive.TryGetSubpart("ShieldTop", out SidePartArray[2]);
                shellActive.TryGetSubpart("ShieldBottom", out SidePartArray[3]);
                shellActive.TryGetSubpart("ShieldFront", out SidePartArray[4]);
                shellActive.TryGetSubpart("ShieldBack", out SidePartArray[5]);
                ShellActive = shellActive;
            }

            private void UpdateColor(MyEntitySubpart shellSide)
            {
                var emissive = 100f;
                shellSide.SetEmissiveParts(ShieldEmissiveAlpha, _activeColor, emissive);
            }

            public void StepEffects()
            {
                _mainLoop++;
                if (_mainLoop == 60)
                {
                    _mainLoop = 0;
                    _lCount++;
                    if (_lCount == 10)
                    {
                        _lCount = 0;
                        if (_longerLoop == 0 && Random.Next(0, 2) == 1 || _longerLoop == 3)
                        {
                            _refresh = true;
                            var localImpacts = Vector3D.Zero - _matrix.Translation;
                            localImpacts.Normalize();
                            _refreshPoint = localImpacts;
                        }
                        _longerLoop++;
                        if (_longerLoop == 6) _longerLoop = 0;
                    }
                }

                if (_impactPosState != Vector3D.NegativeInfinity) ComputeImpacts(); ;

                if (_impact)
                {
                    _impact = false;
                    if (_active)
                    {
                        var impactTransNorm = _impactPosState - _matrix.Translation;
                        _hitFaces.Clear();
                        GetIntersectingFace(_matrix, impactTransNorm, _hitFaces);
                        foreach (var face in _hitFaces)
                        {
                            _sideLoops[face] = 1;
                            SidePartArray[face].Render.UpdateRenderObject(true);
                            UpdateColor(SidePartArray[face]);
                        }
                    }

                    ImpactsFinished = false;
                    _refresh = false;
                    _refreshDrawStep = 0;
                }

                if (_refresh)
                {
                    _refreshDrawStep++;
                    if (_refreshDrawStep == RefreshSteps + 1)
                    {
                        _refresh = false;
                        _refreshDrawStep = 0;
                        for (int i = 0; i < _triColorBuffer.Length; i++) _triColorBuffer[i] = 0;
                    }
                }
                if (!ImpactsFinished)
                {
                    //Log.Line($"{_impactCnt[0]} - {_impactCnt[1]} - {_impactCnt[2]} - {_impactCnt[3]} - {_impactCnt[4]} - {_impactCnt[5]}");
                    for (int i = 0; i < _sideLoops.Length; i++)
                    {
                        if (_sideLoops[i] != 0) _sideLoops[i]++;
                        else continue;

                        if (_sideLoops[i] == SideSteps + 1)
                        {
                            SidePartArray[i].Render.UpdateRenderObject(false);
                            _sideLoops[i] = 0;
                        }
                    }
                    for (int i = 0; i < _impactCnt.Length; i++)
                    {
                        if (_impactPos[i] != Vector3D.NegativeInfinity)
                        {
                            _impactCnt[i]++;
                        }
                        if (_impactCnt[i] == ImpactSteps +1)
                        {
                            _impactCnt[i] = 0;
                            _impactPos[i] = Vector3D.NegativeInfinity;
                            _localImpacts[i] = Vector3D.NegativeInfinity;
                        }
                    }
                    if (_impactCnt[0] == 0 && _impactCnt[1] == 0 && _impactCnt[2] == 0 && _impactCnt[3] == 0 && _impactCnt[4] == 0 && _impactCnt[5] == 0)
                    {
                        ShellActive.Render.UpdateRenderObject(false);
                        ImpactsFinished = true;
                        for (int i = 0; i < _triColorBuffer.Length; i++) _triColorBuffer[i] = 0;
                    }
                }
            }

            private static void GetIntersectingFace(MatrixD matrix, Vector3D hitPosLocal, List<int> impactFaces)
            {
                var boxMax = matrix.Backward + matrix.Right + matrix.Up;
                var boxMin = -boxMax;
                var box = new BoundingBoxD(boxMin, boxMax);

                var maxWidth = box.Max.LengthSquared();
                var testLine = new LineD(Vector3D.Zero, Vector3D.Normalize(hitPosLocal) * maxWidth); //This is to ensure we intersect the box
                LineD testIntersection;
                box.Intersect(ref testLine, out testIntersection);

                var intersection = testIntersection.To;

                var projFront = VectorProjection(intersection, matrix.Forward);
                if (projFront.LengthSquared() >= 0.65 * matrix.Forward.LengthSquared()) //if within the side thickness
                    impactFaces.Add(intersection.Dot(matrix.Forward) > 0 ? 5 : 4);

                var projLeft = VectorProjection(intersection, matrix.Left);
                if (projLeft.LengthSquared() >= 0.65 * matrix.Left.LengthSquared()) //if within the side thickness
                    impactFaces.Add(intersection.Dot(matrix.Left) > 0 ? 1 : 0);

                var projUp = VectorProjection(intersection, matrix.Up);
                if (projUp.LengthSquared() >= 0.65 * matrix.Up.LengthSquared()) //if within the side thickness
                    impactFaces.Add(intersection.Dot(matrix.Up) > 0 ? 2 : 3);
            }

            private static Vector3D VectorProjection(Vector3D a, Vector3D b)
            {
                if (Vector3D.IsZero(b))
                    return Vector3D.Zero;

                return a.Dot(b) / b.LengthSquared() * b;
            }

            public void Draw(uint renderId)
            {
                try
                {
                    if (ImpactsFinished && !_refresh) return;
                    var ib = _backing.IndexBuffer[_lod];
                    Vector4 color;
                    if (!ImpactsFinished)
                    {
                        color = _waveColor;
                        _faceMaterial = _faceWave;
                    }
                    else
                    {
                        color = _refreshColor;
                        _faceMaterial = _faceCharge;
                    }
                    for (int i = 0, j = 0; i < ib.Length; i += 3, j++)
                    {
                        var face = _triColorBuffer[j];
                        if (face != 1 && face != 2) continue;

                        var i0 = ib[i];
                        var i1 = ib[i + 1];
                        var i2 = ib[i + 2];

                        var v0 = _vertexBuffer[i0];
                        var v1 = _vertexBuffer[i1];
                        var v2 = _vertexBuffer[i2];

                        var n0 = _normalBuffer[i0];
                        var n1 = _normalBuffer[i1];
                        var n2 = _normalBuffer[i2];

                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, n0, n1, n2, _v20, _v21, _v22, _faceMaterial, renderId, (v0 + v1 + v2) / 3, color);
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in IcoSphere Draw - renderId {renderId.ToString()}: {ex}"); }
            }
        }
    }
}

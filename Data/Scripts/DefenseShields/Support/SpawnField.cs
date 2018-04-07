using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage;
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

        //Spawn Block
        public static IMyEntity SpawnBlock(string subtypeId, string name = "", bool isVisible = true, bool hasPhysics = false, bool isStatic = false, bool toSave = false, bool destructible = false, long ownerId = 0)
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

            private readonly Vector3D[] _impactPos = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private readonly Vector3D[] _localImpacts = {Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity,
                Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity};
            private Vector3D[] _preCalcNormLclPos;
            private Vector3D[] _vertexBuffer;
            private Vector3D[] _physicsBuffer;

            private Vector3D[] _normalBuffer;
            private Vector4[] _triColorBuffer;

            private Vector3D _impactPosState;
            private Vector3D _chargePoint;
            private MatrixD _matrix;

            private static readonly Random Random = new Random();

            private readonly int[] _impactCnt = new int[10];

            private int _mainLoop;
            private int _longLoop;
            private int _longerLoop;
            private int _impactDrawStep;
            private int _modelCount;
            private int _chargeDrawStep;
            private int _lod;

            private const int ImpactSteps = 80;
            private const int ChargeSteps = 30;

            private Vector4 _hitColor;
            private Vector4 _waveColor;
            private Vector4 _wavePassedColor;
            private Vector4 _waveComingColor;
            private Vector4 _defaultColor;
            private Vector4 _chargeColor;

            private bool _impactsFinished = true;
            private bool _enemy;
            private bool _impact;
            private bool _charge;

            private IMyEntity _shield;

            //private readonly MyStringId _faceId1 = MyStringId.GetOrCompute("CustomIdle");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId1 = MyStringId.GetOrCompute("CockpitFighterGlassInside");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01

            private readonly MyStringId _faceId2 = MyStringId.GetOrCompute("SunDisk");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId3 = MyStringId.GetOrCompute("CockpitFighterGlassInside");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01
            private readonly MyStringId _faceId4 = MyStringId.GetOrCompute("CockpitGlassInside");  //GlareLsThrustLarge //ReflectorCone //SunDisk  //GlassOutside //Spark1 //Lightning_Spherical //Atlas_A_01

            private DSUtils _dsutil1 = new DSUtils();
            private DSUtils _dsutil2 = new DSUtils();
            private DSUtils _dsutil3 = new DSUtils();

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

            public void ComputeEffects(MatrixD matrix, Vector3D impactPos, float impactSize, bool entChanged, bool enemy, IMyEntity shield, int prevLod)
            {
                _shield = shield;
                _enemy = enemy;
                _matrix = matrix;
                _impactPosState = impactPos;
                if (impactPos == Vector3D.NegativeInfinity) _impact = false;
                else ComputeImpacts();
                //if (impactSize <= 10) impactSize = (int)4;
                impactSize = (int)1;
                var impactSpeed = 2;
                if (impactSize < 4) impactSpeed = 1;
                if (prevLod != _lod) // entChanged || Not sure if I need
                {
                    var ib = _backing.IndexBuffer[_lod];
                    Array.Resize(ref _preCalcNormLclPos, ib.Length / 3);
                    Array.Resize(ref _triColorBuffer, ib.Length / 3);
                }

                StepEffects();
                InitColors();
                if (_charge && _impactsFinished && prevLod == _lod) ChargeColorAssignments(prevLod);
                if (_impactsFinished && prevLod == _lod) return;


                //if (_impactCnt[9] != 0) MyAPIGateway.Parallel.Start(Models);
                //_dsutil2.Sw.Start();
                ImpactColorAssignments(impactSize, impactSpeed, prevLod);
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

            private void ImpactColorAssignments(float impactSize, int impactSpeed, int prevLod)
            {
                //Log.Line($"colorAssignments - entChanged: {entChanged} - lod: {_lod} - prevlod: {prevLod}");
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
                            _triColorBuffer[c] = _defaultColor;
                    }
                    if (!_impactsFinished)
                    {
                        for (int s = 9; s > -1; s--)
                        {
                            if (_localImpacts[s] == Vector3D.NegativeInfinity) continue;
                            var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _localImpacts[s]);
                            var impactFactor = Math.Acos(dotOfNormLclImpact);

                            var waveMultiplier = Pi / ImpactSteps / impactSize;
                            var wavePosition = waveMultiplier * _impactCnt[s];
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
                    else if (_impactCnt[9] == 0) _triColorBuffer[j] = _defaultColor;
                }
            }

            private void ChargeColorAssignments(int prevLod)
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
                            _triColorBuffer[c] = _defaultColor;
                    }

                    var dotOfNormLclImpact = Vector3D.Dot(_preCalcNormLclPos[i / 3], _chargePoint);
                    var impactFactor = Math.Acos(dotOfNormLclImpact);
                    var waveMultiplier = Pi / ChargeSteps;
                    var wavePosition = waveMultiplier * _chargeDrawStep;
                    var relativeToWavefront = Math.Abs(impactFactor - wavePosition);
                    if (relativeToWavefront < .10) _triColorBuffer[j] = _chargeColor;
                    else _triColorBuffer[j] = _defaultColor;
                }
            }

            public void Draw(uint renderId)
            {
                //_dsutil1.Sw.Start();
                try
                {
                    var faceMaterial = _faceId2;
                    var ib = _backing.IndexBuffer[_lod];
                    var v20 = new Vector2(.5f);
                    var v21 = new Vector2(0.25f);
                    var v22 = new Vector2(0.25f);
                    //var v21 = new Vector2((0.25f) * (_mainLoop % 2) + 1);
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
                        else if (color == _chargeColor) faceMaterial = _faceId3;
                        //else if (color == _waveComingColor) faceMaterial = _faceId1;
                        //else if (color == _wavePassedColor) faceMaterial = _faceId1;
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, n0, n1, n2, v20, v21, v22, faceMaterial, renderId, (v0 + v1 + v2) / 3, color);
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in IcoSphere Draw - renderId {renderId.ToString()}: {ex}"); }
                //_dsutil1.StopWatchReport("Draw", -1);
            }

            private void ComputeImpacts()
            {
                _impact = true;
                for (var i = 9; i >= 0; i--)
                {
                    if (_impactPos[i] != Vector3D.NegativeInfinity) continue;
                    _impactPos[i] = _impactPosState;
                    break;
                }
                for (int i = 9; i >= 0; i--)
                {
                    if (_impactPos[i] == Vector3D.NegativeInfinity) break;
                    _localImpacts[i] = _impactPos[i] - _matrix.Translation;
                    _localImpacts[i].Normalize();
                }
            }

            private void StepEffects()
            {
                _mainLoop++;
                if (_mainLoop == 61)
                {
                    _mainLoop = 0;
                    _longLoop++;
                    if (_longLoop == 10)
                    {
                        _longLoop = 0;
                        if (_longerLoop == 0 && Random.Next(0, 2) == 1 || _longerLoop == 3)
                        {
                            _charge = true;
                            var localImpacts = Vector3D.Zero - _matrix.Translation;
                            localImpacts.Normalize();
                            _chargePoint = localImpacts;
                        }
                        _longerLoop++;
                        if (_longerLoop == 6) _longerLoop = 0;
                    }
                }
                if (_impact)
                {
                    _impactsFinished = false;
                    _impactDrawStep = 0;
                    _charge = false;
                    _chargeDrawStep = 0;
                }

                if (_charge)
                {
                    _chargeDrawStep++;
                    if (_chargeDrawStep == ChargeSteps + 1)
                    {
                        _charge = false;
                        _chargeDrawStep = 0;
                        for (int i = 0; i < _triColorBuffer.Length; i++)
                            _triColorBuffer[i] = _defaultColor;
                    }
                }
                if (!_impactsFinished)
                {
                    for (int i = 0; i < _impactCnt.Length; i++)
                    {
                        if (_impactPos[i] != Vector3D.NegativeInfinity) _impactCnt[i] += 1;
                        if (_impactCnt[i] == ImpactSteps + 1)
                        {
                            _impactCnt[i] = 0;
                            _impactPos[i] = Vector3D.NegativeInfinity;
                        }
                    }
                    if (_impactCnt[0] == 0 && _impactCnt[1] == 0 && _impactCnt[2] == 0 && _impactCnt[3] == 0 && _impactCnt[4] == 0 
                        && _impactCnt[5] == 0 && _impactCnt[6] == 0 && _impactCnt[7] == 0 && _impactCnt[8] == 0 && _impactCnt[9] == 0)
                    {
                        _impactsFinished = true;
                        _impactDrawStep = 0;
                        for (int i = 0; i < _triColorBuffer.Length; i++)
                            _triColorBuffer[i] = _defaultColor;
                    }
                }
            }

            private void Models()
            {               
                try
                {
                    var modPath = DefenseShieldsBase.Instance.ModPath();
                    if (_impactCnt[9] == 1) _modelCount = 0;
                    var n = _modelCount;
                    if (_impactCnt[9] % 2 == 1)
                    {
                        _shield.Render.Visible = true;
                        ((MyEntity)_shield).RefreshModels($"{modPath}\\Models\\LargeField{n.ToString()}.mwm", null);
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
                    if (_impactCnt[9] == ImpactSteps) 
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
                _defaultColor = Color.FromNonPremultiplied(0, 0, 0, 248);

                //waveColor
                //var vwaveColor = Color.FromNonPremultiplied(cv3, 0, cv4, rndNum1 - 5);
                var vwaveColor = Color.FromNonPremultiplied(0, 0, 0, 225);
                _waveColor = vwaveColor;

                //wavePassedColor
                var vwavePassedColor = Color.FromNonPremultiplied(0, 0, 12, colorRnd1);
                if (_impactCnt[9] % 10 == 0)
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

                //chargeColor
                var vchargeColor = Color.FromNonPremultiplied(255, 255, 255, 255);
                _chargeColor = vchargeColor;
            }
        }
    }
}

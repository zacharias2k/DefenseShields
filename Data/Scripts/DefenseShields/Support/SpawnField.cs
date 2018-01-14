using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields.Support
{
    class Spawn
    {
        #region Cube+subparts Class
        public class Utils
        {
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
                DisplayName = "FieldGenerator",
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
                        Name = "Field",
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
        private Vector3D _oldImpactPos;

        private readonly Vector3[] _vertexBuffer;
        private static readonly Random Random = new Random();
        private readonly int[][] _indexBuffer;
        private readonly SortedList<double, int> _faceLocSlist = new SortedList<double, int>();

        private int _impactNew;
        private int _impactCharge;
        private int _1Face;
        private int _2Face;
        private int _3Face;
        private int _4Face;
        private int _5Face;
        private int _6Face;
        private int _colourRand1;
        private int _colourRand2;
        private int _pulseColor = 80;
        private double _firstFaceLoc;
        private double _lastFaceLoc;

        private bool _charge = true;
        private bool _impactFinished;

        public Icosphere(int lods)
        {
            //const float X = 0.525731112119133606f;
            //const float Z = 0.850650808352039932f;
            Vector3[] data =
            {
                /*new Vector3(-X, 0, Z), new Vector3(X, 0, Z), new Vector3(-X, 0, -Z), new Vector3(X, 0, -Z),
                new Vector3(0, Z, X), new Vector3(0, Z, -X), new Vector3(0, -Z, X), new Vector3(0, -Z, -X),
                new Vector3(Z, X, 0), new Vector3(-Z, X, 0), new Vector3(Z, -X, 0), new Vector3(-Z, -X, 0)*/
                new Vector3(0.000000f, 0.000000f, -1.000000f), new Vector3(0.723600f, -0.525720f, -0.447215f),
                new Vector3(-0.276385f, -0.850640f, -0.447215f), new Vector3(0.723600f, 0.525720f, -0.447215f),
                new Vector3(-0.894425f, 0.000000f, -0.447215f), new Vector3(-0.276385d, 0.850640f, -0.447215f),
                new Vector3(0.894425f, 0.000000f, 0.447215f), new Vector3(0.276385f, -0.850640f, 0.447215f),
                new Vector3(-0.723600f, -0.525720f, 0.447215f), new Vector3(-0.723600f, 0.525720f, 0.447215f),
                new Vector3(0.276385f, 0.850640f, 0.447215f), new Vector3(0.000000f, 0.000000f, 1.000000f)
            };
            List<Vector3> points = new List<Vector3>(12 * (1 << (lods)));
            points.AddRange(data);
            int[][] index = new int[lods][];
            index[0] = new int[]
            {
                /*
                0, 4, 1, 0, 9, 4, 9, 5, 4, 4, 5, 8, 4, 8, 1,
                8, 10, 1, 8, 3, 10, 5, 3, 8, 5, 2, 3, 2, 7, 3, 7, 10, 3, 7,
                6, 10, 7, 11, 6, 11, 0, 6, 0, 1, 6, 6, 1, 10, 9, 0, 11, 9,
                11, 2, 9, 2, 5, 7, 2, 11
                */
                0, 1, 2, 1, 0, 3, 0, 2, 4, 0, 4, 5, 0, 5, 3, 1, 3, 6, 2, 1, 7,
                4, 2, 8, 5, 4, 9, 3, 5, 10, 1, 6, 7, 2, 7, 8, 4, 8, 9, 5, 9, 10,
                3, 10, 6, 7, 6, 11, 8, 7, 11, 9, 8, 11, 10, 9, 11, 6, 10, 11
            };
            for (int i = 1; i < lods; i++)
                index[i] = Subdivide(points, index[i - 1]);

            _indexBuffer = index;
            _vertexBuffer = points.ToArray();
        }

        public void Draw(MatrixD matrix, float radius, int lod, int count, bool enemy, Vector3D impactPos, MatrixD detectMatrix, IMyEntity _shield, MyStringId? faceMaterial = null, MyStringId? lineMaterial = null, float lineThickness = -1f)
        {
            var lineWidth = radius / 600;
            radius = 1f; //We set sphere radius elsewhere
            var impactTrue = !impactPos.Equals(_oldImpactPos);
            //Log.Line($"{impactTrue} - {impactPos} - {_oldImpactPos}");
            if (impactTrue)
            {
                if (_impactNew == 0) _impactFinished = true;
                else _impactFinished = false;
                _oldImpactPos = impactPos;
                _impactNew = 1;
                _impactCharge = 0;
            }
            if (_impactNew == 61)
            {
                _impactNew = 0;
                _impactCharge = 1;
            }
            if (_impactCharge == 121) _impactCharge = 0;

            if (_impactNew != 0)
            {
                var ixImpact = _indexBuffer[lod];
                var waveFacesPer = ixImpact.Length / 3 / 60;
                var firstFace = _impactNew * waveFacesPer - waveFacesPer;
                var lastFace = _impactNew * waveFacesPer;
                if (_impactNew == 1)
                {
                    _faceLocSlist.Clear();
                    for (var i = 0; i < ixImpact.Length - 2; i += 3)
                    {
                        var i0 = ixImpact[i];
                        var i1 = ixImpact[i + 1];
                        var i2 = ixImpact[i + 2];

                        var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                        fnorm.Normalize();
                        var localImpact = Vector3D.Transform(impactPos, MatrixD.Invert(detectMatrix));
                        localImpact.Normalize();
                        var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                        _faceLocSlist.Add(impactFactor, i);
                    }
                    _1Face = _faceLocSlist.ElementAt(0).Value;
                    _2Face = _faceLocSlist.ElementAt(1).Value;
                    _3Face = _faceLocSlist.ElementAt(2).Value;
                    _4Face = _faceLocSlist.ElementAt(3).Value;
                    _5Face = _faceLocSlist.ElementAt(4).Value;
                    _6Face = _faceLocSlist.ElementAt(5).Value;
                }
                _firstFaceLoc = _faceLocSlist.ElementAt(firstFace).Key;
                _lastFaceLoc = _faceLocSlist.ElementAt(lastFace).Key;
            }
            //if (_impactNew != 0) lod = 4;
            #region Color changing code
            var cv1 = 0;
            var cv2 = 0;
            var cv3 = 0;
            var cv4 = 0;
            var cv5 = 0;
            var cv6 = 0;
            if (_charge)
            {
                if (count == 59)
                {
                    _pulseColor = 140;
                    _charge = false;
                }
                else
                {
                    _pulseColor += 1;
                }
            }
            else
            {
                if (count == 59)
                {
                    _pulseColor = 80;
                    _charge = true;
                }
                else
                {
                    _pulseColor -= 1;
                }
            }

            if (count % 5 == 0) _colourRand1 = Random.Next(1, 75);
            _colourRand2 = Random.Next(1, 200);

            if (enemy) cv1 = 100;
            else cv2 = 100;
            if (cv1 != 0) cv3 = cv1;
            if (cv2 != 0) cv4 = cv2;
            if (cv1 != 0) cv5 = _impactCharge;
            if (cv2 != 0) cv6 = _impactCharge;

            var c1 = Color.FromNonPremultiplied(_pulseColor - 35, 0, 0, 16);
            var c2 = Color.FromNonPremultiplied(0, 0, _pulseColor, 16);
            var color1 = enemy ? c1 : c2;

            var c3 = Color.FromNonPremultiplied(cv3, 0, cv4, 16);
            var color2 = c3;

            var c6 = Color.FromNonPremultiplied(_colourRand1, 0, 0, 16);
            var c7 = Color.FromNonPremultiplied(0, 0, _colourRand1, 16);
            var color3 = enemy ? c6 : c7;

            var c8 = Color.FromNonPremultiplied(cv5, 0, cv6, 16);
            var color4 = c8;

            var c9 = Color.FromNonPremultiplied(cv1, 0, cv2, 16);
            var color5 = c9;

            var c10 = Color.FromNonPremultiplied(_colourRand2, 0, 0, 16);
            var c11 = Color.FromNonPremultiplied(0, 0, _colourRand2, 16);
            var color6 = enemy ? c10 : c11;

            var vc4 = Color.FromNonPremultiplied(cv1, 0, cv2, 32);
            Vector4 vColor1 = vc4;
            #endregion
            var ix = _indexBuffer[lod];
            for (var i = 0; i < ix.Length - 2; i += 3)
            {
                var i0 = ix[i];
                var i1 = ix[i + 1];
                var i2 = ix[i + 2];

                var v0 = Vector3.Transform(radius * _vertexBuffer[i0], matrix);
                var v1 = Vector3.Transform(radius * _vertexBuffer[i1], matrix);
                var v2 = Vector3.Transform(radius * _vertexBuffer[i2], matrix);

                var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                fnorm.Normalize();
                var localImpact = Vector3D.Transform(impactPos, MatrixD.Invert(detectMatrix));
                localImpact.Normalize();
                var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                if (faceMaterial.HasValue)
                    if (_impactNew != 0)
                    {
                        if (_1Face == i || _2Face == i || _3Face == i || _4Face == i || _5Face == i || _6Face == i)
                        {
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, color6);
                        }
                        if (impactFactor >= _firstFaceLoc && impactFactor <= _lastFaceLoc)
                        {
                            //Log.Line($"{i} - {count}");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, color2);
                        }
                        if (impactFactor < _lastFaceLoc)
                        {
                            //Log.Line($"{i} - {count}");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, color3);
                        }
                        if (impactFactor > _lastFaceLoc && _impactFinished)
                        {
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, color5);
                        }
                    }
                    else if (_impactCharge != 0)
                    {
                        //Log.Line($"{i} - {count}");
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                        _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                        (v0 + v1 + v2) / 3, color4);
                    }
                    else
                    {
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                            _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                            (v0 + v1 + v2) / 3, color1);
                    }
                if (lineMaterial.HasValue && lineThickness > 0)
                {
                    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref vColor1, lineThickness);
                    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref vColor1, lineThickness);
                    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref vColor1, lineThickness);
                }
                //if (_impactNew != 0)
                //{
                //    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref color3, lineWidth);
                //    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref color3, lineWidth);
                //    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref color3, lineWidth);
                //}
            }
            if (_impactNew != 0) _impactNew++;
            if (_impactCharge != 0) _impactCharge++;
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
    }
}

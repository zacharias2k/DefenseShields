using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private Vector3D _oldImpactPos;

        private readonly Vector3[] _vertexBuffer;
        private static readonly Random Random = new Random();

        private readonly int[][] _indexBuffer;
        private readonly SortedList<double, int> _faceLocSlist = new SortedList<double, int>();
        private readonly SortedList<double, int> _glichSlist = new SortedList<double, int>();

        private int _impactNew;
        private int _glitch;
        private int _impactCharge;
        private int _pulseCount;
        private int _pulse = 25;
        private int _prevLod;

        private double _firstHitFaceLoc1x;
        private double _lastHitFaceLoc1x;
        private double _firstFaceLoc1x;
        private double _lastFaceLoc1x;
        private double _firstFaceLoc2x;
        private double _lastFaceLoc2x;
        private double _firstFaceLoc4x;
        private double _lastFaceLoc4x;
        private double _firstFaceLoc6X;
        private double _lastFaceLoc6X;

        private bool _impactFinished;
        private bool _charged = true;


        public Icosphere(int lods)
        {
            const float X = 0.525731112119133606f;
            const float Z = 0.850650808352039932f;
            Vector3[] data =
            {
                new Vector3(0.000000f, 0.000000f, -1.000000f), new Vector3(0.723600f, -0.525720f, -0.447215f),
                new Vector3(-0.276385f, -0.850640f, -0.447215f), new Vector3(0.723600f, 0.525720f, -0.447215f),
                new Vector3(-0.894425f, 0.000000f, -0.447215f), new Vector3(-0.276385d, 0.850640f, -0.447215f),
                new Vector3(0.894425f, 0.000000f, 0.447215f), new Vector3(0.276385f, -0.850640f, 0.447215f),
                new Vector3(-0.723600f, -0.525720f, 0.447215f), new Vector3(-0.723600f, 0.525720f, 0.447215f),
                new Vector3(0.276385f, 0.850640f, 0.447215f), new Vector3(0.000000f, 0.000000f, 1.000000f)
            };
            List<Vector3> points = new List<Vector3>(12 * (1 << (lods - 1)));
            points.AddRange(data);
            int[][] index = new int[lods][];
            index[0] = new int[]
            {
                0, 1, 2, 1, 0, 3, 0, 2, 4, 0, 4, 5, 0, 5, 3, 1, 3, 6, 2, 1, 7,
                4, 2, 8, 5, 4, 9, 3, 5, 10, 1, 6, 7, 2, 7, 8, 4, 8, 9, 5, 9, 10,
                3, 10, 6, 7, 6, 11, 8, 7, 11, 9, 8, 11, 10, 9, 11, 6, 10, 11
            };
            for (int i = 1; i < lods; i++)
                index[i] = Subdivide(points, index[i - 1]);

            _indexBuffer = index;
            _vertexBuffer = points.ToArray();
        }

        public void Draw(MatrixD matrix, float radius, int lod, int count, bool enemy, Vector3D impactPos,
            MatrixD detectMatrix, IMyEntity shield1, IMyEntity shield2, MyStringId? faceMaterial = null,
            MyStringId? lineMaterial = null, float lineThickness = -1f)
        {
            var lodChange = (_impactNew != 0 || _glitch != 0) && lod != _prevLod;
            if (lodChange) Log.Line($"Lod changed from {_prevLod} to {lod}");
            _prevLod = lod;
            var lineWidth = radius / 600;
            radius = 1f; //We set sphere radius elsewhere

            #region Color changing code
            //Init Colors
            var cv1 = 0;
            var cv2 = 0;
            var cv3 = 0;
            var cv4 = 0;
            if (enemy) cv1 = 75;
            else cv2 = 75;
            if (cv1 != 0) cv3 = cv1;
            if (cv2 != 0) cv4 = cv2;
            var rndNum1 = Random.Next(15, 27);
            var colorRnd1 = Random.Next(15, 50);
            var colorRnd2 = Random.Next(8, 255);
            var rndNum3 = Random.Next(55, 63);
            var rndNum4 = Random.Next(40, 120);

            //waveColor
            var vwaveColor = Color.FromNonPremultiplied(cv3, 0, cv4, rndNum1 - 5);
            Vector4 waveColor = vwaveColor;

            //wavePassedColor
            var vwavePassedColor = Color.FromNonPremultiplied(0, 0, 5, colorRnd1);
            if (count % 10 == 0)
            {
                vwavePassedColor = Color.FromNonPremultiplied(0, 0, rndNum1, rndNum1 - 5);
            }
            Vector4 wavePassedColor = vwavePassedColor;

            //waveComingColor
            var vwaveComingColor = Color.FromNonPremultiplied(cv1, 0, cv2, 16);
            Vector4 waveComingColor = vwaveComingColor;

            //hitColor
            var vhitColor = Color.FromNonPremultiplied(0, 0, colorRnd2, rndNum1);
            Vector4 hitColor = vhitColor;

            //lineColor
            var vlineColor = Color.FromNonPremultiplied(cv1, 0, cv2, 32);
            Vector4 lineColor = vlineColor;

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

            var puleColor1 = Color.FromNonPremultiplied(_pulse, 0, 0, 16);
            var puleColor2 = Color.FromNonPremultiplied(0, 0, 0, _pulse);
            var vglitchColor = Color.FromNonPremultiplied(0, 0, rndNum4, rndNum1 - 5);
            Vector4 glitchColor = vglitchColor;
            if (_pulseCount == 59 && _pulseCount == rndNum3)
            {
                puleColor2 = Color.FromNonPremultiplied(0, 0, 27, _pulse);
                _glitch = 1;
                Log.Line($"Random Pulse: {_pulse}");
            }
            var vpulseColor = enemy ? puleColor1 : puleColor2;
            Vector4 pulseColor = vpulseColor;
            #endregion

            #region Draw Prep
            var impactTrue = !impactPos.Equals(_oldImpactPos);
            if (impactTrue)
            {
                if (_impactNew == 0) _impactFinished = true;
                else _impactFinished = false;
                _oldImpactPos = impactPos;
                _impactNew = 1;
                _impactCharge = 0;
                _charged = false;
                _pulseCount = 0;
                _pulse = 25;
            }
            if (_impactNew == 65)
            {
                _impactNew = 0;
                _impactCharge = 1;
            }
            if (_glitch == 9) _glitch = 0;
            if (_impactCharge == 121)
            {
                _charged = true;
                _impactCharge = 0;
            }
            //chargeColor
            var rndNum2 = Random.Next(1, 9);
            var chargeColor = Color.FromNonPremultiplied(0, 0, 0, 16 + _impactCharge / 6);
            if (_impactCharge % rndNum2 == 0)
            {
                chargeColor = Color.FromNonPremultiplied(0, 0, 0, 16 + _impactCharge / 8);
            }

            if (_glitch != 0)
            {
                var ixImpact = _indexBuffer[lod];
                var waveFacesPer = ixImpact.Length / 3 / 8;
                var firstFace6X = _glitch * waveFacesPer - waveFacesPer;
                var lastFace6X = _glitch * waveFacesPer - 1;
                if (_glitch == 1 || lodChange)
                {
                    Log.Line($"L {ixImpact.Length} -Tris {ixImpact.Length / 3} -Div8 {ixImpact.Length / 3 / 8} -{lodChange}");
                    _glichSlist.Clear();
                    for (var i = 0; i < ixImpact.Length - 2; i += 3)
                    {
                        var i0 = ixImpact[i];
                        var i1 = ixImpact[i + 1];
                        var i2 = ixImpact[i + 2];
                        var glitchRndNum1 = Random.Next(0, 9999999);
                        var glitchRndNum2 = Random.Next(0, 9999999);
                        var glitchRndNum3 = Random.Next(0, 9999999);


                        var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                        fnorm.Normalize();
                        var zeroPos = new Vector3D(glitchRndNum1, glitchRndNum2, glitchRndNum3);
                        var localImpact = Vector3D.Transform(zeroPos, MatrixD.Invert(detectMatrix));
                        localImpact.Normalize();
                        var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                        _glichSlist.Add(impactFactor, i);
                    }
                }
                _firstFaceLoc6X = _glichSlist.ElementAt(firstFace6X).Key;
                _lastFaceLoc6X = _glichSlist.ElementAt(lastFace6X).Key;
            }

            if (_impactNew != 0)
            {
                
                if (_impactNew == 1) shield1.Render.Visible = true;
                else if (_impactNew == 2) shield1.Render.Visible = false;
                else if (_impactNew == 16) shield2.Render.Visible = true;
                else if (_impactNew == 17) shield2.Render.Visible = false;
                if (_impactNew == 32) shield1.Render.Visible = true;
                else if (_impactNew == 33) shield1.Render.Visible = false;
                else if (_impactNew == 48) shield2.Render.Visible = true;
                else if (_impactNew == 49) shield2.Render.Visible = false;
                

                var ixImpact = _indexBuffer[lod];
                var waveFacesPer = ixImpact.Length / 3 / 64;
                var firstFace1X = _impactNew * waveFacesPer - waveFacesPer;
                var lastFace1X = _impactNew * waveFacesPer - 1;

                if (_impactNew == 1 || lodChange)
                {
                    Log.Line($"L {ixImpact.Length} -Tris {ixImpact.Length / 3} -Div64 {ixImpact.Length / 3 / 64} -{lodChange}");
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
                    _firstHitFaceLoc1x = _faceLocSlist.ElementAt(firstFace1X).Key;
                    _lastHitFaceLoc1x = _faceLocSlist.ElementAt(lastFace1X).Key;
                }
                _firstFaceLoc1x = _faceLocSlist.ElementAt(firstFace1X).Key;
                _lastFaceLoc1x = _faceLocSlist.ElementAt(lastFace1X).Key;
            }
            #endregion

            #region Compute lines and colors
            var ix = _indexBuffer[lod];
            for (var i = 0; i < ix.Length - 2; i += 3)
            {
                var i0 = ix[i];
                var i1 = ix[i + 1];
                var i2 = ix[i + 2];

                var v0 = Vector3D.Transform(radius * _vertexBuffer[i0], matrix);
                var v1 = Vector3D.Transform(radius * _vertexBuffer[i1], matrix);
                var v2 = Vector3D.Transform(radius * _vertexBuffer[i2], matrix);

                var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                fnorm.Normalize();
                var localImpact = Vector3D.Transform(impactPos, MatrixD.Invert(detectMatrix));
                localImpact.Normalize();
                var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
                if (faceMaterial.HasValue)
                    if (_impactNew != 0)
                    {
                        if (impactFactor >= _firstHitFaceLoc1x && impactFactor <= _lastHitFaceLoc1x)
                        {
                            //Log.Line($"Hit");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, hitColor);
                        }
                        if (impactFactor >= _firstFaceLoc1x && impactFactor <= _lastFaceLoc1x)
                        {
                            //Log.Line($"Wave");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, waveColor);
                        }
                        if (impactFactor < _lastFaceLoc1x && _impactFinished)
                        {
                            //Log.Line($"Wave passed");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, wavePassedColor);
                        }
                        if (impactFactor > _lastFaceLoc1x && _impactFinished)
                        {
                            //Log.Line($"Wave coming");
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, waveComingColor);
                        }
                    }
                    else if (_impactCharge != 0)
                    {
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                            _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                            (v0 + v1 + v2) / 3, chargeColor);
                    }
                    else if (_glitch != 0)
                    {
                        //Log.Line($"Glitching");
                        if (impactFactor >= _firstFaceLoc6X && impactFactor <= _lastFaceLoc6X)
                        {
                            MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                                _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                                (v0 + v1 + v2) / 3, glitchColor);
                        }
                    }
                    else
                    {
                        //Log.Line($"Idle");
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                            _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                            (v0 + v1 + v2) / 3, pulseColor);
                    }
                if (lineMaterial.HasValue && lineThickness > 0)
                {
                    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref lineColor, lineThickness);
                    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref lineColor, lineThickness);
                    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref lineColor, lineThickness);
                }
                //if (_impactNew != 0)
                //{
                //    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref color3, lineWidth);
                //    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref color3, lineWidth);
                //    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref color3, lineWidth);
                //}
                //});
                #endregion
            }
            if (_impactNew != 0) _impactNew++;
            if (_glitch != 0) _glitch++;
            if (_impactCharge != 0 && _impactNew == 0) _impactCharge++;
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

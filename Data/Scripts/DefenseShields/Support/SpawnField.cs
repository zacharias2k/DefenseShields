using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields.Support
{
    class SpawnField
    {
        #region Cube+subparts Class
        public class Utils
        {
            //SPAWN METHOD
            public static IMyEntity Spawn(string subtypeId, string name = "", bool isVisible = true, bool hasPhysics = false, bool isStatic = false, bool toSave = false, bool destructible = false, long ownerId = 0)
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
        private readonly Vector3[] _vertexBuffer;
        private readonly int[][] _indexBuffer;
        private static readonly Random Random = new Random();
        private int _colourRand1 = 255;
        private int _colourRand2 = 0;



        public Icosphere(int lods)
        {
            float X = 0.525731112119133606f;
            float Z = 0.850650808352039932f;
            Vector3[] data =
            {
                new Vector3(-X, 0, Z), new Vector3(X, 0, Z), new Vector3(-X, 0, -Z), new Vector3(X, 0, -Z),
                new Vector3(0, Z, X), new Vector3(0, Z, -X), new Vector3(0, -Z, X), new Vector3(0, -Z, -X),
                new Vector3(Z, X, 0), new Vector3(-Z, X, 0), new Vector3(Z, -X, 0), new Vector3(-Z, -X, 0)
            };
            List<Vector3> points = new List<Vector3>(12 * (1 << (lods - 1)));
            points.AddRange(data);
            int[][] index = new int[lods][];
            index[0] = new int[]
            {
                0, 4, 1, 0, 9, 4, 9, 5, 4, 4, 5, 8, 4, 8, 1,
                8, 10, 1, 8, 3, 10, 5, 3, 8, 5, 2, 3, 2, 7, 3, 7, 10, 3, 7,
                6, 10, 7, 11, 6, 11, 0, 6, 0, 1, 6, 6, 1, 10, 9, 0, 11, 9,
                11, 2, 9, 2, 5, 7, 2, 11
            };
            for (int i = 1; i < lods; i++)
                index[i] = Subdivide(points, index[i - 1]);

            _indexBuffer = index;
            _vertexBuffer = points.ToArray();
        }

        public void Draw(MatrixD matrix, float radius, int lod, int count, bool enemy, Vector3D worldImpactPosition, MatrixD detectMatrix, MyStringId? faceMaterial = null, MyStringId? lineMaterial = null, float lineThickness = -1f)
        {
            var ix = _indexBuffer[lod];
            /*if (_colourRand1 - _colourRand2 > 180 || _colourRand1 - _colourRand2 < 100)
            {
                Log.Line($"{_colourRand1} - {_colourRand2} - {_colourRand1 - _colourRand2}");
            }*/
            if (count == 0)
            {
                _colourRand1 = Random.Next(144, 172);
                _colourRand2 = Random.Next(1, 64);
            }

            if (count % 6 == 0)
            {
                _colourRand1 += Random.Next(1, 32);
                _colourRand2 += Random.Next(1, 32);
                if (_colourRand1 - _colourRand2 < 80) _colourRand1 = _colourRand2 + 112;
                if (_colourRand1 - _colourRand2 > 200) _colourRand1 = _colourRand2 + 168;

            }

            var cv1 = 0;
            var cv2 = 0;
            if (enemy) cv1 = 50;
            else cv2 = 50;

            var c1 = Color.FromNonPremultiplied(_colourRand1 - _colourRand2, 0, 0, 16);
            var c2 = Color.FromNonPremultiplied(0, 0, _colourRand1 - _colourRand2, 16);
            var c3 = Color.FromNonPremultiplied(cv1, cv2, 0, 16);
            var c4 = Color.FromNonPremultiplied(0, 0, 0, 255);


            var color1 = enemy ? c1 : c2;
            var color2 = c3;
            Vector4 color3 = c4;

            for (var i = 0; i < ix.Length - 2; i += 3)
            {
                var i0 = ix[i];
                var i1 = ix[i + 1];
                var i2 = ix[i + 2];

                var v0 = Vector3.Transform(radius * _vertexBuffer[i0], matrix);
                var v1 = Vector3.Transform(radius * _vertexBuffer[i1], matrix);
                var v2 = Vector3.Transform(radius * _vertexBuffer[i2], matrix);

                // 0 close to impact, 1 far from impact
                var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
                fnorm.Normalize();
                var localImpact = Vector3D.Transform(worldImpactPosition, MatrixD.Invert(detectMatrix));
                localImpact.Normalize();
                var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;


                if (faceMaterial.HasValue)
                    if (impactFactor < 0.006 && !worldImpactPosition.Equals(new Vector3D(0, 0, 0)))
                    {
                        //Log.Line($"{impactFactor} - {localImpact} - {i}");
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                            _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                            (v0 + v1 + v2) / 3, color2);
                    }
                    else
                        MyTransparentGeometry.AddTriangleBillboard(v0, v1, v2, _vertexBuffer[i0], _vertexBuffer[i1],
                            _vertexBuffer[i2], Vector2.Zero, Vector2.Zero, Vector2.Zero, faceMaterial.Value, 0,
                            (v0 + v1 + v2) / 3, color1);
                if (lineMaterial.HasValue && lineThickness > 0)
                {
                    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref color3, lineThickness);
                    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref color3, lineThickness);
                    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref color3, lineThickness);
                }
                if (lineMaterial.HasValue && impactFactor < 0.05 && !worldImpactPosition.Equals(new Vector3D(0, 0, 0)))
                {
                    MySimpleObjectDraw.DrawLine(v0, v1, lineMaterial, ref color3, 0.25f);
                    MySimpleObjectDraw.DrawLine(v1, v2, lineMaterial, ref color3, 0.25f);
                    MySimpleObjectDraw.DrawLine(v2, v0, lineMaterial, ref color3, 0.25f);
                }
            }
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

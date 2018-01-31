using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DefenseShields.Data.Scripts.DefenseShields
{
    class OldCode
    {
        /*
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
         
        private void BuildCollections(MatrixD matrix, Vector3D ImpactPos)
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

            private void LodNormalization()
            {
                Log.Line($"Previous lod was {_prevLod} current lod is {_lod}");
                var ixNew = IcoSphere.IcoIcoSphere._indexBuffer[_lod];
                var ixLenNew = ixNew.Length;
                var ixPrev = IcoSphere.IcoIcoSphere._indexBuffer[_prevLod];
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
        }

        if (faceMaterial.HasValue && _lod > 2)
            if (_impactCount != 0)
                if (impactFactor <= _lastHitFaceLoc1x)

                if (impactFactor >= _firstFaceLoc1x && impactFactor <= _lastFaceLoc1x)

                if (impactFactor < _lastFaceLoc1x)

                if (impactFactor > _lastFaceLoc1x)
            else if (_chargeCount != 0)
            else if (_glitchCount != 0)
        //var localImpact = Vector3D.Transform(_impactPos, MatrixD.Invert(matrix));
        //localImpact.Normalize();

        //var fnorm = (_vertexBuffer[i0] + _vertexBuffer[i1] + _vertexBuffer[i2]);
        //fnorm.Normalize();
        //var impactFactor = 1 - (Vector3D.Dot(localImpact, fnorm) + 1) / 2;
        //var matrixTranslation = matrix.Translation;
        */
    }
}

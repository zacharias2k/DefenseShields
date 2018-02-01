using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
using ProtoBuf;
using System;
using Sandbox.ModAPI.Weapons;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, new string[] { "StationDefenseShield" })]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;
        private const float Massdmg = 0.0025f;
        private const float InOutSpace = 15f;

        private float _power = 0.0001f;
        private float _animStep;
        private float Range;
        private float Width;
        private float Height;
        private float Depth;
        private float _recharge;
        private float _absorb;

        private int Count = -1;
        private int Playercount = 600;
        private int Gridcount = 600;
        private int _time;
        private int _playertime;
        private int _lod;
        private int _prevLod;

        private bool _entityChanged = true;
        private bool NotInitialized = true;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _shotwebbed;
        private bool _shotlocked;
        private bool _closegrids;
        private bool _playerkill;
        private bool _gridIsMobile;
        private bool _warmUp;

        private const ushort ModId = 50099;

        private Vector3D WorldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D DetectionCenter;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;

        private BoundingBox OldGridAabb;
        private MatrixD ShieldShapeMatrix;

        private IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity Shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance Icosphere;
        private MyEntitySubpart _subpartRotor;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> Slider;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> Ellipsoid;
        private MyResourceSinkComponent _sink;
        private MyDefinitionId PowerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash { get; private set; } = new MyConcurrentHashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyGridHash { get; private set; } = new HashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyPlayerHash { get; private set; } = new HashSet<IMyEntity>();

        private readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();



        Stopwatch sw = new Stopwatch();
        #endregion

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

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); Icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); Icosphere = null; } 
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }

        private void StopWatchReport(string message)
        {
            long ticks = sw.ElapsedTicks;
            double ns = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;

            Log.Line($"{message} - ns:{ns} ms:{ms} s:{s}");
        }

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out _sink);
            _sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!Shields.ContainsKey(Entity.EntityId)) Shields.Add(Entity.EntityId, this);
        }
        #endregion

        #region Interfaces
        private interface IPlayerKill{ void PlayerKill(); }
        private interface IGridClose { void GridClose(); }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                //sw.Start();
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && !NotInitialized && Block.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (Count++ == 59) Count = 0;
                if (Count <= 0)
                {
                    if (_warmUp == false)
                    {
                        var warming = true;
                        foreach (var s in Shields)
                        {
                            if (s.Value.NotInitialized) 
                            {
                                warming = false;
                            }
                        }
                        Count = -1;
                        InHashBuilder();
                        if (warming) _warmUp = true;
                        return;
                    }
                    MyAPIGateway.Parallel.Do(InHashBuilder);
                }

                if (Playercount < 600) Playercount++;
                if (Gridcount < 600) Gridcount++;
                if (Count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_playerkill || Playercount == 479)
                {
                    if (_playerkill) Playercount = -1;
                    _playerkill = false;
                    //if (DestroyPlayerHash.Count > 0) DestroyEntity.PlayerKill(Playercount);
                }
                if (_closegrids || Gridcount == 59 || Gridcount == 179 || Gridcount == 299 || Gridcount == 419 || Gridcount == 479|| Gridcount == 599)
                {
                    if (_closegrids) Gridcount = -1;
                    _closegrids = false;
                    //if (DestroyGridHash.Count > 0) DestroyEntity.GridClose(Gridcount);
                }
                UpdateDetection();
                if (NotInitialized || !Block.IsWorking) return;
                MyAPIGateway.Parallel.StartBackground(WebEntities);
                if (_shotwebbed && !_shotlocked) MyAPIGateway.Parallel.Do(ShotEffects);
                if (_playerwebbed) MyAPIGateway.Parallel.Do(PlayerEffects);
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in UpdateBeforeSimulation");
                Log.Line($" {ex}");
            }
            //Log.Line($"{Count}");
            //sw.Stop();
            //StopWatchReport("Full loop");
            //sw.Reset();
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!NotInitialized) return;

            if (Block.CubeGrid.Physics.IsStatic) _gridIsMobile = false;
            else if (!Block.CubeGrid.Physics.IsStatic) _gridIsMobile = true;

            CreateUi();
            Block.AppendingCustomInfo += AppendingCustomInfo;
            Block.RefreshCustomInfo();
            _absorb = 150f;

            Shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
            Shield.Render.Visible = false;

            DefenseShieldsBase.Instance.Shields.Add(this);
            NotInitialized = false;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_gridIsMobile && _entityChanged) UpdateDetection();
                if (_animInit) return;
                if (Block.BlockDefinition.SubtypeId == "StationDefenseShield")
                {
                    if (!Block.IsFunctional) return;
                    BlockAnimationInit();
                    Log.Line($" BlockAnimation {Count}");
                    _animInit = true;
                }
                else
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in UpdateAfterSimulation");
                Log.Line($"{ex}");
            }

        }
        #endregion

        #region Block Power and Config Logic
        private float CalcRequiredPower()
        {
            if (NotInitialized) return _power;
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
            var radius = Slider.Getter(Block);
            var sustaincost = radius * 0.01f;
            _power = _recharge + sustaincost;
            return _power;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");
            if (_gridIsMobile)
            {
                UpdateDetection();
                return;
            }
            Range = GetRadius();
            if (Ellipsoid.Getter(block).Equals(true))
            {
                Width = Range * 0.5f;
                Height = Range * 0.35f;
                Depth = Range;
                UpdateDetection();
            }
            else
            {
                Width = Range;
                Depth = Range;
                Height = Range;
                UpdateDetection();
            }
        }
        #endregion

        #region Create UI
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

        private float GetRadius()
        {
            return Slider.Getter(Block);
        }

        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void CreateUi()
        {
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();


            Ellipsoid = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block,
                "Ellipsoid",
                "Switch to Ellipsoid",
                false);

            Slider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block,
                "RadiusSlider",
                "Shield Size",
                50,
                300,
                300);
        }
        #endregion

        #region Block Animation
        private void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {Count}");
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
            catch (Exception ex)
            {
                Log.Line($"Exception in BlockAnimation");
                Log.Line($"{ex}");
            }
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
                _subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
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
        public bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Block.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + Range) * (x + Range);
            return range;
        }

        private Task? _prepareTask = null;

        public void Draw()
        {
            if (NotInitialized) return;
            _entityChanged = OldGridAabb != Block.CubeGrid.LocalAABB;
            OldGridAabb = Block.CubeGrid.LocalAABB;
            var entitychanged = _entityChanged;

            if (Block.CubeGrid.Physics.IsStatic && entitychanged) ShieldShapeMatrix = MatrixD.Rescale(MatrixD.Identity, new Vector3D(Width, Height, Depth));
            else if (entitychanged) ShieldShapeMatrix = MatrixD.CreateScale(Block.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Block.CubeGrid.PositionComp.LocalAABB.Center); // * Cblock.CubeGrid.WorldMatrix;
            var shapematrix = ShieldShapeMatrix;
            var detectionmatrix = DetectionMatrix;
            if (!Shield.WorldMatrix.Equals(shapematrix)) Shield.SetWorldMatrix(shapematrix);
            var impactpos = WorldImpactPosition;
           if (_gridIsMobile && impactpos != Vector3D.NegativeInfinity)
            {
                Log.Line($"impactPos from Entity: {impactpos}");
                var dm = MatrixD.Invert(detectionmatrix);
                impactpos = Vector3D.TransformNormal(impactpos, dm);
            }
            WorldImpactPosition = Vector3D.NegativeInfinity;

            var shield = Shield;

            int lod;
            int lod2;
            bool enemy;

            if (Distance(650)) lod = 3;
            else if (Distance(2250)) lod = 3;
            else if (Distance(4500)) lod = 1;
            else if (Distance(15000)) lod = 1;
            else if (Distance(25000)) lod = 1;
            else lod = 0;
            lod2 = lod <= 1 ? lod : 2;

            _lod = lod;
            var prevlod = _prevLod;
            _prevLod = lod;

            /*
            var gridmatrix = Cblock.CubeGrid.WorldMatrix;
            var gridaabb = (BoundingBoxD)Cblock.CubeGrid.LocalAABB;
            
            var c = Color.Red;
            MySimpleObjectDraw.DrawTransparentBox(ref gridmatrix, ref gridaabb, ref c, MySimpleObjectRasterizer.Wireframe, 1, 0.04f);
            */

            //_entityChanged = !ShieldShapeMatrix.EqualsFast(ref OldShieldShapeMatrix) || Cblock.CubeGrid.Min != GridMinMax[0] || Cblock.CubeGrid.Max != GridMinMax[1];

            /*
            var entAngularVelocity = Vector3D.IsZero(Cblock.CubeGrid.Physics.AngularVelocity);
            var entLinVel = Vector3D.IsZero(Cblock.CubeGrid.Physics.GetVelocityAtPoint(Cblock.CubeGrid.PositionComp.WorldMatrix.Translation));
            if (!entAngularVelocity || !entLinVel)
            {
                const float dt = MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
                var rate = 0d;
                var angVel = Vector3D.Zero;
                if (!entAngularVelocity)
                {
                    angVel = Vector3D.TransformNormal((Vector3D)Cblock.CubeGrid.Physics.AngularVelocity, Cblock.CubeGrid.PositionComp.LocalMatrix);
                    rate = angVel.Normalize() * dt;
                }
                //PredictedGridWorldMatrix = MatrixD.CreateFromAxisAngle(angVel, rate) * Cblock.CubeGrid.PositionComp.WorldMatrix;
                //PredictedGridWorldMatrix.Translation = Cblock.CubeGrid.PositionComp.WorldMatrix.Translation + Cblock.CubeGrid.Physics.GetVelocityAtPoint(Cblock.CubeGrid.PositionComp.WorldMatrix.Translation) * dt;
                //ShieldShapeMatrix = MatrixD.CreateScale(Cblock.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt3 + 8) * MatrixD.CreateTranslation(Cblock.CubeGrid.PositionComp.LocalAABB.Center) * Cblock.CubeGrid.WorldMatrix;
                //PredictedGridWorldMatrix.Translation = Cblock.PositionComp.WorldMatrix.Translation + Cblock.CubeGrid.Physics.GetVelocityAtPoint(Cblock.PositionComp.WorldMatrix.Translation) * dtTwo + Cblock.CubeGrid.Physics.LinearAcceleration * 0.5f * dtTwo * dtTwo;
                ShieldShapeMatrix = MatrixD.CreateScale(Cblock.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Cblock.CubeGrid.PositionComp.LocalAABB.Center); //* PredictedGridWorldMatrix;
            }
            */
            var sp = new BoundingSphereD(Entity.GetPosition(), Range);
            var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);

            var relations = Block.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) enemy = false;
            else enemy = true;
            //entitychanged = true;
            uint renderId = Block.CubeGrid.Render.GetRenderObjectID();
            //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{_sphereOnCamera} - RenderID {renderId}");

            if (!sphereOnCamera || !Block.IsWorking || renderId == 0) return;
            if (_prepareTask.HasValue && !_prepareTask.Value.IsComplete) _prepareTask.Value.Wait();
            if (_prepareTask.HasValue && _prepareTask.Value.IsComplete) Icosphere.Draw(renderId);
            _prepareTask = MyAPIGateway.Parallel.Start(() => PrepareSphere(entitychanged, enemy, lod, prevlod, impactpos, shapematrix, detectionmatrix, shield));
        }

        private void PrepareSphere(bool entitychanged, bool enemy, int lod, int prevlod, Vector3D impactpos, MatrixD shapematrix, MatrixD detectionmatrix, IMyEntity shield)
        {
            if (entitychanged || lod != prevlod) Icosphere.CalculateTransform(shapematrix, lod);
            Icosphere.CalculateColor(detectionmatrix, impactpos, entitychanged, enemy, shield);
        }
        #endregion

        #region Detect Intersection
        private void UpdateDetection()
        {
            if (_gridIsMobile)
            {
                DetectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;
                Range = (float)ShieldShapeMatrix.Scale.AbsMax() + 15f;
                DetectionMatrix = ShieldShapeMatrix * Block.CubeGrid.WorldMatrix;
            }
            else
            {
                DetectionCenter = Block.PositionComp.GetPosition();
                DetectionMatrix = ShieldShapeMatrix; 
            }
        }

        private Vector3D ContactPoint(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = DetectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        private bool DetectCollision(IMyEntity ent)
        {
            //In this code I compute a point - to - test by computing  a point on the entity's sphere (or the center of the ellipsoid if it's contained by the sphere)
            //var wTest = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            //So subtract from the radius of the entity(edited)
            //(It's fine if that goes negative -- if it does that will move the test point away from the ellipsoid)
            //var wVol = ent.PositionComp.WorldVolume;
            //var wDir = DetectionMatrix.Translation - wVol.Center;
            //var wLen = wDir.Length();
            //var wTest = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            if (Vector3D.Transform(ContactPoint(ent), _detectionMatrixInv).LengthSquared() <= 1) Log.Line($"ent: {ent} - Detect: {Vector3D.Transform(ContactPoint(ent), _detectionMatrixInv).LengthSquared()}");
            return Vector3D.Transform(ContactPoint(ent), _detectionMatrixInv).LengthSquared() <= 1;
        }
        #endregion

        private double ContainmentField(IMyEntity breaching, IMyEntity field)
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

            var transformInv = MatrixD.Invert(ShieldShapeMatrix);
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(ContactPoint(breaching), transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            breaching.Physics.ApplyImpulse(breaching.Physics.Mass * 2 * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, ContactPoint(breaching));
            
            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }

        #region Build inside HashSet
        private void InHashBuilder()
        {
            //var pos = Tblock.CubeGrid.GridIntegerToWorld(Tblock.Position);
            var insphere = new BoundingSphereD(DetectionCenter, Range - InOutSpace);
            var inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (inent is IMyCubeGrid || inent is IMyCharacter && DetectCollision(inent))
                {
                    lock (InHash)
                    {
                        InHash.Add(inent);
                    }
                }
            });
        }
        #endregion

        #region ImpactPos
        private void ImpactTimer(IMyEntity ent)
        {
            WorldImpactPosition = ent.GetPosition();
        }
        #endregion

        #region Web and dispatch all intersecting entities
        private void WebEntities()
        {
            //var pos = Tblock.CubeGrid.GridIntegerToWorld(Tblock.Position);

            var websphere = new BoundingSphereD(DetectionCenter, Range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
               // sw.Start();
                //sw.Stop();
                //StopWatchReport("ApplyImpulse Performance");
                //sw.Reset();
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (_shotwebbed) return;
                    if (DetectCollision(webent))
                    {
                        _shotwebbed = true;
                    }
                    return;
                }
                if (webent is IMyCharacter && (Count == 2 || Count == 17 || Count == 32 || Count == 47) && DetectCollision(webent))
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(webent).IdentityId;
                    var playerrelationship = Block.GetUserRelationToOwner(dude);
                    if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                    _playerwebbed = true;
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == Block.CubeGrid || DestroyGridHash.Contains(grid) || grid == null) return;

                var owners = grid.BigOwners;
                if (owners.Count > 0)
                {
                    var relations = Block.GetUserRelationToOwner(owners[0]);
                    //Log.Line(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                    if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                }
                if (DetectCollision(grid))
                {
                    Log.Line($"Detect is true");
                    ImpactTimer(grid);
                    var griddmg = grid.Physics.Mass * Massdmg;
                    _absorb += griddmg;
                    Log.Line($" gridEffect: {grid} Shield Strike by a {(griddmg / Massdmg)}kilo grid, absorbing {griddmg}MW of energy in loop {Count}");
                    /*
                    _closegrids = true;
                    
                    DestroyGridHash.Add(grid);

                    var vel = grid.Physics.LinearVelocity;
                    vel.SetDim(0, (int)(vel.GetDim(0) * -8.0f));
                    vel.SetDim(1, (int)(vel.GetDim(1) * -8.0f));
                    vel.SetDim(2, (int)(vel.GetDim(2) * -8.0f));
                    grid.Physics.LinearVelocity = vel;

                    var playerchar = MyAPIGateway.Players.GetPlayerControllingEntity(grid).Character;
                    if (playerchar == null) return;
                    DestroyPlayerHash.Add(playerchar);
                    _playerkill = true;
                    */
                    var test = ContainmentField(grid, Block.CubeGrid);
                    Log.Line($"{test}");
                    return;
                }
                //Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
        }
        #endregion

        #region shot effects
        private void ShotEffects()
        {
            _shotlocked = true;
            //var pos = Tblock.CubeGrid.GridIntegerToWorld(Tblock.Position);

            var shotHash = new HashSet<IMyEntity>();
            var shotsphere = new BoundingSphereD(DetectionCenter, Range);
            MyAPIGateway.Entities.GetEntities(shotHash, ent => shotsphere.Intersects(ent.WorldAABB) && ent is IMyMeteor || ent.ToString().Contains("Missile") || ent.ToString().Contains("Torpedo"));

            foreach (var shotent in shotHash)
            {
                Log.Line($"shot being processed {shotent}");
                if (shotent == null || !DetectCollision(shotent)) return;
                try
                {
                    ImpactTimer(shotent);
                    _absorb += Shotdmg;
                    Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {shotent} in loop {Count}");
                    shotent.Close();
                }
                catch (Exception ex)
                {
                    Log.Line($"Exception in shotEffects");
                    Log.Line($"{ex}");
                }
            }
            _shotwebbed = false;
            _shotlocked = false;
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
                catch (Exception ex)
                {
                    Log.Line($" Exception in playerEffects");
                    Log.Line($" {ex}");
                }
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
    }
}
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
using BulletXNA.BulletCollision;
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
        private float _animStep;
        public float Range;
        public float Width;
        public float Height;
        public float Depth;							   															   							   
        private float _recharge;
        private float _absorb;
        private float _power = 0.0001f;
        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;
        private const float Massdmg = 0.0025f;
        private const float InOutSpace = 15f;
        private float _mobileEntitySize; 

        public int Count = -1;
        public int Playercount = 600;
        public int Gridcount = 600;
        private int _time;
        private int _playertime;
        private int _lod;
        private int _prevLod;

        public bool NotInitialized = true;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _shotwebbed;
        private bool _shotlocked;
        private bool _closegrids;
        private bool _playerkill;
        private bool _entityChanged = true;
        private bool _entityIsMobile;
        private bool _warmUp = false;

        private const ushort ModId = 50099;

        public Vector3D WorldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        public Vector3D DetectionCenter;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;

        public BoundingBox OldGridAabb;
        public MatrixD BlockWorldMatrix;
        public MatrixD ShieldShapeMatrix;
        //public MatrixD PredictedGridWorldMatrix;
        //public MatrixD ReSized;
        //MatrixD shieldShapeMatrix = MatrixD.Identity;

        public IMyOreDetector Oblock;
        public IMyFunctionalBlock Fblock;
        public IMyTerminalBlock Tblock;
        public IMyCubeBlock Cblock;
        public IMyEntity Shield;

        public Icosphere.Instance Sphere;

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); Sphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); Sphere = null; } // check
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }

        private MyEntitySubpart _subpartRotor;
        public RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> Slider;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> Ellipsoid;
        public MyResourceSinkComponent Sink;
        public MyDefinitionId PowerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash = new MyConcurrentHashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyGridHash = new HashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyPlayerHash = new HashSet<IMyEntity>();

        public readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();

        private static readonly Random Random = new Random();

        Stopwatch sw = new Stopwatch();

        #endregion

        public MatrixD DetectionMatrix
        {
            get { return _detectionMatrix; }
            set
            {
                _detectionMatrix = value;
                _detectionMatrixInv = MatrixD.Invert(value);
            }
        }

        public void StopWatchReport()
        {
            long ticks = sw.ElapsedTicks;
            double ns = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;

            Log.Line($"ns:{ns} ms:{ms} s:{s}");
        }

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            Oblock = Entity as IMyOreDetector; 
            Fblock = Entity as IMyFunctionalBlock;
            Tblock = Entity as IMyTerminalBlock;
            Cblock = Entity as IMyCubeBlock;

            if (!Shields.ContainsKey(Entity.EntityId)) Shields.Add(Entity.EntityId, this);
        }
        #endregion

        #region Interfaces
        public interface IPlayerKill{ void PlayerKill(); }
        public interface IGridClose { void GridClose(); }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && !NotInitialized && Oblock.IsWorking)
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
                                //Sphere.CalculateTransform(ShieldShapeMatrix, _lod);
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
                    Tblock.GameLogic.GetAs<DefenseShields>().Sink.Update();
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
                if (!NotInitialized && Oblock.IsWorking)
                {
                    MyAPIGateway.Parallel.StartBackground(WebEntities);
                    if (_shotwebbed && !_shotlocked) MyAPIGateway.Parallel.Do(ShotEffects);
                    if (_playerwebbed) MyAPIGateway.Parallel.Do(PlayerEffects);
                }
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in UpdateBeforeSimulation");
                Log.Line($" {ex}");
            }
            //Log.Line($"{Count}");
        }

        public override void UpdateBeforeSimulation100()
        {
            if (NotInitialized)
            {
                if (Cblock.CubeGrid.Physics.IsStatic) _entityIsMobile = false;
                else if (!Cblock.CubeGrid.Physics.IsStatic) _entityIsMobile = true;
                Log.Line($"BeforeSim100 - H:{Height} W:{Width} D:{Depth} R:{Range}");
                CreateUi();
                Oblock.AppendingCustomInfo += AppendingCustomInfo;
                Tblock.RefreshCustomInfo();
                Log.Line($"");
                _absorb = 150f;
                var modPath = DefenseShieldsBase.Instance.ModPath();
                Shield = Spawn.Utils.Sphere("Field", $"{modPath}\\Models\\LargeField0.mwm");
                Shield.Render.Visible = false;

                DefenseShieldsBase.Instance.Shields.Add(this);

                NotInitialized = false;
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_entityIsMobile) UpdateDetection();
                if (!_animInit)
                {
                    if (Oblock.BlockDefinition.SubtypeId == "StationDefenseShield")
                    {
                        if (!Oblock.IsFunctional) return;
                        BlockAnimationInit();
                        Log.Line($" BlockAnimation {Count}");
                        _animInit = true;
                    }
                    else
                    {
                        NeedsUpdate = MyEntityUpdateEnum.NONE;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in UpdateAfterSimulation");
                Log.Line($"{ex}");
            }
        }
        #endregion

        #region Block Animation
        public void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {Count}");
            _subpartRotor.Subparts.Clear();
            _subpartsArms.Clear();
            _subpartsReflectors.Clear();
            BlockAnimationInit();
        }

        public void BlockAnimationInit()
        {
            try
            {
                _animStep = 0f;

                _matrixArmsOff = new List<Matrix>();
                _matrixArmsOn = new List<Matrix>();
                _matrixReflectorsOff = new List<Matrix>();
                _matrixReflectorsOn = new List<Matrix>();

                BlockWorldMatrix = Entity.WorldMatrix;
                BlockWorldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;

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

        public void BlockAnimation()
        {
            BlockWorldMatrix = Entity.WorldMatrix;
            BlockWorldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;
            //Animations
            if (Fblock.Enabled && Fblock.IsFunctional && Oblock.IsWorking)
            {
                //Color change for on =-=-=-=-
                _subpartRotor.SetEmissiveParts("Emissive", Color.White, 1);
                _time += 1;
                Matrix temp1 = Matrix.CreateRotationY(0.1f * _time);
                temp1.Translation = _subpartRotor.PositionComp.LocalMatrix.Translation;
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {
                //Color change for off =-=-=-=-
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

        #region Power Logic
        float GetRadius()
        {
            return Slider.Getter(Oblock);
        }

        public float CalcRequiredPower()
        {

            if (!NotInitialized)
            {
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
                var radius = Slider.Getter(Oblock);
                var sustaincost = radius * 0.01f;
                _power = _recharge + sustaincost;
                return _power;
            }
            return _power;
        }

        void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");
            if (_entityIsMobile)
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

        #region Cleanup
        public override void Close()
        {
            try
            {
                DefenseShieldsBase.Instance.Shields.RemoveAt(DefenseShieldsBase.Instance.Shields.IndexOf(this));
                //MyAPIGateway.Entities.RemoveEntity(Shield);
            }
            catch{}
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
                //MyAPIGateway.Entities.RemoveEntity(Shield);
            }
            catch {}
            base.MarkForClose();
        }
        #endregion

        #region Create UI
        void RemoveOreUi()
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

        bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        public void CreateUi()
        {
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            
            Ellipsoid = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Oblock,
                "Ellipsoid",
                "Switch to Ellipsoid",
                false);
            
            Slider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Oblock,
                "RadiusSlider",
                "Shield Size",
                50,
                300,
                300);
        }
        #endregion
      
        public bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Cblock.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + Range) * (x + Range);
            return range;
        }

        private Task? _prepareTask = null;

        public void Draw()
        {
            if (NotInitialized) return;
            _entityChanged = OldGridAabb != Cblock.CubeGrid.LocalAABB;
            OldGridAabb = Cblock.CubeGrid.LocalAABB;
            var impactpos = WorldImpactPosition;
            WorldImpactPosition = Vector3D.NegativeInfinity;
            var entitychanged = _entityChanged;

            var blockworldmatrix = BlockWorldMatrix;
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
            if (lod <= 1) lod2 = lod;
            else lod2 = 2;

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
            if (Cblock.CubeGrid.Physics.IsStatic && entitychanged) ShieldShapeMatrix = MatrixD.Rescale(blockworldmatrix, new Vector3D(Width, Height, Depth));
            else if (entitychanged) ShieldShapeMatrix = MatrixD.CreateScale(Cblock.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Cblock.CubeGrid.PositionComp.LocalAABB.Center); // * Cblock.CubeGrid.WorldMatrix;
            var shapematrix = ShieldShapeMatrix;
            if (!Shield.WorldMatrix.Equals(shapematrix)) Shield.SetWorldMatrix(shapematrix);

            var sp = new BoundingSphereD(Entity.GetPosition(), Range);
            var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);

            var relations = Oblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) enemy = false;
            else enemy = true;
            entitychanged = true;
            uint renderId = Cblock.CubeGrid.Render.GetRenderObjectID();
            //Log.Line($"Grid name {Cblock.CubeGrid.CustomName} - RenderID {renderId} - Ent: {Entity}");
            //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{_sphereOnCamera}");
            if (sphereOnCamera && Oblock.IsWorking && renderId != 0)
            {
                if (_prepareTask.HasValue && !_prepareTask.Value.IsComplete) _prepareTask.Value.Wait();
                if (_prepareTask.HasValue && _prepareTask.Value.IsComplete) Sphere.Draw(renderId);
                _prepareTask = MyAPIGateway.Parallel.Start(() => PrepareSphere(entitychanged, enemy, lod, prevlod, impactpos, shapematrix, shield));
            }
        }

        private void PrepareSphere(bool entitychanged, bool enemy, int lod, int prevlod, Vector3D impactpos, MatrixD shapematrix, IMyEntity shield)
        {
            if (entitychanged || lod != prevlod) Sphere.CalculateTransform(shapematrix, lod);
            Sphere.CalculateColor(shapematrix, impactpos, entitychanged, enemy, shield);
        }

        private void UpdateDetection()
        {
            if (_entityIsMobile)
            {
                DetectionCenter = Cblock.CubeGrid.PositionComp.WorldVolume.Center;
                Range = (float)ShieldShapeMatrix.Scale.AbsMax() * 2 + 15f;
                Width = (float)ShieldShapeMatrix.Left.Length() * 2;
                Depth = (float)ShieldShapeMatrix.Forward.Length() * 2;
                Height = (float)ShieldShapeMatrix.Up.Length() * 2;
                DetectionMatrix = ShieldShapeMatrix;
            }
            else
            {
                DetectionCenter = Cblock.PositionComp.GetPosition();
                DetectionMatrix = ShieldShapeMatrix;
            }
        }

        #region Impact
        private void ImpactTimer(IMyEntity ent)
        {
            WorldImpactPosition = ent.GetPosition();
        }
        #endregion

        #region Detect Intersection

        private bool DetectCollision(IMyEntity ent)
        {
            var wVol = ent.PositionComp.WorldVolume;
            var wDir = DetectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var wTest = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            Log.Line($"ent: {ent} - Detect:{Vector3D.Transform(wTest, _detectionMatrixInv).LengthSquared() <= 1}");
            return Vector3D.Transform(wTest, _detectionMatrixInv).LengthSquared() <= 1;
        }

        private bool Detectedge(IMyEntity ent, float f)
        {
            float x = Vector3Extensions.Project(DetectionMatrix.Left, ent.GetPosition() - DetectionMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(DetectionMatrix.Forward, ent.GetPosition() - DetectionMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(DetectionMatrix.Up, ent.GetPosition() - DetectionMatrix.Translation).AbsMax();
            float detect = (x * x) / ((Width - f) * (Width - f)) + (y * y) / ((Depth - f) * (Depth - f)) + (z * z) / ((Height - f) * (Height - f));
            if (ent is IMyCharacter) Log.Line($"Ent: {ent.DisplayName} - EID {Entity.EntityId} - Detect: {detect}");
            if (detect <= 1)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Build inside HashSet
        public void InHashBuilder()
        {
            //var pos = Tblock.CubeGrid.GridIntegerToWorld(Tblock.Position);
            var insphere = new BoundingSphereD(DetectionCenter, Range - InOutSpace);
            List<IMyEntity> inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

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

        #region Web and dispatch all intersecting entities
        public void WebEntities()
        {
            //var pos = Tblock.CubeGrid.GridIntegerToWorld(Tblock.Position);

            var websphere = new BoundingSphereD(DetectionCenter, Range);
            List<IMyEntity> webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (webent.ToString().Contains("Missile")) Log.Line($"shot detected {webent}");
                    if (_shotwebbed) return;
                    if (DetectCollision(webent))
                    {
                        _shotwebbed = true;
                    }
                    return;
                }
                if (webent is IMyCharacter && (Count == 2 || Count == 17 || Count == 32 || Count == 47) && DetectCollision(webent))
                {
                    Log.Line($"bounding {DetectionCenter} - r:{Range} - h:{Height} - w:{Width} - d:{Depth}");
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(webent).IdentityId;
                    var playerrelationship = Tblock.GetUserRelationToOwner(dude);
                    if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                    _playerwebbed = true;
                }
                
                if (webent is IMyCharacter || InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == Tblock.CubeGrid || DestroyGridHash.Contains(grid) || grid == null) return;

                var owners = grid.BigOwners;
                if (owners.Count > 0)
                {
                    var relations = Tblock.GetUserRelationToOwner(owners[0]);
                    //Log.Line(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                    if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                }
                if (DetectCollision(grid))
                {
                    ImpactTimer(grid);
                    var griddmg = grid.Physics.Mass * Massdmg;
                    _absorb += griddmg;
                    Log.Line($" gridEffect: {grid} Shield Strike by a {(griddmg / Massdmg)}kilo grid, absorbing {griddmg}MW of energy in loop {Count}");

                    _closegrids = true;
                    DestroyGridHash.Add(grid);

                    var vel = grid.Physics.LinearVelocity;
                    vel.SetDim(0, (int)(vel.GetDim(0) * -8.0f));
                    vel.SetDim(1, (int)(vel.GetDim(1) * -8.0f));
                    vel.SetDim(2, (int)(vel.GetDim(2) * -8.0f));
                    grid.Physics.LinearVelocity = vel;
                    /*
                    var direction = Vector3D.Normalize(grid.Center() - grid.Center);
                    Vector3D velocity = grid.Physics.LinearVelocity;
                    if (Vector3D.IsZero(velocity))
                        velocity += direction;
                    Vector3D forceDir = Vector3D.Reflect(Vector3D.Normalize(velocity), direction);
                    grid.Physics.SetSpeeds(velocity * forceDir, grid.Physics.AngularVelocity);
                    var dist = Vector3D.Distance(grid.GetPosition(), websphere.Center);

                    var d = grid.Physics.CenterOfMass - thingRepellingYou;
                    var v = d * repulsionVelocity / d.Length();
                    grid.Physics.AddForce((v - grid.Physics.LinearVelocity) * grid.Physics.Mass / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                    */
                    var playerchar = MyAPIGateway.Players.GetPlayerControllingEntity(grid).Character;
                    if (playerchar == null) return;
                    DestroyPlayerHash.Add(playerchar);
                    _playerkill = true;
                    return;
                }
                Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
        }
        #endregion

        #region shot effects
        public void ShotEffects()
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
        public void PlayerEffects()
        {
            var rnd = new Random();
            //MyAPIGateway.Parallel.ForEach(InHash, playerent =>
            foreach (var playerent in InHash)
            {
                if (!(playerent is IMyCharacter)) continue;
                try
                {
                    var playerid = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                    var relationship = Tblock.GetUserRelationToOwner(playerid);
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
                //});
            }
            _playerwebbed = false;
        }
        #endregion
    }
}
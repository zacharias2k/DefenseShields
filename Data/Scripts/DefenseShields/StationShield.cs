using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
using ProtoBuf;
using System;
using Sandbox.ModAPI.Weapons;
using System.Collections.Generic;
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
        private const float InOutSpace = 15f;

        private float _power = 0.0001f;
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;

        private int _count = -1;
        private int _playercount = 600;
        private int _gridcount = 600;
        private int _time;
        private int _playertime;
        private int _prevLod;

        private bool _entityChanged = true;
        private bool _initialized;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _closegrids;
        private bool _playerkill;
        private bool _gridIsMobile;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _detectionCenter;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;

        private BoundingBox _oldGridAabb;
        private MatrixD _shieldShapeMatrix;

        private IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;
        private MyEntitySubpart _subpartRotor;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;

        //private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> Ellipsoid;
        private MyResourceSinkComponent _sink;
        private readonly MyDefinitionId _powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash { get; } = new MyConcurrentHashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyGridHash { get; } = new HashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyPlayerHash { get; } = new HashSet<IMyEntity>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

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

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; } 
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        #endregion

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out _sink);
            _sink.SetRequiredInputFuncByType(_powerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
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
                if (_count++ == 59) _count = 0;
                if (_count <= 0)
                {
                    if (!_initialized)
                    {
                        _count = -1;
                        InHashBuilder();
                        return;
                    }
                    InHashBuilder();
                }
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && _initialized && Block.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (_playercount < 600) _playercount++;
                if (_gridcount < 600) _gridcount++;
                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_gridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); //remove when impact local vec is fixed
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation)); //remove when impact local vec is fixed
                    _entityChanged = _oldGridAabb != Block.CubeGrid.LocalAABB || entAngularVelocity || entLinVel;
                    if (_entityChanged || _range <= 0) UpdateDetection();
                }
                if (Block.CubeGrid.Physics.IsStatic) _entityChanged = RefreshDimensions();
                if (_playerkill || _playercount == 479)
                {
                    if (_playerkill) _playercount = -1;
                    _playerkill = false;
                    //if (DestroyPlayerHash.Count > 0) DestroyEntity.PlayerKill(Playercount);
                }
                if (_closegrids || _gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419 || _gridcount == 479 || _gridcount == 599)
                {
                    if (_closegrids) _gridcount = -1;
                    _closegrids = false;
                    //if (DestroyGridHash.Count > 0) DestroyEntity.GridClose(Gridcount);
                }
                if (!_initialized || !Block.IsWorking) return;
                MyAPIGateway.Parallel.StartBackground(WebEntities);
                if (_playerwebbed) PlayerEffects();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_initialized) return;
            Log.Line($"Initting entity");
            if (Block.CubeGrid.Physics.IsStatic) _gridIsMobile = false;
            else if (!Block.CubeGrid.Physics.IsStatic) _gridIsMobile = true;

            CreateUi();
            Block.AppendingCustomInfo += AppendingCustomInfo;
            Block.RefreshCustomInfo();
            _absorb = 150f;

            _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
            _shield.Render.Visible = false;

            DefenseShieldsBase.Instance.Shields.Add(this);
            _initialized = true;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_animInit) return;
                if (Block.BlockDefinition.SubtypeId == "StationDefenseShield")
                {
                    if (!Block.IsFunctional) return;
                    BlockAnimationInit();
                    Log.Line($" BlockAnimation {_count}");
                    _animInit = true;
                }
                else
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation: {ex}"); }
        }
        #endregion

        #region Block Power and Entity Config Logic
        private float CalcRequiredPower()
        {
            if (!_initialized || !Block.IsWorking) return _power;
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
            var radius = GetRadius();
            var sustaincost = radius * 0.001f;
            _power = _recharge + sustaincost;

            return _power;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            if (!_gridIsMobile)RefreshDimensions();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");
        }

        private bool RefreshDimensions()
        {
            var width = _widthSlider.Getter(Block);
            var height = _heightSlider.Getter(Block);
            var depth = _depthSlider.Getter(Block);
            var oWidth = _width;
            var oHeight = _height;
            var oDepth = _depth;
            _width = width;
            _height = height;
            _depth = depth;
            var changed = (int)oWidth != (int)width || (int)oHeight != (int)height || (int)oDepth != (int)depth;
            if (!changed) return false;
            UpdateDetection();
            return true;
        }

        private float GetRadius()
        {
            float radius;
            if (_gridIsMobile)
            {
                var p = (float)_shieldShapeMatrix.Scale.Sum / 3 / 2;
                radius = p * p * 4 * (float)Math.PI;
                //Log.Line($"Dims rad:{radius} ran:{_range} c:{_count}");
                return radius;
            }
            var r = (_width + _height + _depth) / 3 / 2;
            var r2 = r * r;
            var r3 = r2 * 4;
            radius = r3 * (float)Math.PI;

            //Log.Line($"Dims rad:{radius} ran:{_range} w:{_width} h:{_height} d:{_depth} c:{_count}");
            return radius;
        }
        #endregion

        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

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

        private void CreateUi()
        {
            Log.Line($"Create UI - c:{_count}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();


            /*Ellipsoid = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block,
                "Ellipsoid",
                "Switch to Ellipsoid",
                false);*/

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "WidthSlider", "Shield Size Width", 35, 300, 300);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "HeightSlider", "Shield Size Height", 35, 300, 300);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "DepthSlider", "Shield Size Depth", 35, 300, 300);

        }
        #endregion

        #region Block Animation
        private void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {_count}");
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
            catch (Exception ex) { Log.Line($"Exception in BlockAnimation: {ex}"); }
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
                //_subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
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
        private Task? _prepareTask = null;
        public void Draw()
        {
            try
            {
                if (!_initialized) return;

                var prevlod = _prevLod;
                var lod = CalculateLod();
                var shield = _shield;
                var impactpos = _worldImpactPosition;
                _worldImpactPosition = Vector3D.NegativeInfinity;

                SetShieldShapeMatrix();

                var entitychanged = _entityChanged;
                _oldGridAabb = Block.CubeGrid.LocalAABB;

                var shapematrix = _shieldShapeMatrix;
                var enemy = IsEnemy(null);
                var renderId = GetRenderId();

                //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{_sphereOnCamera} - RenderID {renderId}");
                var sp = new BoundingSphereD(Entity.GetPosition(), _range);
                var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);
                renderId = 0; //remove when impact local vec is fixed
                if (_prepareTask.HasValue && !_prepareTask.Value.IsComplete) _prepareTask.Value.Wait();
                if (_prepareTask.HasValue && _prepareTask.Value.IsComplete && sphereOnCamera && Block.IsWorking) _icosphere.Draw(renderId);
                if (sphereOnCamera && Block.IsWorking || entitychanged) _prepareTask = MyAPIGateway.Parallel.Start(() => PrepareSphere(entitychanged, enemy, lod, prevlod, impactpos, shapematrix, shield));

                /*var gridmatrix = Cblock.CubeGrid.WorldMatrix;
                var gridaabb = (BoundingBoxD)Cblock.CubeGrid.LocalAABB;

                var c = Color.Red;
                MySimpleObjectDraw.DrawTransparentBox(ref gridmatrix, ref gridaabb, ref c, MySimpleObjectRasterizer.Wireframe, 1, 0.04f); */
            }
            catch (Exception ex) { Log.Line($"Exception in Entity Draw: {ex}"); }
        }

        private void PrepareSphere(bool entitychanged, bool enemy, int lod, int prevlod, Vector3D impactpos, MatrixD shapematrix, IMyEntity shield)
        {
            if (entitychanged || lod != prevlod) _icosphere.CalculateTransform(shapematrix, lod);
            _icosphere.CalculateColor(shapematrix, impactpos, entitychanged, enemy, shield);
        }

        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Block.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + _range) * (x + _range);
            return range;
        }

        private int CalculateLod()
        {
            int lod;

            if (Distance(650)) lod = 3;
            else if (Distance(2250)) lod = 3;
            else if (Distance(4500)) lod = 2;
            else if (Distance(15000)) lod = 1;
            else if (Distance(25000)) lod = 1;
            else lod = 1;

            _prevLod = lod;
            return lod;
        }

        private void SetShieldShapeMatrix()
        {
            //if (Block.CubeGrid.Physics.IsStatic && entitychanged) _shieldShapeMatrix = MatrixD.Rescale(MatrixD.Identity, new Vector3D(_width, _height, _depth));
            //else if (entitychanged) _shieldShapeMatrix = MatrixD.CreateScale(Block.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Block.CubeGrid.PositionComp.LocalAABB.Center); // * Cblock.CubeGrid.WorldMatrix;
            //if (Block.CubeGrid.Physics.IsStatic && entitychanged) _shieldShapeMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
            //else if (entitychanged) _shieldShapeMatrix = MatrixD.CreateScale(Block.CubeGrid.PositionComp.WorldAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Block.CubeGrid.PositionComp.WorldAABB.Center); // * Cblock.CubeGrid.WorldMatrix;

            // Remove when local impact vec is fixed

            if (Block.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
                DetectionMatrix = _shieldShapeMatrix;
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            if (!_entityChanged || Block.CubeGrid.Physics.IsStatic) return;
            var localMatrix = MatrixD.CreateScale(Block.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Block.CubeGrid.PositionComp.LocalAABB.Center);

            if (_entityChanged)
            {
                var entAngularVelocity = Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity);
                const float dt = MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
                var rate = 0d;
                var angVel = Vector3D.Zero;
                if (!entAngularVelocity)
                {
                    angVel = Vector3D.TransformNormal((Vector3D) Block.CubeGrid.Physics.AngularVelocity, Block.CubeGrid.PositionComp.LocalMatrix);
                    rate = angVel.Normalize() * dt;
                }
                var predictedGridWorldMatrix = MatrixD.CreateFromAxisAngle(angVel, rate) * Block.CubeGrid.PositionComp.WorldMatrix;
                predictedGridWorldMatrix.Translation = Block.CubeGrid.PositionComp.WorldMatrix.Translation + Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation) * dt;
                DetectionMatrix = localMatrix * Block.CubeGrid.WorldMatrix;
                _shieldShapeMatrix = localMatrix * predictedGridWorldMatrix;
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            else
            {
                DetectionMatrix = localMatrix * Block.CubeGrid.WorldMatrix;
                _shieldShapeMatrix = DetectionMatrix;
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
        }

        private bool IsEnemy(IMyEntity enemy)
        {
            if (enemy != null)
            {
                if (enemy is IMyCharacter)
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(enemy).IdentityId;
                    var playerrelationship = Block.GetUserRelationToOwner(dude);
                    return playerrelationship != MyRelationsBetweenPlayerAndBlock.Owner && playerrelationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                }
                if (enemy is IMyCubeGrid grid)
                {
                    var owners = grid.BigOwners;
                    if (owners.Count > 0)
                    {
                        var relationship = Block.GetUserRelationToOwner(owners[0]);
                        return relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                    }
                }
            }
            var relations = Block.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            return relations != MyRelationsBetweenPlayerAndBlock.Owner && relations != MyRelationsBetweenPlayerAndBlock.FactionShare;
        }

        private uint GetRenderId()
        {
            var renderId = Block.CubeGrid.Render.GetRenderObjectID();
            if (!_gridIsMobile) renderId = Block.Render.GetRenderObjectID();
            return renderId;
        }

        #endregion

        #region Detect Intersection
        private void UpdateDetection()
        {
            if (_gridIsMobile)
            {
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;
                DetectionMatrix = MatrixD.CreateScale(Block.CubeGrid.PositionComp.LocalAABB.HalfExtents * (float)MathHelper.Sqrt2 + 5f) * MatrixD.CreateTranslation(Block.CubeGrid.PositionComp.LocalAABB.Center) * Block.CubeGrid.WorldMatrix;
                _range = (float)_shieldShapeMatrix.Scale.AbsMax() + 15f;
                //Log.Line($"mobile dims {_range} - {_width} - {_height} - {_depth} - changed: {_entityChanged}");

            }
            else
            {
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                DetectionMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                //Log.Line($"static dims {_range} - {_width} - {_height} - {_depth}");
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

        private bool Intersect(IMyEntity ent, bool impactcheck)
        {
            //In this code I compute a point - to - test by computing  a point on the entity's sphere (or the center of the ellipsoid if it's contained by the sphere)
            //var wTest = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            //So subtract from the radius of the entity(edited)
            //(It's fine if that goes negative -- if it does that will move the test point away from the ellipsoid)
            //var wVol = ent.PositionComp.WorldVolume;
            //var wDir = DetectionMatrix.Translation - wVol.Center;
            //var wLen = wDir.Length();
            //var wTest = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            var contactpoint = ContactPoint(ent);

            if (Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared() <= 1)
            {
                if (impactcheck) _worldImpactPosition = contactpoint;
                Log.Line($"intersect true: {ent} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
                return true;
            }
            //Log.Line($"intersect false: {ent.GetFriendlyName()} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
            return false;
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

            var transformInv = MatrixD.Invert(DetectionMatrix);
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(ContactPoint(breaching), transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            breaching.Physics.ApplyImpulse(breaching.Physics.Mass * 2 * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, ContactPoint(breaching));
            
            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }

        #region Build inside HashSet
        private void InHashBuilder()
        {
            var insphere = new BoundingSphereD(_detectionCenter, _range - InOutSpace);
            var inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (!(inent is IMyCubeGrid) && (!(inent is IMyCharacter) || !Intersect(inent, false))) return;
                lock (InHash)
                {
                    InHash.Add(inent);
                }
            });
        }
        #endregion

        #region Web and dispatch all intersecting entities
        private void WebEntities()
        {
            var websphere = new BoundingSphereD(_detectionCenter, _range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    //if (_shotwebbed) return;
                    if (Intersect(webent, true))
                    {
                        //_shotwebbed = true;
                        _absorb += Shotdmg;
                        Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {webent} in loop {_count}");
                        webent.Close();
                    }
                    return;
                }
                if (webent is IMyCharacter && (_count == 2 || _count == 17 || _count == 32 || _count == 47) && IsEnemy(webent) && Intersect(webent, true))
                {
                    Log.Line($"Enemy Player Intersected");
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == Block.CubeGrid || DestroyGridHash.Contains(grid) || grid == null || !IsEnemy(webent)) return;
                if (Intersect(grid, true))
                {
                    _absorb += (float)ContainmentField(grid, Block.CubeGrid);
                }
                //Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
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
                catch (Exception ex) { Log.Line($"Exception in playerEffects: {ex}"); }
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
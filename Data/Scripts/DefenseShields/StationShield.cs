using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
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

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "StationDefenseShield")]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup

        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;

        private float _power = 0.0001f;
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;
        private float _impactSize;

        private const int PhysicsLod = 3;

        private int _count = -1;
        private int _longLoop = 0;
        private int _explodeCount;
        private int _time;
        private int _playertime;
        private int _prevLod;

        private const bool Debug = false;
        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _buildByVerts = true;
        private bool _buildVertZones = true;
        private bool _buildLines = false;
        private bool _buildTris = false;
        private bool _buildOnce;
        public bool _initialized; 
        private bool _animInit;
        private bool _playerwebbed;
        private bool _gridIsMobile;
        private bool _explode;
        private bool _longLoop10;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _detectionCenter;
        private Vector3D _shieldSize;

        private Vector3D[] _rootVecs = new Vector3D[12];
        private Vector3D[] _physicsOutside;
        private Vector3D[] _physicsInside;

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;
        private MatrixD _detectionMatrixOutside;
        private MatrixD _detectionMatrixInside;
        private MatrixD _mobileMatrix;

        private BoundingBox _oldGridAabb;

        public IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;
        private readonly DataStructures _dataStructures = new DataStructures();
        private readonly StructureBuilder _structureBuilder = new StructureBuilder();
        private DSUtils _dsutil1 = new DSUtils();
        private DSUtils _dsutil2 = new DSUtils();
        private DSUtils _dsutil3 = new DSUtils();



        private MyEntitySubpart _subpartRotor;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;

        private MyResourceSinkComponent _sink;
        private readonly MyDefinitionId _powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash { get; } = new MyConcurrentHashSet<IMyEntity>();
        public MyConcurrentHashSet<IMyEntity> IsOutside { get; } = new MyConcurrentHashSet<IMyEntity>();

        private MyConcurrentList<IMySlimBlock> DmgBlocks { get; } = new MyConcurrentList<IMySlimBlock>();
        private List<IMyCubeGrid> GridIsColliding = new List<IMyCubeGrid>();
        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();
        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } =  new MyConcurrentDictionary<IMyEntity, Vector3D>();

        #endregion

        #region constructors
        private MatrixD DetectionMatrix
        {
            get { return _detectionMatrix; }
            set
            {
                _detectionMatrix = value;
                _detectionMatrixInv = MatrixD.Invert(value);
                _detectionMatrixOutside = value;
                _detectionMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
            }
        }
        #endregion


        public MyResourceSinkComponent Sink { get { return _sink; } set { _sink = value; } }

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; } 
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }

        // temp
        private bool needsMatrixUpdate = false;
        public DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        private bool blocksNeedRefresh = false;
        public const float MIN_SCALE = 15f; // Scale slider min/max
        public const float MAX_SCALE = 300f;
        public float LargestGridLength = 2.5f;
        public static MyModStorageComponent Storage { get; set; }
        private HashSet<ulong> playersToReceive = null;
        // 

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

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10) _longLoop = 0;
                }
                if (_longLoop == 0 && _count == 0) _longLoop10 = true;
                else _longLoop10 = false;
                if (_explode && _explodeCount++ == 14) _explodeCount = 0;
                if (_explodeCount == 0 && _explode) _explode = false;

                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_gridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); 
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation));
                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = entAngularVelocity || entLinVel || _gridChanged;
                    if (_entityChanged || _range <= 0) CreateShieldMatrices();
                }
                if ((_initialized || Block.IsWorking) && _range > 0)
                {
                    if (_count == 0) _enablePhysics = false;
                    if (_enablePhysics == false) QuickWebCheck();
                    if ((_enablePhysics && _entityChanged) || _physicsOutside == null) BuildPhysicsArrays();
                    if (_animInit)
                    {
                        if (_subpartRotor.Closed.Equals(true)) BlockAnimationReset();
                        if (Distance(1000))
                        {
                            var blockCam = new BoundingSphereD(Block.PositionComp.WorldVolume.Center, Block.WorldVolume.Radius);
                            if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam)) MyAPIGateway.Parallel.StartBackground(BlockAnimation);
                        }
                    }
                    _dsutil1.Sw.Start();
                    SyncThreadedEnts();
                    _dsutil1.StopWatchReport("Main loop", 1);
                    if (_playerwebbed && _enablePhysics) PlayerEffects();
                    if (_enablePhysics) MyAPIGateway.Parallel.StartBackground(WebEntities);
                }
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        #region Prep / Misc
        private void BuildPhysicsArrays()
        {
            _physicsOutside = _icosphere.ReturnPhysicsVerts(_detectionMatrixOutside, 3);
            _rootVecs = _icosphere.ReturnPhysicsVerts(_detectionMatrixOutside, 0);
            _physicsInside = _icosphere.ReturnPhysicsVerts(_detectionMatrixInside, 3);
            //_structureBuilder.BuildTriNums(_icosphere.CalculatePhysics(_detectionMatrixOutside, 3), _physicsOutside);
            //if (_buildOnce == false) _structureBuilder.BuildBase(_icosphere.CalculatePhysics(_detectionMatrixOutside, 3), _rootVecs, _physicsOutside, _buildLines, _buildTris, _buildVertZones, _buildByVerts);
            //_buildOnce = true;
        }
        #endregion

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
            //DefenseShieldsBase.Instance.Components.Add(this);
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
                else NeedsUpdate = MyEntityUpdateEnum.NONE;
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

        private void RefreshDimensions()
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

            if (!changed) return;
            CreateShieldMatrices();
            _entityChanged = true;
        }

        private float GetRadius()
        {
            float radius;
            if (_gridIsMobile)
            {
                var p = (float)_shieldShapeMatrix.Scale.Sum / 3 / 2;
                radius = p * p * 4 * (float)Math.PI;
                return radius;
            }
            var r = (_width + _height + _depth) / 3 / 2;
            var r2 = r * r;
            var r3 = r2 * 4;
            radius = r3 * (float)Math.PI;

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

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "WidthSlider", "Shield Size Width", 10, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "HeightSlider", "Shield Size Height", 10, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "DepthSlider", "Shield Size Depth", 10, 300, 100);
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

                _time += 1;
                var temp1 = Matrix.CreateRotationY(0.1f * _time);
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {

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
        private Task? _prepareDraw = null;
        public void Draw()
        {
            try
            {
                if (!_initialized) return;

                SetShieldShapeMatrix();
                var drawShapeChanged = _entityChanged;
                _entityChanged = false;
                var prevlod = _prevLod;
                var lod = CalculateLod();
                var shield = _shield;
                var impactPos = _worldImpactPosition;

                var cubeBlockLocalMatrix = Block.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;

                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                if (impactPos != Vector3D.NegativeInfinity) impactPos = localPosition;
                _worldImpactPosition = Vector3D.NegativeInfinity;

                var impactSize = _impactSize;

                var shapeMatrix = _shieldShapeMatrix;
                var enemy = IsEnemy(null);
                var renderId = GetRenderId();
                //var shapeMatrix = DetectionMatrix;
                //uint renderId = 0;

                var sp = new BoundingSphereD(Entity.GetPosition(), _range);
                var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);
                //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{sphereOnCamera} - RenderID {renderId}");
                if (_prepareDraw.HasValue && !_prepareDraw.Value.IsComplete) _prepareDraw.Value.Wait();
                if (_prepareDraw.HasValue && _prepareDraw.Value.IsComplete && sphereOnCamera && Block.IsWorking) _icosphere.Draw(renderId);
                if (Block.IsWorking || drawShapeChanged) _prepareDraw = MyAPIGateway.Parallel.Start(() => PrepareSphere(drawShapeChanged, sphereOnCamera, enemy, lod, prevlod, impactPos, impactSize, shapeMatrix, shield));

            }
            catch (Exception ex) { Log.Line($"Exception in Entity Draw: {ex}"); }
        }

        private void PrepareSphere(bool drawShapeChanged, bool sphereOnCamera, bool enemy, int lod, int prevlod, Vector3D impactPos, float impactSize, MatrixD shapeMatrix,  IMyEntity shield)
        {
            if (drawShapeChanged || lod != prevlod) _icosphere.CalculateTransform(shapeMatrix, lod);
            _icosphere.ComputeEffects(shapeMatrix, impactPos, impactSize, drawShapeChanged, enemy, sphereOnCamera, shield, prevlod);
        }

        #endregion

        #region Shield Draw Prep
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

        private void CreateShieldMatrices()
        {
            if (_gridIsMobile)
            {
                _shieldGridMatrix = Block.CubeGrid.WorldMatrix;
                CreateMobileShape();
                var mobileMatrix = _mobileMatrix;
                DetectionMatrix = mobileMatrix * _shieldGridMatrix;
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;

                //Log.Line($"mobile dims {_range} - {_width} - {_height} - {_depth} - changed: {_entityChanged}");
            }
            else
            {
                _shieldGridMatrix = Block.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                //Log.Line($"static dims {_range} - {_width} - {_height} - {_depth}");
            }
            _range = (float)_detectionMatrix.Scale.AbsMax() + 15f;
        }

        private void CreateMobileShape()
        {
            if (!_gridChanged) return;

            var gridHalfExtents = Block.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const float ellipsoidAdjust = (float)MathHelper.Sqrt2;
            var buffer = 5f;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            _shieldSize = shieldSize;
            var gridLocalCenter = Block.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize) * MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Block.CubeGrid.PositionComp.LocalVolume.Center;
            _mobileMatrix = mobileMatrix;
        }

        private void SetShieldShapeMatrix()
        {
            if (Block.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Block.LocalMatrix, new Vector3D(_width, _height, _depth));
                //_shieldShapeMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            if (!_entityChanged || Block.CubeGrid.Physics.IsStatic) return;
            CreateMobileShape();
            var mobileMatrix = _mobileMatrix;

            _shieldShapeMatrix = mobileMatrix;
            _shield.SetWorldMatrix(_shieldShapeMatrix);
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
                if (enemy is IMyCubeGrid)
                {
                    var grid = enemy as IMyCubeGrid;
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

        private static bool IsNeutral(IMyEntity neutral)
        {
            if (!(neutral is IMyCubeGrid)) return false;
            var grid = (IMyCubeGrid) neutral;
            var owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            return false;
        }

        private uint GetRenderId()
        {
            //var renderId = _gridIsMobile ? Block.CubeGrid.Render.GetRenderObjectID() : Block.CubeGrid.Render.GetRenderObjectID(); 
            var renderId = Block.CubeGrid.Render.GetRenderObjectID(); 
            return renderId;
        }
        #endregion

        #region Detect Intersection
        private Vector3D Intersect(IMyEntity ent, bool impactcheck, bool enemy)
        {
            if (!(ent is IMyCubeGrid))
            {
                var simpleContactPoint = ContactPointOutside(ent);
                var simpleContactBool = Vector3D.Transform(simpleContactPoint, _detectionMatrixInv).LengthSquared() <= 1;
                if (!simpleContactBool) return Vector3D.NegativeInfinity;
                _worldImpactPosition = simpleContactPoint;
                return simpleContactPoint;
            }
            var grid = ent as IMyCubeGrid;
            var bOriBBoxD = GetWorldObb(grid);
            var contactpoint = ContactPointObb(grid, bOriBBoxD, enemy);

            if (contactpoint != Vector3D.NegativeInfinity)
            {
                _impactSize = grid.Physics.Mass;
                if (impactcheck && !GridIsColliding.Contains(grid))
                {
                    _worldImpactPosition = contactpoint;
                }
                if (impactcheck && !GridIsColliding.Contains(grid)) GridIsColliding.Add(grid);
                if (contactpoint == Vector3D.PositiveInfinity) grid.Physics.LinearVelocity = grid.Physics.LinearVelocity * -0.25f;
                return contactpoint;
            }
            if (GridIsColliding.Contains(grid)) GridIsColliding.Remove(grid);
            return Vector3D.NegativeInfinity;
        }

        private static MyOrientedBoundingBoxD GetWorldObb(IMyEntity ent)
        {
            var localBox = (BoundingBoxD)ent.LocalAABB;
            var worldMatrix = ent.WorldMatrix;
            return new MyOrientedBoundingBoxD(localBox, worldMatrix);
        }

        private Vector3D[] GetCorners(MyOrientedBoundingBoxD obb, int startIndex, int endIndex)
        {
            var indexSize = 8 - startIndex - (7 - endIndex);
            var corners = new Vector3D[indexSize];
            var matrixD = MatrixD.CreateFromQuaternion(obb.Orientation);
            var value = matrixD.Left * obb.HalfExtent.X;
            var value2 = matrixD.Up * obb.HalfExtent.Y;
            var value3 = matrixD.Backward * obb.HalfExtent.Z;
            corners[startIndex++] = obb.Center - value + value2 + value3;
            corners[startIndex++] = obb.Center + value + value2 + value3;
            corners[startIndex++] = obb.Center + value - value2 + value3;
            corners[startIndex++] = obb.Center - value - value2 + value3;
            corners[startIndex++] = obb.Center - value + value2 - value3;
            corners[startIndex++] = obb.Center + value + value2 - value3;
            corners[startIndex++] = obb.Center + value - value2 - value3;
            corners[startIndex] = obb.Center - value - value2 - value3;
            return corners;
        }

        private Vector3D ContactPointOutside(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = _detectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        private Vector3D ContactPointInside(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = _detectionMatrixInside.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }
        #endregion

        private Vector3D ContactPointObb(IMyEntity breaching, MyOrientedBoundingBoxD bOriBBoxD, bool enemy)
        {
            // Well checking the measly 3 faces of the OBB against the ellipsoid is going to be the fastest.
            // compute the 8 corners of the OBB in world space
            // transform those corners by the inverse of the ellipsoid matrix
            // for each face collide it with the unit sphere.
            // Colliding a face with a sphere is:
            // find the nearest point on the face to the sphere's center
            // if distance is greater than radius reject
            // Finding the nearest point on the face is:
            // find the face normal (cross product)
            // compose a plane with that normal and one point of the face
            // project test point onto plane
            // solve the equation (pt1-pt0)*s + (pt2-pt0)*t + pt0 = projected point
            // clamp s and t to the domain 0-1
            // closest point is (pt1-pt0)*s + (pt2-pt0)*t + pt0

            var collision = new Vector3D(Vector3D.NegativeInfinity);

            var bLocalAabb = breaching.PositionComp.LocalAABB;
            var bWorldAabb = breaching.PositionComp.WorldAABB;
            var bWorldCenter = bWorldAabb.Center;

            var lodScaler = (int)Math.Pow(2, PhysicsLod);
            var gridScaler = (float)(((_detectionMatrix.Scale.X + _detectionMatrix.Scale.Y + _detectionMatrix.Scale.Z) / 3 / lodScaler) * 1.33) / bLocalAabb.Extents.Min();
            var faceAndInside = new int[5];
            var boxedTriangles = new List<Vector3D>();
            var center = new Vector3D[3];
            var inside = false;

            //if (_count == 0) Log.Line($"gridscaler is: {gridScaler} <1 = large - >1 = small");
            if (gridScaler > 1)
            {
                var rangedVert3 = VertRangeFullCheck(_physicsOutside, bWorldCenter);
                var closestFace0 = _dataStructures.p3VertTris[rangedVert3[0]];
                var closestFace1 = _dataStructures.p3VertTris[rangedVert3[1]];
                var closestFace2 = _dataStructures.p3VertTris[rangedVert3[2]];
                //var faceAndInside = GetAllClosestInOutTri(_physicsOutside, _physicsInside, closestFace0, closestFace1, closestFace2, bWorldCenter, triNums[rangedVert3[0]], triNums[rangedVert3[1]], triNums[rangedVert3[2]]);
                faceAndInside = GetAllClosestInOutTri(_physicsOutside, _physicsInside, closestFace0, closestFace1, closestFace2, bWorldCenter);
                inside = faceAndInside[1] == 1;
                int[] closestFace;
                switch (faceAndInside[0])
                {
                    case 0:
                        closestFace = closestFace0;
                        break;
                    case 1:
                        closestFace = closestFace1;
                        break;
                    default:
                        closestFace = closestFace2;
                        break;
                }
                //var checkBackupFace1 = CheckFirstFace(closestFace0, rangedVert3[1]);
                //var checkBackupFace2 = CheckFirstFace(closestFace0, rangedVert3[2]);
                //var boxedTriangles = IntersectSmallBoxFaces(closestFace0, closestFace1, closestFace2, _physicsOutside, bWorldAabb, checkBackupFace1, checkBackupFace2);
                boxedTriangles = IntersectSmallBox(closestFace, _physicsOutside, bWorldAabb);

                if (inside)
                {
                    center = new Vector3D[3] { _physicsOutside[faceAndInside[2]], _physicsOutside[faceAndInside[3]], _physicsOutside[faceAndInside[4]] };
                }
                if (Debug)
                {
                    //DrawNums(_physicsOutside,zone, Color.AntiqueWhite);
                    DsDebugDraw.DrawLineToNum(_physicsOutside, rangedVert3[0], bWorldCenter, Color.Red);
                    DsDebugDraw.DrawLineToNum(_physicsOutside, rangedVert3[1], bWorldCenter, Color.Green);
                    DsDebugDraw.DrawLineToNum(_physicsOutside, rangedVert3[2], bWorldCenter, Color.Gold);

                    int[] closestLineFace;
                    switch (faceAndInside[0])
                    {
                        case 0:
                            closestLineFace = _dataStructures.p3VertLines[rangedVert3[0]];
                            break;
                        case 1:
                            closestLineFace = _dataStructures.p3VertLines[rangedVert3[1]];
                            break;
                        default:
                            closestLineFace = _dataStructures.p3VertLines[rangedVert3[2]];
                            break;
                    }

                    var c1 = Color.Black;
                    var c2 = Color.Black;
                    //if (checkBackupFace1) c1 = Color.Green;
                    //if (checkBackupFace2) c2 = Color.Gold;
                    c1 = Color.Green;
                    c2 = Color.Gold;

                    DsDebugDraw.DrawLineNums(_physicsOutside, closestLineFace, Color.Red);
                    //DrawLineNums(_physicsOutside, closestLineFace1, c1);
                    //DrawLineNums(_physicsOutside, closestLineFace2, c2);

                    DsDebugDraw.DrawTriVertList(boxedTriangles);

                    //DrawLineToNum(_physicsOutside, rootVerts, bWorldCenter, Color.HotPink);
                    //DrawLineToNum(_physicsOutside, rootVerts[1], bWorldCenter, Color.Green);
                    //DrawLineToNum(_physicsOutside, rootVerts[2], bWorldCenter, Color.Gold);
                }

                if (boxedTriangles.Count > 0 && !inside)
                {
                    var locCenterSphere = DSUtils.CreateFromPointsList(boxedTriangles);
                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
                    _worldImpactPosition = collision;
                }
            }
            else 
            {
                var tSphere = new BoundingSphereD(bWorldCenter, bWorldAabb.HalfExtents.Max());
                //if (_count == 0) DSUtils.Sw.Start();

                var collection = ContainPointObb(_physicsOutside, bOriBBoxD, tSphere);
                if (collection.Count == 0) return Vector3D.NegativeInfinity;
                
                var collisionCenter = DSUtils.CreateFromPointsList(collection).Center;

                if (collection.Count > 0)
                {
                    if (collisionCenter != Vector3D.Zero) collision = collisionCenter;
                }
                //if (_count == 0) DSUtils.StopWatchReport("simple test", -1);

                if (collision != Vector3D.NegativeInfinity)
                {
                    if (_count == 0) Log.Line($"Collision");
                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, collisionCenter, .9);
                    _worldImpactPosition = collision;
                }
            }
            var cSphere = BoundingSphereD.CreateFromPoints(center);
            var grid = breaching as IMyCubeGrid;
            if (grid == null) return collision;
            try
            {
                if (!enemy && !inside && !IsOutside.Contains(grid))
                {
                    IsOutside.Add(grid);
                    collision = Vector3D.PositiveInfinity;
                }
                if (!enemy && IsOutside.Contains(grid) && inside)
                {
                    IsOutside.Remove(grid);
                    return Vector3D.NegativeInfinity;
                }
                if (_count == 0 && IsOutside.Contains(grid)) Log.Line($"outside - {grid.Name} {IsOutside.Count}");

                if (collision != Vector3D.NegativeInfinity && enemy)
                {
                    lock (Eject) Eject.Add(grid, cSphere.Center);
                    var deathPointsOut = new Vector3D[3] { _physicsOutside[faceAndInside[2]], _physicsOutside[faceAndInside[3]], _physicsOutside[faceAndInside[4]]};
                    var deathSphereOut = BoundingSphereD.CreateFromPoints(deathPointsOut);

                    var getBlocks = grid.GetBlocksInsideSphere(ref deathSphereOut);
                    lock (DmgBlocks)
                    {
                        for (int i = 0; i < 25 && i < getBlocks.Count; i++)
                        {
                            var block = getBlocks[i];
                            DmgBlocks.Add(block);
                            if (i == 25) break;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}"); }
            return collision;
        }

        private static bool CheckFirstFace(int[] firstFace, int secondVertNum)
        {
            for (int i = 0; i < firstFace.Length; i++)
            {
                if (firstFace[i] == secondVertNum) return false;
            }
            return true;
        }

        private static List<Vector3D> IntersectSmallBox(int[] closestFace, Vector3D[] physicsVerts, BoundingBoxD bWorldAabb)
        {
            var boxedTriangles = new List<Vector3D>();
            for (int i = 0, j = 0; i < closestFace.Length; i += 3, j++)
            {
                var v0 = physicsVerts[closestFace[i]];
                var v1 = physicsVerts[closestFace[i + 1]];
                var v2 = physicsVerts[closestFace[i + 2]];
                var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                if (!test1) continue;
                boxedTriangles.Add(v0);
                boxedTriangles.Add(v1);
                boxedTriangles.Add(v2);
            }
            return boxedTriangles;
        }


        private static List<Vector3D> IntersectSmallBoxFaces(int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D[] physicsVerts, BoundingBoxD bWorldAabb, bool secondFace, bool thirdFace)
        {
            var boxedTriangles = new List<Vector3D>();
            for (int i = 0, j = 0; i < closestFace0.Length; i += 3, j++)
            {
                var v0 = physicsVerts[closestFace0[i]];
                var v1 = physicsVerts[closestFace0[i + 1]];
                var v2 = physicsVerts[closestFace0[i + 2]];
                var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                if (!test1) continue;
                boxedTriangles.Add(v0);
                boxedTriangles.Add(v1);
                boxedTriangles.Add(v2);
            }
            if (boxedTriangles.Count == 0 && secondFace)
            {
                for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
                {
                    var v0 = physicsVerts[closestFace1[i]];
                    var v1 = physicsVerts[closestFace1[i + 1]];
                    var v2 = physicsVerts[closestFace1[i + 2]];

                    var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                    if (!test1) continue;
                    boxedTriangles.Add(v0);
                    boxedTriangles.Add(v1);
                    boxedTriangles.Add(v2);
                }
            }
            if (boxedTriangles.Count == 0 && thirdFace)
            {
                for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
                {
                    var v0 = physicsVerts[closestFace2[i]];
                    var v1 = physicsVerts[closestFace2[i + 1]];
                    var v2 = physicsVerts[closestFace2[i + 2]];

                    var test1 = bWorldAabb.IntersectsTriangle(v0, v1, v2);

                    if (!test1) continue;
                    boxedTriangles.Add(v0);
                    boxedTriangles.Add(v1);
                    boxedTriangles.Add(v2);
                }
            }
            return boxedTriangles;
        }

        private static bool GetClosestInOutTri(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace, Vector3D bWorldCenter)
        {
            var closestTri1 = -1;
            double triDist1 = 9999999999999999999;

            for (int i = 0; i < closestFace.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace[i]];
                var ov1 = physicsOutside[closestFace[i + 1]];
                var ov2 = physicsOutside[closestFace[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                }
            }

            var iv0 = physicsInside[closestFace[closestTri1]];
            var iv1 = physicsInside[closestFace[closestTri1 + 1]];
            var iv2 = physicsInside[closestFace[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);
            return triDist1 > idistTri.GetSquared();
        }

        private static int[] GetAllClosestInOutTriBackup(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter, int[] triNums0, int[] triNums1, int[] triNums2)
        {
            var closestTri1 = -1;
            var closestFace = -1;
            var status =  new int[2];
            double triDist1 = 9999999999999999999;

            for (int i = 0; i < closestFace0.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace0[i]];
                var ov1 = physicsOutside[closestFace0[i + 1]];
                var ov2 = physicsOutside[closestFace0[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 0;
                }
            }
            for (int i = 0, j = 0; i < closestFace1.Length; i += 3, j++)
            {
                var skip = false;
                for (int f = 0; f < triNums0.Length; f++)
                {
                    if (triNums0[f] != triNums1[j]) continue;
                    skip = true;
                    break;
                }

                if (skip) continue;
                var ov0 = physicsOutside[closestFace1[i]];
                var ov1 = physicsOutside[closestFace1[i + 1]];
                var ov2 = physicsOutside[closestFace1[i + 2]];

                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 1;
                }
            }

            for (int i = 0, j  = 0; i < closestFace2.Length; i += 3, j++)
            {
                var skip = false;
                for (int f = 0; f < triNums0.Length; f++)
                {
                    if (triNums0[f] != triNums2[j]) continue;
                    skip = true;
                    break;
                }
                if (skip) continue;
                for (int f = 0; f < triNums1.Length; f++)
                {
                    if (triNums1[f] != triNums2[j]) continue;
                    skip = true;
                    break;
                }
                if (skip) continue;

                var ov0 = physicsOutside[closestFace2[i]];
                var ov1 = physicsOutside[closestFace2[i + 1]];
                var ov2 = physicsOutside[closestFace2[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 2;
                }
            }

            int[] face;
            switch (closestFace)
            {
                case 0:
                    face = closestFace0;
                    break;
                case 1:
                    face = closestFace1;
                    break;
                default:
                    face = closestFace2;
                    break;
            }
            
            var iv0 = physicsInside[face[closestTri1]];
            var iv1 = physicsInside[face[closestTri1 + 1]];
            var iv2 = physicsInside[face[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);

            status[0] = closestFace;
            if (triDist1 > idistTri.GetSquared()) status[1] = 1;
            return status;
        }

        private static int[] GetAllClosestInOutTri(Vector3D[] physicsOutside, Vector3D[] physicsInside, int[] closestFace0, int[] closestFace1, int[] closestFace2, Vector3D bWorldCenter)
        {
            var closestTri1 = -1;
            var closestFace = -1;
            var status = new int[5];

            double triDist1 = 9999999999999999999;

            for (int i = 0; i < closestFace0.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace0[i]];
                var ov1 = physicsOutside[closestFace0[i + 1]];
                var ov2 = physicsOutside[closestFace0[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 0;
                }
            }

            for (int i = 0; i < closestFace1.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace1[i]];
                var ov1 = physicsOutside[closestFace1[i + 1]];
                var ov2 = physicsOutside[closestFace1[i + 2]];

                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 1;
                }
            }

            for (int i = 0; i < closestFace2.Length; i += 3)
            {
                var ov0 = physicsOutside[closestFace2[i]];
                var ov1 = physicsOutside[closestFace2[i + 1]];
                var ov2 = physicsOutside[closestFace2[i + 2]];
                var otri = new Triangle3d(ov0, ov1, ov2);
                var odistTri = new DistPoint3Triangle3(bWorldCenter, otri);

                var test = odistTri.GetSquared();
                if (test < triDist1)
                {
                    triDist1 = test;
                    closestTri1 = i;
                    closestFace = 2;
                }
            }

            int[] face;
            switch (closestFace)
            {
                case 0:
                    face = closestFace0;
                    break;
                case 1:
                    face = closestFace1;
                    break;
                default:
                    face = closestFace2;
                    break;
            }

            status[2] = face[closestTri1];
            status[3] = face[closestTri1 + 1];
            status[4] = face[closestTri1 + 2];

            var iv0 = physicsInside[face[closestTri1]];
            var iv1 = physicsInside[face[closestTri1 + 1]];
            var iv2 = physicsInside[face[closestTri1 + 2]];

            var itri = new Triangle3d(iv0, iv1, iv2);
            var idistTri = new DistPoint3Triangle3(bWorldCenter, itri);

            status[0] = closestFace;
            if (triDist1 > idistTri.GetSquared()) status[1] = 1;

            return status;
        }

        private static List<Vector3D> ContainPointObb(Vector3D[] physicsVerts, MyOrientedBoundingBoxD bOriBBoxD, BoundingSphereD tSphere)
        {
            var containedPoints = new List<Vector3D>();
            foreach (var vert in physicsVerts)
            {
                var vec = vert;
                if (tSphere.Contains(vec) == ContainmentType.Disjoint) continue;
                if (bOriBBoxD.Contains(ref vec))
                {
                    containedPoints.Add(vec);
                }
            }
            return containedPoints;
        }

        private static int[] VertRangeFullCheck(Vector3D[] physicsVerts, Vector3D bWorldCenter)
        {
            double minValue1 = 9999999999999999999;
            double minValue2 = 9999999999999999999;
            double minValue3 = 9999999999999999999;


            var minNum1 = -2;
            var minNum2 = -2;
            var minNum3 = -2;


            for (int p = 0; p < physicsVerts.Length; p++)
            {
                var vert = physicsVerts[p];
                var range = vert - bWorldCenter;
                var test = (range.X * range.X + range.Y * range.Y + range.Z * range.Z);
                //var test = Vector3D.DistanceSquared(vert, bWorldCenter);
                if (test < minValue3)
                {
                    if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = minValue1;
                        minNum2 = minNum1;
                        minValue1 = test;
                        minNum1 = p;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        minNum3 = minNum2;
                        minValue2 = test;
                        minNum2 = p;
                    }
                    else
                    {
                        minValue3 = test;
                        minNum3 = p;
                    }
                }
            }
            return new [] { minNum1, minNum2, minNum3};
        }

        private double ContainmentField(IMyEntity breaching, IMyEntity field, Vector3D intersect)
        {
            //var direction = Vector3D.Normalize(grid.Center() - grid.Center);
            //Vector3D velocity = grid.Physics.LinearVelocity;
            //if (Vector3D.IsZero(velocity)) velocity += direction;
            //
            //Vector3D forceDir = Vector3D.Reflect(Vector3D.Normalize(velocity), direction);
            //grid.Physics.SetSpeeds(velocity * forceDir, grid.Physics.AngularVelocity);
            //var dist = Vector3D.Distance(grid.GetPosition(), websphere.Center);
            //
            //var d = grid.Physics.CenterOfMass -ContainmentField thingRepellingYou;
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
            //var contactPoint = ContactPoint(breaching);
            var contactPoint = intersect;

            //var transformInv = MatrixD.Invert(DetectionMatrix);
            //var transformInv = _detectionMatrixInv;
            //var normalMat = MatrixD.Transpose(transformInv);
            //var localNormal = Vector3D.Transform(contactPoint, transformInv);
            //var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            var bmass = -breaching.Physics.Mass;
            //var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
            //var expelForce = (bmass); /// Math.Pow(cpDist, 2);
            //if (expelForce < -9999000000f || bmass >= -67f) expelForce = -9999000000f;
            var expelForce = (bmass);// / (float)Math.Pow(cpDist, 4);

            var worldPosition = breaching.WorldMatrix.Translation;
            var worldDirection = contactPoint - worldPosition;

            if (_gridIsMobile)
            {
                Block.CubeGrid.Physics.ApplyImpulse(Vector3D.Negate(worldDirection) * (expelForce / Block.CubeGrid.Physics.Mass), contactPoint);
                breaching.Physics.ApplyImpulse(worldDirection * (expelForce), contactPoint);
            }
            else breaching.Physics.ApplyImpulse(worldDirection * (expelForce), contactPoint);
            //breaching.Physics.ApplyImpulse(breaching.Physics.Mass * -0.050f * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, contactPoint);
            //Log.Line($"cpDist:{cpDist} pow:{expelForce} bmass:{bmass} adjbmass{bmass / 50}");

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }   

        private void SyncThreadedEnts()
        {
            try
            {
                if (Eject.Count > 0)
                    lock (Eject)
                    {
                        foreach (var e in Eject)
                        {
                            e.Key.SetPosition(Vector3D.Lerp(e.Key.GetPosition(), e.Value, 0.1d));
                        }
                        Eject.Clear();
                    }

                if (DmgBlocks.Count > 0)
                {
                    lock (DmgBlocks)
                    {
                        for (int i = 0; i < 25 && i < DmgBlocks.Count; i++)
                        {
                            var block = DmgBlocks[i];
                            block.DoDamage(5000f, MyDamageType.Fire, true, null, Block.EntityId);
                            if (i == 25) break;
                        }
                        DmgBlocks.Clear();
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in DamgeGrids: {ex}"); }
        }

        private void GridKillField()
        {
            try
            {
                var bigkillSphere = new BoundingSphereD(_detectionCenter, _range);
                var killList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref bigkillSphere);
                if (killList.Count == 0) return;
                MyAPIGateway.Parallel.ForEach(killList, killent =>
                {
                    var grid = killent as IMyCubeGrid;
                    if (grid == null || grid == Block.CubeGrid || !IsEnemy(killent) || Intersect(killent, false, false) == Vector3D.NegativeInfinity) return;

                    var contactPoint = ContactPointOutside(killent);
                    var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
                    //var worldPosition = killent.WorldVolume.Center;
                    //var worldDirection = contactPoint - worldPosition;
                    //var worldDirection = worldPosition - contactPoint;


                    var killSphere = new BoundingSphereD(contactPoint, 5f);
                    if (cpDist > 0.95f && _explode == false && _explodeCount == 0)
                    {
                        //Log.Line($"EXPLOSION! - dist:{cpDist}");
                        _explode = true;
                        MyVisualScriptLogicProvider.CreateExplosion(killSphere.Center, (float)killSphere.Radius, 20000);
                    }

                    if (!(cpDist <= 0.99)) return;
                    //Log.Line($"DoDamage - dist:{cpDist}");
                    var killBlocks = grid.GetBlocksInsideSphere(ref killSphere);
                    MyAPIGateway.Parallel.ForEach(killBlocks, block =>
                    {
                        block.DoDamage(99999f, MyDamageType.Fire, true, null, Block.EntityId);
                    });
                });

            }
            catch (Exception ex) { Log.Line($"Exception in GridKillField: {ex}"); }
        }

        #region Build inside HashSet
        private void InHashBuilder()
        {
            /*
            var insphere = new BoundingSphereD(_detectionCenter, _range - InOutSpace);
            var inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (!(inent is IMyCubeGrid) && (!(inent is IMyCharacter) || Intersect(inent, false) == Vector3D.NegativeInfinity)) return;
                lock (InHash)
                {
                    if (inent is IMyCubeGrid && IsEnemy(inent)) return;
                    InHash.Add(inent);
                }
            });
            */
        }
        #endregion

        #region Web and dispatch all intersecting entities
        private void QuickWebCheck()
        {
            //Log.Line($"begin quickweb {_enablePhysics} range {_range} - {_detectionCenter} - {Block.WorldVolume.Center}");
            var qWebsphere = new BoundingSphereD(_detectionCenter, _range);
            var qWebList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref qWebsphere);
            foreach (var webent in qWebList)
            {
                if (webent == null || webent is IMyFloatingObject || webent is IMyEngineerToolBase || webent == Block.CubeGrid || webent == _shield || webent is IMyCharacter) continue;
                if (Block.CubeGrid.Physics.IsStatic && webent is IMyVoxelBase) continue;
                //Log.Line($"{webent.DisplayName}");
                _enablePhysics = true;
                //if (_count == 0) Log.Line($"changed physics {_enablePhysics}");
                return;
            }
        }

        private void WebEntities()
        {
            if (_count == 0) _dsutil3.Sw.Start();
            var websphere = new BoundingSphereD(_detectionCenter, _range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (Intersect(webent, true, true) != Vector3D.NegativeInfinity)
                    {
                        _absorb += Shotdmg;
                        Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {webent} in loop {_count}");
                        MyVisualScriptLogicProvider.CreateExplosion(webent.GetPosition(), 0, 0);
                        webent.Close();
                    }
                    return;
                }
                if (webent is IMyCharacter && (_count == 2 || _count == 17 || _count == 32 || _count == 47) && IsEnemy(webent) && Intersect(webent, true, true) != Vector3D.NegativeInfinity)
                {
                    Log.Line($"Enemy Player Intersected");
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid != null && grid != Block.CubeGrid && IsEnemy(webent))
                {
                    var intersect = Intersect(grid, true, true);
                    if (intersect != Vector3D.NegativeInfinity && intersect != Vector3D.PositiveInfinity)
                    {
                        ContainmentField(grid, Block.CubeGrid, intersect);
                    }
                    return;
                }
                if (grid != null && grid != Block.CubeGrid && IsNeutral(webent))
                {
                    var intersect = Intersect(grid, true, false);
                    if (intersect != Vector3D.NegativeInfinity && intersect != Vector3D.PositiveInfinity)
                    {
                        ContainmentField(grid, Block.CubeGrid, intersect);
                    }
                }

                //Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
            if (_count == 0) _dsutil3.StopWatchReport("collision", -1);
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
                DefenseShieldsBase.Instance.Components.RemoveAt(DefenseShieldsBase.Instance.Components.IndexOf(this));
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

        #region Settings
        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            ShieldVisable = newSettings.Enabled;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
        }

        public void SaveSettings()
        {
            if (DefenseShields.Storage == null)
                DefenseShields.Storage = new MyModStorageComponent();

            DefenseShields.Storage[DefenseShieldsBase.Instance.SETTINGS_GUID] = MyAPIGateway.Utilities.SerializeToXML(Settings);

            Log.Line("SaveSettings()");
        }

        private bool LoadSettings()
        {
            Log.Line("LoadSettings");

            if (DefenseShields.Storage == null)
                return false;

            string rawData;
            bool loadedSomething = false;

            if (DefenseShields.Storage.TryGetValue(DefenseShieldsBase.Instance.SETTINGS_GUID, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }

                Log.Line($"  Loaded settings:\n{Settings.ToString()}");
            }

            return loadedSomething;
        }

        public bool ShieldVisable
        {
            get { return Settings.Enabled; }
            set
            {
                Settings.Enabled = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public float Width
        {
            get { return Settings.Width; }
            set
            {
                Settings.Width = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Height
        {
            get { return Settings.Height; }
            set
            {
                Settings.Height = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Depth
        {
            get { return Settings.Depth; }
            set
            {
                Settings.Depth = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        private void RefreshControls(bool refreshRemoveButton = false, bool refeshCustomInfo = false)
        {
        }

        public void UseThisShip_Receiver(bool fix)
        {
            Log.Line($"UseThisShip_Receiver({fix})");

            //UseThisShip_Internal(fix);
        }
        #endregion
    }
}
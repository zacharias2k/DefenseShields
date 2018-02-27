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
using VRageRender.Utils;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "StationDefenseShield")]
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
        private float _impactSize;

        private const int PhysicsLod = 3;

        private int _count = -1;
        private int _explodeCount;
        private int _time;
        private int _playertime;
        private int _prevLod;

        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _buildByVerts = true;
        private bool _buildVertZones = false;
        private bool _buildLines = false;
        private bool _buildTris = true;
        private bool _buildOnce;
        private bool _initialized;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _gridIsMobile;
        private bool _explode;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _detectionCenter;
        private Vector3D _shieldSize;

        private Vector3D[] _rootVecs = new Vector3D[12];
        private Vector3D[] _physicsVerts;

        private double[] _shieldRanged;

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;
        private MatrixD _mobileMatrix;

        private BoundingBox _oldGridAabb;

        private IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;
        private readonly DataStructures _dataStructures = new DataStructures();
        private readonly StructureBuilder _structureBuilder = new StructureBuilder();

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
        private MyConcurrentHashSet<IMySlimBlock> DmgBlocks { get; } = new MyConcurrentHashSet<IMySlimBlock>();

        private List<IMyCubeGrid> GridIsColliding = new List<IMyCubeGrid>();
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

        #region Prep / Misc
        private void BuildPhysicsArrays()
        {

            _physicsVerts = _icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsLod);
            _rootVecs = _icosphere.ReturnPhysicsVerts(DetectionMatrix, 0);
            //DrawNums(_dataStructures.p0RootZones[0]);

            if (_buildOnce == false) _structureBuilder.BuildBase(_icosphere.CalculatePhysics(DetectionMatrix, 0), _rootVecs, _physicsVerts, _buildLines, _buildTris, _buildVertZones, _buildByVerts);
            _buildOnce = true;
        }
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

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_count++ == 59) _count = 0;
                if (_explode && _explodeCount++ == 14) _explodeCount = 0;
                if (_explodeCount == 0 && _explode) _explode = false;

                if (_count <= 0)
                {
                    if (!_initialized)
                    {
                        _count = -1;
                        //InHashBuilder();
                        return;
                    }
                    //InHashBuilder();
                }
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && _initialized && Block.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                //if (_count == 0) _enablePhysics = false;
                //if (_enablePhysics == false) QuickWebCheck();
                _enablePhysics = true;
                if (_gridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); 
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation));
                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = entAngularVelocity || entLinVel || _gridChanged;
                    //if (_entityChanged || _gridChanged) Log.Line($"Entity Change Loop ec:{_entityChanged} gc:{_gridChanged} vel:{entLinVel} avel:{entAngularVelocity}");
                    if (_entityChanged || _range <= 0) CreateShieldMatrices();
                }
                if (_enablePhysics && _initialized && Block.IsWorking)
                {
                    BuildPhysicsArrays();
                }
                if (!_initialized || !Block.IsWorking) return;
                //DrawRootVerts();
                DamageGrids();
                //if (_enablePhysics) MyAPIGateway.Parallel.StartBackground(WebEntities);
                if (_enablePhysics) WebEntities();
                if (_playerwebbed && _enablePhysics) PlayerEffects();
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
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
        private Task? _prepareDraw = null;
        public void Draw()
        {
            try
            {
                if (!_initialized) return;

                SetShieldShapeMatrix();
                var drawShapeChanged = _entityChanged;

                var prevlod = _prevLod;
                var lod = CalculateLod();
                var shield = _shield;
                var impactPos = _worldImpactPosition;

                var referenceWorldPosition = _shieldGridMatrix.Translation; 
                var worldDirection = impactPos - referenceWorldPosition; 
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(_shieldGridMatrix));
                if (impactPos != Vector3D.NegativeInfinity) impactPos = localPosition;
                //if (impactpos != Vector3D.NegativeInfinity) impactpos = Vector3D.Transform(impactpos, Block.CubeGrid.WorldMatrixInvScaled);
                _worldImpactPosition = Vector3D.NegativeInfinity;

                var impactSize = _impactSize;

                //var shapeMatrix = _shieldShapeMatrix;
                var enemy = IsEnemy(null);
                //var renderId = GetRenderId();
                var shapeMatrix = DetectionMatrix;
                uint renderId = 0;

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
            _icosphere.CalculateColor(shapeMatrix, impactPos, impactSize, drawShapeChanged, enemy, sphereOnCamera, shield);
        }

        public void DrawBox(MyOrientedBoundingBoxD obb, Color color, bool matrix)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromTransformScale(obb.Orientation, obb.Center, Vector3D.One);
            if (matrix) wm = wm * _shieldGridMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, MySimpleObjectRasterizer.Solid, 1, 1f, null, null, true);
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

            if (Distance(650)) lod = 0;
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
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;

                //Log.Line($"mobile dims {_range} - {_width} - {_height} - {_depth} - changed: {_entityChanged}");
            }
            else
            {
                _shieldGridMatrix = Block.WorldMatrix;
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                //Log.Line($"static dims {_range} - {_width} - {_height} - {_depth}");
            }
        }

        private void CreateMobileShape()
        {
            if (!_gridChanged) return;

            var gridHalfExtents = Block.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const float ellipsoidAdjust = (float)MathHelper.Sqrt2;
            var buffer = 5f;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            _shieldSize = shieldSize;
            //var gridLocalCenter = Block.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize); //* MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Block.CubeGrid.PositionComp.LocalVolume.Center;
            _mobileMatrix = mobileMatrix;
        }

        private void SetShieldShapeMatrix()
        {
            if (Block.CubeGrid.Physics.IsStatic)
            {
                //_shieldShapeMatrix = MatrixD.Rescale(Block.LocalMatrix, new Vector3D(_width, _height, _depth));
                _shieldShapeMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
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

        private uint GetRenderId()
        {
            var renderId = _gridIsMobile ? Block.CubeGrid.Render.GetRenderObjectID() : Block.CubeGrid.Render.GetRenderObjectID();
            return renderId;
        }
        #endregion

        #region Detect Intersection
        private Vector3D ContactPointObb(IMyEntity breaching)
        {
            var collision = new Vector3D(Vector3D.NegativeInfinity);
            var locCenterSphere = new BoundingSphereD();

            var dWorldAabb =  Block.CubeGrid.WorldAABB;

            var bLocalAabb = breaching.PositionComp.LocalAABB;
            var bWorldAabb = breaching.PositionComp.WorldAABB;
            var bWorldCenter = bWorldAabb.Center;


            var lodScaler = (int)Math.Pow(2, PhysicsLod);
            var gridScaler = (float)(((DetectionMatrix.Scale.X + DetectionMatrix.Scale.Y + DetectionMatrix.Scale.Z) / 3 / lodScaler) * 1.33) / bLocalAabb.Extents.Min();
            var bLength = bLocalAabb.Size.Max() / 2 + 2;
            var bLengthSqr = bLength * bLength;

            var reSized = bLocalAabb.Extents.Min() * gridScaler;
            var reSizedSqr = reSized * reSized;

            if (gridScaler > 1)
            {
                var rangedVert = VertRangeCheckClosest(_physicsVerts, _rootVecs, _dataStructures.p0RootZones, bWorldCenter);
                var closestFace = _dataStructures.p0VertTris[rangedVert];
                var boxedTriangles = IntersectSmallBox(closestFace, _physicsVerts, bWorldAabb);
                //GetZonesContaingNum(rangedVert, 1, true);
                GetTriDistance(_physicsVerts, _dataStructures.p3VertTris, bWorldCenter, _count);
                /*
                var matchedZones = GetZonesContaingNum(rangedVert, 0, true);
                var zNum = 0;
                foreach (var zone in matchedZones)
                {
                    if (zone) DrawNums(_dataStructures.p0RootZones[zNum]);
                    zNum++;
                }
                */
                //DrawLineNums(_dataStructures.p0RootZones[rangedVert]); // testing
                //DrawTriVertList(boxedTriangles); // testing
                //DrawNums(closestFace, Color.Black);
                if (boxedTriangles.Count > 0)
                {
                    var pointCollectionScaled = new Vector3D[boxedTriangles.Count];
                    boxedTriangles.CopyTo(pointCollectionScaled);
                    locCenterSphere = BoundingSphereD.CreateFromPoints(pointCollectionScaled);

                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
                    _worldImpactPosition = collision;
                    //DrawVertCollection(collision, locCenterSphere.Radius / 2, Color.Blue); // testing
                }
            }
            /*
            else 
            {

                foreach (var magic in _rootVecs)
                {
                    //Log.Line($"magic: {magic}");
                    DrawVertCollection(magic, 5, Color.Red);
                }

                //var rangedVectors = IntersectRangeCheck(shieldTris, bWorldCenter, bLengthSqr);
                //var boxedVectors = IntersectVecBox(rangedVectors, bWorldAabb);
                var rangedVert = VertRangeCheckClosest(_physicsVerts, bWorldCenter);
                var closestFace = _dataStructures.p3VertTris[rangedVert];
                IntersectSmallBox(closestFace, _physicsVerts, bWorldAabb);

                /*
                var obbLines = IntersectLineObb(rangedVerts, bLocalAabb, breaching.PositionComp.WorldMatrix);
                if (obbLines)
                {
                    collision = Vector3D.Lerp(_gridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, rangedVerts[0].To, .9);
                    _worldImpactPosition = collision;
                    DrawVertCollection(collision, locCenterSphere.Radius, Color.Blue); // testing
                }
                if (rangedVerts.Length > 0) Log.Line($"total triangles: {_shieldTris.Length / 3} - Ranged {rangedVerts.Length / 3} - Obb Collision {obbLines}");
            }
            */
            //var grid = breaching as IMyCubeGrid;
            //if (grid == null) return collision;

            /*
            try
            {
                var getBlocks = grid.GetBlocksInsideSphere(ref locCenterSphere);
                lock (DmgBlocks)
                {
                    foreach (var block in getBlocks)
                    {
                        DmgBlocks.Add(block);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}"); }
            */
            return collision;
        }

        private static void GetTriDistance(Vector3D[] physicsVerts, int[][] p3VertTris, Vector3D bWorldCenter, int count)
        {
            if (count == 0) DSUtils.Sw.Start();
            //var point = new Vector3d(bWorldCenter.X, bWorldCenter.Y, bWorldCenter.Z);
            var point = bWorldCenter;

            foreach (var van in p3VertTris)
            {
                for (int i = 0; i < van.Length; i += 3)
                {
                    var pv0 = physicsVerts[van[i]];
                    var pv1 = physicsVerts[van[i + 1]];

                    var pv2 = physicsVerts[van[i + 2]];

                    //var v0 = new Vector3d(pv0.X, pv0.Y, pv0.Z);
                    //var v1 = new Vector3d(pv1.X, pv1.Y, pv1.Z);
                    //var v2 = new Vector3d(pv2.X, pv2.Y, pv2.Z);
                    var tri = new Triangle3d(pv0, pv1, pv2);
                    var distTri = new DistPoint3Triangle3(point, tri);
                    if (count == 0 && distTri.DistanceSquared >= 1) Log.Line($"Distance Check is yes");

                }
            }
            if (count == 0) DSUtils.StopWatchReport("Tri Distance", -1);
        }

        private int VertRangeCheckClosest(Vector3D[] physicsVerts3, Vector3D[] roots, int[][] zones, Vector3D bWorldCenter)
        {
            double minValue1 = 9999999999999999999;
            double minValue2 = 9999999999999999999;
            var minNum1 = -1;
            var minNum2 = -1;

            for (int r = 0; r < roots.Length; r++)
            {
                var vert = roots[r];
                var test1 = Vector3D.DistanceSquared(vert, bWorldCenter);

                if (test1 < minValue1)
                {
                    minValue1 = test1;
                    minNum1 = r;
                }
            }
            DrawLineToNum(minNum1, bWorldCenter);
            var zone = zones[minNum1];
            var zoneLen = zone.Length;
            for (int i = 0; i < zoneLen; i++)
            {
                var vert = physicsVerts3[zone[i]];
                var test2 = Vector3D.DistanceSquared(vert, bWorldCenter);
                //Log.Line($"zoneLen: {zone.Length} - i: {i} - minNum1: {minNum1} - numCheck: {zone[i]} - test: {test2}");

                if (test2 < minValue2)
                {
                    minValue2 = test2;
                    minNum2 = zone[i];
                    //Log.Line($"minNum2 {minNum2} - value {minValue2}");
                }
            }
            return minNum2;
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


        private bool IntersectLineObb(LineD[] boxedVectors, BoundingBox bLocalAabb, MatrixD matrix)
        {
            var bOriBBoxD = new MyOrientedBoundingBoxD(bLocalAabb, matrix);

            foreach (var line in boxedVectors)
            {
                var lineTest = line;
                if (bOriBBoxD.Intersects(ref lineTest).HasValue)
                {
                    return true;
                }
            }
            return false;
        }

        private List<Vector3D> IntersectVecBox(List<Vector3D> rangedVectors, BoundingBoxD bWorldAabb)
        {
            var boxedVectors = new List<Vector3D>();

            for (int i = 0, j = 0; i < rangedVectors.Count; i += 3, j++)
            {
                var v0 = rangedVectors[i];
                var v1 = rangedVectors[i + 1];
                var v2 = rangedVectors[i + 2];
                var test1 = bWorldAabb.Contains(v0);
                var test2 = bWorldAabb.Contains(v1);
                var test3 = bWorldAabb.Contains(v2);

                if (test1 == ContainmentType.Contains && test2 == ContainmentType.Contains && test3 == ContainmentType.Contains)
                {
                    boxedVectors.Add(v0);
                    boxedVectors.Add(v1);
                    boxedVectors.Add(v2);
                }
            }
            return boxedVectors;
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
            //var contactPoint = ContactPoint(breaching);
            var contactPoint = intersect;

            var transformInv = MatrixD.Invert(DetectionMatrix);
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(contactPoint, transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            var bmass = -breaching.Physics.Mass;
            var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
            //var expelForce = (bmass); /// Math.Pow(cpDist, 2);
            //if (expelForce < -9999000000f || bmass >= -67f) expelForce = -9999000000f;
            var expelForce = (bmass / 16) / (float)Math.Pow(cpDist, 2);


            var worldPosition = breaching.WorldMatrix.Translation;
            var worldDirection = contactPoint - worldPosition;

            //if (_gridIsMobile) Block.CubeGrid.Physics.ApplyImpulse(Vector3D.Negate(worldDirection) * (expelForce / 20), contactPoint);
            //else breaching.Physics.ApplyImpulse(worldDirection * (expelForce / 40), contactPoint);

            //breaching.Physics.ApplyImpulse(breaching.Physics.Mass * -0.050f * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, contactPoint);
            //Log.Line($"cpDist:{cpDist} pow:{expelForce} bmass:{bmass} adjbmass{bmass / 50}");

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }   

        private void DamageGrids()
        {
            try
            {
                lock (DmgBlocks)
                {
                    foreach (var block in DmgBlocks)
                    {
                        //block.DoDamage(100f, MyDamageType.Fire, true, null, Block.EntityId);
                    }
                    DmgBlocks.Clear();
                }
                //if (_count == 0) Log.Line($"Block Count {DmgBlocks.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in DamgeGrids: {ex}"); }
        }

        private Vector3D Intersect(IMyEntity ent, bool impactcheck)
        {
            var contactpoint = ContactPointObb(ent);

            if (contactpoint != Vector3D.NegativeInfinity) 
            {
                //Log.Line($"GridIsColliding {GridIsColliding.Count} - check {impactcheck} - containsEnt {GridIsColliding.Contains(ent as IMyCubeGrid)}");
                _impactSize = ent.Physics.Mass;
                if (impactcheck && !GridIsColliding.Contains(ent as IMyCubeGrid))
                {
                    //Log.Line($"ContactPoint to WorldImpact: {contactpoint}");
                    _worldImpactPosition = contactpoint;
                }
                if (impactcheck && ent is IMyCubeGrid && !GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Add(ent as IMyCubeGrid);
                //if (impactcheck && _worldImpactPosition != Vector3D.NegativeInfinity) Log.Line($"intersect true: {ent} - ImpactSize: {_impactSize} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()} - _worldImpactPosition: {_worldImpactPosition}");
                return contactpoint;
            }
            //if (impactcheck) Log.Line($"intersect false: {ent.GetFriendlyName()} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
            if (ent is IMyCubeGrid && GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Remove(ent as IMyCubeGrid);
            return Vector3D.NegativeInfinity;
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
                    if (grid == null || grid == Block.CubeGrid || !IsEnemy(killent) || Intersect(killent, false) == Vector3D.NegativeInfinity) return;

                    var contactPoint = ContactPoint(killent);
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
        private Vector3D ContactPoint(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = DetectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }
        #endregion

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
            var qWebsphere = new BoundingSphereD(_detectionCenter, _range);
            var qWebList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref qWebsphere);
            foreach (var webent in qWebList)
            {
                if (webent == null || webent is IMyFloatingObject || webent is IMyEngineerToolBase || webent == Block.CubeGrid) return;
                if (Block.CubeGrid.Physics.IsStatic && webent is IMyVoxelBase) continue;
                _enablePhysics = true;
                break;
            }
        }

        private void WebEntities()
        {
            var websphere = new BoundingSphereD(_detectionCenter, _range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    Log.Line($"intersect");
                    if (Intersect(webent, true) != Vector3D.NegativeInfinity)
                    {
                        _absorb += Shotdmg;
                        Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {webent} in loop {_count}");
                        if (webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo")) MyVisualScriptLogicProvider.CreateExplosion(webent.GetPosition(), 0, 0);
                        webent.Close();
                    }
                    return;
                }
                if (webent is IMyCharacter && (_count == 2 || _count == 17 || _count == 32 || _count == 47) && IsEnemy(webent) && Intersect(webent, true) != Vector3D.NegativeInfinity)
                {
                    Log.Line($"Enemy Player Intersected");
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid != null && grid != Block.CubeGrid && IsEnemy(webent))
                {
                    var intersect = Intersect(grid, true);
                    if (intersect != Vector3D.NegativeInfinity)
                    {
                        ContainmentField(grid, Block.CubeGrid, intersect);
                    }
                    return;
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

        private int GetVertNum(Vector3D vec)
        {
            var pmatch = false;
            var pNum = -1;
            foreach (var pvert in _physicsVerts)
            {
                pNum++;
                if (vec == pvert) pmatch = true;
                if (pmatch) return pNum;
            }
            return pNum;
        }

        private void FindRoots()
        {
            for (int i = 0, j = 0; i < _physicsVerts.Length; i++, j++)
            {
                var vec = _physicsVerts[i];
                foreach (var magic in _rootVecs)
                {
                    for (int num = 0; num < 12; num++)
                    {
                        if (_count == 0 && vec == magic && _rootVecs[num] == vec) Log.Line($"Found root {num} at index: {i}");
                    }

                }
            }
        }


        private bool[] GetZonesContaingNum(int locateVertNum, int size, bool draw = false)
        {
            // 1 = p3SmallZones, 2 = p3MediumZones, 3 = p3LargeZones, 4 = p3LargestZones
            var root = _dataStructures.p0RootZones;
            var small = _dataStructures.p3SmallZones;
            var medium = _dataStructures.p3MediumZones;
            var zone = size == 1 ? small : medium;
            if (size == 0) zone = root;
            var zMatch = new bool[12];

            for (int i = 0; i < zone.Length; i++)
            {
                foreach (var vertNum in zone[i])
                {
                    if (vertNum == locateVertNum) zMatch[i] = true;
                }
            }
            if (draw)
            {
                var c = 0;
                var j = 0;
                foreach (var z in zMatch)
                {
                    if (z)
                    {
                        DrawNums(zone[c]);
                    }
                    c++;
                }
            }

            return zMatch;
        }

        private int[] FindClosestZoneToVec(Vector3D locateVec, int size)
        {
            // 1 = p3SmallZones, 2 = p3MediumZones, 3 = p3LargeZones, 4 = p3LargestZones
            var root = _dataStructures.p0RootZones;
            var small = _dataStructures.p3SmallZones;
            var medium = _dataStructures.p3MediumZones;
            var zone = size == 1 ? small : medium;
            if (size == 0) zone = root;

            var zoneNum = -1;
            var tempNum = -1;
            var tempVec = Vector3D.Zero;
            double pNumDistance = 9999999999999999999;

            for (int i = 0; i < _physicsVerts.Length; i++)
            {
                var v = _physicsVerts[i];
                if (v != locateVec) continue;
                tempVec = v;
                tempNum = i;
            }
            var c = 0;
            foreach (int[] numArray in zone)
            {
                foreach (var vertNum in numArray)
                {
                    if (vertNum != tempNum) continue;
                    var distCheck = Vector3D.DistanceSquared(locateVec, tempVec);
                    if (!(distCheck < pNumDistance)) continue;
                    pNumDistance = distCheck;
                    zoneNum = c;
                }
                c++;
            }
            return zone[zoneNum];
        }

        private void DrawVertCollection(Vector3D collision, double radius, Color color, int lineWidth = 1)
        {
            var posMatCenterScaled = MatrixD.CreateTranslation(collision);
            var posMatScaler = MatrixD.Rescale(posMatCenterScaled, radius);
            var rangeGridResourceId = MyStringId.GetOrCompute("Build new");
            MySimpleObjectDraw.DrawTransparentSphere(ref posMatScaler, 1f, ref color, MySimpleObjectRasterizer.Solid, lineWidth, null, rangeGridResourceId, -1, -1);
        }

        private void DrawTriNumArray(int[] array)
        {
            var lineId = MyStringId.GetOrCompute("Square");
            var c = Color.Red.ToVector4();

            for (int i = 0; i < array.Length; i += 3)
            {
                var vn0 = array[i];
                var vn1 = array[i + 1];
                var vn2 = array[i + 2];

                var v0 = _physicsVerts[vn0];
                var v1 = _physicsVerts[vn1];
                var v2 = _physicsVerts[vn2];

                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v0, v2, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v1, v2, lineId, ref c, 0.25f);

            }
        }

        private void DrawTriVertList(List<Vector3D> list)
        {
            var lineId = MyStringId.GetOrCompute("Square");
            var c = Color.Red.ToVector4();
            for (int i = 0; i < list.Count; i += 3)
            {
                var v0 = list[i];
                var v1 = list[i + 1];
                var v2 = list[i + 2];

                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v0, v2, lineId, ref c, 0.25f);
                MySimpleObjectDraw.DrawLine(v1, v2, lineId, ref c, 0.25f);

            }
        }

        private void DrawLineNums(int[] lineArray)
        {
            var c = Color.Blue.ToVector4();
            var lineId = MyStringId.GetOrCompute("Square");

            for (int i = 0; i < lineArray.Length; i += 2)
            {
                var v0 = _physicsVerts[lineArray[i]];
                var v1 = _physicsVerts[lineArray[i + 1]]; 
                MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
            }
        }

        private void DrawLineToNum(int num, Vector3D fromVec)
        {
            var c = Color.Red.ToVector4();
            var lineId = MyStringId.GetOrCompute("Square");

            var v0 = _physicsVerts[num];
            var v1 = fromVec;
            MySimpleObjectDraw.DrawLine(v0, v1, lineId, ref c, 0.25f);
        }

        private void DrawRootVerts()
        {
            var i = 0;
            foreach (var root in _rootVecs)
            {
                var rootColor = _dataStructures.zoneColors[i];
                DrawVertCollection(root, 5, rootColor, 20);
                i++;
            }
        }

        private void DrawVerts(Vector3D[] list, Color color = default(Color))
        {
            var i = 0;
            foreach (var vec in list)
            {
                var rootColor = _dataStructures.zoneColors[i];
                if (vec == _rootVecs[i]) color = rootColor;
                DrawVertCollection(vec, 5, color, 8);
                i++;
            }
        }

        private void DrawNums(int[] list, Color color = default(Color))
        {
            foreach (var num in list)
            {
                var i = 0;
                foreach (var root in _rootVecs)
                {
                    var rootColor = _dataStructures.zoneColors[i];
                    if (_physicsVerts[num] == root) color = rootColor;
                    i++;
                }
                DrawVertCollection(_physicsVerts[num], 5, color, 8);
            }
        }

        private void DrawSingleNum(int num)
        {
            //Log.Line($"magic: {magic}");
            var c = Color.Black;
            DrawVertCollection(_physicsVerts[num], 7, c, 20);
        }
    }
}
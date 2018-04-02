﻿using Sandbox.Game;
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
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Gui;
using VRageRender.Models;

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
        internal float Range;
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
        private int _prevLod;
        private int _onCount;

        private uint _tick;

        private const bool Debug = true;
        private const bool DrawDebug = false;
        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _buildByVerts = true;
        private bool _buildVertZones = true;
        private bool _buildLines = false;
        private bool _buildTris = false;
        private bool _buildOnce;
        internal bool Initialized; 
        private bool _animInit;
        private bool _playerwebbed;
        internal bool GridIsMobile;
        private bool _explode;
        private bool _longLoop10;
        private bool _firstRun = true;
        private bool _enemy;
        internal bool ShieldActive;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _localImpactPosition;
        private Vector3D _detectionCenter;
        internal Vector3D ShieldSize { get; set; }

        private readonly Vector3D[] _rootVecs = new Vector3D[12];
        public readonly Vector3D[] _physicsOutside = new Vector3D[642];
        private readonly Vector3D[] _physicsInside = new Vector3D[642];

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectMatrix;
        private MatrixD _detectMatrixInv;
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectionInsideInv;
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

        //private readonly  MyEntitySubpart[] _subpartsArms = new MyEntitySubpart[8];
        //private readonly MyEntitySubpart[] _subpartsReflectors = new MyEntitySubpart[4];
        //private Matrix[] _matrixArmsOff = new Matrix[8];
        //private Matrix[] _matrixArmsOn = new Matrix[8];
        //private Matrix[] _matrixReflectorsOff = new Matrix[4];
        //private Matrix[] _matrixReflectorsOn = new Matrix[4];

        public HashSet<IMyEntity> InFriendlyCache = new HashSet<IMyEntity>();
        public MyConcurrentHashSet<IMyEntity> InShield = new MyConcurrentHashSet<IMyEntity>();
        private MyConcurrentHashSet<IMyEntity> OutShield = new MyConcurrentHashSet<IMyEntity>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();
        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private MyConcurrentList<IMySlimBlock> DmgBlocks { get; } = new MyConcurrentList<IMySlimBlock>();
        private MyConcurrentList<IMySlimBlock> FewDmgBlocks { get; } = new MyConcurrentList<IMySlimBlock>();


        public MyResourceSinkComponent Sink { get { return _sink; } set { _sink = value; } }

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }

        // temp
        private bool needsMatrixUpdate = false;
        internal DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        private bool blocksNeedRefresh = false;
        public const float MIN_SCALE = 15f; // Scale slider min/max
        public const float MAX_SCALE = 300f;
        public float LargestGridLength = 2.5f;
        public static MyModStorageComponent Storage { get; set; } // broken, shouldn't be static.  Move to Session if possible.
        private HashSet<ulong> playersToReceive = null;
        // 
        #endregion

        #region constructors
        private MatrixD DetectionMatrix
        {
            get { return _detectMatrix; }
            set
            {
                _detectMatrix = value;
                _detectMatrixInv = MatrixD.Invert(value);
                _detectMatrixOutside = value;
                _detectMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
                _detectionInsideInv = MatrixD.Invert(_detectMatrixInside);

            }
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
            Block.CubeGrid.Components.Add(new ShieldGridComponent(this));

        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            _dsutil2.Sw.Start();
            ShieldActive = Initialized && Block.IsWorking && Range > 0 && Block.IsFunctional;
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            try
            {
                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10)
                    {
                        lock (_webEnts)
                            foreach (var i in _webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1).ToList())
                                _webEnts.Remove(i.Key);
                        lock (InFriendlyCache) InFriendlyCache.Clear();
                        _longLoop = 0;
                    }
                }

                if (_longLoop == 0 && _count == 0) _longLoop10 = true;
                else _longLoop10 = false;
                if (_explode && _explodeCount++ == 14) _explodeCount = 0;
                if (_explodeCount == 0 && _explode) _explode = false;

                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel) // ugly workaround for realtime terminal updates
                    {
                        Block.ShowInToolbarConfig = false;
                        Block.ShowInToolbarConfig = true;
                    }
                }

                if (GridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); 
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation));
                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = entAngularVelocity || entLinVel || _gridChanged;
                    if (_entityChanged || Range <= 0) CreateShieldMatrices();
                }

                if (ShieldActive)
                {
                    if (_count == 0) _enablePhysics = false;
                    if (_enablePhysics == false) QuickWebCheck();
                    if ((_enablePhysics && _entityChanged) || _firstRun) BuildPhysicsArrays();
                    if (_animInit)
                    {
                        if (_subpartRotor.Closed.Equals(true)) BlockAnimationReset();
                        if (Distance(1000))
                        {
                            var blockCam = Block.PositionComp.WorldVolume;
                            if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam)) MyAPIGateway.Parallel.Start(BlockAnimation);
                        }
                    }
                    SyncThreadedEnts();
                    if (_enablePhysics) MyAPIGateway.Parallel.Start(WebEntities);
                    //WebEntities();
                }
                _dsutil2.StopWatchReport("main loop", 1);
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }

        }

        #region Prep / Misc
        private void BuildPhysicsArrays()
        {
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _physicsOutside);
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _rootVecs);
            _icosphere.ReturnPhysicsVerts(_detectMatrixInside, _physicsInside);
            //_structureBuilder.BuildTriNums(_icosphere.CalculatePhysics(_detectMatrixOutside, 3), _physicsOutside);
            //if (_buildOnce == false) _structureBuilder.BuildBase(_icosphere.CalculatePhysics(_detectMatrixOutside, 3), _rootVecs, _physicsOutside, _buildLines, _buildTris, _buildVertZones, _buildByVerts);
            //_buildOnce = true;
            _firstRun = false;
        }
        #endregion

        public override void UpdateBeforeSimulation100()
        {
            if (Initialized) return;
            Log.Line($"Initting entity");
            if (Block.CubeGrid.Physics.IsStatic) GridIsMobile = false;
            else if (!Block.CubeGrid.Physics.IsStatic) GridIsMobile = true;

            CreateUi();
            Block.AppendingCustomInfo += AppendingCustomInfo;
            Block.RefreshCustomInfo();
            _absorb = 150f;

            _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
            _shield.Render.Visible = false;
            Initialized = true;
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
                    Log.Line($" BlockAnimation {_count.ToString()}");
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
            if (!Initialized || !Block.IsWorking) return _power;
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
            if (!GridIsMobile)RefreshDimensions();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW\nCharge percent: ");
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
            if (GridIsMobile)
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

        private void CreateShieldMatrices()
        {
            if (GridIsMobile)
            {
                _shieldGridMatrix = Block.CubeGrid.WorldMatrix;
                CreateMobileShape();
                var mobileMatrix = _mobileMatrix;
                DetectionMatrix = mobileMatrix * _shieldGridMatrix;
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;
            }
            else
            {
                _shieldGridMatrix = Block.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                ShieldSize = DetectionMatrix.Scale;
            }
            Range = (float)_detectMatrix.Scale.AbsMax() + 15f;
        }

        private void CreateMobileShape()
        {
            if (!_gridChanged) return;

            var gridHalfExtents = Block.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const float ellipsoidAdjust = (float)MathHelper.Sqrt2;
            const float buffer = 5f;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            ShieldSize = shieldSize;
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
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            if (!_entityChanged || Block.CubeGrid.Physics.IsStatic) return;
            CreateMobileShape();
            var mobileMatrix = _mobileMatrix;

            _shieldShapeMatrix = mobileMatrix;
            _shield.SetWorldMatrix(_shieldShapeMatrix);
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
            Log.Line($"Create UI - c:{_count.ToString()}");
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
            Log.Line($"Resetting BlockAnimation in loop {_count.ToString()}");
            _subpartRotor.Subparts.Clear();
            //Array.Clear(_subpartsArms, 0, 8);
            //Array.Clear(_subpartsReflectors, 0, 4);
            BlockAnimationInit();
        }

        private void BlockAnimationInit()
        {
            try
            {
                /*
                _animStep = 0f;
                _matrixArmsOff = new Matrix[8];
                _matrixArmsOn = new Matrix[8];
                _matrixReflectorsOff = new Matrix[4];
                _matrixReflectorsOn = new Matrix[4];

                */
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                /*

                for (var i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    _subpartRotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    _matrixArmsOff[i - 1] = (temp1.PositionComp.LocalMatrix);

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
                    _matrixArmsOn[i - 1] = (temp2);
                    _subpartsArms[i - 1] = (temp1);
                }

                for (var i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    _subpartsArms[i].TryGetSubpart("Reflector", out temp3);
                    _subpartsReflectors[i] = (temp3);
                    _matrixReflectorsOff[i] = (temp3.PositionComp.LocalMatrix);

                    var temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    _matrixReflectorsOn[i] = (temp4);
                }
                */
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
                //if (_animStep < 1f) _animStep += 0.05f;
            }
            //else  if (_animStep > 0f) _animStep -= 0.05f;

            /*
            for (var i = 0; i < 8; i++)
            {
                if (i < 4) _subpartsReflectors[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixReflectorsOff[i], _matrixReflectorsOn[i], _animStep);
                _subpartsArms[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixArmsOff[i], _matrixArmsOn[i], _animStep);
            }
            */
        }
        #endregion

        #region Shield Draw
        //private Task? _prepareDraw = null;
        public void Draw(int onCount, bool sphereOnCamera)
        {
            try
            {
                if (!Initialized) return;
                SetShieldShapeMatrix();
                _onCount = onCount;
                var enemy = false;
                var relation = MyAPIGateway.Session.Player.GetRelationTo(Block.OwnerId);
                if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
                _enemy = enemy;
                var impactPos = _worldImpactPosition;
                if (impactPos != Vector3D.NegativeInfinity)
                {
                    var cubeBlockLocalMatrix = Block.CubeGrid.LocalMatrix;
                    var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                    var worldDirection = impactPos - referenceWorldPosition;
                    var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                    impactPos = localPosition;
                }
                _localImpactPosition = impactPos;
                _worldImpactPosition = Vector3D.NegativeInfinity;

                //if (_prepareDraw.HasValue && !_prepareDraw.Value.IsComplete) _prepareDraw.Value.Wait();
                //if (_prepareDraw.HasValue && _prepareDraw.Value.IsComplete && sphereOnCamera && Block.IsWorking) _icosphere.Draw(GetRenderId());
                //if (Block.IsWorking || _entityChanged) _prepareDraw = MyAPIGateway.Parallel.Start(PrepareSphere);
                if (Block.IsWorking || _entityChanged) PrepareSphere();
                if (sphereOnCamera && Block.IsWorking) _icosphere.Draw(GetRenderId());
            }
            catch (Exception ex) { Log.Line($"Exception in Entity Draw: {ex}"); }
        }

        private void PrepareSphere()
        {
            var prevlod = _prevLod;
            var lod = CalculateLod(_onCount);
            _icosphere.CalculateTransform(_shieldShapeMatrix, lod);
            _icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _impactSize, _entityChanged, _enemy, _shield, prevlod);
            _entityChanged = false;
        }

        #endregion

        #region Shield Draw Prep
        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Block.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + Range) * (x + Range);
            return range;
        }

        private int CalculateLod(int onCount)
        {
            var lod = 2;

            if (Distance(2500) && onCount == 1) lod = 3;
            else if (Distance(8000) && onCount < 7) lod = 2;
            else lod = 1;

            _prevLod = lod;
            return lod;
        }

        private int EntType(IMyEntity ent)
        {
            if (ent == null) return -1;
            if (ent is IMyVoxelMap && !GridIsMobile) return -1;
            if (ent is IMyCharacter)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return -1;
                var playerrelationship = Block.GetUserRelationToOwner((long) dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner ||
                    playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return 0;
                return 1;
            }
            if (ent is IMyCubeGrid)
            {
                var grid = ent as IMyCubeGrid;

                if (grid.PositionComp.WorldVolume.Radius < 6.5 && grid.BigOwners.Count == 0)
                {
                    Log.Line($"bigowners 0 and radius < 6.5");
                    return 2;
                }
                if (grid.BigOwners.Count <= 0) return 3;

                var enemy = GridEnemy(grid);

                if (enemy && grid.PositionComp.WorldVolume.Radius < 6.5)
                {
                    Log.Line($"enemy and radius < 6.5");
                    return 4;
                }

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent != null && !(shieldComponent.DefenseShields.ShieldActive) && enemy) return 3;
                if (shieldComponent != null && Entity.EntityId > shieldComponent.DefenseShields.Entity.EntityId) return 6;
                if (shieldComponent != null) return -1; //only process the higher EntityID
                return enemy ? 5 : 0;
            }

            if (ent is IMyMeteor || ent.ToString().Contains("Missile")) return 7;
            if (ent is IMyVoxelMap && GridIsMobile) return 8;
            return 0;
        }

        private bool GridEnemy(IMyCubeGrid grid)
        {
            var owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = Block.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }

        private uint GetRenderId()
        {
            //var renderId = _gridIsMobile ? Block.CubeGrid.Render.GetRenderObjectID() : Block.CubeGrid.Render.GetRenderObjectID(); 
            var renderId = Block.CubeGrid.Render.GetRenderObjectID(); 
            return renderId;
        }
        #endregion

        #region Detect Intersection
        private void VoxelIntersect(IMyVoxelMap voxelMap)
        {
            var center = Block.CubeGrid.WorldVolume.Center;
            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(_shield.WorldAABB);
            bOriBBoxD.Center = center;
            CustomCollision.VoxelCollisionSphere(Block.CubeGrid, _physicsOutside, voxelMap, bOriBBoxD);
        }

        private void SmallGridIntersect(IMyCubeGrid grid)
        {
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;
            var contactPoint = CustomCollision.SmallIntersect(FewDmgBlocks, grid, _detectMatrix, _detectMatrixInv);
            if (contactPoint != Vector3D.NegativeInfinity) _worldImpactPosition = contactPoint;
        }

        private void GridIntersect(IMyCubeGrid grid, EntIntersectInfo entInfo)
        {
            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB);
            if (GridInside(grid, bOriBBoxD)) return;

            var contactpoint = ContactPointObb(grid, bOriBBoxD, entInfo);
            if (contactpoint == Vector3D.NegativeInfinity) return;
            //Log.Line($"large Grid Intersect");

            //ContainmentField(grid, Block.CubeGrid, contactpoint);
            _impactSize = grid.Physics.Mass;
            _worldImpactPosition = contactpoint;
        }

        private void ShieldIntersect(IMyCubeGrid grid)
        {
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);

            var dsVerts = shieldComponent.DefenseShields._physicsOutside;
            var dsMatrixInv = shieldComponent.DefenseShields._detectMatrixInv;
            var myGrid = Block.CubeGrid;
            if (GridIsMobile)
            {
                var insidePoints = new List<Vector3D>();
                CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, _physicsOutside, _detectMatrixInv, insidePoints);
                for (int i = 0; i < insidePoints.Count; i++)
                {
                    grid.Physics.ApplyImpulse((grid.PositionComp.WorldVolume.Center - insidePoints[i]) * grid.Physics.Mass / 250, insidePoints[i]);
                    myGrid.Physics.ApplyImpulse((myGrid.PositionComp.WorldVolume.Center - insidePoints[i]) * myGrid.Physics.Mass / 250, insidePoints[i]);
                }

                if (insidePoints.Count <= 0) return;

                var contactPoint = DSUtils.CreateFromPointsList(insidePoints).Center;
                _worldImpactPosition = contactPoint;
                shieldComponent.DefenseShields._worldImpactPosition = contactPoint;
            }
            else
            {
                var insidePoints = new List<Vector3D>();
                CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, _physicsOutside, _detectMatrixInv, insidePoints);
                for (int i = 0; i < insidePoints.Count; i++)
                {
                    grid.Physics.ApplyImpulse((grid.PositionComp.WorldVolume.Center - insidePoints[i]) * grid.Physics.Mass / 250, insidePoints[i]);
                    myGrid.Physics.ApplyImpulse((myGrid.PositionComp.WorldVolume.Center - insidePoints[i]) * myGrid.Physics.Mass / 250, insidePoints[i]);
                }

                if (insidePoints.Count <= 0) return;

                var contactPoint = DSUtils.CreateFromPointsList(insidePoints).Center;
                _worldImpactPosition = contactPoint;
                shieldComponent.DefenseShields._worldImpactPosition = contactPoint;
            }
        }

        private bool GridInside(IMyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, _detectionInsideInv))
            {
                if (CustomCollision.AllCornersInShield(bOriBBoxD, _detectMatrixInv)) return true;

                var ejectDir = CustomCollision.EjectDirection(grid, _physicsOutside, _dataStructures.p3VertTris, bOriBBoxD, _detectMatrixInv);
                if (ejectDir == Vector3D.NegativeInfinity) return false;
                Log.Line($"ejecting grid");
                lock (Eject) Eject.Add(grid, ejectDir);
                return true;
            }
            return false;
        }

        private void PlayerIntersect(EntIntersectInfo player)
        {
            var rnd = new Random();
            var character = MyAPIGateway.Entities.GetEntityById(player.EntId) as IMyCharacter;
            if (character == null) return;

            var playerid = character.EntityId;
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
            if (!character.EnabledThrusts) return;

            var insideTime = (int)player.LastTick - (int)player.FirstTick;
            var explodeRollChance = rnd.Next(0 - insideTime, insideTime);
            if (explodeRollChance <= 666) return;

            lock (_webEnts) _webEnts.Remove(MyAPIGateway.Entities.GetEntityById(player.EntId));

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;

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

        private Vector3D ContactPointObb(IMyEntity breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
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

            var bLocalAabb = breaching.PositionComp.LocalAABB;
            var bWorldAabb = breaching.PositionComp.WorldAABB;
            var bWorldCenter = bWorldAabb.Center;
            var tSphere = breaching.WorldVolume;

            var lodScaler = (int)Math.Pow(2, PhysicsLod);
            var gridScaler = (float)(((_detectMatrix.Scale.X + _detectMatrix.Scale.Y + _detectMatrix.Scale.Z) / 3 / lodScaler) * 1.33) / bLocalAabb.Extents.Min();
            var faceTri = new int[4];
            var rangedVerts = new int[3];
            var intersections = new List<Vector3D>();
            var dsutil = new DSUtils();
            if (gridScaler > 1)
            {

                var closestFace0 = _dataStructures.p3VertTris[rangedVerts[0]];
                var closestFace1 = _dataStructures.p3VertTris[rangedVerts[1]];
                var closestFace2 = _dataStructures.p3VertTris[rangedVerts[2]];

                CustomCollision.GetClosestTriAndFace(_physicsOutside, _physicsInside, closestFace0, closestFace1, closestFace2, bWorldCenter, faceTri);

                int[] closestFace;
                switch (faceTri[0])
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
                CustomCollision.IntersectSmallBox(closestFace, _physicsOutside, bWorldAabb, intersections);
                if (DrawDebug) DsDebugDraw.SmallIntersectDebugDraw(_physicsOutside, faceTri[0], _dataStructures.p3VertLines, rangedVerts, bWorldCenter, intersections);
            }
            else intersections = CustomCollision.ContainPointObb(_physicsOutside, bOriBBoxD, tSphere);

            var grid = breaching as IMyCubeGrid;
            if (grid == null) return Vector3D.NegativeInfinity;

            if (intersections.Count == 0) return Vector3D.NegativeInfinity;

            var locCenterSphere = DSUtils.CreateFromPointsList(intersections);
            var collision = Vector3D.Lerp(GridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);

            try
            {
                if (collision != Vector3D.NegativeInfinity)
                {
                    dsutil.Sw.Start();
                    var c = 0;
                    if (entInfo.CacheBlockList.Count != 0)
                    {
                        var sCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;
                        var gridDetectMatrix = MatrixD.CreateScale(grid.GridSize) * grid.PositionComp.WorldMatrix * _detectMatrixInv;
                        foreach (var block in entInfo.CacheBlockList)
                        {
                            if (Vector3.Transform(block.Position, gridDetectMatrix).LengthSquared() <= 1)
                            {
                                grid.Physics.ApplyImpulse((bWorldCenter - sCenter) * grid.Physics.Mass / 180, sCenter);
                                Block.CubeGrid.Physics.ApplyImpulse((sCenter - bWorldCenter) * Block.CubeGrid.Physics.Mass / 180, bWorldCenter);

                                if (DmgBlocks.Count == 25) continue;

                                lock (DmgBlocks) DmgBlocks.Add(block);
                                c++;
                            }
                        }
                    }
                    dsutil.StopWatchReport("block eject", -1);
                    if (c > 0) return collision;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}"); }
            return Vector3D.NegativeInfinity;
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
            //var transformInv = _detectMatrixInv;
            //var normalMat = MatrixD.Transpose(transformInv);
            //var localNormal = Vector3D.Transform(contactPoint, transformInv);
            //var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            var bmass = -breaching.Physics.Mass;
            //var cpDist = Vector3D.Transform(contactPoint, _detectMatrixInv).LengthSquared();
            //var expelForce = (bmass); /// Math.Pow(cpDist, 2);
            //if (expelForce < -9999000000f || bmass >= -67f) expelForce = -9999000000f;
            var expelForce = (bmass);// / (float)Math.Pow(cpDist, 4);

            var worldPosition = breaching.WorldMatrix.Translation;
            var worldDirection = contactPoint - worldPosition;

            /*
            if (GridIsMobile)
            {
                Block.CubeGrid.Physics.ApplyImpulse(Vector3D.Negate(worldDirection) * (expelForce / Block.CubeGrid.Physics.Mass), contactPoint);
                breaching.Physics.ApplyImpulse(worldDirection * (expelForce), contactPoint);
            }
            else breaching.Physics.ApplyImpulse(worldDirection * (expelForce), contactPoint);
            */
            //breaching.Physics.ApplyImpulse(breaching.Physics.Mass * -0.050f * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, contactPoint);
            //Log.Line($"cpDist:{cpDist} pow:{expelForce} bmass:{bmass} adjbmass{bmass / 50}");

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }   
        #endregion

        #region Web and Sync Entities
        private void QuickWebCheck()
        {
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyEntity>();
            var queryType = GridIsMobile ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;

            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList, queryType);
            for (int i = 0; i < pruneList.Count; i++)
            {
                var webent = pruneList[i];
                //var relation = EntType(webent);

                if ((webent is IMyCubeGrid && webent as IMyCubeGrid != Block.CubeGrid && GridEnemy(webent as IMyCubeGrid)) || (GridIsMobile && webent is IMyVoxelMap))
                {
                    _enablePhysics = true;
                    return;
                }
            }
        }

        private void WebEntities()
        {
            //_dsutil3.Sw.Start();
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyEntity>();

            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);
            lock (_webEnts)
            {
                for (int i = 0; i < pruneList.Count; i++)
                {
                    var ent = pruneList[i];
                    if (ent == null) continue;
                    var entCenter = ent.PositionComp.WorldVolume.Center;

                    if (ent == _shield || ent as IMyCubeGrid == Block.CubeGrid || ent.Physics == null || ent.MarkedForClose || ent is IMyVoxelBase && !GridIsMobile
                        || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || InFriendlyCache.Contains(ent)|| ent.GetType().Name == "MyDebrisBase") continue;

                    if (_webEnts.ContainsKey(ent)) _webEnts[ent].LastTick = _tick;
                    else
                    {
                        var relation = EntType(ent);

                        if (relation > 0 && CustomCollision.PointInShield(entCenter, _detectionInsideInv) == false) _webEnts.Add(ent, new EntIntersectInfo(ent.EntityId, _tick, _tick, relation, false, new List<IMySlimBlock>()));
                        else if (relation == 0 || relation == 3) lock (InFriendlyCache) InFriendlyCache.Add(ent);
                    }
                }

                foreach (var webent in _webEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = _webEnts[webent];
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (_webEnts[webent].Relation == 3 || _webEnts[webent].Relation == 5)) ((IMyCubeGrid) webent).GetBlocks(_webEnts[webent].CacheBlockList, Collect); 
                    switch (_webEnts[webent].Relation)
                    // -1=null, 0=friend, 1=enemyPlayer, 2=sNobodyGrid, 3=lNobodyGrid, 4=sEnemyGrid, 5=lEnemyGrid, 6=shielded, 7=Other, 8=VoxelMap  
                    {
                        case 1:
                        {
                            if (_count == 2 || _count == 17 || _count == 32 || _count == 47  && CustomCollision.PointInShield(entCenter, _detectMatrixInv))
                                MyAPIGateway.Parallel.Start(() => PlayerIntersect(entInfo));
                            continue;
                        }
                        case 2:
                        {
                            MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                            //SmallGridIntersect(webent as IMyCubeGrid);
                            continue;
                        }
                        case 3:
                        {
                            MyAPIGateway.Parallel.Start(() => GridIntersect(webent as IMyCubeGrid, _webEnts[webent]));
                            //GridIntersect(webent as IMyCubeGrid, _webEnts[webent]);
                            continue;
                        }
                        case 4:
                        {
                            MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                            //SmallGridIntersect(webent as IMyCubeGrid);
                            continue;
                        }
                        case 5:
                        {
                            //Log.Line($"enemy large grid");
                            MyAPIGateway.Parallel.Start(() => GridIntersect(webent as IMyCubeGrid, _webEnts[webent]));
                            //GridIntersect(webent as IMyCubeGrid, _webEnts[webent]);
                            continue;
                        }
                        case 6:
                        {
                            //Log.Line($"enemy shield grid");
                            MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent as IMyCubeGrid));
                            continue;
                        }
                        case 7:
                        {
                            if (entInfo.LastTick == _tick && CustomCollision.PointInShield(entCenter, _detectMatrixInv) && !entInfo.SpawnedInside)
                            {
                                _worldImpactPosition = entCenter;
                                _absorb += Shotdmg;
                                MyVisualScriptLogicProvider.CreateExplosion(entCenter, 0, 0);
                                webent.Close();
                            }
                            continue;
                        }
                        case 8:
                        {
                            MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as IMyVoxelMap));
                            continue;
                        }
                        default:
                            continue;
                    }
                }
                //_dsutil3.StopWatchReport("Webbing", .1f);
            }
        }

        private bool Collect(IMySlimBlock mySlimBlock)
        {
            //if (!(mySlimBlock.Mass < 80 || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel) || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel) || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_ButtonPanel))) Log.Line($"{mySlimBlock.BlockDefinition.DisplayNameText} {mySlimBlock.Mass} {mySlimBlock.BlockDefinition.Id}");

            return mySlimBlock.Mass > 80 || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel)) || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel)) || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_ButtonPanel));
        }

        private void SyncThreadedEnts()
        {
            try
            {
                lock (Eject)
                {
                    if (Eject.Count != 0)
                    {
                        foreach (var e in Eject) e.Key.SetPosition(Vector3D.Lerp(e.Key.GetPosition(), e.Value, 0.25d));
                        Eject.Clear();
                    }
                }
                lock (FewDmgBlocks)
                {
                    var blockLen = FewDmgBlocks.Count;
                    if (blockLen != 0)
                    {
                        var c = 0;
                        for (int i = 0; i < blockLen; i++)
                        {
                            if (c == 25) break;
                            var block = FewDmgBlocks[i];
                            if (block == null || block.IsDestroyed) continue;
                            lock (_webEnts) if (_webEnts.ContainsKey(block.CubeGrid)) _webEnts[block.CubeGrid].CacheBlockList.Remove(block);
                            block.OnDestroy();
                            block.DoDamage(500f, MyDamageType.Explosion, true, null, Block.CubeGrid.EntityId);
                            if (i < blockLen)
                            {
                                var myCube = (MyCubeGrid)block.CubeGrid;
                                if (myCube.BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                            }
                            c++;
                            if (c == 5)
                            {
                                Vector3D center;
                                block.ComputeWorldCenter(out center);
                                MyVisualScriptLogicProvider.CreateExplosion(center, 10f, 1500);
                            }
                        }
                        FewDmgBlocks.Clear();
                    }
                }

                lock (DmgBlocks)
                {
                    var blockLen = DmgBlocks.Count;
                    if (blockLen != 0)
                    {
                        var c = 0;
                        for (int i = 0; i < blockLen; i++)
                        {
                            if (c == 25) break;
                            var block = DmgBlocks[i];
                            if (block == null || block.IsDestroyed) continue;
                            lock (_webEnts) if (_webEnts.ContainsKey(block.CubeGrid)) _webEnts[block.CubeGrid].CacheBlockList.Remove(block);
                            block.OnDestroy();
                            block.DoDamage(5000f, MyDamageType.Explosion, true, null, Block.CubeGrid.EntityId);
                            if (i < blockLen)
                            {
                                var myCube = (MyCubeGrid)block.CubeGrid;
                                if (myCube.BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                            }
                            c++;
                            if (c == 25 && new Random().Next(0, 10) == 2)
                            {
                                Vector3D center;
                                block.ComputeWorldCenter(out center);
                                MyVisualScriptLogicProvider.CreateExplosion(center, (float)block.CubeGrid.WorldAABB.Max.Min() / 3, 15000);
                            }
                        }
                        DmgBlocks.Clear();
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in DamageGrids: {ex}"); }
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
            Log.Line($"UseThisShip_Receiver({fix.ToString()})");

            //UseThisShip_Internal(fix);
        }
        #endregion
    }
}
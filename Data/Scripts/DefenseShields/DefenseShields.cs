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
using Sandbox.Game.Entities;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "StationDefenseShield")]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private const float Shotdmg = 1f;

        private float _power = 0.0001f;
        internal float Range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;
        private float _impactSize;

        private double _sAvelSqr;
        private double _sVelSqr;

        private const int PhysicsLod = 3;

        private int _count = -1;
        private int _longLoop;
        private int _explodeCount;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private int _prevLod;
        private int _onCount;

        private uint _tick;

        private const bool Debug = true;
        private const bool DrawDebug = false;
        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _enableWeb = true;
        private bool _buildByVerts = true;
        private bool _buildVertZones = true;
        private bool _buildLines = false;
        private bool _buildTris = false;
        private bool _buildOnce;
        internal bool Initialized; 
        private bool _animInit;
        internal bool GridIsMobile;
        private bool _firstRun = true;
        private bool _enemy;
        internal bool ShieldActive;
        private bool _shieldMoving = true;

        private const ushort ModId = 50099;

        private readonly MyParticleEffect[] _effects = new MyParticleEffect[1];

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _localImpactPosition;
        private Vector3D _detectionCenter;
        private Vector3D _sVel;
        private Vector3D _sAvel;
        internal Vector3D ShieldSize { get; set; }

        private readonly Vector3D[] _rootVecs = new Vector3D[12];
        private readonly Vector3D[] _physicsOutside = new Vector3D[642];
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

        private MyConcurrentHashSet<IMyEntity> _inFriendlyCache = new MyConcurrentHashSet<IMyEntity>();
        public MyConcurrentHashSet<IMyEntity> InShield = new MyConcurrentHashSet<IMyEntity>();
        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();
        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private MyConcurrentQueue<IMySlimBlock> _dmgBlocks  = new MyConcurrentQueue<IMySlimBlock>();
        private MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();

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

        #region constructors and Enums
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
        // -1=null, 0=friend, 1=enemyPlayer, 2=sNobodyGrid, 3=lNobodyGrid, 4=sEnemyGrid, 5=lEnemyGrid, 6=shielded, 7=Other, 8=VoxelMap  

        public enum Ent
        {
            Ignore,
            Friend,
            EnemyPlayer,
            SmallNobodyGrid,
            LargeNobodyGrid,
            SmallEnemyGrid,
            LargeEnemyGrid,
            Shielded,
            Other,
            VoxelMap
        };
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
                        foreach (var i in _webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1).ToList())
                            _webEnts.Remove(i.Key);
                        _inFriendlyCache.Clear();
                        _longLoop = 0;
                    }
                }

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
                    //_sVel = Block.CubeGrid.Physics.LinearVelocity;
                    //_sAvel = Block.CubeGrid.Physics.AngularVelocity;
                    _sVelSqr = Block.CubeGrid.Physics.LinearVelocity.LengthSquared();
                    _sAvelSqr = Block.CubeGrid.Physics.AngularVelocity.LengthSquared();
                    if (_sVelSqr > 0.00001 || _sAvelSqr > 0.00001) _shieldMoving = true;
                    else _shieldMoving = false;

                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = Block.CubeGrid.Physics.IsMoving || _gridChanged;
                    if (_entityChanged || Range <= 0) CreateShieldMatrices();
                }
                if (ShieldActive)
                {
                    if (_count == 0)
                    {
                        _enablePhysics = false;
                        _enableWeb = false;
                    }
                    if (_enablePhysics == false) QuickWebCheck();
                    if ((_enablePhysics && _entityChanged) || _firstRun) BuildPhysicsArrays();
                    if (_animInit)
                    {
                        if (_subpartRotor.Closed.Equals(true)) BlockAnimationReset();
                        if (Distance(1000))
                        {
                            var blockCam = Block.PositionComp.WorldVolume;
                            if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam)) BlockAnimation();
                        }
                    }
                    //if (_enablePhysics || _enableWeb) MyAPIGateway.Parallel.Start(WebEntities);
                    if (_enablePhysics || _enableWeb) WebEntities();
                }
                SyncThreadedEnts();
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
            _dsutil2.StopWatchReport("main loop", 0.5f);
        }

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
            if (_animInit) return;
            if (Block.BlockDefinition.SubtypeId == "StationDefenseShield")
            {
                if (!Block.IsFunctional) return;
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                Log.Line($" BlockAnimation {_count.ToString()}");
                _animInit = true;
            }
            else NeedsUpdate = MyEntityUpdateEnum.NONE;
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
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            var controls = new List<IMyTerminalControl>();
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
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockAnimation()
        {
            if (!Block.Enabled || !Block.IsFunctional || !Block.IsWorking)
            {
                for (int i = 0; i < _effects.Length; i++) if (_effects[i] != null) _effects[i].RemoveInstance(_effects[i]);
                return;
            }

            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] == null)
                {
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = 10f;
                    _effects[i].UserEmitterScale = 1f;
                }
                if (_effects[i] != null) _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
            }

            _time -= 1;
            if (_count == 0 && _longLoop == 0) _time2 = 0;
            if (_longLoop < 5) _time2 += 1;
            else _time2 -= 1;
            if (_count == 0) _emissiveIntensity = 2;
            if (_count < 30) _emissiveIntensity += 1;
            else _emissiveIntensity -= 1;
                
            var temp1 = MatrixD.CreateRotationY(0.05f * _time);
            var temp2 = MatrixD.CreateTranslation(0, 0.002f * _time2, 0);
            _subpartRotor.PositionComp.LocalMatrix = temp1 * temp2;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }
        #endregion

        #region Shield Draw
        public void Draw(int onCount, bool sphereOnCamera)
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

            if (Block.IsWorking || _entityChanged) PrepareSphere();
            if (sphereOnCamera && Block.IsWorking) _icosphere.Draw(GetRenderId());
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

        private uint GetRenderId()
        {
            var renderId = Block.CubeGrid.Render.GetRenderObjectID(); 
            return renderId;
        }
        #endregion

        #region Entity Information
        private Ent EntType(IMyEntity ent)
        {
            if (ent == null) return Ent.Ignore;
            if (ent is IMyVoxelMap && !GridIsMobile) return Ent.Ignore;

            if (ent is IMyCharacter)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = Block.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return Ent.Friend;
                return Ent.EnemyPlayer;
            }
            if (ent is IMyCubeGrid)
            {
                var grid = ent as IMyCubeGrid;
                if (grid.PositionComp.WorldVolume.Radius < 6.5 && grid.BigOwners.Count == 0) return Ent.SmallNobodyGrid;
                if (grid.BigOwners.Count <= 0) return Ent.LargeNobodyGrid;

                var enemy = GridEnemy(grid);
                if (enemy && grid.PositionComp.WorldVolume.Radius < 6.5) return Ent.SmallEnemyGrid;

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent != null && !(shieldComponent.DefenseShields.ShieldActive) && enemy) return Ent.LargeEnemyGrid;
                if (shieldComponent != null && Entity.EntityId > shieldComponent.DefenseShields.Entity.EntityId) return Ent.Shielded;
                if (shieldComponent != null) return Ent.Ignore; //only process the higher EntityID
                return enemy ? Ent.LargeEnemyGrid : Ent.Friend;
            }

            if (ent is IMyMeteor || ent.ToString().Contains("Missile")) return Ent.Other;
            if (ent is IMyVoxelMap && GridIsMobile) return Ent.VoxelMap;
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

        private bool MovingCheck(IMyEntity ent)
        {
            float bVelSqr = 0;
            float bAvelSqr = 0;
            if (ent.Physics.IsMoving)
            {
                bVelSqr = ent.Physics.LinearVelocity.LengthSquared();
                bAvelSqr = ent.Physics.AngularVelocity.LengthSquared();
            }
            else if (!_shieldMoving) return false;
            //Log.Line($"{sVelSqr} {sAvelSqr} {bVelSqr} {bAvelSqr}");
            return _shieldMoving || bVelSqr > 0.00001 || bAvelSqr > 0.00001;
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
                if ((webent is IMyCubeGrid && webent as IMyCubeGrid != Block.CubeGrid && GridEnemy(webent as IMyCubeGrid)) || (GridIsMobile && webent is IMyVoxelMap))
                {
                    if (webent.Physics.IsMoving || _shieldMoving)
                    {
                        //Log.Line($"test quickweb");
                        _enablePhysics = true;
                        return;
                    }
                }
                else if (webent is IMyMeteor || webent.ToString().Contains("Missile"))
                {
                    Log.Line($"test enableweb");
                    _enableWeb = true;
                    return;
                }
            }
        }

        private void WebEntities()
        {
            lock (_webEnts)
            {
                var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
                var pruneList = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);

                for (int i = 0; i < pruneList.Count; i++)
                {
                    var ent = pruneList[i];
                    if (ent == null) continue;
                    var entCenter = ent.PositionComp.WorldVolume.Center;

                    if (ent == _shield || ent as IMyCubeGrid == Block.CubeGrid || ent.Physics == null || ent.MarkedForClose || ent is IMyVoxelBase && !GridIsMobile
                        || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || _inFriendlyCache.Contains(ent) || ent.GetType().Name == "MyDebrisBase") continue;

                    EntIntersectInfo entInfo;
                    _webEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null) entInfo.LastTick = _tick;
                    else
                    {
                        var relation = EntType(ent);
                        if ((relation != Ent.Ignore || relation != Ent.Friend) && CustomCollision.PointInShield(entCenter, _detectionInsideInv) == false)
                            _webEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, Vector3D.NegativeInfinity, _tick, _tick, relation, false, new List<IMySlimBlock>()));
                        else if (relation == Ent.Friend || relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid) _inFriendlyCache.Add(ent);
                    }
                }

                foreach (var webent in _webEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = _webEnts[webent];
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (_webEnts[webent].Relation == Ent.LargeNobodyGrid || _webEnts[webent].Relation == Ent.LargeEnemyGrid)) ((IMyCubeGrid)webent).GetBlocks(_webEnts[webent].CacheBlockList, Collect);
                    switch (_webEnts[webent].Relation)
                    {
                        case Ent.EnemyPlayer:
                            {
                                if (_count == 2 || _count == 17 || _count == 32 || _count == 47 && CustomCollision.PointInShield(entCenter, _detectMatrixInv))
                                    MyAPIGateway.Parallel.Start(() => PlayerIntersect(webent));
                                continue;
                            }
                        case Ent.SmallNobodyGrid:
                            {
                                //MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                                SmallGridIntersect(webent as IMyCubeGrid);
                                continue;
                            }
                        case Ent.LargeNobodyGrid:
                            {

                                //MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                GridIntersect(webent);
                                continue;
                            }
                        case Ent.SmallEnemyGrid:
                            {
                                //MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                                SmallGridIntersect(webent as IMyCubeGrid);
                                continue;
                            }
                        case Ent.LargeEnemyGrid:
                            {
                                //Log.Line($"enemy large grid");
                                //MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                GridIntersect(webent);
                                continue;
                            }
                        case Ent.Shielded:
                            {
                                //Log.Line($"enemy shield grid");
                                MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent as IMyCubeGrid));
                                continue;
                            }
                        case Ent.Other:
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
                        case Ent.VoxelMap:
                            {
                                MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as IMyVoxelMap));
                                continue;
                            }
                        default:
                            continue;
                    }
                }
            }
        }

        private static bool Collect(IMySlimBlock mySlimBlock)
        {
            //if (!(mySlimBlock.Mass < 80 || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel) || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel) || mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_ButtonPanel))) Log.Line($"{mySlimBlock.BlockDefinition.DisplayNameText} {mySlimBlock.Mass} {mySlimBlock.BlockDefinition.Id}");

            return mySlimBlock.Mass > 80 || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel)) || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_TextPanel)) || !(mySlimBlock.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_ButtonPanel));
        }

        private void SyncThreadedEnts()
        {
            try
            {
                if (Eject.Count != 0)
                {
                    foreach (var e in Eject) e.Key.SetPosition(Vector3D.Lerp(e.Key.GetPosition(), e.Value, 0.25d));
                    Eject.Clear();
                }

                var destroyedLen = _destroyedBlocks.Count;
                if (destroyedLen != 0)
                {
                    lock (_webEnts)
                    {
                        IMySlimBlock block;
                        var nullCount = 0;
                        while (_destroyedBlocks.TryDequeue(out block))
                        {
                            EntIntersectInfo entInfo;
                            _webEnts.TryGetValue(block.CubeGrid, out entInfo);
                            if (entInfo == null)
                            {
                                nullCount++;
                                continue;
                            }
                            if (nullCount > 0)
                            {
                                Log.Line($"cleaning blocks");
                                _webEnts.Remove(block.CubeGrid);
                            }
                            entInfo.CacheBlockList.Remove(block);
                        }
                    }
                }
                //_dsutil1.Sw.Start();
                //Log.Line($"{FewDmgBlocks.Count} {DmgBlocks.Count}");
                if (_impactSize > 0) Log.Line($"{_impactSize}");
                if (_fewDmgBlocks.Count != 0)
                {
                    var c = _fewDmgBlocks.Count;
                    IMySlimBlock block;
                    while (_fewDmgBlocks.TryDequeue(out block))
                    {
                        if (block == null || block.IsDestroyed) continue;

                        //block.OnDestroy();
                        block.DoDamage(5000f, MyDamageType.Explosion, true, null, Block.CubeGrid.EntityId);

                        if (c == 1)
                        {
                            var myCube = (MyCubeGrid) block.CubeGrid;
                            if (myCube.BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                        }
                        /*
                        if (c == 5)
                        {
                            Vector3D center;
                            block.ComputeWorldCenter(out center);
                            MyVisualScriptLogicProvider.CreateExplosion(center, 10f, 1500);
                        }
                        */
                        c--;
                    }
                }
                if (_dmgBlocks.Count != 0)
                {
                    var c = _dmgBlocks.Count;
                    IMySlimBlock block;
                    while (_dmgBlocks.TryDequeue(out block))
                    {
                        if (block == null || block.IsDestroyed) continue;

                        //block.OnDestroy();
                        block.DoDamage(5000f, MyDamageType.Explosion, true, null, Block.CubeGrid.EntityId);

                        if (c == 1)
                        {
                            var myCube = (MyCubeGrid)block.CubeGrid;
                            if (myCube.BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                        }
                        c--;
                        /*
                        if (c == 5)
                        {
                            Vector3D center;
                            block.ComputeWorldCenter(out center);
                            MyVisualScriptLogicProvider.CreateExplosion(center, 10f, 1500);
                        }
                        */
                    }
                }
                _impactSize = 0;
                //_dsutil1.StopWatchReport("block damage", 1);
            }
            catch (Exception ex) { Log.Line($"Exception in DamageGrids: {ex}"); }
        }
        #endregion

        #region Intersect
        private bool GridInside(IMyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, _detectionInsideInv))
            {
                if (CustomCollision.AllCornersInShield(bOriBBoxD, _detectMatrixInv)) return true;

                var ejectDir = CustomCollision.EjectDirection(grid, _physicsOutside, _dataStructures.p3VertTris, bOriBBoxD, _detectMatrixInv);
                if (ejectDir == Vector3D.NegativeInfinity) return false;
                //Log.Line($"ejecting grid");
                Eject.Add(grid, ejectDir);
                return true;
            }
            return false;
        }

        private void SmallGridIntersect(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;

            EntIntersectInfo entInfo;
            _webEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            CustomCollision.SmallIntersect(entInfo, _fewDmgBlocks, grid, _detectMatrix, _detectMatrixInv);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                _impactSize += entInfo.Damage;
                entInfo.Damage = 0;
                _worldImpactPosition = contactpoint;
            }
        }

        private void GridIntersect(IMyEntity ent)
        {
            if (!MovingCheck(ent)) return;
            lock (_webEnts)
            {
                var grid = (IMyCubeGrid)ent;
                EntIntersectInfo entInfo;
                _webEnts.TryGetValue(ent, out entInfo);
                if (entInfo == null) return;

                var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB);
                if (GridInside(grid, bOriBBoxD)) return;

                ContactPointObb(grid, bOriBBoxD, entInfo);
                var contactpoint = entInfo.ContactPoint;
                entInfo.ContactPoint = Vector3D.NegativeInfinity;
                if (contactpoint == Vector3D.NegativeInfinity) return;

                _impactSize += entInfo.Damage;
                entInfo.Damage = 0;
                _worldImpactPosition = contactpoint;
            }
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

        private void VoxelIntersect(IMyVoxelMap voxelMap)
        {
            var center = Block.CubeGrid.WorldVolume.Center;
            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(_shield.WorldAABB);
            bOriBBoxD.Center = center;
            CustomCollision.VoxelCollisionSphere(Block.CubeGrid, _physicsOutside, voxelMap, bOriBBoxD);
        }

        private void PlayerIntersect(IMyEntity ent)
        {
            var player = _webEnts[ent];
            var rnd = new Random();
            var character = MyAPIGateway.Entities.GetEntityById(player.EntId) as IMyCharacter;
            if (character == null) return;

            var playerid = character.EntityId;
            var npcname = character.ToString();
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

            _webEnts.Remove(MyAPIGateway.Entities.GetEntityById(player.EntId));

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;

            character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hydrogenId, (playerGasLevel * -0.0001f) + .002f);
            MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
            character.DoDamage(50f, MyDamageType.Fire, true);
            var vel = character.Physics.LinearVelocity;
            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
            var speedDir = Vector3D.Normalize(vel);
            var randomSpeed = rnd.Next(10, 20);
            var additionalSpeed = vel + speedDir * randomSpeed;
            character.Physics.LinearVelocity = additionalSpeed;
        }

        private void ContactPointObb(IMyEntity breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
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
            /*
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
            */
            intersections = CustomCollision.ContainPointObb(_physicsOutside, bOriBBoxD, tSphere);

            var grid = breaching as IMyCubeGrid;
            if (grid == null) return;

            if (intersections.Count == 0) return;

            var locCenterSphere = DSUtils.CreateFromPointsList(intersections);
            var collision = Vector3D.Lerp(GridIsMobile ? Block.PositionComp.WorldVolume.Center : Block.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);

            try
            {
                if (collision != Vector3D.NegativeInfinity)
                {
                    //dsutil.Sw.Start();
                    var c = 0;
                    var cacheBlockList = entInfo.CacheBlockList;
                    if (cacheBlockList.Count != 0)
                    {
                        var sCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;
                        var gridDetectMatrix = MatrixD.CreateScale(grid.GridSize) * grid.PositionComp.WorldMatrix * _detectMatrixInv;
                        var damage = 0f;
                        for (int i = 0; i < cacheBlockList.Count; i++)
                        {
                            var block = cacheBlockList[i];
                            if (block.IsDestroyed)
                            {
                                _destroyedBlocks.Enqueue(block);
                                continue;
                            }

                            if (Vector3.Transform(block.Position, gridDetectMatrix).LengthSquared() <= 1)
                            {
                                grid.Physics.ApplyImpulse((bWorldCenter - sCenter) * grid.Physics.Mass / 200, sCenter);
                                Block.CubeGrid.Physics.ApplyImpulse((sCenter - bWorldCenter) * Block.CubeGrid.Physics.Mass / 200, bWorldCenter);

                                if (_dmgBlocks.Count > 50) continue;
                                damage += block.Mass;
                                _dmgBlocks.Enqueue(block);
                                c++;
                            }
                        }
                        entInfo.Damage = damage;
                        entInfo.ContactPoint = collision;
                    }
                    //dsutil.StopWatchReport("obb", -1);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}");}
        }

        private double PowerCalculation(IMyEntity breaching, IMyEntity field)
        {
            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = field.Physics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = field.Physics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = breaching.Physics.LinearVelocity;
            var linearImpulse = breaching.Physics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
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
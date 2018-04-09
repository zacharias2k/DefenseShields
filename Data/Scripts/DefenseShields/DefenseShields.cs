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
using System.Threading;
using DefenseShields.Control;
using VRage.Collections;
using Sandbox.Game.Entities.Character.Components;
using DefenseShields.Support;
using Sandbox.Game.Entities;

public static class MathematicalConstants
{
    public const double SQRT2 = 1.414213562373095048801688724209698078569671875376948073176679737990732478462107038850387534327641573;
    public const double SQRT3 = 1.7320508075689d;
}

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "DefenseShieldsLS", "DefenseShieldsSS", "DefenseShieldsST")]
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
        private int _animationLoop;
        private int _explodeCount;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;

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
        internal bool EmitterActive;
        internal bool BlockWorking;

        private bool _shieldMoving = true;
        private bool _blockParticleStopped;
        private bool _shieldLineOfSight = false;
        private bool _shieldCheckSight = true;
        private bool _prevShieldActive;
        private bool _shieldStarting;

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

        private Matrix _oldLocalMatrix;

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectMatrix;
        private MatrixD _detectMatrixInv;
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectionInsideInv;
        private MatrixD _mobileMatrix;

        private BoundingBox _oldGridAabb;
        private BoundingBoxD _shieldAABB;

        private BoundingSphereD _shieldSphere;

        public IMyOreDetector Shield => (IMyOreDetector)Entity;
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
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _visablilityCheckBox;

        private MyResourceSinkComponent _sink;

        private readonly MyDefinitionId _powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private MyConcurrentHashSet<IMyEntity> _friendlyCache = new MyConcurrentHashSet<IMyEntity>();
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
            Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));

        }
        #endregion

        #region Prep / Misc
        private void BuildPhysicsArrays()
        {
            Log.Line($"building arrays");
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _physicsOutside);
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _rootVecs);
            _icosphere.ReturnPhysicsVerts(_detectMatrixInside, _physicsInside);
            //_structureBuilder.BuildTriNums(_icosphere.CalculatePhysics(_detectMatrixOutside, 3), _physicsOutside);
            //if (_buildOnce == false) _structureBuilder.BuildBase(_icosphere.CalculatePhysics(_detectMatrixOutside, 3), _rootVecs, _physicsOutside, _buildLines, _buildTris, _buildVertZones, _buildByVerts);
            //_buildOnce = true;
        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                //Log.Line($"{BlockWorking} {ShieldActive} {EmitterActive} {Block.IsWorking}  {Block.IsFunctional} {Initialized}");
                if (!Initialized && Shield.IsWorking) return;
                if ((!Shield.IsWorking || !Shield.IsFunctional) && ShieldActive)
                {
                    BlockParticleStop();
                    ShieldActive = false;
                    BlockWorking = false;
                    EmitterActive = false;
                    return;
                }

                BlockWorking = Initialized && Shield.IsWorking;
                if (_firstRun)
                {
                    if (BlockWorking)
                    {
                        _icosphere.ReturnPhysicsVerts(Shield.WorldMatrix, _physicsOutside);
                        _firstRun = false;
                    }
                    else return;
                }
                else if (!Shield.IsFunctional) return;

                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10)
                    {
                        foreach (var i in _webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1).ToList())
                            _webEnts.Remove(i.Key);
                        _friendlyCache.Clear();
                        _longLoop = 0;
                    }
                }

                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Shield.GameLogic.GetAs<DefenseShields>().Sink.Update();
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel) // ugly workaround for realtime terminal updates
                    {
                        Shield.ShowInToolbarConfig = false;
                        Shield.ShowInToolbarConfig = true;
                    }
                }

                if (GridIsMobile)
                {
                    //_sVel = Shield.CubeGrid.Physics.LinearVelocity;
                    //_sAvel = Shield.CubeGrid.Physics.AngularVelocity;
                    _sVelSqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
                    _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
                    if (_sVelSqr > 0.00001 || _sAvelSqr > 0.00001) _shieldMoving = true;
                    else _shieldMoving = false;

                    _gridChanged = _oldGridAabb != Shield.CubeGrid.LocalAABB;
                    _oldGridAabb = Shield.CubeGrid.LocalAABB;
                    _entityChanged = Shield.CubeGrid.Physics.IsMoving || _gridChanged;
                    if (_entityChanged || Range <= 0) CreateShieldShape();
                }

                EmitterActive = BlockWorking && Range > 0 && Shield.IsFunctional;

                var blockCount = ((MyCubeGrid)Shield.CubeGrid).BlocksCount;
                if (blockCount != _oldBlockCount)
                {
                    _oldBlockCount = blockCount;
                    Log.Line($"block count changed");
                    _shieldCheckSight = true;
                }

                if (_longLoop == 0 && EmitterActive && _shieldCheckSight) CheckShieldLineOfSight();

                _dsutil2.Sw.Start();
                ShieldActive = EmitterActive && _shieldLineOfSight;
                if (_prevShieldActive == false && EmitterActive) _shieldStarting = true;
                else if (_shieldStarting && _prevShieldActive && ShieldActive) _shieldStarting = false; 
                _prevShieldActive = ShieldActive;

                //if (_longLoop == 0 && _count == 0) Log.Line($"S: {ShieldActive} - I: {Initialized} - A: {_animInit} - L: {_shieldLineOfSight} W: {Block.IsWorking} F: {Block.IsFunctional}");
                if (ShieldActive)
                {
                    if (_animInit)
                    {
                        if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
                        if (Distance(1000))
                        {
                            if (_shieldMoving || _shieldStarting) BlockParticleUpdate();
                            var blockCam = Shield.PositionComp.WorldVolume;
                            if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam))
                            {

                                if (_blockParticleStopped) BlockParticleStart();
                                _blockParticleStopped = false;
                                BlockMoveAnimation();

                                if (_animationLoop++ == 599) _animationLoop = 0;
                            }
                        }
                    }

                    SyncThreadedEnts();
                    _enablePhysics = false;
                    WebEntities();
                }
                else
                {
                    SyncThreadedEnts();
                    if (!_blockParticleStopped) BlockParticleStop();
                }
            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
            _dsutil2.StopWatchReport("main loop", 3f);
        }

        public override void UpdateAfterSimulation100()
        {
            if (!Initialized)
            {
                Log.Line($"Initting entity");
                if (Shield.CubeGrid.Physics.IsStatic) GridIsMobile = false;
                else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                CreateUi();
                Shield.AppendingCustomInfo += AppendingCustomInfo;
                Shield.RefreshCustomInfo();
                _absorb = 150f;

                _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
                _shield.Render.Visible = false;
                Initialized = true;
            }
            else if (!_animInit && !_firstRun)
            {
                if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                {
                    if (!Shield.IsFunctional) return;
                    _shieldCheckSight = true;
                    Log.Line($"block is functional");
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    BlockParticleCreate();
                    _animInit = true;
                }
                else NeedsUpdate = MyEntityUpdateEnum.NONE;
            }
        }
        #endregion

        #region Block Power and Entity Config Logic
        private float CalcRequiredPower()
        {
            if (!Initialized || !Shield.IsWorking) return _power;
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
            var width = _widthSlider.Getter(Shield);
            var height = _heightSlider.Getter(Shield);
            var depth = _depthSlider.Getter(Shield);
            var oWidth = _width;
            var oHeight = _height;
            var oDepth = _depth;
            _width = width;
            _height = height;
            _depth = depth;
            var changed = (int)oWidth != (int)width || (int)oHeight != (int)height || (int)oDepth != (int)depth;

            if (!changed) return;
            CreateShieldShape();
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

        private void CheckShieldLineOfSight()
        {
            _shieldCheckSight = false;

            //Log.Line($"sight check for {Block.EntityId.ToString()} on tick {_tick.ToString()} block localPos: {Block.Position} block worldPos: {Block.GetPosition()}");
            var c = 0;
            MyAPIGateway.Parallel.For(0, _physicsOutside.Length, i =>
            {
                //Subpart.WorldMatrix * Grid.WorldMatrixNormalizedInv
                //Vector3D.Transform(Subpart.PositionComp.WorldAABB.Center, Grid.WorldMatrixNormalizedInv)
                //Log.Line($"{_physicsOutside[i]} {Shield.GetPosition()} {Shield.Position}");
                var hit = Shield.CubeGrid.RayCastBlocks(_physicsOutside[i], Shield.GetPosition());
                if (hit.HasValue && hit.Value != Shield.Position) Interlocked.Increment(ref c);
                //if (hit.HasValue && hit.Value != Shield.Position) DsDebugDraw.DrawLineToVec(hit.Value, Shield.GetPosition(), Color.Black);
            });

            Log.Line($"number of hits {c.ToString()}");
            _shieldLineOfSight = c < 610;
        }

        private void CreateShieldShape()
        {
            if (GridIsMobile)
            {
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_gridChanged) CreateMobileShape();
                DetectionMatrix = _mobileMatrix * _shieldGridMatrix;
                _detectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                //_shield.SetLocalMatrix(_mobileMatrix);
                _shieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.Max());
                _shieldAABB = BoundingBoxD.CreateFromSphere(_shieldSphere);
            }
            else
            {
                _shieldGridMatrix = Shield.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                //_shield.SetLocalMatrix(MatrixD.Rescale(Shield.LocalMatrix, new Vector3D(_width, _height, _depth)));
                _detectionCenter = Shield.PositionComp.WorldVolume.Center;
                ShieldSize = DetectionMatrix.Scale;
                _shieldSphere = new BoundingSphereD(Shield.PositionComp.LocalVolume.Center, ShieldSize.Max());
                _shieldAABB = BoundingBoxD.CreateFromSphere(_shieldSphere);
            }
            Range = (float)_detectMatrix.Scale.AbsMax() + 15f;
            SetShieldShape();
        }

        private void CreateMobileShape()
        {
            Vector3D gridHalfExtents = Shield.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const double ellipsoidAdjust = MathematicalConstants.SQRT2;
            const double buffer = 2.5d;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            ShieldSize = shieldSize;
            var gridLocalCenter = Shield.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize) * MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            _mobileMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            if (Shield.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Shield.LocalMatrix, new Vector3D(_width, _height, _depth));
                //_shield.WorldVolume.Transform(_shieldShapeMatrix * Shield.WorldMatrix);
            }
            if (!_entityChanged || Shield.CubeGrid.Physics.IsStatic) return;

            _shieldShapeMatrix = _mobileMatrix;
            //_shield.SetWorldMatrix(_shieldShapeMatrix * Shield.CubeGrid.WorldMatrix);
            //_shield.WorldVolume.Transform(_shieldShapeMatrix * Shield.WorldMatrix);
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

            _visablilityCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "Visability", "Hide Shield From Allied", true);

            //if (Block.BlockDefinition.SubtypeId == "DefenseShieldsST")
            //{
            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 10, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 300, 100);
            //}
        }
        #endregion

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            Log.Line($"Resetting BlockMovement in loop {_count.ToString()}");
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            _time -= 1;
            if (_animationLoop == 0) _time2 = 0;
            if (_animationLoop < 299) _time2 += 1;
            else _time2 -= 1;
            if (_count == 0) _emissiveIntensity = 2;
            if (_count < 30) _emissiveIntensity += 1;
            else _emissiveIntensity -= 1;
                
            var temp1 = MatrixD.CreateRotationY(0.05f * _time);
            var temp2 = MatrixD.CreateTranslation(0, 0.002f * _time2, 0);
            _subpartRotor.PositionComp.LocalMatrix = temp1 * temp2;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }

        private void BlockParticleCreate()
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] == null)
                {
                    Log.Line($"null");
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = 10f;
                    _effects[i].UserEmitterScale = 1f;
                }

                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
                    _effects[i].Stop();
                    _blockParticleStopped = true;
                }
            }
        }

        private void BlockParticleUpdate()
        {
            //_dsutil3.Sw.Start();
            var predictedMatrix = Shield.PositionComp.WorldMatrix;
            if (_sVelSqr > 4000) predictedMatrix.Translation = Shield.PositionComp.WorldMatrix.Translation + Shield.CubeGrid.Physics.GetVelocityAtPoint(Shield.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            for (int i = 0; i < _effects.Length; i++) if (_effects[i] != null) _effects[i].WorldMatrix = predictedMatrix;
            //_dsutil3.StopWatchReport("predict", -1);
        }

        private void BlockParticleStop()
        {
            Log.Line($"Particle Stop");
            _blockParticleStopped = true;
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                {
                    _effects[i].Stop();
                    _effects[i].Close(false, true);
                }
            }

        }

        private void BlockParticleStart()
        {
            Log.Line($"Particle Start");
            for (int i = 0; i < _effects.Length; i++)
            {
                if (!_effects[i].IsStopped) continue;

                MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                _effects[i].UserScale = 1f;
                _effects[i].UserRadiusMultiplier = 10f;
                _effects[i].UserEmitterScale = 1f;
                BlockParticleUpdate();
            }
        }
        #endregion

        #region Shield Draw
        public void Draw(int onCount, bool sphereOnCamera)
        {
            //if (!ShieldActive) return;
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            _enemy = enemy;
            var visable = !_visablilityCheckBox.Getter(Shield).Equals(true) && !enemy;

            var impactPos = _worldImpactPosition;
            if (impactPos != Vector3D.NegativeInfinity)
            {
                var cubeBlockLocalMatrix = Shield.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                impactPos = localPosition;
            }
            _localImpactPosition = impactPos;
            _worldImpactPosition = Vector3D.NegativeInfinity;

            if (Shield.IsWorking) PrepareSphere();
            if (sphereOnCamera && Shield.IsWorking) _icosphere.Draw(GetRenderId(), visable);
        }

        private void PrepareSphere()
        {
            var prevlod = _prevLod;
            var lod = CalculateLod(_onCount);
            if (_gridChanged || lod != prevlod) _icosphere.CalculateTransform(_shieldShapeMatrix, lod);
            _icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _impactSize, _entityChanged, _enemy, _shield, prevlod);
            _entityChanged = false;
        }

        #endregion

        #region Shield Draw Prep
        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Shield.CubeGrid.PositionComp.GetPosition();
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
            var renderId = Shield.CubeGrid.Render.GetRenderObjectID(); 
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
                var playerrelationship = Shield.GetUserRelationToOwner((long)dude);
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
            var relationship = Shield.GetUserRelationToOwner(owners[0]);
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
        private void WebEntities()
        {

            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);

            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                if (ent == null) continue;
                var entCenter = ent.PositionComp.WorldVolume.Center;

                if (ent == _shield || ent as IMyCubeGrid == Shield.CubeGrid || ent.Physics == null || ent.MarkedForClose || ent is IMyVoxelBase && !GridIsMobile
                    || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || _friendlyCache.Contains(ent) || ent.GetType().Name == "MyDebrisBase") continue;

                var relation = EntType(ent);
                if (relation == Ent.Ignore || relation == Ent.Friend)
                {
                    _friendlyCache.Add(ent);
                    continue;
                }
                if (relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid && CustomCollision.PointInShield(entCenter, _detectionInsideInv))
                {
                    _friendlyCache.Add(ent);
                    continue;
                }

                _enablePhysics = true;
                lock (_webEnts)
                {
                    EntIntersectInfo entInfo;
                    _webEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null) entInfo.LastTick = _tick;
                    else
                    {
                         _webEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, Vector3D.NegativeInfinity, _tick, _tick, relation, false, new List<IMySlimBlock>()));
                    }
                }
            }

            if (_enablePhysics || _shieldMoving || _gridChanged)
            {
                _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _physicsOutside);
                _icosphere.ReturnPhysicsVerts(_detectMatrixInside, _physicsInside);
            }
            if (_enablePhysics) MyAPIGateway.Parallel.Start(WebDispatch);
            //if (_enablePhysics) WebDispatch();
        }

        private void WebDispatch()
        {
            lock(_webEnts)
            {
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
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                                //SmallGridIntersect(webent as IMyCubeGrid);
                                continue;
                            }
                        case Ent.LargeNobodyGrid:
                            {

                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                //GridIntersect(webent);
                                continue;
                            }
                        case Ent.SmallEnemyGrid:
                            {
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent as IMyCubeGrid));
                                //SmallGridIntersect(webent as IMyCubeGrid);
                                continue;
                            }
                        case Ent.LargeEnemyGrid:
                            {
                                //Log.Line($"enemy large grid");
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                //GridIntersect(webent);
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
                    foreach (var e in Eject) e.Key.SetPosition(Vector3D.Lerp(e.Key.GetPosition(), e.Value, 0.1d));
                    Eject.Clear();
                }

                var destroyedLen = _destroyedBlocks.Count;
                try
                {
                    if (destroyedLen != 0)
                    {
                        lock (_webEnts)
                        {
                            IMySlimBlock block;
                            var nullCount = 0;
                            while (_destroyedBlocks.TryDequeue(out block))
                            {
                                if (block?.CubeGrid == null) continue;
                                EntIntersectInfo entInfo;
                                _webEnts.TryGetValue(block.CubeGrid, out entInfo);
                                if (entInfo == null)
                                {
                                    nullCount++;
                                    ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                    continue;
                                }
                                if (nullCount > 0) _webEnts.Remove(block.CubeGrid);
                                entInfo.CacheBlockList.Remove(block);
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in destroyedBlocks: {ex}"); }

                try
                {
                    if (_fewDmgBlocks.Count != 0)
                    {
                        //var c = _fewDmgBlocks.Count;
                        IMySlimBlock block;
                        while (_fewDmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }

                            block.DoDamage(5000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set sync to true for multiplayer?
                            //if (((MyCubeGrid)block.CubeGrid).BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                            /*
                            if (c == 5)
                            {
                                Vector3D center;
                                block.ComputeWorldCenter(out center);
                                MyVisualScriptLogicProvider.CreateExplosion(center, 10f, 1500);
                            }
                            */
                            //c--;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in fewBlocks: {ex}"); }


                try
                {
                    if (_dmgBlocks.Count != 0)
                    {
                        //var c = _dmgBlocks.Count;
                        IMySlimBlock block;
                        while (_dmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            block.DoDamage(5000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set sync to true for multiplayer?
                            if (((MyCubeGrid)block.CubeGrid).BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
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
                }
                catch (Exception ex) { Log.Line($"Exception in dmgBlocks: {ex}"); }
                _impactSize = 0;
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
                //if (_impactSize > 0) Log.Line($"small - tick: {_tick.ToString()} - {_impactSize.ToString()}");

                entInfo.Damage = 0;
                _worldImpactPosition = contactpoint;
            }
        }

        private void GridIntersect(IMyEntity ent)
        {
            lock (_webEnts)
            {
                var grid = (IMyCubeGrid)ent;
                EntIntersectInfo entInfo;
                _webEnts.TryGetValue(ent, out entInfo);
                if (entInfo == null) return;

                var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB);
                if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD)) return;

                BlockIntersect(grid, bOriBBoxD, entInfo);
                var contactpoint = entInfo.ContactPoint;
                entInfo.ContactPoint = Vector3D.NegativeInfinity;
                if (contactpoint == Vector3D.NegativeInfinity) return;

                _impactSize += entInfo.Damage;
                //if (_impactSize > 0) Log.Line($"large - tick: {_tick.ToString()} - {_impactSize.ToString()}");

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
            var myGrid = Shield.CubeGrid;
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
            var center = Shield.CubeGrid.WorldVolume.Center;
            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(_shieldAABB);
            bOriBBoxD.Center = center;
            CustomCollision.VoxelCollisionSphere(Shield.CubeGrid, _physicsOutside, voxelMap, bOriBBoxD);
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

        private void BlockIntersect(IMyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            /*

            var bLocalAabb = breaching.PositionComp.LocalAABB;
            var bWorldAabb = breaching.PositionComp.WorldAABB;
            var bMatrix = breaching.PositionComp.WorldMatrix;
            var bWorldCenter = bWorldAabb.Center;
            var gridScaler = (float)(((_detectMatrix.Scale.X + _detectMatrix.Scale.Y + _detectMatrix.Scale.Z) / 3 / lodScaler) * 1.33) / bLocalAabb.Extents.Min();
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
            var sCenter = Shield.CubeGrid.WorldVolume.Center;
            var lodScaler = (int)Math.Pow(2, PhysicsLod);
            var faceTri = new int[4];
            var rangedVerts = new int[3];
                        var intersections = new List<Vector3D>();
            intersections = CustomCollision.ContainPointObb(_physicsOutside, bOriBBoxD, tSphere);

            if (intersections.Count == 0) return;
            var locCenterSphere = DSUtils.CreateFromPointsList(intersections);
            var collision = Vector3D.Lerp(GridIsMobile ? Shield.PositionComp.WorldVolume.Center : Shield.CubeGrid.PositionComp.WorldVolume.Center, locCenterSphere.Center, .9);
            */
            var dsutil = new DSUtils();
            var tSphere = breaching.WorldVolume;
            var box = new BoundingBoxD(-Vector3D.One, Vector3D.One);
            var sOriBBoxD = MyOrientedBoundingBoxD.Create(box, DetectionMatrix);
            //var sphere = new BoundingSphereD(_shieldSphere.Center, _shieldSphere.Radius);
            //sphere.Center = _detectionCenter;
            //DsDebugDraw.DrawSphere(sphere, Color.Blue);
            var intersection = bOriBBoxD.Intersects(ref sOriBBoxD);
            try
            {
                if (intersection)
                {
                    if (_count == 0) dsutil.Sw.Start();
                    //Log.Line($"intersection");
                    var cacheBlockList = entInfo.CacheBlockList;
                    var bPhysics = breaching.Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sAngVel = sPhysics.AngularVelocity;
                    var bAngVel = bPhysics.AngularVelocity;
                    var sVel = sPhysics.LinearVelocity;
                    var bVel = bPhysics.LinearVelocity;
                    var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
                    var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);
                    var bBlockCenter = Vector3D.NegativeInfinity;
                    var gridInShieldWorld = MatrixD.CreateScale(breaching.GridSize) * breaching.PositionComp.WorldMatrix * _detectMatrixInv;

                    var damage = 0f;
                    Vector3I gc = breaching.WorldToGridInteger(_detectionCenter);
                    double rc = _shieldSphere.Radius / breaching.GridSize;
                    rc *= rc;
                    var c1 = 0;
                    var c2 = 0;
                    var c3 = 0;
                    var c4 = 0;
                    var c5 = 0;
                    var c6 = 0;
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        Vector3I blockPos = block.Position;
                        int num1 = gc.X - blockPos.X;
                        int num2 = gc.Y - blockPos.Y;
                        int num3 = gc.Z - blockPos.Z;
                        int result = num1 * num1 + num2 * num2 + num3 * num3;
                        /*
                        if (result - 9 > rc && Vector3.Transform(block.Position, gridInShieldWorld).LengthSquared() <= 1)
                        {
                            Log.Line($"false negative: result: {result.ToString()} - rc: {rc.ToString()} - Transform:{Vector3.Transform(block.Position, gridInShieldWorld).LengthSquared().ToString()} - block:{block.BlockDefinition.Id.TypeId}");
                            c5++;
                        }
                        */
                        if (result - 8 > rc) continue;
                        c1++;
                        if (block.IsDestroyed)
                        {
                            c6++;
                            _destroyedBlocks.Enqueue(block);
                            continue;
                        }
                        c2++;
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);
                        Vector3D[] blockPoints = new Vector3D[9];

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;
                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, _detectMatrixInv).LengthSquared() > 1) continue;
                            if (bBlockCenter == Vector3D.NegativeInfinity) bBlockCenter = point;
                            bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, breaching.Physics.CenterOfMassWorld);
                            sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, Shield.CubeGrid.Physics.CenterOfMassWorld);

                            if (_dmgBlocks.Count > 50) break;
                            c4++;
                            damage += block.Mass;
                            _dmgBlocks.Enqueue(block);
                            break;
                        }
                        /*
                        if (Vector3.Transform(block.Position, gridInShieldWorld).LengthSquared() <= 1)
                        {
                            c3++;
                            if (bBlockCenter != Vector3D.NegativeInfinity) block.ComputeWorldCenter(out bBlockCenter);
                            bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, breaching.PositionComp.WorldVolume.Center);
                            sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, Shield.CubeGrid.PositionComp.WorldVolume.Center);

                            if (_dmgBlocks.Count > 50) continue;
                            c4++;
                            damage += block.Mass;
                            _dmgBlocks.Enqueue(block);
                        }
                        */
                    }
                    //sPhysics.AngularVelocity = sAngVel;
                    //bPhysics.AngularVelocity = bAngVel;
                    entInfo.Damage = damage;
                    if (bBlockCenter != Vector3D.NegativeInfinity) entInfo.ContactPoint = bBlockCenter;
                    if (_count == 58) Log.Line($"[status] obb: true - blocks:{cacheBlockList.Count.ToString()} - sphered:{c1.ToString()} [{c5.ToString()}] - IsDestroyed:{c6.ToString()} not:[{c2.ToString()}] - bCenter Inside Ellipsoid:{c3.ToString()} - Damaged:{c4.ToString()}");
                    if (_count == 0) dsutil.StopWatchReport("[perform]", -1);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}");}
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
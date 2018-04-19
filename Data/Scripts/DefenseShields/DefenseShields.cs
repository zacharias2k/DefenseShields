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
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;


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
        private const ushort ModId = 50099;
        private const float Shotdmg = 1f;

        private uint _tick;

        private float _power = 0.0001f;
        internal double Range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;
        private float _impactSize;
        private float _gridMaxPower;
        private float _gridCurrentPower;
        private float _gridAvailablePower;
        private float _shieldMaxBuffer;
        private float _shieldBuffer;
        private float _shieldMaxChargeRate;
        private float _shieldChargeRate;
        private float _shieldEfficiency;
        private float _shieldCurrentPower;
        private float _shieldMaintain;

        private double _sAvelSqr;
        private double _sVelSqr;

        private int _count = -1;
        private int _longLoop;
        private int _animationLoop;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;

        internal bool MainInit;
        internal bool AnimateInit;
        internal bool GridIsMobile;
        internal bool ShieldActive;
        internal bool BlockWorking;
        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _shieldMoving = true;
        private bool _blocksChanged = true;
        private bool _blockParticleStopped;
        private bool _shieldLineOfSight;
        private bool _prevShieldActive;
        private bool _shieldStarting;
        private bool _enemy;

        internal Vector3D ShieldSize { get; set; }
        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _localImpactPosition;
        private Vector3D _detectionCenter;
        private Vector3D _sVel;
        private Vector3D _sAvel;
        private Vector3D _sightPos;

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

        private BoundingBox _oldGridAabb;
        private BoundingBox _shieldAabb;
        private BoundingSphereD _shieldSphere;
        private MyOrientedBoundingBoxD _sOriBBoxD;

        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        static MyDefinitionId gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly DataStructures _dataStructures = new DataStructures();
        private readonly StructureBuilder _structureBuilder = new StructureBuilder();
        private readonly ResourceTracker _resourceTracker = new ResourceTracker(MyResourceDistributorComponent.ElectricityId);

        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();


        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();
        public readonly MyConcurrentHashSet<IMyEntity> FriendlyCache = new MyConcurrentHashSet<IMyEntity>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private readonly MyConcurrentQueue<IMySlimBlock> _dmgBlocks  = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyCubeGrid> _staleGrids = new MyConcurrentQueue<IMyCubeGrid>();

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;

        private readonly MyParticleEffect[] _effects = new MyParticleEffect[1];
        private MyEntitySubpart _subpartRotor;

        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _chargeSlider;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _visablilityCheckBox;

        public MyResourceDistributorComponent SinkDistributor { get; set; }

        internal MyResourceSinkComponent Sink;

        public IMyOreDetector Shield => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private DSUtils _dsutil1 = new DSUtils();
        private DSUtils _dsutil2 = new DSUtils();
        private DSUtils _dsutil3 = new DSUtils();

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }

        // tem
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

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
            Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));
        }
        #endregion

        /*
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
        */

        private void AddResourceSourceComponent()
        {
            var info = new MyResourceSinkInfo()
            {
                ResourceTypeId = gId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => _power,
            };

            Entity.Components.TryGet(out Sink);
            Sink.Init(MyStringHash.GetOrCompute("Defense"), info);
            Sink.AddType(ref info);
        }

        #region Simulation
        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (AnimateInit && MainInit) return;

                if (!MainInit)
                {
                    Log.Line($"Initting {Shield.BlockDefinition.SubtypeId} - tick:{_tick.ToString()}");
                    if (Shield.CubeGrid.Physics.IsStatic) GridIsMobile = false;
                    else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                    CreateUi();

                    _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
                    _shield.Render.Visible = false;

                    AddResourceSourceComponent();
                    UpdateGridPower();
                    CalculatePowerCharge();
                    SetPower();

                    Shield.AppendingCustomInfo += AppendingCustomInfo;
                    Shield.RefreshCustomInfo();
                    MainInit = true;
                }
                //Log.Line($"{AnimateInit} {MainInit} {Shield.IsFunctional}");
                if (AnimateInit || !MainInit || !Shield.IsFunctional) return;

                if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                {
                    _blocksChanged = true;
                    Log.Line($"{Shield.BlockDefinition.SubtypeId} is functional - tick:{_tick.ToString()}");
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    BlockParticleCreate();
                    if (GridIsMobile) MobileUpdate();
                    else RefreshDimensions();
                    _icosphere.ReturnPhysicsVerts(DetectionMatrix, _physicsOutside);
                    AnimateInit = true;
                }
                else NeedsUpdate = MyEntityUpdateEnum.NONE;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation100: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            //_dsutil2.Sw.Start();
            try
            {
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                if (!BlockFunctional()) return;

                if (GridIsMobile) MobileUpdate();

                if (_longLoop == 0 && _blocksChanged)
                {
                    MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
                    CheckShieldLineOfSight();
                    _blocksChanged = false;
                }

                if (_shieldLineOfSight == false) DrawHelper();

                ShieldActive = BlockWorking && _shieldLineOfSight;
                if (_prevShieldActive == false && BlockWorking) _shieldStarting = true;
                else if (_shieldStarting && _prevShieldActive && ShieldActive) _shieldStarting = false;
                _prevShieldActive = ShieldActive;

                if (_staleGrids.Count != 0) CleanUp(0);
                if (_longLoop == 0 && _count == 0) CleanUp(1);
                if (_longLoop == 9 && _count == 58) CleanUp(2);

                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10) _longLoop = 0;
                }

                UpdateGridPower();
                CalculatePowerCharge();
                SetPower();

                if (_count == 29)
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel) // ugly workaround for realtime terminal updates
                    {

                        Shield.ShowInToolbarConfig = false;
                        Shield.ShowInToolbarConfig = true;
                    }
                }

                if (ShieldActive)
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

            //DsDebugDraw.DrawSphere(_shield.LocalVolume, Color.White);
            //DsDebugDraw.DrawBox(_sOriBBoxD, Color.Black);
            //_dsutil2.StopWatchReport("main", -1);
        }
        #endregion

        #region Block Power and Entity Config Logic

        private void BackGroundChecks()
        {
            _powerSources.Clear();

            _dsutil1.Sw.Start();

            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                var source = block.Components.Get<MyResourceSourceComponent>();
                if (source == null) continue;
                foreach (var type in source.ResourceTypes)
                {
                    if (type != MyResourceDistributorComponent.ElectricityId) continue;
                    lock (_powerSources) _powerSources.Add(source);
                    break;
                }
            }

            _dsutil1.StopWatchReport("PowerGetNew", -1);

           // Shield.CubeGrid.GetBlocks(null, CollectPowerBlocks);
            Log.Line($"powerCnt: {_powerSources.Count.ToString()}");
        }

        private bool BlockFunctional()
        {

            if (!MainInit || !AnimateInit) return false;

            if ((!Shield.IsWorking || !Shield.IsFunctional) && ShieldActive)
            {
                Log.Line($"Shield went offline - Working?: {Shield.IsWorking.ToString()} - Functional?: {Shield.IsFunctional.ToString()} - Active?: {ShieldActive.ToString()} - tick:{_tick.ToString()}");
                _shieldCurrentPower = Sink.CurrentInputByType(gId);
                UpdateGridPower();
                Log.Line($"2 - Power:{_power} - Rate:{_shieldChargeRate} - sCurrent:{_shieldCurrentPower} - sBuffer:{_shieldBuffer} - gAvail:{_gridAvailablePower} - gCurrent:{_gridCurrentPower}");

                BlockParticleStop();
                ShieldActive = false;
                BlockWorking = false;
                _absorb = 0;
                _shieldBuffer = 0;
                _shieldChargeRate = 0;
                _shieldMaxChargeRate = 0;
                _shieldEfficiency = 0;
                _shieldMaxBuffer = 0;
                return false;
            }

            var blockCount = ((MyCubeGrid)Shield.CubeGrid).BlocksCount;
            if (!_blocksChanged) _blocksChanged = blockCount != _oldBlockCount;
            _oldBlockCount = blockCount;

            BlockWorking = MainInit && AnimateInit && Shield.IsWorking && Shield.IsFunctional;
            return BlockWorking;
        }

        private void UpdateGridPower()
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;

            lock (_powerSources)
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    _gridMaxPower += _powerSources[i].MaxOutput;
                    _gridCurrentPower += _powerSources[i].CurrentOutput;
                }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
        }

        private void CalculatePowerCharge()
        {
            var powerForShield = 0f;

            const float ratio = 1.25f;
            var rate = _chargeSlider.Getter(Shield);
            var percent = rate * ratio;
            _shieldMaintain = (percent / 100) / 100;
            var fPercent = (percent / ratio) / 100;

            _shieldEfficiency = 100f;

            if (_shieldBuffer > 0 && _shieldCurrentPower < 0.001f)
            {
                if (_shieldBuffer > _gridMaxPower * _shieldMaintain) _shieldBuffer -= _gridMaxPower * _shieldMaintain;
                else _shieldBuffer = 0f;
            }

            _shieldCurrentPower = Sink.CurrentInputByType(gId);

            var otherPower = _gridMaxPower - _gridAvailablePower - _shieldCurrentPower;
            var cleanPower = _gridMaxPower - otherPower;
            powerForShield = cleanPower * fPercent;

            _shieldMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxBuffer = _gridMaxPower * (100 / percent) * 30;

            if (_shieldBuffer + _shieldMaxChargeRate < _shieldMaxBuffer) _shieldChargeRate = _shieldMaxChargeRate;
            else
            {
                if (_shieldMaxBuffer - _shieldBuffer > 0) _shieldChargeRate = _shieldMaxBuffer - _shieldBuffer;
                else _shieldMaxChargeRate = 0f;
            }

            if (_shieldMaxChargeRate < 0.001f)
            {
                _shieldChargeRate = 0f;
                if (_shieldBuffer > _shieldMaxBuffer)  _shieldBuffer = _shieldMaxBuffer;
                return;
            }
            if (_shieldBuffer < _shieldMaxBuffer && _count == 0) _shieldBuffer += _shieldChargeRate;
        }

        private void SetPower()
        {
            Log.Line($"{_shieldChargeRate} - {_gridMaxPower * _shieldMaintain} - {_shieldMaintain}");
            _power = _shieldChargeRate + _gridMaxPower * _shieldMaintain;
            Sink.Update();
            _shieldCurrentPower = Sink.CurrentInputByType(gId);

            if (_absorb > 0) _shieldBuffer -= (_absorb / _shieldEfficiency);
            if (_shieldBuffer < 0)
            {
                Log.Line($"CalcRequiredPower - buffer is neg");
                BlockFunctional();
            }
            _absorb = 0f;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var rId = MyResourceDistributorComponent.ElectricityId;
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null)
            {
                Log.Line($"Appending shield is null");
                return;
            }
            if (!GridIsMobile)RefreshDimensions();
            var shieldPercent = 100f;
            var secToFull = 0;
            if (_shieldBuffer < _shieldMaxBuffer) shieldPercent = (_shieldBuffer / _shieldMaxBuffer) * 100;
            if (_shieldChargeRate >= 1) secToFull = (int) ((_shieldMaxBuffer - _shieldBuffer) / _shieldChargeRate);
            stringBuilder.Append("[ Shield Status ] Max Mw: " + _gridMaxPower.ToString("0.00") +
                                 "\n" +
                                 "\n[Shield HP__]: " + (_shieldBuffer * _shieldEfficiency).ToString("0.0") + " (" + shieldPercent.ToString("0") + "%)" +
                                 "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                 "\n[Full Charge_]: " + secToFull.ToString("0") + "s" +
                                 "\n[Efficiency__]: " + _shieldEfficiency.ToString("0.0") +
                                 "\n" +
                                 "\n[Availabile]: " + _gridAvailablePower.ToString("0.0") + " Mw" +
                                 "\n[Current__]: " + Sink.CurrentInputByType(rId).ToString("0.0"));
        }

        private void MobileUpdate()
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

        private void CreateShieldShape()
        {

            if (GridIsMobile)
            {
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_gridChanged) CreateMobileShape();
                DetectionMatrix = _shieldShapeMatrix * _shieldGridMatrix;
                _detectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(_detectionCenter, ShieldSize.AbsMax());
            }
            else
            {
                _shieldGridMatrix = Shield.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                ShieldSize = DetectionMatrix.Scale;
                _detectionCenter = Shield.PositionComp.WorldVolume.Center;
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(_detectionCenter, ShieldSize.AbsMax());
            }
            Range = ShieldSize.AbsMax() + 7.5f;
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
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            if (Shield.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Shield.LocalMatrix, new Vector3D(_width, _height, _depth));
                _shield.SetWorldMatrix(Shield.WorldMatrix);
                _shield.LocalAABB = _shieldAabb;
                _shield.SetPosition(_detectionCenter);
                _sOriBBoxD = new MyOrientedBoundingBoxD(_shield.LocalAABB, _shield.WorldMatrix);
            }
            if (!_entityChanged || Shield.CubeGrid.Physics.IsStatic) return;

            _shield.SetWorldMatrix(Shield.CubeGrid.WorldMatrix);
            _shield.LocalAABB = _shieldAabb;
            _shield.SetPosition(_detectionCenter);
            _sOriBBoxD = new MyOrientedBoundingBoxD(_shield.LocalAABB, _shield.WorldMatrix);

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

        private void CheckShieldLineOfSight()
        {
            if (GridIsMobile) MobileUpdate();
            else RefreshDimensions();
            _icosphere.ReturnPhysicsVerts(DetectionMatrix, _physicsOutside);

            var testDist = 0d;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") testDist = 4.5d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") testDist = 2.5d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") testDist = 8.0d;

            var testDir = _subpartRotor.PositionComp.WorldVolume.Center - Shield.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = Shield.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;
            
            MyAPIGateway.Parallel.For(0, _physicsOutside.Length, i =>
            {
                var hit = Shield.CubeGrid.RayCastBlocks(testPos, _physicsOutside[i]);
                if (hit.HasValue)
                {
                    _blocksLos.Add(i);
                    return;
                }
                _noBlocksLos.Add(i);
            });
            MyAPIGateway.Parallel.For(0, _noBlocksLos.Count, i =>
            {
                const int filter = CollisionLayers.VoxelCollisionLayer;
                IHitInfo hitInfo;
                var hit = MyAPIGateway.Physics.CastRay(testPos, _physicsOutside[_noBlocksLos[i]], out hitInfo, filter);
                if (hit) _blocksLos.Add(_noBlocksLos[i]);
            });

            for (int i = 0; i < _physicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
            _shieldLineOfSight = _blocksLos.Count < 342;
            Log.Line($"blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {_shieldLineOfSight.ToString()}");
        }

        private void DrawHelper()
        {
            var lineDist = 0d;
            const float lineWidth = 0.025f;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") lineDist = 5.0d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") lineDist = 3d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") lineDist = 7.5d;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = _physicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = _physicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
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
            Log.Line($"Create UI - Tick:{_tick.ToString()}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _chargeSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ChargeRate", "Shield Charge Rate", 5, 95, 80);
            _visablilityCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "Visability", "Hide Shield From Allied", false);

            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") return;

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 10, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 300, 100);
        }
        #endregion

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
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
                    Log.Line($"Particle #{i.ToString()} is null, creating - tick:{_tick.ToString()}");
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = 10f;
                    _effects[i].UserEmitterScale = 1f;
                }

                if (_effects[i] != null)
                {
                    Log.Line($"Particle #{i.ToString()} exists, updating - tick:{_tick.ToString()}");

                    _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
                    _effects[i].Stop();
                    _blockParticleStopped = true;
                }
            }
        }

        private void BlockParticleUpdate()
        {
            var predictedMatrix = Shield.PositionComp.WorldMatrix;
            if (_sVelSqr > 4000) predictedMatrix.Translation = Shield.PositionComp.WorldMatrix.Translation + Shield.CubeGrid.Physics.GetVelocityAtPoint(Shield.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            for (int i = 0; i < _effects.Length; i++)
                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = predictedMatrix;
                }
        }

        private void BlockParticleStop()
        {
            Log.Line($"Particle Stop");
            _blockParticleStopped = true;
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                {
                    Log.Line($"Particle #{i.ToString()} active, stopping - tick:{_tick.ToString()}");

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
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            _enemy = enemy;
            var visable = !(_visablilityCheckBox.Getter(Shield).Equals(true) && !enemy);

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
            //return Shield.CubeGrid.Physics.IsStatic ? Shield.CubeGrid.Render.GetRenderObjectID() : Shield.CubeGrid.Render.GetRenderObjectID();
            return Shield.CubeGrid.Render.GetRenderObjectID();

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
                if (((MyCubeGrid)grid).BlocksCount < 3 && grid.BigOwners.Count == 0) return Ent.SmallNobodyGrid;
                if (grid.BigOwners.Count <= 0) return Ent.LargeNobodyGrid;

                var enemy = GridEnemy(grid);
                if (enemy && ((MyCubeGrid)grid).BlocksCount < 3) return Ent.SmallEnemyGrid;

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent != null && !enemy) return Ent.Friend;
                if (shieldComponent != null && !shieldComponent.DefenseShields.ShieldActive) return Ent.LargeEnemyGrid;
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
            return _shieldMoving || bVelSqr > 0.00001 || bAvelSqr > 0.00001;
        }
        #endregion

        #region Web and Sync Entities
        private void WebEntities()
        {
            //_dsutil1.Sw.Start();
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);

            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                if (ent == null) continue;
                var entCenter = ent.PositionComp.WorldVolume.Center;

                if (ent == _shield || ent as IMyCubeGrid == Shield.CubeGrid || ent.Physics == null || ent.MarkedForClose || ent is IMyVoxelBase && !GridIsMobile
                    || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || FriendlyCache.Contains(ent) || ent.GetType().Name == "MyDebrisBase") continue;

                var relation = EntType(ent);
                if (relation == Ent.Ignore || relation == Ent.Friend)
                {
                    FriendlyCache.Add(ent);
                    continue;
                }

                _enablePhysics = true;
                lock (_webEnts)
                {
                    EntIntersectInfo entInfo;
                    _webEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
                        entInfo.LastTick = _tick;
                        if (entInfo.SpawnedInside) FriendlyCache.Add(ent);
                    }
                    else
                    {
                        var inside = false;
                        if ((relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid || relation == Ent.Other) && CustomCollision.AllAabbInShield(((IMyEntity) ent).WorldAABB, _detectMatrixInv))
                        {
                            inside = true;
                            FriendlyCache.Add(ent);
                        }
                        _webEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, Vector3D.NegativeInfinity, _tick, _tick, relation, inside, new List<IMySlimBlock>()));
                    }
                }
            }
            if (_enablePhysics || _shieldMoving || _gridChanged)
            {
                _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _physicsOutside);
                _icosphere.ReturnPhysicsVerts(_detectMatrixInside, _physicsInside);
            }
            if (_enablePhysics) MyAPIGateway.Parallel.Start(WebDispatch);
            //_dsutil1.StopWatchReport("web", -1);
        }

        private void WebDispatch()
        {
            lock(_webEnts)
            {
                foreach (var webent in _webEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = _webEnts[webent];
                    //Log.Line($"ent {webent.GetType().Name} {_webEnts[webent].Relation} {_webEnts[webent].SpawnedInside} {webent.DisplayName}");
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (_webEnts[webent].Relation == Ent.LargeNobodyGrid || _webEnts[webent].Relation == Ent.LargeEnemyGrid)) ((IMyCubeGrid)webent).GetBlocks(_webEnts[webent].CacheBlockList, CollectCollidableBlocks);
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
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                //GridIntersect(webent);
                                continue;
                            }
                        case Ent.Shielded:
                            {
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

        private static bool CollectCollidableBlocks(IMySlimBlock mySlimBlock)
        {
            return mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_TextPanel) 
                   && mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_ButtonPanel) 
                   && mySlimBlock.BlockDefinition.Id.SubtypeId != MyStringHash.TryGet("SmallLight");
        }

        private void CleanUp(int task)
        {
            switch (task)
            {
                case 0:
                    IMyCubeGrid grid;
                    while (_staleGrids.TryDequeue(out grid)) _webEnts.Remove(grid);
                    Log.Line($"Stale grid - tick:{_tick.ToString()}");
                    break;
                case 1:
                    if (Shield.CubeGrid.Physics.IsStatic && Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS")
                    {
                        MyVisualScriptLogicProvider.ShowNotification("Station shields are not allowed on SHIPS.", 6000, "Red", 0);
                        Shield.Enabled = false;
                    }
                    if (!Shield.CubeGrid.Physics.IsStatic && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                    {
                        MyVisualScriptLogicProvider.ShowNotification("Large Ship shields are not allowed on Stations.", 6000, "Red", 0);
                        Shield.Enabled = false;
                    }

                    break;
                case 2:
                    //var cnt = _webEnts.Count;
                    //Log.Line($"webentCache Count: {_webEnts.Count.ToString()} - FriendCache Count: {FriendlyCache.Count.ToString()} - tick:{_tick.ToString()}");
                    foreach (var i in _webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1).ToList())
                        _webEnts.Remove(i.Key);
                    FriendlyCache.Clear();
                    //Log.Line($"webentCache Cleaned: {(cnt - _webEnts.Count).ToString()} - tick:{_tick.ToString()}");
                    break;
            }
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
                        IMySlimBlock block;
                        while (_fewDmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }

                            block.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set sync to true for multiplayer?
                            if (((MyCubeGrid)block.CubeGrid).BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
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
                        IMySlimBlock block;
                        while (_dmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            block.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set sync to true for multiplayer?
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
                _absorb += entInfo.Damage;
                _impactSize += entInfo.Damage;

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
            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(_shieldAabb);
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
            //var tSphere = breaching.WorldVolume;
            var box = new BoundingBoxD(-Vector3D.One, Vector3D.One);
            var sOriBBoxD = MyOrientedBoundingBoxD.Create(box, DetectionMatrix);

            var collisionAvg = Vector3D.Zero;
            var transformInv = _detectMatrixInv;
            var normalMat = MatrixD.Transpose(transformInv);
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
                    //var sAngVel = sPhysics.AngularVelocity;
                    //var bAngVel = bPhysics.AngularVelocity;
                    //var sVel = sPhysics.LinearVelocity;
                    //var bVel = bPhysics.LinearVelocity;
                    var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
                    var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);
                    var bBlockCenter = Vector3D.NegativeInfinity;
                    //var gridInShieldWorld = MatrixD.CreateScale(breaching.GridSize) * breaching.PositionComp.WorldMatrix * _detectMatrixInv;

                    var stale = false;
                    var damage = 0f;
                    Vector3I gc = breaching.WorldToGridInteger(_detectionCenter);
                    double rc = ShieldSize.AbsMax() / breaching.GridSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var c1 = 0;
                    var c2 = 0;
                    var c3 = 0;
                    var c4 = 0;
                    var c5 = 0;
                    var c6 = 0;
                    Vector3D[] blockPoints = new Vector3D[9];
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        Vector3I blockPos = block.Position;
                        int num1 = gc.X - blockPos.X;
                        int num2 = gc.Y - blockPos.Y;
                        int num3 = gc.Z - blockPos.Z;
                        int result = num1 * num1 + num2 * num2 + num3 * num3;
                        /*
                        if (result > rc && Vector3.Transform(block.Position, gridInShieldWorld).LengthSquared() <= 1)
                        {
                            Log.Line($"false negative: result: {result.ToString()} - rc: {rc.ToString()} - " +
                                     $"Transform:{Vector3.Transform(block.Position, gridInShieldWorld).LengthSquared().ToString()} - " +
                                     $"block: Name:{block.BlockDefinition.Id.SubtypeId} Mass:{block.Mass}");
                            c5++;
                        }
                        */
                        if (result > rc) continue;
                        c1++;
                        if (block.IsDestroyed)
                        {
                            c6++;
                            _destroyedBlocks.Enqueue(block);
                            continue;
                        }
                        if (block.CubeGrid != breaching)
                        {
                            if (!stale) _staleGrids.Enqueue(breaching);
                            stale = true;
                            continue;
                        }
                        c2++;
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;
                        //var point2 = Vector3D.Clamp(_detectMatrixInv.Translation, blockBox.Min, blockBox.Max);
                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, _detectMatrixInv).LengthSquared() > 1) continue;

                            collisionAvg += point;
                            c3++;

                            if (_dmgBlocks.Count > 50) break;
                            c4++;
                            damage += block.Mass;
                            _dmgBlocks.Enqueue(block);
                            break;
                        }
                    }
                    if (collisionAvg != Vector3D.Zero)
                    {


                        collisionAvg /= c3;
                        bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, bPhysics.CenterOfMassWorld);
                        sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, sPhysics.CenterOfMassWorld);
                        var surfaceMass = (bPhysics.Mass > sPhysics.Mass) ? sPhysics.Mass : bPhysics.Mass;
                        var surfaceMulti = (c3 > 5) ? 5 : c3;
                        var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                        var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                        bPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 5) * -Vector3D.Dot(bPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        sPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 5) * -Vector3D.Dot(sPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        bBlockCenter = collisionAvg;
                    }
                    entInfo.Damage = damage;
                    _absorb += damage;
                    if (bBlockCenter != Vector3D.NegativeInfinity) entInfo.ContactPoint = bBlockCenter;
                    if (_count == 58) Log.Line($"[status] obb: true - blocks:{cacheBlockList.Count.ToString()} - sphered:{c1.ToString()} [{c5.ToString()}] - IsDestroyed:{c6.ToString()} not:[{c2.ToString()}] - bCenter Inside Ellipsoid:{c3.ToString()} - Damaged:{c4.ToString()}");
                    if (_count == 0) dsutil.StopWatchReport("[perform]", -1);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}");}
        }

        private double PowerCalculation(IMyEntity breaching)
        {
            var bPhysics = breaching.Physics;
            var sPhysics = Shield.CubeGrid.Physics;

            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = sPhysics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = sPhysics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = bPhysics.LinearVelocity;
            var linearImpulse = bPhysics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }
        #endregion

        #region DSModAPI
        private Vector3D? ApiLineCheck(LineD lineToCheck, float damageFactor)
        {
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, _physicsOutside);
            var obbCheck = _sOriBBoxD.Intersects(ref lineToCheck);
            if (obbCheck == null) return null;

            var testDir = lineToCheck.From - lineToCheck.To;
            testDir.Normalize();
            var ray = new RayD(lineToCheck.From, -testDir);
            var sphereCheck = _shieldSphere.Intersects(ray);
            if (sphereCheck == null) return null;

            var furthestHit = obbCheck < sphereCheck ? sphereCheck : obbCheck;
            var hitPos = lineToCheck.From + testDir * -(double)furthestHit;
            DsDebugDraw.DrawSingleVec(hitPos, 1f, Color.Gold);
            DsDebugDraw.DrawLineToVec(lineToCheck.From, hitPos, Color.Red, .1f);
            return hitPos;
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
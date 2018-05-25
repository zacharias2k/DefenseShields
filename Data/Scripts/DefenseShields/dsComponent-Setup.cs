using System.Collections.Generic;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields 
    {
        #region Setup
        private const ulong ModId = 41000;

        private uint _tick;

        public float ImpactSize { get; set; } = 9f;
        public float Absorb { get; set; }
        private float _power = 0.0001f;
        private float _gridMaxPower;
        private float _gridCurrentPower;
        private float _gridAvailablePower;
        private float _shieldMaxBuffer;
        private float _shieldMaxChargeRate;
        private float _shieldChargeRate;
        private float _shieldDps;
        private float _shieldCurrentPower;
        private float _shieldMaintaintPower;
        private float _shieldPercent;
        private float _shieldConsumptionRate;

        internal double Range;
        private double _sAvelSqr;
        private double _sVelSqr;
        private double _ellipsoidSurfaceArea;
        private double _sizeScaler;

        public int BulletCoolDown { get; private set; } = -1;
        public int EntityCoolDown { get; private set; } = -1;
        private int _count = -1;
        private int _shieldDownLoop = -1;
        private int _longLoop;
        private int _animationLoop;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;

        public bool ServerUpdate;
        public bool EnforceUpdate;
        internal const bool Debug = true;
        internal bool MainInit;
        internal bool AnimateInit;
        internal bool DefinitionsLoaded;
        internal bool GridIsMobile;
        internal bool ShieldActive;
        internal bool BlockWorking;
        internal bool HardDisable { get; private set; }
        internal bool NoPower;
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
        private bool _effectsCleanup;
        private bool _startupWarning;
        private bool _hideShield;
        private bool _updateDimensions;
        private bool _firstRun = true;

        internal Vector3D ShieldSize { get; set; }
        public Vector3D WorldImpactPosition { get; set; } = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _localImpactPosition;
        private Vector3D _detectionCenter;
        private Vector3D _sightPos;

        public readonly Vector3D[] PhysicsOutside = new Vector3D[642];
        public readonly Vector3D[] PhysicsOutsideLow = new Vector3D[162];
        public readonly Vector3D[] PhysicsInside = new Vector3D[642];

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixOutsideInv;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectInsideInv;

        private BoundingBox _oldGridAabb;
        private BoundingBox _shieldAabb;
        private BoundingSphereD _shieldSphere;
        private MyOrientedBoundingBoxD _sOriBBoxD;
        private Quaternion _sQuaternion;

        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<KeyValuePair<IMyEntity, EntIntersectInfo>> _webEntsTmp = new List<KeyValuePair<IMyEntity, EntIntersectInfo>>();

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly DataStructures _dataStructures = new DataStructures();
        private readonly StructureBuilder _structureBuilder = new StructureBuilder();
        private readonly ResourceTracker _resourceTracker = new ResourceTracker(MyResourceDistributorComponent.ElectricityId);

        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();

        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();
        public readonly MyConcurrentHashSet<IMyEntity> FriendlyCache = new MyConcurrentHashSet<IMyEntity>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();
        private readonly Dictionary<string, AmmoInfo> _ammoInfo = new Dictionary<string, AmmoInfo>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private readonly MyConcurrentQueue<IMySlimBlock> _dmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyEntity> _missileDmg = new MyConcurrentQueue<IMyEntity>();
        private readonly MyConcurrentQueue<IMyMeteor> _meteorDmg = new MyConcurrentQueue<IMyMeteor>();
        private readonly MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyCubeGrid> _staleGrids = new MyConcurrentQueue<IMyCubeGrid>();
        private readonly MyConcurrentQueue<IMyCharacter> _characterDmg = new MyConcurrentQueue<IMyCharacter>();
        private readonly MyConcurrentQueue<MyVoxelBase> _voxelDmg = new MyConcurrentQueue<MyVoxelBase>();

        private readonly MyParticleEffect[] _effects = new MyParticleEffect[1];
        private MyEntitySubpart _subpartRotor;

        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _chargeSlider;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _hidePassiveCheckBox;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _hideActiveCheckBox;


        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        public IMyOreDetector Shield => (IMyOreDetector)Entity;
        public MyEntity _shield;
        private MyEntity _shellPassive;
        private MyEntity _shellActive;

        internal Icosphere.Instance Icosphere;
        internal readonly Spawn Spawn = new Spawn();
        internal readonly EllipsoidOxygenProvider EllipsoidOxyProvider = new EllipsoidOxygenProvider(Matrix.Zero);
        internal readonly EllipsoidSA EllipsoidSa = new EllipsoidSA(double.MinValue, double.MinValue, double.MinValue);

        internal DSUtils Dsutil1 = new DSUtils();
        internal DSUtils Dsutil2 = new DSUtils();
        internal DSUtils Dsutil3 = new DSUtils();
        internal DSUtils Dsutil4 = new DSUtils();

        public MyModStorageComponentBase Storage { get; set; }
        internal HashSet<ulong> playersToReceive = null;
        internal DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        public static DefenseShieldsEnforcement ServerEnforcedValues = new DefenseShieldsEnforcement();

        #endregion

        #region constructors and Enums
        private MatrixD DetectionMatrix
        {
            get { return _detectMatrixOutside; }
            set
            {
                _detectMatrixOutside = value;
                _detectMatrixOutsideInv = MatrixD.Invert(value);
                _detectMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
                _detectInsideInv = MatrixD.Invert(_detectMatrixInside);
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
            VoxelBase
        };
        #endregion
    }
}

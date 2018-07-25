using System;
using System.Collections.Generic;
using DefenseShields.Support;
using ParallelTasks;
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
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields 
    {
        #region Setup
        private uint _tick;
        private uint _shieldEntRendId;
        private uint _enforceTick;
        private uint _hierarchyTick = 1;
        private uint _unsuspendTick;

        internal float ImpactSize { get; set; } = 9f;
        internal float Absorb { get; set; }
        internal float ModulateEnergy = 1f;
        internal float ModulateKinetic = 1f;
        private float _power = 0.001f;
        private float _gridMaxPower;
        private float _gridCurrentPower;
        private float _powerNeeded;
        private float _otherPower;
        private float _gridAvailablePower;
        private float _batteryMaxPower;
        private float _batteryCurrentPower;
        private float _shieldMaxBuffer;
        private float _shieldMaxChargeRate;
        private float _shieldChargeRate;
        private float _damageCounter;
        private float _shieldCurrentPower;
        private float _shieldMaintaintPower;
        private float _shieldConsumptionRate;
        private float _shieldFudge;

        internal double BoundingRange;
        private double _ellipsoidAdjust = Math.Sqrt(2);
        private double _oldEllipsoidAdjust;
        private double _sAvelSqr;
        private double _ellipsoidSurfaceArea;
        private double _sizeScaler;

        public int BulletCoolDown { get; internal set; } = -1;
        public int HitCoolDown { get; private set; } = -11;
        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private int _powerLossLoop;
        private int _randomCount = -1;
        private int _offlineCnt = -1;
        private int _overLoadLoop = -1;
        private int _genericDownLoop = -1;
        private int _reModulationLoop = -1;
        private const int ReModulationCount = 300;
        private const int ShieldDownCount = 1200;
        private const int GenericDownCount = 300;

        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;
        private int _shieldRatio;

        internal bool ControllerGridAccess = true;
        internal bool DeformEnabled;
        internal bool ExplosionEnabled;
        internal bool ControlBlockWorking;
        internal bool IsOwner;
        internal bool InFaction;
        internal bool MainInit;
        internal bool PrePowerInit;
        internal bool PowerInited;
        internal bool HadPowerBefore;
        internal bool AllInited;
        internal bool HealthInited;
        internal bool ShieldOffline;
        internal bool CheckGridRegister;
        internal bool WarmedUp;
        internal bool PrevShieldActive;
        internal bool IsStatic;
        internal bool ComingOnline;
        internal bool Warming;
        internal bool Starting;

        internal bool UpdateDimensions;
        internal bool FitChanged;
        internal bool GridIsMobile;
        internal bool ShieldWasLowered;
        internal bool Suspended;
        internal bool ShieldWasSleeping;
        private bool _effectsCleanup;
        private bool _hideShield;
        private bool _shapeAdjusted;
        private bool _hierarchyDelayed;
        private bool _entityChanged;
        private bool _updateRender;
        private bool _functionalAdded;
        private bool _functionalRemoved;
        private bool _functionalChanged;
        private bool _functionalsChanged;
        private bool _blockAdded;
        private bool _blockRemoved;
        private bool _blockChanged;
        private bool _blocksChanged;
        private bool _enablePhysics = true;
        private bool _shapeLoaded = true;
        private bool _createMobileShape = true;

        private Task _backGround;

        private const string SpaceWolf = "Space_Wolf";
        private const string MyMissile = "MyMissile";
        private string _shieldModel = "\\Models\\Cubes\\ShieldActiveBase.mwm";

        private Vector2D _shieldIconPos = new Vector2D(-0.89, -0.86);

        internal Vector3D DetectionCenter;
        internal Vector3D WorldImpactPosition { get; set; } = new Vector3D(Vector3D.NegativeInfinity);
        internal Vector3D ShieldSize { get; set; }
        private Vector3D _localImpactPosition;
        private Vector3D _gridHalfExtents;
        private Vector3D _oldGridHalfExtents;

        internal MatrixD DetectMatrixOutsideInv;
        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectInsideInv;

        private BoundingBox _shieldAabb;
        public BoundingSphereD ShieldSphere;
        public MyOrientedBoundingBoxD SOriBBoxD;
        private Quaternion _sQuaternion;
        private readonly Random _random = new Random();
        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<KeyValuePair<IMyEntity, EntIntersectInfo>> _webEntsTmp = new List<KeyValuePair<IMyEntity, EntIntersectInfo>>();

        internal readonly HashSet<IMyEntity> AuthenticatedCache = new HashSet<IMyEntity>();
        internal readonly HashSet<IMyEntity> FriendlyCache = new HashSet<IMyEntity>();
        internal readonly HashSet<IMyEntity> PartlyProtectedCache = new HashSet<IMyEntity>();
        internal readonly HashSet<IMyEntity> IgnoreCache = new HashSet<IMyEntity>();
        internal readonly HashSet<MyEntity> EnemyShields = new HashSet<MyEntity>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        public readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> WebEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private readonly MyConcurrentQueue<IMySlimBlock> _dmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyEntity> _missileDmg = new MyConcurrentQueue<IMyEntity>();
        private readonly MyConcurrentQueue<IMyMeteor> _meteorDmg = new MyConcurrentQueue<IMyMeteor>();
        private readonly MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyCubeGrid> _staleGrids = new MyConcurrentQueue<IMyCubeGrid>();
        private readonly MyConcurrentQueue<IMyCharacter> _characterDmg = new MyConcurrentQueue<IMyCharacter>();
        private readonly MyConcurrentQueue<MyVoxelBase> _voxelDmg = new MyConcurrentQueue<MyVoxelBase>();

        private static readonly MyStringId HudIconOffline = MyStringId.GetOrCompute("DS_ShieldOffline");
        private static readonly MyStringId HudIconHealth10 = MyStringId.GetOrCompute("DS_ShieldHealth10");
        private static readonly MyStringId HudIconHealth20 = MyStringId.GetOrCompute("DS_ShieldHealth20");
        private static readonly MyStringId HudIconHealth30 = MyStringId.GetOrCompute("DS_ShieldHealth30");
        private static readonly MyStringId HudIconHealth40 = MyStringId.GetOrCompute("DS_ShieldHealth40");
        private static readonly MyStringId HudIconHealth50 = MyStringId.GetOrCompute("DS_ShieldHealth50");
        private static readonly MyStringId HudIconHealth60 = MyStringId.GetOrCompute("DS_ShieldHealth60");
        private static readonly MyStringId HudIconHealth70 = MyStringId.GetOrCompute("DS_ShieldHealth70");
        private static readonly MyStringId HudIconHealth80 = MyStringId.GetOrCompute("DS_ShieldHealth80");
        private static readonly MyStringId HudIconHealth90 = MyStringId.GetOrCompute("DS_ShieldHealth90");
        private static readonly MyStringId HudIconHealth100 = MyStringId.GetOrCompute("DS_ShieldHealth100");

        //private static readonly MyStringId HudIconHeal = MyStringId.GetOrCompute("DS_ShieldHeal");
        private static readonly MyStringId HudIconHeal10 = MyStringId.GetOrCompute("DS_ShieldHeal10");
        private static readonly MyStringId HudIconHeal20 = MyStringId.GetOrCompute("DS_ShieldHeal20");
        private static readonly MyStringId HudIconHeal30 = MyStringId.GetOrCompute("DS_ShieldHeal30");
        private static readonly MyStringId HudIconHeal40 = MyStringId.GetOrCompute("DS_ShieldHeal40");
        private static readonly MyStringId HudIconHeal50 = MyStringId.GetOrCompute("DS_ShieldHeal50");
        private static readonly MyStringId HudIconHeal60 = MyStringId.GetOrCompute("DS_ShieldHeal60");
        private static readonly MyStringId HudIconHeal70 = MyStringId.GetOrCompute("DS_ShieldHeal70");
        private static readonly MyStringId HudIconHeal80 = MyStringId.GetOrCompute("DS_ShieldHeal80");
        private static readonly MyStringId HudIconHeal90 = MyStringId.GetOrCompute("DS_ShieldHeal90");
        private static readonly MyStringId HudIconHeal100 = MyStringId.GetOrCompute("DS_ShieldHeal100");

        //private static readonly MyStringId HudIconDps = MyStringId.GetOrCompute("DS_HudIconDps");
        private static readonly MyStringId HudIconDps10 = MyStringId.GetOrCompute("DS_ShieldDps10");
        private static readonly MyStringId HudIconDps20 = MyStringId.GetOrCompute("DS_ShieldDps20");
        private static readonly MyStringId HudIconDps30 = MyStringId.GetOrCompute("DS_ShieldDps30");
        private static readonly MyStringId HudIconDps40 = MyStringId.GetOrCompute("DS_ShieldDps40");
        private static readonly MyStringId HudIconDps50 = MyStringId.GetOrCompute("DS_ShieldDps50");
        private static readonly MyStringId HudIconDps60 = MyStringId.GetOrCompute("DS_ShieldDps60");
        private static readonly MyStringId HudIconDps70 = MyStringId.GetOrCompute("DS_ShieldDps70");
        private static readonly MyStringId HudIconDps80 = MyStringId.GetOrCompute("DS_ShieldDps80");
        private static readonly MyStringId HudIconDps90 = MyStringId.GetOrCompute("DS_ShieldDps90");
        private static readonly MyStringId HudIconDps100 = MyStringId.GetOrCompute("DS_ShieldDps100");

        private static readonly MyStringHash MPdamage = MyStringHash.GetOrCompute("MPdamage");

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private readonly DataStructures _dataStructures = new DataStructures();
        //private readonly StructureBuilder _structureBuilder = new StructureBuilder();

        internal IMyUpgradeModule Shield => (IMyUpgradeModule)Entity;
        internal ShieldType ShieldMode;
        internal MyEntity ShieldEnt;
        private MyEntity _shellPassive;
        private MyEntity _shellActive;

        private MyParticleEffect _effect = new MyParticleEffect();
        internal Icosphere.Instance Icosphere;
        internal readonly Spawn Spawn = new Spawn();
        internal readonly EllipsoidOxygenProvider EllipsoidOxyProvider = new EllipsoidOxygenProvider(Matrix.Zero);
        internal readonly EllipsoidSA EllipsoidSa = new EllipsoidSA(double.MinValue, double.MinValue, double.MinValue);
        internal DSUtils Dsutil1 = new DSUtils();
        internal DSUtils Dsutil2 = new DSUtils();
        internal DSUtils Dsutil3 = new DSUtils();
        internal DSUtils Dsutil4 = new DSUtils();
        internal DSUtils Dsutil5 = new DSUtils();

        public MyModStorageComponentBase Storage { get; set; }
        internal DefenseShieldsSettings DsSet;

        internal ShieldGridComponent ShieldComp;

        internal HashSet<ulong> playersToReceive = null;

        internal MyStringId CustomDataTooltip = MyStringId.GetOrCompute("Shows an Editor for custom data to be used by scripts and mods");
        internal MyStringId CustomData = MyStringId.GetOrCompute("CustomData");
        internal MyStringId Password = MyStringId.GetOrCompute("Password");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Set the shield modulation password");

        public bool Enabled
        {
            get { return DsSet.Settings.Enabled; }
            set { DsSet.Settings.Enabled = value; }
        }

        public bool ShieldPassiveHide
        {
            get { return DsSet.Settings.PassiveInvisible; }
            set { DsSet.Settings.PassiveInvisible = value; }
        }

        public bool ShieldActiveHide
        {
            get { return DsSet.Settings.ActiveInvisible; }
            set { DsSet.Settings.ActiveInvisible = value; }
        }

        public float Width
        {
            get { return DsSet.Settings.Width; }
            set { DsSet.Settings.Width = value; }
        }

        public float Height
        {
            get { return DsSet.Settings.Height; }
            set { DsSet.Settings.Height = value; }
        }

        public float Depth
        {
            get { return DsSet.Settings.Depth; }
            set { DsSet.Settings.Depth = value; }
        }

        public float Rate
        {
            get { return DsSet.Settings.Rate; }
            set { DsSet.Settings.Rate = value; }
        }

        public bool ExtendFit
        {
            get { return DsSet.Settings.ExtendFit; }
            set { DsSet.Settings.ExtendFit = value; }
        }

        public bool SphereFit
        {
            get { return DsSet.Settings.SphereFit; }
            set { DsSet.Settings.SphereFit = value; }
        }

        public bool FortifyShield
        {
            get { return DsSet.Settings.FortifyShield; }
            set { DsSet.Settings.FortifyShield = value; }
        }

        public bool UseBatteries
        {
            get { return DsSet.Settings.UseBatteries; }
            set { DsSet.Settings.UseBatteries = value; }
        }

        public bool SendToHud
        {
            get { return DsSet.Settings.SendToHud; }
            set { DsSet.Settings.SendToHud = value; }
        }

        public float ShieldBuffer
        {
            get { return DsSet.Settings.Buffer; }
            set { DsSet.Settings.Buffer = value; }
        }

        public bool ModulateVoxels
        {
            get { return DsSet.Settings.ModulateVoxels; }
            set { DsSet.Settings.ModulateVoxels = value; }
        }

        public bool ModulateGrids
        {
            get { return DsSet.Settings.ModulateGrids; }
            set { DsSet.Settings.ModulateGrids = value; }
        }
        #endregion

        #region constructors
        private MatrixD DetectionMatrix
        {
            get { return _detectMatrixOutside; }
            set
            {
                _detectMatrixOutside = value;
                DetectMatrixOutsideInv = MatrixD.Invert(value);
                _detectMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
                _detectInsideInv = MatrixD.Invert(_detectMatrixInside);
            }
        }
        #endregion
    }
}

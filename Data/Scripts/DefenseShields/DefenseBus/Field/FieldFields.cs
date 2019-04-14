using System.Collections.Concurrent;
using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        private const int OverHeat = 600;
        private const int HeatingStep = 600;
        private const int CoolingStep = 1200;
        private const int FallBackStep = 10;

        private const string ModelActive = "\\Models\\Cubes\\ShieldActiveBase.mwm";
        private const string ModelMediumReflective = "\\Models\\Cubes\\ShieldPassive11.mwm";
        private const string ModelHighReflective = "\\Models\\Cubes\\ShieldPassive.mwm";
        private const string ModelLowReflective = "\\Models\\Cubes\\ShieldPassive10.mwm";
        private const string ModelRed = "\\Models\\Cubes\\ShieldPassive09.mwm";
        private const string ModelBlue = "\\Models\\Cubes\\ShieldPassive08.mwm";
        private const string ModelGreen = "\\Models\\Cubes\\ShieldPassive07.mwm";
        private const string ModelPurple = "\\Models\\Cubes\\ShieldPassive06.mwm";
        private const string ModelGold = "\\Models\\Cubes\\ShieldPassive05.mwm";
        private const string ModelOrange = "\\Models\\Cubes\\ShieldPassive04.mwm";
        private const string ModelCyan = "\\Models\\Cubes\\ShieldPassive03.mwm";
        private const string SpaceWolf = "Space_Wolf";

        private const int ReModulationCount = 300;
        private const int ShieldDownCount = 1200;
        private const int EmpDownCount = 3600;
        private const int PowerNoticeCount = 600;
        private const int CapacitorDrainCount = 60;
        private const int CapacitorStableCount = 600;

        private readonly List<KeyValuePair<MyEntity, EntIntersectInfo>> _webEntsTmp = new List<KeyValuePair<MyEntity, EntIntersectInfo>>();
        private readonly List<KeyValuePair<MyEntity, ProtectCache>> _protectEntsTmp = new List<KeyValuePair<MyEntity, ProtectCache>>();

        private uint _capacitorTick;
        private uint _heatVentingTick = uint.MaxValue;
        private uint ShieldEntRendId { get; set; }
        private uint _lastSendDamageTick = uint.MaxValue;

        private int _expChargeReduction;
        private int _prevLod;
        private int _onCount;
        private int _heatCycle = -1;
        private int _fallbackCycle;
        private int _currentHeatStep;
        private int _powerNoticeLoop;
        private int _capacitorLoop;
        private int _empScaleTime = 1;
        private int _overLoadLoop = -1;
        private int _empOverLoadLoop = -1;
        private int _reModulationLoop = -1;

        private double _oldEllipsoidAdjust;
        private double _ellipsoidSurfaceArea;
        private double _shieldVol;
        private double _sizeScaler;

        private float _oldShieldFudge;
        private float _accumulatedHeat;
        private float _empScaleHp = 1f;
        private float _shieldPeakRate;
        private float _runningDamage;
        private float _runningHeal;
        private float _otherPower;
        private float _shieldRatio = 1f;

        private bool _enablePhysics = true;
        private bool _firstLoop = true;
        private bool _hideShield;
        private bool _hideColor;
        private bool _supressedColor;
        private bool _viewInShield;
        private bool _shapeChanged;
        private bool _entityChanged;
        private bool _halfExtentsChanged;
        private bool _empOverLoad;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;

        private bool _needPhysics;
        private bool _clientAltered;

        private string _modelPassive = string.Empty;
        private string _modelActive = "\\Models\\Cubes\\ShieldActiveBase.mwm";

        private readonly Vector2D _shieldIconPos = new Vector2D(-0.89, -0.86);
        private Vector3D _localImpactPosition;
        private Vector3D _oldGridHalfExtents;

        private Quaternion _sQuaternion;

        private MyParticleEffect _effect1 = new MyParticleEffect();
        private MyParticleEffect _effect2 = new MyParticleEffect();

        private readonly RunningAverage _dpsAvg = new RunningAverage(2);
        private readonly RunningAverage _hpsAvg = new RunningAverage(2);
        private readonly EllipsoidOxygenProvider _ellipsoidOxyProvider = new EllipsoidOxygenProvider(Matrix.Zero);
        private readonly EllipsoidSA _ellipsoidSa = new EllipsoidSA(double.MinValue, double.MinValue, double.MinValue);
        private MyEntity ShellPassive { get; set; }
        private MyEntity ShellActive { get; set; }
        internal Bus Bus;


        internal readonly List<MyEntity> PruneList = new List<MyEntity>();
        internal readonly List<ShieldHit> ShieldHits = new List<ShieldHit>();
        internal readonly Queue<ShieldHitValues> ShieldHitsToSend = new Queue<ShieldHitValues>();

        internal readonly HashSet<MyEntity> AuthenticatedCache = new HashSet<MyEntity>();
        internal readonly HashSet<MyEntity> IgnoreCache = new HashSet<MyEntity>();
        internal readonly HashSet<MyEntity> EntityBypass = new HashSet<MyEntity>();
        internal readonly HashSet<MyEntity> EnemyShields = new HashSet<MyEntity>();
        internal readonly HashSet<MyEntity> Missiles = new HashSet<MyEntity>();
        internal readonly HashSet<MyEntity> FriendlyMissileCache = new HashSet<MyEntity>();
        internal readonly Dictionary<MyEntity, ProtectCache> ProtectedEntCache = new Dictionary<MyEntity, ProtectCache>();
        internal readonly ConcurrentDictionary<MyEntity, EntIntersectInfo> WebEnts = new ConcurrentDictionary<MyEntity, EntIntersectInfo>();
        internal readonly ConcurrentDictionary<MyVoxelBase, int> VoxelsToIntersect = new ConcurrentDictionary<MyVoxelBase, int>();

        internal const int ConvToHp = 100;

        internal const double MagicRatio = 2.40063050674088;
        internal const float ChargeRatio = 1.25f;

        internal const float ConvToDec = 0.01f;
        internal const float ConvToWatts = 0.01f;

        internal uint ShapeTick { get; set; }
        internal uint EffectsCleanTick { get; set; }
        internal uint LosCheckTick { get; set; }

        internal readonly int[] ExpChargeReductions = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 };

        internal int KineticCoolDown { get; set; } = -1;
        internal int EnergyCoolDown { get; set; } = -1;
        internal int HitCoolDown { get; set; } = -11;

        internal double BoundingRange { get; set; }
        internal double EllipsoidVolume { get; set; }
        internal double ShieldVolume { get; set; }

        internal float ShieldChargeRate { get; set; }
        internal float ShieldMaxCharge { get; set; }
        internal float ShieldHpBase { get; set; }
        internal float HpScaler { get; set; } = 1f;
        internal float ImpactSize { get; set; } = 9f;
        internal float ShieldConsumptionRate { get; set; }
        internal float ShieldMaxChargeRate { get; set; }
        internal float ShieldMaintaintPower { get; set; }
        internal float Absorb { get; set; }
        internal float DefaultO2 { get; set; }
        internal float FieldAvailablePower { get; set; }
        internal float FieldMaxPower { get; set; }
        internal float DamageReadOut { get; set; }
        internal float PowerNeeds { get; set; }

        internal bool DeformEnabled { get; set; }
        internal bool WebDamage { get; set; }
        internal bool EnergyHit { get; set; }
        internal bool UpdateDimensions { get; set; }
        internal bool FitChanged { get; set; }
        internal bool ShieldIsMobile { get; set; }
        internal bool ShapeEvent { get; set; }
        internal bool AdjustShape { get; set; }
        internal bool UpdateRender { get; set; }
        internal bool UpdateMobileShape { get; set; }
        internal bool FieldActive { get; set; }
        internal bool ModulateGrids { get; set; }
        internal bool EmitterLos { get; set; }
        internal bool EmitterEvent { get; set; }
        internal bool O2Updated { get; set; }
        internal bool CheckEmitters { get; set; }
        internal bool Warming { get; set; }
        internal bool ForceBufferSync { get; set; }
        internal bool PowerFail { get; set; }
        internal bool Inited { get; set; }

        private readonly Vector3D[] _resetEntCorners = new Vector3D[8];
        private readonly Vector3D[] _obbCorners = new Vector3D[8];
        private readonly Vector3D[] _obbPoints = new Vector3D[9];
        internal Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];
        internal Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        internal Vector3D WorldImpactPosition { get; set; } = new Vector3D(Vector3D.NegativeInfinity);
        internal Vector3D MyGridCenter { get; set; }
        internal Vector3D DetectionCenter { get; set; }
        internal Vector3D ShieldSize { get; set; }

        internal MatrixD DetectMatrixOutsideInv { get; set; }
        internal MatrixD ShieldShapeMatrix { get; set; }
        internal MatrixD DetectMatrixOutside { get; set; }
        internal MatrixD ShieldMatrix { get; set; }
        internal MatrixD OffsetEmitterWMatrix { get; set; }

        internal MyOrientedBoundingBoxD SOriBBoxD = new MyOrientedBoundingBoxD();

        internal BoundingBoxD WebBox = new BoundingBoxD();
        internal BoundingBoxD ShieldBox3K = new BoundingBoxD();
        internal BoundingSphereD ShieldSphere = new BoundingSphereD(Vector3D.Zero, 1);
        internal BoundingBox ShieldAabbScaled = new BoundingBox(Vector3D.One, -Vector3D.One);
        internal BoundingSphereD ShieldSphere3K = new BoundingSphereD(Vector3D.Zero, 1f);
        internal BoundingSphereD WebSphere = new BoundingSphereD(Vector3D.Zero, 1f);

        internal MatrixD OldShieldMatrix;

        internal ShieldHitValues ShieldHit { get; set; } = new ShieldHitValues();
        internal MyEntity ShieldEnt { get; set; }
        internal Icosphere.Instance Icosphere { get; set; }
        internal DamageHandlerHit HandlerImpact { get; set; } = new DamageHandlerHit();

        public enum Ent
        {
            Ignore,
            Protected,
            Friendly,
            EnemyPlayer,
            EnemyInside,
            NobodyGrid,
            EnemyGrid,
            Shielded,
            Other,
            VoxelBase,
            Authenticated,
            Floater
        }

        internal MatrixD DetectionMatrix
        {
            get { return DetectMatrixOutside; }
            set
            {
                DetectMatrixOutside = value;
                DetectMatrixOutsideInv = MatrixD.Invert(value);
            }
        }
    }
}

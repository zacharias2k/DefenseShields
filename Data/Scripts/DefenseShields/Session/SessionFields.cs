using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace DefenseShields
{
    public partial class Session
    {
        internal static uint Tick;
        internal uint OldestRefreshTick;
        internal const ushort PacketIdPlanetShieldSettings = 62514;
        internal const ushort PacketIdPlanetShieldState = 62515; // 
        internal const ushort PacketIdEmitterState = 62516;
        internal const ushort PacketIdO2GeneratorState = 62517;
        internal const ushort PacketIdEnhancerState = 62518;
        internal const ushort PacketIdControllerState = 62519;
        internal const ushort PacketIdControllerSettings = 62520;
        internal const ushort PacketIdEnforce = 62521;
        internal const ushort PacketIdModulatorSettings = 62522;
        internal const ushort PacketIdModulatorState = 62523; // 

        private int _count = -1;
        private int _lCount;
        private int _eCount;

        internal int OnCount;
        internal int RefreshCycle;
        internal int RefreshCounter = 1;
        internal int EntCleanCycle = 3600;
        internal int EntMaxTickAge = 36000;

        internal static int EntSlotScaler = 9;

        internal long LastTerminalId;

        internal float MaxEntitySpeed = 210;
        internal const double TickTimeDiv = 0.0625;

        internal static double HudShieldDist = double.MaxValue;

        private static double _syncDistSqr;

        internal static bool EnforceInit;

        private volatile bool _newFrame;

        internal bool CustomDataReset = true;
        internal bool ShowOnHudReset = true;
        internal bool OnCountThrottle;
        internal bool DefinitionsLoaded;
        internal static bool Tick20;
        internal static bool Tick60;
        internal static bool Tick180;
        internal static bool Tick600;
        internal static bool Tick1800;

        internal volatile bool Wake;
        internal volatile bool Monitor = true;
        internal volatile bool EntSlotTick;
        internal bool ScalerChanged;
        internal bool HideActions;
        internal bool DsControl;
        internal bool PsControl;
        internal bool ModControl;

        internal static bool MpActive;
        internal static bool IsServer;
        internal static bool DedicatedServer;
        internal static bool DsAction;
        internal static bool PsAction;
        internal static bool ModAction;
        internal bool[] SphereOnCamera = new bool[0];
        internal readonly int[] SlotCnt = new int[9];

        internal static readonly MyStringHash MPdamage = MyStringHash.GetOrCompute("MPdamage");
        internal static readonly MyStringHash DelDamage = MyStringHash.GetOrCompute("DelDamage");
        internal static readonly MyStringHash DSdamage = MyStringHash.GetOrCompute("DSdamage");
        internal static readonly MyStringHash DSheal = MyStringHash.GetOrCompute("DSheal");
        internal static readonly MyStringHash DSbypass = MyStringHash.GetOrCompute("DSbypass");
        internal static readonly MyStringHash MpDoDeform = MyStringHash.GetOrCompute("MpDoDeform");
        internal static readonly MyStringHash MpDoExplosion = MyStringHash.GetOrCompute("MpDoExplosion");

        internal MyStringHash Bypass = MyStringHash.GetOrCompute("bypass");
        internal MyStringId Password = MyStringId.GetOrCompute("Shield Access Frequency");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Match a shield's modulation frequency/code");
        internal MyStringId ShieldFreq = MyStringId.GetOrCompute("Shield Frequency");
        internal MyStringId ShieldFreqTooltip = MyStringId.GetOrCompute("Set this to the secret frequency/code used for shield access");

        internal readonly Guid EnhancerStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811503");
        internal readonly Guid O2GeneratorStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811504");
        internal readonly Guid ControllerStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811505");
        internal readonly Guid EmitterStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811506");
        internal readonly Guid DisplaySettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811507");
        internal readonly Guid ControllerSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");
        internal readonly Guid ModulatorSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811509");
        internal readonly Guid ModulatorStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811510");
        internal readonly Guid ControllerEnforceGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811511");
        internal readonly Guid PlanetShieldSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811512");
        internal readonly Guid PlanetShieldStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811513");

        internal static readonly TimeSpan SleepTime = TimeSpan.FromTicks(10);
        internal static readonly Type MissileObj = typeof(MyObjectBuilder_Missile);
        internal static Session Instance { get; private set; }
        internal static DefenseShields HudComp;
        internal readonly MyModContext MyModContext = new MyModContext();
        internal readonly Icosphere Icosphere = new Icosphere(5);
        internal DSUtils Dsutil1 = new DSUtils();
        internal DSUtils Dsutil2 = new DSUtils();

        private DsAutoResetEvent _autoResetEvent = new DsAutoResetEvent();
        private readonly Work _workData = new Work();

        internal static readonly List<KeyValuePair<MyEntity, uint>> EntRefreshTmpList = new List<KeyValuePair<MyEntity, uint>>();
        internal static readonly Dictionary<string, AmmoInfo> AmmoCollection = new Dictionary<string, AmmoInfo>();
        internal static readonly Dictionary<MyEntity, MyProtectors> GlobalProtect = new Dictionary<MyEntity, MyProtectors>();
        internal static readonly ConcurrentDictionary<MyEntity, uint> GlobalEntTmp = new ConcurrentDictionary<MyEntity, uint>();
        internal static readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal static readonly ConcurrentQueue<MyEntity> EntRefreshQueue = new ConcurrentQueue<MyEntity>();
        internal static readonly MyConcurrentPool<CachingHashSet<DefenseShields>> ProtSets = new MyConcurrentPool<CachingHashSet<DefenseShields>>(150, null, 1000);
        internal static readonly MyConcurrentHashSet<DefenseShields> ActiveShields = new MyConcurrentHashSet<DefenseShields>();
        internal static readonly MyConcurrentHashSet<DefenseShields> FunctionalShields = new MyConcurrentHashSet<DefenseShields>();

        internal static readonly List<PlanetShields> PlanetShields = new List<PlanetShields>();
        internal static readonly List<Emitters> Emitters = new List<Emitters>();
        internal static readonly List<Displays> Displays = new List<Displays>();
        internal static readonly List<Enhancers> Enhancers = new List<Enhancers>();
        internal static readonly List<O2Generators> O2Generators = new List<O2Generators>();
        internal static readonly List<Modulators> Modulators = new List<Modulators>();
        internal static readonly List<DefenseShields> Controllers = new List<DefenseShields>();

        public static DefenseShieldsEnforcement Enforced = new DefenseShieldsEnforcement();

        internal readonly HashSet<string> DsActions = new HashSet<string>()
        {
            "DS-C_ToggleShield_Toggle",
            "DS-C_ToggleShield_On",
            "DS-C_ToggleShield_Off",
            "DS-C_ChargeRate_Reset",
            "DS-C_ChargeRate_Increase",
            "DS-C_ChargeRate_Decrease",
            "DS-C_ExtendFit_Toggle",
            "DS-C_SphereFit_Toggle",
            "DS-C_ShieldFortify_Toggle",
            "DS-C_HideActive_Toggle",
            "DS-C_RefreshAnimation_Toggle",
            "DS-C_HitWaveAnimation_Toggle",
            "DS-C_HideIcon_Toggle",
            "DS-C_UseBatteries_Toggle"
        };

        internal readonly HashSet<string> ModActions = new HashSet<string>()
        {
            "DS-M_DamageModulation_Reset",
            "DS-M_DamageModulation_Increase",
            "DS-M_DamageModulation_Decrease",
            "DS-M_ModulateVoxels_Toggle",
            "DS-M_ModulateGrids_Toggle",
            "DS-M_ModulateEmpProt_Toggle"
        };

        internal IMyTerminalControlSlider WidthSlider;
        internal IMyTerminalControlSlider HeightSlider;
        internal IMyTerminalControlSlider DepthSlider;
        internal IMyTerminalControlSlider OffsetWidthSlider;
        internal IMyTerminalControlSlider OffsetHeightSlider;
        internal IMyTerminalControlSlider OffsetDepthSlider;
        internal IMyTerminalControlSlider ChargeSlider;
        internal IMyTerminalControlCheckbox ExtendFit;
        internal IMyTerminalControlCheckbox SphereFit;
        internal IMyTerminalControlCheckbox FortifyShield;
        internal IMyTerminalControlCheckbox BatteryBoostCheckBox;
        internal IMyTerminalControlCheckbox HideActiveCheckBox;
        internal IMyTerminalControlCheckbox RefreshAnimationCheckBox;
        internal IMyTerminalControlCheckbox HitWaveAnimationCheckBox;

        internal IMyTerminalControlCheckbox SendToHudCheckBox;
        internal IMyTerminalControlOnOffSwitch ToggleShield;
        internal IMyTerminalControlCombobox ShellSelect;
        internal IMyTerminalControlCombobox ShellVisibility;

        internal IMyTerminalControlSlider ModDamage;
        internal IMyTerminalControlCheckbox ModVoxels;
        internal IMyTerminalControlCheckbox ModGrids;
        internal IMyTerminalControlCheckbox ModEmp;
        internal IMyTerminalControlSeparator ModSep1;
        internal IMyTerminalControlSeparator ModSep2;

        internal IMyTerminalControlCheckbox PsBatteryBoostCheckBox;
        internal IMyTerminalControlCheckbox PsHideActiveCheckBox;
        internal IMyTerminalControlCheckbox PsRefreshAnimationCheckBox;
        internal IMyTerminalControlCheckbox PsHitWaveAnimationCheckBox;

        internal IMyTerminalControlCheckbox PsSendToHudCheckBox;
        internal IMyTerminalControlOnOffSwitch PsToggleShield;
    }
}

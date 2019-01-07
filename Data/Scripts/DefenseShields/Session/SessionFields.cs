namespace DefenseShields
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using global::DefenseShields.Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Interfaces.Terminal;
    using VRage.Collections;
    using VRage.Game;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;

    using VRageMath;

    public partial class Session
    {
        internal const ushort PacketIdShieldHit = 62512;
        internal const ushort PacketIdO2GeneratorSettings = 62513;
        internal const ushort PacketIdPlanetShieldSettings = 62514;
        internal const ushort PacketIdPlanetShieldState = 62515;  
        internal const ushort PacketIdEmitterState = 62516;
        internal const ushort PacketIdO2GeneratorState = 62517;
        internal const ushort PacketIdEnhancerState = 62518;
        internal const ushort PacketIdControllerState = 62519;
        internal const ushort PacketIdControllerSettings = 62520;
        internal const ushort PacketIdEnforce = 62521;
        internal const ushort PacketIdModulatorSettings = 62522;
        internal const ushort PacketIdModulatorState = 62523;
        internal const double TickTimeDiv = 0.0625;

        internal static readonly MyConcurrentPool<MyProtectors> ProtSets = new MyConcurrentPool<MyProtectors>(150, null, 1000);

        internal readonly int[] SlotCnt = new int[9];

        internal readonly MyStringHash MPdamage = MyStringHash.GetOrCompute("MPdamage");
        internal readonly MyStringHash DelDamage = MyStringHash.GetOrCompute("DelDamage");
        internal readonly MyStringHash DSdamage = MyStringHash.GetOrCompute("DSdamage");
        internal readonly MyStringHash DSheal = MyStringHash.GetOrCompute("DSheal");
        internal readonly MyStringHash DSbypass = MyStringHash.GetOrCompute("DSbypass");
        internal readonly MyStringHash MpDoDeform = MyStringHash.GetOrCompute("MpDoDeform");
        internal readonly MyStringHash MpDoExplosion = MyStringHash.GetOrCompute("MpDoExplosion");
        internal readonly MyStringHash MpDmgEffect = MyStringHash.GetOrCompute("MpDmgEffect");

        internal readonly MyStringHash Bypass = MyStringHash.GetOrCompute("bypass");
        internal readonly MyStringId Password = MyStringId.GetOrCompute("Shield Access Frequency");
        internal readonly MyStringId PasswordTooltip = MyStringId.GetOrCompute("Match a shield's modulation frequency/code");
        internal readonly MyStringId ShieldFreq = MyStringId.GetOrCompute("Shield Frequency");
        internal readonly MyStringId ShieldFreqTooltip = MyStringId.GetOrCompute("Set this to the secret frequency/code used for shield access");

        internal readonly Guid O2GeneratorSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811502");
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

        internal readonly Type MissileObj = typeof(MyObjectBuilder_Missile);

        internal readonly MyModContext MyModContext = new MyModContext();
        internal readonly Icosphere Icosphere = new Icosphere(5);

        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();

        internal readonly ConcurrentQueue<DefenseShields> WebWrapper = new ConcurrentQueue<DefenseShields>();
        internal readonly ConcurrentDictionary<long, WarHeadBlast> EmpDraw = new ConcurrentDictionary<long, WarHeadBlast>();

        internal readonly Dictionary<string, AmmoInfo> AmmoCollection = new Dictionary<string, AmmoInfo>();
        internal readonly Dictionary<MyEntity, MyProtectors> GlobalProtect = new Dictionary<MyEntity, MyProtectors>();

        internal readonly MyConcurrentHashSet<DefenseShields> ActiveShields = new MyConcurrentHashSet<DefenseShields>();
        internal readonly MyConcurrentHashSet<DefenseShields> FunctionalShields = new MyConcurrentHashSet<DefenseShields>();

        internal readonly List<PlanetShields> PlanetShields = new List<PlanetShields>();
        internal readonly List<Emitters> Emitters = new List<Emitters>();
        internal readonly List<Displays> Displays = new List<Displays>();
        internal readonly List<Enhancers> Enhancers = new List<Enhancers>();
        internal readonly List<O2Generators> O2Generators = new List<O2Generators>();
        internal readonly List<Modulators> Modulators = new List<Modulators>();
        internal readonly List<DefenseShields> Controllers = new List<DefenseShields>();

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

        internal readonly Color White1 = new Color(255, 255, 255);
        internal readonly Color White2 = new Color(90, 118, 255);
        internal readonly Color White3 = new Color(47, 86, 255);
        internal readonly Color Blue1 = Color.Aquamarine;
        internal readonly Color Blue2 = new Color(0, 66, 255);
        internal readonly Color Blue3 = new Color(0, 7, 255, 255);
        internal readonly Color Blue4 = new Color(22, 0, 170);
        internal readonly Color Red1 = new Color(87, 0, 66);
        internal readonly Color Red2 = new Color(121, 0, 13);
        internal readonly Color Red3 = new Color(255, 0, 0);

        internal readonly MyStringId HudIconOffline = MyStringId.GetOrCompute("DS_ShieldOffline");
        internal readonly MyStringId HudIconHealth10 = MyStringId.GetOrCompute("DS_ShieldHealth10");
        internal readonly MyStringId HudIconHealth20 = MyStringId.GetOrCompute("DS_ShieldHealth20");
        internal readonly MyStringId HudIconHealth30 = MyStringId.GetOrCompute("DS_ShieldHealth30");
        internal readonly MyStringId HudIconHealth40 = MyStringId.GetOrCompute("DS_ShieldHealth40");
        internal readonly MyStringId HudIconHealth50 = MyStringId.GetOrCompute("DS_ShieldHealth50");
        internal readonly MyStringId HudIconHealth60 = MyStringId.GetOrCompute("DS_ShieldHealth60");
        internal readonly MyStringId HudIconHealth70 = MyStringId.GetOrCompute("DS_ShieldHealth70");
        internal readonly MyStringId HudIconHealth80 = MyStringId.GetOrCompute("DS_ShieldHealth80");
        internal readonly MyStringId HudIconHealth90 = MyStringId.GetOrCompute("DS_ShieldHealth90");
        internal readonly MyStringId HudIconHealth100 = MyStringId.GetOrCompute("DS_ShieldHealth100");

        internal readonly MyStringId[] HudHealthHpIcons = 
        {
            MyStringId.NullOrEmpty,
            MyStringId.GetOrCompute("DS_ShieldHeal10"),
            MyStringId.GetOrCompute("DS_ShieldHeal20"),
            MyStringId.GetOrCompute("DS_ShieldHeal30"),
            MyStringId.GetOrCompute("DS_ShieldHeal40"),
            MyStringId.GetOrCompute("DS_ShieldHeal50"),
            MyStringId.GetOrCompute("DS_ShieldHeal60"),
            MyStringId.GetOrCompute("DS_ShieldHeal70"),
            MyStringId.GetOrCompute("DS_ShieldHeal80"),
            MyStringId.GetOrCompute("DS_ShieldHeal90"),
            MyStringId.GetOrCompute("DS_ShieldHeal100"),
            MyStringId.GetOrCompute("DS_ShieldDps100"),
            MyStringId.GetOrCompute("DS_ShieldDps90"),
            MyStringId.GetOrCompute("DS_ShieldDps80"),
            MyStringId.GetOrCompute("DS_ShieldDps70"),
            MyStringId.GetOrCompute("DS_ShieldDps60"),
            MyStringId.GetOrCompute("DS_ShieldDps50"),
            MyStringId.GetOrCompute("DS_ShieldDps40"),
            MyStringId.GetOrCompute("DS_ShieldDps30"),
            MyStringId.GetOrCompute("DS_ShieldDps20"),
            MyStringId.GetOrCompute("DS_ShieldDps10"),
        };

        internal readonly MyStringId HudIconHeat10 = MyStringId.GetOrCompute("DS_ShieldHeat10");
        internal readonly MyStringId HudIconHeat20 = MyStringId.GetOrCompute("DS_ShieldHeat20");
        internal readonly MyStringId HudIconHeat30 = MyStringId.GetOrCompute("DS_ShieldHeat30");
        internal readonly MyStringId HudIconHeat40 = MyStringId.GetOrCompute("DS_ShieldHeat40");
        internal readonly MyStringId HudIconHeat50 = MyStringId.GetOrCompute("DS_ShieldHeat50");
        internal readonly MyStringId HudIconHeat60 = MyStringId.GetOrCompute("DS_ShieldHeat60");
        internal readonly MyStringId HudIconHeat70 = MyStringId.GetOrCompute("DS_ShieldHeat70");
        internal readonly MyStringId HudIconHeat80 = MyStringId.GetOrCompute("DS_ShieldHeat80");
        internal readonly MyStringId HudIconHeat90 = MyStringId.GetOrCompute("DS_ShieldHeat90");
        internal readonly MyStringId HudIconHeat100 = MyStringId.GetOrCompute("DS_ShieldHeat100");

        internal bool[] SphereOnCamera = Array.Empty<bool>();
        internal volatile bool CustomDataReset = true;
        internal volatile bool Monitor = true;
        internal volatile bool Wake;
        internal volatile bool EntSlotTick;
        internal volatile bool Dispatched;
        internal volatile bool WarHeadLoaded;

        private const int EntCleanCycle = 3600;
        private const int EntMaxTickAge = 36000;

        private readonly Work _workData = new Work();
        private readonly List<KeyValuePair<MyEntity, uint>> _entRefreshTmpList = new List<KeyValuePair<MyEntity, uint>>();
        private readonly ConcurrentQueue<MyEntity> _entRefreshQueue = new ConcurrentQueue<MyEntity>();
        private readonly ConcurrentDictionary<MyEntity, uint> _globalEntTmp = new ConcurrentDictionary<MyEntity, uint>();

        private DsAutoResetEvent _autoResetEvent = new DsAutoResetEvent();
        private MyParticleEffect _effect = new MyParticleEffect();

        private int _count = -1;
        private int _lCount;
        private int _eCount;

        private volatile bool _newFrame;

        internal static DefenseShieldsEnforcement Enforced { get; set; } = new DefenseShieldsEnforcement();
        internal static Session Instance { get; private set; }
        internal static bool EnforceInit { get; set; }

        internal uint Tick { get; set; }
        internal uint OldestRefreshTick { get; set; }

        internal int OnCount { get; set; }
        internal int RefreshCycle { get; set; }
        internal int EntSlotScaler { get; set; } = 9;

        internal long LastTerminalId { get; set; }

        internal float MaxEntitySpeed { get; set; } = 210;

        internal double HudShieldDist { get; set; } = double.MaxValue;
        internal double SyncDistSqr { get; private set; }
        internal double SyncDist { get; private set; }

        internal bool WarheadButtonAdd { get; set; }
        internal bool ShowOnHudReset { get; set; } = true;
        internal bool OnCountThrottle { get; set; }
        internal bool GameLoaded { get; set; }
        internal bool MiscLoaded { get; set; }
        internal bool Tick20 { get; set; }
        internal bool Tick60 { get; set; }
        internal bool Tick180 { get; set; }
        internal bool Tick600 { get; set; }
        internal bool Tick1800 { get; set; }
        internal bool WebWrapperOn { get; set; }
        internal bool ScalerChanged { get; set; }
        internal bool HideActions { get; set; }
        internal bool DsControl { get; set; }
        internal bool PsControl { get; set; }
        internal bool ModControl { get; set; }
        internal bool O2Control { get; set; }
        internal bool MpActive { get; set; }
        internal bool IsServer { get; set; }
        internal bool DedicatedServer { get; set; }
        internal bool DsAction { get; set; }
        internal bool PsAction { get; set; }
        internal bool ModAction { get; set; }

        internal Vector3 ResetWarHeadHSV { get; set; } = new Vector3(0, -1, 0);

        internal DefenseShields HudComp { get; set; }

        internal DSUtils Dsutil1 { get; set; } = new DSUtils();
        internal DSUtils Dsutil2 { get; set; } = new DSUtils();

        internal IMyTerminalControlSlider WidthSlider { get; set; }
        internal IMyTerminalControlSlider HeightSlider { get; set; }
        internal IMyTerminalControlSlider DepthSlider { get; set; }
        internal IMyTerminalControlSlider OffsetWidthSlider { get; set; }
        internal IMyTerminalControlSlider OffsetHeightSlider { get; set; }
        internal IMyTerminalControlSlider OffsetDepthSlider { get; set; }
        internal IMyTerminalControlSlider ChargeSlider { get; set; }
        internal IMyTerminalControlCheckbox ExtendFit { get; set; }
        internal IMyTerminalControlCheckbox SphereFit { get; set; }
        internal IMyTerminalControlCheckbox FortifyShield { get; set; }
        internal IMyTerminalControlCheckbox BatteryBoostCheckBox { get; set; }
        internal IMyTerminalControlCheckbox HideActiveCheckBox { get; set; }
        internal IMyTerminalControlCheckbox RefreshAnimationCheckBox { get; set; }
        internal IMyTerminalControlCheckbox HitWaveAnimationCheckBox { get; set; }

        internal IMyTerminalControlCheckbox SendToHudCheckBox { get; set; }
        internal IMyTerminalControlOnOffSwitch ToggleShield { get; set; }
        internal IMyTerminalControlCombobox ShellSelect { get; set; }
        internal IMyTerminalControlCombobox ShellVisibility { get; set; }

        internal IMyTerminalControlSlider ModDamage { get; set; }
        internal IMyTerminalControlCheckbox ModVoxels { get; set; }
        internal IMyTerminalControlCheckbox ModGrids { get; set; }
        internal IMyTerminalControlCheckbox ModEmp { get; set; }
        internal IMyTerminalControlCheckbox ModReInforce { get; set; }
        internal IMyTerminalControlSeparator ModSep1 { get; set; }
        internal IMyTerminalControlSeparator ModSep2 { get; set; }

        internal IMyTerminalControlCheckbox O2DoorFix { get; set; }

        internal IMyTerminalControlCheckbox PsBatteryBoostCheckBox { get; set; }
        internal IMyTerminalControlCheckbox PsHideActiveCheckBox { get; set; }
        internal IMyTerminalControlCheckbox PsRefreshAnimationCheckBox { get; set; }
        internal IMyTerminalControlCheckbox PsHitWaveAnimationCheckBox { get; set; }

        internal IMyTerminalControlCheckbox PsSendToHudCheckBox { get; set; }
        internal IMyTerminalControlOnOffSwitch PsToggleShield { get; set; }

        internal IMyTerminalBlock WarTerminalReset { get; set; }
    }
}

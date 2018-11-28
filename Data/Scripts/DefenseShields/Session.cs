using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefenseShields.Control;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

namespace DefenseShields
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class Session : MySessionComponentBase
    {
        public static uint Tick;

        public const ushort PacketIdPlanetShieldSettings = 62514;
        public const ushort PacketIdPlanetShieldState = 62515; // 
        public const ushort PacketIdEmitterState = 62516;
        public const ushort PacketIdO2GeneratorState = 62517; 
        public const ushort PacketIdEnhancerState = 62518;
        public const ushort PacketIdControllerState = 62519; 
        public const ushort PacketIdControllerSettings = 62520;
        public const ushort PacketIdEnforce = 62521;
        public const ushort PacketIdModulatorSettings = 62522; 
        public const ushort PacketIdModulatorState = 62523; // 

        private const long WorkshopId = 1365616918;

        private int _count = -1;
        private int _lCount;
        private int _eCount;
        internal int OnCount;
        internal int RefreshCounter = 1;
        internal int RefreshCycle;

        internal static int EntSlotScaler = 1;

        private volatile bool _newFrame; 
        internal bool OnCountThrottle;
        internal bool DefinitionsLoaded;
        internal bool CustomDataReset = true;
        internal bool ShowOnHudReset = true;
        internal static bool Tick180;
        internal static bool Tick600;
        internal bool EntSlotTick;
        internal bool RefreshTick;
        internal bool HideActions;

        public static bool EnforceInit;
        public bool DsControl { get; set; }
        public bool PsControl { get; set; }
        public bool ModControl { get; set; }
        
        public static bool DsAction { get; set; }
        public static bool PsAction { get; set; }
        public static bool ModAction { get; set; }

        internal static readonly MyStringHash MPdamage = MyStringHash.GetOrCompute("MPdamage");
        internal static readonly MyStringHash DelDamage = MyStringHash.GetOrCompute("DelDamage");
        internal static readonly MyStringHash DSdamage = MyStringHash.GetOrCompute("DSdamage");
        internal static readonly MyStringHash DSheal = MyStringHash.GetOrCompute("DSheal");
        internal static readonly MyStringHash DSbypass= MyStringHash.GetOrCompute("DSbypass");
        private static readonly Type MissileObj = typeof(MyObjectBuilder_Missile);

        internal MyStringHash Bypass = MyStringHash.GetOrCompute("bypass");
        internal MyStringId Password = MyStringId.GetOrCompute("Shield Access Frequency");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Match a shield's modulation frequency/code");
        internal MyStringId ShieldFreq = MyStringId.GetOrCompute("Shield Frequency");
        internal MyStringId ShieldFreqTooltip = MyStringId.GetOrCompute("Set this to the secret frequency/code used for shield access");
        public static bool MpActive;
        public static bool IsServer;
        public static bool DedicatedServer;

        internal static DefenseShields HudComp;
        internal static double HudShieldDist = double.MaxValue;

        public readonly Guid EnhancerStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811503");
        public readonly Guid O2GeneratorStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811504");
        public readonly Guid ControllerStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811505");
        public readonly Guid EmitterStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811506");
        public readonly Guid DisplaySettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811507");
        public readonly Guid ControllerSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");
        public readonly Guid ModulatorSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811509");
        public readonly Guid ModulatorStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811510");
        public readonly Guid ControllerEnforceGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811511");
        public readonly Guid PlanetShieldSettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811512");
        public readonly Guid PlanetShieldStateGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811513");
        //public string disabledBy = null;

        public static Session Instance { get; private set; }

        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(5);
        public DSUtils Dsutil1 = new DSUtils();

        public IMyTerminalControlSlider WidthSlider;
        public IMyTerminalControlSlider HeightSlider;
        public IMyTerminalControlSlider DepthSlider;
        public IMyTerminalControlSlider OffsetWidthSlider;
        public IMyTerminalControlSlider OffsetHeightSlider;
        public IMyTerminalControlSlider OffsetDepthSlider;
        public IMyTerminalControlSlider ChargeSlider;
        public IMyTerminalControlCheckbox ExtendFit;
        public IMyTerminalControlCheckbox SphereFit;
        public IMyTerminalControlCheckbox FortifyShield;
        public IMyTerminalControlCheckbox BatteryBoostCheckBox;
        public IMyTerminalControlCheckbox HideActiveCheckBox;
        public IMyTerminalControlCheckbox RefreshAnimationCheckBox;
        public IMyTerminalControlCheckbox HitWaveAnimationCheckBox;

        public IMyTerminalControlCheckbox SendToHudCheckBox;
        public IMyTerminalControlOnOffSwitch ToggleShield;
        public IMyTerminalControlCombobox ShellSelect;
        public IMyTerminalControlCombobox ShellVisibility;

        public IMyTerminalControlSlider ModDamage;
        public IMyTerminalControlCheckbox ModVoxels;
        public IMyTerminalControlCheckbox ModGrids;
        public IMyTerminalControlCheckbox ModEmp;
        public IMyTerminalControlSeparator ModSep1;
        public IMyTerminalControlSeparator ModSep2;

        public IMyTerminalControlCheckbox PsBatteryBoostCheckBox;
        public IMyTerminalControlCheckbox PsHideActiveCheckBox;
        public IMyTerminalControlCheckbox PsRefreshAnimationCheckBox;
        public IMyTerminalControlCheckbox PsHitWaveAnimationCheckBox;

        public IMyTerminalControlCheckbox PsSendToHudCheckBox;
        public IMyTerminalControlOnOffSwitch PsToggleShield;

        public bool[] SphereOnCamera = new bool[0];
        public long LastTerminalId;

        public static readonly Dictionary<string, AmmoInfo> AmmoCollection = new Dictionary<string, AmmoInfo>();
        private readonly Dictionary<string, Func<IMyTerminalBlock, bool>> actionEnabled = new Dictionary<string, Func<IMyTerminalBlock, bool>>();

        public readonly HashSet<string> DsActions = new HashSet<string>()
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

        public readonly HashSet<string> ModActions = new HashSet<string>()
        {
            "DS-M_DamageModulation_Reset",
            "DS-M_DamageModulation_Increase",
            "DS-M_DamageModulation_Decrease",
            "DS-M_ModulateVoxels_Toggle",
            "DS-M_ModulateGrids_Toggle",
            "DS-M_ModulateEmpProt_Toggle"
        };

        public readonly List<PlanetShields> PlanetShields = new List<PlanetShields>();
        public readonly List<Emitters> Emitters = new List<Emitters>();
        public readonly List<Displays> Displays = new List<Displays>();
        public readonly List<Enhancers> Enhancers = new List<Enhancers>();
        public readonly List<O2Generators> O2Generators = new List<O2Generators>();
        public readonly List<Modulators> Modulators = new List<Modulators>();
        public readonly List<DefenseShields> Controllers = new List<DefenseShields>();
        public static readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        public readonly MyConcurrentHashSet<DefenseShields> ActiveShields = new MyConcurrentHashSet<DefenseShields>();
        public readonly MyConcurrentHashSet<DefenseShields> Shields = new MyConcurrentHashSet<DefenseShields>();

        public readonly Dictionary<MyEntity, MyProtectors> GlobalProtectDict = new Dictionary<MyEntity, MyProtectors>();
        public readonly MyConcurrentPool<Dictionary<DefenseShields, ProtectorInfo>> ProtDicts = new MyConcurrentPool<Dictionary<DefenseShields, ProtectorInfo>>(150, null, 1000);

        public static DefenseShieldsEnforcement Enforced = new DefenseShieldsEnforcement();

        private readonly List<KeyValuePair<MyEntity, MyProtectors>> _globalEntTmp = new List<KeyValuePair<MyEntity, MyProtectors>>();
        private readonly List<Vector3D> _playerPositions = new List<Vector3D>();
        private readonly MyConcurrentDictionary<DefenseShields, bool> _playerNearShield = new MyConcurrentDictionary<DefenseShields, bool>();
        private static double _syncDistSqr;

        #region Simulation / Init
        public override void BeforeStart()
        {
            try
            {
                MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
                IsServer = MyAPIGateway.Multiplayer.IsServer;
                DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started: Server:{IsServer} - Dedicated:{DedicatedServer} - MpActive:{MpActive}");

                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEnforce, EnforcementReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdControllerState, ControllerStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdControllerSettings, ControllerSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdModulatorSettings, ModulatorSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdModulatorState, ModulatorStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEnhancerState, EnhancerStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdO2GeneratorState, O2GeneratorStateReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEmitterState, EmitterStateReceived);

                RefreshPlayers();
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                MyVisualScriptLogicProvider.PlayerDisconnected += PlayerDisconnected;


                if (!DedicatedServer)
                {
                    MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
                }

                if (IsServer)
                {
                    Log.Line($"LoadConf - Session: This is a server");
                    UtilsStatic.PrepConfigFile();
                    UtilsStatic.ReadConfigFile();
                }

                if (MpActive)
                {
                    _syncDistSqr = MyAPIGateway.Session.SessionSettings.SyncDistance;
                    _syncDistSqr += 1000; // some safety padding, avoid desync
                    _syncDistSqr *= _syncDistSqr;
                }
                else
                {
                    _syncDistSqr = MyAPIGateway.Session.SessionSettings.ViewDistance;
                    _syncDistSqr += 1000;
                    _syncDistSqr *= _syncDistSqr;
                }
                if (Enforced.Debug >= 1) Log.Line($"SyncDistSqr:{_syncDistSqr} - DistNorm:{Math.Sqrt(_syncDistSqr)}");
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }


        #endregion

        #region Draw
        public override void Draw()
        {

            if (DedicatedServer) return;
            try
            {
                if (Controllers.Count == 0) return;
                if (_count == 0 && _lCount == 0) OnCountThrottle = false;
                var onCount = 0;
                for (int i = 0; i < Controllers.Count; i++)
                {
                    var s = Controllers[i];
                    if (s.BulletCoolDown > -1)
                    {
                        s.BulletCoolDown++;
                        if (s.BulletCoolDown == 9) s.BulletCoolDown = -1;
                    }

                    if (s.WebCoolDown > -1)
                    {
                        s.WebCoolDown++;
                        if (s.WebCoolDown == 6) s.WebCoolDown = -1;
                    }

                    if (!s.WarmedUp || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking) continue;
                    var sp = new BoundingSphereD(s.DetectionCenter, s.BoundingRange);
                    if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp))
                    {
                        SphereOnCamera[i] = false;
                        continue;
                    }
                    SphereOnCamera[i] = true;
                    if (!s.Icosphere.ImpactsFinished) onCount++;
                }

                if (onCount >= OnCount)
                {
                    OnCount = onCount;
                    OnCountThrottle = true;
                }
                else if (!OnCountThrottle && _count == 59 && _lCount == 9) OnCount = onCount;

                for (int i = 0; i < Controllers.Count; i++)
                {
                    var s = Controllers[i];
                    if (!s.WarmedUp || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking) continue;
                    if (s.DsState.State.Online && SphereOnCamera[i]) s.Draw(OnCount, SphereOnCamera[i]);
                    else
                    {
                        if (s.DsState.State.Online)
                        {
                            if (!s.Icosphere.ImpactsFinished) s.Icosphere.StepEffects();
                        }
                        else if (s.IsWorking && SphereOnCamera[i]) s.DrawShieldDownIcon();
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }
        #endregion
        
        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                LoadBalancer();
                LogicUpdates();

                Timings();
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public override void UpdateAfterSimulation()
        {
            if (Shields.Count > 0) MyAPIGateway.Parallel.StartBackground(WebMonitor);
        }
        #endregion


        private void Timings()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10) _eCount = 0;
                }
            }
            if (!DefinitionsLoaded && Tick > 100)
            {
                DefinitionsLoaded = true;
                UtilsStatic.GetDefinitons();
            }
        }

        #region EntSlotAssigner
        public static int EntSlotAssigner;
        public static int GetSlot()
        {
            if (EntSlotAssigner++ >= EntSlotScaler - 1) EntSlotAssigner = 0;
            return EntSlotAssigner;
        }
        #endregion

        private void LoadBalancer()
        {
            _newFrame = true;
            Tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            Tick180 = Tick % 180 == 0;
            Tick600 = Tick % 600 == 0;

            var globalProtCnt = GlobalProtectDict.Count;
            if (globalProtCnt <= 25) EntSlotScaler = 1;
            else if (globalProtCnt <= 50) EntSlotScaler = 2;
            else if (globalProtCnt <= 75) EntSlotScaler = 3;
            else if (globalProtCnt <= 100) EntSlotScaler = 4;
            else if (globalProtCnt <= 150) EntSlotScaler = 5;
            else if (globalProtCnt <= 200) EntSlotScaler = 6;
            else EntSlotScaler = 9;

            EntSlotTick = Tick % (180 / EntSlotScaler) == 0;

            RefreshTick = false;
            if (EntSlotTick)
            {
                if (RefreshCycle++ >= EntSlotScaler - 1) RefreshCycle = 0;
                RefreshTick = true;
            }
            var entsRefreshed = 0;
            if (RefreshTick)
            {
                var aa = 0;
                var bb = 0;
                var cc = 0;
                var dd = 0;
                var ee = 0;
                var ff = 0;
                var gg = 0;
                var hh = 0;
                var ii = 0;
                var wrongSlot = 0;

                foreach (var k in GlobalProtectDict.Values)
                {
                    if (k.RefreshSlot == 0) aa++;
                    else if (k.RefreshSlot == 1) bb++;
                    else if (k.RefreshSlot == 2) cc++;
                    else if (k.RefreshSlot == 3) dd++;
                    else if (k.RefreshSlot == 4) ee++;
                    else if (k.RefreshSlot == 5) ff++;
                    else if (k.RefreshSlot == 6) gg++;
                    else if (k.RefreshSlot == 7) hh++;
                    else if (k.RefreshSlot == 8) ii++;
                }

                _globalEntTmp.Clear();
                _globalEntTmp.AddRange(GlobalProtectDict.Where(info => info.Value.RefreshSlot == RefreshCycle || info.Value.RefreshSlot > EntSlotScaler - 1));
                for (int i = 0; i < _globalEntTmp.Count; i++)
                {
                    var ent = _globalEntTmp[i];
                    var refresh = false;
                    foreach (var shield in GlobalProtectDict[ent.Key].Shields.Keys)
                    {
                        if (shield.Asleep && (_playerNearShield.ContainsKey(shield) || Tick <= shield.LastWokenTick + 1800))
                        {
                            entsRefreshed++;
                            shield.Asleep = false;
                            refresh = true;
                        }
                    }
                    if (refresh) GlobalProtectDict.Remove(ent.Key);
                }
                if (Enforced.Debug >= 2) Log.Line($"[NewRefresh] SlotScaler:{EntSlotScaler} - RefreshingEnts:{entsRefreshed} - EntInRefreshSlots:({aa} - {bb} - {cc} - {dd} - {ee} - {ff} - {gg} - {hh} - {ii})");
            }
        }

        private void LogicUpdates()
        {
            var y = 0;
            if (RefreshTick && Enforced.Debug >= 2) Log.Line($"[NearShield] MonitoredShields:{_playerNearShield.Count} - OnlineShields:{Controllers.Count} - SleepingShields:{ActiveShields.Count} - ShieldBlocks: {Shields.Count}");
             var compCount = Controllers.Count;
            if (Enforced.Debug >= 1) Dsutil1.Sw.Restart();
            for (int i = 0; i < compCount; i++)
            {
                var ds = Controllers[i];
                if (RefreshTick)
                {
                    var entScaler = EntSlotScaler;
                    if (ds.LogicSlotScaler != entScaler)
                    {
                        ds.LogicSlotScaler = entScaler;
                        ds.LogicSlot = GetSlot();
                    }
                }
                if (!ds.Asleep) ds.ProtectMyself();
                if (IsServer && !ds.Asleep)
                {
                    ds.WebEntities();
                    y++;
                }
                ds.DeformEnabled = false;
            }
            if (Enforced.Debug >= 2 && RefreshTick) Dsutil1.StopWatchReport($"[Protecting] ProtectedEnts:{GlobalProtectDict.Count} - WakingShields:{y} - CPU:", -1);
            else if (Enforced.Debug >= 1) Dsutil1.Sw.Reset();

            if (SphereOnCamera.Length != compCount) Array.Resize(ref SphereOnCamera, compCount);
        }

        private void RefreshPlayers()
        {
            Log.Line("Refreshing Players");
            Players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players);
            for (int i = 0; i < 1; i++)
            {
                if (!MpActive) Players.Add(MyAPIGateway.Session.Player);
            }

            lock (_playerPositions)
            {
                _playerPositions.Clear();
                foreach (var player in Players)
                {
                    _playerPositions.Add(player.Character.PositionComp.WorldMatrix.Translation);
                }
            }
        }

        #region WebMonitor
        public void WebMonitor()
        {
            try
            {
                _newFrame = false;
                var monitorList = new List<MyEntity>();
                var rId = MyResourceDistributorComponent.ElectricityId;
                var tick = Tick;
                lock (_playerPositions)
                {
                    foreach (var s in Shields)
                    {
                        if (_newFrame) break;
                        if (!s.ControlBlockWorking && !s.DsState.State.Lowered) continue;

                        lock (s.SubLock)
                        {
                            var cleanDistributor = s.MyGridDistributor != null && s.FuncTask.IsComplete && s.MyGridDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects;
                            if (cleanDistributor)
                            {
                                s.GridCurrentPower = s.MyGridDistributor.TotalRequiredInputByType(rId);
                                s.GridMaxPower = s.MyGridDistributor.MaxAvailableResourceByType(rId);
                            }
                        }

                        if (!IsServer || !s.WasOnline || !ActiveShields.Contains(s)) continue;

                        if (tick > 2000 && tick < s.LastWokenTick + 1798)
                        {
                            s.Asleep = false;
                            continue;
                        }

                        var closePlayer = false;
                        var pCnt = _playerPositions.Count;
                        for (int i = 0; i < pCnt; i++)
                        {
                            if (Vector3D.DistanceSquared(_playerPositions[i], s.DetectionCenter) < _syncDistSqr)
                            {
                                closePlayer = true;
                                _playerNearShield.TryAdd(s, true);
                                break;
                            }
                        }

                        if (!closePlayer)
                        {
                            bool removedShield;
                            _playerNearShield.TryRemove(s, out removedShield);
                            s.Asleep = true;
                            continue;
                        }
                        monitorList.Clear();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s.PruneSphere2, monitorList, MyEntityQueryType.Dynamic);

                        var wakeUp = false;
                        for (int i = 0; i < monitorList.Count; i++)
                        {
                            if (_newFrame) break;
                            var ent = monitorList[i];
                            if (!(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                            if (ent.Physics.IsMoving && CustomCollision.CornerOrCenterInShield(ent, s.DetectMatrixOutsideInv) == 0)
                            {
                                //Log.Line($"Awaking shield");
                                s.LastWokenTick = tick;
                                wakeUp = true;
                                break;
                            }
                        }
                        s.Asleep = !wakeUp;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in WebMonitor: {ex}"); }
        }
        #endregion

        #region DamageHandler
        private readonly long[] Nodes = new long[1000];
        private int _emptySpot;
        private readonly Dictionary<long, MyEntity> _backingDict = new Dictionary<long, MyEntity>(1001);
        private MyEntity _previousEnt;
        private long _previousEntId = -1;
        private DefenseShields _blockingShield = null;

        public void UpdatedHostileEnt(long attackerId, out MyEntity ent)
        {
            if (attackerId == 0)
            {
                ent = null;
                return;
            }
            if (_backingDict.TryGetValue(attackerId, out _previousEnt))
            {
                ent = _previousEnt;
                _previousEntId = attackerId;
                return;
            }
            if (MyEntities.TryGetEntityById(attackerId, out _previousEnt))
            {
                if (_emptySpot + 1 >= Nodes.Length) _backingDict.Remove(Nodes[0]);
                Nodes[_emptySpot] = attackerId;
                _backingDict.Add(attackerId, _previousEnt);

                if (_emptySpot++ >= Nodes.Length) _emptySpot = 0;

                ent = _previousEnt;
                _previousEntId = attackerId;
                return;
            }
            _previousEnt = null;
            ent = null;
            _previousEntId = -1;
        }

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                var block = target as IMySlimBlock;
                if (block != null)
                {
                    var myEntity = block.CubeGrid as MyEntity;
                    if (myEntity == null) return;
                    MyProtectors protectors;
                    GlobalProtectDict.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;
                    if (info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure)
                    {
                        Log.Line($"OddDamageType:{info.Type}");
                        return;
                    }
                    if (info.Type == DelDamage || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind) return;


                    MyEntity hostileEnt;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);

                    var shieldHitPos = Vector3D.NegativeInfinity;
                    if (hostileEnt != null)
                    {
                        var shieldCnt = protectors.Shields.Count;
                        var hitDist = -1d;
                        foreach (var dict in protectors.Shields)
                        {
                            var shield = dict.Key;
                            var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                            if (!IsServer && shieldActive && !shield.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }

                            if (!shieldActive) continue;

                            var enclosed = dict.Value.FullCoverage;

                            Vector3D tmpBlockPos;
                            block.ComputeWorldCenter(out tmpBlockPos);
                            if (enclosed && shieldCnt == 1)
                            {
                                _blockingShield = shield;
                            }

                            if (!enclosed && Vector3D.Transform(tmpBlockPos, shield.DetectMatrixOutsideInv).LengthSquared() > 1)
                            {
                                //Log.Line($"no block hit: enclosed:{enclosed} - gridIsParent:{gridIsParent}");
                                continue;
                            }

                            var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, tmpBlockPos);
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);
                            var worldSphere = shield.ShieldSphere;
                            var sphereCheck = worldSphere.Intersects(ray);
                            if (!sphereCheck.HasValue) continue;
                            var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                            var obb = obbCheck ?? 0;
                            var sphere = sphereCheck ?? 0;
                            double furthestHit;
                            if (obb <= 0 && sphere <= 0) furthestHit = 0;
                            else if (obb > sphere) furthestHit = obb;
                            else furthestHit = sphere;
                            var tmphitPos = line.From + testDir * -furthestHit;

                            if (furthestHit > hitDist)
                            {
                                //Log.Line($"shield closer to attacker - dist: {furthestHit} - prevDist: {hitDist} - ShieldId:{shield.MyCube.EntityId}");
                                hitDist = furthestHit;
                                _blockingShield = shield;
                                shieldHitPos = tmphitPos;
                            }
                        }
                    }
                    else
                    {
                        var shieldActive = _blockingShield.DsState.State.Online && !_blockingShield.DsState.State.Lowered;
                        if (!IsServer && shieldActive && !_blockingShield.WarmedUp)
                        {
                            info.Amount = 0;
                            return;
                        }
                        if (!shieldActive || !protectors.Shields.ContainsKey(_blockingShield))
                        {
                            var foundBackupShield = false;
                            foreach (var dict in protectors.Shields)
                            {
                                var shield = dict.Key;
                                var shieldActive2 = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!IsServer && shieldActive2 && !shield.WarmedUp)
                                {
                                    info.Amount = 0;
                                    return;
                                }

                                if (!shieldActive2) continue;
                                _blockingShield = shield;
                                foundBackupShield = true;
                                Log.Line($"found backup shield");
                                break;
                            }

                            if (!foundBackupShield)
                            {
                                Log.Line($"did not find backup shield");
                                _blockingShield = null;
                                return;
                            }
                        }
                    }

                    if (_blockingShield != null)
                    {
                        var shield = _blockingShield;
                        if (!info.IsDeformation) shield.DeformEnabled = false;
                        else if (!shield.DeformEnabled && hostileEnt == null)
                        {
                            info.Amount = 0;
                            return;
                        }

                        if (info.Type == Bypass)
                        {
                            shield.DeformEnabled = true;
                            return;
                        }

                        if (hostileEnt is MyVoxelBase || hostileEnt != null && shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            return;
                        }

                        var gunBase = hostileEnt as IMyGunBaseUser;

                        if (info.Type == DSdamage || info.Type == DSheal || info.Type == DSbypass)
                        {
                            if (info.Type == DSheal)
                            {
                                info.Amount = 0f;
                                return;
                            }

                            if (gunBase != null && block.FatBlock == shield.Shield) //temp fix for GSF laser bug
                            {
                                shield.Absorb += 1000;
                                shield.WorldImpactPosition = shield.ShieldEnt.Render.ColorMaskHsv;
                                info.Amount = 0f;
                                return;
                            }
                            info.Amount = 0f;
                            return;
                        }

                        if (gunBase != null)
                        {
                            var hostileParent = hostileEnt.Parent != null;
                            if (hostileParent && CustomCollision.PointInShield(hostileEnt.Parent.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                return;
                            }
                            var hostilePos = hostileEnt.PositionComp.WorldMatrix.Translation;

                            if (hostilePos == Vector3D.Zero && gunBase.Owner != null) hostilePos = gunBase.Owner.PositionComp.WorldMatrix.Translation;
                            if (!hostileParent && CustomCollision.PointInShield(hostilePos, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                return;
                            }
                        }

                        if (info.IsDeformation && shield.DeformEnabled) return;
                        var bullet = info.Type == MyDamageType.Bullet;
                        var deform = info.Type == MyDamageType.Deformation;
                        if (bullet || deform) info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;
                        else info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;

                        if (!DedicatedServer && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                        {
                            shield.WorldImpactPosition = shieldHitPos;
                            shield.ImpactSize = info.Amount;
                        }

                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
                }
                else if (target is IMyCharacter)
                {
                    var myEntity = target as MyEntity;
                    if (myEntity == null) return;

                    if (info.Type == DelDamage || info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind
                        || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure) return;

                    MyProtectors protectors;
                    GlobalProtectDict.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;

                    foreach (var dict in protectors.Shields)
                    {
                        if (!dict.Value.GridIsParent) continue;
                        var shield = dict.Key;

                        var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;

                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (shieldActive && shield.FriendlyCache.Contains(myEntity) && hostileEnt == null || hostileEnt != null && !shield.FriendlyCache.Contains(hostileEnt))
                        {
                            info.Amount = 0f;
                            myEntity.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
                        }
                    }
                }
                else if (info.Type == MPdamage)
                {
                    var ds = target as DefenseShields;
                    if (ds == null)
                    {
                        info.Amount = 0;
                        return;
                    }

                    if (!DedicatedServer)
                    {
                        var shieldActive = ds.DsState.State.Online && !ds.DsState.State.Lowered;
                        if (!shieldActive || ds.DsState.State.Buffer <= 0)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield inactive or no buff - Active:{shieldActive} - Buffer:{ds.DsState.State.Buffer} - Amount:{info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (hostileEnt == null)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield nullAttacker - Amount:{info.Amount} - Buffer:{ds.DsState.State.Buffer}");
                            info.Amount = 0;
                            return;
                        }
                        var worldSphere = ds.ShieldSphere;
                        var hostileCenter = hostileEnt.PositionComp.WorldVolume.Center;
                        var hostileTestLoc = hostileCenter;
                        var line = new LineD(hostileTestLoc, ds.SOriBBoxD.Center);
                        var obbCheck = ds.SOriBBoxD.Intersects(ref line);
                        var testDir = line.From - line.To;
                        testDir.Normalize();
                        var ray = new RayD(line.From, -testDir);
                        var sphereCheck = worldSphere.Intersects(ray);
                        var obb = obbCheck ?? 0;
                        var sphere = sphereCheck ?? 0;
                        double furthestHit;
                        if (obb <= 0 && sphere <= 0) furthestHit = 0;
                        else if (obb > sphere) furthestHit = obb;
                        else furthestHit = sphere;
                        var hitPos = line.From + testDir * -furthestHit;
                        ds.WorldImpactPosition = hitPos;
                        var warHead = hostileEnt as IMyWarhead;
                        if (warHead != null)
                        {
                            var magicValue = info.Amount;
                            var empPos = warHead.PositionComp.WorldAABB.Center;
                            ds.EmpDetonation = empPos;
                            ds.EmpSize = ds.EllipsoidVolume / magicValue;
                            info.Amount = ds.ShieldMaxBuffer * Enforced.Efficiency / magicValue;
                            UtilsStatic.CreateExplosion(empPos, 2.1f, 9999);
                        }
                        else ds.ImpactSize = info.Amount;

                        if (hostileEnt.DefinitionId.HasValue && hostileEnt.DefinitionId.Value.TypeId == MissileObj)
                        {
                            UtilsStatic.CreateFakeSmallExplosion(hitPos);
                            if (hostileEnt.InScene && !hostileEnt.MarkedForClose)
                            {
                                hostileEnt.Close();
                                hostileEnt.InScene = false;
                            }
                        }
                    }
                    ds.Absorb += info.Amount;
                    info.Amount = 0f;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }
        #endregion

        #region Network sync
        private static void EnforcementReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEnforce>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"EnforceData Received; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Enforce:
                    {
                        if (data.Enforce == null) return;

                        if (Enforced.Debug == 1) Log.Line($"EnforceData Received; Enforce - Server:\n{data.Enforce}");
                        if (!IsServer)
                        {
                            Enforcements.SaveEnforcement(logic.Shield, data.Enforce);
                            EnforceInit = true;
                            if (Enforced.Debug == 1) Log.Line($"client accepted enforcement");
                            if (Enforced.Debug == 1) Log.Line($"Client EnforceInit Complete with enforcements:\n{data.Enforce}");
                        }
                        else PacketizeEnforcements(logic.Shield, data.Enforce.SenderId);
                    }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketEnforcementReceived: {ex}"); }
        }

        private static void ControllerStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataControllerState>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    if (Enforced.Debug >= 1) Log.Line($"Data State null");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    if (Enforced.Debug >= 1) Log.Line($"State PacketReceived; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null)
                {
                    if (Enforced.Debug >= 1) Log.Line($"Logic State null");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.Controllerstate:
                        {
                            if (data.State == null)
                            {
                                if (Enforced.Debug >= 1) Log.Line($"Packet State null");
                                return;
                            }

                            if (Enforced.Debug >= 2) Log.Line($"Packet State Packet received data:\n{data.State}");

                            if (IsServer) ControllerStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketStatsReceived: {ex}"); }
        }


        private static void ControllerSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataControllerSettings>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Controler PacketReceived; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Controllersettings:
                        {
                            if (data.Settings == null) return;

                            logic.UpdateSettings(data.Settings);
                            if (IsServer) ControllerSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            if (Enforced.Debug >= 2) Log.Line($"Packet Settings Packet received:- data:\n{data.Settings}");
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketSettingsReceived: {ex}"); }
        }

        private static void ModulatorSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataModulatorSettings>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Modulator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Modulatorsettings:
                        {
                            if (data.Settings == null) return;

                            logic.UpdateSettings(data.Settings);
                            if (IsServer) ModulatorSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                            if (Enforced.Debug == 1) Log.Line($"Modulator received:\n{data.Settings}");
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorSettingsReceived: {ex}"); }
        }

        private static void ModulatorStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataModulatorState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Modulator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Modulatorstate:
                    {                                                                                
                        if (data.State == null) return;

                        if (Enforced.Debug == 1) Log.Line($"Modulator received:\n{data.State}");

                        if (IsServer) ModulatorStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        else logic.UpdateState(data.State);
                    }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorStateReceived: {ex}"); }
        }

        private static void O2GeneratorStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataO2GeneratorState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"O2Generator PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<O2Generators>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.O2Generatorstate:
                    {
                        if (data.State == null) return;

                        if (Enforced.Debug == 1) Log.Line($"O2Generator received:\n{data.State}");

                        if (IsServer) O2GeneratorStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in O2GeneratorStateReceived: {ex}"); }
        }

        private static void EnhancerStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEnhancerState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Enhancer PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Enhancers>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Enhancerstate:
                    {
                        if (data.State == null) return;

                        if (Enforced.Debug == 1) Log.Line($"Enhancer received:\n{data.State}");

                        if (IsServer) EnhancerStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        else logic.UpdateState(data.State);
                    }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in EnhancerStateReceived: {ex}"); }
        }

        private static void EmitterStateReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2) return;

                var data = MyAPIGateway.Utilities.SerializeFromBinary<DataEmitterState>(bytes); // this will throw errors on invalid data

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"Emitter PacketReceive; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Emitters>();

                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Emitterstate:
                    {
                        if (data.State == null) return;

                        if (IsServer) EmitterStateToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        else logic.UpdateState(data.State);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in EmitterStateReceived: {ex}"); }
        }

        public static void PacketizeEnforcements(IMyCubeBlock block, ulong senderId)
        {
            var data = new DataEnforce(MyAPIGateway.Multiplayer.MyId, block.EntityId, Enforced);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnforce, bytes, senderId);
        }

        public static void PacketizeControllerState(IMyCubeBlock block, ProtoControllerState state)
        {
            var data = new DataControllerState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ControllerStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeControllerSettings(IMyCubeBlock block, ProtoControllerSettings settings)
        {
            var data = new DataControllerSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ControllerSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeModulatorSettings(IMyCubeBlock block, ProtoModulatorSettings settings)
        {
            var data = new DataModulatorSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeModulatorState(IMyCubeBlock block, ProtoModulatorState state)
        {
            var data = new DataModulatorState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizePlanetShieldSettings(IMyCubeBlock block, ProtoPlanetShieldSettings settings)
        {
            var data = new DataPlanetShieldSettings(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            PlanetShieldSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizePlanetShieldState(IMyCubeBlock block, ProtoPlanetShieldState state)
        {
            var data = new DataPlanetShieldState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            PlanetShieldStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeO2GeneratorState(IMyCubeBlock block, ProtoO2GeneratorState state)
        {
            var data = new DataO2GeneratorState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            O2GeneratorStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeEnhancerState(IMyCubeBlock block, ProtoEnhancerState state)
        {
            var data = new DataEnhancerState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            EnhancerStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeEmitterState(IMyCubeBlock block, ProtoEmitterState state)
        {
            var data = new DataEmitterState(MyAPIGateway.Multiplayer.MyId, block.EntityId, state);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
           EmitterStateToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void ControllerStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerState, bytes, p.SteamUserId);
            }
        }

        public static void ControllerSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerSettings, bytes, p.SteamUserId);
            }
        }

        public static void ModulatorSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorSettings, bytes, p.SteamUserId);
            }
        }

        public static void ModulatorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorState, bytes, p.SteamUserId);
                }
            }
        }

        public static void PlanetShieldSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldSettings, bytes, p.SteamUserId);
            }
        }

        public static void PlanetShieldStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender &&
                    Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldState, bytes, p.SteamUserId);
                }
            }
        }

        public static void O2GeneratorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdO2GeneratorState, bytes, p.SteamUserId);
            }
        }

        public static void EnhancerStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnhancerState, bytes, p.SteamUserId);
            }
        }

        public static void EmitterStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;

            foreach (var p in Players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= _syncDistSqr)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEmitterState, bytes, p.SteamUserId);
            }
        }
        #endregion

        #region UI Config
        public void CreateControllerElements(IMyTerminalBlock block)
        {
            try
            {
                if (DsControl) return;
                var comp = block?.GameLogic?.GetAs<DefenseShields>();
                TerminalHelpers.Separator(comp?.Shield, "DS-C_sep0");
                ToggleShield = TerminalHelpers.AddOnOff(comp?.Shield, "DS-C_ToggleShield", "Shield Status", "Raise or Lower Shields", "Up", "Down", DsUi.GetRaiseShield, DsUi.SetRaiseShield);
                TerminalHelpers.Separator(comp?.Shield, "DS-C_sep1");
                ChargeSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_ChargeRate", "Shield Charge Rate", "Percentage Of Power The Shield May Consume", DsUi.GetRate, DsUi.SetRate);
                ChargeSlider.SetLimits(20, 95);

                if (comp != null && comp.GridIsMobile)
                {
                    TerminalHelpers.Separator(comp?.Shield, "DS-C_sep2");
                }

                ExtendFit = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_ExtendFit", "Extend Shield", "Extend Shield", DsUi.GetExtend, DsUi.SetExtend);
                SphereFit = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_SphereFit", "Sphere Shield", "Sphere Shield", DsUi.GetSphereFit, DsUi.SetSphereFit);
                FortifyShield = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_ShieldFortify", "Fortify Shield ", "Fortify Shield ", DsUi.GetFortify, DsUi.SetFortify);
                TerminalHelpers.Separator(comp?.Shield, "DS-C_sep3");

                WidthSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_WidthSlider", "Shield Size Width", "Shield Size Width", DsUi.GetWidth, DsUi.SetWidth);
                WidthSlider.SetLimits(30, 600);

                HeightSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_HeightSlider", "Shield Size Height", "Shield Size Height", DsUi.GetHeight, DsUi.SetHeight);
                HeightSlider.SetLimits(30, 600);

                DepthSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_DepthSlider", "Shield Size Depth", "Shield Size Depth", DsUi.GetDepth, DsUi.SetDepth);
                DepthSlider.SetLimits(30, 600);

                OffsetWidthSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_OffsetWidthSlider", "Width Offset", "Width Offset", DsUi.GetOffsetWidth, DsUi.SetOffsetWidth);
                OffsetWidthSlider.SetLimits(-69, 69);

                OffsetHeightSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_OffsetHeightSlider", "Height Offset", "Height Offset", DsUi.GetOffsetHeight, DsUi.SetOffsetHeight);
                OffsetHeightSlider.SetLimits(-69, 69);

                OffsetDepthSlider = TerminalHelpers.AddSlider(comp?.Shield, "DS-C_OffsetDepthSlider", "Depth Offset", "Depth Offset", DsUi.GetOffsetDepth, DsUi.SetOffsetDepth);
                OffsetDepthSlider.SetLimits(-69, 69);

                TerminalHelpers.Separator(comp?.Shield, "DS-C_sep4");

                BatteryBoostCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_UseBatteries", "Batteries Contribute To Shields", "Batteries May Contribute To Shield Strength", DsUi.GetBatteries, DsUi.SetBatteries);
                SendToHudCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_HideIcon", "Broadcast Shield Status To Hud", "Broadcast Shield Status To Nearby Friendly Huds", DsUi.GetSendToHud, DsUi.SetSendToHud);
                TerminalHelpers.Separator(comp?.Shield, "DS-C_sep5");
                ShellSelect = TerminalHelpers.AddCombobox(comp?.Shield, "DS-C_ShellSelect", "Select Shield Look", "Select shield's shell texture", DsUi.GetShell, DsUi.SetShell, DsUi.ListShell);

                ShellVisibility = TerminalHelpers.AddCombobox(comp?.Shield, "DS-C_ShellSelect", "Select Shield Visibility", "Determines when the shield is visible", DsUi.GetVisible, DsUi.SetVisible, DsUi.ListVisible);

                HideActiveCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_HideActive", "Hide Shield Health On Hit  ", "Hide Shield Health Grid On Hit", DsUi.GetHideActive, DsUi.SetHideActive);

                RefreshAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_RefreshAnimation", "Show Refresh Animation  ", "Show Random Refresh Animation", DsUi.GetRefreshAnimation, DsUi.SetRefreshAnimation);
                HitWaveAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "DS-C_HitWaveAnimation", "Show Hit Wave Animation", "Show Wave Effect On Shield Damage", DsUi.GetHitWaveAnimation, DsUi.SetHitWaveAnimation);

                CreateAction<IMyUpgradeModule>(ToggleShield);

                CreateActionChargeRate<IMyUpgradeModule>(ChargeSlider);

                CreateAction<IMyUpgradeModule>(ExtendFit);
                CreateAction<IMyUpgradeModule>(SphereFit);
                CreateAction<IMyUpgradeModule>(FortifyShield);

                CreateAction<IMyUpgradeModule>(HideActiveCheckBox);
                CreateAction<IMyUpgradeModule>(RefreshAnimationCheckBox);
                CreateAction<IMyUpgradeModule>(HitWaveAnimationCheckBox);
                CreateAction<IMyUpgradeModule>(SendToHudCheckBox);
                CreateAction<IMyUpgradeModule>(BatteryBoostCheckBox);
                DsControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        public void CreatePlanetShieldElements(IMyTerminalBlock block)
        {
            try
            {
                if (PsControl) return;
                var comp = block?.GameLogic?.GetAs<PlanetShields>();
                TerminalHelpers.Separator(comp?.PlanetShield, "DS-P_sep0");
                PsToggleShield = TerminalHelpers.AddOnOff(comp?.PlanetShield, "DS-P_ToggleShield", "Shield Status", "Raise or Lower Shields", "Up", "Down", PsUi.GetRaiseShield, PsUi.SetRaiseShield);
                TerminalHelpers.Separator(comp?.PlanetShield, "DS-P_sep1");

                PsBatteryBoostCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "DS-P_UseBatteries", "Batteries Contribute To Shields", "Batteries May Contribute To Shield Strength", PsUi.GetBatteries, PsUi.SetBatteries);
                PsSendToHudCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "DS-P_HideIcon", "Broadcast Shield Status To Hud", "Broadcast Shield Status To Nearby Friendly Huds", PsUi.GetSendToHud, PsUi.SetSendToHud);
                TerminalHelpers.Separator(comp?.PlanetShield, "DS-P_sep2");

                PsHideActiveCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "DS-P_HideActive", "Hide Shield Health On Hit  ", "Hide Shield Health Grid On Hit", PsUi.GetHideActive, PsUi.SetHideActive);

                PsRefreshAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "DS-P_RefreshAnimation", "Show Refresh Animation  ", "Show Random Refresh Animation", PsUi.GetRefreshAnimation, PsUi.SetRefreshAnimation);
                PsHitWaveAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "DS-P_HitWaveAnimation", "Show Hit Wave Animation", "Show Wave Effect On Shield Damage", PsUi.GetHitWaveAnimation, PsUi.SetHitWaveAnimation);

                CreateAction<IMyUpgradeModule>(PsToggleShield);

                CreateAction<IMyUpgradeModule>(PsHideActiveCheckBox);
                CreateAction<IMyUpgradeModule>(PsRefreshAnimationCheckBox);
                CreateAction<IMyUpgradeModule>(PsHitWaveAnimationCheckBox);
                CreateAction<IMyUpgradeModule>(PsSendToHudCheckBox);
                CreateAction<IMyUpgradeModule>(PsBatteryBoostCheckBox);
                PsControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlerUi: {ex}"); }
        }

        public void CreateModulatorUi(IMyTerminalBlock block)
        {
            try
            {
                if (ModControl) return;
                var comp = block?.GameLogic?.GetAs<Modulators>();
                ModSep1 = TerminalHelpers.Separator(comp?.Modulator, "DS-M_sep1");
                ModDamage = TerminalHelpers.AddSlider(comp?.Modulator, "DS-M_DamageModulation", "Balance Shield Protection", "Balance Shield Protection", ModUi.GetDamage, ModUi.SetDamage);
                ModDamage.SetLimits(20, 180);
                ModSep2 = TerminalHelpers.Separator(comp?.Modulator, "DS-M_sep2");
                ModVoxels = TerminalHelpers.AddCheckbox(comp?.Modulator, "DS-M_ModulateVoxels", "Let voxels bypass shield", "Let voxels bypass shield", ModUi.GetVoxels, ModUi.SetVoxels);
                ModGrids = TerminalHelpers.AddCheckbox(comp?.Modulator, "DS-M_ModulateGrids", "Let grids bypass shield", "Let grid bypass shield", ModUi.GetGrids, ModUi.SetGrids);
                ModEmp = TerminalHelpers.AddCheckbox(comp?.Modulator, "DS-M_ModulateEmpProt", "Protects against EMPs", "But generates heat 10x faster", ModUi.GetEmpProt, ModUi.SetEmpProt);

                CreateActionDamageModRate<IMyUpgradeModule>(ModDamage);

                CreateAction<IMyUpgradeModule>(ModVoxels);
                CreateAction<IMyUpgradeModule>(ModGrids);
                CreateAction<IMyUpgradeModule>(ModEmp);
                ModControl = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CreateModulatorUi: {ex}"); }
        }

        public static void AppendConditionToAction<T>(Func<IMyTerminalAction, bool> actionFindCondition, Func<IMyTerminalAction, IMyTerminalBlock, bool> actionEnabledAppend)
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);

            foreach (var a in actions)
            {
                if (actionFindCondition(a))
                {
                    var existingAction = a.Enabled;

                    a.Enabled = (b) => (existingAction == null ? true : existingAction.Invoke(b)) && actionEnabledAppend(a, b);
                }
            }
        }

        private void CustomControls(IMyTerminalBlock block, List<IMyTerminalControl> myTerminalControls)
        {
            try
            {
                LastTerminalId = block.EntityId;
                switch (block.BlockDefinition.SubtypeId)
                {
                    case "LargeShieldModulator":
                    case "SmallShieldModulator":
                        SetCustomDataToPassword(myTerminalControls);
                        break;
                    case "DSControlLarge":
                    case "DSControlSmall":
                    case "DSControlTable":
                        SetCustomDataToShieldFreq(myTerminalControls);
                        break;
                    default:
                        if (!CustomDataReset) ResetCustomData(myTerminalControls);
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CustomDataToPassword: {ex}"); }
        }

        private void SetCustomDataToPassword(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = Password;
            ((IMyTerminalControlTitleTooltip)customData).Tooltip = PasswordTooltip;
            customData.RedrawControl();
            CustomDataReset = false;
        }

        private void SetCustomDataToShieldFreq(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = ShieldFreq;
            ((IMyTerminalControlTitleTooltip)customData).Tooltip = ShieldFreqTooltip;
            customData.RedrawControl();
            CustomDataReset = false;
        }

        private void ResetCustomData(IEnumerable<IMyTerminalControl> controls)
        {
            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            ((IMyTerminalControlTitleTooltip)customData).Title = MySpaceTexts.Terminal_CustomData;
            ((IMyTerminalControlTitleTooltip)customData).Tooltip = MySpaceTexts.Terminal_CustomDataTooltip;
            customData.RedrawControl();
            CustomDataReset = true;
        }


        public void CreateAction<T>(IMyTerminalControlOnOffSwitch c)
        {
            try
            {
                var id = ((IMyTerminalControl)c).Id;
                var gamePath = MyAPIGateway.Utilities.GamePaths.ContentPath;
                Action<IMyTerminalBlock, StringBuilder> writer = (b, s) => s.Append(c.Getter(b) ? c.OnText : c.OffText);
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Toggle");
                    a.Name = new StringBuilder(c.Title.String).Append(" - ").Append(c.OnText.String).Append("/").Append(c.OffText.String);

                    a.Icon = gamePath + @"\Textures\GUI\Icons\Actions\SmallShipToggle.dds";

                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, !c.Getter(b));
                    a.Writer = writer;

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_On");
                    a.Name = new StringBuilder(c.Title.String).Append(" - ").Append(c.OnText.String);
                    a.Icon = gamePath + @"\Textures\GUI\Icons\Actions\SmallShipSwitchOn.dds";
                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, true);
                    a.Writer = writer;

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Off");
                    a.Name = new StringBuilder(c.Title.String).Append(" - ").Append(c.OffText.String);
                    a.Icon = gamePath + @"\Textures\GUI\Icons\Actions\LargeShipSwitchOn.dds";
                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, false);
                    a.Writer = writer;

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateAction: {ex}"); }
        }

        private void CreateAction<T>(IMyTerminalControlCheckbox c,
            bool addToggle = true,
            bool addOnOff = false,
            string iconPack = null,
            string iconToggle = null,
            string iconOn = null,
            string iconOff = null)
        {
            try
            {

                var id = ((IMyTerminalControl)c).Id;
                var name = c.Title.String;
                Action<IMyTerminalBlock, StringBuilder> writer = (b, s) => s.Append(c.Getter(b) ? c.OnText : c.OffText);

                if (iconToggle == null && iconOn == null && iconOff == null)
                {
                    var pack = iconPack ?? "";
                    var gamePath = MyAPIGateway.Utilities.GamePaths.ContentPath;
                    iconToggle = gamePath + @"\Textures\GUI\Icons\Actions\" + pack + "Toggle.dds";
                    iconOn = gamePath + @"\Textures\GUI\Icons\Actions\" + pack + "SwitchOn.dds";
                    iconOff = gamePath + @"\Textures\GUI\Icons\Actions\" + pack + "SwitchOff.dds";
                }

                if (addToggle)
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Toggle");
                    a.Name = new StringBuilder(name).Append(" On/Off");
                    a.Icon = iconToggle;
                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, !c.Getter(b));
                    if (writer != null)
                        a.Writer = writer;

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }

                if (addOnOff)
                {
                    {
                        var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_On");
                        a.Name = new StringBuilder(name).Append(" On");
                        a.Icon = iconOn;
                        a.ValidForGroups = true;
                        a.Action = (b) => c.Setter(b, true);
                        if (writer != null)
                            a.Writer = writer;

                        MyAPIGateway.TerminalControls.AddAction<T>(a);
                    }
                    {
                        var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Off");
                        a.Name = new StringBuilder(name).Append(" Off");
                        a.Icon = iconOff;
                        a.ValidForGroups = true;
                        a.Action = (b) => c.Setter(b, false);
                        if (writer != null)
                            a.Writer = writer;

                        MyAPIGateway.TerminalControls.AddAction<T>(a);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateAction<T>(IMyTerminalControlCheckbox: {ex}"); }
        }

        private void CreateActionChargeRate<T>(IMyTerminalControlSlider c,
            float defaultValue = 50f, // HACK terminal controls don't have a default value built in...
            float modifier = 1f,
            string iconReset = null,
            string iconIncrease = null,
            string iconDecrease = null,
            bool gridSizeDefaultValue = false) // hacky quick way to get a dynamic default value depending on grid size)
        {
            try
            {
                var id = ((IMyTerminalControl)c).Id;
                var name = c.Title.String;

                if (iconReset == null && iconIncrease == null && iconDecrease == null)
                {
                    var gamePath = MyAPIGateway.Utilities.GamePaths.ContentPath;
                    iconReset = gamePath + @"\Textures\GUI\Icons\Actions\Reset.dds";
                    iconIncrease = gamePath + @"\Textures\GUI\Icons\Actions\Increase.dds";
                    iconDecrease = gamePath + @"\Textures\GUI\Icons\Actions\Decrease.dds";
                }

                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Reset");
                    a.Name = new StringBuilder("Default ").Append(name);
                    if (!gridSizeDefaultValue)
                        a.Name.Append(" (").Append(defaultValue.ToString("0.###")).Append(")");
                    a.Icon = iconReset;
                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, (gridSizeDefaultValue ? b.CubeGrid.GridSize : defaultValue));
                    a.Writer = (b, s) => s.Append(c.Getter(b));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Increase");
                    a.Name = new StringBuilder("Increase ").Append(name).Append(" (+").Append(modifier.ToString("0.###")).Append(")");
                    a.Icon = iconIncrease;
                    a.ValidForGroups = true;
                    a.Action = ActionAddChargeRate;
                    //a.Action = (b) => c.Setter(b, c.Getter(b) + modifier);
                    a.Writer = (b, s) => s.Append(c.Getter(b));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Decrease");
                    a.Name = new StringBuilder("Decrease ").Append(name).Append(" (-").Append(modifier.ToString("0.###")).Append(")");
                    a.Icon = iconDecrease;
                    a.ValidForGroups = true;
                    a.Action = ActionSubtractChargeRate;
                    //a.Action = (b) =>  c.Setter(b, c.Getter(b) - modifier);
                    a.Writer = (b, s) => s.Append(c.Getter(b).ToString("0.###"));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateActionChargeRate: {ex}"); }
        }

        private void ActionAddChargeRate(IMyTerminalBlock b)
        {
            try
            {
                var controls = new List<IMyTerminalControl>();
                MyAPIGateway.TerminalControls.GetControls<IMyUpgradeModule>(out controls);
                var chargeRate = controls.First((x) => x.Id.ToString() == "DS-C_ChargeRate");
                var c = ((IMyTerminalControlSlider)chargeRate);
                if (c.Getter(b) > 94)
                {
                    c.Setter(b, 95f);
                    return;
                }
                c.Setter(b, c.Getter(b) + 5f);
            }
            catch (Exception ex) { Log.Line($"Exception in ActionSubtractChargeRate: {ex}"); }
        }

        private void ActionSubtractChargeRate(IMyTerminalBlock b)
        {
            try
            {
                var controls = new List<IMyTerminalControl>();
                MyAPIGateway.TerminalControls.GetControls<IMyUpgradeModule>(out controls);
                var chargeRate = controls.First((x) => x.Id.ToString() == "DS-C_ChargeRate");
                var c = ((IMyTerminalControlSlider)chargeRate);
                if (c.Getter(b) < 21)
                {
                    c.Setter(b, 20f);
                    return;
                }
                c.Setter(b, c.Getter(b) - 5f);
            }
            catch (Exception ex) { Log.Line($"Exception in ActionSubtractChargeRate: {ex}"); }
        }

        private void CreateActionDamageModRate<T>(IMyTerminalControlSlider c,
        float defaultValue = 50f, // HACK terminal controls don't have a default value built in...
        float modifier = 1f,
        string iconReset = null,
        string iconIncrease = null,
        string iconDecrease = null,
        bool gridSizeDefaultValue = false) // hacky quick way to get a dynamic default value depending on grid size)
        {
            try
            {
                var id = ((IMyTerminalControl)c).Id;
                var name = c.Title.String;

                if (iconReset == null && iconIncrease == null && iconDecrease == null)
                {
                    var gamePath = MyAPIGateway.Utilities.GamePaths.ContentPath;
                    iconReset = gamePath + @"\Textures\GUI\Icons\Actions\Reset.dds";
                    iconIncrease = gamePath + @"\Textures\GUI\Icons\Actions\Increase.dds";
                    iconDecrease = gamePath + @"\Textures\GUI\Icons\Actions\Decrease.dds";
                }

                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Reset");
                    a.Name = new StringBuilder("Default ").Append(name);
                    if (!gridSizeDefaultValue)
                        a.Name.Append(" (").Append(defaultValue.ToString("0.###")).Append(")");
                    a.Icon = iconReset;
                    a.ValidForGroups = true;
                    a.Action = (b) => c.Setter(b, (gridSizeDefaultValue ? b.CubeGrid.GridSize : defaultValue));
                    a.Writer = (b, s) => s.Append(c.Getter(b));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Increase");
                    a.Name = new StringBuilder("Increase ").Append(name).Append(" (+").Append(modifier.ToString("0.###")).Append(")");
                    a.Icon = iconIncrease;
                    a.ValidForGroups = true;
                    a.Action = ActionAddDamageMod;
                    //a.Action = (b) => c.Setter(b, c.Getter(b) + modifier);
                    a.Writer = (b, s) => s.Append(c.Getter(b));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
                {
                    var a = MyAPIGateway.TerminalControls.CreateAction<T>(id + "_Decrease");
                    a.Name = new StringBuilder("Decrease ").Append(name).Append(" (-").Append(modifier.ToString("0.###")).Append(")");
                    a.Icon = iconDecrease;
                    a.ValidForGroups = true;
                    a.Action = ActionSubtractDamageMod;
                    //a.Action = (b) =>  c.Setter(b, c.Getter(b) - modifier);
                    a.Writer = (b, s) => s.Append(c.Getter(b).ToString("0.###"));

                    MyAPIGateway.TerminalControls.AddAction<T>(a);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateActionDamageModRate: {ex}"); }
        }

        private void ActionAddDamageMod(IMyTerminalBlock b)
        {
            try
            {
                var controls = new List<IMyTerminalControl>();
                MyAPIGateway.TerminalControls.GetControls<IMyUpgradeModule>(out controls);
                var damageMod = controls.First((x) => x.Id.ToString() == "DS-M_DamageModulation");
                var c = ((IMyTerminalControlSlider)damageMod);
                if (c.Getter(b) > 179)
                {
                    c.Setter(b, 180f);
                    return;
                }
                c.Setter(b, c.Getter(b) + 1f);
            }
            catch (Exception ex) { Log.Line($"Exception in ActionAddDamageMod: {ex}"); }
        }

        private void ActionSubtractDamageMod(IMyTerminalBlock b)
        {
            try
            {
                var controls = new List<IMyTerminalControl>();
                MyAPIGateway.TerminalControls.GetControls<IMyUpgradeModule>(out controls);
                var chargeRate = controls.First((x) => x.Id.ToString() == "DS-M_DamageModulation");
                var c = ((IMyTerminalControlSlider)chargeRate);
                if (c.Getter(b) < 21)
                {
                    c.Setter(b, 20f);
                    return;
                }
                c.Setter(b, c.Getter(b) - 1f);
            }
            catch (Exception ex) { Log.Line($"Exception in ActionSubtractDamageMod: {ex}"); }
        }

        private void CreateActionCombobox<T>(IMyTerminalControlCombobox c,
            string[] itemIds = null,
            string[] itemNames = null,
            string icon = null)
        {
            var items = new List<MyTerminalControlComboBoxItem>();
            c.ComboBoxContent.Invoke(items);

            foreach (var item in items)
            {
                var id = (itemIds == null ? item.Value.String : itemIds[item.Key]);

                if (id == null)
                    continue; // item id is null intentionally in the array, this means "don't add action".

                var a = MyAPIGateway.TerminalControls.CreateAction<T>(id);
                a.Name = new StringBuilder(itemNames == null ? item.Value.String : itemNames[item.Key]);
                if (icon != null)
                    a.Icon = icon;
                a.ValidForGroups = true;
                a.Action = (b) => c.Setter(b, item.Key);
                //if(writer != null)
                //    a.Writer = writer;

                MyAPIGateway.TerminalControls.AddAction<T>(a);
            }
        }
        #endregion

        #region Events
        private void PlayerConnected(long l)
        {
            RefreshPlayers();
        }

        private void PlayerDisconnected(long l)
        {
            RefreshPlayers();
        }
        #endregion


        #region Misc
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
            HudComp = null;
            Enforced = null;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEnforce, EnforcementReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdControllerState, ControllerStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdControllerSettings, ControllerSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdModulatorSettings, ModulatorSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdModulatorState, ModulatorStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEnhancerState, EnhancerStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdO2GeneratorState, O2GeneratorStateReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PacketIdEmitterState, EmitterStateReceived);

            MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;

            if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControls;

            //Terminate();
            Log.Line("Logging stopped.");
            Log.Close();
        }
        #endregion

    }
}

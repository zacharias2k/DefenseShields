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
using Sandbox.Game.Localization;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        public uint Tick;

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

        internal bool OnCountThrottle;
        internal bool DefinitionsLoaded;
        internal bool CustomDataReset = true;
        internal bool ShowOnHudReset = true;
        public static bool EnforceInit;
        public bool DsControl { get; set; }
        public bool PsControl { get; set; }
        public bool ModControl { get; set; }


        internal static readonly MyStringHash MPdamage = MyStringHash.GetOrCompute("MPdamage");
        internal static readonly MyStringHash DelDamage = MyStringHash.GetOrCompute("DelDamage");
        internal static readonly MyStringHash DSdamage = MyStringHash.GetOrCompute("DSdamage");
        internal static readonly MyStringHash DSheal = MyStringHash.GetOrCompute("DSheal");
        internal static readonly MyStringHash DSbypass= MyStringHash.GetOrCompute("DSbypass");

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
        private DSUtils _dsutil1 = new DSUtils();

        public IMyTerminalControlSlider WidthSlider;
        public IMyTerminalControlSlider HeightSlider;
        public IMyTerminalControlSlider DepthSlider;
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

        public static readonly Dictionary<string, AmmoInfo> AmmoCollection = new Dictionary<string, AmmoInfo>();
        public readonly Dictionary<IMySlimBlock, DefenseShields> ControllerBlockCache = new Dictionary<IMySlimBlock, DefenseShields>();

        public readonly List<PlanetShields> PlanetShields = new List<PlanetShields>();
        public readonly List<Emitters> Emitters = new List<Emitters>();
        public readonly List<Displays> Displays = new List<Displays>();
        public readonly List<Enhancers> Enhancers = new List<Enhancers>();
        public readonly List<O2Generators> O2Generators = new List<O2Generators>();
        public readonly List<Modulators> Modulators = new List<Modulators>();
        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public static DefenseShieldsEnforcement Enforced = new DefenseShieldsEnforcement();

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

                if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;
                if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomActionGetter += ShowHideActions;

                if (IsServer)
                {
                    Log.Line($"LoadConf - Session: This is a server");
                    UtilsStatic.PrepConfigFile();
                    UtilsStatic.ReadConfigFile();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (_count == 0) ControllerBlockCache.Clear();
                for (int i = 0; i < Components.Count; i++)
                {
                    var shield = Components[i];
                    shield.DeformEnabled = false;
                    if (_count == 0)
                    {
                        if (shield.Starting) ControllerBlockCache.Add(shield.Shield.SlimBlock, shield);
                    }
                }
                if (SphereOnCamera.Length != Components.Count) Array.Resize(ref SphereOnCamera, Components.Count);
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
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }
        #endregion

        #region Draw
        public override void Draw()
        {
            if (DedicatedServer) return;
            try
            {
                if (Components.Count == 0) return;
                if (_count == 0 && _lCount == 0) OnCountThrottle = false;
                 var onCount = 0;
                for (int i = 0; i < Components.Count; i++)
                {
                    var s = Components[i];
                    if (s.BulletCoolDown > -1)
                    {
                        s.BulletCoolDown++;
                        if (s.BulletCoolDown == 9) s.BulletCoolDown = -1;
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
                
                for (int i = 0; i < Components.Count; i++)
                {
                    var s = Components[i];
                    if (!s.WarmedUp || s.DsState.State.Lowered || s.DsState.State.Sleeping || s.DsState.State.Suspended || !s.DsState.State.EmitterWorking) continue;
                    if (s.DsState.State.Online && SphereOnCamera[i]) s.Draw(OnCount, SphereOnCamera[i]);
                    else
                    {
                        if (s.DsState.State.Online)
                        {
                            if (!s.Icosphere.ImpactsFinished) s.Icosphere.StepEffects();
                        }
                        else if (s.Shield.IsWorking && SphereOnCamera[i]) s.DrawShieldDownIcon();
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }
        #endregion

        #region DamageHandler
        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                if (Components.Count == 0 || info.Type == DelDamage ||info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure) return;
                
                var player = target as IMyCharacter;
                if (player != null)
                {
                    foreach (var shield in Components)
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);

                        if (shield.DsState.State.Online
                            && shield.FriendlyCache.Contains(player as MyEntity)
                            && (hostileEnt == null || !shield.FriendlyCache.Contains(hostileEnt))) info.Amount = 0f;
                    }
                    return;
                }

                if (info.Type == MPdamage)
                {
                    var shield = target as IMySlimBlock;
                    if (shield == null)
                    {
                        if (Enforced.Debug == 1) Log.Line($"shield is null");
                        info.Amount = 0;
                        return;
                    }

                    DefenseShields ds;
                    ControllerBlockCache.TryGetValue(shield, out ds);
                    if (ds == null)
                    {
                        if (Enforced.Debug == 1) Log.Line($"ds is null");
                        info.Amount = 0;
                        return;
                    }
                    if (!DedicatedServer)
                    {
                        var shieldActive = ds.DsState.State.Online && !ds.DsState.State.Lowered;
                        if (!shieldActive || ds.DsState.State.Buffer <= 0)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield inactive or no buff - Active:{shieldActive} - Buffer:{ds.DsState.State.Buffer} - Amount:{info.Amount} - AttackerId:{info.AttackerId}");
                            info.Amount = 0;
                            return;
                        }

                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (hostileEnt == null)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield nullAttacker - Amount:{info.Amount} - Buffer:{ds.DsState.State.Buffer}");
                            info.Amount = 0;
                            return;
                        }
                        //if (Enforced.Debug == 1) Log.CleanLine("");
                        //if (Enforced.Debug == 1) Log.CleanLine($"part: SId:{ds.Shield.EntityId} - attacker: {hostileEnt.DebugName}");
                        //if (Enforced.Debug == 1) Log.CleanLine($"part: T:{info.Type} - A:{info.Amount} - HF:{ds.FriendlyCache.Contains(hostileEnt)} - HI:{ds.IgnoreCache.Contains(hostileEnt)}");

                        //block.ComputeWorldCenter(out blockPos);
                        //if (!CustomCollision.PointInShield(blockPos, shield.DetectMatrixOutsideInv)) continue;
                        var blockPos = shield.FatBlock.PositionComp.WorldAABB.Center;
                        var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, blockPos);
                        var obbCheck = ds.SOriBBoxD.Intersects(ref line);
                        var testDir = line.From - line.To;
                        testDir.Normalize();
                        var ray = new RayD(line.From, -testDir);
                        var worldSphere = ds.ShieldSphere;
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

                        if (hostileEnt.DefinitionId.HasValue && hostileEnt.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile))
                        {
                            UtilsStatic.CreateFakeSmallExplosion(hitPos);
                            hostileEnt.Close();
                        }
                    }

                    ds.Absorb += info.Amount;
                    info.Amount = 0f;
                    return;
                }

                var block = target as IMySlimBlock;
                if (block == null) return;
                foreach (var shield in Components)
                {
                    var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                    if (!IsServer && shieldActive && !shield.WarmedUp) info.Amount = 0;

                    var blockGrid = (MyCubeGrid)block.CubeGrid;
                    if (shieldActive && shield.FriendlyCache.Contains(blockGrid))
                    {
                        if (!shield.DeformEnabled && info.IsDeformation && info.AttackerId == 0)
                        {
                            info.Amount = 0;
                            //if (Enforced.Debug == 1) Log.Line($"deform not enabled and deform damage + attackerId0");
                            continue;
                        }

                        if (info.Type == Bypass)
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (hostileEnt is MyVoxelBase || shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        if (hostileEnt is IMyGunBaseUser)
                        {
                            var hostileParent = hostileEnt.Parent != null;
                            if (hostileParent && CustomCollision.PointInShield(hostileEnt.Parent.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                continue;
                            }
                            if (!hostileParent && CustomCollision.PointInShield(hostileEnt.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                continue;
                            }
                        }

                        if (hostileEnt != null && block.FatBlock == shield.Shield && (info.Type == DSdamage || info.Type == DSheal || info.Type == DSbypass))
                        {
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            shield.WorldImpactPosition = shield.ShieldEnt.Render.ColorMaskHsv;
                            continue;
                        }

                        if (info.IsDeformation && shield.DeformEnabled) continue;
                        if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Deformation) info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;
                        else info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;

                        if (!DedicatedServer && hostileEnt != null && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                        {
                            //Log.CleanLine("ent attack shielded grid");
                            //Log.CleanLine($"part: SId:{shield.Shield.EntityId} - attacker: {hostileEnt.DebugName} - attacked:{blockGrid.DebugName}");
                            //Log.CleanLine($"part: T:{info.Type} - A:{info.Amount} - HF:{shield.FriendlyCache.Contains(hostileEnt)} - HI:{shield.IgnoreCache.Contains(hostileEnt)} - PF:{shield.FriendlyCache.Contains(blockGrid)} - PI:{shield.IgnoreCache.Contains(blockGrid)}");
                            Vector3D blockPos;
                            block.ComputeWorldCenter(out blockPos);
                            if (!CustomCollision.PointInShield(blockPos, shield.DetectMatrixOutsideInv)) continue;
                            var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, blockPos);
                            var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                            var testDir = line.From - line.To;
                            testDir.Normalize();
                            var ray = new RayD(line.From, -testDir);
                            var worldSphere = shield.ShieldSphere;
                            var sphereCheck = worldSphere.Intersects(ray);
                            var obb = obbCheck ?? 0;
                            var sphere = sphereCheck ?? 0;
                            double furthestHit;
                            if (obb <= 0 && sphere <= 0) furthestHit = 0;
                            else if (obb > sphere) furthestHit = obb;
                            else furthestHit = sphere;
                            var hitPos = line.From + testDir * -furthestHit;
                            shield.WorldImpactPosition = hitPos;
                            shield.ImpactSize = info.Amount;
                        }

                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
                    else if (shieldActive && shield.PartlyProtectedCache.Contains(blockGrid))
                    {
                        if (!shield.DeformEnabled && info.IsDeformation && info.AttackerId == 0)
                        {
                            info.Amount = 0;
                            continue;
                        }

                        if (info.Type == Bypass)
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (hostileEnt is MyVoxelBase || shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        if (hostileEnt is IMyGunBaseUser)
                        {
                            var hostileParent = hostileEnt.Parent != null;
                            if (hostileParent && CustomCollision.PointInShield(hostileEnt.Parent.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                continue;
                            }
                            if (!hostileParent && CustomCollision.PointInShield(hostileEnt.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                continue;
                            }
                        }

                        if (info.IsDeformation && shield.DeformEnabled) continue;

                        if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Deformation) info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;
                        else info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;

                        if (!DedicatedServer && hostileEnt != null && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                        {
                            //Log.CleanLine("ent attack partly shielded grid");
                            //Log.CleanLine($"part: SId:{shield.Shield.EntityId} - attacker: {hostileEnt.DebugName} - attacked:{blockGrid.DebugName}");
                            //Log.CleanLine($"part: T:{info.Type} - A:{info.Amount} - HF:{shield.FriendlyCache.Contains(hostileEnt)} - HI:{shield.IgnoreCache.Contains(hostileEnt)} - PF:{shield.FriendlyCache.Contains(blockGrid)} - PI:{shield.IgnoreCache.Contains(blockGrid)}");
                            Vector3D blockPos;
                            block.ComputeWorldCenter(out blockPos);
                            if (!CustomCollision.PointInShield(blockPos, shield.DetectMatrixOutsideInv)) continue;
                            var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, blockPos);
                            var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                            var testDir = line.From - line.To;
                            testDir.Normalize();
                            var ray = new RayD(line.From, -testDir);
                            var worldSphere = shield.ShieldSphere;
                            var sphereCheck = worldSphere.Intersects(ray);
                            var obb = obbCheck ?? 0;
                            var sphere = sphereCheck ?? 0;
                            double furthestHit;
                            if (obb <= 0 && sphere <= 0) furthestHit = 0;
                            else if (obb > sphere) furthestHit = obb;
                            else furthestHit = sphere;
                            var hitPos = line.From + testDir * -furthestHit;

                            shield.WorldImpactPosition = hitPos;
                            shield.ImpactSize = info.Amount;
                        }

                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
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

                if (data == null) return;

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"State PacketReceived; {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();
                if (logic == null) return;

                switch (data.Type)
                {
                    case PacketType.Controllerstate:
                        {
                            if (data.State == null) return;

                            if (Enforced.Debug >= 2) Log.Line($"Packet State Packet received:- data:\n{data.State}");

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
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerState, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ControllerSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdControllerSettings, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ModulatorSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorSettings, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ModulatorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender &&
                    Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                {
                    Log.Line($"sending modulator state packet to client: {p.SteamUserId}");
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulatorState, bytes, p.SteamUserId);
                }
            }
            players.Clear();
        }

        public static void PlanetShieldSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldSettings, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void PlanetShieldStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender &&
                    Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                {
                    Log.Line($"sending modulator state packet to client: {p.SteamUserId}");
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdPlanetShieldState, bytes, p.SteamUserId);
                }
            }
            players.Clear();
        }

        public static void O2GeneratorStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdO2GeneratorState, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void EnhancerStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnhancerState, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void EmitterStateToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)

                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEmitterState, bytes, p.SteamUserId);
            }
            players.Clear();
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
                TerminalHelpers.Separator(comp?.PlanetShield, "PS-C_sep0");
                PsToggleShield = TerminalHelpers.AddOnOff(comp?.PlanetShield, "PS-C_ToggleShield", "Shield Status", "Raise or Lower Shields", "Up", "Down", DsUi.GetRaiseShield, DsUi.SetRaiseShield);
                TerminalHelpers.Separator(comp?.PlanetShield, "PS-C_sep1");

                PsBatteryBoostCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "PS-C_UseBatteries", "Batteries Contribute To Shields", "Batteries May Contribute To Shield Strength", DsUi.GetBatteries, DsUi.SetBatteries);
                PsSendToHudCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "PS-C_HideIcon", "Broadcast Shield Status To Hud", "Broadcast Shield Status To Nearby Friendly Huds", DsUi.GetSendToHud, DsUi.SetSendToHud);
                TerminalHelpers.Separator(comp?.PlanetShield, "PS-C_sep2");

                PsHideActiveCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "PS-C_HideActive", "Hide Shield Health On Hit  ", "Hide Shield Health Grid On Hit", DsUi.GetHideActive, DsUi.SetHideActive);

                PsRefreshAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "PS-C_RefreshAnimation", "Show Refresh Animation  ", "Show Random Refresh Animation", DsUi.GetRefreshAnimation, DsUi.SetRefreshAnimation);
                PsHitWaveAnimationCheckBox = TerminalHelpers.AddCheckbox(comp?.PlanetShield, "PS-C_HitWaveAnimation", "Show Hit Wave Animation", "Show Wave Effect On Shield Damage", DsUi.GetHitWaveAnimation, DsUi.SetHitWaveAnimation);

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

        private void CustomControls(IMyTerminalBlock block, List<IMyTerminalControl> myTerminalControls)
        {
            try
            {
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

        private void ShowHideActions(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            try
            {
                switch (block.BlockDefinition.SubtypeId)
                {
                    case "LargeShieldModulator":
                    case "SmallShieldModulator":
                        ModulatorShowHideActions(actions);
                        break;
                    case "DSControlLarge":
                    case "DSControlSmall":
                    case "DSControlTable":
                        ControllerShowHideActions(actions);
                        break;
                    case "EmitterL":
                    case "EmitterS":
                    case "EmitterST":
                    case "EmitterLA":
                    case "EmitterSA":
                    case "LargeDamageEnhancer":
                    case "SmallDamageEnhancer":
                        HideAllActions(actions);
                        break;
                    case "PlanetaryEmitterLarge":
                        PlanetShieldShowHideActions(actions);
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CustomDataToPassword: {ex}"); }
        }

        private static void HideAllActions(List<IMyTerminalAction> actions)
        {
            foreach (var a in actions)
            {
                if (a.Id.StartsWith("DS-")) a.Enabled = terminalBlock => false;
            }
        }

        private static void ModulatorShowHideActions(List<IMyTerminalAction> actions)
        {
            foreach (var a in actions)
            {
                if (!a.Id.StartsWith("DS-M_") && a.Id.StartsWith("DS-")) a.Enabled = terminalBlock => false;
                else if (a.Id.StartsWith("DS-M_")) a.Enabled = terminalBlock => true;
            }
        }

        private static void PlanetShieldShowHideActions(List<IMyTerminalAction> actions)
        {
            foreach (var a in actions)
            {
                if (!a.Id.StartsWith("PS-M_") && a.Id.StartsWith("PS-")) a.Enabled = terminalBlock => false;
                else if (a.Id.StartsWith("PS-M_")) a.Enabled = terminalBlock => true;
            }
        }

        private static void ControllerShowHideActions(List<IMyTerminalAction> actions)
        {
            foreach (var a in actions)
            {
                if (!a.Id.StartsWith("DS-C_") && a.Id.StartsWith("DS-")) a.Enabled = terminalBlock => false;
                else if (a.Id.StartsWith("DS-C_")) a.Enabled = terminalBlock => true;
            }
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
                c.Setter(b, c.Getter(b) + 1f);
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
                c.Setter(b, c.Getter(b) - 1f);
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

            if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControls;
            if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomActionGetter -= ShowHideActions;
            Log.Line("Logging stopped.");
            Log.Close();
        }
        #endregion

    }
}

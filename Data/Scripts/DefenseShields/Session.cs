using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Control;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        private uint _tick;

        public const ushort PacketIdDisplay = 62519; // network
        public const ushort PacketIdSettings = 62520; // network
        public const ushort PacketIdEnforce = 62521; // network
        public const ushort PacketIdModulator = 62522; // network
        private const long WorkshopId = 1365616918;

        private int _count = -1;
        private int _lCount;
        private int _eCount;

        internal bool SessionInit;
        internal bool DefinitionsLoaded;
        internal bool CustomDataReset = true;
        internal bool ShowOnHudReset = true;
        public static bool EnforceInit;
        public bool DsControl { get; set; }
        public bool ModControl { get; set; }
        public bool StationEmitterControlsLoaded { get; set; }
        public bool LargeEmitterControlsLoaded { get; set; }
        public bool SmallEmitterControlsLoaded { get; set; }
        public bool DisplayControlsLoaded { get; set; }
        public bool Enabled = true;

        internal MyStringId Password = MyStringId.GetOrCompute("Shield Access Frequency");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Match a shield's modulation frequency/code");
        internal MyStringId ShieldFreq = MyStringId.GetOrCompute("Shield Frequency");
        internal MyStringId ShieldFreqTooltip = MyStringId.GetOrCompute("Set this to the secret frequency/code used for shield access");
        public static readonly bool MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
        public static readonly bool IsServer = MyAPIGateway.Multiplayer.IsServer;
        public static readonly bool DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

        internal static DefenseShields HudComp;
        internal static double HudShieldDist = double.MaxValue;

        public readonly Guid EmitterGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811506");
        public readonly Guid DisplayGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811507");
        public readonly Guid SettingsGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811508");
        public readonly Guid ModulatorGuid = new Guid("85BBB4F5-4FB9-4230-BEEF-BB79C9811509");

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
        public IMyTerminalControlCheckbox HidePassiveCheckBox;
        public IMyTerminalControlCheckbox HideActiveCheckBox;
        public IMyTerminalControlCheckbox SendToHudCheckBox;
        public IMyTerminalControlOnOffSwitch ToggleShield;

        public IMyTerminalControlSlider ModDamage;
        public IMyTerminalControlCheckbox ModVoxels;
        public IMyTerminalControlCheckbox ModGrids;
        public IMyTerminalControlSeparator ModSep1;
        public IMyTerminalControlSeparator ModSep2;

        public static readonly Dictionary<string, AmmoInfo> AmmoCollection = new Dictionary<string, AmmoInfo>();
        public bool[] SphereOnCamera = new bool[0];

        public readonly List<Emitters> Emitters = new List<Emitters>();
        public readonly List<Displays> Displays = new List<Displays>();
        public readonly List<Enhancers> Enhancers = new List<Enhancers>();
        public readonly List<O2Generators> O2Generators = new List<O2Generators>();
        public readonly List<Modulators> Modulators = new List<Modulators>();
        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public static DefenseShieldsEnforcement Enforced = new DefenseShieldsEnforcement();

        public void Init()
        {
            try
            {
                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started");
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdSettings, PacketSettingsReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdEnforce, PacketEnforcementReceived);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(PacketIdModulator, ModulatorSettingsReceived);
                if (!DedicatedServer) MyAPIGateway.TerminalControls.CustomControlGetter += CustomControls;

                if (DedicatedServer || IsServer)
                {
                    Log.Line($"This is a server, loading config");
                    UtilsStatic.PrepConfigFile();
                    UtilsStatic.ReadConfigFile();
                }
                SessionInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in SessionInit: {ex}"); }
        }

        public override void Draw()
        {
            if (DedicatedServer) return;
            if (Enforced.Debug == 1 && _eCount == 0 & _lCount == 0 && _count == 0) Log.Line($"Shields in the world: {Components.Count.ToString()}");
            try
            {
                if (!SessionInit || Components.Count == 0) return;
                var onCount = 0;
                for (int i = 0; i < Components.Count; i++)
                {
                    var s = Components[i];
                    if (!s.WarmedUp || !s.RaiseShield) continue;
                    var sp = new BoundingSphereD(s.DetectionCenter, s.ShieldComp.BoundingRange);
                    if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sp))
                    {
                        SphereOnCamera[i] = false;
                        continue;
                    }
                    SphereOnCamera[i] = true;
                    onCount++;
                }

                for (int i = 0; i < Components.Count; i++)
                {
                    var s = Components[i];
                    if (!s.WarmedUp || !s.RaiseShield) continue;
                    if (s.ShieldComp.ShieldActive && SphereOnCamera[i]) s.Draw(onCount, SphereOnCamera[i]);
                    else if (s.ShieldComp.ShieldActive && !s.Icosphere.ImpactsFinished) s.Icosphere.StepEffects();
                    else if (!s.ShieldComp.ShieldActive && SphereOnCamera[i]) s.DrawShieldDownIcon();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                for (int i = 0; i < Components.Count; i++) Components[i].DeformEnabled = false;
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

                if (!SessionInit)
                {
                    if (DedicatedServer) Init();
                    else if (MyAPIGateway.Session != null) Init();
                }

                if (!DefinitionsLoaded && SessionInit && _tick > 200)
                {
                    DefinitionsLoaded = true;
                    UtilsStatic.GetDefinitons();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                if (Components.Count == 0 || info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind || info.Type == MyDamageType.Environment) return;

                var player = target as IMyCharacter;
                if (player != null)
                {
                    foreach (var shield in Components)
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);

                        if (shield.ShieldComp.ShieldActive
                            && shield.FriendlyCache.Contains(player) 
                            && (hostileEnt == null  || !shield.FriendlyCache.Contains(hostileEnt))) info.Amount = 0f;
                    }
                    return;
                }

                var block = target as IMySlimBlock;
                if (block == null) return;
                var blockGrid = (MyCubeGrid)block.CubeGrid;

                foreach (var shield in Components)
                {
                    if (shield.ShieldComp.ShieldActive && shield.RaiseShield && shield.FriendlyCache.Contains(blockGrid))
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (hostileEnt is MyVoxelBase || shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        if (hostileEnt != null && block.FatBlock == shield.Shield && (info.Type.String.Equals("DSdamage") || info.Type.String.Equals("DSheal") || info.Type.String.Equals("DSbypass")))
                        {
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            shield.WorldImpactPosition = shield.ShieldEnt.Render.ColorMaskHsv;
                            continue;
                        }

                        if (shield.DeformEnabled) continue;

                        if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Deformation) info.Amount = info.Amount * shield.ModulateKinetic;
                        else info.Amount = info.Amount * shield.ModulateEnergy;

                        if (hostileEnt != null && shield.Absorb < 1 && shield.BulletCoolDown == -1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity)
                        {
                            //Log.CleanLine("");
                            //Log.CleanLine($"full: SId:{shield.Shield.EntityId} - attacker: {hostileEnt.DebugName} - attacked:{blockGrid.DebugName}");
                            //Log.CleanLine($"full: T:{info.Type} - A:{info.Amount} - HF:{shield.FriendlyCache.Contains(hostileEnt)} - HI:{shield.IgnoreCache.Contains(hostileEnt)} - PF:{shield.FriendlyCache.Contains(blockGrid)} - PI:{shield.IgnoreCache.Contains(blockGrid)}");
                            Vector3D blockPos;
                            block.ComputeWorldCenter(out blockPos);
                            var vertPos = CustomCollision.ClosestVert(shield.ShieldComp.PhysicsOutside, blockPos);
                            shield.WorldImpactPosition = vertPos;
                            shield.ImpactSize = 5;
                        }

                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
                    else if (shield.ShieldComp.ShieldActive && shield.RaiseShield && shield.PartlyProtectedCache.Contains(blockGrid))
                    {
                        MyEntity hostileEnt;
                        MyEntities.TryGetEntityById(info.AttackerId, out hostileEnt);
                        if (hostileEnt is MyVoxelBase || shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            continue;
                        }

                        if (shield.DeformEnabled) continue;

                        if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Deformation) info.Amount = info.Amount * shield.ModulateKinetic;
                        else info.Amount = info.Amount * shield.ModulateEnergy;

                        if (hostileEnt != null && shield.Absorb < 1 && shield.BulletCoolDown == -1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity)
                        {
                            //Log.Line($"part - attacker: {hostileEnt.DebugName} - attacked:{blockGrid.DebugName} - {info.Type} - {info.Amount} - {shield.FriendlyCache.Contains(hostileEnt)} - {shield.IgnoreCache.Contains(hostileEnt)}");
                            Vector3D blockPos;
                            block.ComputeWorldCenter(out blockPos);
                            if (!CustomCollision.PointInShield(blockPos, shield.DetectMatrixOutsideInv)) continue;
                            var vertPos = CustomCollision.ClosestVert(shield.ShieldComp.PhysicsOutside, blockPos);
                            shield.WorldImpactPosition = vertPos;
                            shield.ImpactSize = 5;
                        }
                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }

        #region Network sync
        private static void PacketSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<PacketData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.SETTINGS:
                        {
                            if (data.Settings == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; settings are null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"Packet Settings Packet received:- data:\n{data.Settings}");
                            logic.UpdateSettings(data.Settings);
                            logic.UpdateDimensions = true;
                            logic.DsSet.SaveSettings();
                            if (IsServer)
                                ShieldSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketSettingsReceived: {ex}"); }
        }

        private static void PacketEnforcementReceived(byte[] bytes)
        {
            try
            {
                if (!IsServer) Log.Line($"client received enforcement");
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<EnforceData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<DefenseShields>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.ENFORCE:
                        {
                            if (data.Enforce == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; Enforce is null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"PacketReceived(); Enforce - Server:\n{data.Enforce}");
                            if (!IsServer)
                            {
                                Enforcements.UpdateEnforcement(data.Enforce);
                                logic.DsSet.SaveSettings();
                                EnforceInit = true;
                                Log.Line($"client accepted enforcement");
                                if (Enforced.Debug == 1) Log.Line($"Client EnforceInit Complete with enforcements:\n{data.Enforce}");
                            }
                            else PacketizeEnforcements(logic.Shield, data.Enforce.SenderId);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PacketEnforcementReceived: {ex}"); }
        }

        private static void ModulatorSettingsReceived(byte[] bytes)
        {
            try
            {
                if (bytes.Length <= 2)
                {
                    Log.Line($"PacketReceived(); invalid length <= 2; length={bytes.Length.ToString()}");
                    return;
                }

                var data = MyAPIGateway.Utilities.SerializeFromBinary<ModulatorData>(bytes); // this will throw errors on invalid data

                if (data == null)
                {
                    Log.Line($"PacketReceived(); no deserialized data!");
                    return;
                }

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.EntityId, out ent) || ent.Closed)
                {
                    Log.Line($"PacketReceived(); {data.Type}; {(ent == null ? "can't find entity" : (ent.Closed ? "found closed entity" : "entity not a shield"))}");
                    return;
                }

                var logic = ent.GameLogic.GetAs<Modulators>();

                if (logic == null)
                {
                    Log.Line($"PacketReceived(); {data.Type}; shield doesn't have the gamelogic component!");
                    return;
                }

                switch (data.Type)
                {
                    case PacketType.MODULATOR:
                        {
                            if (data.Settings == null)
                            {
                                Log.Line($"PacketReceived(); {data.Type}; settings are null!");
                                return;
                            }

                            if (Enforced.Debug == 1) Log.Line($"Packet received:\n{data.Settings}");
                            logic.UpdateSettings(data.Settings);
                            logic.ModSet.SaveSettings();
                            logic.ServerUpdate = true;

                            if (IsServer)
                                ModulatorSettingsToClients(((IMyCubeBlock)ent).CubeGrid.GetPosition(), bytes, data.Sender);
                        }
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in ModulatorSettingsReceived: {ex}"); }
        }

        public static void PacketizeEnforcements(IMyCubeBlock block, ulong senderId)
        {
            var data = new EnforceData(MyAPIGateway.Multiplayer.MyId, block.EntityId, Enforced);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageTo(PacketIdEnforce, bytes, senderId);
        }

        public static void PacketizeModulatorSettings(IMyCubeBlock block, ModulatorBlockSettings settings)
        {
            var data = new ModulatorData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ModulatorSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void PacketizeShieldSettings(IMyCubeBlock block, DefenseShieldsModSettings settings)
        {
            var data = new PacketData(MyAPIGateway.Multiplayer.MyId, block.EntityId, settings);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            ShieldSettingsToClients(block.CubeGrid.GetPosition(), bytes, data.Sender);
        }

        public static void ShieldSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 1000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdSettings, bytes, p.SteamUserId);
            }
            players.Clear();
        }

        public static void ModulatorSettingsToClients(Vector3D syncPosition, byte[] bytes, ulong sender)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 1000; // some safety padding, avoid desync
            distSq *= distSq;

            var players = Instance.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                var id = p.SteamUserId;

                if (id != localSteamId && id != sender && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(PacketIdModulator, bytes, p.SteamUserId);
            }
            players.Clear();
        }
        #endregion

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
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

        public void CreateControlerUi(IMyTerminalBlock block)
        {
            if (DsControl) return;
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            var sep0 = TerminalHelpers.Separator(comp?.Shield, "sep0");
            ToggleShield = TerminalHelpers.AddOnOff(comp?.Shield, "ToggleShield", "Shield Status", "Raise or Lower Shields", "Up", "Down", DsUi.GetRaiseShield, DsUi.SetRaiseShield);
            var sep1 = TerminalHelpers.Separator(comp?.Shield, "sep1");
            ChargeSlider = TerminalHelpers.AddSlider(comp?.Shield, "ChargeRate", "Shield Charge Rate", "Shield Charge Rate", DsUi.GetRate, DsUi.SetRate);
            ChargeSlider.SetLimits(20, 95);

            if (comp != null && comp.GridIsMobile)
            {
                var sep2 = TerminalHelpers.Separator(comp?.Shield, "sep2");
            }

            ExtendFit = TerminalHelpers.AddCheckbox(comp?.Shield, "ExtendFit", "Extend Shield", "Extend Shield", DsUi.GetExtend, DsUi.SetExtend);
            SphereFit = TerminalHelpers.AddCheckbox(comp?.Shield, "SphereFit", "Sphere Fit      ", "Sphere Fit      ", DsUi.GetSphereFit, DsUi.SetSphereFit);
            FortifyShield = TerminalHelpers.AddCheckbox(comp?.Shield, "ShieldFortify", "Fortify Shield ", "Fortify Shield ", DsUi.GetFortify, DsUi.SetFortify);
            var sep3 = TerminalHelpers.Separator(comp?.Shield, "sep3");

            WidthSlider = TerminalHelpers.AddSlider(comp?.Shield, "WidthSlider", "Shield Size Width", "Shield Size Width", DsUi.GetWidth, DsUi.SetWidth);
            WidthSlider.SetLimits(30, 600);

            HeightSlider = TerminalHelpers.AddSlider(comp?.Shield, "HeightSlider", "Shield Size Height", "Shield Size Height", DsUi.GetHeight, DsUi.SetHeight);
            HeightSlider.SetLimits(30, 600);

            DepthSlider = TerminalHelpers.AddSlider(comp?.Shield, "DepthSlider", "Shield Size Depth", "Shield Size Depth", DsUi.GetDepth, DsUi.SetDepth);
            DepthSlider.SetLimits(30, 600);
            var sep4 = TerminalHelpers.Separator(comp?.Shield, "sep4");

            HidePassiveCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "UseBatteries", "Batteries boost shields        ", "Batteries boost shields        ", DsUi.GetBatteries, DsUi.SetBatteries);
            HidePassiveCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "HidePassive", "Hide idle shield state            ", "Hide idle shield state            ", DsUi.GetHidePassive, DsUi.SetHidePassive);
            HideActiveCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "HideActive", "Hide active shield state        ", "Hide active shield state        ", DsUi.GetHideActive, DsUi.SetHideActive);
            SendToHudCheckBox = TerminalHelpers.AddCheckbox(comp?.Shield, "HideIcon", "Send status to nearby HUDs", "Send status to nearby HUDs", DsUi.GetSendToHud, DsUi.SetSendToHud);
           
            DsControl = true;
        }

        public void CreateModulatorUi(IMyTerminalBlock block)
        {
            if (ModControl) return;
            var comp = block?.GameLogic?.GetAs<Modulators>();
            ModSep1 = TerminalHelpers.Separator(comp?.Modulator, "sep1");
            ModDamage = TerminalHelpers.AddSlider(comp?.Modulator, "DamageModulation", "Balance Shield Protection", "Balance Shield Protection", ModUi.GetDamage, ModUi.SetDamage);
            ModDamage.SetLimits(20, 180);
            ModSep2 = TerminalHelpers.Separator(comp?.Modulator, "sep2");
            ModVoxels = TerminalHelpers.AddCheckbox(comp?.Modulator, "ModulateVoxels", "Let voxels bypass shield", "Let voxels bypass shield", ModUi.GetVoxels, ModUi.SetVoxels);
            ModGrids = TerminalHelpers.AddCheckbox(comp?.Modulator, "ModulateGrids", "Let grids bypass shield", "Let grid bypass shield", ModUi.GetGrids, ModUi.SetGrids);
            ModControl = true;
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
    #endregion
}

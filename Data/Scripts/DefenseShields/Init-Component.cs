using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Startup Logic
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                Entity.Components.TryGet(out Sink);
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.RemoveType(ref ResourceInfo.ResourceTypeId);
                Sink.Init(MyStringHash.GetOrCompute("Defense"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);

                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                Session.Instance.Components.Add(this);
                Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));
                if (Session.Enforced.Debug == 1) Log.Line($"pre-Init complete");
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!DefinitionsLoaded && MainInit && _tick > 200)
                {
                    DefinitionsLoaded = true;
                    GetDefinitons();
                    if (Session.Enforced.Debug == 1) Log.Line($"Get Definitions");
                }
                if (AnimateInit && MainInit || !Shield.IsFunctional) return;

                if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);

                if (!MainInit && Shield.IsFunctional)
                {

                    StorageSetup();

                    var enableState = Shield.Enabled;

                    if (enableState)
                    {
                        Shield.Enabled = false;
                        Shield.Enabled = true;
                    }

                    if (Session.IsServer) ServerEnforcementSetup();
                    else ClientEnforcementRequest();

                    if (Shield.CubeGrid.Physics.IsStatic) GridIsMobile = false;
                    else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                    CreateUi();

                    if (Session.Enforced.Debug == 1) Log.Line($"MainInit complete");

                    MainInit = true;
                    return;
                }

                if (Session.Enforced.Nerf.Equals(-1f)) return;

                if (!HealthAndPowerCheck()) return;

                if (!PhysicsInit)
                {
                    SpawnEntities();

                    switch (Shield.BlockDefinition.SubtypeId)
                    {
                        case "DefenseShieldsST":
                            _shieldRatio = Session.Enforced.StationRatio;
                            break;
                        case "DefenseShieldsLS":
                            _shieldRatio = Session.Enforced.LargeShipRatio;
                            break;
                        case "DefenseShieldsSS":
                            _shieldRatio = Session.Enforced.SmallShipRatio;
                            break;
                    }

                    if (Session.Enforced.Debug == 1) Log.Line($"PhysicsInit complete");

                    PhysicsInit = true;
                }

                if (AnimateInit || !MainInit || !Shield.IsFunctional) return;

                if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - {Shield.BlockDefinition.SubtypeId} is functional - tick:{_tick.ToString()}");
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);

                    if (!Session.DedicatedServer) BlockParticleCreate();

                    if (Session.Enforced.Debug == 1) Log.Line($"AnimateInit complete");

                    AnimateInit = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation100: {ex}"); }
        }

        private bool HealthAndPowerCheck()
        {
            if (SinkInit) return true;

            HardDisable = false || Shield.EntityId != ThereCanBeOnlyOne() || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" &&
                !Shield.CubeGrid.Physics.IsStatic || Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic;

            NoPower = false;
            if (!HardDisable && !SinkInit) PowerInitCheck();

            if (!HardDisable || SinkInit) return true;
            if (Session.Enforced.Debug == 1) Log.Line($"HardDisable is triggered - Power: {NoPower} - {SinkInit} - {Shield.BlockDefinition.SubtypeId}");

            var realPlayerIds = new List<long>();
            DsUtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
            foreach (var id in realPlayerIds)
            {
                if (!_startupWarning && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" && !Shield.CubeGrid.Physics.IsStatic)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Station shields only allowed on stations", 5000, "Red", id);
                    _startupWarning = true;
                }
                else if (!_startupWarning && Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Large Ship Shields only allowed on ships, not stations", 5000, "Red", id);
                    _startupWarning = true;
                }
                else if (!_startupWarning && NoPower)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Insufficent power to bring Shield online", 5000, "Red", id);
                    _startupWarning = true;
                }
                else if (!_startupWarning)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Only one generator per grid in this version", 5000, "Red", id);
                    _startupWarning = true;
                }
            }
            return false;
        }

        private void SpawnEntities()
        {
            //Log.Line($"Initting {Shield.BlockDefinition.SubtypeId} - tick:{_tick.ToString()}");

            MyEntity parent;
            if (GridIsMobile) parent = (MyEntity)Shield.CubeGrid;
            else parent = (MyEntity)Shield.CubeGrid;

            _shellPassive = Spawn.EmptyEntity("dShellPassive", $"{Session.Instance.ModPath()}\\Models\\Cubes\\ShieldPassive_LOD0.mwm", parent, true);
            _shellPassive.Render.CastShadows = false;
            _shellPassive.IsPreview = true;
            _shellPassive.Render.Visible = true;
            _shellPassive.Render.RemoveRenderObjects();
            _shellPassive.Render.UpdateRenderObject(true);
            _shellPassive.Render.UpdateRenderObject(false);
            _shellPassive.Save = false;

            _shellActive = Spawn.EmptyEntity("dShellActive", $"{Session.Instance.ModPath()}\\Models\\Cubes\\ShieldActiveH_LOD3.mwm", parent, true);
            _shellActive.Render.CastShadows = false;
            _shellActive.IsPreview = true;
            _shellActive.Render.Visible = true;
            _shellActive.Render.RemoveRenderObjects();
            _shellActive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(false);
            _shellActive.Save = false;
            _shellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Black, 0.01f);

            _shield = Spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
            //_shield = Spawn.SpawnBlock("dShield", $"{Shield.EntityId}", true, false, false, false, true, Shield.OwnerId);
            _shield.Render.CastShadows = false;
            _shield.Render.RemoveRenderObjects();
            _shield.Render.UpdateRenderObject(true);
            _shield.Render.Visible = true;
            _shield.Save = false;

            Shield.AppendingCustomInfo += AppendingCustomInfo;
            Shield.RefreshCustomInfo();

            if (Session.Enforced.Debug == 1) Log.Line($"SpawnEntities complete");
        }

        private void StorageSetup()
        {
            Storage = Shield.Storage;
            LoadSettings();
            UpdateSettings(Settings, false);
            if (Session.Enforced.Debug == 1) Log.Line($"StorageSetup complete");
        }

        private void ClientEnforcementRequest()
        {
            if (Session.Enforced.Nerf >= 0) ShieldNerf = Session.Enforced.Nerf; 
            if (Session.Enforced.BaseScaler >= 1) ShieldBaseScaler = Session.Enforced.BaseScaler;
            if (Session.Enforced.Efficiency >= 1) ShieldEfficiency = Session.Enforced.Efficiency;

            if (Session.Enforced.StationRatio > 0) StationRatio = Session.Enforced.StationRatio; 
            if (Session.Enforced.LargeShipRatio > 0) LargeShipRatio = Session.Enforced.LargeShipRatio;
            if (Session.Enforced.SmallShipRatio > 0) SmallShipRatio = Session.Enforced.SmallShipRatio;

            if (Session.Enforced.DisableVoxelSupport > 0) VoxelSupport = Session.Enforced.DisableVoxelSupport;
            if (Session.Enforced.DisableGridDamageSupport > 0) GridDamageSupport = Session.Enforced.DisableGridDamageSupport;

            if (Session.Enforced.Debug > 0) Debug = Session.Enforced.Debug;


            if (Session.Enforced.BaseScaler.Equals(-1) && !Settings.BaseScaler.Equals(-1))
            {
                Session.Enforced.BaseScaler = Settings.BaseScaler;
                Session.Enforced.Nerf = Settings.Nerf;
                Session.Enforced.Efficiency = Settings.Efficiency;

                Session.Enforced.StationRatio = Settings.StationRatio;
                Session.Enforced.LargeShipRatio = Settings.LargeShipRatio;
                Session.Enforced.SmallShipRatio = Settings.SmallShipRatio;

                Session.Enforced.DisableVoxelSupport = Settings.DisableVoxelSupport;
                Session.Enforced.DisableGridDamageSupport = Settings.DisableGridDamageSupport;

                Session.Enforced.Debug = Settings.Debug;
                if (Session.Enforced.Debug == 1) Log.Line($"Local enforcements found, bypassing request");
            }

            if (Session.Enforced.BaseScaler < 1) EnforcementRequest();
            if (Session.Enforced.Debug == 1) Log.Line($"ClientEnforcementRequest Check finished - Enforcement Request?: {Session.Enforced.BaseScaler < 1}");
        }

        private void ServerEnforcementSetup()
        {
            ShieldNerf = Session.Enforced.Nerf; 
            ShieldBaseScaler = Session.Enforced.BaseScaler;
            ShieldEfficiency = Session.Enforced.Efficiency;

            StationRatio = Session.Enforced.StationRatio;
            LargeShipRatio = Session.Enforced.LargeShipRatio;
            SmallShipRatio = Session.Enforced.SmallShipRatio;

            VoxelSupport = Session.Enforced.DisableVoxelSupport;
            GridDamageSupport = Session.Enforced.DisableGridDamageSupport;

            Debug = Session.Enforced.Debug;

            if (Session.Enforced.Debug == 1) Log.Line($"ServerEnforcementSetup - nerf: {ShieldNerf} - base: {ShieldBaseScaler} - Eff: {ShieldEfficiency} - ST:{StationRatio} - LS:{LargeShipRatio} - SS:{SmallShipRatio} - Voxel:{VoxelSupport} - Grid:{GridDamageSupport}\n{Session.Enforced}");
        }

        private void PowerInitCheck()
        {
            try
            {
                if (SinkInit) return;
                if (!Sink.IsPowerAvailable(GId, _power))
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"HardDisable");
                    NoPower = true;
                    HardDisable = true;
                    return;
                }
                SinkInit = true;
                HardDisable = false;
                NoPower = false;
                _power = 0.0000000001f;
                _shieldCurrentPower = _power;
                Sink.Update();
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit complete");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private long ThereCanBeOnlyOne()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"ThereCanBeOnlyOne start");
            var gridStatic = Shield.CubeGrid.Physics.IsStatic;
            var shieldBlocks = new List<MyCubeBlock>();
            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                if (block == null) continue;

                if (block.BlockDefinition.BlockPairName.Equals("DefenseShield") || block.BlockDefinition.BlockPairName.Equals("StationShield"))
                {
                    if (gridStatic && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                    else if (!gridStatic && (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS"))
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                }
            }
            var shieldDistFromCenter = double.MinValue;
            var shieldId = long.MinValue;
            foreach (var shield in shieldBlocks)
            {
                if (shield == null) continue;
                if (gridStatic && shield.BlockDefinition.BlockPairName.Equals("DefenseShield")) continue;
                if (!gridStatic && shield.BlockDefinition.BlockPairName.Equals("StationShield")) continue;

                var dist = Vector3D.DistanceSquared(shield.PositionComp.WorldVolume.Center, Shield.CubeGrid.WorldVolume.Center);
                if (dist > shieldDistFromCenter)
                {
                    shieldDistFromCenter = dist;
                    shieldId = shield.EntityId;
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"ThereCanBeOnlyOne complete, found shield: {shieldId}");
            return shieldId;
        }

        private void GetDefinitons()
        {
            try
            {
                var defintions = MyDefinitionManager.Static.GetAllDefinitions();
                foreach (var def in defintions)
                {
                    if (!(def is MyAmmoMagazineDefinition)) continue;
                    var ammoDef = def as MyAmmoMagazineDefinition;
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(ammoDef.AmmoDefinitionId);
                    if (!(ammo is MyMissileAmmoDefinition)) continue;
                    var shot = ammo as MyMissileAmmoDefinition;
                    if (_ammoInfo.ContainsKey(shot.MissileModelName)) continue;
                    _ammoInfo.Add(shot.MissileModelName, new AmmoInfo(shot.IsExplosive, shot.MissileExplosionDamage, shot.MissileExplosionRadius, shot.DesiredSpeed, shot.MissileMass, shot.BackkickForce));
                }
            }
            catch (Exception ex) { Log.Line($"Exception in GetAmmoDefinitions: {ex}"); }
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
            Session.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _chargeSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ChargeRate", "Shield Charge Rate", 20, 95, 50);
            _hidePassiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HidePassive", "Hide idle shield state", false);
            _hideActiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HideActive", "Hide active shield state", false);

            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") return;

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 30, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 300, 100);
            if (Session.Enforced.Debug == 1) Log.Line($"CreateUI Complete");
        }
        #endregion
    }
}

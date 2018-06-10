using System;
using System.Collections.Generic;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

                //Shield.CubeGrid.OnIsStaticChanged += CheckStatic;
                if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                Session.Instance.Components.Add(this);
                Shield.CubeGrid.Components.Add(SGridComponent);
                StorageSetup();
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                if (Session.Enforced.Debug == 1) Log.Line($"pre-Init complete");
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public DefenseShields()
        {
            SGridComponent = new ShieldGridComponent(this);
        }

        private void HierarchyChanged(IMyCubeGrid myCubeGrid)
        {
            if (_hierarchyChanged)
            {
                _hierarchyDelayed = true;
                return;
            }
            _hierarchyChanged = true;
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Logical);
            SGridComponent.GetSubGrids.Clear();
            for (int i = 0; i < gotGroups.Count; i++) SGridComponent.GetSubGrids.Add(gotGroups[i]);
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!Session.EnforceInit && (_enforceTick == 0 || _tick - _enforceTick > 60))
                {
                    _enforceTick = _tick;
                    if (Session.IsServer) ServerEnforcementSetup();
                    else ClientEnforcementRequest();
                    return;
                }

                if (AllInited || !Shield.IsFunctional || ConnectCheck(true)) return;

                if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);

                if (!MainInit && Shield.IsFunctional)
                {
                    Definition = DefinitionManager.Get(Shield.BlockDefinition.SubtypeId);

                    var enableState = Shield.Enabled;

                    if (enableState)
                    {
                        Shield.Enabled = false;
                        Shield.Enabled = true;
                    }

                    if (Shield.CubeGrid.Physics.IsStatic)
                    {
                        GridIsMobile = false;
                        _shapeAdjusted = false;
                        _shapeLoaded = false;
                    }
                    else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                    CreateUi();
                    PowerInit();

                    if (Session.Enforced.Debug == 1) Log.Line($"MainInit complete");

                    MainInit = true;
                }

                if (!HealthCheck()) return;

                if (!PhysicsInit)
                {
                    SpawnEntities();
                    CleanUp(3);

                    switch (Definition.Name)
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

                if (AllInited || !PhysicsInit || !MainInit || !Shield.IsFunctional) return;

                if (Session.Enforced.Debug == 1) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - {Shield.BlockDefinition.SubtypeId} is functional - tick:{_tick.ToString()}");
                Entity.TryGetSubpart("Rotor", out _subpartRotor);

                if (!Session.DedicatedServer) BlockParticleCreate();

                if (Session.Enforced.Debug == 1) Log.Line($"AnimateInit complete");

                AllInited = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation100: {ex}"); }
        }

        private bool HealthCheck()
        {
            HardDisable = false || Shield.EntityId != UtilsStatic.ThereCanBeOnlyOne(Shield) || Definition.Name == "DefenseShieldsST" &&
                !Shield.CubeGrid.Physics.IsStatic || Definition.Name == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic;

            if (!HardDisable) return true;
            if (Session.Enforced.Debug == 1) Log.Line($"HardDisable is triggered - {Shield.BlockDefinition.SubtypeId}");

            _startupWarning = UtilsStatic.CheckShieldType(Shield, _startupWarning);

            return false;
        }

        private void SpawnEntities()
        {
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
            if (Session.Enforced.Debug == 1) Log.Line($"SpawnEntities complete");
        }

        private void StorageSetup()
        {
            Storage = Shield.Storage;
            DsSet = new DefenseShieldsSettings(Shield);
            DsSet.LoadSettings();
            UpdateSettings(DsSet.Settings);
            if (Session.Enforced.Debug == 1) Log.Line($"StorageSetup complete");
        }

        private void ClientEnforcementRequest()
        {
            if (Session.Enforced.Version > 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Local enforcements found, bypassing request - IsServer? {Session.IsServer}");
            }
            else 
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientEnforcementRequest Check finished - Enforcement Request?: {Session.Enforced.Version <= 0}");
                Enforcements.EnforcementRequest(Shield.EntityId);
            }
        }

        private static void ServerEnforcementSetup()
        {
            if (Session.Enforced.Version > 0) Session.EnforceInit = true;
            else Log.Line($"Server has failed to set its own enforcements!! Report this as a bug");

            if (Session.Enforced.Debug == 1) Log.Line($"ServerEnforcementSetup\n{Session.Enforced}");
        }

        private void PowerInit()
        {
            try
            {
                HardDisable = false;
                _power = 0.0000000001f;
                _shieldCurrentPower = _power;
                Sink.Update();

                Shield.AppendingCustomInfo += AppendingCustomInfo;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit complete");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }
        #endregion

        #region Create UI
        private void CreateUi()
        {
            Session.Instance.ControlsLoaded = true;
            UtilsStatic.RemoveOreUi();
            _chargeSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ChargeRate", "Shield Charge Rate", 20, 95, 50);

            if (Definition.Name != "DefenseShieldsST")
            {
                _extendFit = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ExtendFit", "Extend Shield", false);
                _sphereFit = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "SphereFit", "Sphere Fit", false);
                _fortifyShield = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ShieldFortify", "Fortify Shield", false);
            }
            else
            {
                _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 600, 100);
                _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 30, 600, 100);
                _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 600, 100);
            }

            _hidePassiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HidePassive", "Hide idle shield state", false);
            _hideActiveCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HideActive", "Hide active shield state", false);
            if (Session.Enforced.Debug == 1) Log.Line($"CreateUI Complete");
        }
        #endregion
    }
}

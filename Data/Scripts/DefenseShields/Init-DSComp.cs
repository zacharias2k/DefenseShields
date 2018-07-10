using System;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public enum ShieldType
        {
            Station,
            LargeGrid,
            SmallGrid,
        };

        #region Startup Logic
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

                if (Session.Enforced.Debug == 1) Log.Line($"pre-Init complete");
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void HierarchyChanged(IMyCubeGrid myCubeGrid = null)
        {
            try
            {
                Log.Line($"HierarchyChanged");
                if (_tick == _hierarchyTick) return;
                if (_hierarchyTick > _tick - 9)
                {
                    _hierarchyDelayed = true;
                    return;
                }
                _hierarchyTick = _tick;

                var gotGroups = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Mechanical);
                ShieldComp?.GetSubGrids.Clear();
                _connectedGrids.Clear();
                for (int i = 0; i < gotGroups.Count; i++) ShieldComp?.GetSubGrids.Add(gotGroups[i]);
            }
            catch (Exception ex) { Log.Line($"Exception in HierarchyChanged: {ex}"); }
        }

        private void SetShieldType()
        {
            if (Shield.CubeGrid.Physics.IsStatic) ShieldMode = ShieldType.Station;
            else if (Shield.CubeGrid.GridSizeEnum == MyCubeSize.Large) ShieldMode = ShieldType.LargeGrid;
            else
            {
                ShieldMode = ShieldType.SmallGrid;
                _shieldModel = "\\Models\\Cubes\\ShieldActiveBase_LOD4.mwm";
            }

            if (ShieldMode != ShieldType.Station) GridIsMobile = true;

            switch (ShieldMode)
            {
                case ShieldType.Station:
                    _shieldRatio = Session.Enforced.StationRatio;
                    break;
                case ShieldType.LargeGrid:
                    _shieldRatio = Session.Enforced.LargeShipRatio;
                    break;
                case ShieldType.SmallGrid:
                    _shieldRatio = Session.Enforced.SmallShipRatio;
                    break;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                _shields.Add(Entity.EntityId, this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                Session.Instance.Components.Add(this);
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;

                StorageSetup();

                if (!Shield.CubeGrid.Components.Has<ShieldGridComponent>())
                {
                    Shield.CubeGrid.Components.Add(ShieldComp);
                    ShieldComp.DefaultO2 = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(Shield.PositionComp.WorldVolume.Center);
                }
                else Shield.CubeGrid.Components.TryGet(out ShieldComp);
                if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        private bool MasterElection()
        {
            if (ShieldComp.DefenseShields != this && ShieldComp.DefenseShields != null) return true;
            ShieldComp.BoundingRange = 0f;
            ShieldComp.ShieldPercent = 0f;
            ShieldComp.Warming = false;
            ShieldComp.Starting = false;
            ShieldComp.ShieldActive = false;
            ShieldComp.ModulationPassword = null;
            ShieldComp.ComingOnline = false;
            ShieldComp.DefenseShields = this;
            return false;
        }

        public void PostInit()
        {
            try
            {
                if (AllInited) return;
                if (!Session.EnforceInit)
                {
                    if (Session.IsServer) ServerEnforcementSetup();
                    else if (_enforceTick == 0 || _tick - _enforceTick > 60)
                    {
                        _enforceTick = _tick;
                        ClientEnforcementRequest();
                    }
                    if (!Session.EnforceInit) return;
                }

                if (AllInited || MasterElection() || !Shield.IsFunctional) return;
                if (!HealthInited)
                {
                    if (ConnectCheck(true)) return;
                    if (!HealthCheck()) return;
                    HealthInited = true;
                }

                if (!MainInit && Shield.IsFunctional)
                {
                    PowerInit();

                    ((MyCubeBlock)Shield).ChangeOwner(Shield.CubeGrid.BigOwners[0], MyOwnershipShareModeEnum.Faction);

                    SetShieldType();
                    if (ShieldMode == ShieldType.Station)
                    {
                        _shapeAdjusted = false;
                        _shapeLoaded = false;
                    }

                    DsUi.CreateUi(Shield);
                    MainInit = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"MainInit complete");
                }

                if (!PhysicsInit)
                {
                    SpawnEntities();
                    CleanUp(3);
                    PhysicsInit = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"PhysicsInit complete");
                }

                if (AllInited || !PhysicsInit || !MainInit || !Shield.IsFunctional) return;
                if (!BlockReady()) return;

                if (Session.Enforced.Debug == 1) Log.Line($"AnimateInit complete");
                if (Session.EnforceInit) AllInited = true;
            }
            catch (Exception ex) { Log.Line($"Exception in DSControl PostInit: {ex}"); }
        }

        private bool HealthCheck()
        {
            HardDisable = false || Shield.EntityId != UtilsStatic.ThereCanBeOnlyOne(Shield);
            if (!HardDisable) return true;
            if (Session.Enforced.Debug == 1) Log.Line($"HardDisable is triggered - {Shield.BlockDefinition.SubtypeId}");

            //_startupWarning = UtilsStatic.CheckShieldType(Shield, _startupWarning);

            return false;
        }

        private void SpawnEntities()
        {
            var parent = (MyEntity)Shield.CubeGrid;

            _shellPassive = Spawn.EmptyEntity("dShellPassive", $"{Session.Instance.ModPath()}\\Models\\Cubes\\ShieldPassive_LOD0.mwm", parent, true);
            _shellPassive.Render.CastShadows = false;
            _shellPassive.IsPreview = true;
            _shellPassive.Render.Visible = true;
            _shellPassive.Render.RemoveRenderObjects();
            _shellPassive.Render.UpdateRenderObject(true);
            _shellPassive.Render.UpdateRenderObject(false);
            _shellPassive.Save = false;

            _shellActive = Spawn.EmptyEntity("dShellActive", $"{Session.Instance.ModPath()}{_shieldModel}", parent, true);
            _shellActive.Render.CastShadows = false;
            _shellActive.IsPreview = true;
            _shellActive.Render.Visible = true;
            _shellActive.Render.RemoveRenderObjects();
            _shellActive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(false);
            _shellActive.Save = false;
            _shellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Transparent, 0f);

            ShieldEnt = Spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
            //_shield = Spawn.SpawnBlock("dShield", $"{Shield.EntityId}", true, false, false, false, true, Shield.OwnerId);
            ShieldEnt.Render.CastShadows = false;
            ShieldEnt.Render.RemoveRenderObjects();
            ShieldEnt.Render.UpdateRenderObject(true);
            ShieldEnt.Render.Visible = true;
            ShieldEnt.Save = false;
            if (Session.Enforced.Debug == 1) Log.Line($"SpawnEntities complete");
        }

        private void StorageSetup()
        {
            Storage = Shield.Storage;
            DsSet = new DefenseShieldsSettings(Shield);
            ShieldComp = new ShieldGridComponent(this, DsSet);
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

        private void PowerPreInit()
        {
            try
            {
                if (Sink == null)
                {
                    Sink = new MyResourceSinkComponent();
                }
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.Init(MyStringHash.GetOrCompute("Defense"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                HardDisable = false;
                _shieldCurrentPower = _power;
                Sink.Update();

                Shield.AppendingCustomInfo += AppendingCustomInfo;
                Shield.RefreshCustomInfo();

                var enableState = Shield.Enabled;
                if (enableState)
                {
                    Shield.Enabled = false;
                    Shield.Enabled = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit complete");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool BlockReady()
        {
            if (Shield.IsWorking) return true;
            Log.Line($"Block was not ready {_tick}");
            Shield.Enabled = false;
            Shield.Enabled = true;
            return Shield.IsWorking;
        }
        #endregion
    }
}

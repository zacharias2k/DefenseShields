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
            Unknown
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
            catch (Exception ex) { Log.Line($"Exception in Controller EntityInit: {ex}"); }
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
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (ShieldComp == null ||_tick == _hierarchyTick) return;
                if (_hierarchyTick > _tick - 9)
                {
                    _hierarchyDelayed = true;
                    return;
                }
                _hierarchyTick = _tick;
                _oldBlockCount = -1;
                UpdateSubGrids();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller HierarchyChanged: {ex}"); }
        }

        private void UpdateSubGrids()
        {
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Physical);
            ShieldComp.GetSubGrids.Clear();
            ShieldComp.GetLinkedGrids.Clear();
            for (int i = 0; i < gotGroups.Count; i++)
            {
                var sub = gotGroups[i];
                if (sub == null) continue;
                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Mechanical))ShieldComp.GetSubGrids.Add(sub);
                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Physical)) ShieldComp.GetLinkedGrids.Add(sub);
            }
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

                if (AllInited || !Shield.IsFunctional || ShieldComp.EmitterMode < 0) return;

                if (!MainInit && Shield.IsFunctional)
                {
                    PowerInit();
                    ((MyCubeBlock)Shield).ChangeOwner(Shield.CubeGrid.BigOwners[0], MyOwnershipShareModeEnum.Faction);

                    Session.Instance.CreateControllerElements(Shield);
                    SetShieldType(false, true);

                    CleanUp(3);
                    MainInit = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller MainInit complete");
                }

                if (AllInited || !MainInit || !Shield.IsFunctional || !BlockReady()) return;

                if (Session.EnforceInit) AllInited = true;
                if (Session.Enforced.Debug == 1) Log.Line($"Controller AnimateInit complete");
            }
            catch (Exception ex) { Log.Line($"Exception in Controller PostInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Shield.Storage;
            if (DsSet == null) DsSet = new DefenseShieldsSettings(Shield);
            if (ShieldComp == null) ShieldComp = new ShieldGridComponent(this, DsSet);
            else if (ShieldComp.Settings == null) ShieldComp.Settings = DsSet;
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

        private void SetShieldType(bool quickCheck, bool hideShells)
        {
            var noChange = false;
            var oldMode = ShieldMode;
            switch (ShieldComp.EmitterMode)
            {
                case 0:
                    ShieldMode = ShieldType.Station;
                    break;
                case 1:
                    ShieldMode = ShieldType.LargeGrid;
                    break;
                case 2:
                    ShieldMode = ShieldType.SmallGrid;
                    break;
                default:
                    ShieldMode = ShieldType.Unknown;
                    Suspended = true;
                    break;
            }
            if (ShieldMode == oldMode) noChange = true;

            if ((quickCheck && noChange) || ShieldMode == ShieldType.Unknown) return;

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

            switch (ShieldMode)
            {
                case ShieldType.Station:
                    _shapeAdjusted = false;
                    _shapeLoaded = false;
                    UpdateDimensions = true;
                    break;
                case ShieldType.LargeGrid:
                    _createMobileShape = true;
                    break;
                case ShieldType.SmallGrid:
                    _shieldModel = "\\Models\\Cubes\\ShieldActiveBase_LOD4.mwm";
                    _createMobileShape = true;
                    break;
            }

            GridIsMobile = ShieldMode != ShieldType.Station;

            DsUi.CreateUi(Shield);
            InitEntities(true, hideShells);
        }

        private void InitEntities(bool fullInit, bool hideShells)
        {
            ShieldEnt?.Close();
            _shellActive?.Close();
            _shellPassive?.Close();

            if (!fullInit)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"InitEntities: shield mode: {ShieldMode}, remove complete");
                return;
            }

            var parent = (MyEntity)Shield.CubeGrid;
            _shellPassive = Spawn.EmptyEntity("dShellPassive", $"{Session.Instance.ModPath()}\\Models\\Cubes\\ShieldPassive.mwm", parent, true);
            _shellPassive.Render.CastShadows = false;
            _shellPassive.IsPreview = true;
            _shellPassive.Render.Visible = true;
            _shellPassive.Render.RemoveRenderObjects();
            _shellPassive.Render.UpdateRenderObject(true);
            if (hideShells) _shellPassive.Render.UpdateRenderObject(false);
            _shellPassive.Save = false;

            _shellActive = Spawn.EmptyEntity("dShellActive", $"{Session.Instance.ModPath()}{_shieldModel}", parent, true);
            _shellActive.Render.CastShadows = false;
            _shellActive.IsPreview = true;
            _shellActive.Render.Visible = true;
            _shellActive.Render.RemoveRenderObjects();
            _shellActive.Render.UpdateRenderObject(true);
            if (hideShells) _shellActive.Render.UpdateRenderObject(false);
            _shellActive.Save = false;
            _shellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Transparent, 0f);

            ShieldEnt = Spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
            //_shield = Spawn.SpawnBlock("dShield", $"{Shield.EntityId}", true, false, false, false, true, Shield.OwnerId);
            ShieldEnt.Render.CastShadows = false;
            ShieldEnt.Render.RemoveRenderObjects();
            ShieldEnt.Render.UpdateRenderObject(true);
            ShieldEnt.Render.Visible = true;
            ShieldEnt.Save = false;

            if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            if (Session.Enforced.Debug == 1) Log.Line($"InitEntities: shield mode: {ShieldMode}, spawn complete");
        }

        private bool Election()
        {
            if (ShieldComp == null)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Controller Election: ShieldComp is null, mode: {ShieldMode}");
                var girdHasShieldComp = Shield.CubeGrid.Components.Has<ShieldGridComponent>();

                if (girdHasShieldComp)
                {
                    Shield.CubeGrid.Components.TryGet(out ShieldComp);
                    StorageSetup();
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller Election: grid had Comp, mode: {ShieldMode}");
                }
                {
                    StorageSetup();
                    Shield.CubeGrid.Components.Add(ShieldComp);
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller Election: grid didn't have Comp, mode: {ShieldMode}");
                }
                ShieldComp.DefaultO2 = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(Shield.PositionComp.WorldVolume.Center);
            }
            if (ShieldComp.DefenseShields != null) return false;
            if (Session.Enforced.Debug == 1) Log.Line($"Controller Election: Shield controller election is being held, mode was: {ShieldMode}");
            ShieldComp.Warming = false;
            ShieldComp.BoundingRange = 0f;
            ShieldComp.ShieldPercent = 0f;
            ShieldComp.Starting = false;
            ShieldComp.ShieldActive = false;
            ShieldComp.ModulationPassword = null;
            ShieldComp.ComingOnline = false;
            ShieldComp.DefenseShields = this;
            SetShieldType(false, false);
            if (Session.Enforced.Debug == 1) Log.Line($"Controller Election: Shield controller election was held, new mode is: {ShieldMode}");
            return true;
        }

        private bool Suspend()
        {
            var isStatic = Shield.CubeGrid.IsStatic;
            if (ShieldMode != ShieldType.Station && isStatic) Suspended = true;
            else if (ShieldMode == ShieldType.Station && !isStatic) Suspended = true;
            else
            {
                if (Suspended)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller Suspend: Shield controller unsuspending");
                    UpdateSubGrids();
                    BackGroundChecks();
                    _shapeLoaded = true;
                    _blocksChanged = true;
                    BlockChanged();
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller Suspend: Shield controller mode was: {ShieldMode}");
                    SetShieldType(false, false);
                    if (Session.Enforced.Debug == 1) Log.Line($"Controller Suspend: Shield controller mode is now: {ShieldMode}");
                    _shellActive.Render.UpdateRenderObject(false);

                    GetModulationInfo();
                    _updateRender = true;
                    _shapeLoaded = true;
                    Suspended = false;
                    _genericDownLoop = 0;
                    Log.Line($"Controller Unsuspended - CM:{ShieldMode} - EM:{ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{ShieldComp.BoundingRange}");
                }
                Suspended = false;
            }

            if (Suspended)
            {
                //Log.Line($"Controller Suspended - CM:{ShieldMode} - EM:{ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{ShieldComp.BoundingRange}");
                SetShieldType(true, true);
                //_genericDownLoop = 0;
            }
            //Log.Line($"Controller Suspended - CM:{ShieldMode} - EM:{ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{ShieldComp.BoundingRange}");
            return Suspended;
        }

        private bool WarmUpSequence()
        {
            if (ShieldComp.DefenseShields != this) return false;
            if (ShieldComp.Warming) return true;

            if (ShieldComp.Starting)
            {
                if (ShieldComp?.EmitterPrime != null || ShieldComp?.EmitterBeta != null)
                {
                    if (GridIsMobile)
                    {
                        CreateHalfExtents();
                        GetShapeAdjust();
                        MobileUpdate();
                    }
                    else
                    {
                        UpdateDimensions = true;
                        if (UpdateDimensions) RefreshDimensions();
                    }
                    _shapeAdjusted = false;
                    _blocksChanged = false;
                    ShieldComp.CheckEmitters = true;
                    Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
                    ShieldComp.Warming = true;
                    return false;
                }
                return false;
            }

            if (!PowerOnline()) return false;
            HadPowerBefore = true;
            _hierarchyDelayed = false;

            HierarchyChanged();
            UpdateBlockCount();
            BackGroundChecks();
            GetModulationInfo();

            ControlBlockWorking = AllInited && Shield.IsWorking && Shield.IsFunctional;
            if (Session.Enforced.Debug == 1) Log.Line($"start warmup enforced:\n{Session.Enforced}");
            if (Session.Enforced.Debug == 1) Log.Line($"start warmup buffer:{ShieldBuffer} - BlockWorking:{ControlBlockWorking} - Active:{ShieldComp.ShieldActive}");
            ShieldComp.Starting = true;
            return false;
        }
        #endregion
    }
}

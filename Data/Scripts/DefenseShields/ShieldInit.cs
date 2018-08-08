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

                if (Session.Enforced.Debug == 1) Log.Line($"pre-Init: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in Controller EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Shield.CubeGrid.Physics == null) return;
                _shields.Add(Entity.EntityId, this);
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(EllipsoidOxyProvider);
                Session.Instance.Components.Add(this);
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded += BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved += BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded += FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved += FatBlockRemoved;
                StorageSetup();

                PowerInit();
                if (!Shield.CubeGrid.Components.Has<ShieldGridComponent>())
                {
                    Shield.CubeGrid.Components.Add(ShieldComp);
                    ShieldComp.DefaultO2 = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(Shield.PositionComp.WorldVolume.Center);
                }
                else
                {
                    Shield.CubeGrid.Components.TryGet(out ShieldComp);
                    if (ShieldComp != null && ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
                }
                if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller UpdateOnceBeforeFrame: {ex}"); }
        }

        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded += BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved += BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded += FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved += FatBlockRemoved;
            }
            else
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded -= BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved -= BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded -= FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved -= FatBlockRemoved;
            }
        }

        private void BlockAdded(IMySlimBlock mySlimBlock)
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"BlockAdded: ShieldId [{Shield?.EntityId}]");
                _blockAdded = true;
                _blockChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }
        
        private void BlockRemoved(IMySlimBlock mySlimBlock)
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"BlockRemoved: ShieldId [{Shield?.EntityId}]");
                _blockRemoved = true;
                _blockChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        } 

        private void FatBlockAdded(MyCubeBlock mySlimBlock)
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"FatBlockAdded: ShieldId [{Shield?.EntityId}]");
                _functionalAdded = true;
                _functionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }
        
        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"FatBlockRemoved: ShieldId [{Shield?.EntityId}]");
                _functionalRemoved = true;
                _functionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (Session.Enforced.Debug >= 2 && myCubeGrid != null) Log.Line($"HierarchyChanged: {myCubeGrid.DebugName} - ShieldId [{Shield.EntityId}]");
                if (ShieldComp == null ||_tick == _hierarchyTick) return;
                if (_hierarchyTick > _tick - 9)
                {
                    _hierarchyDelayed = true;
                    return;
                }
                _hierarchyTick = _tick;
                UpdateSubGrids();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller HierarchyChanged: {ex}"); }
        }

        private void UpdateSubGrids(bool force = false)
        {
            var checkGroups = Shield.IsWorking && Shield.IsFunctional && DsStatus.State.Online;
            if (Session.Enforced.Debug >= 2) Log.Line($"SubCheckGroups: check:{checkGroups} - SW:{Shield.IsWorking} - SF:{Shield.IsFunctional} - Offline:{DsStatus.State.Online} - ShieldId [{Shield.EntityId}]");
            if (!checkGroups && !force) return;
            var gotGroups = MyAPIGateway.GridGroups.GetGroup(Shield.CubeGrid, GridLinkTypeEnum.Physical);
            if (gotGroups.Count == ShieldComp.GetLinkedGrids.Count) return;
            if (Session.Enforced.Debug == 1) Log.Line($"SubGroupCnt: subCountChanged:{ShieldComp.GetLinkedGrids.Count != gotGroups.Count} - old:{ShieldComp.GetLinkedGrids.Count} - new:{gotGroups.Count} - ShieldId [{Shield.EntityId}]");

            lock (ShieldComp.GetSubGrids) ShieldComp.GetSubGrids.Clear();
            lock (ShieldComp.GetLinkedGrids) ShieldComp.GetLinkedGrids.Clear();
            var c = 0;
            for (int i = 0; i < gotGroups.Count; i++)
            {
                var sub = gotGroups[i];
                if (sub == null) continue;
                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Mechanical)) lock (ShieldComp.GetSubGrids) ShieldComp.GetSubGrids.Add(sub);
                if (MyAPIGateway.GridGroups.HasConnection(Shield.CubeGrid, sub, GridLinkTypeEnum.Physical)) lock (ShieldComp.GetLinkedGrids) ShieldComp.GetLinkedGrids.Add(sub);
            }
            _blockChanged = true;
            _functionalChanged = true;
        }

        private void StorageSetup()
        {
            Storage = Shield.Storage;
            if (DsSet == null) DsSet = new DefenseShieldsSettings(Shield);
            if (DsStatus == null) DsStatus = new ShieldStatus(Shield);
            if (ShieldComp == null) ShieldComp = new ShieldGridComponent(this);
            DsSet.LoadSettings();
            UpdateSettings(DsSet.Settings);
            DsStatus.LoadState();
            if (Session.Enforced.Debug == 1) Log.Line($"StorageSetup: ShieldId [{Shield.EntityId}]");
        }

        public override bool IsSerialized()
        {
            if (Session.IsServer)
            {
                if (Storage != null && AllInited)
                {
                    DsStatus.SaveState();
                    DsSet.SaveSettings();
                }
                if (Session.Enforced.Debug == 1) Log.Line($"IsSerializedCalled: saved before replication - ShieldId [{Shield.EntityId}]");
            }
            return false;
        }

        private void ClientEnforcementRequest()
        {
            if (Session.Enforced.Version > 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Localenforcements: bypassing request - IsServer? {Session.IsServer} - ShieldId [{Shield.EntityId}]");
            }
            else 
            {
                if (Session.Enforced.Debug == 1) Log.Line($"ClientEnforcementRequest: Check finished - Enforcement Request?: {Session.Enforced.Version <= 0} - ShieldId [{Shield.EntityId}]");
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
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool BlockReady()
        {
            if (Shield.IsWorking) return true;
            if (Session.Enforced.Debug == 1) Log.Line($"BlockNotReady: was not ready {_tick} - ShieldId [{Shield.EntityId}]");
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
                    DsStatus.State.Suspended = true;
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
                    _shapeChanged = false;
                    UpdateDimensions = true;
                    break;
                case ShieldType.LargeGrid:
                    _updateMobileShape = true;
                    break;
                case ShieldType.SmallGrid:
                    _modelActive = "\\Models\\Cubes\\ShieldActiveBase_LOD4.mwm";
                    _updateMobileShape = true;
                    break;
            }

            GridIsMobile = ShieldMode != ShieldType.Station;

            DsUi.CreateUi(Shield);
            InitEntities(true);
        }

        public void SelectPassiveShell()
        {
            switch (DsSet.Settings.ShieldShell)
            {
                case 0:
                    _modelPassive = ModelMediumReflective;
                    break;
                case 1:
                    _modelPassive = ModelHighReflective;
                    break;
                case 2:
                    _modelPassive = ModelLowReflective;
                    break;
                case 3:
                    _modelPassive = ModelRed;
                    break;
                case 4:
                    _modelPassive = ModelBlue;
                    break;
                case 5:
                    _modelPassive = ModelGreen;
                    break;
                case 6:
                    _modelPassive = ModelPurple;
                    break;
                case 7:
                    _modelPassive = ModelGold;
                    break;
                case 8:
                    _modelPassive = ModelOrange;
                    break;
                case 9:
                    _modelPassive = ModelCyan;
                    break;
                default:
                    _modelPassive = ModelMediumReflective;
                    break;
            }
        }

        public void UpdatePassiveModel()
        {
            _shellPassive.Render.Visible = true;
            _shellPassive.RefreshModels($"{Session.Instance.ModPath()}{_modelPassive}", null);
            _shellPassive.Render.RemoveRenderObjects();
            _shellPassive.Render.UpdateRenderObject(true);
            if (Session.Enforced.Debug == 1) Log.Line($"UpdatePassiveModel: modelString:{_modelPassive} - ShellNumber:{DsSet.Settings.ShieldShell} - ShieldId [{Shield.EntityId}]");
        }

        private void InitEntities(bool fullInit)
        {
            ShieldEnt?.Close();
            _shellActive?.Close();
            _shellPassive?.Close();

            if (!fullInit)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"InitEntities: mode: {ShieldMode}, remove complete - ShieldId [{Shield.EntityId}]");
                return;
            }

            SelectPassiveShell();
            if (!Session.DedicatedServer)
            {
                var parent = (MyEntity)Shield.CubeGrid;
                _shellPassive = Spawn.EmptyEntity("dShellPassive", $"{Session.Instance.ModPath()}{_modelPassive}", parent, true);
                _shellPassive.Render.CastShadows = false;
                _shellPassive.IsPreview = true;
                _shellPassive.Render.Visible = true;
                _shellPassive.Render.RemoveRenderObjects();
                _shellPassive.Render.UpdateRenderObject(true);
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Save = false;
                _shellPassive.SyncFlag = false;

                _shellActive = Spawn.EmptyEntity("dShellActive", $"{Session.Instance.ModPath()}{_modelActive}", parent, true);
                _shellActive.Render.CastShadows = false;
                _shellActive.IsPreview = true;
                _shellActive.Render.Visible = true;
                _shellActive.Render.RemoveRenderObjects();
                _shellActive.Render.UpdateRenderObject(true);
                _shellActive.Render.UpdateRenderObject(false);
                _shellActive.Save = false;
                _shellActive.SyncFlag = false;
                _shellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Transparent, 0f);
            }

            ShieldEnt = Spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
            ShieldEnt.Render.CastShadows = false;
            ShieldEnt.Render.RemoveRenderObjects();
            ShieldEnt.Render.UpdateRenderObject(true);
            ShieldEnt.Render.Visible = true;
            ShieldEnt.Save = false;
            _shieldEntRendId = ShieldEnt.Render.GetRenderObjectID();

            if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            if (Session.Enforced.Debug == 1) Log.Line($"InitEntities: mode: {ShieldMode}, spawn complete - ShieldId [{Shield.EntityId}]");
        }

        public void PostInit()
        {
            try
            {
                if (AllInited || Shield.CubeGrid.Physics == null) return;
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
                if (AllInited || !Shield.IsFunctional || ShieldComp.EmitterMode < 0)
                {
                    if (_tick % 600 == 0)
                    {
                        GridOwnsController();
                        Shield.RefreshCustomInfo();
                    }
                    return;
                }

                if (!MainInit && Shield.IsFunctional)
                {
                    Session.Instance.CreateControllerElements(Shield);
                    SetShieldType(false, true);

                    CleanUp(3);
                    MainInit = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"MainInit: ShieldId [{Shield.EntityId}]");
                }

                if (AllInited || !MainInit || !Shield.IsFunctional || !BlockReady()) return;

                if (Session.EnforceInit)
                {
                    AllInited = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"AllInited: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in Controller PostInit: {ex}"); }
        }

        private bool WarmUpSequence()
        {
            if (Warming) return true;
            if (Starting)
            {
                Warming = true;
                return true;
            }

            if (!PowerOnline()) return false;
            HadPowerBefore = true;
            _blockChanged = true;
            _functionalChanged = true;

            ResetShape(false, true);
            ResetShape(false, false);
            _oldGridHalfExtents = DsStatus.State.GridHalfExtents;
            _oldEllipsoidAdjust = DsStatus.State.EllipsoidAdjust;
            GetModulationInfo();

            Starting = true;
            ControlBlockWorking = AllInited && Shield.IsWorking && Shield.IsFunctional;
            if (Session.Enforced.Debug == 1) Log.Line($"Warming: buffer:{DsStatus.State.Buffer} - BlockWorking:{ControlBlockWorking} - Active:{DsStatus.State.Online} - ShieldId [{Shield.EntityId}]");
            return false;
        }
        #endregion
    }
}

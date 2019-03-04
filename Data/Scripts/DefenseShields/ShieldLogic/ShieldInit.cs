using Sandbox.Game.Entities;

namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using Support;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public partial class DefenseShields
    {
        #region Startup Logic
        internal void AssignSlots()
        {
            LogicSlot = Session.GetSlot();
            MonitorSlot = LogicSlot - 1 < 0 ? Session.Instance.EntSlotScaler - 1 : LogicSlot - 1;
        }


        internal void SelectPassiveShell()
        {
            try
            {
                switch (DsSet.Settings.ShieldShell)
                {
                    case 0:
                        _modelPassive = ModelMediumReflective;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 1:
                        _modelPassive = ModelHighReflective;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 2:
                        _modelPassive = ModelLowReflective;
                        _hideColor = false;
                        _supressedColor = false;
                        break;
                    case 3:
                        _modelPassive = ModelRed;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 4:
                        _modelPassive = ModelBlue;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 5:
                        _modelPassive = ModelGreen;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 6:
                        _modelPassive = ModelPurple;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 7:
                        _modelPassive = ModelGold;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 8:
                        _modelPassive = ModelOrange;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    case 9:
                        _modelPassive = ModelCyan;
                        _hideColor = true;
                        _supressedColor = false;
                        break;
                    default:
                        _modelPassive = ModelMediumReflective;
                        _hideColor = false;
                        _supressedColor = false;
                        break;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SelectPassiveShell: {ex}"); }
        }

        internal void UpdatePassiveModel()
        {
            try
            {
                if (_shellPassive == null) return;
                _shellPassive.Render.Visible = true;
                _shellPassive.RefreshModels($"{Session.Instance.ModPath()}{_modelPassive}", null);
                _shellPassive.Render.RemoveRenderObjects();
                _shellPassive.Render.UpdateRenderObject(true);
                _hideShield = false;
                if (Session.Enforced.Debug == 3) Log.Line($"UpdatePassiveModel: modelString:{_modelPassive} - ShellNumber:{DsSet.Settings.ShieldShell} - ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatePassiveModel: {ex}"); }
        }

        private bool PostInit()
        {
            try
            {
                if (_isServer && (ShieldComp.EmitterMode < 0 || (ShieldComp.EmitterMode == 0 && ShieldComp.StationEmitter == null ) || ShieldComp.EmittersSuspended || !IsFunctional))
                {
                    if (_tick600)
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"PostInit: Server Not Ready - GridComp:{MyGrid.Components.Has<ShieldGridComponent>()} - InvalidMode:{ShieldComp.EmitterMode < 0} - Functional:{IsFunctional} - EmitterSus:{ShieldComp.EmittersSuspended} - StationEmitterNull:{ShieldComp.StationEmitter == null } - EmitterNull:{ShieldComp.StationEmitter?.Emitter == null} - ShieldId [{Shield.EntityId}]");
                        Shield.RefreshCustomInfo();
                    }
                    return false;
                }

                MyEntity emitterEnt = null;
                if (RequestEnforcement() || _clientNotReady || (!_isServer && (DsState.State.Mode < 0 || !MyEntities.TryGetEntityById(DsState.State.ActiveEmitterId, out emitterEnt) || !(emitterEnt is IMyUpgradeModule))))
                {
                    return false;
                }

                Session.Instance.CreateControllerElements(Shield);
                SetShieldType(false);
                if (!Session.Instance.DsAction)
                {
                    Session.AppendConditionToAction<IMyUpgradeModule>((a) => Session.Instance.DsActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<DefenseShields>() != null && Session.Instance.DsActions.Contains(a.Id));
                    Session.Instance.DsAction = true;
                }

                if (!IsFunctional) return false;

                if (_mpActive && _isServer) DsState.NetworkUpdate();

                _allInited = true;

                if (Session.Enforced.Debug == 3) Log.Line($"AllInited: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in Controller PostInit: {ex}"); }
            return true;
        }

        private void ResetEntity()
        {
            _allInited = false;
            Warming = false;
            WarmedUp = false;

            _resetEntity = false;
            _prevShieldActive = false;
            _hadPowerBefore = false;
            ControlBlockWorking = false;

            ResetComp();

            if (_isServer)
            {
                GridIntegrity();
                ShieldChangeState();
            }
            if (Session.Enforced.Debug == 3) Log.Line($"ResetEntity: ShieldId [{Shield.EntityId}]");
        }

        private void WarmUpSequence()
        {
            if (_isServer)
            {
                _hadPowerBefore = true;
                ControlBlockWorking = _allInited && IsWorking && IsFunctional;
            }

            _blockChanged = true;
            _functionalChanged = true;

            ResetShape(false);
            ResetShape(false, true);
            
            _oldGridHalfExtents = DsState.State.GridHalfExtents;
            _oldEllipsoidAdjust = DsState.State.EllipsoidAdjust;

            _updateRender = true;
            Warming = true;
        }

        private void StorageSetup()
        {
            try
            {
                var isServer = MyAPIGateway.Multiplayer.IsServer;

                if (DsSet == null) DsSet = new ControllerSettings(Shield);
                if (DsState == null) DsState = new ControllerState(Shield);
                if (Shield.Storage == null) DsState.StorageInit();
                if (!isServer)
                {
                    var enforcement = Enforcements.LoadEnforcement(Shield);
                    if (enforcement != null) Session.Enforced = enforcement;
                }
                DsSet.LoadSettings();
                if (!DsState.LoadState() && !isServer) _clientNotReady = true;
                UpdateSettings(DsSet.Settings);
                if (isServer)
                {
                    DsState.State.Overload = false;
                    DsState.State.NoPower = false;
                    DsState.State.Remodulate = false;
                    DsState.State.Suspended = false;
                    DsState.State.Waking = false;
                    DsState.State.ActiveEmitterId = 0;
                    DsState.State.FieldBlocked = false;
                    //DsState.State.Sleeping = false;
                    DsState.State.Heat = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in StorageSetup: {ex}"); }
        }

        private void PowerPreInit()
        {
            try
            {
                if (_sink == null) _sink = new MyResourceSinkComponent();
                _resourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                _sink.Init(MyStringHash.GetOrCompute("Defense"), _resourceInfo);
                _sink.AddType(ref _resourceInfo);
                Entity.Components.Add(_sink);
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void CurrentInputChanged(MyDefinitionId resourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            ShieldCurrentPower = sink.CurrentInputByType(GId);
        }

        private void PowerInit()
        {
            try
            {
                ShieldCurrentPower = _power;
                _sink.Update();
                Shield.RefreshCustomInfo();

                var enableState = Shield.Enabled;
                if (enableState)
                {
                    Shield.Enabled = false;
                    Shield.Enabled = true;
                }
                IsWorking = MyCube.IsWorking;
                if (Session.Enforced.Debug == 3) Log.Line($"PowerInit: ShieldId [{Shield.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool RequestEnforcement()
        {
            if (Session.Enforced.Version <= 0)
            {
                if (!_isServer)
                {
                    var enforcement = Enforcements.LoadEnforcement(Shield);
                    if (enforcement != null) Session.Enforced = enforcement;
                    else if (!_requestedEnforcement)
                    {
                        Enforcements.EnforcementRequest(Shield.EntityId);
                        _requestedEnforcement = true;
                    }
                }
            }
            return Session.Enforced.Version <= 0;
        }

        private void SetShieldType(bool quickCheck)
        {
            var noChange = false;
            var oldMode = ShieldMode;
            if (_isServer)
            {
                switch (ShieldComp.EmitterMode)
                {
                    case 0:
                        ShieldMode = ShieldType.Station;
                        DsState.State.Mode = (int)ShieldMode;
                        break;
                    case 1:
                        ShieldMode = ShieldType.LargeGrid;
                        DsState.State.Mode = (int)ShieldMode;
                        break;
                    case 2:
                        ShieldMode = ShieldType.SmallGrid;
                        DsState.State.Mode = (int)ShieldMode;
                        break;
                    default:
                        ShieldMode = ShieldType.Unknown;
                        DsState.State.Mode = (int)ShieldMode;
                        DsState.State.Suspended = true;
                        break;
                }
            }
            else ShieldMode = (ShieldType)DsState.State.Mode;

            if (ShieldMode == oldMode) noChange = true;

            if ((quickCheck && noChange) || ShieldMode == ShieldType.Unknown) return;

            switch (ShieldMode)
            {
                case ShieldType.Station:
                    if (Session.Enforced.StationRatio > 0) _shieldRatio = Session.Enforced.StationRatio;
                    break;
                case ShieldType.LargeGrid:
                    if (Session.Enforced.LargeShipRatio > 0) _shieldRatio = Session.Enforced.LargeShipRatio;
                    break;
                case ShieldType.SmallGrid:
                    if (Session.Enforced.SmallShipRatio > 0) _shieldRatio = Session.Enforced.SmallShipRatio;
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

        private void InitEntities(bool fullInit)
        {
            ShieldEnt?.Close();
            _shellActive?.Close();
            _shellPassive?.Close();

            if (!fullInit)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"InitEntities: mode: {ShieldMode}, remove complete - ShieldId [{Shield.EntityId}]");
                return;
            }

            SelectPassiveShell();
            var parent = (MyEntity)MyGrid;
            if (!_isDedicated)
            {
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

            ShieldEnt = Spawn.EmptyEntity("dShield", null, parent);
            ShieldEnt.Render.CastShadows = false;
            ShieldEnt.Render.RemoveRenderObjects();
            ShieldEnt.Render.UpdateRenderObject(true);
            ShieldEnt.Render.Visible = true;
            ShieldEnt.Save = false;
            _shieldEntRendId = ShieldEnt.Render.GetRenderObjectID();
            _updateRender = true;

            if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            if (Session.Enforced.Debug == 3) Log.Line($"InitEntities: mode: {ShieldMode}, spawn complete - ShieldId [{Shield.EntityId}]");
        }

        private float GridIntegrity(IMyCubeGrid grid = null, bool remove = false)
        {
            var mainSub = false;
            if (grid == null)
            {
                DsState.State.GridIntegrity = 0;
                grid = Shield.CubeGrid;
            }
            else if (grid == MyGrid) mainSub = true;

            var integrityAdjustment = 0f;

            var blockList = new List<IMySlimBlock>();
            grid.GetBlocks(blockList);

            for (int i = 0; i < blockList.Count; i++)
            {
                integrityAdjustment += blockList[i].MaxIntegrity;
            }

            if (!mainSub)
            {
                if (!remove) DsState.State.GridIntegrity += integrityAdjustment;
                else DsState.State.GridIntegrity -= integrityAdjustment;
            }

            return integrityAdjustment;
        }
        #endregion
    }
}

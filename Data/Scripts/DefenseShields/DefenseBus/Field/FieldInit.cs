using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        internal void CheckBlocksAndNewShape(bool refreshBlocks)
        {
            Bus.BlockChanged = true;
            Bus.FunctionalChanged = true;
            ResetShape(false);
            ResetShape(false, true);
            if (refreshBlocks) Bus.SomeBlockChanged(false);
            UpdateRender = true;
        }

        internal void EmitterEventDetected()
        {
            var b = Bus;
            var a = b.ActiveController;
            var e = b.ActiveEmitter;

            EmitterEvent = false;
            a.State.Value.ActiveEmitterId = Bus.ActiveEmitterId;
            a.State.Value.EmitterLos = EmitterLos;
            if (!ShieldIsMobile)
            {
                RefreshDimensions();
            }

            if (!EmitterLos)
            {
                if (!a.WarmedUp)
                {
                    b.Spine.Physics.ForceActivate();
                    if (Session.Enforced.Debug >= 3) Log.Line($"EmitterStartupFailure: - MaxPower:{FieldMaxPower} - {ShieldSphere.Radius} - ControllerId [{a.Controller.EntityId}]");
                    //LosCheckTick = Session.Instance.Tick + 1800;
                    Bus.DelayEvents(Bus.Events.LosCheckTick);
                    a.ProtChangedState();
                    return;
                }
                if (ShieldIsMobile && e != null && !e.EmiState.State.Los) a.State.Value.Message = true;
                else if (!ShieldIsMobile && e != null && !e.EmiState.State.Los) a.State.Value.Message = true;
                if (Session.Enforced.Debug >= 3) Log.Line($"EmitterEvent: no emitter is working, emitter mode: {b.EmitterMode} - WarmedUp:{a.WarmedUp} - MaxPower:{FieldMaxPower} - Radius:{ShieldSphere.Radius} - Broadcast:{a.State.Value.Message} - ControllerId [{a.Controller.EntityId}]");
            }
        }

        internal void SelectPassiveShell()
        {
            try
            {
                if (Bus.ActiveController == null)
                {
                    _modelPassive = ModelMediumReflective;
                    _hideColor = true;
                    _supressedColor = false;
                    return;
                }

                switch (Bus.ActiveController.Set.Value.ShieldShell)
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
                var a = Bus.ActiveController;
                var set = a.Set;
                if (ShellPassive == null) return;
                ShellPassive.Render.Visible = true;
                ShellPassive.RefreshModels($"{Session.Instance.ModPath()}{_modelPassive}", null);
                ShellPassive.Render.RemoveRenderObjects();
                ShellPassive.Render.UpdateRenderObject(true);
                _hideShield = false;
                if (Session.Enforced.Debug == 3) Log.Line($"UpdatePassiveModel: modelString:{_modelPassive} - ShellNumber:{set.Value.ShieldShell} - ControllerId [{a.Controller.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in UpdatePassiveModel: {ex}"); }
        }

        internal void SetEmitterMode()
        {
            switch (Bus.EmitterMode)
            {
                case Bus.EmitterModes.Station:
                    if (Session.Enforced.StationRatio > 0) _shieldRatio = Session.Enforced.StationRatio;
                    break;
                case Bus.EmitterModes.LargeShip:
                    if (Session.Enforced.LargeShipRatio > 0) _shieldRatio = Session.Enforced.LargeShipRatio;
                    break;
                case Bus.EmitterModes.SmallShip:
                    if (Session.Enforced.SmallShipRatio > 0) _shieldRatio = Session.Enforced.SmallShipRatio;
                    break;
            }

            switch (Bus.EmitterMode)
            {
                case Bus.EmitterModes.Station:
                    _shapeChanged = false;
                    //UpdateDimensions = true;
                    Bus.DelayEvents(Bus.Events.UpdateDimensions);
                    break;
                case Bus.EmitterModes.LargeShip:
                    UpdateMobileShape = true;
                    break;
                case Bus.EmitterModes.SmallShip:
                    _modelActive = "\\Models\\Cubes\\ShieldActiveBase_LOD4.mwm";
                    UpdateMobileShape = true;
                    break;
            }
            ShieldIsMobile = Bus.EmitterMode != Bus.EmitterModes.Station;
            InitEntities(true);
        }

        private void InitEntities(bool fullInit)
        {
            ShieldEnt?.Close();
            ShellActive?.Close();
            ShellPassive?.Close();

            if (!fullInit)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"InitEntities: mode: {Bus.EmitterMode}, remove complete ");
                return;
            }

            SelectPassiveShell();
            var parent = (MyEntity)Bus.Spine;
            if (!_isDedicated)
            {
                ShellPassive = Spawn.EmptyEntity("dShellPassive", $"{Session.Instance.ModPath()}{_modelPassive}", parent, true);
                ShellPassive.Render.CastShadows = false;
                ShellPassive.IsPreview = true;
                ShellPassive.Render.Visible = true;
                ShellPassive.Render.RemoveRenderObjects();
                ShellPassive.Render.UpdateRenderObject(true);
                ShellPassive.Render.UpdateRenderObject(false);
                ShellPassive.Save = false;
                ShellPassive.SyncFlag = false;

                ShellActive = Spawn.EmptyEntity("dShellActive", $"{Session.Instance.ModPath()}{_modelActive}", parent, true);
                ShellActive.Render.CastShadows = false;
                ShellActive.IsPreview = true;
                ShellActive.Render.Visible = true;
                ShellActive.Render.RemoveRenderObjects();
                ShellActive.Render.UpdateRenderObject(true);
                ShellActive.Render.UpdateRenderObject(false);
                ShellActive.Save = false;
                ShellActive.SyncFlag = false;
                ShellActive.SetEmissiveParts("ShieldEmissiveAlpha", Color.Transparent, 0f);
            }

            ShieldEnt = Spawn.EmptyEntity("dShield", null, parent);
            ShieldEnt.Render.CastShadows = false;
            ShieldEnt.Render.RemoveRenderObjects();
            ShieldEnt.Render.UpdateRenderObject(true);
            ShieldEnt.Render.Visible = false;
            ShieldEnt.Save = false;
            ShieldEntRendId = ShieldEnt.Render.GetRenderObjectID();
            UpdateRender = true;

            if (Icosphere == null) Icosphere = new Icosphere.Instance(Session.Instance.Icosphere);
            if (Session.Enforced.Debug == 3) Log.Line($"InitEntities: mode: {Bus.EmitterMode}, spawn complete");
        }

        internal bool WarmUpSequence()
        {
            var a = Bus.ActiveController;
            /*
            if (_isServer && (Bus.EmitterMode < 0 || Bus.EmitterMode == 0 && Bus.ActiveEmitter == null || Bus.EmitterMode != 0 && Bus.ActiveEmitter == null || !IsFunctional))
            {
                return;
            }
            */

            MyEntity emitterEnt = null;
            if (!_isServer && (Session.Enforced.Version <= 0 || a.State.Value.ActiveEmitterId != 0 && !MyEntities.TryGetEntityById(a.State.Value.ActiveEmitterId, out emitterEnt) || !(emitterEnt is IMyUpgradeModule)))
                return false;

            CheckBlocksAndNewShape(false);

            _oldGridHalfExtents = a.State.Value.GridHalfExtents;
            _oldEllipsoidAdjust = a.State.Value.EllipsoidAdjust;
            return true;
        }

        internal void LosCheck()
        {
            LosCheckTick = uint.MaxValue;
            CheckEmitters = true;
            //FitChanged = true;
            Bus.DelayEvents(Bus.Events.FitChanged);
            //AdjustShape = true;
            Bus.DelayEvents(Bus.Events.AdjustShape);
        }

    }
}

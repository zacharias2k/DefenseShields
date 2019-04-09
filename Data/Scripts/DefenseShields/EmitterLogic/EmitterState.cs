using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    public partial class Emitters
    {
        #region Block Status
        private bool ControllerLink()
        {
            if (!_isServer)
            {
                var link = ClientEmitterReady();
                if (!link && !_blockReset) BlockReset(true);

                return link;
            }

            if (!_firstSync && _readyToSync) SaveAndSendAll();

            var linkWas = EmiState.State.Link;
            var losWas = EmiState.State.Los;
            var idWas = EmiState.State.ActiveEmitterId;
            if (!EmitterReady())
            {
                EmiState.State.Link = false;

                if (linkWas || losWas != EmiState.State.Los || idWas != EmiState.State.ActiveEmitterId)
                {
                    if (!_isDedicated && !_blockReset) BlockReset(true);
                    NeedUpdate();
                }
                return false;
            }

            EmiState.State.Link = true;

            if (!linkWas || losWas != EmiState.State.Los || idWas != EmiState.State.ActiveEmitterId) NeedUpdate();

            return true;
        }

        private bool EmitterReady()
        {
            if (Suspend() || !BlockWorking())
                return false;

            return true;
        }

        private bool ClientEmitterReady()
        {
            if (Bus?.ActiveController == null) return false;

            if (!_compact)
            {
                if (IsFunctional) Entity.TryGetSubpart("Rotor", out SubpartRotor);
                if (SubpartRotor == null) return false;
            }

            if (!EmiState.State.Los) LosLogic();

            if (EmiState.State.Los && !_wasLosState)
            {
                _wasLosState = EmiState.State.Los;
                _updateLosState = false;
                LosScaledCloud.Clear();
            }
            return EmiState.State.Link;
        }

        private bool Suspend()
        {
            //Log.Line($"{Bus.ActiveEmitter == null} - {Bus.ActiveEmitter == this} - {Bus.SubGrids.Contains(Bus.ActiveEmitter.MyCube.CubeGrid)} - {Bus.SubGrids.Contains(MyCube.CubeGrid)} - {EmiState.State.Suspend} - {EmiState.State.Backup} - {Bus.EmitterMode} - {(int)EmitterMode}");
            if (Bus.ActiveEmitter != this)
            {
                if (!EmiState.State.Suspend)
                {
                    EmiState.State.ActiveEmitterId = 0;
                    EmiState.State.Backup = true;
                    EmiState.State.Suspend = true;
                    Session.Instance.BlockTagBackup(Emitter);
                    if (Bus.ActiveEmitter == this)
                    {
                        Bus.EmitterMode = (int)EmitterMode;
                        Bus.EmitterEvent = true;
                    }
                }
                if (!_isDedicated && !_blockReset) BlockReset(true);
                return true;
            }

            var currentMode = (int)EmitterMode;
            var busMode = Bus.EmitterMode;
            if (currentMode != busMode || EmiState.State.Backup || EmiState.State.Suspend)
            {
                EmiState.State.Suspend = false;
                EmiState.State.Backup = false;
                Bus.EmitterMode = (int)EmitterMode;
                Bus.EmitterEvent = true;
                Session.Instance.BlockTagActive(Emitter);
            }
            return false;
        }

        private bool BlockWorking()
        {
            EmiState.State.ActiveEmitterId = MyCube.EntityId;

            if (Bus.EmitterMode != (int)EmitterMode) Bus.EmitterMode = (int)EmitterMode;

            LosLogic();

            Bus.EmitterLos = EmiState.State.Los;

            var bus = Bus;
            var controller = bus.ActiveController;
            var nullController = controller == null;
            var shieldWaiting = !nullController && controller.DsState.State.EmitterLos != EmiState.State.Los;
            if (shieldWaiting) bus.EmitterEvent = true;

            if (!EmiState.State.Los || nullController || shieldWaiting || !controller.DsState.State.Online || !(_tick >= controller.ResetEntityTick))
            {
                if (!_isDedicated && !_blockReset) BlockReset(true);
                return false;
            }
            return true;
        }
        #endregion

        #region Block States
        internal void UpdateState(EmitterStateValues newState)
        {
            if (newState.MId > EmiState.State.MId)
            {
                if (Session.Enforced.Debug >= 3) Log.Line($"UpdateState - NewLink:{newState.Link} - OldLink:{EmiState.State.Link} - EmitterId [{Emitter.EntityId}]:\n{EmiState.State}");
                EmiState.State = newState;
            }
        }

        private void NeedUpdate()
        {
            EmiState.State.Mode = (int)EmitterMode;
            EmiState.State.BoundingRange = Bus.ActiveController?.BoundingRange ?? 0f;
            EmiState.State.Compatible = (Bus.IsStatic && EmitterMode == EmitterType.Station) || (!Bus.IsStatic && EmitterMode != EmitterType.Station);
            EmiState.SaveState();
            if (Session.Instance.MpActive) EmiState.NetworkUpdate();
        }

        private void CheckEmitter(IMyTerminalBlock myTerminalBlock)
        {
            try
            {
                if (myTerminalBlock.IsWorking && Bus != null) Bus.CheckEmitters = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CheckEmitter: {ex}"); }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsFunctional = myCubeBlock.IsWorking;
            IsWorking = myCubeBlock.IsWorking;
        }

        private void SetEmitterType()
        {
            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            switch (Definition.Name)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
                case "EmitterL":
                case "EmitterLA":
                    EmitterMode = EmitterType.Large;
                    if (Definition.Name == "EmitterLA") _compact = true;
                    else Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") _compact = true;
                    else Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
            }
            Emitter.AppendingCustomInfo += AppendingCustomInfo;
        }
        #endregion

    }
}

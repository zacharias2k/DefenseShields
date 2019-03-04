using System;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public partial class Emitters
    {
        #region Block Status
        private bool ControllerLink()
        {
            if (!EmitterReady())
            {
                if (_isServer) EmiState.State.Link = false;

                if (StateChange())
                {
                    if (_isServer)
                    {
                        BlockReset(true);
                        NeedUpdate();
                        StateChange(true);
                    }
                    else
                    {
                        BlockReset(true);
                        StateChange(true);
                    }
                }
                return false;
            }
            if (_isServer)
            {
                EmiState.State.Link = true;
                if (StateChange())
                {
                    NeedUpdate();
                    StateChange(true);
                }
            }
            else if (!EmiState.State.Link)
            {
                if (StateChange())
                {
                    BlockReset(true);
                    StateChange(true);
                }
                return false;
            }
            return true;
        }

        private bool EmitterReady()
        {
            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);
            if (_isServer)
            {
                if (Suspend() || !BlockWorking()) return false;
            }
            else
            {
                if (ShieldComp == null) return false;

                //if (EmiState.State.Mode == 0 && EmiState.State.Link && ShieldComp.StationEmitter == null) ShieldComp.StationEmitter = this;
                //else if (EmiState.State.Mode != 0 && EmiState.State.Link && ShieldComp.ShipEmitter == null) ShieldComp.ShipEmitter = this;

                if (ShieldComp.DefenseShields == null || ShieldComp.DefenseShields.DsState.State.ActiveEmitterId != MyCube.EntityId || !IsFunctional)
                    return false;

                if (!_compact && SubpartRotor == null)
                {
                    Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    if (SubpartRotor == null) return false;
                }

                //if (EmiState.State.Online && !EmiState.State.Los) LosLogic();
                if (!EmiState.State.Los) LosLogic();


                if (EmiState.State.Los && !_wasLosState)
                {
                    _wasLosState = EmiState.State.Los;
                    _updateLosState = false;
                    LosScaledCloud.Clear();
                }
                //if (!EmiState.State.Link || !EmiState.State.Online) return false;
                if (!EmiState.State.Link) return false;
            }
            return true;
        }

        private bool Suspend()
        {
            //EmiState.State.Online = false;
            EmiState.State.ActiveEmitterId = 0;
            var functional = IsFunctional;
            if (!functional)
            {
                EmiState.State.Suspend = true;
                if (ShieldComp?.StationEmitter == this) ShieldComp.StationEmitter = null;
                else if (ShieldComp?.ShipEmitter == this) ShieldComp.ShipEmitter = null;
                return true;
            }
            if (!_compact && SubpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out SubpartRotor);
                if (SubpartRotor == null)
                {
                    EmiState.State.Suspend = true;
                    return true;
                }
            }

            if (ShieldComp == null)
            {
                EmiState.State.Suspend = true;
                return true;
            }

            var working = IsWorking;
            var stationMode = EmitterMode == EmitterType.Station;
            var shipMode = EmitterMode != EmitterType.Station;
            var modes = (IsStatic && stationMode) || (!IsStatic && shipMode);
            var mySlotNull = (stationMode && ShieldComp.StationEmitter == null) || (shipMode && ShieldComp.ShipEmitter == null);
            var myComp = (stationMode && ShieldComp.StationEmitter == this) || (shipMode && ShieldComp.ShipEmitter == this);

            var myMode = working && modes;
            var mySlotOpen = working && mySlotNull;
            var myShield = myMode && myComp;
            var iStopped = !working && myComp && modes;
            if (mySlotOpen)
            {
                if (stationMode)
                {
                    EmiState.State.Backup = false;
                    ShieldComp.StationEmitter = this;
                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                else
                {
                    EmiState.State.Backup = false;
                    ShieldComp.ShipEmitter = this;

                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"mySlotOpen: {Definition.Name} - myMode:{myMode} - MyShield:{myShield} - Mode:{EmitterMode} - Static:{IsStatic} - ELos:{ShieldComp.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - ModeM:{(int)EmitterMode == ShieldComp.EmitterMode} - S:{EmiState.State.Suspend} - EmitterId [{Emitter.EntityId}]");
            }
            else if (!myMode)
            {
                var compMode = ShieldComp.EmitterMode;
                if ((!EmiState.State.Suspend && ((compMode == 0 && !IsStatic) || (compMode != 0 && IsStatic))) || (!EmiState.State.Suspend && iStopped))
                {
                    ShieldComp.EmittersSuspended = true;
                    ShieldComp.EmitterLos = false;
                    ShieldComp.EmitterEvent = true;
                    if (Session.Enforced.Debug == 2) Log.Line($"!myMode: {Definition.Name} suspending - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - ELos:{ShieldComp.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                else if (!EmiState.State.Suspend)
                {
                    if (Session.Enforced.Debug == 2) Log.Line($"!myMode: {Definition.Name} suspending - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - ELos:{ShieldComp.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                EmiState.State.Suspend = true;
            }
            if (iStopped)
            {
                return EmiState.State.Suspend;
            }

            if (!myShield)
            {
                if (!EmiState.State.Backup)
                {
                    EmiState.State.Backup = true;
                    if (Session.Enforced.Debug == 2) Log.Line($"!myShield - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - CompMode: {ShieldComp.EmitterMode} - ELos:{ShieldComp.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
                }
                EmiState.State.Suspend = true;
            }

            if (myShield && EmiState.State.Suspend)
            {
                ShieldComp.EmittersSuspended = false;
                ShieldComp.EmitterEvent = true;
                EmiState.State.Backup = false;
                EmiState.State.Suspend = false;
                if (Session.Enforced.Debug == 2) Log.Line($"Unsuspend - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - CompMode: {ShieldComp.EmitterMode} - ELos:{ShieldComp.EmitterLos} - ES:{ShieldComp.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
            }
            else if (EmiState.State.Suspend) return true;

            EmiState.State.Suspend = false;
            return false;
        }

        private bool BlockWorking()
        {
            //EmiState.State.Online = true;
            EmiState.State.ActiveEmitterId = MyCube.EntityId;
            ShieldComp.ActiveEmitterId = EmiState.State.ActiveEmitterId;

            if (ShieldComp.EmitterMode != (int)EmitterMode) ShieldComp.EmitterMode = (int)EmitterMode;
            if (ShieldComp.EmittersSuspended) SuspendCollisionDetected();

            LosLogic();

            //ShieldComp.EmittersWorking = EmiState.State.Los && EmiState.State.Online;
            ShieldComp.EmitterLos = EmiState.State.Los;

            if (!EmiState.State.Los || ShieldComp.DefenseShields == null || !ShieldComp.DefenseShields.DsState.State.Online || !(_tick >= ShieldComp.DefenseShields.UnsuspendTick))
            {
                BlockReset();
                return false;
            }
            return true;
        }

        private void SuspendCollisionDetected()
        {
            ShieldComp.EmitterMode = (int)EmitterMode;
            ShieldComp.EmittersSuspended = false;
            ShieldComp.EmitterEvent = true;
            TookControl = true;
        }
        #endregion

        #region Block States
        internal void UpdateState(EmitterStateValues newState)
        {
            EmiState.State = newState;
            if (Session.Enforced.Debug <= 3) Log.Line($"UpdateState - EmitterId [{Emitter.EntityId}]:\n{EmiState.State}");
        }

        private bool StateChange(bool update = false)
        {
            if (update)
            {
                //_emitterFailed = EmiState.State.Online;
                _wasActiveEmitterId = EmiState.State.ActiveEmitterId;
                _wasLink = EmiState.State.Link;
                _wasBackup = EmiState.State.Backup;
                _wasSuspend = EmiState.State.Suspend;
                _wasLos = EmiState.State.Los;
                //_wasCompact = EmiState.State.Compact;
                _wasCompatible = EmiState.State.Compatible;
                _wasMode = EmiState.State.Mode;
                _wasBoundingRange = EmiState.State.BoundingRange;
                Emitter.RefreshCustomInfo(); // temp
                return true;
            }

            //return _emitterFailed != EmiState.State.Online || _wasLink != EmiState.State.Link ||
            return _wasActiveEmitterId != EmiState.State.ActiveEmitterId || _wasLink != EmiState.State.Link ||

                   _wasBackup != EmiState.State.Backup || _wasSuspend != EmiState.State.Suspend ||
                   //_wasLos != EmiState.State.Los || _wasCompact != EmiState.State.Compact ||
                   _wasCompatible != EmiState.State.Compatible || _wasMode != EmiState.State.Mode ||
                   _wasLos != EmiState.State.Los || !_wasBoundingRange.Equals(EmiState.State.BoundingRange);
        }

        private void NeedUpdate()
        {
            EmiState.State.Mode = (int)EmitterMode;
            EmiState.State.BoundingRange = ShieldComp?.DefenseShields?.BoundingRange ?? 0f;
            EmiState.State.Compatible = (IsStatic && EmitterMode == EmitterType.Station) || (!IsStatic && EmitterMode != EmitterType.Station);
            EmiState.SaveState();
            if (Session.Instance.MpActive) EmiState.NetworkUpdate();
        }

        private void CheckEmitter(IMyTerminalBlock myTerminalBlock)
        {
            try
            {
                if (myTerminalBlock.IsWorking && ShieldComp != null) ShieldComp.CheckEmitters = true;
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

using VRage.Game;

namespace DefenseSystems
{
    using Support;
    using VRageMath;

    public partial class Controllers
    {
        private bool EntityAlive()
        {
            //if (Controller.CubeGrid.GridSizeEnum == MyCubeSize.Small &&  _shellPassive == null) Log.Line($"_shellPassiveNull");
            //else if (ShieldMode == ShieldType.SmallGrid) Log.Line($"{State.Value.Online} - {_shellPassive.Render.IsVisible()} - {_shellPassive.Render.Visible}");
            //else if (Controller.CubeGrid.GridSizeEnum == MyCubeSize.Small) Log.Line("other");
            Bus.Tick = Session.Instance.Tick;
            Bus.Tick20 = Session.Instance.Tick20;
            Bus.Tick60 = Session.Instance.Tick60;
            Bus.Tick180 = Session.Instance.Tick180;
            Bus.Tick300 = Session.Instance.Tick300;
            Bus.Tick600 = Session.Instance.Tick600;
            Bus.Tick1800 = Session.Instance.Tick1800;
            if (Bus.Count++ == 59) Bus.Count = 0;

            var fieldMode = State.Value.ProtectMode != 2;
            if (WasPaused && (PlayerByShield || MoverByShield || NewEntByShield || LostPings > 59)) UnPauseLogic();
            LostPings = 0;

            var wait = _isServer && !Bus.Tick60 && State.Value.Suspended;

            LocalGrid = MyCube.CubeGrid;
            if (LocalGrid?.Physics == null) return false;
            if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && Bus.Count == 29) TerminalRefresh();
            if (Bus.Tick1800 && Session.Enforced.Debug > 0) Debug();

            if (wait || (!_allInited && !PostInit())) return false;

            //if (!Bus.Warming) return false;

            if (Bus.SubUpdate && Bus.Tick >= Bus.SubTick) Bus.SubGridDetect(LocalGrid);
            if (Bus.BlockEvent && Bus.Tick >= Bus.FuncTick) Bus.SomeBlockChanged(true);
            if (Bus.BlockChanged) Bus.BlockMonitor();
            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();

            if (_mpActive && fieldMode) Bus.Field.NetHits();
            return true;
        }

        private Status ProtectionOn(bool fieldMode)
        {
            if (_isServer)
            {
                if (Suspended()) return Status.Init;
                if (Sleeping()) return Status.Sleep;
                if (Waking()) return Status.Wake;

                if (Bus.Tick60)
                {
                    GetModulationInfo();
                    GetEnhancernInfo();
                }

                if (fieldMode)
                {
                    var status = Bus.Field.Status();
                    if (status != Status.Active) return status;
                }
                else
                {
                    var status = Bus.Armor.Status();
                    if (status != Status.Active) return status;
                }
            }
            else
            {
                if (ClientOfflineStates()) return Status.Failure;

                if (fieldMode)
                {
                    var status = Bus.Field.ClientStatus();
                    if (status != Status.Active) return status;
                }
                else
                {
                    var status = Bus.Armor.ClientStatus();
                    if (status != Status.Active) return status;
                }
            }

            return Status.Active;
        }

        private bool Sleeping()
        {
            if (Bus.SlaveControllerLink(_firstLoop))
            {
                if (!State.Value.Sleeping)
                {
                    var fieldMode = State.Value.ProtectMode != 2;
                    if (fieldMode) Bus.Field.Sleeping();
                    State.Value.Sleeping = true;
                    TerminalRefresh(false);
                    if (Session.Enforced.Debug >= 3) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {Bus.EmitterMode} - ControllerId [{Controller.EntityId}]");
                }
                State.Value.Sleeping = true;
                return true;
            }
            if (State.Value.Sleeping)
            {
                State.Value.Sleeping = false;
                if (!_isDedicated && Bus.Tick60) TerminalRefresh();
                if (Session.Enforced.Debug >= 3) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {Bus.EmitterMode} - ControllerId [{Controller.EntityId}]");
            }

            State.Value.Sleeping = false;
            return false;
        }

        private bool Suspended()
        {
            var notStation = Bus.EmitterMode != Bus.EmitterModes.Station && Bus.IsStatic;
            var notShip = Bus.EmitterMode == Bus.EmitterModes.Station && !Bus.IsStatic;
            var unKnown = Bus.EmitterMode == Bus.EmitterModes.Unknown;
            var wrongOwner = !State.Value.ControllerGridAccess;
            var myShield = Bus.ActiveController == this;
            var wrongRole = notStation || notShip || unKnown;
            if (Bus.Tick180 && Bus == null) Log.Line("no active controller");
            if (Bus.Tick180 && Bus.ActiveController == null) Log.Line("no active controller");
            if (Bus.Tick180 && Bus.ActiveEmitter == null) Log.Line("no active emitter");
            if (Bus.Tick180 && Bus.Spine == null) Log.Line("no master grid");
            if (Bus.Tick180 && !Bus.SubGrids.Contains(MyCube.CubeGrid)) Log.Line("I am on wrong bus");

            if (!myShield || !IsFunctional || Bus.ActiveEmitter == null || wrongOwner || wrongRole)
            {
                if (!State.Value.Suspended) Suspend();
                return true;
            }

            State.Value.Mode = (int) Bus.EmitterMode;

            if (State.Value.Suspended)
            {
                UnSuspend();
                return true;
            }

            return !IsWorking;
        }

        private void Suspend()
        {
            State.Value.Suspended = true;
            if (Bus.ActiveEmitter != null) Bus.Field.Suspend(this);

            bool value;
            Session.Instance.BlockTagBackup(Controller);
            Session.Instance.FunctionalShields.TryRemove(this, out value);
        }

        private void UnSuspend()
        {
            State.Value.Suspended = false;
            Session.Instance.BlockTagActive(Controller);
            Session.Instance.FunctionalShields[this] = false;
            //UpdateEntity();
            GetEnhancernInfo();
            GetModulationInfo();
            if (Session.Enforced.Debug == 3) Log.Line($"Unsuspended: CM:{Bus.EmitterMode} - EW:{State.Value.EmitterLos} - Range:{Bus.Field.BoundingRange} - ControllerId [{Controller.EntityId}]");
        }

        private bool Waking()
        {
            if (Bus.Tick < ResetEntityTick)
            {
                if (!State.Value.Waking)
                {
                    State.Value.Waking = true;
                    State.Value.Message = true;
                    if (Session.Enforced.Debug >= 2) Log.Line($"Waking: ControllerId [{Controller.EntityId}]");
                }
                return true;
            }
            if (ResetEntityTick != uint.MinValue && Bus.Tick >= ResetEntityTick)
            {
                Bus.Field.ResetShape(false);
                Bus.Field.UpdateRender = true;
                ResetEntityTick = uint.MinValue;

                if (Session.Enforced.Debug >= 2) Log.Line($"Woke: ControllerId [{Controller.EntityId}]");
            }
            else if (Bus.Field.ShapeTick != uint.MinValue && Bus.Tick >= Bus.Field.ShapeTick)
            {
                Bus.Field.ShapeEvent = true;
                Bus.Field.ShapeTick = uint.MinValue;
            }
            State.Value.Waking = false;
            return false;
        }

        private void ComingOnline()
        {
            _firstLoop = false;
            var fieldMode = State.Value.ProtectMode != 2;
            if (fieldMode) Bus.Field.Up();

            Bus.Starting = false;
            LastWokenTick = Bus.Tick;
            NotFailed = true;
            WarmedUp = true;

            if (_isServer)
            {
                ProtChangedState();
                if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: ComingOnlineSetup - ControllerId [{Controller.EntityId}]");
            }
            else
            {
                TerminalRefresh();
                if (Session.Enforced.Debug == 3) Log.Line($"StateUpdate: ComingOnlineSetup - ControllerId [{Controller.EntityId}]");
            }
            lock (Session.Instance.ActiveProtection) Session.Instance.ActiveProtection.Add(this);
            //Bus.SubGridDetect(LocalGrid, true);
        }

        private bool ClientOfflineStates()
        {
            var shieldUp = State.Value.Online && !State.Value.Suspended && !State.Value.Sleeping;

            if (!shieldUp)
            {
                if (_clientOn)
                {
                    var fieldMode = State.Value.ProtectMode != 2;
                    if (fieldMode) Bus.Field.ClientDown();
                    _clientOn = false;
                    TerminalRefresh();
                }
                return true;
            }

            if (!_clientOn)
            {
                ComingOnline();
                _clientOn = true;
            }
            return false;
        }

        internal void UpdateSettings(ControllerSettingsValues newSettings)
        {
            if (newSettings.MId > Set.Value.MId)
            {
                var newShape = newSettings.ExtendFit != Set.Value.ExtendFit || newSettings.FortifyShield != Set.Value.FortifyShield || newSettings.SphereFit != Set.Value.SphereFit;
                Set.Value = newSettings;
                SettingsUpdated = true;
                if (newShape) Bus.Field.FitChanged = true;
            }
        }

        internal void UpdateState(ControllerStateValues newState)
        {
            if (newState.MId > State.Value.MId)
            {
                if (!_isServer)
                {
                    if (!newState.EllipsoidAdjust.Equals(State.Value.EllipsoidAdjust) || !newState.ShieldFudge.Equals(State.Value.ShieldFudge) ||
                        !newState.GridHalfExtents.Equals(State.Value.GridHalfExtents))
                    {
                        Bus.Field.UpdateMobileShape = true;
                    }
                    if (State.Value.Message) Bus.BroadcastMessage();
                }
                State.Value = newState;
                _clientNotReady = false;
            }
        }

        private void UpdateSettings()
        {
            if (Bus.Tick % 33 == 0)
            {
                if (SettingsUpdated)
                {

                    SettingsUpdated = false;
                    Set.SaveSettings();
                    Bus.Field.ResetShape(false);
                    if (_isServer)
                    {
                        if (Set.Value.ProtectMode != State.Value.ProtectMode)
                        {
                            State.Value.ProtectMode = Set.Value.ProtectMode;
                            ProtChangedState();
                        }
                    }
                }
            }
            else if (Bus.Tick % 34 == 0)
            {
                if (ClientUiUpdate)
                {
                    ClientUiUpdate = false;
                    if (!_isServer) Set.NetworkUpdate();
                }
            }
        }

        internal void ProtChangedState()
        {
            if (Session.Instance.MpActive)
            {
                State.NetworkUpdate();
                if (_isServer) TerminalRefresh(false);
            }

            if (!_isDedicated && State.Value.Message) Bus.BroadcastMessage();

            State.Value.Message = false;
            State.SaveState();
        }
    }
}

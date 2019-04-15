using Sandbox.Game.Entities;
using VRage.ModAPI;
using System;
using DefenseSystems.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace DefenseSystems
{
    public partial class Controllers
    {
        #region Startup Logic
        internal void AssignSlots()
        {
            LogicSlot = Session.GetSlot();
            MonitorSlot = LogicSlot - 1 < 0 ? Session.Instance.EntSlotScaler - 1 : LogicSlot - 1;
        }

        private void UnPauseLogic()
        {
            if (Session.Enforced.Debug >= 2) Log.Line($"[Logic Resumed] Player:{PlayerByShield} - Mover:{MoverByShield} - NewEnt:{NewEntByShield} - Lost:{LostPings > 59} - LastWoken:{LastWokenTick} - ASleep:{Asleep} - TicksNoActivity:{TicksWithNoActivity}");
            TicksWithNoActivity = 0;
            LastWokenTick = Bus.Tick;
            Asleep = false;
            PlayerByShield = true;
            lock (Session.Instance.ActiveProtection) Session.Instance.ActiveProtection.Add(this);
            WasPaused = false;
        }

        private bool ResetEntity()
        {
            LocalGrid = (MyCubeGrid)Controller.CubeGrid;
            MyCube = Controller as MyCubeBlock;
            if (LocalGrid.Physics == null) return false;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            if (_bInit) ResetEntityTick = Session.Instance.Tick + 1800;
            AssignSlots();

            _bCount = 0;
            _bInit = false;
            _aInit = false;
            _allInited = false;
            WarmedUp = false;
            /*
            if (_isServer)
            {
                GridIntegrity();
                ProtChangedState();
            }
            */
            return true;
        }

        private void BeforeInit()
        {
            if (Controller.CubeGrid.Physics == null) return;
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;

            PowerInit();

            if (_isServer) Enforcements.SaveEnforcement(Controller, Session.Enforced, true);

            Session.Instance.FunctionalShields[this] = false;
            Session.Instance.AllControllers.Add(this);
            if (MyAPIGateway.Session.CreativeMode) CreativeModeWarning();
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Registry.RegisterWithBus(this, LocalGrid, true, Bus, out Bus);
            _bTime = 1;
            _bInit = true;
            if (Session.Enforced.Debug == 3) Log.Line($"UpdateOnceBeforeFrame: ControllerId [{Controller.EntityId}]");

        }

        private void AfterInit()
        {
            Bus.Init();
            Bus.GetSpineIntegrity();
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _aInit = true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            Session.Instance.CreateControllerElements(Controller);
            if (!Session.Instance.DsAction)
            {
                Session.AppendConditionToAction<IMyUpgradeModule>((a) => Session.Instance.DsActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<Controllers>() != null && Session.Instance.DsActions.Contains(a.Id));
                Session.Instance.DsAction = true;
            }
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        /*
        private void UpdateEntity()
        {
            Bus.LinkedGrids.Clear();
            Bus.SubGrids.Clear();
            Bus.BlockChanged = true;
            Bus.FunctionalChanged = true;
            ResetShape(false, true);
            ResetShape(false);
            SetEmitterMode(false);
            if (!_isDedicated) ShellVisibility(true);
            if (Session.Enforced.Debug == 2) Log.Line($"UpdateEntity: sEnt:{ShieldEnt == null} - sPassive:{_shellPassive == null} - controller mode is: {EmitterMode} - EW:{State.Value.EmitterLos} - ControllerId [{Controller.EntityId}]");
            Icosphere.ShellActive = null;
            State.Value.Heat = 0;

            _updateRender = true;
            _currentHeatStep = 0;
            _accumulatedHeat = 0;
            _heatCycle = -1;
        }
        */

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
            if (Session.Enforced.Debug >= 3) Log.Line($"SaveAndSendAll: ControllerId [{Controller.EntityId}]");
        }

        private void StorageSetup()
        {
            try
            {
                var isServer = MyAPIGateway.Multiplayer.IsServer;

                if (Set == null) Set = new ControllerSettings(Controller);
                if (State == null) State = new ControllerState(Controller);
                if (Controller.Storage == null) State.StorageInit();
                if (!isServer)
                {
                    var enforcement = Enforcements.LoadEnforcement(Controller);
                    if (enforcement != null) Session.Enforced = enforcement;
                }
                Set.LoadSettings();
                if (!State.LoadState() && !isServer) _clientNotReady = true;
                UpdateSettings(Set.Value);
                if (isServer)
                {
                    State.Value.Overload = false;
                    State.Value.NoPower = false;
                    State.Value.Remodulate = false;
                    if (State.Value.Suspended)
                    {
                        State.Value.Suspended = false;
                        State.Value.Online = false;
                    }
                    State.Value.Sleeping = false;
                    State.Value.Waking = false;
                    State.Value.FieldBlocked = false;
                    State.Value.Heat = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in StorageSetup: {ex}"); }
        }

        private void PowerPreInit()
        {
            try
            {
                if (Sink == null) Sink = new MyResourceSinkComponent();
                _resourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => SinkPower
                };
                Sink.Init(MyStringHash.GetOrCompute("Defense"), _resourceInfo);
                Sink.AddType(ref _resourceInfo);
                Entity.Components.Add(Sink);
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void CurrentInputChanged(MyDefinitionId resourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            if (Bus.ActiveController == this) SinkCurrentPower = sink.CurrentInputByType(GId);
        }

        private void PowerInit()
        {
            try
            {
                Sink.Update();
                Controller.RefreshCustomInfo();

                var enableState = Controller.Enabled;
                if (enableState)
                {
                    Controller.Enabled = false;
                    Controller.Enabled = true;
                }
                IsWorking = MyCube.IsWorking;
                if (Session.Enforced.Debug == 3) Log.Line($"PowerInit: ControllerId [{Controller.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }
        #endregion
    }
}

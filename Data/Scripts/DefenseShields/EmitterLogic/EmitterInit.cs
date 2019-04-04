using System;
using System.Text;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;

namespace DefenseSystems
{
    public partial class Emitters
    {
        #region Init/Misc
        private bool ResetEntity()
        {
            LocalGrid = (MyCubeGrid)Emitter.CubeGrid;
            MyCube = Emitter as MyCubeBlock;
            if (LocalGrid.Physics == null) return false;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            SetEmitterType();
            _aInit = false;
            _bInit = false;
            return true;
        }

        private void BeforeInit()
        {
            if (Emitter.CubeGrid.Physics == null) return;
            Session.Instance.Emitters.Add(this);
            PowerInit();
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            IsStatic = Emitter.CubeGrid.IsStatic;
            _disableLos = Session.Enforced.DisableLineOfSight == 1;
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Registry.RegisterWithBus(this, LocalGrid, true, Bus, out Bus);
            _bTime = _isDedicated ? 10 : 1;
            _bInit = true;
        }

        private void AfterInit()
        {
            Bus.Init();
            if (!MyAPIGateway.Utilities.IsDedicated) NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _aInit = true;
        }

        private void StorageSetup()
        {
            if (EmiState == null) EmiState = new EmitterState(Emitter);
            EmiState.StorageInit();
            EmiState.LoadState();

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                EmiState.State.ActiveEmitterId = 0;
                EmiState.State.Backup = false;
                EmiState.State.Los = true;
                if (EmiState.State.Suspend)
                {
                    EmiState.State.Suspend = false;
                    EmiState.State.Link = false;
                    EmiState.State.Mode = -1;
                    EmiState.State.BoundingRange = -1;
                }
            }
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
                    ResourceTypeId = _gId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
                Sink.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Emitter.Enabled;
                if (enableState)
                {
                    Emitter.Enabled = false;
                    Emitter.Enabled = true;
                }
                Sink.Update();
                if (Session.Enforced.Debug == 3) Log.Line($"PowerInit: EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var mode = Enum.GetName(typeof(EmitterType), EmiState.State.Mode);
                if (!EmiState.State.Link)
                {
                    stringBuilder.Append("[ No Valid Controller ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Controller Bus]: " + (Bus?.ActiveController != null) +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
                //else if (!EmiState.State.Online)
                else if (EmiState.State.ActiveEmitterId == 0)
                {
                    stringBuilder.Append("[ Emitter Offline ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
                else
                {
                    stringBuilder.Append("[ Emitter Online ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfo: {ex}"); }
        }

        internal void RegisterEvents(MyCubeGrid grid, Bus bus, bool register = true)
        {
            if (register)
            {
                bus.Events.OnBusSplit += OnBusSplit;
                Emitter.EnabledChanged += CheckEmitter;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
            }
            else
            {
                bus.Events.OnBusSplit -= OnBusSplit;
                Emitter.AppendingCustomInfo -= AppendingCustomInfo;
                Emitter.EnabledChanged -= CheckEmitter;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void OnBusSplit<T>(T type, Bus.LogicState state)
        {
            var grid = type as MyCubeGrid;
            if (grid == null) return;
            if (state == Bus.LogicState.Leave)
            {
                var onMyBus = Bus.SubGrids.Contains(grid);
                if (!onMyBus && Bus.ActiveEmitter == null)
                {
                    IsAfterInited = false;
                    Bus.Inited = false;
                }
                Log.Line($"[eId:{MyCube.EntityId}] [Splitter - gId:{grid.EntityId} - bCnt:{grid.BlocksCount}] - [Receiver - gId:{MyCube.CubeGrid.EntityId} - OnMyBus:{onMyBus} - iMaster:{MyCube.CubeGrid == Bus.Spine} - mSize:{Bus.Spine.BlocksCount}]");
            }
        }

        internal void TerminalRefresh(bool update = true)
        {
            Emitter.RefreshCustomInfo();
            if (update && InControlPanel && InThisTerminal)
            {
                MyCube.UpdateTerminal();
            }
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            EmiState.SaveState();
            EmiState.NetworkUpdate();
            if (Session.Enforced.Debug >= 3) Log.Line($"SaveAndSendAll: EmitterId [{Emitter.EntityId}]");

        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (_count == 29 && !_isDedicated)
            {
                TerminalRefresh(true);
            }
        }
        #endregion

    }
}

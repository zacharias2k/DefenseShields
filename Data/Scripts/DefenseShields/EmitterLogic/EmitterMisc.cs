using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace DefenseShields
{
    public partial class Emitters
    {
        #region Init/Misc
        private void StorageSetup()
        {
            if (EmiState == null) EmiState = new EmitterState(Emitter);
            EmiState.StorageInit();
            EmiState.LoadState();
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
                IsWorking = MyCube.IsWorking;
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
                                         "\n[Controller Bus]: " + (ShieldComp?.DefenseShields != null) +
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

        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Emitter.EnabledChanged += CheckEmitter;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
            }
            else
            {
                Emitter.AppendingCustomInfo -= AppendingCustomInfo;
                Emitter.EnabledChanged -= CheckEmitter;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }
            if (_count == 29 && !_isDedicated && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel && Session.Instance.LastTerminalId == Emitter.EntityId)
            {
                Emitter.RefreshCustomInfo();
            }
        }
        #endregion

    }
}

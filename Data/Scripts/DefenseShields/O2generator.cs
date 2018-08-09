using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "DSSupergen")]
    public class O2Generators : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _airIPercent = -1;
        private int _lCount;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;

        private double _oldShieldVol;
        internal float EmissiveIntensity;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool Suspended;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;

        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        internal MyResourceSourceComponent Source;
        internal O2GeneratorState O2State;

        internal DSUtils Dsutil1 = new DSUtils();

        public IMyGasGenerator O2Generator => (IMyGasGenerator)Entity;
        private IMyInventory _inventory;

        private readonly Dictionary<long, O2Generators> _o2Generator = new Dictionary<long, O2Generators>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                Session.Instance.O2Generators.Add(this);
                _o2Generator.Add(Entity.EntityId, this);
                StorageSetup();
                Source = O2Generator.Components.Get<MyResourceSourceComponent>();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = O2Generator.Storage;
            /*
            if (O2Set == null)
            {
                O2Set = new O2GeneratorSettings(O2Generator);
                O2Set.SaveSettings();
            }
            */
            if (O2State == null)
            {
                O2State = new O2GeneratorState(O2Generator);

                O2State.SaveState();
            }

            //O2Set.LoadSettings();
            //O2State.LoadState();
            //UpdateSettings(O2Set.Settings);
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                IsStatic = O2Generator.CubeGrid.Physics.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                if (!O2GenReady(Session.IsServer)) return;
                Timing();

                if (_count > 0) return;
                if (Session.IsServer)
                {
                    Pressurize();
                }
                else UpdateVisuals();

                if (ServerUpdate)
                {
                    Log.Line($"server update");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private bool O2GenReady(bool server)
        {
            ServerUpdate = false;

            if (server)
            {
                O2State.State.Pressurized = false;
                if (!AllInited && !InitO2Generator() || Suspend()) return false;
                if (!BlockWorking() || ShieldComp?.DefenseShields == null || !ShieldComp.DefenseShields.DsState.State.Online || ShieldComp.DefenseShields.DsState.State.Lowered)
                {
                    if (BlockIsWorking)
                    {
                        Timing();
                        UpdateAirEmissives(0f);
                        O2Generator.RefreshCustomInfo();
                    }
                    return false;
                }
            }
            else
            {
                if (!AllInited && !InitO2Generator() || !O2State.State.Pressurized)
                {
                    Timing();
                    UpdateAirEmissives(0f);
                    O2Generator.RefreshCustomInfo();
                    if (O2State.State.Pressurized) ServerUpdate = true;
                    return false;
                }
            }
            return true;
        }

        private void Pressurize()
        {
            var sc = ShieldComp;
            var shieldFullVol = sc.ShieldVolume;
            var increaseO2ByFPercent = sc.DefenseShields.DsState.State.IncreaseO2ByFPercent;
            var newO2Level = sc.DefaultO2 + increaseO2ByFPercent;

            if (!O2State.State.O2Level.Equals(newO2Level)) ServerUpdate = true;

            var startingO2Fpercent = sc.DefaultO2 + sc.DefenseShields.DsState.State.IncreaseO2ByFPercent;

            O2State.State.DefaultO2 = sc.DefaultO2;

            if (shieldFullVol < _oldShieldVol)
            {
                var ratio = _oldShieldVol / shieldFullVol;
                if (startingO2Fpercent * ratio > 1) startingO2Fpercent = 1d;
                else startingO2Fpercent = startingO2Fpercent * ratio;
            }
            else if (shieldFullVol > _oldShieldVol)
            {
                var ratio = _oldShieldVol / shieldFullVol;
                startingO2Fpercent = startingO2Fpercent * ratio;
                ServerUpdate = true;
            }
            _oldShieldVol = shieldFullVol;
            var newVolVilled = shieldFullVol * O2State.State.O2Level;
            if (!O2State.State.VolFilled.Equals(newVolVilled)) ServerUpdate = true;
            O2State.State.VolFilled = shieldFullVol * startingO2Fpercent;
            if (!Session.DedicatedServer) UpdateAirEmissives(O2State.State.O2Level);

            var shieldVolStillEmpty = shieldFullVol - O2State.State.VolFilled;
            if (O2State.State.VolFilled > 0) O2State.State.Pressurized = true;
            O2State.State.O2Level = startingO2Fpercent;

            Log.Line($"{O2State.State.O2Level} - {O2State.State.DefaultO2} - {O2State.State.VolFilled} - {O2State.State.ShieldVolume} - {O2State.State.Pressurized}");
            if (!(shieldVolStillEmpty > 0)) return;

            var amount = _inventory.CurrentVolume.RawValue;
            if (amount <= 0) return;
            if (amount - 10.3316326531 > 0)
            {
                _inventory.RemoveItems(0, 2700);
                O2State.State.VolFilled += 10.3316326531 * 261.333333333;
                ServerUpdate = true;
            }
            else
            {
                _inventory.RemoveItems(0, _inventory.CurrentVolume);
                O2State.State.VolFilled += amount * 261.333333333;
                ServerUpdate = true;
            }
            if (O2State.State.VolFilled > shieldFullVol) O2State.State.VolFilled = shieldFullVol;

            var shieldVolPercentFull = O2State.State.VolFilled * 100.0;
            var fPercentToAddToDefaultO2Level = shieldVolPercentFull / shieldFullVol * 0.01 - sc.DefaultO2;

            increaseO2ByFPercent = fPercentToAddToDefaultO2Level;
            sc.O2Updated = true;
            if (Session.Enforced.Debug == 1) Log.Line($"default:{ShieldComp.DefaultO2} - Filled/(Max):{O2State.State.VolFilled}/({shieldFullVol}) - ShieldO2Level:{increaseO2ByFPercent} - O2Before:{MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(O2Generator.PositionComp.WorldVolume.Center)}");
        }

        private void UpdateVisuals()
        {
            UpdateAirEmissives(O2State.State.O2Level);
        }

        private bool InitO2Generator()
        {
            if (!AllInited)
            {
                if (Session.IsServer)
                {
                    if (ShieldComp == null) O2Generator.CubeGrid.Components.TryGet(out ShieldComp);

                    if (ShieldComp?.DefenseShields == null || ShieldComp?.ActiveO2Generator != null || !ShieldComp.DefenseShields.Starting || ShieldComp.ShieldVolume <= 0) return false;
                    ShieldComp.ActiveO2Generator = this;
                    _oldShieldVol = ShieldComp.ShieldVolume;
                }

                RemoveControls();
                O2Generator.AppendingCustomInfo += AppendingCustomInfo;
                Source.Enabled = false;
                O2Generator.AutoRefill = false;
                _inventory = O2Generator.GetInventory();

                ResetAirEmissives(-1);
                BlockWasWorking = true;
                AllInited = true;
                return !Suspend();
            }
            return false;
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (_count == 29 && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                O2Generator.RefreshCustomInfo();
                O2Generator.ShowInToolbarConfig = false;
                O2Generator.ShowInToolbarConfig = true;
            }
            else if (_lCount % 2 == 0 && _count == 0) O2Generator.RefreshCustomInfo();
        }

        private bool BlockWorking()
        {
            if (ShieldComp?.DefenseShields == null || !ShieldComp.DefenseShields.Warming) return false;

            BlockIsWorking = O2Generator.IsWorking;
            BlockWasWorking = BlockIsWorking;

            return BlockIsWorking;
        }

        private bool Suspend()
        {
            if (ShieldComp?.ActiveO2Generator != this || !IsStatic)
            {
                return true;
            }
            if (!O2Generator.IsFunctional && BlockIsWorking)
            {
                BlockIsWorking = false;
                return true;
            }

            return false;
        }

        private void UpdateAirEmissives(double fPercent)
        {
            var tenPercent = fPercent * 10;
            if ((int)tenPercent != _airIPercent) _airIPercent = (int)tenPercent;
            else return;
            if (tenPercent > 9) tenPercent = 9;
            ResetAirEmissives(tenPercent);
        }

        private void ResetAirEmissives(double tenPercent)
        {
            for (int i = 0; i < 10; i++)
            {
                if (tenPercent < 0 || i > tenPercent)
                {
                    O2Generator.SetEmissiveParts("Emissive" + i, Color.Transparent, 0f);
                }
                else
                {
                    O2Generator.SetEmissiveParts("Emissive" + i, UtilsStatic.GetAirEmissiveColorFromDouble(i * 10), 1f);
                }
            }
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (!O2State.State.Pressurized)
            {
                stringBuilder.Append("\n" +
                                     "\n[ Shield Offline ]");
            }
            else
            {
                stringBuilder.Append("\n" +
                                     "\n[Ice-to-Air volumetric ratio]: 261.3" +
                                     "\n[Shield Volume]: " + O2State.State.ShieldVolume.ToString("N0") +
                                     "\n[Volume Filled]: " + O2State.State.VolFilled.ToString("N0") +
                                     "\n[Internal O2 Lvl]: " + ((O2State.State.O2Level + O2State.State.DefaultO2) * 100).ToString("0") + "%" +
                                     "\n[External O2 Lvl]: " + (O2State.State.DefaultO2 * 100).ToString("0") + "%");
            }
        }

        public void UpdateState(ProtoO2GeneratorState state)
        {

            if (Session.Enforced.Debug == 1) Log.Line($"UpdateState - O2GenId [{O2Generator.EntityId}]:\n{state}");
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_o2Generator.ContainsKey(Entity.EntityId)) _o2Generator.Remove(Entity.EntityId);
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }

        public static void RemoveControls()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyGasGenerator>(out actions);
            var aRefill = actions.First((x) => x.Id.ToString() == "Refill");
            aRefill.Enabled = block => false;
            var aAutoRefill = actions.First((x) => x.Id.ToString() == "Auto-Refill");
            aAutoRefill.Enabled = block => false;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyGasGenerator>(out controls);
            var cRefill = controls.First((x) => x.Id.ToString() == "Refill");
            cRefill.Enabled = block => false;
            cRefill.Visible = block => false;
            cRefill.RedrawControl();

            var cAutoRefill = controls.First((x) => x.Id.ToString() == "Auto-Refill");
            cAutoRefill.Enabled = block => false;
            cAutoRefill.Visible = block => false;
            cAutoRefill.RedrawControl();
        }
    }
}
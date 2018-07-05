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
using VRage.Game.Entity;
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

        private double _shieldVolFilled;
        private double _oldShieldVol;

        internal float EmissiveIntensity;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool Suspended;
        internal bool Prime;
        internal bool Alpha;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        public bool O2Online;

        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        internal O2GeneratorGridComponent OGridComp;
        internal MyResourceSourceComponent Source;
        private MyEntitySubpart _subpartRotor;

        internal DSUtils Dsutil1 = new DSUtils();

        public IMyGasGenerator O2Generator => (IMyGasGenerator)Entity;
        private IMyInventory _inventory;

        private readonly Dictionary<long, O2Generators> _o2Generator = new Dictionary<long, O2Generators>();

        public override void UpdateAfterSimulation100()
        {
            try
            {
                IsStatic = O2Generator.CubeGrid.Physics.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (Suspend() || StoppedWorking() || !AllInited && !InitO2Generator()) return;
                if (Prime && OGridComp?.Comp == null) MasterElection();

                if (!BlockWorking() || !ShieldComp.ShieldActive) return;

                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                {
                    O2Generator.RefreshCustomInfo();
                    O2Generator.ShowInToolbarConfig = false;
                    O2Generator.ShowInToolbarConfig = true;
                }
                else O2Generator.RefreshCustomInfo();

                var sc = ShieldComp;
                var shieldFullVol = sc.ShieldVolume;
                var startingO2Fpercent = sc.DefaultO2 + sc.IncreaseO2ByFPercent;

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
                }
                _oldShieldVol = shieldFullVol;

                _shieldVolFilled = shieldFullVol * startingO2Fpercent;
                UpdateAirEmissives(startingO2Fpercent);

                var shieldVolStillEmpty = shieldFullVol - _shieldVolFilled;
                if (!(shieldVolStillEmpty > 0)) return;

                var amount = _inventory.CurrentVolume.RawValue;
                if (amount <= 0) return;
                if (amount - 1000 > 0)
                {
                    _inventory.RemoveItems(0, 1000);
                    _shieldVolFilled += 1000 * 261.333333333;
                }
                else
                {
                    _inventory.RemoveItems(0, _inventory.CurrentVolume);
                    _shieldVolFilled += amount * 261.333333333;
                }
                if (_shieldVolFilled > shieldFullVol) _shieldVolFilled = shieldFullVol;

                var shieldVolPercentFull = _shieldVolFilled * 100.0;
                var fPercentToAddToDefaultO2Level = shieldVolPercentFull / shieldFullVol * 0.01 - sc.DefaultO2;

                sc.IncreaseO2ByFPercent = fPercentToAddToDefaultO2Level;
                sc.O2Updated = true;
                if (Session.Enforced.Debug == 1) Log.Line($"default:{ShieldComp.DefaultO2} - Filled/(Max):{_shieldVolFilled}/({shieldFullVol}) - ShieldO2Level:{sc.IncreaseO2ByFPercent} - O2Before:{MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(O2Generator.PositionComp.WorldVolume.Center)}");
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private bool BlockWorking()
        {
            if (Alpha || !IsStatic || ShieldComp.DefenseShields == null || !ShieldComp.Warming) return false;

            BlockIsWorking = O2Generator.IsWorking;
            BlockWasWorking = BlockIsWorking;

            if (!BlockIsWorking)
            {
                //if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
                return false;
            }
            return true;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
                Session.Instance.O2Generators.Add(this);
                if (!_o2Generator.ContainsKey(Entity.EntityId)) _o2Generator.Add(Entity.EntityId, this);
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private bool InitO2Generator()
        {
            if (!AllInited)
            {
                O2Generator.CubeGrid.Components.TryGet(out ShieldComp);
                Source = O2Generator.Components.Get<MyResourceSourceComponent>();
                if (ShieldComp == null || Source == null || !ShieldComp.Starting || ShieldComp.ShieldVolume <= 0) return false;
                RemoveControls();
                O2Generator.AppendingCustomInfo += AppendingCustomInfo;
                Source.Enabled = false;
                O2Generator.AutoRefill = false;
                _inventory = O2Generator.GetInventory();
                if (!O2Generator.CubeGrid.Components.Has<O2GeneratorGridComponent>())
                {
                    OGridComp = new O2GeneratorGridComponent(this);
                    O2Generator.CubeGrid.Components.Add(OGridComp);
                    OGridComp.Comp = this;
                    Prime = true;
                }
                else
                {
                    O2Generator.CubeGrid.Components.TryGet(out OGridComp);
                    if (OGridComp.Comp != null) OGridComp.Comp.Alpha = true;
                    OGridComp.Comp = this;
                    Prime = true;
                }

                _oldShieldVol = ShieldComp.ShieldVolume;
                ResetAirEmissives(-1);
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                OGridComp.RegisteredComps.Add(this);
                BlockWasWorking = true;
                AllInited = true;
                return !Suspend();
            }
            return false;
        }

        private bool Suspend()
        {
            if (Prime && !IsStatic)
            {
                return true;
            }

            return false;
        }

        private bool StoppedWorking()
        {
            if (!O2Generator.IsFunctional && BlockIsWorking)
            {
                BlockIsWorking = false;
                return true;
            }
            return !O2Generator.IsFunctional;
        }

        private void MasterElection()
        {
            var hasOComp = O2Generator.CubeGrid.Components.Has<O2GeneratorGridComponent>();
            if (!hasOComp)
            {
                if (!IsStatic) return;
                OGridComp = new O2GeneratorGridComponent(this);
                O2Generator.CubeGrid.Components.Add(OGridComp);
                _inventory = O2Generator.GetInventory();
                OGridComp.Comp = this;
                Prime = true;
                Alpha = false;
            }
            else 
            {
                if (!IsStatic) return;
                O2Generator.CubeGrid.Components.TryGet(out OGridComp);
                if (OGridComp.Comp != null) OGridComp.Comp.Alpha = true;
                _inventory = O2Generator.GetInventory();
                OGridComp.Comp = this;
                Prime = true;
                Alpha = false;
            }
            _oldShieldVol = ShieldComp.ShieldVolume;
            ResetAirEmissives(-1);
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
            stringBuilder.Append("\n" +
                                 "\n[Ice-to-Air volumetric ratio]: 261.3" +
                                 "\n[Shield Volume]: " + ShieldComp.ShieldVolume.ToString("N0") +
                                 "\n[Volume Filled]: " + _shieldVolFilled.ToString("N0") +
                                 "\n[Internal O2 Lvl]: " + ((ShieldComp.IncreaseO2ByFPercent + ShieldComp.DefaultO2) * 100).ToString("0") + "%" +
                                 "\n[External O2 Lvl]: " + (ShieldComp.DefaultO2 * 100).ToString("0") + "%");
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
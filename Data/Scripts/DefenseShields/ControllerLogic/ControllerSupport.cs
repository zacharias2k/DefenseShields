namespace DefenseSystems
{
    using Support;
    using Sandbox.ModAPI;
    using VRageMath;

    public partial class Controllers
    {
        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            var update = false;
            if (Bus.ActiveModulator != null && Bus.ActiveModulator.ModState.State.Online)
            {
                var modEnergyRatio = Bus.ActiveModulator.ModState.State.ModulateEnergy * 0.01f;
                var modKineticRatio = Bus.ActiveModulator.ModState.State.ModulateKinetic * 0.01f;
                if (!State.Value.ModulateEnergy.Equals(modEnergyRatio) || !State.Value.ModulateKinetic.Equals(modKineticRatio) || !State.Value.EmpProtection.Equals(Bus.ActiveModulator.ModSet.Settings.EmpEnabled)) update = true;
                State.Value.ModulateEnergy = modEnergyRatio;
                State.Value.ModulateKinetic = modKineticRatio;
                if (State.Value.Enhancer)
                {
                    State.Value.EmpProtection = Bus.ActiveModulator.ModSet.Settings.EmpEnabled;
                }

                if (update) ProtChangedState();
            }
            else
            {
                if (!State.Value.ModulateEnergy.Equals(1f) || !State.Value.ModulateKinetic.Equals(1f) || State.Value.EmpProtection) update = true;
                State.Value.ModulateEnergy = 1f;
                State.Value.ModulateKinetic = 1f;
                State.Value.EmpProtection = false;
                if (update) ProtChangedState();

            }
        }

        public void GetEnhancernInfo()
        {
            var update = false;
            if (Bus.ActiveEnhancer != null && Bus.ActiveEnhancer.EnhState.State.Online)
            {
                if (!State.Value.EnhancerPowerMulti.Equals(2) || !State.Value.EnhancerProtMulti.Equals(1000) || !State.Value.Enhancer) update = true;
                State.Value.EnhancerPowerMulti = 2;
                State.Value.EnhancerProtMulti = 1000;
                State.Value.Enhancer = true;
                if (update) ProtChangedState();
            }
            else
            {
                if (!State.Value.EnhancerPowerMulti.Equals(1) || !State.Value.EnhancerProtMulti.Equals(1) || State.Value.Enhancer) update = true;
                State.Value.EnhancerPowerMulti = 1;
                State.Value.EnhancerProtMulti = 1;
                State.Value.Enhancer = false;
                if (update) ProtChangedState();
            }
        }
        #endregion

        internal void TerminalRefresh(bool update = true)
        {
            Controller.RefreshCustomInfo();
            if (update && InControlPanel && InThisTerminal)
            {
                var mousePos = MyAPIGateway.Input.GetMousePosition();
                var startPos = new Vector2(800, 700);
                var endPos = new Vector2(1070, 750);
                var match1 = mousePos.Between(ref startPos, ref endPos);
                var match2 = mousePos.Y > 700 && mousePos.Y < 760 && mousePos.X > 810 && mousePos.X < 1070;
                if (!(match1 && match2)) MyCube.UpdateTerminal();
            }
        }

    }
}

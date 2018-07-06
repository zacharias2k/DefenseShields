using Sandbox.ModAPI;

namespace DefenseShields
{
    internal static class ModUi
    {
        #region Create UI
        internal static void CreateUi(IMyTerminalBlock modualator)
        {
            Session.Instance.CreateModulatorUi(modualator);
            Session.Instance.ModDamage.Enabled = block => true;
            Session.Instance.ModDamage.Visible = ShowControl;
            Session.Instance.ModVoxels.Enabled = block => true;
            Session.Instance.ModVoxels.Visible = ShowControl;
            Session.Instance.ModGrids.Enabled = block => true;
            Session.Instance.ModGrids.Visible = ShowControl;

            Session.Instance.ModSep1.Visible = ShowControl;
            Session.Instance.ModSep2.Visible = ShowControl;
        }

        internal static bool ShowControl(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            var station = comp != null;
            return station;
        }

        public static float GetDamage(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            return comp?.ModSet.Settings.ModulateDamage ?? 0;
        }

        public static void SetDamage(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            if (comp == null) return;

            ComputeDamage(comp, newValue);
            comp.ModSet.Settings.ModulateDamage = (int)newValue;
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
        }

        public static void ComputeDamage(Modulators comp, float newValue)
        {
            if (newValue < 100)
            {
                comp.ModulatorComp.Energy = 200 - newValue;
                comp.ModulatorComp.Kinetic = newValue;
            }
            else if (newValue > 100)
            {
                comp.ModulatorComp.Energy = 200 - newValue;
                comp.ModulatorComp.Kinetic = newValue;
            }
            else
            {
                comp.ModulatorComp.Kinetic = newValue;
                comp.ModulatorComp.Energy = newValue;
            }
            comp.ModulatorComp.ModulateDamage = (int)newValue;
        }

        public static bool GetVoxels(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            return comp?.ModSet.Settings.ModulateVoxels ?? false;
        }

        public static void SetVoxels(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            if (comp == null) return;
            comp.ModSet.Settings.ModulateVoxels = newValue;
            comp.ModulatorComp.ModulateVoxels = newValue;
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
        }

        public static bool GetGrids(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            return comp?.ModSet.Settings.ModulateGrids ?? false;
        }

        public static void SetGrids(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            if (comp == null) return;
            comp.ModSet.Settings.ModulateGrids = newValue;
            comp.ModulatorComp.ModulateGrids = newValue;
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
        }
        #endregion
    }
}

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
            Session.Instance.ModEmp.Enabled = block => true;
            Session.Instance.ModEmp.Visible = ShowEmp;
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
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static void ComputeDamage(Modulators comp, float newValue)
        {
            if (newValue < 100)
            {
                comp.ModState.State.ModulateEnergy = 200 - newValue;
                comp.ModState.State.ModulateKinetic = newValue;
            }
            else if (newValue > 100)
            {
                comp.ModState.State.ModulateEnergy = 200 - newValue;
                comp.ModState.State.ModulateKinetic = newValue;
            }
            else
            {
                comp.ModState.State.ModulateKinetic = newValue;
                comp.ModState.State.ModulateEnergy = newValue;
            }
            comp.ModState.State.ModulateDamage = (int)newValue;
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
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
        }

        internal static bool ShowEmp(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            var empControl = comp?.ShieldComp?.Enhancer != null && comp.ShieldComp?.DefenseShields != null && !comp.ShieldComp.DefenseShields.IsStatic;
            if (!empControl && comp?.ShieldComp?.DefenseShields != null && comp.ShieldComp.DefenseShields.IsStatic) comp.ModSet.Settings.EmpEnabled = true;
            return empControl;
        }

        public static bool GetEmpProt(IMyTerminalBlock block)
        {
            ShowEmp(block);
            var comp = block?.GameLogic?.GetAs<Modulators>();
            return comp?.ModSet.Settings.EmpEnabled ?? false;
        }

        public static void SetEmpProt(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            if (comp == null) return;
            comp.ModSet.Settings.EmpEnabled = newValue;
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
        }
        #endregion
    }
}

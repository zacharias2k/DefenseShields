using Sandbox.ModAPI;

namespace DefenseShields
{
    internal static class ModUi
    {
        #region Create UI
        internal static void CreateUi(IMyTerminalBlock modualator)
        {
            Session.Instance.CreateModulatorUi(modualator);
        }

        public static float GetDamage(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            return comp?.ModSet.Settings.ModulateDamage ?? 0f;
        }

        public static void SetDamage(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<Modulators>();
            if (comp == null) return;
            comp.ModSet.Settings.ModulateDamage = newValue;
            comp.ModSet.NetworkUpdate();
            comp.ModSet.SaveSettings();
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
        #endregion
    }
}

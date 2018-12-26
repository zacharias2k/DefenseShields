using Sandbox.ModAPI;

namespace DefenseShields
{
    internal static class O2Ui
    {
        #region Create UI
        internal static void CreateUi(IMyTerminalBlock o2Generator)
        {
            Session.Instance.CreateO2GeneratorUi(o2Generator);
            Session.Instance.O2DoorFix.Enabled = block => true;
            Session.Instance.O2DoorFix.Visible = ShowControl;
        }

        internal static bool ShowControl(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<O2Generators>();
            var valid = comp != null;
            return valid;
        }

        public static bool FixStatus(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<O2Generators>();
            return comp != null && comp.O2Set.Settings.FixRoomPressure;
        }

        public static void FixRooms(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<O2Generators>();
            if (comp == null) return;
            comp.O2Set.Settings.FixRoomPressure = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }
        #endregion
    }
}

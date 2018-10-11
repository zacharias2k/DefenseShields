using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DefenseShields
{
    internal static class DsUi
    {
        #region Create UI
        internal static void CreateUi(IMyTerminalBlock shield)
        {
            Session.Instance.WidthSlider.Visible = ShowSizeSlider;
            Session.Instance.HeightSlider.Visible = ShowSizeSlider;
            Session.Instance.DepthSlider.Visible = ShowSizeSlider;

            Session.Instance.OffsetWidthSlider.Visible = ShowSizeSlider;
            Session.Instance.OffsetHeightSlider.Visible = ShowSizeSlider;
            Session.Instance.OffsetDepthSlider.Visible = ShowSizeSlider;

            Session.Instance.ExtendFit.Visible = ShowReSizeCheckBoxs;
            Session.Instance.SphereFit.Visible = ShowReSizeCheckBoxs;
            Session.Instance.FortifyShield.Visible = ShowReSizeCheckBoxs;
        }

        internal static bool ShowSizeSlider(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            var station = comp != null && comp.Shield.CubeGrid.IsStatic;
            return station;
        }

        private static bool ShowReSizeCheckBoxs(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            var notStation = comp != null && !comp.Shield.CubeGrid.IsStatic;
            return notStation;
        }

        public static float GetRate(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.Rate ?? 0f;
        }

        public static void SetRate(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.Rate = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetExtend(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ExtendFit ?? false;
        }

        public static void SetExtend(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.ExtendFit = newValue;
            comp.FitChanged = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetSphereFit(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.SphereFit ?? false;
        }

        public static void SetSphereFit(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.SphereFit = newValue;
            comp.FitChanged = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetFortify(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.FortifyShield ?? false;
        }

        public static void SetFortify(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.FortifyShield = newValue;
            comp.FitChanged = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static float GetWidth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.Width ?? 0f;
        }

        public static void SetWidth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.Width = newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
        }

        public static float GetHeight(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.Height ?? 0f;
        }

        public static void SetHeight(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.Height = newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
        }

        public static float GetDepth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.Depth ?? 0f;
        }

        public static void SetDepth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.Depth = newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
        }

        public static float GetOffsetWidth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ShieldOffset.X ?? 0;
        }

        public static void SetOffsetWidth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;

            comp.DsSet.Settings.ShieldOffset.X = (int) newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
            block.ShowInToolbarConfig = false;
            block.ShowInToolbarConfig = true;
        }

        public static float GetOffsetHeight(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ShieldOffset.Y ?? 0;
        }

        public static void SetOffsetHeight(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;

            comp.DsSet.Settings.ShieldOffset.Y = (int) newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
            block.ShowInToolbarConfig = false;
            block.ShowInToolbarConfig = true;
        }

        public static float GetOffsetDepth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ShieldOffset.Z ?? 0;
        }

        public static void SetOffsetDepth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;

            comp.DsSet.Settings.ShieldOffset.Z = (int) newValue;
            comp.UpdateDimensions = true;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
            comp.LosCheckTick = Session.Instance.Tick + 1800;
            block.ShowInToolbarConfig = false;
            block.ShowInToolbarConfig = true;
        }

        public static bool GetBatteries(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.UseBatteries ?? false;
        }

        public static void SetBatteries(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.UseBatteries = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetHideActive(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ActiveInvisible ?? false;
        }

        public static void SetHideActive(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.ActiveInvisible = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetRefreshAnimation(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.RefreshAnimation ?? false;
        }

        public static void SetRefreshAnimation(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.RefreshAnimation = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetHitWaveAnimation(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.HitWaveAnimation ?? false;
        }

        public static void SetHitWaveAnimation(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.HitWaveAnimation = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetSendToHud(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.SendToHud ?? false;
        }

        public static void SetSendToHud(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.SendToHud = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static bool GetRaiseShield(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.RaiseShield ?? false;
        }

        public static void SetRaiseShield(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.RaiseShield = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static long GetShell(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.ShieldShell ?? 0;
        }

        public static void SetShell(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.ShieldShell = newValue;
            comp.SelectPassiveShell();
            comp.UpdatePassiveModel();
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }

        public static long GetVisible(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.DsSet.Settings.Visible ?? 0;
        }

        public static void SetVisible(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.DsSet.Settings.Visible = newValue;
            comp.SettingsUpdated = true;
            comp.ClientUiUpdate = true;
        }
        public static void ListShell(List<MyTerminalControlComboBoxItem> shellList)
        {
            foreach (var shell in ShellList) shellList.Add(shell);
        }

        public static void ListVisible(List<MyTerminalControlComboBoxItem> visibleList)
        {
            foreach (var visible in VisibleList) visibleList.Add(visible);
        }

        private static readonly List<MyTerminalControlComboBoxItem> ShellList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Medium Reflective") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("High Reflective") },
            new MyTerminalControlComboBoxItem() { Key = 2, Value = MyStringId.GetOrCompute("Low Reflective") },
            new MyTerminalControlComboBoxItem() { Key = 3, Value = MyStringId.GetOrCompute("Medium Reflective Red Tint") },
            new MyTerminalControlComboBoxItem() { Key = 4, Value = MyStringId.GetOrCompute("Medium Reflective Blue Tint") },
            new MyTerminalControlComboBoxItem() { Key = 5, Value = MyStringId.GetOrCompute("Medium Reflective Green Tint") },
            new MyTerminalControlComboBoxItem() { Key = 6, Value = MyStringId.GetOrCompute("Medium Reflective Purple Tint") },
            new MyTerminalControlComboBoxItem() { Key = 7, Value = MyStringId.GetOrCompute("Medium Reflective Gold Tint") },
            new MyTerminalControlComboBoxItem() { Key = 8, Value = MyStringId.GetOrCompute("Medium Reflective Orange Tint") },
            new MyTerminalControlComboBoxItem() { Key = 9, Value = MyStringId.GetOrCompute("Medium Reflective Cyan Tint") },
        };

        private static readonly List<MyTerminalControlComboBoxItem> VisibleList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Always Visible") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("Never Visible") },
            new MyTerminalControlComboBoxItem() { Key = 2, Value = MyStringId.GetOrCompute("Visible On Hit") }
        };
        #endregion
    }
}

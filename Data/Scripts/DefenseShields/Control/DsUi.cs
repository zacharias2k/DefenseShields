using System.Collections.Generic;
using Sandbox.ModAPI;
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

        public static string GetShieldRaised(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return null;

            return comp.ShieldComp.RaiseShield ? "On" : "Off";
        }

        public static float GetRate(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.Rate ?? 0f;
        }

        public static void SetRate(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.Rate = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetExtend(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.ExtendFit ?? false;
        }

        public static void SetExtend(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.ExtendFit = newValue;
            comp.FitChanged = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetSphereFit(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.SphereFit ?? false;
        }

        public static void SetSphereFit(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.SphereFit = newValue;
            comp.FitChanged = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetFortify(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.FortifyShield ?? false;
        }

        public static void SetFortify(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.FortifyShield = newValue;
            comp.FitChanged = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static float GetWidth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.Width ?? 0f;
        }

        public static void SetWidth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.Width = newValue;
            comp.FitChanged = true;
            comp.UpdateDimensions = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static float GetHeight(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.Height ?? 0f;
        }

        public static void SetHeight(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.Height = newValue;
            comp.FitChanged = true;
            comp.UpdateDimensions = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static float GetDepth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.Depth ?? 0f;
        }

        public static void SetDepth(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.Depth = newValue;
            comp.FitChanged = true;
            comp.UpdateDimensions = true;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetBatteries(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.UseBatteries ?? false;
        }

        public static void SetBatteries(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.UseBatteries = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetHidePassive(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.ShieldPassiveHide ?? false;
        }

        public static void SetHidePassive(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.ShieldPassiveHide = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetHideActive(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.ShieldActiveHide ?? false;
        }

        public static void SetHideActive(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.ShieldActiveHide = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetSendToHud(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.SendToHud ?? false;
        }

        public static void SetSendToHud(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.SendToHud = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static bool GetRaiseShield(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.ShieldComp.RaiseShield ?? false;
        }

        public static void SetRaiseShield(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.ShieldComp.RaiseShield = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static long GetShell(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            return comp?.ShieldShell ?? 0;
        }

        public static void SetShell(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.ShieldShell = newValue;
            comp.SelectPassiveShell();
            comp.UpdatePassiveModel();
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }

        public static void ListShell(List<MyTerminalControlComboBoxItem> shellList)
        {
            foreach (var shell in ShellList) shellList.Add(shell);
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
            new MyTerminalControlComboBoxItem() { Key = 9, Value = MyStringId.GetOrCompute("Medium Reflective Cyan Tint") }
        };
        #endregion
    }
}

using Sandbox.ModAPI;

namespace DefenseShields
{
    internal static class DsUi
    {
        #region Create UI
        internal static void CreateUi(IMyTerminalBlock shield)
        {
            Session.Instance.CreateControlerUi(shield);
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
            var station = comp != null && comp.Shield.CubeGrid.Physics.IsStatic;
            return station;
        }

        private static bool ShowReSizeCheckBoxs(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            var notStation = comp != null && !comp.Shield.CubeGrid.Physics.IsStatic;
            return notStation;
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
            return comp?.RaiseShield ?? false;
        }

        public static void SetRaiseShield(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.GameLogic?.GetAs<DefenseShields>();
            if (comp == null) return;
            comp.RaiseShield = newValue;
            comp.DsSet.NetworkUpdate();
            comp.DsSet.SaveSettings();
        }
        #endregion
    }
}

using Sandbox.ModAPI;

namespace DefenseShields
{
    static class DsGetSet
    {
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
            comp.UpdateDimensions = true;
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
    }
}

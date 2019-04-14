namespace DefenseSystems
{
    using System.Collections.Generic;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.ModAPI;
    using VRage.Utils;

    internal static class DsUi
    {
        #region Create UI
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

        private static readonly List<MyTerminalControlComboBoxItem> PowerScaleList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Disabled") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("KiloWatt") },
            new MyTerminalControlComboBoxItem() { Key = 2, Value = MyStringId.GetOrCompute("MegaWatt") },
            new MyTerminalControlComboBoxItem() { Key = 3, Value = MyStringId.GetOrCompute("GigaWatt") },
            new MyTerminalControlComboBoxItem() { Key = 4, Value = MyStringId.GetOrCompute("TeraWatt") },
        };

        private static readonly List<MyTerminalControlComboBoxItem> ModeShieldList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Boson Force Shield") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("Structural Integrity Field") },
        };

        private static readonly List<MyTerminalControlComboBoxItem> ModeArmorList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Regenerative Ablative Armor") },
        };

        private static readonly List<MyTerminalControlComboBoxItem> ModeAllList = new List<MyTerminalControlComboBoxItem>()
        {
            new MyTerminalControlComboBoxItem() { Key = 0, Value = MyStringId.GetOrCompute("Boson Force Shield") },
            new MyTerminalControlComboBoxItem() { Key = 1, Value = MyStringId.GetOrCompute("Structural Integrity Field") },
            new MyTerminalControlComboBoxItem() { Key = 2, Value = MyStringId.GetOrCompute("Regenerative Ablative Armor") },
        };

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
            var logic = block?.GameLogic?.GetAs<Controllers>();
            var station = logic != null && logic.Bus.IsStatic;
            return station;
        }

        internal static float GetRate(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Controllers>();
            return comp?.Set.Value.Rate ?? 0f;
        }

        internal static void SetRate(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.Rate = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetExtend(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ExtendFit ?? false;
        }

        internal static void SetExtend(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.ExtendFit = newValue;
            logic.Bus.Field.FitChanged = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetSphereFit(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Controllers>();
            return comp?.Set.Value.SphereFit ?? false;
        }

        internal static void SetSphereFit(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.SphereFit = newValue;
            logic.Bus.Field.FitChanged = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetFortify(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.FortifyShield ?? false;
        }

        internal static void SetFortify(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.FortifyShield = newValue;
            logic.Bus.Field.FitChanged = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static float GetWidth(IMyTerminalBlock block)
        {
            var comp = block?.GameLogic?.GetAs<Controllers>();
            return comp?.Set.Value.Width ?? 0f;
        }

        internal static void SetWidth(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.Width = newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
        }

        internal static float GetHeight(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.Height ?? 0f;
        }

        internal static void SetHeight(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.Height = newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
        }

        internal static float GetDepth(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.Depth ?? 0f;
        }

        internal static void SetDepth(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.Depth = newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
        }

        internal static float GetOffsetWidth(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ShieldOffset.X ?? 0;
        }

        internal static void SetOffsetWidth(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;

            logic.Set.Value.ShieldOffset.X = (int)newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
            ((MyCubeBlock)block).UpdateTerminal();
        }

        internal static float GetOffsetHeight(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ShieldOffset.Y ?? 0;
        }

        internal static void SetOffsetHeight(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;

            logic.Set.Value.ShieldOffset.Y = (int)newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
            ((MyCubeBlock)block).UpdateTerminal();
        }

        internal static float GetOffsetDepth(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ShieldOffset.Z ?? 0;
        }

        internal static void SetOffsetDepth(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;

            logic.Set.Value.ShieldOffset.Z = (int)newValue;
            logic.Bus.Field.UpdateDimensions = true;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
            logic.Bus.Field.LosCheckTick = Session.Instance.Tick + 1800;
            ((MyCubeBlock)block).UpdateTerminal();
        }

        internal static bool GetBatteries(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.UseBatteries ?? false;
        }

        internal static void SetBatteries(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.UseBatteries = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetHideActive(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ActiveInvisible ?? false;
        }

        internal static void SetHideActive(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.ActiveInvisible = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetRefreshAnimation(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.RefreshAnimation ?? false;
        }

        internal static void SetRefreshAnimation(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.RefreshAnimation = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetHitWaveAnimation(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.HitWaveAnimation ?? false;
        }

        internal static void SetHitWaveAnimation(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.HitWaveAnimation = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetNoWarningSounds(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.NoWarningSounds ?? false;
        }

        internal static void SetDimShieldHits(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.DimShieldHits = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetDimShieldHits(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.DimShieldHits ?? false;
        }

        internal static void SetNoWarningSounds(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.NoWarningSounds = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetSendToHud(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.SendToHud ?? false;
        }

        internal static void SetSendToHud(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.SendToHud = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool GetRaiseShield(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.RaiseShield ?? false;
        }

        internal static void SetRaiseShield(IMyTerminalBlock block, bool newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.RaiseShield = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static long GetShell(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ShieldShell ?? 0;
        }

        internal static void SetShell(IMyTerminalBlock block, long newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic?.Bus?.Field == null) return;
            logic.Set.Value.ShieldShell = newValue;
            logic.Bus.Field.SelectPassiveShell();
            logic.Bus.Field.UpdatePassiveModel();
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static long GetVisible(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.Visible ?? 0;
        }

        internal static void SetVisible(IMyTerminalBlock block, long newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.Visible = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }
        internal static void ListShell(List<MyTerminalControlComboBoxItem> shellList)
        {
            foreach (var shell in ShellList) shellList.Add(shell);
        }

        internal static void ListVisible(List<MyTerminalControlComboBoxItem> visibleList)
        {
            foreach (var visible in VisibleList) visibleList.Add(visible);
        }

        private static bool ShowReSizeCheckBoxs(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            var notStation = logic != null && !logic.Bus.IsStatic;
            return notStation;
        }

        internal static void ListPowerScale(List<MyTerminalControlComboBoxItem> powerScaleList)
        {
            foreach (var scale in PowerScaleList) powerScaleList.Add(scale);
        }

        internal static long GetPowerScale(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.PowerScale ?? 0;
        }

        internal static void SetPowerScale(IMyTerminalBlock block, long newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.PowerScale = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static float GetPowerWatts(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.PowerWatts ?? 0;
        }

        internal static void SetPowerWatts(IMyTerminalBlock block, float newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.PowerWatts = (int)newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }

        internal static bool EnablePowerWatts(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return false;
            return logic.Set.Value.PowerScale != 0;
        }

        internal static void ListArmor(List<MyTerminalControlComboBoxItem> modeList)
        {
            foreach (var mode in ModeArmorList) modeList.Add(mode);
        }

        internal static bool VisibleArmor(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic != null && logic.Bus.ActiveEmitter == null && logic.Bus.ActiveRegen != null;
        }

        internal static void ListShield(List<MyTerminalControlComboBoxItem> modeList)
        {
            foreach (var mode in ModeShieldList) modeList.Add(mode);
        }

        internal static bool VisibleShield(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic != null && logic.Bus.ActiveRegen == null && logic.Bus.ActiveEmitter != null;
        }

        internal static void ListAll(List<MyTerminalControlComboBoxItem> modeList)
        {
            foreach (var mode in ModeAllList) modeList.Add(mode);
        }

        internal static bool VisibleAll(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic != null && logic.Bus.ActiveRegen != null && logic.Bus.ActiveEmitter != null;
        }

        internal static bool EnableModes(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic != null;
        }

        internal static long GetModes(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic?.Set.Value.ProtectMode ?? 0;
        }

        internal static void SetModes(IMyTerminalBlock block, long newValue)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            if (logic == null) return;
            logic.Set.Value.ProtectMode = newValue;
            logic.SettingsUpdated = true;
            logic.ClientUiUpdate = true;
        }
        #endregion
    }
}

using DefenseShields.Support;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            Enabled = newSettings.Enabled;
            ShieldPassiveHide = newSettings.PassiveInvisible;
            ShieldActiveHide = newSettings.ActiveInvisible;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
            Rate = newSettings.Rate;
            ExtendFit = newSettings.ExtendFit;
            SphereFit = newSettings.SphereFit;
            FortifyShield = newSettings.FortifyShield;
            UseBatteries = newSettings.UseBatteries;
            SendToHud = newSettings.SendToHud;
            ShieldBuffer = newSettings.Buffer;
            ShieldComp.IncreaseO2ByFPercent = newSettings.IncreaseO2ByFPercent;
            ShieldComp.ShieldActive = newSettings.ShieldActive;
            ShieldComp.RaiseShield = newSettings.RaiseShield;

            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }

        public void UpdateStats(ShieldStats stats)
        {
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateStats - ShieldId [{Shield.EntityId}]:\n{stats}");
        }
    }
}

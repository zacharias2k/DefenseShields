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

            ModulateVoxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSet - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }
    }
}

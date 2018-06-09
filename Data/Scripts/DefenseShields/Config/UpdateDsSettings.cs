using DefenseShields.Support;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            Enabled = newSettings.Enabled;
            ShieldIdleVisible = newSettings.IdleInvisible;
            ShieldActiveVisible = newSettings.ActiveInvisible;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
            Rate = newSettings.Rate;
            ExtendFit = newSettings.ExtendFit;
            SphereFit = newSettings.SphereFit;
            FortifyShield = newSettings.FortifyShield;
            ShieldBuffer = newSettings.Buffer;
            ModulateVoxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;

            if (Session.Enforced.Debug == 1) Log.Line($"Updated settings:\n{newSettings}");
        }
    }
}

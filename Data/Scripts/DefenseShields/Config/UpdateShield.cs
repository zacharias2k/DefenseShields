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
            ShieldComp.ShieldActive = newSettings.ShieldActive;
            ShieldComp.RaiseShield = newSettings.RaiseShield;

            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }

        public void UpdateState(ShieldState state)
        {
            if (!DsStatus.State.Online)
            {
                if (DsStatus.State.Overload) PlayerMessages(PlayerNotice.OverLoad);
                else if (DsStatus.State.Waking) PlayerMessages(PlayerNotice.EmitterInit);
                else if (DsStatus.State.FieldBlocked) PlayerMessages(PlayerNotice.FieldBlocked);
                else if (DsStatus.State.Remodulate) PlayerMessages(PlayerNotice.Remodulate);
                OfflineShield();
            }
            else ResetShape(false, false);

            if (Session.Enforced.Debug == 1) Log.Line($"UpdateState - ShieldId [{Shield.EntityId}]:\n{state}");
        }
    }
}

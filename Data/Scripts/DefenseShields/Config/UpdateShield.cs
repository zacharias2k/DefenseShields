using DefenseShields.Support;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            DsSet.Settings = newSettings;
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

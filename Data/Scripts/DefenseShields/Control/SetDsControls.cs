using DefenseShields.Support;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void SyncControlsServer()
        {
            if (Shield != null && !Shield.Enabled.Equals(DsSet.Settings.Enabled))
            {
                Enabled = DsSet.Settings.Enabled;
            }

            if (_widthSlider != null && !_widthSlider.Getter(Shield).Equals(DsSet.Settings.Width))
            {
                _widthSlider.Setter(Shield, DsSet.Settings.Width);
            }

            if (_heightSlider != null && !_heightSlider.Getter(Shield).Equals(DsSet.Settings.Height))
            {
                _heightSlider.Setter(Shield, DsSet.Settings.Height);
            }

            if (_depthSlider != null && !_depthSlider.Getter(Shield).Equals(DsSet.Settings.Depth))
            {
                _depthSlider.Setter(Shield, DsSet.Settings.Depth);
            }

            if (_chargeSlider != null && !_chargeSlider.Getter(Shield).Equals(DsSet.Settings.Rate))
            {
                _chargeSlider.Setter(Shield, DsSet.Settings.Rate);
            }

            if (_extendFit != null && !_extendFit.Getter(Shield).Equals(DsSet.Settings.ExtendFit))
            {
                _extendFit.Setter(Shield, DsSet.Settings.ExtendFit);
            }

            if (_sphereFit != null && !_sphereFit.Getter(Shield).Equals(DsSet.Settings.SphereFit))
            {
                _sphereFit.Setter(Shield, DsSet.Settings.SphereFit);
            }

            if (_fortifyShield != null && !_fortifyShield.Getter(Shield).Equals(DsSet.Settings.FortifyShield))
            {
                _fortifyShield.Setter(Shield, DsSet.Settings.FortifyShield);
            }

            if (_hideActiveCheckBox != null && !_hideActiveCheckBox.Getter(Shield).Equals(DsSet.Settings.ActiveInvisible))
            {
                _hideActiveCheckBox.Setter(Shield, DsSet.Settings.ActiveInvisible);
            }

            if (_hidePassiveCheckBox != null && !_hidePassiveCheckBox.Getter(Shield).Equals(DsSet.Settings.PassiveInvisible))
            {
                _hidePassiveCheckBox.Setter(Shield, DsSet.Settings.PassiveInvisible);
            }

            if (_sendToHudCheckBoxe != null && !_sendToHudCheckBoxe.Getter(Shield).Equals(DsSet.Settings.SendToHud))
            {
                _sendToHudCheckBoxe.Setter(Shield, DsSet.Settings.SendToHud);
            }

            ServerUpdate = false;
            _updateDimensions = true;
            DsSet.SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer");
        }
    }
}

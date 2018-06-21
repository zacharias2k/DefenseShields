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

            if (_hidePassiveCheckBox != null && !_hidePassiveCheckBox.Getter(Shield).Equals(DsSet.Settings.IdleInvisible))
            {
                _hidePassiveCheckBox.Setter(Shield, DsSet.Settings.IdleInvisible);
            }

            ServerUpdate = false;
            _updateDimensions = true;
            DsSet.SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!Enabled.Equals(Enabled)
                || !_chargeSlider.Getter(Shield).Equals(Rate)
                || !_hideActiveCheckBox.Getter(Shield).Equals(ShieldActiveVisible)
                || !_hidePassiveCheckBox.Getter(Shield).Equals(ShieldIdleVisible))
            {
                Enabled = DsSet.Settings.Enabled;
                Rate = _chargeSlider.Getter(Shield);
                ShieldActiveVisible = _hideActiveCheckBox.Getter(Shield);
                ShieldIdleVisible = _hidePassiveCheckBox.Getter(Shield);
                needsSync = true;
            }

            if (!_gridIsMobile)
            {
                if (!_widthSlider.Getter(Shield).Equals(Width)
                    || !_heightSlider.Getter(Shield).Equals(Height)
                    || !_depthSlider.Getter(Shield).Equals(Depth))
                {
                    Width = _widthSlider.Getter(Shield);
                    Height = _heightSlider.Getter(Shield);
                    Depth = _depthSlider.Getter(Shield);
                    needsSync = true;
                }
            }
            else
            {
                if (!_extendFit.Getter(Shield).Equals(ExtendFit)
                    || !_sphereFit.Getter(Shield).Equals(SphereFit)
                    || !_fortifyShield.Getter(Shield).Equals(FortifyShield))
                {
                    ExtendFit = _extendFit.Getter(Shield);
                    SphereFit = _sphereFit.Getter(Shield);
                    FortifyShield = _fortifyShield.Getter(Shield);
                    needsSync = true;
                    _fitChanged = true;
                }
            }

            if (needsSync)
            {
                if (!_gridIsMobile) _updateDimensions = true;
                DsSet.NetworkUpdate();
                DsSet.SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Needed sync");
            }
        }
    }
}

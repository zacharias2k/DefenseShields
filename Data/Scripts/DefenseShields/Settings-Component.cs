using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Settings
        private void SyncControlsServer()
        {
            if (Shield != null && !Shield.Enabled.Equals(Settings.Enabled))
            {
                Enabled = Settings.Enabled;
            }
            if (_widthSlider != null && !_widthSlider.Getter(Shield).Equals(Settings.Width))
            {
                _widthSlider.Setter(Shield, Settings.Width);
            }

            if (_heightSlider != null && !_heightSlider.Getter(Shield).Equals(Settings.Height))
            {
                _heightSlider.Setter(Shield, Settings.Height);
            }

            if (_depthSlider != null && !_depthSlider.Getter(Shield).Equals(Settings.Depth))
            {
                _depthSlider.Setter(Shield, Settings.Depth);
            }

            if (_chargeSlider != null && !_chargeSlider.Getter(Shield).Equals(Settings.Rate))
            {
                _chargeSlider.Setter(Shield, Settings.Rate);
            }

            if (_extendFit != null && !_extendFit.Getter(Shield).Equals(Settings.ExtendFit))
            {
                _extendFit.Setter(Shield, Settings.ExtendFit);
            }

            if (_sphereFit != null && !_sphereFit.Getter(Shield).Equals(Settings.SphereFit))
            {
                _sphereFit.Setter(Shield, Settings.SphereFit);
            }

            if (_fortifyShield != null && !_fortifyShield.Getter(Shield).Equals(Settings.FortifyShield))
            {
                _fortifyShield.Setter(Shield, Settings.FortifyShield);
            }

            if (_hideActiveCheckBox != null && !_hideActiveCheckBox.Getter(Shield).Equals(Settings.ActiveInvisible))
            {
                _hideActiveCheckBox.Setter(Shield, Settings.ActiveInvisible);
            }

            if (_hidePassiveCheckBox != null && !_hidePassiveCheckBox.Getter(Shield).Equals(Settings.IdleInvisible))
            {
                _hidePassiveCheckBox.Setter(Shield, Settings.IdleInvisible);
            }

            ServerUpdate = false;
            _updateDimensions = true;
            SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!GridIsMobile)
            {
                if (!Enabled.Equals(Enabled) ||
                    !_widthSlider.Getter(Shield).Equals(Width)
                    || !_heightSlider.Getter(Shield).Equals(Height)
                    || !_depthSlider.Getter(Shield).Equals(Depth)
                    || !_chargeSlider.Getter(Shield).Equals(Rate)
                    || !_hideActiveCheckBox.Getter(Shield).Equals(ShieldActiveVisible)
                    || !_hidePassiveCheckBox.Getter(Shield).Equals(ShieldIdleVisible))
                {
                    needsSync = true;
                    Enabled = Settings.Enabled;
                    Width = _widthSlider.Getter(Shield);
                    Height = _heightSlider.Getter(Shield);
                    Depth = _depthSlider.Getter(Shield);
                    Rate = _chargeSlider.Getter(Shield);
                    ShieldActiveVisible = _hideActiveCheckBox.Getter(Shield);
                    ShieldIdleVisible = _hidePassiveCheckBox.Getter(Shield);
                }
            }
            else
            {
                if (!Enabled.Equals(Enabled) 
                    || !_chargeSlider.Getter(Shield).Equals(Rate)
                    || !_hideActiveCheckBox.Getter(Shield).Equals(ShieldActiveVisible)
                    || !_hidePassiveCheckBox.Getter(Shield).Equals(ShieldIdleVisible)
                    || !_chargeSlider.Getter(Shield).Equals(Rate))
                {
                    needsSync = true;
                    Enabled = Settings.Enabled;
                    Rate = _chargeSlider.Getter(Shield);

                    ShieldActiveVisible = _hideActiveCheckBox.Getter(Shield);
                    ShieldIdleVisible = _hidePassiveCheckBox.Getter(Shield);
                }

                if (!_extendFit.Getter(Shield).Equals(ExtendFit)
                    || !_sphereFit.Getter(Shield).Equals(SphereFit)
                    || !_fortifyShield.Getter(Shield).Equals(FortifyShield))
                {
                    needsSync = true;
                    ExtendFit = _extendFit.Getter(Shield);
                    SphereFit = _sphereFit.Getter(Shield);
                    FortifyShield = _fortifyShield.Getter(Shield);
                    _fitChanged = true;
                }
            }

            if (needsSync)
            {
                if (!GridIsMobile) _updateDimensions = true;
                NetworkUpdate();
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Needed sync");
            }
        }

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

        public void UpdateEnforcement(DefenseShieldsEnforcement newEnforce)
        {
            Session.Enforced.Nerf = newEnforce.Nerf;
            Session.Enforced.BaseScaler = newEnforce.BaseScaler;
            Session.Enforced.Efficiency = newEnforce.Efficiency;
            Session.Enforced.StationRatio = newEnforce.StationRatio;
            Session.Enforced.LargeShipRatio = newEnforce.LargeShipRatio;
            Session.Enforced.SmallShipRatio = newEnforce.SmallShipRatio;
            Session.Enforced.DisableVoxelSupport = newEnforce.DisableVoxelSupport;
            Session.Enforced.DisableGridDamageSupport = newEnforce.DisableGridDamageSupport;
            Session.Enforced.Debug = newEnforce.Debug;
            Session.Enforced.AltRecharge = newEnforce.AltRecharge;
            Session.Enforced.Version = newEnforce.Version;
            Session.Enforced.SenderId = newEnforce.SenderId;

            if (Session.Enforced.Debug == 1) Log.Line($"Updated Enforcements:\n{Session.Enforced}");
        }

        public void SaveSettings()
        {
            if (Shield.Storage == null)
            {
                Log.Line($"ShieldId:{Shield.EntityId.ToString()} - Storage = null");
                Shield.Storage = new MyModStorageComponent();
            }
            Shield.Storage[Session.Instance.SettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(Session.Instance.SettingsGuid, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"ShieldId:{Shield.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Loaded settings:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        public bool Enabled
        {
            get { return Settings.Enabled; }
            set { Settings.Enabled = value; }
        }

        public bool ShieldIdleVisible
        {
            get { return Settings.IdleInvisible; }
            set { Settings.IdleInvisible = value; }
        }

        public bool ShieldActiveVisible
        {
            get { return Settings.ActiveInvisible; }
            set { Settings.ActiveInvisible = value; }
        }

        public float Width
        {
            get { return Settings.Width; }
            set { Settings.Width = value; }
        }

        public float Height
        {
            get { return Settings.Height; }
            set { Settings.Height = value; }
        }

        public float Depth
        {
            get { return Settings.Depth; }
            set { Settings.Depth = value; }
        }

        public float Rate
        {
            get { return Settings.Rate; }
            set { Settings.Rate = value; }
        }

        public bool ExtendFit
        {
            get { return Settings.ExtendFit; }
            set { Settings.ExtendFit = value; }
        }

        public bool SphereFit
        {
            get { return Settings.SphereFit; }
            set { Settings.SphereFit = value; }
        }

        public bool FortifyShield
        {
            get { return Settings.FortifyShield; }
            set { Settings.FortifyShield = value; }
        }
        public float ShieldBuffer
        {
            get { return Settings.Buffer; }
            set { Settings.Buffer = value; }
        }

        public bool ModulateVoxels
        {
            get { return Settings.ModulateVoxels; }
            set { Settings.ModulateVoxels = value; }
        }

        public bool ModulateGrids
        {
            get { return Settings.ModulateGrids; }
            set { Settings.ModulateGrids = value; }
        }

        private void EnforcementRequest()
        {
            if (Session.IsServer)
            {
                Log.Line($"I am the host, no one has power over me:\n{Session.Enforced}");
            }
            else 
            {
                Log.Line($"Client [{MyAPIGateway.Multiplayer.MyId}] requesting enforcement - current:\n{Session.Enforced}");
                Session.Enforced.SenderId = MyAPIGateway.Multiplayer.MyId;
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new EnforceData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Session.Enforced));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_ENFORCE, bytes);
            }
        }

        private void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for shield {Shield.EntityId}");
                Session.PacketizeShieldSettings(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for shield {Shield.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new PacketData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_SETTINGS, bytes);
            }
        }
        #endregion
    }
}

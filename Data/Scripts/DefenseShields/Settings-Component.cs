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
                if (!Enabled.Equals(Enabled) ||
                    !_chargeSlider.Getter(Shield).Equals(Rate)
                    || !_hideActiveCheckBox.Getter(Shield).Equals(ShieldActiveVisible)
                    || !_hidePassiveCheckBox.Getter(Shield).Equals(ShieldIdleVisible))
                {
                    needsSync = true;
                    Enabled = Settings.Enabled;
                    Rate = _chargeSlider.Getter(Shield);
                    ShieldActiveVisible = _hideActiveCheckBox.Getter(Shield);
                    ShieldIdleVisible = _hidePassiveCheckBox.Getter(Shield);
                }
            }

            if (needsSync)
            {
                if (!GridIsMobile) _updateDimensions = true;
                NetworkUpdate();
                SaveSettings();
            }
        }

        public void UpdateSettings(DefenseShieldsModSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            ShieldIdleVisible = newSettings.IdleInvisible;
            ShieldActiveVisible = newSettings.ActiveInvisible;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
            Rate = newSettings.Rate;
            ShieldBuffer = newSettings.Buffer;

            if (localOnly)
            {
                ShieldBaseScaler = newSettings.BaseScaler;
                ShieldNerf = newSettings.Nerf;
                ShieldEfficiency = newSettings.Efficiency;
                StationRatio = newSettings.StationRatio;
                LargeShipRatio = newSettings.LargeShipRatio;
                SmallShipRatio = newSettings.SmallShipRatio;
                VoxelSupport = newSettings.DisableVoxelSupport;
                GridDamageSupport = newSettings.DisableGridDamageSupport;
                Debug = newSettings.Debug;
            }
        }

        public void UpdateEnforcement(DefenseShieldsEnforcement newEnforce)
        {
            ShieldNerf = newEnforce.Nerf;
            ShieldBaseScaler = newEnforce.BaseScaler;
            ShieldEfficiency = newEnforce.Efficiency;
            StationRatio = newEnforce.StationRatio;
            LargeShipRatio = newEnforce.LargeShipRatio;
            SmallShipRatio = newEnforce.SmallShipRatio;
            VoxelSupport = newEnforce.DisableVoxelSupport;
            GridDamageSupport = newEnforce.DisableGridDamageSupport;
            Debug = newEnforce.Debug;

            ServerEnforcedValues.Nerf = newEnforce.Nerf;
            ServerEnforcedValues.BaseScaler = newEnforce.BaseScaler;
            ServerEnforcedValues.Efficiency = newEnforce.Efficiency;
            ServerEnforcedValues.StationRatio = newEnforce.StationRatio;
            ServerEnforcedValues.LargeShipRatio = newEnforce.LargeShipRatio;
            ServerEnforcedValues.SmallShipRatio = newEnforce.SmallShipRatio;
            ServerEnforcedValues.DisableVoxelSupport = newEnforce.DisableVoxelSupport;
            ServerEnforcedValues.DisableGridDamageSupport = newEnforce.DisableGridDamageSupport;
            ServerEnforcedValues.Debug = newEnforce.Debug;
        }

        public void SaveSettings()
        {
            if (Shield.Storage == null)
            {
                Log.Line($"ShieldId:{Shield.EntityId.ToString()} - Storage = null");
                Shield.Storage = new MyModStorageComponent();
            }
            Shield.Storage[DefenseShieldsBase.Instance.SettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Shield.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Shield.Storage.TryGetValue(DefenseShieldsBase.Instance.SettingsGuid, out rawData))
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
                //Log.Line($"Loaded settings:\n{Settings.ToString()}");
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

        public float ShieldBuffer
        {
            get { return Settings.Buffer; }
            set { Settings.Buffer = value; }
        }

        public float ShieldNerf
        {
            get { return Settings.Nerf; }
            set { Settings.Nerf = value; }
        }

        public float ShieldEfficiency
        {
            get { return Settings.Efficiency; }
            set { Settings.Efficiency = value; }
        }

        public int ShieldBaseScaler
        {
            get { return Settings.BaseScaler; }
            set { Settings.BaseScaler = value; }
        }

        public int StationRatio
        {
            get { return Settings.StationRatio; }
            set { Settings.StationRatio = value; }
        }

        public int LargeShipRatio
        {
            get { return Settings.LargeShipRatio; }
            set { Settings.LargeShipRatio = value; }
        }

        public int SmallShipRatio
        {
            get { return Settings.SmallShipRatio; }
            set { Settings.SmallShipRatio = value; }
        }

        public int VoxelSupport
        {
            get { return Settings.DisableVoxelSupport; }
            set { Settings.DisableVoxelSupport = value; }
        }

        public int GridDamageSupport
        {
            get { return Settings.DisableGridDamageSupport; }
            set { Settings.DisableGridDamageSupport = value; }
        }

        public int Debug
        {
            get { return Settings.Debug; }
            set { Settings.Debug = value; }
        }

        private void EnforcementRequest()
        {
            if ((MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Multiplayer.IsServer))
            {
                //Log.Line($"This is the server (Dedicated: {MyAPIGateway.Utilities.IsDedicated}) bypassing enforcement request");
            }
            else 
            {
                //Log.Line($"Client requesting enforcement - current: {ShieldNerf} - {ShieldBaseScaler} - {Settings.Nerf} - {Settings.BaseScaler} - {ServerEnforcedValues.Nerf} - {ServerEnforcedValues.BaseScaler}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new EnforceData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, ServerEnforcedValues));
                MyAPIGateway.Multiplayer.SendMessageToServer(DefenseShieldsBase.PACKET_ID_ENFORCE, bytes);
            }
        }

        private void NetworkUpdate()
        {

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                //Log.Line($"server sent network update for shield {Shield.EntityId}");
                DefenseShieldsBase.RelaySettingsToClients(Shield, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                //Log.Line($"client sent network update {Shield.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new PacketData(MyAPIGateway.Multiplayer.MyId, Shield.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(DefenseShieldsBase.PACKET_ID_SETTINGS, bytes);
            }
        }
        #endregion
    }
}

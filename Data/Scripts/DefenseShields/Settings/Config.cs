using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRageMath;

namespace DefenseShields.Settings
{
    class Config
    {
        #region Settings
        private bool needsMatrixUpdate = false;
        public const float SliderMin = 30f; 
        public const float SliderMax = 300f;
        public float LargestGridLength = 2.5f;

        public MyModStorageComponent Storage { get; set; }
        internal HashSet<ulong> playersToReceive = null;
        internal DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();

        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            Shield = newSettings.Enabled;
            ShieldIdleVisible = newSettings.IdleVisible;
            ShieldActiveVisible = newSettings.ActiveVisible;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
        }

        public void SaveSettings()
        {
            if (Storage == null) Storage = new MyModStorageComponent();
            Storage[DefenseShieldsBase.Instance.SettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Storage.TryGetValue(DefenseShieldsBase.Instance.SettingsGuid, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
                Log.Line($"Loaded settings:\n{Settings.ToString()}");
            }
            return loadedSomething;
        }

        public bool Shield
        {
            get { return Settings.Enabled; }
            set
            {
                Settings.Enabled = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public bool ShieldIdleVisible
        {
            get { return Settings.IdleVisible; }
            set
            {
                Settings.IdleVisible = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public bool ShieldActiveVisible
        {
            get { return Settings.ActiveVisible; }
            set
            {
                Settings.ActiveVisible = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public float Width
        {
            get { return Settings.Width; }
            set
            {
                Settings.Width = value;
                needsMatrixUpdate = true;
            }
        }

        public float Height
        {
            get { return Settings.Height; }
            set
            {
                Settings.Height = value;
                needsMatrixUpdate = true;
            }
        }

        public float Depth
        {
            get { return Settings.Depth; }
            set
            {
                Settings.Depth = value;
                needsMatrixUpdate = true;
            }
        }

        private void RefreshControls(bool refreshRemoveButton = false, bool refeshCustomInfo = false)
        {
        }

        public void UseThisShip_Receiver(bool fix)
        {
            Log.Line($"UseThisShip_Receiver({fix.ToString()})");

            //UseThisShip_Internal(fix);
        }
        #endregion
    }
}

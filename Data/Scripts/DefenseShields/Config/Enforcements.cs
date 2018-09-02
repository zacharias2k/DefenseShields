using System;
using DefenseShields.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    internal class Enforcements
    {
        public static void SaveEnforcement(IMyFunctionalBlock shield, DefenseShieldsEnforcement enforce, bool createStorage = false)
        {
            if (createStorage && shield.Storage == null) shield.Storage = new MyModStorageComponent();
            else if (shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(enforce);
            shield.Storage[Session.Instance.ControllerEnforceGuid] = Convert.ToBase64String(binary);
            Log.Line($"Enforcement Saved - Version:{enforce.Version} - ShieldId [{shield.EntityId}]");
        }

        public static DefenseShieldsEnforcement LoadEnforcement(IMyFunctionalBlock shield)
        {
            if (shield.Storage == null) return null;

            string rawData;

            if (shield.Storage.TryGetValue(Session.Instance.ControllerEnforceGuid, out rawData))
            {
                DefenseShieldsEnforcement loadedEnforce = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedEnforce = MyAPIGateway.Utilities.SerializeFromBinary<DefenseShieldsEnforcement>(base64);
                Log.Line($"Enforcement Loaded {loadedEnforce != null} - Version:{loadedEnforce?.Version} - ShieldId [{shield.EntityId}]");
                if (loadedEnforce != null) return loadedEnforce;
            }
            Log.Line($"Enforcement Not Loaded - ShieldId [{shield.EntityId}]");
            return null;
        }
    }
}

using System;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DefenseShields
{
    internal class Enforcements
    {
        public static void SaveEnforcement(IMyFunctionalBlock shield, EnforcementValues enforce, bool createStorage = false)
        {
            if (createStorage && shield.Storage == null) shield.Storage = new MyModStorageComponent();
            else if (shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(enforce);
            shield.Storage[Session.Instance.ControllerEnforceGuid] = Convert.ToBase64String(binary);
            if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Saved - Version:{enforce.Version} - ShieldId [{shield.EntityId}]");
        }

        public static EnforcementValues LoadEnforcement(IMyFunctionalBlock shield)
        {
            if (shield.Storage == null) return null;

            string rawData;

            if (shield.Storage.TryGetValue(Session.Instance.ControllerEnforceGuid, out rawData))
            {
                EnforcementValues loadedEnforce = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedEnforce = MyAPIGateway.Utilities.SerializeFromBinary<EnforcementValues>(base64);
                if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Loaded {loadedEnforce != null} - Version:{loadedEnforce?.Version} - ShieldId [{shield.EntityId}]");
                if (loadedEnforce != null) return loadedEnforce;
            }
            return null;
        }

        public static void EnforcementRequest(long shieldId)
        {
            if (Session.Enforced.Debug == 2) Log.Line($"Client [{MyAPIGateway.Multiplayer.MyId}] requesting enforcement - current:\n{Session.Enforced}");
            Session.Enforced.SenderId = MyAPIGateway.Multiplayer.MyId;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataEnforce(shieldId, Session.Enforced));
            MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
        }
    }
}

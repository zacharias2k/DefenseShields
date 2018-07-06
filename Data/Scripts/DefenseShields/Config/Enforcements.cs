using DefenseShields.Support;
using Sandbox.ModAPI;

namespace DefenseShields
{
    internal class Enforcements
    {
        public static void UpdateEnforcement(DefenseShieldsEnforcement newEnforce)
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

        public static void EnforcementRequest(long shieldId)
        {
            if (Session.IsServer)
            {
                Log.Line($"I am the host, no one has power over me:\n{Session.Enforced}");
            }
            else
            {
                Log.Line($"Client [{MyAPIGateway.Multiplayer.MyId}] requesting enforcement - current:\n{Session.Enforced}");
                Session.Enforced.SenderId = MyAPIGateway.Multiplayer.MyId;
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new EnforceData(MyAPIGateway.Multiplayer.MyId, shieldId, Session.Enforced));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketIdEnforce, bytes);
            }
        }

    }
}

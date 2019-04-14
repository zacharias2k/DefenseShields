using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Bus
    {
        private void PlayerMessages(Controllers.PlayerNotice notice)
        {
            var a = ActiveController;
            var set = a.Set;
            double radius;
            if (notice == Controllers.PlayerNotice.EmpOverLoad || notice == Controllers.PlayerNotice.OverLoad) radius = 500;
            else radius = Field.ShieldSphere.Radius * 2;

            var center = Field.ShieldIsMobile ? Spine.PositionComp.WorldVolume.Center : Field.OffsetEmitterWMatrix.Translation;
            var sphere = new BoundingSphereD(center, radius);
            var sendMessage = false;
            IMyPlayer targetPlayer = null;

            foreach (var player in Session.Instance.Players.Values)
            {
                if (player.IdentityId != MyAPIGateway.Session.Player.IdentityId) continue;
                if (!sphere.Intersects(player.Character.WorldVolume)) continue;
                var relation = MyAPIGateway.Session.Player.GetRelationTo(a.MyCube.OwnerId);
                if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) continue;
                sendMessage = true;
                targetPlayer = player;
                break;
            }
            if (sendMessage && !set.Value.NoWarningSounds) BroadcastSound(targetPlayer.Character, notice);

            switch (notice)
            {
                case Controllers.PlayerNotice.EmitterInit:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- shield is reinitializing and checking LOS, attempting startup in 30 seconds!", 4816);
                    break;
                case Controllers.PlayerNotice.FieldBlocked:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + "-- the shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case Controllers.PlayerNotice.OverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case Controllers.PlayerNotice.EmpOverLoad:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- shield was EMPed, restarting in 60 seconds!!", 8000, "Red");
                    break;
                case Controllers.PlayerNotice.Remodulate:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- shield remodulating, restarting in 5 seconds.", 4800);
                    break;
                case Controllers.PlayerNotice.NoLos:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- Emitter does not have line of sight, shield offline", 8000, "Red");
                    break;
                case Controllers.PlayerNotice.NoPower:
                    if (sendMessage) MyAPIGateway.Utilities.ShowNotification("[ " + Spine.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }
            if (Session.Enforced.Debug == 3) Log.Line($"[PlayerMessages] Sending:{sendMessage} - rangeToClinetPlayer:{Vector3D.Distance(sphere.Center, MyAPIGateway.Session.Player.Character.WorldVolume.Center)}");
        }

        private static void BroadcastSound(IMyCharacter character, Controllers.PlayerNotice notice)
        {
            var soundEmitter = Session.Instance.AudioReady((MyEntity)character);
            if (soundEmitter == null) return;

            MySoundPair pair = null;
            switch (notice)
            {
                case Controllers.PlayerNotice.EmitterInit:
                    pair = new MySoundPair("Arc_reinitializing");
                    break;
                case Controllers.PlayerNotice.FieldBlocked:
                    pair = new MySoundPair("Arc_solidbody");
                    break;
                case Controllers.PlayerNotice.OverLoad:
                    pair = new MySoundPair("Arc_overloaded");
                    break;
                case Controllers.PlayerNotice.EmpOverLoad:
                    pair = new MySoundPair("Arc_EMP");
                    break;
                case Controllers.PlayerNotice.Remodulate:
                    pair = new MySoundPair("Arc_remodulating");
                    break;
                case Controllers.PlayerNotice.NoLos:
                    pair = new MySoundPair("Arc_noLOS");
                    break;
                case Controllers.PlayerNotice.NoPower:
                    pair = new MySoundPair("Arc_insufficientpower");
                    break;
            }
            if (soundEmitter.Entity != null && pair != null) soundEmitter.PlaySingleSound(pair, true);
        }

        internal void BroadcastMessage(bool forceNoPower = false)
        {
            var a = ActiveController;
            var state = a.State;
            if (Session.Enforced.Debug >= 3) Log.Line($"Broadcasting message to local playerId{Session.Instance.Players.Count} - Server:{_isServer} - Dedicated:{_isDedicated} - Id:{MyAPIGateway.Multiplayer.MyId}");

            if (!state.Value.EmitterLos && Field.ShieldIsMobile && !state.Value.Waking) PlayerMessages(Controllers.PlayerNotice.NoLos);
            else if (state.Value.NoPower || forceNoPower) PlayerMessages(Controllers.PlayerNotice.NoPower);
            else if (state.Value.Overload) PlayerMessages(Controllers.PlayerNotice.OverLoad);
            else if (state.Value.EmpOverLoad) PlayerMessages(Controllers.PlayerNotice.EmpOverLoad);
            else if (state.Value.FieldBlocked) PlayerMessages(Controllers.PlayerNotice.FieldBlocked);
            else if (state.Value.Waking) PlayerMessages(Controllers.PlayerNotice.EmitterInit);
            else if (state.Value.Remodulate) PlayerMessages(Controllers.PlayerNotice.Remodulate);
            state.Value.Message = false;
        }
    }
}

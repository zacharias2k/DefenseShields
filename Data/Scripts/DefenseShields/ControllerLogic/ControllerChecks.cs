using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace DefenseSystems
{
    public partial class Controllers
    {
        private void Debug()
        {
            var name = Controller.CustomName;
            var nameLen = name.Length;
            if (nameLen == 5 && name == "DEBUG")
            {
                if (Bus.Tick <= 1800) Controller.CustomName = "DEBUGAUTODISABLED";
                else UserDebug();
            }
        }

        private void UserDebug()
        {
            bool active;
            lock (Session.Instance.ActiveProtection) active = Session.Instance.ActiveProtection.Contains(this);
            var message = $"User({MyAPIGateway.Multiplayer.Players.TryGetSteamId(Controller.OwnerId)}) Debugging\n" +
                          $"On:{State.Value.Online} - Sus:{State.Value.Suspended} - Act:{active}\n" +
                          $"Sleep:{Asleep} - Tick/Woke:{Bus.Tick}/{LastWokenTick}\n" +
                          $"Mode:{State.Value.Mode} - Waking:{State.Value.Waking}\n" +
                          $"Low:{State.Value.Lowered} - Sl:{State.Value.Sleeping}\n" +
                          $"Failed:{!NotFailed} - PNull:{Bus.MyResourceDist == null}\n" +
                          $"NoP:{State.Value.NoPower} - PSys:{Bus.MyResourceDist?.SourcesEnabled}\n" +
                          $"Access:{State.Value.ControllerGridAccess} - EmitterLos:{State.Value.EmitterLos}\n" +
                          $"ProtectedEnts:{Bus.Field.ProtectedEntCache.Count} - ProtectMyGrid:{Session.Instance.GlobalProtect.ContainsKey(Bus.Spine)}\n" +
                          $"EmitterMode:{Bus.EmitterMode} - pFail:{Bus.Field.PowerFail}\n" +
                          $"Sink:{Sink.CurrentInputByType(GId)} - PFS:{Bus.Field.PowerNeeds}/{Bus.Field.FieldMaxPower}\n" +
                          $"AvailPoW:{Bus.Field.FieldAvailablePower} - MTPoW:{Bus.Field.ShieldMaintaintPower}\n" +
                          $"Pow:{SinkPower} HP:{State.Value.Charge}: {Bus.Field.ShieldMaxCharge}";

            if (!_isDedicated) MyAPIGateway.Utilities.ShowNotification(message, 28800);
            else Log.Line(message);
        }

        private static void CreativeModeWarning()
        {
            if (Session.Instance.CreativeWarn || Session.Instance.Tick < 600) return;
            Session.Instance.CreativeWarn = true;
            const string message = "DefenseSystems is not fully supported in\n" +
                                   "Creative Mode, due to unlimited power and \n" +
                                   "it will not operate as designed.\n";
            MyAPIGateway.Utilities.ShowNotification(message, 6720);
        }

        private void GridOwnsController()
        {
            if (Bus.Spine.BigOwners.Count == 0)
            {
                State.Value.ControllerGridAccess = false;
                return;
            }

            _gridOwnerId = Bus.Spine.BigOwners[0];
            _controllerOwnerId = MyCube.OwnerId;

            if (_controllerOwnerId == 0) MyCube.ChangeOwner(_gridOwnerId, MyOwnershipShareModeEnum.Faction);

            var controlToGridRelataion = MyCube.GetUserRelationToOwner(_gridOwnerId);
            State.Value.InFaction = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.FactionShare;
            State.Value.IsOwner = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.Owner;

            if (controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.Owner && controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.FactionShare)
            {
                if (State.Value.ControllerGridAccess)
                {
                    State.Value.ControllerGridAccess = false;
                    Controller.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is not owned: {Bus.EmitterMode} - ControllerId [{Controller.EntityId}]");
                }
                State.Value.ControllerGridAccess = false;
                return;
            }

            if (!State.Value.ControllerGridAccess)
            {
                State.Value.ControllerGridAccess = true;
                Controller.RefreshCustomInfo();
                if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is owned: {Bus.EmitterMode} - ControllerId [{Controller.EntityId}]");
            }
            State.Value.ControllerGridAccess = true;
        }

        public void ProtectSubs(uint tick)
        {
            foreach (var sub in Bus.SubGrids)
            {
                MyProtectors protectors;
                Session.Instance.GlobalProtect.TryGetValue(sub, out protectors);

                if (protectors == null)
                {
                    protectors = Session.Instance.GlobalProtect[sub] = Session.ProtSets.Get();
                    protectors.Init(LogicSlot, tick);
                }
                protectors.NotBubble = this;
            }
        }
    }
}

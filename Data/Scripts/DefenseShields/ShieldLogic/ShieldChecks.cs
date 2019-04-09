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
            var name = Shield.CustomName;
            var nameLen = name.Length;
            if (nameLen == 5 && name == "DEBUG")
            {
                if (_tick <= 1800) Shield.CustomName = "DEBUGAUTODISABLED";
                else UserDebug();
            }
        }

        private void UserDebug()
        {
            bool active;
            lock (Session.Instance.ActiveShields) active = Session.Instance.ActiveShields.Contains(this);
            var message = $"User({MyAPIGateway.Multiplayer.Players.TryGetSteamId(Shield.OwnerId)}) Debugging\n" +
                          $"On:{DsState.State.Online} - Sus:{DsState.State.Suspended} - Act:{active}\n" +
                          $"Sleep:{Asleep} - Tick/Woke:{_tick}/{LastWokenTick}\n" +
                          $"Mode:{DsState.State.Mode} - Waking:{DsState.State.Waking}\n" +
                          $"Low:{DsState.State.Lowered} - Sl:{DsState.State.Sleeping}\n" +
                          $"Failed:{!NotFailed} - PNull:{Bus.MyResourceDist == null}\n" +
                          $"NoP:{DsState.State.NoPower} - PSys:{Bus.MyResourceDist?.SourcesEnabled}\n" +
                          $"Access:{DsState.State.ControllerGridAccess} - EmitterLos:{DsState.State.EmitterLos}\n" +
                          $"ProtectedEnts:{ProtectedEntCache.Count} - ProtectMyGrid:{Session.Instance.GlobalProtect.ContainsKey(Bus.Spine)}\n" +
                          $"ShieldMode:{ShieldMode} - pFail:{_powerFail}\n" +
                          $"Sink:{_sink.CurrentInputByType(GId)} - PFS:{_powerNeeded}/{Bus.ShieldMaxPower}\n" +
                          $"AvailPoW:{Bus.ShieldAvailablePower} - MTPoW:{_shieldMaintaintPower}\n" +
                          $"Pow:{_power} HP:{DsState.State.Charge}: {ShieldMaxCharge}";

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
                DsState.State.ControllerGridAccess = false;
                return;
            }

            _gridOwnerId = Bus.Spine.BigOwners[0];
            _controllerOwnerId = MyCube.OwnerId;

            if (_controllerOwnerId == 0) MyCube.ChangeOwner(_gridOwnerId, MyOwnershipShareModeEnum.Faction);

            var controlToGridRelataion = MyCube.GetUserRelationToOwner(_gridOwnerId);
            DsState.State.InFaction = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.FactionShare;
            DsState.State.IsOwner = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.Owner;

            if (controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.Owner && controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.FactionShare)
            {
                if (DsState.State.ControllerGridAccess)
                {
                    DsState.State.ControllerGridAccess = false;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is not owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.ControllerGridAccess = false;
                return;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            DsState.State.ControllerGridAccess = true;
        }


        private bool FieldShapeBlocked()
        {
            if (Bus.ActiveModulator == null || Bus.ActiveModulator.ModSet.Settings.ModulateVoxels || Session.Enforced.DisableVoxelSupport == 1) return false;

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, Bus.PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(Bus.PhysicsOutsideLow, voxel)) continue;

                Shield.Enabled = false;
                DsState.State.FieldBlocked = true;
                DsState.State.Message = true;
                if (Session.Enforced.Debug == 3) Log.Line($"Field blocked: - ShieldId [{Shield.EntityId}]");
                return true;
            }
            DsState.State.FieldBlocked = false;
            return false;
        }

        private void FailureDurations()
        {
            if (_overLoadLoop == 0 || _empOverLoadLoop == 0 || _reModulationLoop == 0)
            {
                if (DsState.State.Online || !WarmedUp)
                {
                    if (_overLoadLoop != -1)
                    {
                        DsState.State.Overload = true;
                        DsState.State.Message = true;
                    }

                    if (_empOverLoadLoop != -1)
                    {
                        DsState.State.EmpOverLoad = true;
                        DsState.State.Message = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        DsState.State.Remodulate = true;
                        DsState.State.Message = true;
                    }
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    DsState.State.Remodulate = false;
                    _reModulationLoop = -1;
                }
            }

            if (_overLoadLoop > -1)
            {
                _overLoadLoop++;
                if (_overLoadLoop == ShieldDownCount - 1) Bus.CheckEmitters = true;
                if (_overLoadLoop == ShieldDownCount)
                {
                    if (!DsState.State.EmitterLos)
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                        var recharged = ShieldChargeRate * ShieldDownCount / 60;
                        DsState.State.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.10f, ShieldMaxCharge * 0.25f);
                    }
                }
            }

            if (_empOverLoadLoop > -1)
            {
                _empOverLoadLoop++;
                if (_empOverLoadLoop == EmpDownCount - 1) Bus.CheckEmitters = true;
                if (_empOverLoadLoop == EmpDownCount)
                {
                    if (!DsState.State.EmitterLos)
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                        _empOverLoad = false;
                        var recharged = ShieldChargeRate * EmpDownCount / 60;
                        DsState.State.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.25f, ShieldMaxCharge * 0.62f);
                    }
                }
            }
        }
    }
}

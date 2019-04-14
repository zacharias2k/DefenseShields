using System;
using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    internal class TapiBackend
    {
        private readonly Dictionary<string, Delegate> _terminalModApiMethods = new Dictionary<string, Delegate>()
        {
            ["RayAttackShield"] = new Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?>(TAPI_RayAttackShield),
            ["PointAttackShield"] = new Func<IMyTerminalBlock, Vector3D, long, float, bool, bool>(TAPI_PointAttackShield),
            ["SetShieldHeat"] = new Action<IMyTerminalBlock, int>(TAPI_SetShieldHeat),
            ["OverLoadShield"] = new Action<IMyTerminalBlock>(TAPI_OverLoadShield),
            ["SetCharge"] = new Action<IMyTerminalBlock, float>(TAPI_SetCharge),
            ["RayIntersectShield"] = new Func<IMyTerminalBlock, RayD, Vector3D?>(TAPI_RayIntersectShield),
            ["PointInShield"] = new Func<IMyTerminalBlock, Vector3D, bool>(TAPI_PointInShield),
            ["GetShieldPercent"] = new Func<IMyTerminalBlock, float>(TAPI_GetShieldPercent),
            ["GetShieldHeat"] = new Func<IMyTerminalBlock, int>(TAPI_GetShieldHeatLevel),
            ["GetChargeRate"] = new Func<IMyTerminalBlock, float>(TAPI_GetChargeRate),
            ["HpToChargeRatio"] = new Func<IMyTerminalBlock, int>(TAPI_HpToChargeRatio),
            ["GetMaxCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxCharge),
            ["GetCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetCharge),
            ["GetPowerUsed"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerUsed),
            ["GetPowerCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerCap),
            ["GetMaxHpCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxHpCap),
            ["IsShieldUp"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldUp),
            ["ShieldStatus"] = new Func<IMyTerminalBlock, string>(TAPI_ShieldStatus),
            ["EntityBypass"] = new Func<IMyTerminalBlock, IMyEntity, bool, bool>(TAPI_EntityBypass),
            ["GridHasShield"] = new Func<IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<IMyEntity, IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["IsShieldBlock"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
            ["GetClosestShield"] = new Func<Vector3D, IMyTerminalBlock>(TAPI_GetClosestShield),
            ["GetDistanceToShield"] = new Func<IMyTerminalBlock, Vector3D, double>(TAPI_GetDistanceToShield),
            ["GetClosestShieldPoint"] = new Func<IMyTerminalBlock, Vector3D, Vector3D?>(TAPI_GetClosestShieldPoint),

        };

        private readonly Dictionary<string, Delegate> _terminalPbApiMethods = new Dictionary<string, Delegate>()
        {
            ["RayIntersectShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, RayD, Vector3D?>(TAPI_RayIntersectShield),
            ["PointInShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, bool>(TAPI_PointInShield),
            ["GetShieldPercent"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetShieldPercent),
            ["GetShieldHeat"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(TAPI_GetShieldHeatLevel),
            ["GetChargeRate"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetChargeRate),
            ["HpToChargeRatio"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(TAPI_HpToChargeRatio),
            ["GetMaxCharge"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetMaxCharge),
            ["GetCharge"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetCharge),
            ["GetPowerUsed"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetPowerUsed),
            ["GetPowerCap"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetPowerCap),
            ["GetMaxHpCap"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetMaxHpCap),
            ["IsShieldUp"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(TAPI_IsShieldUp),
            ["ShieldStatus"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, string>(TAPI_ShieldStatus),
            ["EntityBypass"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, bool, bool>(TAPI_EntityBypass),
            ["GridHasShield"] = new Func<VRage.Game.ModAPI.Ingame.IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<VRage.Game.ModAPI.Ingame.IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<VRage.Game.ModAPI.Ingame.IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<VRage.Game.ModAPI.Ingame.IMyEntity, Sandbox.ModAPI.Ingame.IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["IsShieldBlock"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
            ["GetClosestShield"] = new Func<Vector3D, Sandbox.ModAPI.Ingame.IMyTerminalBlock>(TAPI_GetClosestShield),
            ["GetDistanceToShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, double>(TAPI_GetDistanceToShield),
            ["GetClosestShieldPoint"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, Vector3D?>(TAPI_GetClosestShieldPoint),
        };

        internal void Init()
        {
            var mod = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("DefenseSystemsAPI");
            mod.Getter = (b) => _terminalModApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(mod);
            MyAPIGateway.TerminalControls.RemoveControl<IMyProgrammableBlock>(mod);

            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("DefenseSystemsPbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        // ModApi only methods below
        private static Vector3D? TAPI_RayAttackShield(IMyTerminalBlock block, RayD ray, long attackerId, float damage, bool energy = false)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return null;
            var a = b.ActiveController;
            var f = b.Field;

            var intersectDist = CustomCollision.IntersectEllipsoid(b.Field.DetectMatrixOutsideInv, b.Field.DetectMatrixOutside, ray);
            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            var hitPos = ray.Position + (ray.Direction * -ellipsoid);

            if (energy) damage *= a.State.Value.ModulateKinetic;
            else damage *= a.State.Value.ModulateEnergy;

            if (Session.Instance.MpActive)
            {
                var damageType = energy ? Session.Instance.MPEnergy : Session.Instance.MPKinetic;
                f.AddShieldHit(attackerId, damage, damageType, null, true, hitPos);
            }
            else
            {
                b.Field.ImpactSize = damage;
                f.WorldImpactPosition = hitPos;
            }
            f.WebDamage = true;
            f.Absorb += damage;

            return hitPos;
        }

        private static bool TAPI_PointAttackShield(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, bool energy = false)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return false;
            var a = b.ActiveController;
            var f = b.Field;

            var hit = CustomCollision.PointInShield(pos, f.DetectMatrixOutsideInv);
            if (!hit) return false;

            if (energy) damage *= a.State.Value.ModulateKinetic;
            else damage *= a.State.Value.ModulateEnergy;

            if (Session.Instance.MpActive)
            {
                var damageType = energy ? Session.Instance.MPEnergy : Session.Instance.MPKinetic;
                f.AddShieldHit(attackerId, damage, damageType, null, true, pos);
            }
            else
            {
                f.ImpactSize = damage;
                f.WorldImpactPosition = pos;
            }

            f.EnergyHit = energy;
            f.WebDamage = true;
            f.Absorb += damage;

            return true;
        }

        private static void TAPI_SetShieldHeat(IMyTerminalBlock block, int value)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return;
            var a = b.ActiveController;
            var f = b.Field;

            a.State.Value.Heat = value;
        }

        private static void TAPI_OverLoadShield(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return;
            var a = b.ActiveController;
            var f = b.Field;

            a.State.Value.Charge = -(f.ShieldMaxCharge * 2);
        }


        private static void TAPI_SetCharge(IMyTerminalBlock block, float value)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return;
            var a = b.ActiveController;
            var f = b.Field;

            a.State.Value.Charge = value;
        }

        // ModApi and PB methods below.
        private static Vector3D? TAPI_RayIntersectShield(IMyTerminalBlock block, RayD ray)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return null;
            var a = b.ActiveController;
            var f = b.Field;

            var intersectDist = CustomCollision.IntersectEllipsoid(f.DetectMatrixOutsideInv, f.DetectMatrixOutside, ray);
            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            return ray.Position + (ray.Direction * -ellipsoid);
        }

        private static bool TAPI_PointInShield(IMyTerminalBlock block, Vector3D pos)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return false;
            var a = b.ActiveController;
            var f = b.Field;

            return CustomCollision.PointInShield(pos, f.DetectMatrixOutsideInv);
        }

        private static float TAPI_GetShieldPercent(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return a.State.Value.ShieldPercent;
        }

        private static int TAPI_GetShieldHeatLevel(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return a.State.Value.Heat;
        }

        private static int TAPI_HpToChargeRatio(IMyTerminalBlock block)
        {
            return Fields.ConvToHp;
        }

        private static float TAPI_GetChargeRate(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return f.ShieldChargeRate * Fields.ConvToDec;
        }

        private static float TAPI_GetMaxCharge(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return f.ShieldMaxCharge;
        }

        private static float TAPI_GetCharge(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return a.State.Value.Charge;
        }

        private static float TAPI_GetPowerUsed(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return a.SinkPower;
        }

        private static float TAPI_GetPowerCap(IMyTerminalBlock block)
        {
            return float.MinValue;
        }

        private static float TAPI_GetMaxHpCap(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return f.ShieldHpBase * Fields.ConvToDec;
        }

        private static bool TAPI_IsShieldUp(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return false;
            var a = b.ActiveController;
            var f = b.Field;

            return a.State.Value.Online;
        }

        private static string TAPI_ShieldStatus(IMyTerminalBlock block)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return string.Empty;
            var a = b.ActiveController;
            var f = b.Field;

            return a.GetShieldStatus();
        }

        private static bool TAPI_EntityBypass(IMyTerminalBlock block, IMyEntity entity, bool remove)
        {
            var ent = (MyEntity)entity;
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null || ent == null) return false;
            var a = b.ActiveController;
            var f = b.Field;

            var success = remove ? f.EntityBypass.Remove(ent) : f.EntityBypass.Add(ent);

            return success;
        }

        private static bool TAPI_GridHasShield(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;

            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors))
            {
                foreach (var s in protectors.Controllers)
                {
                    if (s.Bus.SubGrids.Contains(myGrid)) return true;
                }
            }
            return false;
        }

        private static bool TAPI_GridShieldOnline(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;
            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors))
            {
                foreach (var s in protectors.Controllers)
                {
                    if (s.Bus.SubGrids.Contains(myGrid) && s.State.Value.Online) return true;
                }
            }
            return false;
        }

        private static bool TAPI_ProtectedByShield(IMyEntity entity)
        {
            if (entity == null) return false;

            MyProtectors protectors;
            var ent = (MyEntity)entity;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                foreach (var s in protectors.Controllers)
                {
                    if (s.State.Value.Online) return true;
                }
            }
            return false;
        }

        private static IMyTerminalBlock TAPI_GetShieldBlock(IMyEntity entity)
        {
            if (entity == null) return null;

            MyProtectors protectors;
            var ent = (MyEntity) entity;
            var grid = ent as MyCubeGrid;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                Controllers firstShield = null;
                foreach (var s in protectors.Controllers)
                {
                    if (firstShield == null) firstShield = s;
                    if (s.Bus.SubGrids.Contains(grid)) return s.MyCube as IMyTerminalBlock;
                }
                if (firstShield != null) return firstShield.MyCube as IMyTerminalBlock;
            }
            return null;
        }

        private static bool TAPI_IsShieldBlock(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<Controllers>();
            return logic != null;
        }

        private static IMyTerminalBlock TAPI_GetClosestShield(Vector3D pos)
        {
            MyCubeBlock cloestSBlock = null;
            var closestDist = double.MaxValue;
            lock (Session.Instance.ActiveProtection)
            {
                foreach (var s in Session.Instance.ActiveProtection)
                {
                    if (s.Bus?.Field == null) continue;
                    if (Vector3D.DistanceSquared(s.Bus.Field.DetectionCenter, pos) > Session.Instance.SyncDistSqr) continue;

                    var sDist = CustomCollision.EllipsoidDistanceToPos(s.Bus.Field.DetectMatrixOutsideInv, s.Bus.Field.DetectMatrixOutside, pos);
                    if (sDist > 0 && sDist < closestDist)
                    {
                        cloestSBlock = s.MyCube;
                        closestDist = sDist;
                    }
                }
            }
            return cloestSBlock as IMyTerminalBlock;
        }

        private static double TAPI_GetDistanceToShield(IMyTerminalBlock block, Vector3D pos)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return -1;
            var a = b.ActiveController;
            var f = b.Field;

            return CustomCollision.EllipsoidDistanceToPos(f.DetectMatrixOutsideInv, f.DetectMatrixOutside, pos);
        }

        private static Vector3D? TAPI_GetClosestShieldPoint(IMyTerminalBlock block, Vector3D pos)
        {
            var b = block?.GameLogic?.GetAs<Controllers>()?.Bus;
            if (b?.Field == null || b.ActiveController == null) return null;
            var a = b.ActiveController;
            var f = b.Field;

            return CustomCollision.ClosestEllipsoidPointToPos(f.DetectMatrixOutsideInv, f.DetectMatrixOutside, pos);
        }

        // PB overloads
        private static Vector3D? TAPI_RayIntersectShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, RayD arg2) => TAPI_RayIntersectShield(arg1 as IMyTerminalBlock, arg2);
        private static bool TAPI_PointInShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_PointInShield(arg1 as IMyTerminalBlock, arg2);
        private static float TAPI_GetShieldPercent(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetShieldPercent(arg as IMyTerminalBlock);
        private static int TAPI_GetShieldHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetShieldHeatLevel(arg as IMyTerminalBlock);
        private static float TAPI_GetChargeRate(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetChargeRate(arg as IMyTerminalBlock);
        private static int TAPI_HpToChargeRatio(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_HpToChargeRatio(arg as IMyTerminalBlock);
        private static float TAPI_GetMaxCharge(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetMaxCharge(arg as IMyTerminalBlock);
        private static float TAPI_GetCharge(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetCharge(arg as IMyTerminalBlock);
        private static float TAPI_GetPowerUsed(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetPowerUsed(arg as IMyTerminalBlock);
        private static float TAPI_GetPowerCap(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetPowerCap(arg as IMyTerminalBlock);
        private static bool TAPI_IsShieldBlock(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_IsShieldBlock(arg as IMyTerminalBlock);
        private static float TAPI_GetMaxHpCap(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetMaxHpCap(arg as IMyTerminalBlock);
        private static string TAPI_ShieldStatus(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_ShieldStatus(arg as IMyTerminalBlock);
        private static bool TAPI_EntityBypass(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, VRage.Game.ModAPI.Ingame.IMyEntity arg2, bool arg3) =>TAPI_EntityBypass(arg1 as IMyTerminalBlock, arg2 as IMyEntity, arg3);
        private static bool TAPI_GridHasShield(VRage.Game.ModAPI.Ingame.IMyCubeGrid arg) => TAPI_GridHasShield(arg as IMyCubeGrid);
        private static bool TAPI_GridShieldOnline(VRage.Game.ModAPI.Ingame.IMyCubeGrid arg) => TAPI_GridShieldOnline(arg as IMyCubeGrid);
        private static bool TAPI_ProtectedByShield(VRage.Game.ModAPI.Ingame.IMyEntity arg) => TAPI_ProtectedByShield(arg as IMyEntity);
        private static bool TAPI_IsShieldUp(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_IsShieldUp(arg as IMyTerminalBlock);
        private static Sandbox.ModAPI.Ingame.IMyTerminalBlock TAPI_GetShieldBlock(VRage.Game.ModAPI.Ingame.IMyEntity arg) => TAPI_GetShieldBlock(arg as IMyEntity);
        private static double TAPI_GetDistanceToShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_GetDistanceToShield(arg1 as IMyTerminalBlock, arg2);
        private static Vector3D? TAPI_GetClosestShieldPoint(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_GetClosestShieldPoint(arg1 as IMyTerminalBlock, arg2);

    }
}

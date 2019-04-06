using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
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
            ["GridHasShield"] = new Func<IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<IMyEntity, IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["IsShieldBlock"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
        };

        private readonly Dictionary<string, Delegate> _terminalPbApiMethods = new Dictionary<string, Delegate>()
        {
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
            ["GridHasShield"] = new Func<IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<IMyEntity, IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["IsShieldBlock"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
        };

        internal void Init()
        {
            var mod = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("DefenseSystemsAPI");
            mod.Getter = (b) => _terminalModApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(mod);

            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyProgrammableBlock>("DefenseSystemsPbAPI");
            pb.Getter = (b) => _terminalPbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(pb);
        }

        // ModApi only methods below
        private static Vector3D? TAPI_RayAttackShield(IMyTerminalBlock block, RayD ray, long attackerId, float damage, bool energy = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            var intersectDist = CustomCollision.IntersectEllipsoid(logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ray);
            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            var hitPos = ray.Position + (ray.Direction * -ellipsoid);

            if (energy) damage *= logic.DsState.State.ModulateKinetic;
            else damage *= logic.DsState.State.ModulateEnergy;

            if (Session.Instance.MpActive)
            {
                var damageType = energy ? Session.Instance.MPEnergy : Session.Instance.MPKinetic;
                logic.AddShieldHit(attackerId, damage, damageType, null, true, hitPos);
            }
            else
            {
                logic.ImpactSize = damage;
                logic.WorldImpactPosition = hitPos;
            }
            logic.WebDamage = true;
            logic.Absorb += damage;

            return hitPos;
        }

        private static bool TAPI_PointAttackShield(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, bool energy = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;
            var hit = CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv);
            if (!hit) return false;

            if (energy) damage *= logic.DsState.State.ModulateKinetic;
            else damage *= logic.DsState.State.ModulateEnergy;

            if (Session.Instance.MpActive)
            {
                var damageType = energy ? Session.Instance.MPEnergy : Session.Instance.MPKinetic;
                logic.AddShieldHit(attackerId, damage, damageType, null, true, pos);
            }
            else
            {
                logic.ImpactSize = damage;
                logic.WorldImpactPosition = pos;
            }

            logic.EnergyHit = energy;
            logic.WebDamage = true;
            logic.Absorb += damage;

            return true;
        }

        private static void TAPI_SetShieldHeat(IMyTerminalBlock block, int value)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return;

            logic.DsState.State.Heat = value;
        }

        private static void TAPI_OverLoadShield(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return;

            logic.DsState.State.Charge = -(logic.ShieldMaxCharge * 2);
        }


        private static void TAPI_SetCharge(IMyTerminalBlock block, float value)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return;

            logic.DsState.State.Charge = value;
        }

        // ModApi and PB methods below.
        private static Vector3D? TAPI_RayIntersectShield(IMyTerminalBlock block, RayD ray)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            var intersectDist = CustomCollision.IntersectEllipsoid(logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ray);
            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            return ray.Position + (ray.Direction * -ellipsoid);
        }

        private static bool TAPI_PointInShield(IMyTerminalBlock block, Vector3D pos)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            return logic != null && CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv);
        }

        private static float TAPI_GetShieldPercent(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.ShieldPercent;
        }

        private static int TAPI_GetShieldHeatLevel(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.Heat;
        }

        private static int TAPI_HpToChargeRatio(IMyTerminalBlock block)
        {
            return DefenseShields.ConvToHp;
        }

        private static float TAPI_GetChargeRate(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsSet.Settings.Rate * DefenseShields.ConvToDec;
        }

        private static float TAPI_GetMaxCharge(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldMaxCharge;
        }

        private static float TAPI_GetCharge(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.Charge;
        }

        private static float TAPI_GetPowerUsed(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldCurrentPower;
        }

        private static float TAPI_GetPowerCap(IMyTerminalBlock block)
        {
            return float.MinValue;
        }

        private static float TAPI_GetMaxHpCap(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.GridIntegrity * DefenseShields.ConvToDec;
        }

        private static bool TAPI_IsShieldUp(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;

            return logic.DsState.State.Online;
        }

        private static string TAPI_ShieldStatus(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return string.Empty;

            return logic.GetShieldStatus();
        }

        private static bool TAPI_GridHasShield(IMyCubeGrid grid)
        {
            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;
            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors))
            {
                foreach (var s in protectors.Shields)
                {
                    if (s.ShieldComp.SubGrids.Contains(myGrid)) return true;
                }
            }
            return false;
        }

        private static bool TAPI_GridShieldOnline(IMyCubeGrid grid)
        {
            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;
            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors))
            {
                foreach (var s in protectors.Shields)
                {
                    if (s.ShieldComp.SubGrids.Contains(myGrid) && s.DsState.State.Online) return true;
                }
            }
            return false;
        }

        private static bool TAPI_ProtectedByShield(IMyEntity entity)
        {
            MyProtectors protectors;
            var ent = (MyEntity)entity;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                foreach (var s in protectors.Shields)
                {
                    if (s.DsState.State.Online) return true;
                }
            }
            return false;
        }

        private static IMyTerminalBlock TAPI_GetShieldBlock(IMyEntity entity)
        {
            MyProtectors protectors;
            var ent = (MyEntity) entity;
            var grid = ent as MyCubeGrid;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                DefenseShields firstShield = null;
                foreach (var s in protectors.Shields)
                {
                    if (firstShield == null) firstShield = s;
                    if (s.ShieldComp.SubGrids.Contains(grid)) return s.MyCube as IMyTerminalBlock;
                }
                if (firstShield != null) return firstShield.MyCube as IMyTerminalBlock;
            }
            return null;
        }

        private static bool TAPI_IsShieldBlock(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>();
            return logic != null;
        }
    }
}

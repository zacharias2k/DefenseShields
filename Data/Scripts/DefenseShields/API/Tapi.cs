using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

namespace DefenseShields
{
    internal class TapiFrontend
    {
        private readonly Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?> _rayAttackShield;
        private readonly Func<IMyTerminalBlock, Vector3D, long, float, bool, bool> _pointAttackShield;
        private readonly Func<IMyTerminalBlock, RayD, Vector3D?> _rayIntersectShield;
        private readonly Func<IMyTerminalBlock, Vector3D, bool> _pointInShield;
        private readonly Func<IMyTerminalBlock, float> _getShieldPercent;
        private readonly Func<IMyTerminalBlock, int> _getShieldHeat;
        private readonly Action<IMyTerminalBlock, int> _setShieldHeat;
        private readonly Action<IMyTerminalBlock> _overLoad;
        private readonly Func<IMyTerminalBlock, float> _getChargeRate;
        private readonly Func<IMyTerminalBlock, int> _hpToChargeRatio;
        private readonly Func<IMyTerminalBlock, float> _getMaxCharge;
        private readonly Func<IMyTerminalBlock, float> _getCharge;
        private readonly Action<IMyTerminalBlock, float> _setCharge;
        private readonly Func<IMyTerminalBlock, float> _getPowerUsed;
        private readonly Func<IMyTerminalBlock, float> _getPowerCap;
        private readonly Func<IMyTerminalBlock, float> _getMaxHpCap;
        private readonly Func<IMyTerminalBlock, bool> _isShieldUp;
        private readonly Func<IMyTerminalBlock, string> _shieldStatus;

        private readonly IMyTerminalBlock _block;
        private readonly bool _apiLive;

        public TapiFrontend(IMyTerminalBlock block)
        {
            _block = block;
            var delegates = _block.GetProperty("DefenseSystemsAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
            if (delegates == null) return;
            _apiLive = true;
            _rayAttackShield = (Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?>)delegates["RayAttackShield"];
            _pointAttackShield = (Func<IMyTerminalBlock, Vector3D, long, float, bool, bool>)delegates["PointAttackShield"];
            _rayIntersectShield = (Func<IMyTerminalBlock, RayD, Vector3D?>)delegates["RayIntersectShield"];
            _pointInShield = (Func<IMyTerminalBlock, Vector3D, bool>)delegates["PointInShield"];
            _getShieldPercent = (Func<IMyTerminalBlock, float>)delegates["GetShieldPercent"];
            _getShieldHeat = (Func<IMyTerminalBlock, int>)delegates["GetShieldHeat"];
            _setShieldHeat = (Action<IMyTerminalBlock, int>)delegates["SetShieldHeat"];
            _overLoad = (Action<IMyTerminalBlock>)delegates["OverLoadShield"];
            _getChargeRate = (Func<IMyTerminalBlock, float>)delegates["GetChargeRate"];
            _hpToChargeRatio = (Func<IMyTerminalBlock, int>)delegates["HpToChargeRatio"];
            _getMaxCharge = (Func<IMyTerminalBlock, float>)delegates["GetMaxCharge"];
            _getCharge = (Func<IMyTerminalBlock, float>)delegates["GetCharge"];
            _setCharge = (Action<IMyTerminalBlock, float>)delegates["SetCharge"];
            _getPowerUsed = (Func<IMyTerminalBlock, float>)delegates["GetPowerUsed"];
            _getPowerCap = (Func<IMyTerminalBlock, float>)delegates["GetPowerCap"];
            _getMaxHpCap = (Func<IMyTerminalBlock, float>)delegates["GetMaxHpCap"];
            _isShieldUp = (Func<IMyTerminalBlock, bool>)delegates["IsShieldUp"];
            _shieldStatus = (Func<IMyTerminalBlock, string>)delegates["ShieldStatus"];
        }

        public Vector3D? RayAttackShield(RayD ray, long attackerId, float damage, bool energy = false)
        {
            return !_apiLive ? null : _rayAttackShield.Invoke(_block, ray, attackerId, damage, energy);
        }

        public bool PointAttackShield(Vector3D pos, long attackerId, float damage, bool energy = false)
        {
            return _apiLive && _pointAttackShield.Invoke(_block, pos, attackerId, damage, energy);
        }

        public Vector3D? RayIntersectShield(RayD ray)
        {
            return !_apiLive ? null : _rayIntersectShield.Invoke(_block, ray);
        }

        public bool PointInShield(Vector3D pos)
        {
            return _apiLive && _pointInShield.Invoke(_block, pos);
        }

        public float GetShieldPercent()
        {
            return !_apiLive ? -1 : _getShieldPercent.Invoke(_block);
        }

        public float GetShieldHeat()
        {
            return !_apiLive ? -1 : _getShieldHeat.Invoke(_block);
        }

        public void SetShieldHeat(int value)
        {
            _setShieldHeat.Invoke(_block, value);
        }

        public void OverLoadShield()
        {
            _overLoad.Invoke(_block);
        }

        public float GetChargeRate()
        {
            return !_apiLive ? -1 : _getChargeRate.Invoke(_block);
        }

        public float HpToChargeRatio()
        {
            return !_apiLive ? -1 : _hpToChargeRatio.Invoke(_block);
        }

        public float GetMaxCharge()
        {
            return !_apiLive ? -1 : _getMaxCharge.Invoke(_block);
        }

        public float GetCharge()
        {
            return !_apiLive ? -1 : _getCharge.Invoke(_block);
        }

        public void SetCharge(float value)
        {
            _setCharge.Invoke(_block, value);
        }

        public float GetPowerUsed()
        {
            return !_apiLive ? -1 : _getPowerUsed.Invoke(_block);
        }

        public float GetPowerCap()
        {
            return !_apiLive ? -1 : _getPowerCap.Invoke(_block);
        }

        public float GetMaxHpCap()
        {
            return !_apiLive ? -1 : _getMaxHpCap.Invoke(_block);
        }

        public bool IsShieldUp()
        {
            return _apiLive && _isShieldUp.Invoke(_block);
        }

        public string ShieldStatus()
        {
            return !_apiLive ? string.Empty : _shieldStatus.Invoke(_block);
        }
    }

    internal class TapiBackend
    {
        private readonly Dictionary<string, Delegate> _terminalApiMethods = new Dictionary<string, Delegate>()
        {
            ["RayAttackShield"] = new Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?>(TAPI_RayAttackShield),
            ["PointAttackShield"] = new Func<IMyTerminalBlock, Vector3D, long, float, bool, bool>(TAPI_PointAttackShield),
            ["RayIntersectShield"] = new Func<IMyTerminalBlock, RayD, Vector3D?>(TAPI_RayIntersectShield),
            ["PointInShield"] = new Func<IMyTerminalBlock, Vector3D, bool>(TAPI_PointInShield),
            ["GetShieldPercent"] = new Func<IMyTerminalBlock, float>(TAPI_GetShieldPercent),
            ["GetShieldHeat"] = new Func<IMyTerminalBlock, int>(TAPI_GetShieldHeatLevel),
            ["SetShieldHeat"] = new Action<IMyTerminalBlock, int>(TAPI_SetShieldHeat),
            ["OverLoadShield"] = new Action<IMyTerminalBlock>(TAPI_OverLoadShield),
            ["GetChargeRate"] = new Func<IMyTerminalBlock, float>(TAPI_GetChargeRate),
            ["HpToChargeRatio"] = new Func<IMyTerminalBlock, int>(TAPI_HpToChargeRatio),
            ["GetMaxCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxCharge),
            ["GetCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetCharge),
            ["SetCharge"] = new Action<IMyTerminalBlock, float>(TAPI_SetCharge),
            ["GetPowerUsed"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerUsed),
            ["GetPowerCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerCap),
            ["GetMaxHpCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxHpCap),
            ["IsShieldUp"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldUp),
            ["ShieldStatus"] = new Func<IMyTerminalBlock, string>(TAPI_ShieldStatus),
        };

        internal void Init()
        {
            var c = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyUpgradeModule>("DefenseSystemsAPI");
            c.Getter = (b) => _terminalApiMethods;
            //c.Setter = (b, v) => { };
            MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(c);
        }

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

        private static void TAPI_SetCharge(IMyTerminalBlock block, float value)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return;

            logic.DsState.State.Charge = value;
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
    }
}

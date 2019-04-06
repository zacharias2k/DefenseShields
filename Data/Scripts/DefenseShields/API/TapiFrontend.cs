using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    internal class TapiFrontend
    {
        private IMyTerminalBlock _block;

        // ModApi only methods below
        private readonly IMySession _fullApi = MyAPIGateway.Session; // ModAPI only, unlocks all methods
        private readonly Func<IMyTerminalBlock, IMySession, RayD, long, float, bool, Vector3D?> _rayAttackShield; // negative damage values heal
        private readonly Func<IMyTerminalBlock, IMySession, Vector3D, long, float, bool, bool> _pointAttackShield; // negative damage values heal
        private readonly Action<IMyTerminalBlock, IMySession, int> _setShieldHeat;
        private readonly Action<IMyTerminalBlock, IMySession> _overLoad;
        private readonly Action<IMyTerminalBlock, IMySession, float> _setCharge;
        // ModApi and PB methods below.
        private readonly Func<IMyTerminalBlock, RayD, Vector3D?> _rayIntersectShield;
        private readonly Func<IMyTerminalBlock, Vector3D, bool> _pointInShield;
        private readonly Func<IMyTerminalBlock, float> _getShieldPercent;
        private readonly Func<IMyTerminalBlock, int> _getShieldHeat;
        private readonly Func<IMyTerminalBlock, float> _getChargeRate;
        private readonly Func<IMyTerminalBlock, int> _hpToChargeRatio;
        private readonly Func<IMyTerminalBlock, float> _getMaxCharge;
        private readonly Func<IMyTerminalBlock, float> _getCharge;
        private readonly Func<IMyTerminalBlock, float> _getPowerUsed;
        private readonly Func<IMyTerminalBlock, float> _getPowerCap;
        private readonly Func<IMyTerminalBlock, float> _getMaxHpCap;
        private readonly Func<IMyTerminalBlock, bool> _isShieldUp;
        private readonly Func<IMyTerminalBlock, string> _shieldStatus;
        // Fields below do not require SetActiveShield to be defined first.
        private readonly Func<IMyCubeGrid, bool> _gridHasShield; 
        private readonly Func<IMyCubeGrid, bool> _gridShieldOnline; 
        private readonly Func<IMyEntity, bool> _protectedByShield; 
        private readonly Func<IMyEntity, IMyTerminalBlock> _getShieldBlock;
        private readonly Func<IMyTerminalBlock, bool> _isShieldBlock;

        public void SetActiveShield(IMyTerminalBlock block) => _block = block; // AutoSet to TapiFrontend(block) if shield exists on grid.

        public TapiFrontend(IMyTerminalBlock block)
        {
            _block = block;
            var delegates = _block.GetProperty("DefenseSystemsAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
            if (delegates == null) return;

            // ModApi only methods below
            _rayAttackShield = (Func<IMyTerminalBlock, IMySession, RayD, long, float, bool, Vector3D?>)delegates["RayAttackShield"];
            _pointAttackShield = (Func<IMyTerminalBlock, IMySession, Vector3D, long, float, bool, bool>)delegates["PointAttackShield"];
            _setShieldHeat = (Action<IMyTerminalBlock, IMySession, int>)delegates["SetShieldHeat"];
            _overLoad = (Action<IMyTerminalBlock, IMySession>)delegates["OverLoadShield"];
            _setCharge = (Action<IMyTerminalBlock, IMySession, float>)delegates["SetCharge"];
            // PB & ModApi methods below
            _rayIntersectShield = (Func<IMyTerminalBlock, RayD, Vector3D?>)delegates["RayIntersectShield"];
            _pointInShield = (Func<IMyTerminalBlock, Vector3D, bool>)delegates["PointInShield"];
            _getShieldPercent = (Func<IMyTerminalBlock, float>)delegates["GetShieldPercent"];
            _getShieldHeat = (Func<IMyTerminalBlock, int>)delegates["GetShieldHeat"];
            _getChargeRate = (Func<IMyTerminalBlock, float>)delegates["GetChargeRate"];
            _hpToChargeRatio = (Func<IMyTerminalBlock, int>)delegates["HpToChargeRatio"];
            _getMaxCharge = (Func<IMyTerminalBlock, float>)delegates["GetMaxCharge"];
            _getCharge = (Func<IMyTerminalBlock, float>)delegates["GetCharge"];
            _getPowerUsed = (Func<IMyTerminalBlock, float>)delegates["GetPowerUsed"];
            _getPowerCap = (Func<IMyTerminalBlock, float>)delegates["GetPowerCap"];
            _getMaxHpCap = (Func<IMyTerminalBlock, float>)delegates["GetMaxHpCap"];
            _isShieldUp = (Func<IMyTerminalBlock, bool>)delegates["IsShieldUp"];
            _shieldStatus = (Func<IMyTerminalBlock, string>)delegates["ShieldStatus"];
            _gridHasShield = (Func<IMyCubeGrid, bool>)delegates["GridHasShield"]; 
            _gridShieldOnline = (Func<IMyCubeGrid, bool>)delegates["GridShieldOnline"]; 
            _protectedByShield = (Func<IMyEntity, bool>)delegates["ProtectedByShield"]; 
            _getShieldBlock = (Func<IMyEntity, IMyTerminalBlock>)delegates["GetShieldBlock"];
            _isShieldBlock = (Func<IMyTerminalBlock, bool>)delegates["IsShieldBlock"];
            if (!IsShieldBlock()) _block = GetShieldBlock(_block.CubeGrid) ?? _block;
        }
        // ModApi only methods below.
        public Vector3D? RayAttackShield(RayD ray, long attackerId, float damage, bool energy = false) =>
            _rayAttackShield?.Invoke(_block, _fullApi, ray, attackerId, damage, energy) ?? null;
        public bool PointAttackShield(Vector3D pos, long attackerId, float damage, bool energy = false) =>
            _pointAttackShield?.Invoke(_block, _fullApi, pos, attackerId, damage, energy) ?? false;
        public void SetShieldHeat(int value) => _setShieldHeat?.Invoke(_block, _fullApi, value);
        public void OverLoadShield() => _overLoad?.Invoke(_block, _fullApi);
        public void SetCharge(float value) => _setCharge.Invoke(_block, _fullApi, value);
        // PB and Modapi methods below.
        public Vector3D? RayIntersectShield(RayD ray) => _rayIntersectShield?.Invoke(_block, ray) ?? null;
        public bool PointInShield(Vector3D pos) => _pointInShield?.Invoke(_block, pos) ?? false;
        public float GetShieldPercent() => _getShieldPercent?.Invoke(_block) ?? -1;
        public int GetShieldHeat() => _getShieldHeat?.Invoke(_block) ?? -1;
        public float GetChargeRate() => _getChargeRate?.Invoke(_block) ?? -1;
        public float HpToChargeRatio() => _hpToChargeRatio?.Invoke(_block) ?? -1;
        public float GetMaxCharge() => _getMaxCharge?.Invoke(_block) ?? -1;
        public float GetCharge() => _getCharge?.Invoke(_block) ?? -1;
        public float GetPowerUsed() => _getPowerUsed?.Invoke(_block) ?? -1;
        public float GetPowerCap() => _getPowerCap?.Invoke(_block) ?? -1;
        public float GetMaxHpCap() => _getMaxHpCap?.Invoke(_block) ?? -1;
        public bool IsShieldUp() => _isShieldUp?.Invoke(_block) ?? false;
        public string ShieldStatus() => _shieldStatus?.Invoke(_block) ?? string.Empty;
        public bool GridHasShield(IMyCubeGrid grid) => _gridHasShield?.Invoke(grid) ?? false;
        public bool GridShieldOnline(IMyCubeGrid grid) => _gridShieldOnline?.Invoke(grid) ?? false;
        public bool ProtectedByShield(IMyEntity entity) => _protectedByShield?.Invoke(entity) ?? false;
        public IMyTerminalBlock GetShieldBlock(IMyEntity entity) => _getShieldBlock?.Invoke(entity) ?? null;
        public bool IsShieldBlock() => _isShieldBlock?.Invoke(_block) ?? false;
    }
}

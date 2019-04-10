using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DefenseSystems
{
    public partial class BlockRegen
    {
        private bool ResetEntity()
        {
            MyCube = (MyCubeBlock)Entity;
            LocalGrid = MyCube.CubeGrid;
            if (LocalGrid.Physics == null) return false;

            AttachedGrid = LocalGrid;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _aInit = false;
            _bInit = false;
            return true;
        }

        private void BeforeInit()
        {
            if (MyCube.CubeGrid.Physics == null) return;
            Session.Instance.RegenLogics.Add(this);
            Session.Instance.GridsToLogics.Add(LocalGrid, this);

            //PowerInit();
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Registry.RegisterWithBus(this, LocalGrid, true, Bus, out Bus);
            _bTime = _isDedicated ? 10 : 1;
            _bInit = true;
        }

        private void AfterInit()
        {
            Bus.Init();
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            _aInit = true;
        }

        private void AddBlock(IMySlimBlock block)
        {
            if (_damagedBlockIdx.ContainsKey(block))
                return;
            _damagedBlockIdx.Add(block, _damagedBlocks.Count);
            _damagedBlocks.Add(block);
        }

        private void RemoveBlock(IMySlimBlock block)
        {
            int idx;
            if (!_damagedBlockIdx.TryGetValue(block, out idx))
                return;
            RemoveBlockAt(idx);
            _damagedBlockIdx.Remove(block);
        }

        private void RemoveBlockAt(int idx)
        {
            _damagedBlocks.RemoveAtFast(idx);
            if (idx < _damagedBlocks.Count)
                _damagedBlockIdx[_damagedBlocks[idx]] = idx;
        }

        private void BlockChanged(IMySlimBlock block)
        {
            //if (_blockUpdates || _blocksNotToRepair.Contains(block.BlockDefinition.Id)) return;
            if (_blockUpdates) return;
            if (!BlockIntegrity(block)) RemoveBlock(block);
            UpdateGen();
        }

        public bool BlockIntegrity(IMySlimBlock block)
        {
            var bIntegrity = block.Integrity;
            var maxIntegrity = block.MaxIntegrity;
            if (bIntegrity > maxIntegrity * MinSelfHeal && bIntegrity < maxIntegrity * MaxSelfHeal || bIntegrity >= maxIntegrity && block.HasDeformation)
            {
                AddBlock(block);
                return true;
            }
            return false;
        }

        private void UpdateGen()
        {
            if (_damagedBlocks.Count > 0) NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
        }

    }
}
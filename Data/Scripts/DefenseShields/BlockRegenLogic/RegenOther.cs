using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DefenseShields
{
    public partial class BlockRegen
    {
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
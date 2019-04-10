using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;

namespace DefenseSystems
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "K_WS_TC_NaniteCore")]
    public partial class BlockRegen : MyGameLogicComponent
    {
        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {

                ContainerInited = true;
            }
            if (Entity.InScene) OnAddedToScene();
        }

        public override void OnAddedToScene()
        {
            try
            {
                if (!ResetEntity()) return;

            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (!_bInit) BeforeInit();
                else if (!_aInit) AfterInit();
                else if (_bCount < SyncCount * _bTime)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    if (Bus.Spine == LocalGrid) _bCount++;
                }
                else _readyToSync = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation100()
        {
            AttachedGrid = Bus.Spine;
            _100Tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            if (Regening && !_blockUpdates && _100Tick > _lastTick + 10)
            {
                var i = 0;
                while (i < QueuedBlocks.Count)
                {
                    if (_damagedBlocks.Count >= MaxBlocksHealedPerCycle) break;
                    BlockIntegrity(QueuedBlocks.Dequeue());
                }
                UpdateGen();
                Regening = false;
            }
        }

        public override void UpdateAfterSimulation()
        {
            _blockUpdates = true;
            _offset = (_offset + 1) % Spread;
            var i = _offset;
            while (i < _damagedBlocks.Count)
            {
                //if (i == 0) Log.Line($"d:{_damagedBlocks.Count} - dId:{_damagedBlockIdx.Count} - qu:{QueuedBlocks.Count}");
                var block = _damagedBlocks[i];
                var bIntegrity = block.Integrity;
                var maxIntegrity = block.MaxIntegrity;

                if (bIntegrity > maxIntegrity * MinSelfHeal && bIntegrity < maxIntegrity * MaxSelfHeal)
                {
                    var repair = MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS * Spread * HealRate;
                    repair = Math.Min(block.MaxIntegrity - block.Integrity, repair);
                    if (block.OwnerId == 0)
                    {
                        var gridOwnerList = Bus.Spine.BigOwners;
                        var ownerCnt = gridOwnerList.Count;
                        var gridOwner = 0L;

                        if (gridOwnerList[0] != 0) gridOwner = gridOwnerList[0];
                        else if (ownerCnt > 1) gridOwner = gridOwnerList[1];

                        if (gridOwner != 0) block.IncreaseMountLevel(repair, gridOwner);
                        else
                        {
                            RemoveBlockAt(i);
                            _damagedBlockIdx.Remove(block);
                        }
                    }
                    else block.IncreaseMountLevel(repair, block.OwnerId);
                }
                bIntegrity = block.Integrity;
                maxIntegrity = block.MaxIntegrity;
                if (bIntegrity >= maxIntegrity * MaxSelfHeal)
                {
                    var deformTest = bIntegrity >= maxIntegrity && block.HasDeformation;
                    if (deformTest) block.FixBones(0.0f, 0.0f);

                    if (!deformTest || !block.HasDeformation)
                    {
                        //if (block.Integrity != block.MaxIntegrity || block.HasDeformation) Log.Line($"I:{block.Integrity} - M:{block.MaxIntegrity} - D:{block.HasDeformation}");
                        RemoveBlockAt(i);
                        _damagedBlockIdx.Remove(block);
                    }
                }
                else i += Spread;
            }
            _blockUpdates = false;
            if (_damagedBlocks.Count == 0)
            {
                _lastTick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            AttachedGrid = null;
        }

    }
}

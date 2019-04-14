using System;
using DefenseSystems.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;

namespace DefenseSystems
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "Emitter1x1LA", "Emitter1x1SA")]
    public partial class Regen : MyGameLogicComponent
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
            //AttachedGrid = Bus.Spine;
            _100Tick = Session.Instance.Tick;
            if (Bus.Regening && !Bus.CheckIntegrity && _100Tick > _lastTick + 10)
            {
                Bus.HitBlocks.ApplyAdditions();
                DsUtil1.Sw.Restart();
                var i = 0;
                foreach (var hitBlock in Bus.HitBlocks)
                {
                    if (i >= MaxBlocksHealedPerCycle) break;
                    BlockIntegrity(hitBlock);
                    Bus.HitBlocks.Remove(hitBlock);
                    i++;
                }
                DsUtil1.StopWatchReport("test", -1);
                Bus.HitBlocks.ApplyRemovals();
                UpdateGen();
                if (Bus.HitBlocks.Count == 0) Bus.Regening = false;
            }
        }

        public override void UpdateAfterSimulation()
        {
            Bus.CheckIntegrity = true;
            _offset = (_offset + 1) % Spread;
            var i = _offset;
            while (i < Bus.DamagedBlocks.Count)
            {
                //if (i == 0) Log.Line($"d:{_damagedBlocks.Count} - dId:{_damagedBlockIdx.Count} - qu:{QueuedBlocks.Count}");
                var block = Bus.DamagedBlocks[i];
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
                            Bus.DamagedBlockIdx.Remove(block);
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
                        Bus.DamagedBlockIdx.Remove(block);
                    }
                }
                else i += Spread;
            }
            Bus.CheckIntegrity = false;
            if (Bus.DamagedBlocks.Count == 0)
            {
                _lastTick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            //AttachedGrid = null;
        }

    }
}

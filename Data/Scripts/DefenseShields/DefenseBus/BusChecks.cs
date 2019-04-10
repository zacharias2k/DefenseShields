using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    public partial class Bus
    {
        internal void LosCheck()
        {
            LosCheckTick = uint.MaxValue;
            CheckEmitters = true;
            ActiveController.FitChanged = true;
            ActiveController.AdjustShape = true;
        }


        internal bool SlaveControllerLink(bool firstLoop)
        {
            var tick = Session.Instance.Tick;
            var notTime = tick % 120 != 0 && SubTick < tick + 10;
            if (notTime && SlaveLink) return true;
            if (IsStatic || (notTime && !firstLoop)) return false;
            var mySize = Spine.PositionComp.WorldAABB.Size.Volume;
            var myEntityId = Spine.EntityId;
            foreach (var grid in LinkedGrids.Keys)
            {
                if (grid == Spine) continue;
                Bus otherBus;
                grid.Components.TryGet(out otherBus);
                var controller = ActiveController;
                if (controller != null && controller.DsState.State.Online && controller.IsWorking)
                {
                    var otherSize = controller.Bus.Spine.PositionComp.WorldAABB.Size.Volume;
                    var otherEntityId = controller.Bus.Spine.EntityId;
                    if ((!IsStatic && controller.Bus.IsStatic) || mySize < otherSize || (mySize.Equals(otherEntityId) && myEntityId < otherEntityId))
                    {
                        SlaveLink = true;
                        return true;
                    }
                }
            }
            SlaveLink = false;
            return false;
        }


        internal float GetSpineIntegrity(IMyCubeGrid grid = null, bool remove = false)
        {
            var mainSub = false;
            if (grid == null)
            {
                ActiveController.DsState.State.GridIntegrity = 0;
                grid = Spine;
            }
            else if (grid == Spine) mainSub = true;

            var integrityAdjustment = 0f;

            var blockList = new List<IMySlimBlock>();
            grid.GetBlocks(blockList);

            for (int i = 0; i < blockList.Count; i++)
            {
                integrityAdjustment += blockList[i].MaxIntegrity;
            }

            if (!mainSub)
            {
                if (!remove) ActiveController.DsState.State.GridIntegrity += integrityAdjustment;
                else ActiveController.DsState.State.GridIntegrity -= integrityAdjustment;
            }

            return integrityAdjustment;
        }

        public void ResetDamageEffects()
        {
            if (ActiveController.DsState.State.Online && !ActiveController.DsState.State.Lowered)
            {
                lock (SubLock)
                {
                    foreach (var funcBlock in _functionalBlocks)
                    {
                        if (funcBlock == null) continue;
                        if (funcBlock.IsFunctional) funcBlock.SetDamageEffect(false);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    internal partial class Bus
    {
        internal enum Events
        {
            UpdateDimensions,
            LosCheckTick,
            EmitterEvent,
            ShapeEvent,
            FitChanged,
            AdjustShape,
            FunctionalEvent,
            BlockChanged,
            Suspend,
            UnSuspend,
            IsWorking,
            NotWorking
        }

        internal void DelayEvents(Events e)
        {
            switch (e)
            {
                case Events.UpdateDimensions:
                    if (!Field.UpdateDimensions) Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.RefreshDimensions(), this, 1);
                    Field.UpdateDimensions = true;
                    break;
                case Events.LosCheckTick:
                    if (Field.LosCheckTick != uint.MaxValue)
                    {
                        Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.LosCheck(), this, 1800);
                        Field.LosCheckTick = Session.Instance.Tick + 1800;
                    }
                    break;
                case Events.AdjustShape:
                    if (!Field.AdjustShape) Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.ReAdjustShape(true), this, 1);
                    Field.AdjustShape = true;
                    break;
                case Events.BlockChanged:
                    break;
                case Events.EmitterEvent:
                    if (!Field.EmitterEvent) Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.EmitterEventDetected(), this, 1);
                    Field.EmitterEvent = true;
                    break;
                case Events.ShapeEvent:
                    if (!Field.ShapeEvent) Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.CheckExtents(), this, 1);
                    Field.ShapeEvent = true;
                    break;
                case Events.FitChanged:
                    if (!Field.FitChanged) Session.Instance.FutureEvents.Schedule((o1) => (o1 as Fields)?.CheckExtents(), this, 1);
                    Field.FitChanged = true;
                    break;
            }
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
                if (controller != null && controller.State.Value.Online && controller.IsWorking)
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
                ActiveController.State.Value.SpineIntegrity = 0;
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
                if (!remove) ActiveController.State.Value.SpineIntegrity += integrityAdjustment;
                else ActiveController.State.Value.SpineIntegrity -= integrityAdjustment;
            }

            return integrityAdjustment;
        }

        public void ResetDamageEffects()
        {
            if (ActiveController.State.Value.Online && !ActiveController.State.Value.Lowered)
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

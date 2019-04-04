using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    public partial class Bus
    {

        public void SetSpine(bool check, MyCubeGrid grid = null)
        {
            var keepSpine = check && !(Spine == null || Spine.MarkedForClose || !Spine.InScene || Spine == grid);
            if (keepSpine)
            {
                Log.Line($"[SpineFine-] - Null:{Spine == null} - Marked:{Spine.MarkedForClose} - !InScene:{!Spine.InScene} - gridMatch:{Spine == grid}");
                return;
            }
            var newSpine = SortedGrids.Max;
            if (Spine == newSpine) return;
            if (Spine != null && Spine.Components.Has<Bus>())
            {
                Log.Line($"[SpineReset] - as:{Spine.DebugName} - Is:{newSpine.DebugName}");
                Spine.Components.Remove<Bus>();
            }
            Log.Line($"[NewSpine--] - Is:{newSpine.DebugName}");
            SetSubFlags(check ? grid : newSpine);

            Spine = newSpine;
            Spine.Components.Add(this);
        }

        public void AddSortedGrids(MyCubeGrid grid)
        {
            if (SortedGrids.Contains(grid)) return;
            SortedGrids.Add(grid);
            RegisterGridEvents(grid, true);
        }

        public void RemoveGrid(MyCubeGrid grid)
        {
            if (SubGrids.Contains(grid)) return;
            GridLeaving(grid);

            SortedGrids.Remove(grid);
            RegisterGridEvents(grid, false);
            SetSpine(true, grid);
        }

        public bool SubGridDetect(MyCubeGrid grid, bool force = false)
        {
            var newLinkGrop = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);
            var newLinkGropCnt = newLinkGrop.Count;
            lock (SubUpdateLock)
            {
                RemSubs.Clear();
                foreach (var sub in LinkedGrids.Keys) RemSubs.Add(sub);
            }

            lock (SubLock)
            {
                if (newLinkGropCnt == LinkedGrids.Count && !force) return false;
                SubGrids.Clear();
                LinkedGrids.Clear();

                for (int i = 0; i < newLinkGropCnt; i++)
                {
                    var sub = (MyCubeGrid)newLinkGrop[i];
                    var mechSub = false;
                    if (MyAPIGateway.GridGroups.HasConnection(grid, sub, GridLinkTypeEnum.Mechanical))
                    {
                        mechSub = true;
                        SubGrids.Add(sub);
                    }
                    LinkedGrids[sub] = new SubGridInfo(sub, sub == grid, mechSub);
                }
            }

            var change = false;
            var add = false;
            var remove = false;
            lock (SubUpdateLock)
            {
                AddSubs.Clear();
                foreach (var sub in LinkedGrids.Keys)
                {
                    AddSubs.Add(sub);
                    NewTmp1.Add(sub);
                }

                NewTmp1.IntersectWith(RemSubs);
                RemSubs.ExceptWith(AddSubs);
                AddSubs.ExceptWith(NewTmp1);
                NewTmp1.Clear();

                if (AddSubs.Count != 0) add = true;
                if (RemSubs.Count != 0) remove = true;
                if (add || remove) change = true;

                foreach (var sub in AddSubs) AddSortedGrids(sub);
                foreach (var sub in RemSubs) RemoveGrid(sub);
            }
            if (change)
            {
                SetSubFlags(grid);
            }
            return change;
        }

        internal void SetSubFlags(MyCubeGrid grid)
        {
            Log.Line($"[IsSubFlagEvent-] - trigged by gId:{grid.EntityId}");
            BlockChanged = true;
            FunctionalChanged = true;
            UpdateGridDistributor = true;
        }

        public void GridLeaving(MyCubeGrid grid)
        {
            Log.Line($"[GridLeaveEvent] - trigged by gId:{grid.EntityId}");
            RemoveSubBlocks(SortedControllers, grid);
            RemoveSubBlocks(SortedEmitters, grid);
        }
    }
}

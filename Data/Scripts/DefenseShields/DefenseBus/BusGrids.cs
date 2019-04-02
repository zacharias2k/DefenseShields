using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    public partial class DefenseBus
    {

        public void SetMasterGrid(bool check, MyCubeGrid grid = null)
        {
            var keepMaster = check && !(MasterGrid == null || MasterGrid.MarkedForClose || !MasterGrid.InScene || MasterGrid == grid);
            if (keepMaster)
            {
                Log.Line($"KeepingMasterGrid: Null:{MasterGrid == null} - Marked:{MasterGrid.MarkedForClose} - !InScene:{!MasterGrid.InScene} - gridMatch:{MasterGrid == grid}");
                return;
            }
            var master = SortedGrids.Max;
            if (MasterGrid == master) return;
            Log.Line("new master not equal old master");
            if (MasterGrid != null && MasterGrid.Components.Has<DefenseBus>())
            {
                Log.Line("ReSetMasterGrid");
                MasterGrid.Components.Remove<DefenseBus>();
            }
            Log.Line("SettingMasterGrid");
            SetSubFlags();

            MasterGrid = master;
            MasterGrid.Components.Add(this);
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
            SetMasterGrid(true, grid);
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

            if (change && ActiveController != null && MasterGrid != null)
            {
                SetSubFlags();
            }
            return change;
        }


        internal void SetSubFlags()
        {
            Log.Line("set flags");
            BlockChanged = true;
            FunctionalChanged = true;
            UpdateGridDistributor = true;
        }
    }
}

using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    public class DefenseBus : MyEntityComponentBase
    {
        public MyCubeGrid MasterGrid;
        internal readonly object SubLock = new object();
        internal readonly object SubUpdateLock = new object();

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
            }
            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        //public List<DefenseSystems> SortedControllers = new List<DefenseSystems>();
        
        /*
        public void AddSortedControllers(DefenseSystems ds)
        {
            if (!SortedControllers.Contains(ds)) SortedControllers.Add(ds);
            else return;
            SortedControllers.Sort((a, b) =>
            {
                var compareVolume = a.LocalGrid.PositionComp.WorldAABB.Volume.CompareTo(b.LocalGrid.PositionComp.WorldAABB.Volume);
                if (compareVolume != 0) return compareVolume;

                return -a.MyCube.EntityId.CompareTo(b.MyCube.EntityId);
            });
            var index = SortedControllers.IndexOf(ds);
            Log.Line($"{index} - {SortedControllers.Count}");
        }
        */
        public void SetMasterGrid()
        {
            if (MasterGrid == SortedGrids.Max) return;
            if (MasterGrid != null && MasterGrid.Components.Has<DefenseBus>())
            {
                Log.Line("ReSetMasterGrid");
                MasterGrid.Components.Remove<DefenseBus>();
            }
            else
            {
                Log.Line("SetMasterGrid");
                DefenseSystems.SetSubFlags();
            }

            MasterGrid = SortedGrids.Max;
            MasterGrid.Components.Add(this);
        }

        public SortedSet<DefenseSystems> SortedControllers = new SortedSet<DefenseSystems>(new BlockPriority());
        public void AddSortedControllers(DefenseSystems ds)
        {
            if (SortedControllers.Contains(ds)) return;
            long oldMaster = -1;
            double oldSize = -1;
            var myId = ds.MyCube.EntityId;
            var mySize = ds.LocalGrid.PositionComp.WorldAABB.Volume;
            if (DefenseSystems != null)
            {
                oldMaster = DefenseSystems.MyCube.EntityId;
                oldSize = DefenseSystems.LocalGrid.PositionComp.WorldAABB.Volume;
            }
            SortedControllers.Add(ds);
            ds.RegisterEvents(ds.LocalGrid, true);
            DefenseSystems = SortedControllers.Max;
            Log.Line($"Add Controller: [my:{myId} - {mySize}] - [old:{oldMaster} - {oldSize}] - [new:{DefenseSystems.MyCube.EntityId} - {DefenseSystems.LocalGrid.PositionComp.WorldAABB.Volume}]");
        }

        public void RemoveController(DefenseSystems ds)
        {
            long oldMaster = -1;
            double oldSize = -1;
            if (DefenseSystems != null)
            {
                oldMaster = DefenseSystems.MyCube.EntityId;
                oldSize = DefenseSystems.LocalGrid.PositionComp.WorldAABB.Volume;
            }
            SortedControllers.Remove(ds);
            ds.RegisterEvents(ds.LocalGrid, false);
            if (DefenseSystems == null || DefenseSystems.MyCube.MarkedForClose || !DefenseSystems.MyCube.InScene || DefenseSystems == ds)
            {
                DefenseSystems = SortedControllers.Max;
            }
            Log.Line($"Remove Controller: oldMaster:{oldMaster} - {oldSize} - newMaster:{DefenseSystems.MyCube.EntityId} - {DefenseSystems.LocalGrid.PositionComp.WorldAABB.Volume}");
        }

        public SortedSet<MyCubeGrid> SortedGrids = new SortedSet<MyCubeGrid>(new GridPriority());
        public void AddSortedGrids(MyCubeGrid grid)
        {
            if (SortedGrids.Contains(grid)) return;
            long oldMaster = -1;
            double oldSize = -1;
            var myId = grid.EntityId;
            var mySize = grid.PositionComp.WorldAABB.Volume;
            if (MasterGrid != null)
            {
                oldMaster = MasterGrid.EntityId;
                oldSize = MasterGrid.PositionComp.WorldAABB.Volume;
            }
            SortedGrids.Add(grid);
            DefenseSystems.RegisterGridEvents(grid, true);

            Log.Line($"Add Grid: [my:{myId} - {mySize}] - [old:{oldMaster} - {oldSize}]");
        }

        public void RemoveGrid(MyCubeGrid grid)
        {
            if (!grid.MarkedForClose) return;
            long oldMaster = -1;
            double oldSize = -1;
            if (MasterGrid != null)
            {
                oldMaster = MasterGrid.EntityId;
                oldSize = MasterGrid.PositionComp.WorldAABB.Volume;
            }
            SortedGrids.Remove(grid);
            DefenseSystems.RegisterGridEvents(grid, false);

            if (MasterGrid == null || MasterGrid.MarkedForClose || !MasterGrid.InScene || MasterGrid == grid)
            {
                SetMasterGrid();
            }
            Log.Line($"Remove Grid: oldMaster:{oldMaster} - {oldSize} - newMaster:{MasterGrid.EntityId} - {MasterGrid.PositionComp.WorldAABB.Volume}");
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
                if (AddSubs.Count != 0 || RemSubs.Count != 0) change = true;
                foreach (var sub in AddSubs) AddSortedGrids(sub);
                foreach (var sub in RemSubs) RemoveGrid(sub);
            }

            if (change && DefenseSystems != null && MasterGrid != null)
            {
                DefenseSystems.SetSubFlags();
            }
            return change;
        }

        public HashSet<MyCubeGrid> NewTmp1 { get; set; } = new HashSet<MyCubeGrid>();
        public HashSet<MyCubeGrid> AddSubs { get; set; } = new HashSet<MyCubeGrid>();
        public HashSet<MyCubeGrid> RemSubs { get; set; } = new HashSet<MyCubeGrid>();
        public HashSet<MyCubeGrid> SubGrids { get; set; } = new HashSet<MyCubeGrid>();
        public Dictionary<MyCubeGrid, SubGridInfo> LinkedGrids { get; set; } = new Dictionary<MyCubeGrid, SubGridInfo>();


        public Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];

        public Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        public DefenseSystems DefenseSystems { get; set; }

        public Enhancers Enhancer { get; set; }

        public Modulators Modulator { get; set; }

        public int EmitterMode { get; set; } = -1;
        public long ActiveEmitterId { get; set; }

        public Emitters StationEmitter { get; set; }
        public Emitters ShipEmitter { get; set; }

        public O2Generators ActiveO2Generator { get; set; }

        public string ModulationPassword { get; set; }

        public bool EmitterLos { get; set; }

        public bool EmittersSuspended { get; set; }

        public bool O2Updated { get; set; }

        public float DefaultO2 { get; set; }

        public bool CheckEmitters { get; set; }

        public bool GridIsMoving { get; set; }

        public bool EmitterEvent { get; set; }

        public double ShieldVolume { get; set; }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

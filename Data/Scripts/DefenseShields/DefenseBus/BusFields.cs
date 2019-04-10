using System;
using System.Collections.Generic;
using DefenseSystems.Support;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseSystems
{
    public partial class Bus 
    {
        public event Action<MyEntity, LogicState> OnBusSplit;

        public enum LogicState
        {
            Join,
            Leave,
            Close,
            Offline,
            Online,
            Active,
            Suspend,
            Init
        }
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal Task FuncTask { get; set; }
        internal readonly float[] ReserveScaler = { float.MaxValue * 0.001f, 0.001f, 1, 1000, 1000000 };

        internal readonly object SubLock = new object();
        internal readonly object SubUpdateLock = new object();

        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<IMyTextPanel> _displayBlocks = new List<IMyTextPanel>();
        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<IMyBatteryBlock> _batteryBlocks = new List<IMyBatteryBlock>();
        private float _batteryMaxPower;
        private float _batteryCurrentOutput;
        private float _batteryCurrentInput;

        internal SortedSet<MyCubeGrid> SortedGrids = new SortedSet<MyCubeGrid>(new GridPriority());
        internal SortedSet<Controllers> SortedControllers = new SortedSet<Controllers>(new ControlPriority());
        internal SortedSet<Emitters> SortedEmitters = new SortedSet<Emitters>(new EmitterPriority());

        internal HashSet<MyCubeGrid> NewTmp1 { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> AddSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> RemSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> SubGrids { get; set; } = new HashSet<MyCubeGrid>();
        internal Dictionary<MyCubeGrid, SubGridInfo> LinkedGrids { get; set; } = new Dictionary<MyCubeGrid, SubGridInfo>();
        internal BusEvents Events { get; set; } = new BusEvents();
        internal MyResourceDistributorComponent MyResourceDist { get; set; }
        internal MyCubeGrid Spine;

        internal uint SubTick { get; set; }
        internal uint LosCheckTick { get; set; }
        internal uint FuncTick { get; set; }
        internal uint EffectsCleanTick { get; set; }

        internal float SpineIntegrity { get; set; }
        internal float SpineAvailablePower { get; set; }
        internal float SpineMaxPower { get; set; }
        internal float SpineCurrentPower { get; set; }
        internal float ShieldAvailablePower { get; set; }
        internal float ShieldMaxPower { get; set; }

        internal bool BusIsSplit { get; set; }
        internal bool Inited { get; set; }
        internal bool SubUpdate { get; set; }
        internal bool FunctionalAdded { get; set; }
        internal bool FunctionalRemoved { get; set; }
        internal bool FunctionalChanged { get; set; }
        internal bool FunctionalEvent { get; set; }
        internal bool BlockAdded { get; set; }
        internal bool BlockRemoved { get; set; }
        internal bool BlockChanged { get; set; }
        internal bool BlockEvent { get; set; }
        internal bool UpdateGridDistributor { get; set; }
        internal bool CheckForDistributor { get; set; }
        internal bool SlaveLink { get; set; }
        internal bool EffectsDirty { get; set; }
        internal bool IsStatic { get; set; }

        internal Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];
        internal Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        internal Controllers ActiveController { get; set; }
        internal Enhancers ActiveEnhancer { get; set; }
        internal Modulators ActiveModulator { get; set; }
        internal Emitters ActiveEmitter { get; set; }
        internal O2Generators ActiveO2Generator { get; set; }

        internal int EmitterMode { get; set; } = -1;
        internal long ActiveEmitterId { get; set; }

        internal string ModulationPassword { get; set; }

        internal bool EmitterLos { get; set; }
        internal bool O2Updated { get; set; }
        internal float DefaultO2 { get; set; }
        internal bool CheckEmitters { get; set; }
        internal bool GridIsMoving { get; set; }
        internal bool EmitterEvent { get; set; }
        internal double ShieldVolume { get; set; }
    }
}

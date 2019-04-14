using System;
using System.Collections.Generic;
using DefenseSystems.Support;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Bus 
    {
        internal Fields Field;
        internal Armors Armor;

        internal Bus()
        {
            Field = new Fields(this);
            Armor = new Armors(this);
        }


        internal event Action<MyEntity, LogicState> OnBusSplit;

        internal enum LogicState
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

        internal readonly Dictionary<IMySlimBlock, int> DamagedBlockIdx = new Dictionary<IMySlimBlock, int>();

        internal readonly ConcurrentCachingHashSet<IMySlimBlock> HitBlocks = new ConcurrentCachingHashSet<IMySlimBlock>();

        internal readonly List<IMySlimBlock> DamagedBlocks = new List<IMySlimBlock>();

        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<IMyBatteryBlock> _batteryBlocks = new List<IMyBatteryBlock>();
        private float _batteryMaxPower;
        private float _batteryCurrentOutput;
        private float _batteryCurrentInput;

        private bool _isDedicated;
        private bool _mpActive;
        private bool _isServer;

        internal SortedSet<MyCubeGrid> SortedGrids = new SortedSet<MyCubeGrid>(new GridPriority());
        internal SortedSet<Controllers> SortedControllers = new SortedSet<Controllers>(new ControlPriority());
        internal SortedSet<Emitters> SortedEmitters = new SortedSet<Emitters>(new EmitterPriority());
        internal SortedSet<Regen> SortedRegens = new SortedSet<Regen>(new RegenPriority());

        internal HashSet<MyCubeGrid> NewTmp1 { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> AddSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> RemSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> SubGrids { get; set; } = new HashSet<MyCubeGrid>();
        internal Dictionary<MyCubeGrid, SubGridInfo> LinkedGrids { get; set; } = new Dictionary<MyCubeGrid, SubGridInfo>();
        internal MyResourceDistributorComponent MyResourceDist { get; set; }
        internal MyCubeGrid Spine;

        internal int Count { get; set; } = -1;

        internal uint SubTick { get; set; }
        internal uint FuncTick { get; set; }
        internal uint Tick { get; set; }

        internal float SpineIntegrity { get; set; }
        internal float SpineAvailablePower { get; set; }
        internal float SpineMaxPower { get; set; }
        internal float SpineCurrentPower { get; set; }

        internal float PowerForUse { get; set; }

        internal bool BusIsSplit { get; set; }
        internal bool Inited { get; set; }
        internal bool SubUpdate { get; set; }
        internal bool FunctionalAdded { get; set; }
        internal bool FunctionalRemoved { get; set; }
        internal bool FunctionalChanged { get; set; }
        internal bool FunctionalEvent { get; set; }
        internal bool CheckIntegrity { get; set; }
        internal bool BlockAdded { get; set; }
        internal bool BlockRemoved { get; set; }
        internal bool BlockChanged { get; set; }
        internal bool BlockEvent { get; set; }
        internal bool UpdateGridDistributor { get; set; }
        internal bool CheckForDistributor { get; set; }
        internal bool SlaveLink { get; set; }
        internal bool EffectsDirty { get; set; }
        internal bool IsStatic { get; set; }
        internal bool Regening { get; set; }
        internal bool Tick20 { get; set; }
        internal bool Tick60 { get; set; }
        internal bool Tick180 { get; set; }
        internal bool Tick300 { get; set; }
        internal bool Tick600 { get; set; }
        internal bool Tick1800 { get; set; }
        internal bool PowerUpdate { get; set; }
        internal bool Starting { get; set; }

        internal Controllers ActiveController { get; set; }
        internal Enhancers ActiveEnhancer { get; set; }
        internal Modulators ActiveModulator { get; set; }
        internal Emitters ActiveEmitter { get; set; }
        internal O2Generators ActiveO2Generator { get; set; }
        internal Regen ActiveRegen { get; set; }

        internal long ActiveEmitterId { get; set; }

        internal string ModulationPassword { get; set; }

        internal bool SpineIsMoving { get; set; }

        internal EmitterModes EmitterMode { get; set; } = EmitterModes.Unknown;

        internal enum EmitterModes
        {
            Station,
            LargeShip,
            SmallShip,
            Unknown
        }

    }
}

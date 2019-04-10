using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DefenseSystems
{ 
    public partial class BlockRegen
    {
        /*
        private static readonly HashSet<MyDefinitionId> _blocksNotToRepair =
            new HashSet<MyDefinitionId>(MyDefinitionId.Comparer)
            {
                new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "K_WS_TC_NaniteCore")
            };
        */
        internal Bus Bus;
        private const int MaxBlocksHealedPerCycle = 50;
        private const float MinSelfHeal = 0.4f;
        private const float MaxSelfHeal = 1.0f;
        private const float HealRate = 0.1f;
        private const int Spread = 10;
        private const int SyncCount = 60;

        internal bool Regening;
        internal bool ContainerInited;
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }
        private int _bCount;
        private int _bTime;
        private int _offset;
        private bool _readyToSync;
        private bool _firstSync;
        private bool _bInit;
        private bool _aInit;
        private bool _blockUpdates;
        private uint _lastTick;
        private uint _100Tick;
        private bool _isServer;
        private bool _isDedicated;
        private MyCubeGrid _attachedGrid;
        internal MyCubeBlock MyCube;
        internal MyCubeGrid LocalGrid;
        internal DSUtils DsUtil1 = new DSUtils();
        internal Registry Registry { get; set; } = new Registry();

        private readonly Dictionary<IMySlimBlock, int> _damagedBlockIdx = new Dictionary<IMySlimBlock, int>();
        private readonly List<IMySlimBlock> _damagedBlocks = new List<IMySlimBlock>();
        internal readonly UniqueQueue<IMySlimBlock> QueuedBlocks = new UniqueQueue<IMySlimBlock>();

        internal bool IsAfterInited
        {
            get { return _aInit; }
            set
            {
                if (_aInit != value)
                {
                    _aInit = value;
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                }
            }
        }

        private MyCubeGrid AttachedGrid
        {
            get
            {
                return _attachedGrid;
            }
            set
            {
                if (_attachedGrid == value)
                {
                    return;
                }

                if (_attachedGrid != null)
                {
                    _attachedGrid.OnBlockIntegrityChanged -= BlockChanged;
                    _attachedGrid.OnBlockAdded -= BlockChanged;
                    _attachedGrid.OnBlockRemoved -= BlockChanged;
                }

                _damagedBlockIdx.Clear();
                _damagedBlocks.Clear();
                _attachedGrid = value;
                if (_attachedGrid != null)
                {
                    ((IMyCubeGrid)_attachedGrid).GetBlocks(null, (x) =>
                    {
                        BlockChanged(x);
                        return false;
                    });
                    _attachedGrid.OnBlockIntegrityChanged += BlockChanged;
                    _attachedGrid.OnBlockAdded += BlockChanged;
                    _attachedGrid.OnBlockRemoved += BlockChanged;
                }
            }
        }

    }
}

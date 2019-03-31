using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;

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

        private const int MaxBlocksHealedPerCycle = 50;
        private const float MinSelfHeal = 0.4f;
        private const float MaxSelfHeal = 1.0f;
        private const float HealRate = 0.1f;
        private const int Spread = 10;

        internal bool Regening;
        internal bool ContainerInited;

        private int _offset;
        private bool _blockUpdates;
        private uint _lastTick;
        private uint _100Tick;

        private MyCubeGrid _attachedGrid;
        internal MyCubeBlock MyCube;
        internal MyCubeGrid MyGrid;
        internal DSUtils DsUtil1 = new DSUtils();

        private readonly Dictionary<IMySlimBlock, int> _damagedBlockIdx = new Dictionary<IMySlimBlock, int>();
        private readonly List<IMySlimBlock> _damagedBlocks = new List<IMySlimBlock>();
        internal readonly UniqueQueue<IMySlimBlock> QueuedBlocks = new UniqueQueue<IMySlimBlock>();

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

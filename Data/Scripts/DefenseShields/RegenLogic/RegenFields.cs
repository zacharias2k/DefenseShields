using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.ModAPI;

namespace DefenseSystems
{ 
    public partial class Regen
    {
        /*
        private static readonly HashSet<MyDefinitionId> _blocksNotToRepair =
            new HashSet<MyDefinitionId>(MyDefinitionId.Comparer)
            {
                new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "K_WS_TC_NaniteCore")
            };
        */
        internal Bus Bus;
        private const int MaxBlocksHealedPerCycle = 15;
        private const float MinSelfHeal = 0.05f;
        private const float MaxSelfHeal = 1.0f;
        private const float HealRate = 0.1f;
        private const int Spread = 10;
        private const int SyncCount = 60;

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
        private uint _lastTick;
        private uint _100Tick;
        private bool _isServer;
        private bool _isDedicated;
        private bool _everyFrame;

        //private MyCubeGrid _attachedGrid;
        internal MyCubeBlock MyCube;
        internal MyCubeGrid LocalGrid;
        internal DSUtils DsUtil1 = new DSUtils();
        internal Registry Registry { get; set; } = new Registry();

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
    }
}

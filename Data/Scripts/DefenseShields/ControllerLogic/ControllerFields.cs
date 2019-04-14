using VRage.ModAPI;
using VRage.Game.ModAPI;
using System.Collections.Concurrent;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Collections;
using VRageMath;

namespace DefenseSystems
{
    public partial class Controllers
    {
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        internal readonly CachingDictionary<MyCubeBlock, uint> DirtyCubeBlocks = new CachingDictionary<MyCubeBlock, uint>();
        internal readonly ConcurrentDictionary<MyCubeGrid, BlockSets> BlockSets = new ConcurrentDictionary<MyCubeGrid, BlockSets>();
        internal readonly ConcurrentDictionary<MyEntity, MoverInfo> EntsByMe = new ConcurrentDictionary<MyEntity, MoverInfo>();

        internal readonly ConcurrentQueue<SubGridComputedInfo> AddSubGridInfo = new ConcurrentQueue<SubGridComputedInfo>();

        internal volatile int LogicSlot;
        internal volatile int MonitorSlot;
        internal volatile int LostPings;
        internal volatile bool MoverByShield;
        internal volatile bool PlayerByShield;
        internal volatile bool NewEntByShield;
        internal volatile bool Asleep;
        internal volatile bool WasPaused;
        internal volatile uint LastWokenTick;
        internal volatile bool NotBubble;

        internal Bus Bus;

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.Instance.LastTerminalId == Controller.EntityId;
        internal float SinkPower = 0.001f;

        private const int SyncCount = 60;

        private uint _fatTick;
        private uint _messageTick;


        private int _bCount;
        private int _bTime;

        private long _gridOwnerId = -1;
        private long _controllerOwnerId = -1;

        private bool _bInit;
        private bool _aInit;

        private bool _allInited;
        private bool _containerInited;

        private bool _isDedicated;
        private bool _mpActive;
        private bool _isServer;

        private bool _clientOn;
        private bool _readyToSync;
        private bool _firstSync;
        private bool _clientNotReady;
        private bool _firstLoop = true;

        private Color _oldPercentColor = Color.Transparent;

        private MyResourceSinkInfo _resourceInfo;
        internal MyResourceSinkComponent Sink { get; set; }

        private DSUtils Dsutil1 { get; set; } = new DSUtils();

        public int DtreeProxyId { get; set; } = -1;

        internal IMyUpgradeModule Controller { get; set; }
        internal MyCubeGrid LocalGrid { get; set; }
        internal MyCubeBlock MyCube { get; set; }

        internal ControllerSettings Set { get; set; }
        internal ControllerState State { get; set; }
        internal Registry Registry { get; set; } = new Registry();
        internal uint ResetEntityTick { get; set; }
        internal uint TicksWithNoActivity { get; set; }

        internal float SinkCurrentPower { get; set; }

        internal bool NotFailed { get; set; }
        internal bool WarmedUp { get; set; }

        internal bool SettingsUpdated { get; set; }
        internal bool ClientUiUpdate { get; set; }
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }
        internal bool EntCleanUpTime { get; set; }

        internal enum Status
        {
            Active,
            Failure,
            Init,
            Lowered,
            Sleep,
            Wake,
            Suspend,
            Other,
            NoArmor
        }

        public enum PlayerNotice
        {
            EmitterInit,
            FieldBlocked,
            OverLoad,
            EmpOverLoad,
            Remodulate,
            NoPower,
            NoLos
        }

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

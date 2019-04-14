using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DefenseSystems
{
    public partial class Regen
    {
        private bool ResetEntity()
        {
            MyCube = (MyCubeBlock)Entity;
            LocalGrid = MyCube.CubeGrid;
            if (LocalGrid.Physics == null) return false;

            //AttachedGrid = LocalGrid;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _aInit = false;
            _bInit = false;
            return true;
        }

        private void BeforeInit()
        {
            if (MyCube.CubeGrid.Physics == null) return;
            Session.Instance.Regens.Add(this);

            //PowerInit();
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            IsWorking = MyCube.IsWorking;
            IsFunctional = MyCube.IsFunctional;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Registry.RegisterWithBus(this, LocalGrid, true, Bus, out Bus);
            _bTime = _isDedicated ? 10 : 1;
            _bInit = true;
        }

        private void AfterInit()
        {
            Bus.Init();
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            _aInit = true;
        }

        internal void RegisterEvents(MyCubeGrid grid, Bus bus, bool register = true)
        {
            if (register)
            {
                bus.OnBusSplit += OnBusSplit;
                //MyCube.EnabledChanged += CheckEmitter;
                //MyCube.IsWorkingChanged += IsWorkingChanged;
                //IsWorkingChanged(MyCube);
            }
            else
            {
                bus.OnBusSplit -= OnBusSplit;
                //MyCube.AppendingCustomInfo -= AppendingCustomInfo;
                //MyCube.EnabledChanged -= CheckEmitter;
                //MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void OnBusSplit<T>(T type, Bus.LogicState state)
        {
            var grid = type as MyCubeGrid;
            if (grid == null) return;
            if (state == Bus.LogicState.Leave)
            {
                var onMyBus = Bus.SubGrids.Contains(grid);
                if (!onMyBus && Bus.ActiveRegen == null)
                {
                    IsAfterInited = false;
                    Bus.Inited = false;
                }
                Log.Line($"[rId:{MyCube.EntityId}] [Splitter - gId:{grid.EntityId} - bCnt:{grid.BlocksCount}] - [Receiver - gId:{MyCube.CubeGrid.EntityId} - OnMyBus:{onMyBus} - iMaster:{MyCube.CubeGrid == Bus.Spine} - mSize:{Bus.Spine.BlocksCount}]");
            }
        }

        internal void AddBlock(IMySlimBlock block)
        {
            if (Bus.DamagedBlockIdx.ContainsKey(block))
                return;
            Bus.DamagedBlockIdx.Add(block, Bus.DamagedBlocks.Count);
            Bus.DamagedBlocks.Add(block);
        }

        internal void RemoveBlock(IMySlimBlock block)
        {
            int idx;
            if (!Bus.DamagedBlockIdx.TryGetValue(block, out idx))
                return;
            RemoveBlockAt(idx);
            Bus.DamagedBlockIdx.Remove(block);
        }

        private void RemoveBlockAt(int idx)
        {
            Bus.DamagedBlocks.RemoveAtFast(idx);
            if (idx < Bus.DamagedBlocks.Count)
                Bus.DamagedBlockIdx[Bus.DamagedBlocks[idx]] = idx;
        }

        internal void BlockChanged(IMySlimBlock block)
        {
            if (!BlockIntegrity(block)) RemoveBlock(block);
            UpdateGen();
        }

        public bool BlockIntegrity(IMySlimBlock block)
        {
            var bIntegrity = block.Integrity;
            var maxIntegrity = block.MaxIntegrity;
            if (bIntegrity > maxIntegrity * MinSelfHeal && bIntegrity < maxIntegrity * MaxSelfHeal || bIntegrity >= maxIntegrity && block.HasDeformation)
            {
                AddBlock(block);
                return true;
            }
            return false;
        }

        private void UpdateGen()
        {
            if (Bus.DamagedBlocks.Count > 0) NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
        }

    }
}
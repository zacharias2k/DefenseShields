﻿using System.Collections.Generic;
using Sandbox.Game.Entities;
namespace DefenseSystems.Support
{
    public class Registry
    {
        public bool RegisterWithBus<T>(T logic, MyCubeGrid localGrid, bool register, Bus oldBus, out Bus bus)
        {
            if (register)
            {
                var newBus = Session.Instance.FindBus(localGrid) ?? new Bus();
                newBus.SortAndAddBlock(logic);
                newBus.SubGridDetect(localGrid, true);
                newBus.SetSpine(false);
                bus = newBus;
                return true;
            }
            if (oldBus != null)
            {
                oldBus.SubGridDetect(localGrid, true);
                oldBus.RemoveBlock(logic);
            }
            bus = null;
            return false;
        }
    }

    public class ControlPriority : IComparer<Controllers>
    {
        public int Compare(Controllers x, Controllers y)
        {
            var compareVolume = x.LocalGrid.PositionComp.WorldAABB.Volume.CompareTo(y.LocalGrid.PositionComp.WorldAABB.Volume);
            if (compareVolume != 0) return compareVolume;

            var compareBlocks = x.LocalGrid.BlocksCount.CompareTo(y.LocalGrid.BlocksCount);
            if (compareBlocks != 0) return compareBlocks;

            return x.MyCube.EntityId.CompareTo(y.MyCube.EntityId);
        }
    }

    public class EmitterPriority : IComparer<Emitters>
    {
        public int Compare(Emitters x, Emitters y)
        {
            var xIsShip = x.EmitterMode != Emitters.EmitterType.Station && !x.MyCube.CubeGrid.IsStatic;
            var xIsStation = x.EmitterMode == Emitters.EmitterType.Station && x.MyCube.CubeGrid.IsStatic;
            var yIsShip = y.EmitterMode != Emitters.EmitterType.Station && !y.MyCube.CubeGrid.IsStatic;
            var yIsStation = y.EmitterMode == Emitters.EmitterType.Station && y.MyCube.CubeGrid.IsStatic;

            var xIsvalid = xIsShip || xIsStation;
            var yIsvalid = yIsShip || yIsStation;

            var compareStates = xIsvalid.CompareTo(yIsvalid);
            if (compareStates != 0) return compareStates;

            var compareVolume = x.LocalGrid.PositionComp.WorldAABB.Volume.CompareTo(y.LocalGrid.PositionComp.WorldAABB.Volume);
            if (compareVolume != 0) return compareVolume;

            var compareBlocks = x.LocalGrid.BlocksCount.CompareTo(y.LocalGrid.BlocksCount);
            if (compareBlocks != 0) return compareBlocks;

            return x.MyCube.EntityId.CompareTo(y.MyCube.EntityId);
        }
    }

    public class GridPriority : IComparer<MyCubeGrid>
    {
        public int Compare(MyCubeGrid x, MyCubeGrid y)
        {
            var compareVolume = x.PositionComp.WorldAABB.Volume.CompareTo(y.PositionComp.WorldAABB.Volume);
            if (compareVolume != 0) return compareVolume;

            var compareBlocks = x.BlocksCount.CompareTo(y.BlocksCount);
            if (compareBlocks != 0) return compareBlocks;

            return x.EntityId.CompareTo(y.EntityId);
        }
    }
}

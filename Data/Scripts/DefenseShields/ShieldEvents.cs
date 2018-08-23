using System;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded += BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved += BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded += FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved += FatBlockRemoved;
            }
            else
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded -= BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved -= BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded -= FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved -= FatBlockRemoved;
            }
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (ShieldComp == null || _tick == _hierarchyTick) return;
                if (_hierarchyTick > _tick - 9)
                {
                    _hierarchyDelayed = true;
                    return;
                }
                _hierarchyTick = _tick;
                UpdateSubGrids();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller HierarchyChanged: {ex}"); }
        }

        private void BlockAdded(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockAdded = true;
                _blockChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemoved(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockRemoved = true;
                _blockChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

        private void FatBlockAdded(MyCubeBlock mySlimBlock)
        {
            try
            {
                _functionalAdded = true;
                _functionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                _functionalRemoved = true;
                _functionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }
    }
}

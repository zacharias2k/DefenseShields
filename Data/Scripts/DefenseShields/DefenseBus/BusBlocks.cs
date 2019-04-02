using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;

namespace DefenseSystems
{
    public partial class DefenseBus
    {
        public void RemoveBlock<T>(T block)
        {
            var controller = block as Controllers;
            if (controller != null)
            {
                if (!SortedControllers.Remove(controller)) return;

                controller.RegisterEvents(controller.LocalGrid, false);
                SetMasterController(true, controller);
                Log.Line("Remove controller");
                controller.BusEvents.OnCheckBus -= NodeChange;
                controller.BusEvents.OnBusSplit -= BusSplit;
                return;
            }
            var emitter = block as Emitters;
            if (emitter != null)
            {
                if (!SortedEmitters.Remove(emitter)) return;

                emitter.RegisterEvents(emitter.LocalGrid, false);
                SetMasterEmitter(true, emitter);
                Log.Line("Remove emitter");
                emitter.BusEvents.OnCheckBus -= NodeChange;
                emitter.BusEvents.OnBusSplit -= BusSplit;
                return;
            }
        }

        public void SortAndAddBlock<T>(T block)
        {
            var controller = block as Controllers;
            if (controller != null)
            {
                SortAndAddControllers(controller);
                return;
            }
            var emitter = block as Emitters;
            if (emitter != null)
            {
                SortAndAddEmitters(emitter);
                return;
            }
        }

        public void SortAndAddControllers(Controllers controller)
        {
            if (!SortedControllers.Add(controller)) return;
            Log.Line("add controller");
            controller.BusEvents.OnCheckBus += NodeChange;
            controller.BusEvents.OnBusSplit += BusSplit;
            controller.RegisterEvents(controller.LocalGrid, true);
            ActiveController = SortedControllers.Max;
        }

        public void SortAndAddEmitters(Emitters emitter)
        {
            if (!SortedEmitters.Add(emitter)) return;
            Log.Line("add emitter");
            emitter.BusEvents.OnCheckBus += NodeChange;
            emitter.BusEvents.OnBusSplit += BusSplit;
            emitter.RegisterEvents(emitter.LocalGrid, true);
            ActiveEmitter = SortedEmitters.Max;
        }

        public void SetMasterController(bool check, Controllers controller = null)
        {
            var keepMaster = check && !(ActiveController == null || ActiveController.MyCube.MarkedForClose || !ActiveController.MyCube.InScene || ActiveController == controller);
            if (keepMaster) return;

            var master = SortedControllers.Max;
            if (ActiveController == master) return;
            ActiveController = master;
        }

        public void SetMasterEmitter(bool check, Emitters emitter = null)
        {
            var keepMaster = check && !(ActiveEmitter == null || ActiveEmitter.MyCube.MarkedForClose || !ActiveEmitter.MyCube.InScene || ActiveEmitter == emitter);
            if (keepMaster) return;

            var master = SortedEmitters.Max;
            if (ActiveEmitter == master) return;
            EmitterLos = false;
            EmitterEvent = true;
            ActiveEmitter = master;
        }

        public void RemoveGridsBlocks<T>(SortedSet<T> list, MyCubeGrid grid)
        {
            var tmpArray = new T[list.Count];
            list.CopyTo(tmpArray);
            foreach (var b in tmpArray)
            {
                var controller = b as Controllers;
                if (controller != null && controller.MyCube.CubeGrid == grid)
                {
                    Log.Line($"controller left Master: {MasterGrid.DebugName}");
                    SortedControllers.Remove(controller);
                    controller.BusEvents.Check(controller.MyCube, LogicState.Leave);
                }

                var emitter = b as Emitters;
                if (emitter != null && emitter.MyCube.CubeGrid == grid)
                {
                    Log.Line($"emitter left Master: {MasterGrid.DebugName}");
                    SortedEmitters.Remove(emitter);
                    emitter.BusEvents.Check(emitter.MyCube, LogicState.Leave);
                }
                /*
                var enhancer = b as Enhancers;
                if (enhancer != null) SortedEnhancers.Remove(enhancer);

                var modulator = b as Modulators;
                if (modulator != null) SortedModulators.Remove(modulator);

                var o2Generator = b as O2Generators;
                if (o2Generator != null) SortedO2Generators.Remove(o2Generator);
                */
            }
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
}
}
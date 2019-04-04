using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;

namespace DefenseSystems
{
    public partial class Bus
    {
        public void SortAndAddBlock<T>(T block)
        {
            var controller = block as Controllers;
            var emitter = block as Emitters;

            if (controller != null && !SortedControllers.Add(controller)) return;
            if (emitter != null && !SortedEmitters.Add(emitter)) return;

            UpdateLogicState(block, LogicState.Join);
        }

        public void RemoveBlock<T>(T block)
        {
            var controller = block as Controllers;
            var emitter = block as Emitters;

            if (controller != null && !SortedControllers.Remove(controller)) return;
            if (emitter != null && !SortedEmitters.Remove(emitter)) return;
            UpdateLogicState(block, LogicState.Leave);
        }

        public void RemoveSubBlocks<T>(SortedSet<T> list, MyCubeGrid grid)
        {
            var tmpArray = new T[list.Count];
            list.CopyTo(tmpArray);
            foreach (var b in tmpArray)
            {
                var controller = b as Controllers;
                if (controller != null && controller.MyCube.CubeGrid == grid)
                {
                    Log.Line($"[ControllerSubSplit] - cId:{controller.MyCube.EntityId} - left BusId:{Spine.EntityId} - iMaster:{controller == ActiveController}");
                    SortedControllers.Remove(controller);
                    UpdateNetworks(controller.MyCube, LogicState.Leave);
                }

                var emitter = b as Emitters;
                if (emitter != null && emitter.MyCube.CubeGrid == grid)
                {
                    Log.Line($"[EmitterSubSplit] - cId:{emitter.MyCube.EntityId} - left BusId:{Spine.EntityId} - iMaster:{emitter == ActiveEmitter}");
                    SortedEmitters.Remove(emitter);
                    UpdateNetworks(emitter.MyCube, LogicState.Leave);
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

        private void UpdateLogicState<T>(T type, LogicState state)
        {
            var controller = type as Controllers;
            if (controller != null)
            {
                switch (state)
                {
                    case LogicState.Join:
                        Log.Line($"[ControllerJoin-] - cId:{controller.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{controller == ActiveController}");
                        controller.RegisterEvents(controller.MyCube.CubeGrid, this, true);
                        controller.IsAfterInited = false;
                        break;
                    case LogicState.Leave:
                        Log.Line($"[ControllerLeave] - cId:{controller.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{controller == ActiveController}");
                        controller.RegisterEvents(controller.MyCube.CubeGrid, this,false);
                        break;
                }
            }
            var emitter = type as Emitters;
            if (emitter != null)
            {
                switch (state)
                {
                    case LogicState.Join:
                        Log.Line($"[EmitterJoin----] - cId:{emitter.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{emitter == ActiveEmitter}");

                        emitter.RegisterEvents(emitter.MyCube.CubeGrid, this, true);
                        emitter.IsAfterInited = false;
                        break;
                    case LogicState.Leave:
                        Log.Line($"[EmitterLeave---] - cId:{emitter.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{emitter == ActiveEmitter}");
                        emitter.RegisterEvents(emitter.MyCube.CubeGrid, this, false);
                        break;
                }
            }

            if (Inited) UpdateLogicMasters(type, state);
        }

        internal void UpdateLogicMasters<T>(T type, LogicState state)
        {
            var controller = type as Controllers;
            var emitter = type as Emitters;
            if (controller != null)
            {
                Controllers newMaster;
                switch (state)
                {
                    case LogicState.Join:
                        newMaster = SortedControllers.Max;
                        if (ActiveController == newMaster) return;
                        Log.Line($"[J-ControllerElect] - [iMaster {newMaster == controller}] - state:{state} - myId:{controller.MyCube.EntityId}");
                        ActiveController = newMaster;
                        break;
                    case LogicState.Leave:
                        var keepMaster = !(ActiveController == null || ActiveController.MyCube.Closed || !ActiveController.MyCube.InScene || ActiveController == controller);
                        if (keepMaster) return;

                        newMaster = SortedControllers.Max;
                        if (ActiveController == newMaster) return;
                        Log.Line($"[L-ControllerElect] - [iMaster {newMaster == controller}] - state:{state} - myId:{controller.MyCube.EntityId}");

                        ActiveController = newMaster;
                        break;
                }
            }
            else if (emitter != null)
            {
                Emitters newMaster;
                switch (state)
                {
                    case LogicState.Join:
                        newMaster = SortedEmitters.Max;
                        if (ActiveEmitter == newMaster) return;
                        Log.Line($"[J-Emitter Elect] - [iMaster {newMaster == emitter}] - state:{state} - myId:{emitter.MyCube.EntityId}");
                        ActiveEmitter = newMaster;
                        break;
                    case LogicState.Leave:
                        var keepMaster = !(ActiveEmitter == null || ActiveEmitter.MyCube.Closed || !ActiveEmitter.MyCube.InScene || ActiveEmitter == emitter);
                        if (keepMaster) return;

                        newMaster = SortedEmitters.Max;
                        if (ActiveEmitter == newMaster) return;
                        Log.Line($"[L-Emitter Elect] - [iMaster {newMaster == emitter}] - state:{state} - myId:{emitter.MyCube.EntityId}");
                        ActiveEmitter = newMaster;
                        break;
                }
                EmitterEvent = true;
                ActiveEmitterId = ActiveEmitter?.MyCube?.EntityId ?? 0;
            }
        }

        private void UpdateNetworks<T>(T type, LogicState state)
        {
            var cube = type as MyCubeBlock;
            var controller = cube?.GameLogic as Controllers;
            var emitter = cube?.GameLogic as Emitters;
            var newSplit = false;

            if (controller != null)
            {
                if (state == LogicState.Leave)
                {
                    if (ActiveController == controller)
                    {
                        Log.Line($"[DeRegistering] - from spine [sId:{Spine.EntityId}] as active controller [cId:{controller.MyCube.EntityId}]");
                        newSplit = true;
                        ActiveController = null;
                        UpdateLogicMasters(type, state);
                    }
                    Log.Line($"[Join New Bus] - cId:{controller.MyCube.EntityId}]");
                    controller.Registry.RegisterWithBus(controller, controller.MyCube.CubeGrid, true, controller.Bus, out controller.Bus);
                }
            }
            else if (emitter != null)
            {
                if (state == LogicState.Leave)
                {
                    if (ActiveEmitter == emitter)
                    {
                        Log.Line($"[DeRegistering] - from spine [sId:{Spine.EntityId}] as active controller [eId:{emitter.MyCube.EntityId}]");
                        newSplit = true;
                        ActiveEmitter = null;
                        UpdateLogicMasters(type, state);
                    }
                    Log.Line($"[Join New Bus] - eId:{emitter.MyCube.EntityId}]");
                    emitter.Registry.RegisterWithBus(emitter, emitter.MyCube.CubeGrid, true, emitter.Bus, out emitter.Bus);
                }
            }
            if (!BusIsSplit && newSplit) Events.Split(cube.CubeGrid, state);
        }
    }
}
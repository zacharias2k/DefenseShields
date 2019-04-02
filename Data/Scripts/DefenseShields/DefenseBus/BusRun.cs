using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.Components;
namespace DefenseSystems
{
    public partial class DefenseBus : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
            }
            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public override string ComponentTypeDebugString
        {
            get { return "DefenseBus"; }
        }

        public void GridLeaving(MyCubeGrid grid)
        {
            Log.Line("grid leave event");
            RemoveGridsBlocks(SortedControllers, grid);
            RemoveGridsBlocks(SortedEmitters, grid);
        }

        public void NodeChange<T>(T logic, LogicState state)
        {
            var cube = logic as MyCubeBlock;
            if (cube == null) return;
            var leave = state == LogicState.Leave;
            var splitBus = false;
            var controller = cube.GameLogic as Controllers;
            if (controller != null)
            {
                if (leave)
                {
                    if (ActiveController == controller)
                    {
                        splitBus = true;
                        ActiveController = null;
                    }
                    controller.MarkForReset = true;
                }
            }
            var emitter = cube.GameLogic as Emitters;
            if (emitter != null)
            {
                if (leave)
                {
                    if (ActiveEmitter == emitter)
                    {
                        splitBus = true;
                        ActiveEmitter = null;
                    }
                    emitter.Registry.RegisterWithBus(emitter, emitter.MyCube.CubeGrid, true, emitter.DefenseBus, out emitter.DefenseBus);
                }
            }

            if (splitBus)
            {
                SetMasterController(false);
                SetMasterEmitter(false);
            }
            Log.Line($"CheckBus: state:{state} - controller:{controller != null}[{ActiveController == controller}] - emitter:{emitter != null}[{ActiveEmitter == emitter}] - connected:{SubGrids.Contains(cube.CubeGrid)}");
        }

        public void BusSplit<T>(T logic, T type2)
        {
            var cube = logic as MyCubeBlock;
            if (cube == null) return;

            var controller = cube.GameLogic as Controllers;
            if (controller != null) ;
            var emitter = cube.GameLogic as Emitters;
            if (emitter != null) ;
            //Log.Line($"CheckBus: state:{state} - controller:{controller != null}[{ActiveController == controller}] - emitter:{emitter != null}[{ActiveEmitter == emitter}] - connected:{SubGrids.Contains(cube.CubeGrid)}");
        }
    }
}

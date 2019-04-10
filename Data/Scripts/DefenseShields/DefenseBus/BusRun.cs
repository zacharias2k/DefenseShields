using DefenseSystems.Support;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace DefenseSystems
{
    public partial class Bus : MyEntityComponentBase
    {
        public void Init(bool reset = false)
        {
            if (Inited && !reset) return;
            SetSpine(false);
            ActiveEmitter = SortedEmitters.Max;
            ActiveController = SortedControllers.Max;
            UpdateLogicMasters(ActiveEmitter, LogicState.Active);
            UpdateLogicMasters(ActiveController, LogicState.Active);
            var busHealthy = Spine != null && ActiveEmitter != null && ActiveController != null;
            if (busHealthy) Log.Line($"[BusInitComplete] - Bus:{Spine.DebugName} - ActiveController:{ActiveController.MyCube.EntityId} - ActiveEmitter:{ActiveEmitter.MyCube.EntityId}");
            else Log.Line($"[BusInitComplete] - Not fully populated");
            Inited = true;
            return;
        }

        public void Split(MyEntity type, Bus.LogicState state)
        {
            Log.Line("[Bus Has Split--]");
            OnBusSplit?.Invoke(type, state);
        }

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
            get { return "Bus"; }
        }
    }
}

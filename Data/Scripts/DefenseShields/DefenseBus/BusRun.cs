using DefenseSystems.Support;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace DefenseSystems
{
    internal partial class Bus : MyEntityComponentBase
    {
        public void Init(bool reset = false)
        {
            if (Inited && !reset) return;
            SetSpine(false);
            ActiveEmitter = SortedEmitters.Max;
            ActiveController = SortedControllers.Max;
            ActiveRegen = SortedRegens.Max;
            UpdateLogicMasters(ActiveEmitter, LogicState.Init);
            UpdateLogicMasters(ActiveController, LogicState.Init);
            UpdateLogicMasters(ActiveRegen, LogicState.Init);
            var busHealthy = Spine != null && ActiveController != null && (ActiveEmitter != null || ActiveRegen != null);
            if (busHealthy) Log.Line($"[BusInitComplete] - Bus:{Spine.DebugName} - ActiveController:{ActiveController.MyCube.EntityId}");
            else Log.Line($"[BusInitComplete] - Not fully populated");
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;
            Inited = true;
        }

        internal void Split(MyEntity type, LogicState state)
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

using System.Collections.Generic;
using VRage.Game.Components;

namespace DefenseShields
{
    public class EmitterGridComponent : MyEntityComponentBase
    {
        private static List<EmitterGridComponent> gridEmitters = new List<EmitterGridComponent>();
        public Emitters PrimeComp;
        public Emitters BetaComp;

        public EmitterGridComponent(Emitters emitter, bool prime)
        {
            if (prime) PrimeComp = emitter;
            else BetaComp = emitter;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridEmitters.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridEmitters.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridEmitters.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridEmitters.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public HashSet<Emitters> RegisteredComps { get; set; } = new HashSet<Emitters>();

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

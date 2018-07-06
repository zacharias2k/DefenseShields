using System.Collections.Generic;
using VRage.Game.Components;

namespace DefenseShields
{
    public class O2GeneratorGridComponent : MyEntityComponentBase
    {
        private static List<O2GeneratorGridComponent> gridO2Generator = new List<O2GeneratorGridComponent>();
        public O2Generators Comp;

        public O2GeneratorGridComponent(O2Generators o2Generator)
        {
            Comp = o2Generator;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridO2Generator.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridO2Generator.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridO2Generator.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridO2Generator.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public HashSet<O2Generators> RegisteredComps { get; set; } = new HashSet<O2Generators>();

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

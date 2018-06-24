using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace DefenseShields
{
    public class ModulatorGridComponent : MyEntityComponentBase
    {
        private static List<ModulatorGridComponent> gridModulator = new List<ModulatorGridComponent>();
        public readonly Modulators Modulators;
        public string Password;
        public bool Enabled;
        public bool Voxels;
        public bool Grids;


        public ModulatorGridComponent(Modulators modulators)
        {
            Modulators = modulators;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridModulator.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridModulator.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridModulator.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridModulator.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public HashSet<IMyCubeGrid> GetSubGrids { get; set; } = new HashSet<IMyCubeGrid>();

        public string ModulationPassword
        {
            get { return Password; }
            set { Password = value; }
        }

        public bool ModulationEnabled
        {
            get { return Enabled; }
            set { Enabled = value; }
        }

        public bool ModulateVoxels
        {
            get { return Voxels; }
            set { Voxels = value; }
        }

        public bool ModulateGrids
        {
            get { return Grids; }
            set { Grids = value; }
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

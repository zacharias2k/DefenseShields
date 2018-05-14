using Sandbox.Game.EntityComponents;
using VRage.Game;

namespace DefenseShields
{
    public class ResourceTracker
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public readonly MyDefinitionId ResourceId;

        public ResourceTracker(MyDefinitionId resourceId)
        {
            ResourceId = resourceId;
        }

        public void TrackSource(MyResourceSourceComponent source)
        {
            source.OutputChanged += (id, oldOutput, component) =>
            {
                if (id == ResourceId)
                {
                    Current -= oldOutput;
                    Current += component.CurrentOutputByType(ResourceId);
                }
            };

            source.MaxOutputChanged += (id, oldOutput, component) =>
            {
                if (id == ResourceId)
                {
                    Max -= oldOutput;
                    Max += component.MaxOutputByType(ResourceId);
                }
            };
        }
    }
}

using VRage.ModAPI;

namespace DefenseShields.Support
{
    class EntIntersectInfo
    {
        public uint LastTick;
        public readonly uint FirstTick;
        public readonly IMyEntity Entity;
        public readonly bool SpawnedInside;
        public readonly bool Stuck;

        public EntIntersectInfo(IMyEntity entity, uint firstTick, uint lastTick, bool inside, bool stuck)
        {
            Entity = entity;
            FirstTick = firstTick;
            LastTick = lastTick;
            SpawnedInside = inside;
            Stuck = stuck;
        }
    }
}

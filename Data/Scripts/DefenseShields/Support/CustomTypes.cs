using VRage.ModAPI;

namespace DefenseShields.Support
{
    public class EntIntersectInfo
    {
        public uint LastTick;
        public readonly uint FirstTick;
        public readonly int Relation;
        public readonly bool SpawnedInside;
        public readonly bool Stuck;

        public EntIntersectInfo(uint firstTick, uint lastTick, int relation, bool inside)
        {
            FirstTick = firstTick;
            LastTick = lastTick;
            Relation = relation;
            SpawnedInside = inside;
        }
    }
}

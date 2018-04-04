using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    public class EntIntersectInfo
    {
        public readonly long EntId;
        public uint LastTick;
        public readonly uint FirstTick;
        public readonly DefenseShields.Ent Relation;
        public readonly bool SpawnedInside;
        public List<IMySlimBlock> CacheBlockList;

        public EntIntersectInfo(long entId, uint firstTick, uint lastTick, DefenseShields.Ent relation, bool inside, List<IMySlimBlock> cacheBlockList)
        {
            CacheBlockList = cacheBlockList;
            EntId = entId;
            FirstTick = firstTick;
            LastTick = lastTick;
            Relation = relation;
            SpawnedInside = inside;
        }
    }

    public class ShieldGridComponent : MyEntityComponentBase
    {
        private static List<ShieldGridComponent> gridShield = new List<ShieldGridComponent>();
        public readonly DefenseShields DefenseShields;

        public ShieldGridComponent(DefenseShields defenseShields)
        {
            DefenseShields = defenseShields;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridShield.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridShield.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridShield.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridShield.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace DefenseShields.Support
{
    public class EntIntersectInfo
    {
        public readonly long EntId;
        public uint LastTick;
        public readonly uint FirstTick;
        public readonly int Relation;
        public readonly bool SpawnedInside;

        public EntIntersectInfo(long entId, uint firstTick, uint lastTick, int relation, bool inside)
        {
            EntId = entId;
            FirstTick = firstTick;
            LastTick = lastTick;
            Relation = relation;
            SpawnedInside = inside;
        }
    }

    public class ShieldGridComponent : MyEntityComponentBase
    {
        private static List<ShieldGridComponent> m_shields = new List<ShieldGridComponent>();
        private readonly IMyCubeBlock Block;

        public ShieldGridComponent(IMyCubeBlock block)
        {
            Block = block;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                m_shields.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                m_shields.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            m_shields.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            m_shields.Remove(this);

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

        public bool ShieldActive()
        {
            return Block.IsFunctional && Block.IsWorking;
        }
    }
}

using System.Collections.Generic;
using VRage.Game.Components;
using VRageMath;

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
        private static List<ShieldGridComponent> gridShield = new List<ShieldGridComponent>();
        public readonly DefenseShields DS;

        public ShieldGridComponent(DefenseShields ds)
        {
            DS = ds;
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

        public void ComponentNames()
        {
            foreach (var shield in gridShield)
            {
                Log.Line(
                    $"{shield.DS.Block.CubeGrid.DisplayName} {shield.DS.ShieldSize.Max()} {shield.DS.ShieldActive} {shield.DS.Block.CubeGrid.Physics.LinearVelocity}");
            }
        }
    }
}

using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace DefenseShields.Support
{
    public class AmmoInfo
    {
        public readonly bool Explosive;
        public readonly float Damage;
        public readonly float Radius;
        public readonly float Speed;
        public readonly float Mass;
        public readonly float BackKickForce;

        public AmmoInfo(bool explosive, float damage, float radius, float speed, float mass, float backKickForce)
        {
            Explosive = explosive;
            Damage = damage;
            Radius = radius;
            Speed = speed;
            Mass = mass;
            BackKickForce = backKickForce;
        }
    }

    public class BlockDamageInfo
    {
        public MyEntity Entity;
        public Vector3I Vector;
        public bool NormalDamage;
        public bool Deformation;
        public int Count;
        public BlockDamageInfo(MyEntity entity, Vector3I vector, bool normalDamage, bool deformation, int count)
        {
            Entity = entity;
            NormalDamage = normalDamage;
            Deformation = deformation;
            Vector = vector;
            Count = count;
        }
    }

    public struct ShieldHit
    {
        public readonly IMySlimBlock Block;
        public readonly float Amount;
        public readonly MyEntity Attacker;
        public readonly MyStringHash Type;

        public ShieldHit(IMySlimBlock block, float amount, MyEntity attacker, MyStringHash type)
        {
            Block = block;
            Amount = amount;
            Attacker = attacker;
            Type = type;
        }
    }

    public class EntIntersectInfo
    {
        public readonly long EntId;
        public float Damage;
        public Vector3D ContactPoint;
        public uint LastTick;
        public readonly uint FirstTick;
        public readonly DefenseShields.Ent Relation;
        public readonly bool SpawnedInside;
        public List<IMySlimBlock> CacheBlockList;
        public readonly MyStorageData TempStorage;

        public EntIntersectInfo(long entId, float damage, Vector3D contactPoint, uint firstTick, uint lastTick, DefenseShields.Ent relation, bool inside, List<IMySlimBlock> cacheBlockList, MyStorageData tempStorage)
        {
            CacheBlockList = cacheBlockList;
            EntId = entId;
            Damage = damage;
            ContactPoint = contactPoint;
            FirstTick = firstTick;
            LastTick = lastTick;
            Relation = relation;
            SpawnedInside = inside;
            TempStorage = tempStorage;
        }
    }

    public class ShieldGridComponent : MyEntityComponentBase
    {
        private static List<ShieldGridComponent> gridShield = new List<ShieldGridComponent>();
        public readonly DefenseShields DefenseShields;
        public string Password;

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

        public string ModulationPassword
        {
            get { return Password; }
            set { Password = value; }
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }

    public class ModulatorGridComponent : MyEntityComponentBase
    {
        private static List<ModulatorGridComponent> gridModulator = new List<ModulatorGridComponent>();
        public readonly Modulators Modulators;
        public string Password;

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

        public string ModulationPassword
        {
            get { return Password; }
            set { Password = value; }
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

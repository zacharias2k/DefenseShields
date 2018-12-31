namespace DefenseShields.Support
{
    using System.Collections.Generic;
    using VRage.Collections;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public struct ShieldHit
    {
        public readonly MyEntity Attacker;
        public readonly float Amount;
        public readonly MyStringHash DamageType;
        public readonly Vector3D HitPos;

        public ShieldHit(MyEntity attacker, float amount, MyStringHash damageType, Vector3D hitPos)
        {

            Attacker = attacker;
            Amount = amount;
            DamageType = damageType;
            HitPos = hitPos;
        }
    }

    public struct MoverInfo
    {
        public readonly Vector3D Pos;
        public readonly uint CreationTick;
        public MoverInfo(Vector3D pos, uint creationTick)
        {
            Pos = pos;
            CreationTick = creationTick;
        }
    }

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
    /*

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
    */

    public class ProtectCache
    {
        public uint LastTick;
        public uint RefreshTick;
        public readonly uint FirstTick;
        public DefenseShields.Ent Relation;
        public DefenseShields.Ent PreviousRelation;

        public ProtectCache(uint firstTick, uint lastTick, uint refreshTick, DefenseShields.Ent relation, DefenseShields.Ent previousRelation)
        {
            FirstTick = firstTick;
            LastTick = lastTick;
            RefreshTick = refreshTick;
            Relation = relation;
            PreviousRelation = previousRelation;
        }
    }

    public class EntIntersectInfo
    {
        public float Damage;
        public double EmpSize;
        public bool Touched;
        public BoundingBox Box;

        public Vector3D ContactPoint;
        public Vector3D EmpDetonation;
        public uint LastTick;
        public uint RefreshTick;
        public uint BlockUpdateTick;
        public readonly uint FirstTick;
        public DefenseShields.Ent Relation;
        public List<IMySlimBlock> CacheBlockList;

        public EntIntersectInfo(float damage, double empSize, bool touched, BoundingBox box, Vector3D contactPoint, Vector3D empDetonation, uint firstTick, uint lastTick, uint refreshTick, uint blockUpdateTick, DefenseShields.Ent relation, List<IMySlimBlock> cacheBlockList)
        {
            CacheBlockList = cacheBlockList;
            Damage = damage;
            EmpSize = empSize;
            Touched = touched;
            Box = box;
            ContactPoint = contactPoint;
            EmpDetonation = empDetonation;
            FirstTick = firstTick;
            LastTick = lastTick;
            RefreshTick = refreshTick;
            BlockUpdateTick = blockUpdateTick;
            Relation = relation;
        }
    }

    public class MyProtectors
    {
        public readonly CachingHashSet<DefenseShields> Shields = new CachingHashSet<DefenseShields>();
        public int RefreshSlot;
        public uint CreationTick;
        public DefenseShields IntegrityShield;

        // Damage Handler Field Cache
        internal DefenseShields BlockingShield = null;
        internal DefenseShields ClientExpShield = null;
        internal IMySlimBlock OriginBlock = null;
        internal long IgnoreAttackerId;
        internal Vector3D OriginHit;
        //

        public void Init(int refreshSlot, uint creationTick)
        {
            RefreshSlot = refreshSlot;
            CreationTick = creationTick;
        }

        public void CleanUp()
        {
            Shields.Clear();
            RefreshSlot = 0;
            CreationTick = 0;
            IntegrityShield = null;
        }
    }
}

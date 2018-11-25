using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
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
        public double EmpSize;
        public bool Touched;
        public BoundingBox Box;

        public Vector3D ContactPoint;
        public Vector3D EmpDetonation;
        public uint LastTick;
        public uint RefreshTick;
        public readonly uint FirstTick;
        public DefenseShields.Ent Relation;
        public List<IMySlimBlock> CacheBlockList;

        public EntIntersectInfo(long entId, float damage, double empSize, bool touched, BoundingBox box, Vector3D contactPoint, Vector3D empDetonation, uint firstTick, uint lastTick, uint refreshTick, DefenseShields.Ent relation, List<IMySlimBlock> cacheBlockList)
        {
            CacheBlockList = cacheBlockList;
            EntId = entId;
            Damage = damage;
            EmpSize = empSize;
            Touched = touched;
            Box = box;
            ContactPoint = contactPoint;
            EmpDetonation = empDetonation;
            FirstTick = firstTick;
            LastTick = lastTick;
            RefreshTick = refreshTick;
            Relation = relation;
        }
    }

    public struct MyProtectors
    {
        public readonly Dictionary<DefenseShields, ProtectorInfo> Shields;
        public readonly int RefreshSlot;
        public readonly uint CreationTick;
        public MyProtectors(Dictionary<DefenseShields, ProtectorInfo> shields, int refreshSlot, uint creationTick)
        {
            Shields = shields;
            RefreshSlot = refreshSlot;
            CreationTick = creationTick;
        }
    }

    public struct ProtectorInfo
    {
        public readonly bool GridIsParent;
        public readonly bool FullCoverage;
        public ProtectorInfo(bool gridIsParent, bool fullCoverage)
        {
            GridIsParent = gridIsParent;
            FullCoverage = fullCoverage;
        }
    }
}

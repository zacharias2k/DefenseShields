namespace DefenseShields.Support
{
    using System;
    using System.Collections.Generic;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Collections;
    using VRage.Game;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.ModAPI;
    using VRage.Utils;
    using VRage.Voxels;
    using VRageMath;

    public struct WarHeadBlast
    {
        public readonly int WarSize;
        public readonly double Yield;
        public readonly Vector3D Position;
        public readonly string CustomData;

        public WarHeadBlast(int warSize, Vector3D position, string customData)
        {
            WarSize = warSize;
            Yield = WarSize * 50;
            Position = position;
            CustomData = customData;
        }
    }

    public struct WarHeadHit
    {
        public readonly uint Duration;
        public BoundingSphereD Sphere;

        public WarHeadHit(BoundingSphereD sphere, uint duration)
        {
            Sphere = sphere;
            Duration = duration;
        }
    }

    public struct VoxelHit : IVoxelOperator
    {
        public bool HasHit;

        public void Op(ref Vector3I pos, MyStorageDataTypeEnum dataType, ref byte content)
        {
            if (content != MyVoxelConstants.VOXEL_CONTENT_EMPTY)
            {
                HasHit = true;
            }
        }
        /*
        public VoxelOperatorFlags Flags
        {
            get { return VoxelOperatorFlags.Read; }
        }
        */
    }

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

    public struct DamageCheck
    {
        public readonly float Damage;
        public readonly long AttackerId;
        public readonly MyStringHash DamageType;
        public DamageCheck(float damage, long attackerId, MyStringHash damageType)
        {
            Damage = damage;
            AttackerId = attackerId;
            DamageType = damageType;
        }
    }

    public struct MonitorBlock : IEquatable<MonitorBlock>
    {
        public readonly IMySlimBlock Block;
        public readonly float Damage;
        public readonly MyStringHash DamageType;
        public readonly uint Tick;

        internal MonitorBlock(IMySlimBlock block, float damage, MyStringHash damageType, uint tick)
        {
            Block = block;
            Damage = damage;
            DamageType = damageType;
            Tick = tick;
        }

        public bool Equals(MonitorBlock other)
        {
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                result = (result * 397) ^ Block.Position.GetHashCode();
                result = (result * 397) ^ DamageType.GetHashCode();
                result = (result * 397) ^ Damage.GetHashCode();
                return result;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is MonitorBlock && Equals((MonitorBlock)obj);
        }
    }

    public struct BlockState
    {
        public readonly MyCubeBlock CubeBlock;
        public readonly IMyFunctionalBlock FunctBlock;
        public readonly bool EnableState;
        public readonly uint StartTick;
        public readonly uint Endtick;
        public BlockState(MyCubeBlock cubeBlock, uint startTick, uint endTick)
        {
            CubeBlock = cubeBlock;
            StartTick = startTick;
            Endtick = endTick;
            FunctBlock = cubeBlock as IMyFunctionalBlock;
            EnableState = ((IMyFunctionalBlock)cubeBlock).Enabled;
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

    public class AmmoInfo2
    {
        public readonly bool Explosive;
        public readonly float Damage;
        public readonly float Radius;
        public readonly float Speed;
        public readonly float Mass;
        public readonly float BackKickForce;

        public readonly bool KineticWeapon; //0 is energy, 1 is kinetic
        public readonly bool HealingWeapon; //0 is damaging, 1 is healing
        public readonly bool BypassWeapon; //0 is normal, 1 is bypass
        public readonly float DmgMulti;
        public readonly float ShieldDamage;

        public AmmoInfo2(bool explosive, float damage, float radius, float speed, float mass, float backKickForce)
        {
            Explosive = explosive;
            Damage = damage;
            Radius = radius;
            Speed = (float)Math.Truncate(speed);
            Mass = mass;
            BackKickForce = (float)Math.Truncate(backKickForce);

            var backCompat = UtilsStatic.GetDmgMulti(backKickForce);
            if (Math.Abs(backCompat) >= 0.001) //back compat, != 0 might get weird
            {
                KineticWeapon = !Explosive;
                BypassWeapon = false;
                DmgMulti = backCompat;
                if (Mass < 0 && Radius <= 0) //ye olde heal check
                    HealingWeapon = true;
            }
            else if (BackKickForce < 0) //emulates the weirdest old behavior
            {
                KineticWeapon = !Explosive;
                BypassWeapon = false;
                DmgMulti = 0;
                ShieldDamage = float.NegativeInfinity; //bls gob no
                if (Mass < 0 && Radius <= 0)
                    ShieldDamage = -ShieldDamage;
                return;
            }
            else //new API
            {
                var slice = Math.Abs(backKickForce - Math.Truncate(backKickForce)) * 10;
                var opNum = (int)Math.Truncate(slice); ////gets first decimal digit
                DmgMulti = (float)Math.Truncate((slice - Math.Truncate(slice)) * 10); ////gets second decimal digit
                var uuid = (int)Math.Round(Math.Abs(speed - Math.Truncate(speed)) * 1000); ////gets UUID

                if (uuid != 537 || backKickForce >= 131072 || speed >= 16384)
                {   ////confirms UUID or if backkick/speed are out of range of float precision
                    KineticWeapon = !Explosive;
                    HealingWeapon = false;
                    BypassWeapon = false;
                    DmgMulti = 1;
                }
                else if (opNum == 8) ////8 is bypass, ignores all other flags
                {
                    KineticWeapon = !Explosive;
                    HealingWeapon = false;
                    BypassWeapon = true;
                    DmgMulti = 1;
                }
                else //eval flags
                {
                    if (Convert.ToBoolean(opNum & 1))  ////bitcheck first bit; 0 is fractional, 1 is whole num
                    {
                        if (Math.Abs(DmgMulti) <= 0.001d) ////fractional and mult 0 = no damage
                            DmgMulti = 10;
                    }
                    else DmgMulti /= 10;
                    KineticWeapon = Convert.ToBoolean(opNum & 2); //second bit; 0 is energy, 1 is kinetic
                    HealingWeapon = Convert.ToBoolean(opNum & 4); //third bit; 0 is damaging, 1 is healing
                }
            }

            if (Explosive)
                ShieldDamage = (Damage * (Radius * 0.5f)) * 7.5f * DmgMulti;
            else
                ShieldDamage = Mass * Speed * DmgMulti;
            //  shieldDamage = Mass * Math.Pow(Speed,2) * DmgMulti / 2; //kinetic equation
            if (HealingWeapon)
                ShieldDamage = -ShieldDamage;
        }
    }

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
        public DefenseShields BlockingShield = null;
        public DefenseShields NotBlockingShield = null;
        public long NotBlockingAttackerId = -1;
        public MyStringHash NotBlockingMainDamageType;
        public IMySlimBlock OriginBlock = null;
        public long IgnoreAttackerId = -1;
        public Vector3D OriginHit;

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
            NotBlockingShield = null;
            NotBlockingAttackerId = -1;
            IgnoreAttackerId = -1;
            OriginBlock = null;
            OriginHit = Vector3D.Zero;
        }

        public void ProtectDamageReset()
        {
            IntegrityShield = null;
            NotBlockingShield = null;
            NotBlockingAttackerId = -1;
            IgnoreAttackerId = -1;
            OriginBlock = null;
            OriginHit = Vector3D.Zero;
            NotBlockingMainDamageType = MyStringHash.NullOrEmpty;
        }
    }
}

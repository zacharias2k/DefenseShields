using System.Collections.Generic;
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
        public List<IMySlimBlock> CacheBlockList;
        public readonly MyStorageData TempStorage;

        public EntIntersectInfo(long entId, float damage, Vector3D contactPoint, uint firstTick, uint lastTick, DefenseShields.Ent relation, List<IMySlimBlock> cacheBlockList, MyStorageData tempStorage)
        {
            CacheBlockList = cacheBlockList;
            EntId = entId;
            Damage = damage;
            ContactPoint = contactPoint;
            FirstTick = firstTick;
            LastTick = lastTick;
            Relation = relation;
            TempStorage = tempStorage;
        }
    }

    public class ShieldGridComponent : MyEntityComponentBase
    {
        private static List<ShieldGridComponent> gridShield = new List<ShieldGridComponent>();
        public HashSet<IMyCubeGrid> SubGrids = new HashSet<IMyCubeGrid>();
        public HashSet<Emitters> Emitters = new HashSet<Emitters>();
        public readonly DefenseShields DefenseShields;

        public string Password;
        public bool GridIsMobile;
        public bool BlockWorking;
        public bool IsMoving;
        public bool IsStarting;
        public bool Warm;
        public bool EmitterUpdate;
        public bool CheckEmitters;

        public double Range;
        public Vector3D[] PhysicsHigh = new Vector3D[642];
        public Vector3D[] PhysicsLow = new Vector3D[162];
        public Vector3D[] PhysicsIn = new Vector3D[642];

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

        public HashSet<IMyCubeGrid> GetSubGrids
        {
            get { return SubGrids; }
            set { SubGrids = value; }
        }

        public HashSet<Emitters> RegisteredEmitters
        {
            get { return Emitters; }
            set { Emitters = value; }
        }

        public string ModulationPassword
        {
            get { return Password; }
            set { Password = value; }
        }

        public bool ControlBlockWorking
        {
            get { return BlockWorking; }
            set { BlockWorking = value; }
        }

        public bool GridIsMoving
        {
            get { return IsMoving; }
            set { IsMoving = value; }
        }

        public bool ShieldIsStarting
        {
            get { return IsStarting; }
            set { IsStarting = value; }
        }

        public bool WarmedUp
        {
            get { return Warm; }
            set { Warm = value; }
        }

        public bool EmitterEvent
        {
            get { return EmitterUpdate; }
            set { EmitterUpdate = value; }
        }

        public double BoundingRange
        {
            get { return Range; }
            set { Range = value; }
        }

        public Vector3D[] PhysicsOutside
        {
            get { return PhysicsHigh; }
            set { PhysicsHigh = value; }
        }

        public Vector3D[] PhysicsOutsideLow
        {
            get { return PhysicsLow; }
            set { PhysicsLow = value; }
        }

        public Vector3D[] PhysicsInside
        {
            get { return PhysicsIn; }
            set { PhysicsIn = value; }
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
        public bool Enabled;
        public bool Voxels;
        public bool Grids;


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

        public bool ModulationEnabled
        {
            get { return Enabled; }
            set { Enabled = value; }
        }

        public bool ModulateVoxels
        {
            get { return Voxels; }
            set { Voxels = value; }
        }

        public bool ModulateGrids
        {
            get { return Grids; }
            set { Grids = value; }
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

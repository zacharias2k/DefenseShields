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
        public DefenseShields DefenseShields;

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

        public HashSet<IMyCubeGrid> GetSubGrids { get; set; } = new HashSet<IMyCubeGrid>();

        public Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];

        public Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        public Vector3D[] PhysicsInside { get; set; } = new Vector3D[642];

        public EmitterGridComponent EmitterComp { get; set; }

        public string ModulationPassword { get; set; }

        public bool EmittersWorking { get; set; } = true;

        public bool ShieldActive { get; set; }

        public bool CheckEmitters { get; set; }

        public bool GridIsMoving { get; set; }

        public bool ShieldIsStarting { get; set; }

        public bool Warming { get; set; }

        public bool EmitterEvent { get; set; }

        public float ShieldPercent { get; set; }

        public double BoundingRange { get; set; }

        public double ShieldVelocitySqr { get; set; }

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
	public class EmitterGridComponent : MyEntityComponentBase
    {
        private static List<EmitterGridComponent> gridEmitters = new List<EmitterGridComponent>();
        public Emitters PrimeComp;
        public Emitters BetaComp;

        public EmitterGridComponent(Emitters emitter, bool prime)
        {
            if (prime) PrimeComp = emitter;
            else BetaComp = emitter;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridEmitters.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridEmitters.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridEmitters.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridEmitters.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public HashSet<Emitters> RegisteredComps { get; set; } = new HashSet<Emitters>();

        public bool PerformEmitterDiagnostic { get; set; }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}

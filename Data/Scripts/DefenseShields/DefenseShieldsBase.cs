using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using DefenseShields.Support;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public bool IsInit;
        public bool ControlsLoaded;
        //public int I;

        public static DefenseShieldsBase Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly Icosphere Icosphere = new Icosphere(4);

        public readonly List<DefenseShields> Components = new List<DefenseShields>();
        public List<DefenseShields> Shields = new List<DefenseShields>(); 

        //private readonly MyStringId _faceId = MyStringId.GetOrCompute("Build new");


        public override void Draw()
        {
            /*
            if (I < 60)
            {
                I++;
                for (var j = 0; j < 32768; j++) MyTransparentGeometry.AddTriangleBillboard(Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, _faceId, 0, Vector3D.Zero);
            }
            */
            foreach (var s in Components)
            {
                s.Draw();
            }
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }
        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            if (IsInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public void Init() 
        {
            Log.Init("debugdevelop.log");
            Log.Line($" Logging Started");
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            IsInit = true;
        }

        public void CheckDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // fix
            {
            }

            if (Shields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            var generator = Shields[0];
            var ent = block as IMyEntity;
            var slimBlock = block as IMySlimBlock;
            if (slimBlock != null) ent = slimBlock.CubeGrid;
            var dude = block as IMyCharacter;
            if (dude != null) ent = dude;
            if (ent == null) return;
            var isProtected = false;
            foreach (var shield in Shields)
                if (shield.InHash.Contains(ent))
                {
                    isProtected = true;
                    generator = shield;
                }
            if (!isProtected) return;
            IMyEntity attacker;
            if (!MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out attacker)) return;
            if (generator.InHash.Contains(attacker)) return;
            info.Amount = 0f;
        }

        //These are the subpart paths for every type of weapon.Then the last subpart has a dummy 
        //with "muzzle_projectile", "muzzle_missile", or "barrel".  The forward direction of this 
        //dummy is the way it shoots, the position is where it shoots from:
        /*
        public static DirectionBarrelComponent CreateAuto(IMyEntity ent)
        {
            if (ent is IMyLargeGatlingTurret)
                return new DirectionBarrelComponent("GatlingTurretBase1", "GatlingTurretBase2", "GatlingBarrel");
            if (ent is IMyLargeInteriorTurret)
                return new DirectionBarrelComponent("InteriorTurretBase1", "InteriorTurretBase2");
            if (ent is IMyLargeMissileTurret)
                return new DirectionBarrelComponent("MissileTurretBase1", "MissileTurretBarrels");
            if (ent is IMySmallGatlingGun)
                return new DirectionBarrelComponent("Barrel");
            if (ent is IMySmallMissileLauncher)
                return new DirectionBarrelComponent();
            return null;
        }
        private bool IsShooting
        {
            get
            {
                IMyUserControllableGun gun = Entity;
                if (gun == null)
                    return false;
                if (gun.IsShooting)
                    return true;
                var gunBase = gun as IMyGunObject<MyGunBase>;
                if (gunBase != null && gunBase.IsShooting)
                    return true;
                return _blockShootProperty?.GetValue(gun) ?? false;
            }
        }

        _blockShootProperty = tBlock.GetProperty("Shoot").Cast<bool>();
        */
    }
    #endregion
}

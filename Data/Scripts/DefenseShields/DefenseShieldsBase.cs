using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public static bool IsInit;
        private static List<DefenseShields> _bulletShields = new List<DefenseShields>(); // check 
        public static bool ControlsLoaded;
        public int i = 0;
        private readonly MyStringId _faceId = MyStringId.GetOrCompute("Build new");


        // Initialisation

        public override void Draw()
        {
            if (i < 60)
            {
                i++;
                for (var j = 0; j < 32768; j++) MyTransparentGeometry.AddTriangleBillboard(Vector3D.Zero, Vector3D.Zero, Vector3D.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, _faceId, 0, Vector3D.Zero);
            }
        }

        protected override void UnloadData()
        {
            Log.Line("Logging stopped.");
            Log.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            if (IsInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public static void Init()
        {
            Log.Init("debugdevelop.log");
            Log.Line($" Logging Started");
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            IsInit = true;
        }

        // Prevent damage by bullets fired from outside zone.

        public static void CheckDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // move below, modify match Type to 
            {
            }

            if (_bulletShields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            var generator = _bulletShields[0];
            var ent = block as IMyEntity;
            var slimBlock = block as IMySlimBlock;
            if (slimBlock != null) ent = slimBlock.CubeGrid;
            var dude = block as IMyCharacter;
            if (dude != null) ent = dude;
            if (ent == null) return;
            var isProtected = false;
            foreach (var shield in _bulletShields)
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

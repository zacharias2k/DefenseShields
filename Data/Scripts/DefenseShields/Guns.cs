using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeTurretBaseDefinition), false, new string[] { "Guns" })]
    class Guns : MyGameLogicComponent
    {
        private IMyLargeTurretBase Entity;
        private long _lastShotTime;
        private ITerminalProperty<bool> _blockShootProperty;

        private IMyTerminalBlock _tblock;

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            //Entity.Components.TryGet(out Sink);
            //Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            //NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            //this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            _tblock = Entity; // as IMyTerminalBlock;
        }
        #endregion
        //These are the subpart paths for every type of weapon.  Then the last subpart has a dummy with 
        //"muzzle_projectile", "muzzle_missile", or "barrel".  The forward direction of this dummy is the
        //way it shoots, the position is where it shoots from:
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



        public void GunDetect()
        {
            var gun = (IMyGunObject<MyGunBase>)Entity;
            var shotTime = gun.GunBase.LastShootTime.Ticks;

            if (shotTime > _lastShotTime)
            {
                _lastShotTime = shotTime;
                // fired...
            }
            _blockShootProperty = _tblock.GetProperty("Shoot").Cast<bool>();
        }
    }

    internal class DirectionBarrelComponent
    {
        private string v1;
        private string v2;
        private string v3;

        public DirectionBarrelComponent()
        {
        }

        public DirectionBarrelComponent(string v)
        {
        }

        public DirectionBarrelComponent(string v1, string v2)
        {
            this.v1 = v1;
            this.v2 = v2;
        }

        public DirectionBarrelComponent(string v1, string v2, string v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }
}

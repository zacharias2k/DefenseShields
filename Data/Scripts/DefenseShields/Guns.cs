using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace DefenseShields.Guns
{
    //[MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeTurretBaseDefinition), false, new string[] { "Guns" })]
    class Guns : MyGameLogicComponent
    {
        //private IMyLargeTurretBase Entity;
        private long _lastShotTime;
        public void GunDetect()
        {
            var gun = (IMyGunObject<MyGunBase>)Entity;
            var shotTime = gun.GunBase.LastShootTime.Ticks;

            if (shotTime > _lastShotTime)
            {
                _lastShotTime = shotTime;
                // fired...
            }
        }
    }
}

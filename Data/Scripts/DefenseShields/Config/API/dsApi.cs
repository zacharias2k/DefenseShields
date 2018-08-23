using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields.Data.Scripts.DefenseShields.API
{
    class DefenseShieldsApi
    {
        /// <summary>
        /// RayCast against shielded targets.  If returns null proceed with normal raycast,
        /// but do not normal cast against entities in ShieldEnted (hashset).
        /// </summary>
        /// 
        /// <param name="shield">the active shield to attack</param>
        /// <param name="line">Ray to check for shield contact</param>
        /// <param name="attackerId">You must pass the EntityID of the attacker</param>
        /// <param name="damage">the amount of damage to do</param>
        /// <param name="effect">optional effects, "DSdamage" is default, "DSheal"and "DSbypass" are possible</param>
        private Vector3D? DsRayCast(IMyEntity shield, LineD line, long attackerId, float damage, MyStringId effect)
        {
            var worldSphere = new BoundingSphereD(shield.PositionComp.WorldVolume.Center, shield.PositionComp.LocalAABB.HalfExtents.AbsMax());
            var myObb = MyOrientedBoundingBoxD.Create(shield.PositionComp.LocalAABB, shield.PositionComp.WorldMatrix.GetOrientation());
            myObb.Center = shield.PositionComp.WorldVolume.Center;
            var obbCheck = myObb.Intersects(ref line);

            var testDir = line.From - line.To;
            testDir.Normalize();
            var ray = new RayD(line.From, -testDir);
            var sphereCheck = worldSphere.Intersects(ray);

            var obb = obbCheck ?? 0;
            var sphere = sphereCheck ?? 0;
            double furthestHit;

            if (obb <= 0 && sphere <= 0) furthestHit = 0;
            else if (obb > sphere) furthestHit = obb;
            else furthestHit = sphere;
            var hitPos = line.From + testDir * -furthestHit;

            var parent = MyAPIGateway.Entities.GetEntityById(long.Parse(shield.Name));
            var cubeBlock = (MyCubeBlock)parent;
            var block = (IMySlimBlock)cubeBlock.SlimBlock;

            if (block == null) return null;
            block.DoDamage(damage, MyStringHash.GetOrCompute(effect.ToString()), true, null, attackerId);
            shield.Render.ColorMaskHsv = hitPos;
            if (effect.ToString() == "bypass") return null;

            return hitPos;
        }
    }
}

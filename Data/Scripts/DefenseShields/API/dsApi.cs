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
        /// but do not normal cast against entities in _shielded (hashset).
        /// </summary>
        /// 
        /// <param name="shield">the active shield to attack</param>
        /// <param name="line">Ray to check for shield contact</param>
        /// <param name="attackerId">You must pass the EntityID of the attacker</param>
        /// <param name="damage">the amount of damage to do</param>
        /// <param name="effect">optional effects, "DSdamage" is default, "DSheal"and "DSbypass" are possible</param>
        private Vector3D? DsRayCast(IMyEntity shield, LineD line, long attackerId, float damage, MyStringId effect)
        {
            var sphere = new BoundingSphereD(shield.PositionComp.WorldVolume.Center, shield.PositionComp.LocalAABB.HalfExtents.AbsMax());
            var obb = MyOrientedBoundingBoxD.Create(shield.PositionComp.LocalAABB, shield.PositionComp.WorldMatrix.GetOrientation());
            obb.Center = shield.PositionComp.WorldVolume.Center;

            // DsDebugDraw.DrawSphere(sphere, Color.Red);
            DsDebugDraw.DrawOBB(obb, Color.Blue, MySimpleObjectRasterizer.Wireframe, 0.1f);
            var obbCheck = obb.Intersects(ref line);
            if (obbCheck == null) return null;

            var testDir = line.From - line.To;
            testDir.Normalize();
            var ray = new RayD(line.From, -testDir);
            var sphereCheck = sphere.Intersects(ray);
            if (sphereCheck == null) return null;

            var furthestHit = obbCheck < sphereCheck ? sphereCheck : obbCheck;
            Vector3 hitPos = line.From + testDir * -(double)furthestHit;

            var parent = MyAPIGateway.Entities.GetEntityById(long.Parse(shield.Name));
            var cubeBlock = (MyCubeBlock)parent;
            var block = (IMySlimBlock)cubeBlock.SlimBlock;

            if (block == null) return null;
            // _shielded.Add(parent);

            /*
            if (Debug)
            {
                DsDebugDraw.DrawSingleVec(hitPos, 1f, Color.Gold);
                var c = new Vector4(15, 0, 0, 10);
                var rnd = new Random();
                var lineWidth = 0.2f;
                if (rnd.Next(0, 5) > 2) lineWidth = 1;
                if (_count % 2 == 0) DsDebugDraw.DrawLineToVec(line.From, hitPos, c, lineWidth);
            }
            */
            block.DoDamage(damage, MyStringHash.GetOrCompute(effect.ToString()), true, null, attackerId);
            shield.Render.ColorMaskHsv = hitPos;
            if (effect.ToString() == "bypass") return null;

            return hitPos;
        }
    }
}

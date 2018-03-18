using Sandbox.Game;
using VRageMath;

namespace DefenseShields.Support
{
    static class Explosions
    {
        public static void CreateDummyExplosion(Vector3D position)
        {
            var explosionType = MyExplosionTypeEnum.WARHEAD_EXPLOSION_15;

            //  Create explosion
            MyExplosionInfo info = new MyExplosionInfo
            {
                PlayerDamage = 0,
                Damage = 0,
                ExplosionType = explosionType,
                ExplosionSphere = new BoundingSphere(position, 15f),
                LifespanMiliseconds = MyExplosionsConstants.EXPLOSION_LIFESPAN,
                AffectVoxels = false,
                ParticleScale = 1,
                Direction = Vector3.Down,
                VoxelExplosionCenter = position,
                ExplosionFlags = MyExplosionFlags.AFFECT_VOXELS |
                                 MyExplosionFlags.APPLY_FORCE_AND_DAMAGE |
                                 MyExplosionFlags.CREATE_DEBRIS |
                                 MyExplosionFlags.CREATE_DECALS |
                                 MyExplosionFlags.CREATE_PARTICLE_EFFECT |
                                 MyExplosionFlags.CREATE_SHRAPNELS |
                                 MyExplosionFlags.APPLY_DEFORMATION,
                VoxelCutoutScale = 1.0f,
                PlaySound = true,
                ApplyForceAndDamage = true,
                ObjectsRemoveDelayInMiliseconds = 40,
            };
            MyExplosions.AddExplosion(ref info);
        }

    public static void CreateExplosion(Vector3D position, float radius, int damage = 5000)
        {
            var explosionType = MyExplosionTypeEnum.WARHEAD_EXPLOSION_50;
            if (radius < 2)
                explosionType = MyExplosionTypeEnum.WARHEAD_EXPLOSION_02;
            else if (radius < 15)
                explosionType = MyExplosionTypeEnum.WARHEAD_EXPLOSION_15;
            else if (radius < 30)
                explosionType = MyExplosionTypeEnum.WARHEAD_EXPLOSION_30;

            //  Create explosion
            MyExplosionInfo info = new MyExplosionInfo
            {
                PlayerDamage = 0,
                Damage = damage,
                ExplosionType = explosionType,
                ExplosionSphere = new BoundingSphereD(position, radius),
                LifespanMiliseconds = MyExplosionsConstants.EXPLOSION_LIFESPAN,
                AffectVoxels = false,
                ParticleScale = 1,
                Direction = Vector3.Down,
                VoxelExplosionCenter = position,
                ExplosionFlags = MyExplosionFlags.AFFECT_VOXELS |
                                 MyExplosionFlags.APPLY_FORCE_AND_DAMAGE |
                                 MyExplosionFlags.CREATE_DEBRIS |
                                 MyExplosionFlags.CREATE_DECALS |
                                 MyExplosionFlags.CREATE_PARTICLE_EFFECT |
                                 MyExplosionFlags.CREATE_SHRAPNELS |
                                 MyExplosionFlags.APPLY_DEFORMATION,
                VoxelCutoutScale = 1.0f,
                PlaySound = true,
                ApplyForceAndDamage = true,
                ObjectsRemoveDelayInMiliseconds = 40
            };
            MyExplosions.AddExplosion(ref info);
        }
    }
}

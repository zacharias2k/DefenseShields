using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems.Support
{

    public interface IThreadEvent
    {
        void Execute();
    }

    internal class ShieldVsShieldThreadEvent : IThreadEvent
    {
        public readonly Fields Field;
        public readonly float Damage;
        public readonly Vector3D CollisionAvg;
        public readonly long AttackerId;

        public ShieldVsShieldThreadEvent(Fields field, float damage, Vector3D collisionAvg, long attackerId)
        {
            Field = field;
            Damage = damage;
            CollisionAvg = collisionAvg;
            AttackerId = attackerId;
        }

        public void Execute()
        {
            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(AttackerId, Damage, Session.Instance.MPEnergy, null, true, CollisionAvg);
            }
            else
            {
                Field.EnergyHit = true;
                Field.ImpactSize = Damage;
                Field.WorldImpactPosition = CollisionAvg;
            }
            Field.WebDamage = true;
            Field.Absorb += Damage;
        }
    }

    internal class MissileThreadEvent : IThreadEvent
    {
        public readonly MyEntity Entity;
        public readonly Fields Field;

        public MissileThreadEvent(MyEntity entity, Fields field)
        {
            Entity = entity;
            Field = field;
        }

        public void Execute()
        {
            if (Entity == null || !Entity.InScene || Entity.MarkedForClose) return;
            var computedDamage = UtilsStatic.ComputeAmmoDamage(Entity);

            var damage = computedDamage * Field.Bus.ActiveController.State.Value.ModulateKinetic;
            if (computedDamage < 0) damage = computedDamage;

            var rayDir = Vector3D.Normalize(Entity.Physics.LinearVelocity);
            var ray = new RayD(Entity.PositionComp.WorldVolume.Center, rayDir);
            var intersect = CustomCollision.IntersectEllipsoid(Field.DetectMatrixOutsideInv, Field.DetectionMatrix, ray);
            var hitDist = intersect ?? 0;
            var hitPos = ray.Position + (ray.Direction * -hitDist);

            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(Entity.EntityId, damage, Session.Instance.MPExplosion, null, true, hitPos);
                Entity.Close();
                Entity.InScene = false;
            }
            else
            {
                Field.EnergyHit = true;
                Field.WorldImpactPosition = hitPos;
                Field.ImpactSize = damage;
                UtilsStatic.CreateFakeSmallExplosion(hitPos);
                Entity.Close();
                Entity.InScene = false;
            }
            Field.WebDamage = true;
            Field.Absorb += damage;
        }
    }

    internal class FloaterThreadEvent : IThreadEvent
    {
        public readonly MyEntity Entity;
        public readonly Fields Field;

        public FloaterThreadEvent(MyEntity entity, Fields field)
        {
            Entity = entity;
            Field = field;
        }

        public void Execute()
        {
            if (Entity == null || Entity.MarkedForClose) return;
            var floater = (IMyFloatingObject)Entity;
            var entVel = Entity.Physics.LinearVelocity;
            var movingVel = entVel != Vector3.Zero ? entVel : -Field.Bus.Spine.Physics.LinearVelocity;

            var rayDir = Vector3D.Normalize(movingVel);
            var ray = new RayD(Entity.PositionComp.WorldVolume.Center, rayDir);
            var intersect = CustomCollision.IntersectEllipsoid(Field.DetectMatrixOutsideInv, Field.DetectionMatrix, ray);
            var hitDist = intersect ?? 0;
            var hitPos = ray.Position + (ray.Direction * -hitDist);

            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(Entity.EntityId, 1, Session.Instance.MPKinetic, null, false, hitPos);
                floater.DoDamage(9999999, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
            else
            {
                Field.WorldImpactPosition = hitPos;
                Field.ImpactSize = 10;
                floater.DoDamage(9999999, Session.Instance.MpIgnoreDamage, false, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
            Field.WebDamage = true;
            Field.Absorb += 1;
        }
    }
    internal class CollisionDataThreadEvent : IThreadEvent
    {
        public readonly MyCollisionPhysicsData CollisionData;
        public readonly Fields Field;

        public CollisionDataThreadEvent(MyCollisionPhysicsData collisionPhysicsData, Fields field)
        {
            CollisionData = collisionPhysicsData;
            Field = field;
        }

        public void Execute()
        {
            if (CollisionData.Entity1 == null || CollisionData.Entity2 == null || CollisionData.Entity1.MarkedForClose || CollisionData.Entity2.MarkedForClose) return;
            var tick = Session.Instance.Tick;
            EntIntersectInfo entInfo;

            var foundInfo = Field.WebEnts.TryGetValue(CollisionData.Entity1, out entInfo);
            if (!foundInfo || entInfo.LastCollision == tick) return;

            if (entInfo.LastCollision >= tick - 8) entInfo.ConsecutiveCollisions++;
            else entInfo.ConsecutiveCollisions = 0;
            entInfo.LastCollision = tick;
            if (entInfo.ConsecutiveCollisions > 0) if (Session.Enforced.Debug >= 2) Log.Line($"Consecutive:{entInfo.ConsecutiveCollisions}");
            if (!CollisionData.E1IsStatic)
            {
                if (entInfo.ConsecutiveCollisions == 0) CollisionData.Entity1.Physics.ApplyImpulse(CollisionData.ImpDirection1, CollisionData.CollisionCorrection1);
                if (CollisionData.E2IsHeavier)
                {
                    var accelCap = CollisionData.E1IsStatic ? 10 : 50;
                    var accelClamp = MathHelper.Clamp(CollisionData.Mass2 / CollisionData.Mass1, 1, accelCap);
                    var collisions = entInfo.ConsecutiveCollisions + 1;
                    var sizeAccel = accelClamp > collisions ? accelClamp : collisions;
                    var forceMulti = (CollisionData.Mass1 * (collisions * sizeAccel));
                    if (CollisionData.Entity1.Physics.LinearVelocity.Length() <= (Session.Instance.MaxEntitySpeed * 0.75))
                        CollisionData.Entity1.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceMulti * CollisionData.Force1, null, null, null, CollisionData.Immediate);
                }
            }

            if (!CollisionData.E2IsStatic)
            {
                if (entInfo.ConsecutiveCollisions == 0) CollisionData.Entity2.Physics.ApplyImpulse(CollisionData.ImpDirection2, CollisionData.CollisionCorrection2);
                if (CollisionData.E1IsHeavier)
                {
                    var accelCap = CollisionData.E1IsStatic ? 10 : 50;
                    var accelClamp = MathHelper.Clamp(CollisionData.Mass1 / CollisionData.Mass2, 1, accelCap);
                    var collisions = entInfo.ConsecutiveCollisions + 1;
                    var sizeAccel = accelClamp > collisions ? accelClamp : collisions;
                    var forceMulti = (CollisionData.Mass2 * (collisions * sizeAccel));
                    if (CollisionData.Entity2.Physics.LinearVelocity.Length() <= (Session.Instance.MaxEntitySpeed * 0.75))
                        CollisionData.Entity2.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceMulti * CollisionData.Force2, null, null, null, CollisionData.Immediate);
                }
            }
        }
    }

    internal class StationCollisionDataThreadEvent : IThreadEvent
    {
        public readonly MyCollisionPhysicsData CollisionData;
        public readonly Fields Field;

        public StationCollisionDataThreadEvent(MyCollisionPhysicsData collisionPhysicsData, Fields field)
        {
            CollisionData = collisionPhysicsData;
            Field = field;
        }

        public void Execute()
        {
            if (CollisionData.Entity1 == null || CollisionData.Entity1.MarkedForClose) return;
            var tick = Session.Instance.Tick;
            EntIntersectInfo entInfo;

            var foundInfo = Field.WebEnts.TryGetValue(CollisionData.Entity1, out entInfo);
            if (!foundInfo || entInfo.LastCollision == tick) return;

            if (entInfo.LastCollision >= tick - 8) entInfo.ConsecutiveCollisions++;
            else entInfo.ConsecutiveCollisions = 0;
            entInfo.LastCollision = tick;
            if (entInfo.ConsecutiveCollisions > 0) if (Session.Enforced.Debug >= 2) Log.Line($"Consecutive Station hits:{entInfo.ConsecutiveCollisions}");

            if (entInfo.ConsecutiveCollisions == 0) CollisionData.Entity1.Physics.ApplyImpulse(CollisionData.ImpDirection1, CollisionData.CollisionAvg);

            var collisions = entInfo.ConsecutiveCollisions + 1;
            var forceMulti = CollisionData.Mass1 * (collisions * 60);
            if (CollisionData.Entity1.Physics.LinearVelocity.Length() <= (Session.Instance.MaxEntitySpeed * 0.75))
                CollisionData.Entity1.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceMulti * CollisionData.Force1, null, null, null, CollisionData.Immediate);

            var transformInv = Field.DetectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(CollisionData.CollisionAvg, transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
            CollisionData.Entity1.Physics.ApplyImpulse((CollisionData.Mass1 * 0.075) * CollisionData.ImpDirection2, CollisionData.CollisionAvg);
        }
    }

    internal class PlayerCollisionThreadEvent : IThreadEvent
    {
        public readonly MyCollisionPhysicsData CollisionData;
        public readonly Fields Field;

        public PlayerCollisionThreadEvent(MyCollisionPhysicsData collisionPhysicsData, Fields field)
        {
            CollisionData = collisionPhysicsData;
            Field = field;
        }

        public void Execute()
        {
            const int forceMulti = 200000;
            CollisionData.Entity1.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceMulti * CollisionData.Force1, null, null, null, CollisionData.Immediate);
            var character = CollisionData.Entity1 as IMyCharacter;
            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(CollisionData.Entity1.EntityId, 1, Session.Instance.MPKinetic, null, false, CollisionData.CollisionAvg);
                character?.DoDamage(1f, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
            else
            {
                Field.ImpactSize = 1;
                Field.WorldImpactPosition = CollisionData.CollisionAvg;
                character?.DoDamage(1f, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
        }
    }

    internal class CharacterEffectThreadEvent : IThreadEvent
    {
        public readonly IMyCharacter Character;
        public readonly Fields Field;

        public CharacterEffectThreadEvent(IMyCharacter character, Fields field)
        {
            Character = character;
            Field = field;
        }

        public void Execute()
        {
            var npcname = Character.ToString();
            if (npcname.Equals("Space_Wolf"))
            {
                Character.Delete();
            }
        }
    }

    internal class ManyBlocksThreadEvent : IThreadEvent
    {
        public readonly Fields Field;
        public readonly HashSet<CubeAccel> AccelSet;
        public readonly float Damage;
        public readonly Vector3D CollisionAvg;
        public readonly long AttackerId;

        public ManyBlocksThreadEvent(HashSet<CubeAccel> accelSet, Fields field, float damage, Vector3D collisionAvg, long attackerId)
        {
            AccelSet = accelSet;
            Field = field;
            Damage = damage;
            CollisionAvg = collisionAvg;
            AttackerId = attackerId;
        }

        public void Execute()
        {
            foreach (var accel in AccelSet)
            {
                EntIntersectInfo entInfo;
                if (accel.Grid != accel.Block.CubeGrid)
                {
                    if (Field.WebEnts.TryGetValue(accel.Grid, out entInfo))
                    {
                        entInfo.RefreshNow = true;
                    }
                    return;
                }

                if (accel.Block.IsDestroyed)
                {
                    if (Field.WebEnts.TryGetValue(accel.Grid, out entInfo)) entInfo.RefreshNow = true;
                    return;
                }

                accel.Block.DoDamage(accel.Block.MaxIntegrity, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);

                if (accel.Block.IsDestroyed)
                {
                    if (Field.WebEnts.TryGetValue(accel.Grid, out entInfo)) entInfo.RefreshNow = true;
                }
            }

            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(AttackerId, Damage, Session.Instance.MPKinetic, null, true, CollisionAvg);
            }
            else
            {
                Field.ImpactSize = Damage;
                Field.WorldImpactPosition = CollisionAvg;
            }
            Field.WebDamage = true;
            Field.Absorb += Damage;
        }
    }

    internal class VoxelCollisionDmgThreadEvent : IThreadEvent
    {
        public readonly MyEntity Entity;
        public readonly Fields Field;
        public readonly float Damage;
        public readonly Vector3D CollisionAvg;

        public VoxelCollisionDmgThreadEvent(MyEntity entity, Fields field, float damage, Vector3D collisionAvg)
        {
            Entity = entity;
            Field = field;
            Damage = damage;
            CollisionAvg = collisionAvg;
        }

        public void Execute()
        {
            if (Entity == null || Entity.MarkedForClose) return;
            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(Entity.EntityId, Damage, Session.Instance.MPKinetic, null, false, CollisionAvg);
            }
            else
            {
                Field.WorldImpactPosition = CollisionAvg;
                Field.ImpactSize = 12000;
            }
            Field.WebDamage = true;
            Field.Absorb += Damage;
        }
    }

    internal class VoxelCollisionPhysicsThreadEvent : IThreadEvent
    {
        public readonly MyCollisionPhysicsData CollisionData;
        public readonly Fields Field;

        public VoxelCollisionPhysicsThreadEvent(MyCollisionPhysicsData collisionPhysicsData, Fields field)
        {
            CollisionData = collisionPhysicsData;
            Field = field;
        }

        public void Execute()
        {
                Vector3 velAtPoint;
                var point = CollisionData.CollisionCorrection2;
                CollisionData.Entity2.Physics.GetVelocityAtPointLocal(ref point, out velAtPoint);
                var speed = MathHelper.Clamp(velAtPoint.Length(), 2f, 20f);
                var forceMulti = (CollisionData.Mass2 * 10) * speed;
                CollisionData.Entity2.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceMulti * CollisionData.Force2, null, null, speed, CollisionData.Immediate);
        }
    }

    internal class VoxelDmgThreadEvent : IThreadEvent
    {
        public readonly MyVoxelBase VoxelBase;
        public readonly Fields Field;

        public VoxelDmgThreadEvent(MyVoxelBase voxelBase, Fields field)
        {
            VoxelBase = voxelBase;
            Field = field;
        }

        public void Execute()
        {
            if (VoxelBase == null || VoxelBase.RootVoxel.MarkedForClose || VoxelBase.RootVoxel.Closed) return;
            VoxelBase.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, Field.DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
        }
    }

    internal class MeteorDmgThreadEvent : IThreadEvent
    {
        public readonly IMyMeteor Meteor;
        public readonly Fields Field;

        public MeteorDmgThreadEvent(IMyMeteor meteor, Fields field)
        {
            Meteor = meteor;
            Field = field;
        }

        public void Execute()
        {
            if (Meteor == null || Meteor.MarkedForClose) return;
            var damage = 5000 * Field.Bus.ActiveController.State.Value.ModulateEnergy;
            if (Session.Instance.MpActive)
            {
                Field.AddShieldHit(Meteor.EntityId, damage, Session.Instance.MPKinetic, null, false, Meteor.PositionComp.WorldVolume.Center);
                Meteor.DoDamage(10000f, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
            else
            {
                Field.WorldImpactPosition = Meteor.PositionComp.WorldVolume.Center;
                Field.ImpactSize = damage;
                Meteor.DoDamage(10000f, Session.Instance.MpIgnoreDamage, true, null, Field.Bus.ActiveController.MyCube.EntityId);
            }
            Field.WebDamage = true;
            Field.Absorb += damage;
        }
    }

    internal class ForceDataThreadEvent : IThreadEvent
    {
        public readonly MyForceData ForceData;
        public readonly Fields Field;

        public ForceDataThreadEvent(MyForceData forceData, Fields field)
        {
            ForceData = forceData;
            Field = field;
        }

        public void Execute()
        {
            if (ForceData.Entity == null || ForceData.Entity.MarkedForClose) return;
            ForceData.Entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, ForceData.Force, null, Vector3D.Zero, ForceData.MaxSpeed, ForceData.Immediate);
        }
    }

    internal class ImpulseDataThreadEvent : IThreadEvent
    {
        public readonly MyImpulseData ImpulseData;
        public readonly Fields Field;

        public ImpulseDataThreadEvent(MyImpulseData impulseData, Fields field)
        {
            ImpulseData = impulseData;
            Field = field;
        }

        public void Execute()
        {
            if (ImpulseData.Entity == null || ImpulseData.Entity.MarkedForClose) return;
            ImpulseData.Entity.Physics.ApplyImpulse(ImpulseData.Direction, ImpulseData.Position);
        }
    }
}

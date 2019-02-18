namespace DefenseShields.Support
{
    using System;
    using Sandbox.Game;
    using Sandbox.Game.Entities;
    using Sandbox.Game.Entities.Character.Components;
    using Sandbox.ModAPI;
    using System.Collections.Generic;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public interface IThreadEvent
    {
        void Execute();
    }

    public struct MissileThreadEvent : IThreadEvent
    {
        public readonly MyEntity Entity;
        public readonly DefenseShields Shield;

        public MissileThreadEvent(MyEntity entity, DefenseShields shield)
        {
            Entity = entity;
            Shield = shield;
        }

        public void Execute()
        {
            if (Entity == null || !Entity.InScene || Entity.MarkedForClose) return;
            var computedDamage = UtilsStatic.ComputeAmmoDamage(Entity);

            var damage = computedDamage * Shield.DsState.State.ModulateKinetic;
            if (computedDamage < 0) damage = computedDamage;

            var rayDir = Vector3D.Normalize(Entity.Physics.LinearVelocity);
            var ray = new RayD(Entity.PositionComp.WorldVolume.Center, rayDir);
            var intersect = CustomCollision.IntersectEllipsoid(Shield.DetectMatrixOutsideInv, Shield.DetectionMatrix, ray);
            var hitDist = intersect ?? 0;
            var hitPos = ray.Position + (ray.Direction * -hitDist);

            if (Session.Instance.MpActive)
            {
                Shield.AddShieldHit(Entity.EntityId, damage, Session.Instance.MPExplosion, null, false, hitPos);
                Entity.Close();
                Entity.InScene = false;
            }
            else
            {
                Shield.EnergyHit = true;
                Shield.WorldImpactPosition = hitPos;
                Shield.Absorb += damage;
                Shield.ImpactSize = damage;
                UtilsStatic.CreateFakeSmallExplosion(hitPos);
                Entity.Close();
                Entity.InScene = false;
            }
        }
    }

    public struct FloaterThreadEvent : IThreadEvent
    {
        public readonly MyEntity Entity;
        public readonly DefenseShields Shield;

        public FloaterThreadEvent(MyEntity entity, DefenseShields shield)
        {
            Entity = entity;
            Shield = shield;
        }

        public void Execute()
        {
            if (Entity == null || Entity.MarkedForClose) return;
            var floater = (IMyFloatingObject)Entity;
            var entVel = Entity.Physics.LinearVelocity;
            var movingVel = entVel != Vector3.Zero ? entVel : -Shield.MyGrid.Physics.LinearVelocity;

            var rayDir = Vector3D.Normalize(movingVel);
            var ray = new RayD(Entity.PositionComp.WorldVolume.Center, rayDir);
            var intersect = CustomCollision.IntersectEllipsoid(Shield.DetectMatrixOutsideInv, Shield.DetectionMatrix, ray);
            var hitDist = intersect ?? 0;
            var hitPos = ray.Position + (ray.Direction * -hitDist);

            if (Session.Instance.MpActive)
            {
                Shield.AddShieldHit(Entity.EntityId, 1, Session.Instance.MPKinetic, null, false, hitPos);
                floater.DoDamage(9999999, Session.Instance.MpIgnoreDamage, true, null, Shield.MyCube.EntityId);
            }
            else
            {
                Shield.WorldImpactPosition = hitPos;
                Shield.Absorb += 1;
                Shield.ImpactSize = 10;
                floater.DoDamage(9999999, Session.Instance.MpIgnoreDamage, false, null, Shield.MyCube.EntityId);
            }
        }
    }

    public struct ForceDataThreadEvent : IThreadEvent
    {
        public readonly MyForceData ForceData;
        public readonly DefenseShields Shield;

        public ForceDataThreadEvent(MyForceData forceData, DefenseShields shield)
        {
            ForceData = forceData;
            Shield = shield;
        }

        public void Execute()
        {
            if (ForceData.Entity == null || ForceData.Entity.MarkedForClose) return;
            ForceData.Entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, ForceData.Force, null, Vector3D.Zero, ForceData.MaxSpeed, ForceData.Immediate);
        }
    }

    public struct ImpulseDataThreadEvent : IThreadEvent
    {
        public readonly MyImpulseData ImpulseData;
        public readonly DefenseShields Shield;

        public ImpulseDataThreadEvent(MyImpulseData impulseData, DefenseShields shield)
        {
            ImpulseData = impulseData;
            Shield = shield;
        }

        public void Execute()
        {
            if (ImpulseData.Entity == null || ImpulseData.Entity.MarkedForClose) return;
            ImpulseData.Entity.Physics.ApplyImpulse(ImpulseData.Direction, ImpulseData.Position);
        }
    }

    public struct ManyBlocksThreadEvent : IThreadEvent
    {
        public readonly DefenseShields Shield;
        public readonly HashSet<CubeAccel> AccelSet;

        public ManyBlocksThreadEvent(HashSet<CubeAccel> accelSet, DefenseShields shield)
        {
            AccelSet = accelSet;
            Shield = shield;
        }

        public void Execute()
        {
            foreach (var accel in AccelSet)
            {
                EntIntersectInfo entInfo;
                if (accel.Grid != accel.Block.CubeGrid)
                {
                    if (Shield.WebEnts.TryGetValue(accel.Grid, out entInfo))
                    {
                        entInfo.RefreshNow = true;
                    }
                    return;
                }

                if (accel.Block.IsDestroyed)
                {
                    if (Shield.WebEnts.TryGetValue(accel.Grid, out entInfo)) entInfo.RefreshNow = true;
                    return;
                }
                var damageMulti = 9999999999;
                if (Shield.ShieldMode == DefenseShields.ShieldType.Station && Shield.DsState.State.Enhancer) damageMulti = 9999999999;

                accel.Block.DoDamage(damageMulti, Session.Instance.MpIgnoreDamage, true, null, Shield.MyCube.EntityId);
                if (accel.Block.IsDestroyed)
                {
                    if (Shield.WebEnts.TryGetValue(accel.Grid, out entInfo)) entInfo.RefreshNow = true;
                }
            }
        }
    }

    public struct CharacterEffectThreadEvent : IThreadEvent
    {
        public readonly IMyCharacter Character;
        public readonly DefenseShields Shield;

        public CharacterEffectThreadEvent(IMyCharacter character, DefenseShields shield)
        {
            Character = character;
            Shield = shield;
        }

        public void Execute()
        {
            var npcname = Character.ToString();
            if (npcname.Equals("Space_Wolf"))
            {
                Character.Delete();
                return;
            }
            var hId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = Character.GetSuitGasFillLevel(hId);
            Character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hId, (playerGasLevel * -0.0001f) + .002f);
            MyVisualScriptLogicProvider.CreateExplosion(Character.GetPosition(), 0, 0);
            Character.DoDamage(50f, Session.Instance.MpIgnoreDamage, true, null, Shield.MyCube.EntityId);
            var vel = Character.Physics.LinearVelocity;
            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
            var speedDir = Vector3D.Normalize(vel);
            var rnd = new Random();
            var randomSpeed = rnd.Next(10, 20);
            var additionalSpeed = vel + (speedDir * randomSpeed);
            Character.Physics.LinearVelocity = additionalSpeed;
        }
    }

    public struct VoxelDmgThreadEvent : IThreadEvent
    {
        public readonly MyVoxelBase VoxelBase;
        public readonly DefenseShields Shield;

        public VoxelDmgThreadEvent(MyVoxelBase voxelBase, DefenseShields shield)
        {
            VoxelBase = voxelBase;
            Shield = shield;
        }

        public void Execute()
        {
            if (VoxelBase == null || VoxelBase.RootVoxel.MarkedForClose || VoxelBase.RootVoxel.Closed) return;
            VoxelBase.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, Shield.DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
        }
    }

    public struct MeteorDmgThreadEvent : IThreadEvent
    {
        public readonly IMyMeteor Meteor;
        public readonly DefenseShields Shield;

        public MeteorDmgThreadEvent(IMyMeteor meteor, DefenseShields shield)
        {
            Meteor = meteor;
            Shield = shield;
        }

        public void Execute()
        {
            if (Meteor == null || Meteor.MarkedForClose) return;
            var damage = 5000 * Shield.DsState.State.ModulateEnergy;
            if (Session.Instance.MpActive)
            {
                Shield.AddShieldHit(Meteor.EntityId, damage, Session.Instance.MPKinetic, null, false, Meteor.PositionComp.WorldVolume.Center);
                Meteor.DoDamage(10000f, Session.Instance.MpIgnoreDamage, true, null, Shield.MyCube.EntityId);
            }
            else
            {
                Shield.WorldImpactPosition = Meteor.PositionComp.WorldVolume.Center;
                Shield.Absorb += damage;
                Shield.ImpactSize = damage;
                Meteor.DoDamage(10000f, Session.Instance.MpIgnoreDamage, true, null, Shield.MyCube.EntityId);
            }
        }
    }
}

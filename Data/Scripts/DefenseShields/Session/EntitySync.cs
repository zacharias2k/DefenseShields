namespace DefenseShields
{
    using System;
    using Support;
    using Sandbox.Game;
    using Sandbox.Game.Entities;
    using Sandbox.Game.Entities.Character.Components;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public partial class Session
    {
        private void SyncThreadedEnts()
        {
            try
            {
                EntitySyncEvent thisEvent;
                while (EntSyncEvents.TryDequeue(out thisEvent))
                {
                    switch (thisEvent.EventType)
                    {
                        case EntEvents.MissileDmg:
                            MissileDmg(thisEvent.Entity, thisEvent.Shield);
                            break;
                        case EntEvents.ForceData:
                            if (thisEvent.AddForceData.HasValue) ForceData(thisEvent.AddForceData.Value);
                            break;
                        case EntEvents.ImpulseData:
                            if (thisEvent.ImpulseData.HasValue) ImpulseData(thisEvent.ImpulseData.Value);
                            break;
                        case EntEvents.CollidingBlocks:
                            if (thisEvent.Accel.HasValue) CollidingBlocks(thisEvent.Accel.Value, thisEvent.Shield);
                            break;
                        case EntEvents.FewDmgBlocks:
                            if (thisEvent.Accel.HasValue) FewDmgBlocks(thisEvent.Accel.Value, thisEvent.Shield);
                            break;
                        case EntEvents.DestroyedBlock:
                            if (thisEvent.Accel.HasValue) DestroyedBlock(thisEvent.Accel.Value, thisEvent.Shield);
                            break;
                        case EntEvents.Eject:
                            if (thisEvent.Accel.HasValue) Eject(thisEvent.Accel.Value);
                            break;
                        case EntEvents.CharacterDmg:
                            CharacterDmg(thisEvent.Character, thisEvent.Shield);
                            break;
                        case EntEvents.VoxelDmg:
                            VoxelDmg(thisEvent.VoxelBase, thisEvent.Shield);
                            break;
                        case EntEvents.MeteorDmg:
                            MeteorDmg(thisEvent.Meteor, thisEvent.Shield);
                            break;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SyncThreadedEnts: {ex}"); }
        }

        private static void MissileDmg(MyEntity ent, DefenseShields shield)
        {
            if (ent == null || !ent.InScene || ent.MarkedForClose) return;
            var computedDamage = UtilsStatic.ComputeAmmoDamage(ent);

            var damage = computedDamage * shield.DsState.State.ModulateKinetic;
            if (computedDamage < 0) damage = computedDamage;

            var rayDir = Vector3D.Normalize(ent.Physics.LinearVelocity);
            var ray = new RayD(ent.PositionComp.WorldVolume.Center, rayDir);
            var intersect = CustomCollision.IntersectEllipsoid(shield.DetectMatrixOutsideInv, shield.DetectionMatrix, ray);
            var hitDist = intersect ?? 0;
            var hitPos = ray.Position + (ray.Direction * -hitDist);

            if (Instance.MpActive)
            {
                shield.AddShieldHit(ent.EntityId, damage, Instance.MPExplosion, null, false, hitPos);
                ent.Close();
                ent.InScene = false;
            }
            else
            {
                shield.EnergyHit = true;
                shield.WorldImpactPosition = hitPos;
                shield.Absorb += damage;
                shield.ImpactSize = damage;
                UtilsStatic.CreateFakeSmallExplosion(hitPos);
                ent.Close();
                ent.InScene = false;
            }
        }

        private static void ForceData(MyAddForceData data)
        {
            if (data.MyGrid == null || data.MyGrid.MarkedForClose) return;
            data.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, data.Force, null, Vector3D.Zero, data.MaxSpeed, data.Immediate);
        }

        private static void ImpulseData(MyImpulseData data)
        {
            if (data.MyGrid == null || data.MyGrid.MarkedForClose) return;
            data.MyGrid.Physics.ApplyImpulse(data.Direction, data.Position);
        }

        private static void DestroyedBlock(CubeAccel accel, DefenseShields shield)
        {
            if (accel.Grid == null) return;
            EntIntersectInfo entInfo;
            shield.WebEnts.TryGetValue(accel.Grid, out entInfo);
            if (entInfo == null)
            {
                accel.Grid.EnqueueDestroyedBlock(accel.BlockPos);
                return;
            }
            entInfo.CacheBlockList.Remove(accel);
        }

        private static void MeteorDmg(IMyMeteor meteor, DefenseShields shield)
        {
            if (meteor == null || meteor.MarkedForClose) return;
            var damage = 5000 * shield.DsState.State.ModulateEnergy;
            if (Instance.MpActive)
            {
                shield.AddShieldHit(meteor.EntityId, damage, Instance.MPKinetic, null, false, meteor.PositionComp.WorldVolume.Center);
                meteor.DoDamage(10000f, Instance.MpIgnoreDamage, true, null, shield.MyCube.EntityId);
            }
            else
            {
                shield.WorldImpactPosition = meteor.PositionComp.WorldVolume.Center;
                shield.Absorb += damage;
                shield.ImpactSize = damage;
                meteor.DoDamage(10000f, Instance.MpIgnoreDamage, true, null, shield.MyCube.EntityId);
            }
        }

        private static void CollidingBlocks(CubeAccel accel, DefenseShields shield)
        {
            if (accel.Grid == null) return;

            if (accel.Block.IsDestroyed)
            {
                accel.Grid.EnqueueDestroyedBlock(accel.BlockPos);
                return;
            }
            var damageMulti = 350;
            if (shield.ShieldMode == DefenseShields.ShieldType.Station && shield.DsState.State.Enhancer) damageMulti = 10000;

            accel.Block.DoDamage(damageMulti, Instance.MpIgnoreDamage, true, null, shield.MyCube.EntityId);
            if (accel.Grid.BlocksCount == 0) accel.Grid.SendGridCloseRequest();
        }

        private static void FewDmgBlocks(CubeAccel accel, DefenseShields shield)
        {
            if (accel.Grid == null) return;

            if (accel.Block.IsDestroyed)
            {
                accel.Grid.EnqueueDestroyedBlock(accel.Block.Position);
                return;
            }
            accel.Block.DoDamage(accel.Block.MaxIntegrity * 0.9f, Instance.MpIgnoreDamage, true, null, shield.MyCube.EntityId);
            if (accel.Grid.BlocksCount == 0) accel.Grid.SendGridCloseRequest();
        }

        private static void CharacterDmg(IMyCharacter character, DefenseShields shield)
        {
            var npcname = character.ToString();
            if (npcname.Equals("Space_Wolf"))
            {
                character.Delete();
                return;
            }
            var hId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hId);
            character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hId, (playerGasLevel * -0.0001f) + .002f);
            MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
            character.DoDamage(50f, Instance.MpIgnoreDamage, true, null, shield.MyCube.EntityId);
            var vel = character.Physics.LinearVelocity;
            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
            var speedDir = Vector3D.Normalize(vel);
            var rnd = new Random();
            var randomSpeed = rnd.Next(10, 20);
            var additionalSpeed = vel + (speedDir * randomSpeed);
            character.Physics.LinearVelocity = additionalSpeed;
        }

        private static void Eject(CubeAccel accel)
        {
            if (accel.Grid == null || accel.Grid.MarkedForClose) return;
            accel.Grid.Physics.LinearVelocity *= -0.25f;
        }

        private static void VoxelDmg(MyVoxelBase voxel, DefenseShields shield)
        {
            if (voxel == null || voxel.RootVoxel.MarkedForClose || voxel.RootVoxel.Closed) return;
            voxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, shield.DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
        }
    }
}

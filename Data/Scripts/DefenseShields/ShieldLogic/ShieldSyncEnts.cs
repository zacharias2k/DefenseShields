namespace DefenseShields
{
    using System;
    using global::DefenseShields.Support;

    using Sandbox.Game;
    using Sandbox.Game.Entities;
    using Sandbox.Game.Entities.Character.Components;
    using Sandbox.Game.Entities.Cube;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;
    using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

    public partial class DefenseShields
    {
        private void SyncThreadedEnts(bool clear = false, bool client = false)
        {
            try
            {
                if (clear)
                {
                    Eject.Clear();
                    DestroyedBlocks.Clear();
                    MissileDmg.Clear();
                    MeteorDmg.Clear();
                    VoxelDmg.Clear();
                    CharacterDmg.Clear();
                    FewDmgBlocks.Clear();
                    DmgBlocks.Clear();
                    ForceData.Clear();
                    ImpulseData.Clear();
                    return;
                }

                try
                {
                    if (!Eject.IsEmpty)
                    {
                        MyCubeGrid myGrid;
                        while (Eject.TryDequeue(out myGrid))
                        {
                            if (myGrid == null || myGrid.MarkedForClose) continue;
                            myGrid.Physics.LinearVelocity *= -0.25f;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in Eject: {ex}"); }

                try
                {
                    if (!ForceData.IsEmpty)
                    {
                        MyAddForceData data;
                        while (ForceData.TryDequeue(out data))
                        {
                            var myGrid = data.MyGrid;
                            if (myGrid == null || myGrid.MarkedForClose) continue;
                            myGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, data.Force, null, Vector3D.Zero, data.MaxSpeed, data.Immediate);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in forceData: {ex}"); }

                try
                {
                    if (!ImpulseData.IsEmpty)
                    {
                        MyImpulseData data;
                        while (ImpulseData.TryDequeue(out data))
                        {
                            var myGrid = data.MyGrid;
                            if (myGrid == null || myGrid.MarkedForClose) continue;
                            myGrid.Physics.ApplyImpulse(data.Direction, data.Position);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in impulseData: {ex}"); }

                if (client) return;

                try
                {
                    if (!DestroyedBlocks.IsEmpty)
                    {
                        IMySlimBlock block;
                        var nullCount = 0;
                        while (DestroyedBlocks.TryDequeue(out block))
                        {
                            var myGrid = block.CubeGrid as MyCubeGrid;
                            if (myGrid == null) continue;
                            EntIntersectInfo entInfo;
                            WebEnts.TryGetValue(myGrid, out entInfo);
                            if (entInfo == null)
                            {
                                nullCount++;
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                continue;
                            }

                            EntIntersectInfo entRemoved;
                            if (nullCount > 0) WebEnts.TryRemove(myGrid, out entRemoved);
                            entInfo.CacheBlockList.Remove(block);
                            myGrid.EnqueueDestroyedBlock(block.Position);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in destroyedBlocks: {ex}"); }

                try
                {
                    if (!MissileDmg.IsEmpty)
                    {
                        MyEntity ent;
                        while (MissileDmg.TryDequeue(out ent))
                        {
                            if (ent == null || !ent.InScene || ent.MarkedForClose) continue;
                            var computedDamage = ComputeAmmoDamage(ent);

                            var damage = computedDamage * DsState.State.ModulateEnergy;
                            if (computedDamage < 0) damage = computedDamage;

                            if (_mpActive)
                            {
                                ShieldDoDamage(damage, ent.EntityId);
                                ent.Close();
                                ent.InScene = false;
                            }
                            else
                            {
                                var rayDir = Vector3D.Normalize(ent.Physics.LinearVelocity);
                                var ray = new RayD(ent.PositionComp.WorldVolume.Center, rayDir);
                                var intersect = CustomCollision.IntersectEllipsoid(DetectMatrixOutsideInv, DetectionMatrix, ray);
                                var hitDist = intersect ?? 0;
                                var hitPos = ray.Position + (ray.Direction * -hitDist);

                                WorldImpactPosition = hitPos;
                                Absorb += damage;
                                ImpactSize = damage;
                                UtilsStatic.CreateFakeSmallExplosion(hitPos);
                                ent.Close();
                                ent.InScene = false;
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (!MeteorDmg.IsEmpty)
                    {
                        IMyMeteor meteor;
                        while (MeteorDmg.TryDequeue(out meteor))
                        {
                            if (meteor == null || meteor.MarkedForClose || meteor.Closed) continue;
                            var damage = 5000 * DsState.State.ModulateKinetic;
                            if (_mpActive)
                            {
                                ShieldDoDamage(damage, meteor.EntityId);
                                meteor.DoDamage(10000f, Session.Instance.DelDamage, true, null, MyGrid.EntityId);
                            }
                            else
                            {
                                WorldImpactPosition = meteor.PositionComp.WorldVolume.Center;
                                Absorb += damage;
                                ImpactSize = damage;
                                meteor.DoDamage(10000f, Session.Instance.DelDamage, true, null, MyGrid.EntityId);
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (!VoxelDmg.IsEmpty)
                    {
                        MyVoxelBase voxel;
                        while (VoxelDmg.TryDequeue(out voxel))
                        {
                            if (voxel == null || voxel.RootVoxel.MarkedForClose || voxel.RootVoxel.Closed) continue;
                            voxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (!CharacterDmg.IsEmpty)
                    {
                        IMyCharacter character;
                        while (CharacterDmg.TryDequeue(out character))
                        {
                            var npcname = character.ToString();
                            if (npcname.Equals("Space_Wolf"))
                            {
                                character.Delete();
                                continue;
                            }
                            var hId = MyCharacterOxygenComponent.HydrogenId;
                            var playerGasLevel = character.GetSuitGasFillLevel(hId);
                            character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hId, (playerGasLevel * -0.0001f) + .002f);
                            MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
                            character.DoDamage(50f, Session.Instance.DelDamage, true, null, MyGrid.EntityId);
                            var vel = character.Physics.LinearVelocity;
                            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
                            var speedDir = Vector3D.Normalize(vel);
                            var rnd = new Random();
                            var randomSpeed = rnd.Next(10, 20);
                            var additionalSpeed = vel + (speedDir * randomSpeed);
                            character.Physics.LinearVelocity = additionalSpeed;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (!DmgBlocks.IsEmpty)
                    {
                        IMySlimBlock block;
                        var damageMulti = 350;
                        if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) damageMulti = 10000;
                        while (DmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            var myGrid = block.CubeGrid as MyCubeGrid;
                            if (block.IsDestroyed)
                            {
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            block.DoDamage(damageMulti, Session.Instance.DelDamage, true, null, myGrid.EntityId); 
                            if (myGrid.BlocksCount == 0) myGrid.SendGridCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in dmgBlocks: {ex}"); }

                try
                {
                    if (!FewDmgBlocks.IsEmpty)
                    {
                        IMySlimBlock block;
                        while (FewDmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            var myGrid = block.CubeGrid as MyCubeGrid;

                            if (block.IsDestroyed)
                            {
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                myGrid.Close();
                                continue;
                            }
                            block.DoDamage(block.MaxIntegrity * 0.9f, Session.Instance.DelDamage, true, null, myGrid.EntityId); 
                            if (myGrid.BlocksCount == 0) myGrid.SendGridCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in fewBlocks: {ex}"); }

                try
                {
                    if (!EmpBlast.IsEmpty)
                    {
                        var stackCount = 0;
                        var warHeadYield = 0d;
                        var epiCenter = Vector3D.Zero;
                        var empResistenceRatio = 1f;
                        var attackerId = 0L;
                        var energyResistenceRatio = DsState.State.ModulateKinetic;

                        if (DsState.State.EmpProtection)
                        {
                            if (energyResistenceRatio < 0.4) energyResistenceRatio = 0.4f;
                            empResistenceRatio = 0.1f;
                        }

                        foreach (var empChild in EmpBlast)
                        {
                            if (empChild.Value.CustomData == string.Empty || !empChild.Value.CustomData.Contains("!EMP"))
                            {
                                var entityId = empChild.Key;
                                if (entityId != 0) attackerId = entityId;
                                warHeadYield = empChild.Value.Yield;
                                epiCenter += empChild.Value.Position;
                                stackCount++;
                            }
                        }
                        EmpBlast.Clear();
                        if (stackCount == 0) return;
                        epiCenter /= stackCount;
                        var line = new LineD(epiCenter, SOriBBoxD.Center);
                        var testDir = Vector3D.Normalize(line.From - line.To);
                        var ray = new RayD(line.From, -testDir);
                        var ellipsoid = CustomCollision.IntersectEllipsoid(DetectMatrixOutsideInv, DetectionMatrix, ray);
                        if (!ellipsoid.HasValue) return;
                        var impactPos = line.From + (testDir * -ellipsoid.Value);
                        IHitInfo hitInfo;
                        MyAPIGateway.Physics.CastRay(epiCenter, impactPos, out hitInfo, CollisionLayers.DefaultCollisionLayer);
                        if (hitInfo != null) 
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"[EmpBlast] {((MyEntity)hitInfo.HitEntity).DebugName} occluded EMP for:{MyGrid.DebugName}");
                            return;
                        }
                        var gridLocalMatrix = MyGrid.PositionComp.LocalMatrix;
                        var worldDirection = impactPos - gridLocalMatrix.Translation;
                        var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(gridLocalMatrix));
                        var hitFaceSurfaceArea = UtilsStatic.GetIntersectingSurfaceArea(ShieldShapeMatrix, localPosition);

                        var empDirYield = (warHeadYield * stackCount) * 0.5;
                        var rangeCap = MathHelper.Clamp(stackCount * warHeadYield, warHeadYield, Session.Instance.SyncDist);
                        var invSqrDist = UtilsStatic.InverseSqrDist(epiCenter, impactPos, rangeCap);
                        var damageScaler = invSqrDist * hitFaceSurfaceArea;
                        if (invSqrDist < 0)
                        {
                            Log.Line($"insqrDist was 0, should never happen!!!");
                            return;
                        }

                        var targetDamage = (float)(((empDirYield * damageScaler) * energyResistenceRatio) * empResistenceRatio);

                        if (targetDamage >= DsState.State.Buffer * Session.Enforced.Efficiency) _empOverLoad = true;
                        //if (Session.Enforced.Debug == 4) Log.Line($"targetDist:{Vector3D.Distance(epiCenter, impactPos)} - invSqrDist:{invSqrDist} - RangeCap:{rangeCap} - SurfaceA:{hitFaceSurfaceArea}({_ellipsoidSurfaceArea * 0.5}) - targetDamage:{targetDamage} - toOver:({(targetDamage / (DsState.State.Buffer * Session.Enforced.Efficiency))}) - warheadYield:{warHeadYield} - numInStack:{stackCount} - directYield:{empDirYield} - damageScaler:{damageScaler} - energyRatio:{energyResistenceRatio} - empRatio:{empResistenceRatio}");

                        if (_isServer && _mpActive)
                            AddEmpBlastHit(attackerId, targetDamage, "MPEMP", impactPos);

                        WorldImpactPosition = epiCenter;
                        Absorb += targetDamage;
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in EmpBlast: {ex}"); }
            }
            catch (Exception ex) { Log.Line($"Exception in DamageGrids: {ex}"); }
        }
    }
}

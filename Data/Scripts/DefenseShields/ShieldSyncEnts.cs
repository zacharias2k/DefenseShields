using System;
using DefenseShields.Support;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void SyncThreadedEnts(bool clear = false, bool client = false)
        {
            try
            {
                if (clear)
                {
                    Eject.Clear();
                    _destroyedBlocks.Clear();
                    _missileDmg.Clear();
                    _meteorDmg.Clear();
                    _voxelDmg.Clear();
                    _characterDmg.Clear();
                    _fewDmgBlocks.Clear();
                    _dmgBlocks.Clear();
                    _empDmg.Clear();
                    _forceData.Clear();
                    _impulseData.Clear();
                    return;
                }

                if (_destroyedBlocks.Count == 0 && _missileDmg.Count == 0 && _meteorDmg.Count == 0 &&
                    _voxelDmg.Count == 0 && _characterDmg.Count == 0 && _fewDmgBlocks.Count == 0 &&
                    _dmgBlocks.Count == 0 && _empDmg.Count == 0 && _forceData.Count == 0 && _impulseData.Count == 0) return;

                if (!client && Session.Enforced.Debug >= 1) Dsutil4.Sw.Restart();

                try
                {
                    if (_forceData.Count != 0)
                    {
                        MyAddForceData data;
                        while (_forceData.TryDequeue(out data))
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
                    if (_impulseData.Count != 0)
                    {
                        MyImpulseData data;
                        while (_impulseData.TryDequeue(out data))
                        {
                            var myGrid = data.MyGrid;
                            if (myGrid == null || myGrid.MarkedForClose) continue;
                            myGrid.Physics.ApplyImpulse(data.Direction, data.Position);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in impulseData: {ex}"); }

                if (client) return;

                var destroyedLen = _destroyedBlocks.Count;
                try
                {
                    if (destroyedLen != 0)
                    {
                        IMySlimBlock block;
                        var nullCount = 0;
                        while (_destroyedBlocks.TryDequeue(out block))
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
                    if (_missileDmg.Count != 0)
                    {
                        MyEntity ent;
                        while (_missileDmg.TryDequeue(out ent))
                        {
                            if (ent == null || ent.MarkedForClose || ent.Closed) continue;
                            var computedDamage = ComputeAmmoDamage(ent);
                            if (computedDamage <= float.NegativeInfinity)
                            {
                                FriendlyCache.Add(ent);
                                continue;
                            }

                            var damage = computedDamage * DsState.State.ModulateEnergy;
                            if (computedDamage < 0) damage = computedDamage;

                            if (_mpActive)
                            {
                                ShieldDoDamage(damage, ent.EntityId);
                                ent.Close();
                            }
                            else
                            {
                                WorldImpactPosition = ent.PositionComp.WorldVolume.Center;
                                Absorb += damage;
                                ImpactSize = damage;
                                UtilsStatic.CreateFakeSmallExplosion(ent.PositionComp.WorldAABB.Center);
                                ent.Close();
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_meteorDmg.Count != 0)
                    {
                        IMyMeteor meteor;
                        while (_meteorDmg.TryDequeue(out meteor))
                        {
                            if (meteor == null || meteor.MarkedForClose || meteor.Closed) continue;
                            var damage = 5000 * DsState.State.ModulateKinetic;
                            if (_mpActive)
                            {

                                ShieldDoDamage(damage, meteor.EntityId);
                                meteor.DoDamage(10000f, DelDamage, true, null, MyGrid.EntityId);
                            }
                            else
                            {
                                WorldImpactPosition = meteor.PositionComp.WorldVolume.Center;
                                Absorb += damage;
                                ImpactSize = damage;
                                meteor.DoDamage(10000f, DelDamage, true, null, MyGrid.EntityId);
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_voxelDmg.Count != 0)
                    {
                        MyVoxelBase voxel;
                        while (_voxelDmg.TryDequeue(out voxel))
                        {
                            if (voxel == null || voxel.RootVoxel.MarkedForClose || voxel.RootVoxel.Closed) continue;
                            voxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_characterDmg.Count != 0)
                    {
                        IMyCharacter character;
                        while (_characterDmg.TryDequeue(out character))
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
                            character.DoDamage(50f, DelDamage, true, null, MyGrid.EntityId);
                            var vel = character.Physics.LinearVelocity;
                            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
                            var speedDir = Vector3D.Normalize(vel);
                            var rnd = new Random();
                            var randomSpeed = rnd.Next(10, 20);
                            var additionalSpeed = vel + speedDir * randomSpeed;
                            character.Physics.LinearVelocity = additionalSpeed;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_dmgBlocks.Count != 0)
                    {
                        IMySlimBlock block;
                        var damageMulti = 350;
                        if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) damageMulti = 10000;
                        while (_dmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            var myGrid = block.CubeGrid as MyCubeGrid;
                            if (block.IsDestroyed)
                            {
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            block.DoDamage(damageMulti, DelDamage, true, null, MyGrid.EntityId); 
                            if (myGrid.BlocksCount == 0) myGrid.SyncObject.SendCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in dmgBlocks: {ex}"); }

                try
                {
                    if (_fewDmgBlocks.Count != 0)
                    {
                        IMySlimBlock block;
                        while (_fewDmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            var myGrid = block.CubeGrid as MyCubeGrid;

                            if (block.IsDestroyed)
                            {
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                myGrid.Close();
                                continue;
                            }
                            block.DoDamage(block.MaxIntegrity * 0.9f, DelDamage, true, null, MyGrid.EntityId); 
                            if (myGrid.BlocksCount == 0) myGrid.SyncObject.SendCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in fewBlocks: {ex}"); }

                try
                {
                    if (_empDmg.Count != 0)
                    {
                        IMyWarhead block;
                        while (_empDmg.TryDequeue(out block))
                        {
                            if (block == null || block.MarkedForClose || block.Closed) continue;
                            var myGrid = block.CubeGrid as MyCubeGrid;

                            if (block.SlimBlock.IsDestroyed)
                            {
                                myGrid.EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            UtilsStatic.CreateExplosion(block.PositionComp.WorldAABB.Center, 2.1f, 9999);
                            if (myGrid.BlocksCount == 0) myGrid.SyncObject.SendCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in fewBlocks: {ex}"); }

                if (Session.Enforced.Debug >= 1) Dsutil4.StopWatchReport($"SyncEnt: ShieldId [{Shield.EntityId}]", 3);
            }
            catch (Exception ex) { Log.Line($"Exception in DamageGrids: {ex}"); }
        }
    }
}

namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using Support;
    using Sandbox.Game.Entities;
    using VRage.Game;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class Session
    {
        #region DamageHandler
        private readonly long[] _nodes = new long[1000];
        private readonly Dictionary<long, MyEntity> _backingDict = new Dictionary<long, MyEntity>(1001);
        private int _emptySpot;
        private MyEntity _previousEnt;
        private long _previousEntId = -1;

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                var block = target as IMySlimBlock;
                if (block != null)
                {

                    var damageType = info.Type;
                    if (damageType == MpIgnoreDamage || damageType == MyDamageType.Drill || damageType == MyDamageType.Grind) return;

                    var myGrid = block.CubeGrid as MyCubeGrid;

                    if (myGrid == null) return;

                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myGrid, out protectors);
                    if (protectors == null) return;

                    MyEntity hostileEnt;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);

                    var shieldHitPos = Vector3D.NegativeInfinity;
                    MyEntity trueAttacker = null;
                    DefenseShields blockingShield = null;

                    try
                    {
                        if (hostileEnt != null)
                        {
                            MyCubeGrid grid;
                            if (damageType != MyDamageType.Environment) grid = hostileEnt as MyCubeGrid;
                            else grid = hostileEnt.Parent as MyCubeGrid;
                            if (grid == null)
                            {
                                var hostileCube = hostileEnt.Parent as MyCubeBlock;
                                trueAttacker = (hostileCube ?? (hostileEnt as IMyGunBaseUser)?.Owner) ?? hostileEnt;
                            }
                            else trueAttacker = grid;

                            Vector3D originHit;
                            block.ComputeWorldCenter(out originHit);
                            var line = new LineD(trueAttacker.PositionComp.WorldAABB.Center, originHit);
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);
                            var hitDist = double.MaxValue;
                            foreach (var shield in protectors.Shields)
                            {
                                var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!shieldActive) continue;
                                var intersectDist = CustomCollision.IntersectEllipsoid(shield.DetectMatrixOutsideInv, shield.DetectionMatrix, ray);
                                var ellipsoid = intersectDist ?? 0;
                                var intersect = ellipsoid > 0 && line.Length > ellipsoid;
                                if (intersect && ellipsoid <= hitDist)
                                {
                                    hitDist = ellipsoid;
                                    shieldHitPos = line.From + (testDir * -ellipsoid);
                                    blockingShield = shield;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in DamageFindShield {_previousEnt == null}: {ex}"); }

                    try
                    {
                        var activeProtector = blockingShield != null && blockingShield.DsState.State.Online && !blockingShield.DsState.State.Lowered && protectors.Shields.Contains(blockingShield);
                        if (activeProtector)
                        {
                            if (!IsServer && !blockingShield.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }
                            var isExplosionDmg = damageType == MyDamageType.Explosion;
                            var isDeformationDmg = damageType == MyDamageType.Deformation;
                            if (trueAttacker is MyVoxelBase || (trueAttacker is MyCubeGrid && isDeformationDmg && blockingShield.ModulateGrids))
                            {
                                blockingShield.DeformEnabled = true;
                                return;
                            }
                            if (damageType == Bypass)
                            {
                                blockingShield.DeformEnabled = true;
                                return;
                            }
                            if (damageType == DSdamage || damageType == DSheal || damageType == DSbypass)
                            {
                                info.Amount = 0f;
                                return;
                            }

                            if (!isDeformationDmg && !isExplosionDmg)
                            {
                                blockingShield.ExplosionEnabled = false;
                                blockingShield.DeformEnabled = false;
                                protectors.IgnoreAttackerId = -1;
                            }
                            else if (!blockingShield.DeformEnabled && trueAttacker == null)
                            {
                                info.Amount = 0;
                                return;
                            }

                            var bullet = damageType == MyDamageType.Bullet;
                            if (bullet || isDeformationDmg) info.Amount = info.Amount * blockingShield.DsState.State.ModulateEnergy;
                            else info.Amount = info.Amount * blockingShield.DsState.State.ModulateKinetic;

                            var noHits = !DedicatedServer && blockingShield.Absorb < 1 && blockingShield.WorldImpactPosition == Vector3D.NegativeInfinity;
                            var hitSlotAvailable = noHits & (bullet && blockingShield.KineticCoolDown == -1) || (!bullet && blockingShield.EnergyCoolDown == -1);
                            if (hitSlotAvailable)
                            {
                                blockingShield.WorldImpactPosition = shieldHitPos;
                                blockingShield.ImpactSize = info.Amount;
                                if (!bullet) blockingShield.EnergyHit = true;
                            }
                            if (isDeformationDmg && trueAttacker != null) protectors.IgnoreAttackerId = attackerId;

                            var fatBlock = block.FatBlock as MyCubeBlock;
                            if (fatBlock != null)
                            {
                                lock (blockingShield.DirtyCubeBlocks)
                                {
                                    blockingShield.EffectsDirty = true;
                                    blockingShield.DirtyCubeBlocks[fatBlock] = Tick;
                                    blockingShield.DirtyCubeBlocks.ApplyAdditionsAndModifications();
                                }
                            }

                            blockingShield.Absorb += info.Amount;
                            info.Amount = 0f;
                            return;
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in DamageHandlerActive {_previousEnt == null}: {ex}"); }

                    var iShield = protectors.IntegrityShield;
                    if (iShield != null && iShield.DsState.State.Online && !iShield.DsState.State.Lowered)
                    {
                        var attackingVoxel = trueAttacker as MyVoxelBase;
                        if (attackingVoxel != null || trueAttacker is MyCubeGrid) iShield.DeformEnabled = true;
                        else if (trueAttacker != null) iShield.DeformEnabled = false;

                        if (damageType == MyDamageType.Deformation && iShield.DeformEnabled)
                        {
                            if (attackingVoxel != null)
                            {
                                if (iShield.Absorb < 1 && iShield.WorldImpactPosition == Vector3D.NegativeInfinity && iShield.KineticCoolDown == -1)
                                {
                                    attackingVoxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One, iShield.DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
                                }
                            }
                            var dmgAmount = info.Amount * 10;
                            if (IsServer)
                            {
                                iShield.AddShieldHit(attackerId, dmgAmount, damageType, block, false);
                                iShield.Absorb += dmgAmount;
                            }
                            info.Amount = 0;
                            return;
                        }
                    }

                    if (info.AttackerId == protectors.IgnoreAttackerId && damageType == MyDamageType.Deformation)
                    {
                        if (Enforced.Debug >= 2) Log.Line($"old Del/Mp Attacker, ignoring: {damageType} - {info.Amount} - attackerId:{attackerId}");
                        info.Amount = 0;
                        return;
                    }
                    protectors.IgnoreAttackerId = -1;

                    if (Enforced.Debug >= 2) Log.Line($"[Uncaught Damage] Type:{damageType} - Amount:{info.Amount} - nullHostileEnt:{trueAttacker == null} - nullShield:{blockingShield == null} - iShell:{protectors.IntegrityShield != null} - protectorShields:{protectors.Shields.Count} - attackerId:{info.AttackerId}");
                }
                else if (target is IMyCharacter) CharacterProtection(target, info);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler {_previousEnt == null}: {ex}"); }
        }

        private void CharacterProtection(object target, MyDamageInformation info)
        {
            if (info.Type == MpIgnoreDamage || info.Type == MyDamageType.LowPressure) return;

            var myEntity = target as MyEntity;
            if (myEntity == null) return;

            MyProtectors protectors;
            GlobalProtect.TryGetValue(myEntity, out protectors);
            if (protectors == null) return;

            foreach (var shield in protectors.Shields)
            {
                var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                if (!shieldActive) continue;

                MyEntity hostileEnt;
                var attackerId = info.AttackerId;
                if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                else UpdatedHostileEnt(attackerId, out hostileEnt);

                var nullAttacker = hostileEnt == null;
                var playerProtected = false;

                ProtectCache protectedEnt;
                if (nullAttacker)
                {
                    shield.ProtectedEntCache.TryGetValue(myEntity, out protectedEnt);
                    if (protectedEnt != null && protectedEnt.Relation == DefenseShields.Ent.Protected) playerProtected = true;
                }
                else
                {
                    shield.ProtectedEntCache.TryGetValue(hostileEnt, out protectedEnt);
                    if (protectedEnt != null && protectedEnt.Relation != DefenseShields.Ent.Protected) playerProtected = true;
                }

                if (!playerProtected) continue;
                info.Amount = 0f;
                myEntity.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
            }
        }

        private void UpdatedHostileEnt(long attackerId, out MyEntity ent)
        {
            if (attackerId == 0)
            {
                ent = null;
                return;
            }
            MyEntity tmpPreviousEnt;
            if (_backingDict.TryGetValue(attackerId, out tmpPreviousEnt))
            {
                if (!tmpPreviousEnt.MarkedForClose)
                {
                    _previousEnt = tmpPreviousEnt;
                    _previousEntId = attackerId;
                    ent = tmpPreviousEnt;
                    return;
                }
                _backingDict.Remove(attackerId);
            }
            if (MyEntities.TryGetEntityById(attackerId, out _previousEnt))
            {
                if (_emptySpot + 1 >= _nodes.Length) _backingDict.Remove(_nodes[0]);
                _nodes[_emptySpot] = attackerId;
                _backingDict.Add(attackerId, _previousEnt);

                if (_emptySpot++ >= _nodes.Length) _emptySpot = 0;

                _previousEntId = attackerId;
                ent = _previousEnt;
                return;
            }
            ent = null;
            _previousEntId = -1;
        }
        #endregion
    }
}

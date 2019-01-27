namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using global::DefenseShields.Support;
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
                    if (damageType == MyDamageType.Drill || damageType == MyDamageType.Grind) return;

                    var myEntity = block.CubeGrid as MyEntity;

                    if (myEntity == null) return;

                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myEntity, out protectors);
                    if (protectors == null) return;

                    var attackerId = info.AttackerId;

                    MyEntity hostileEnt;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);

                    var shieldHitPos = Vector3D.NegativeInfinity;
                    MyEntity trueAttacker = null;
                    if (hostileEnt != null)
                    {
                        var blockingShield = false;
                        var notBlockingShield = false;
                        protectors.ProtectDamageReset();

                        MyCubeGrid myGrid;
                        if (damageType != MyDamageType.Environment) myGrid = hostileEnt as MyCubeGrid;
                        else myGrid = hostileEnt.Parent as MyCubeGrid;
                        if (myGrid == null)
                        {
                            var hostileCube = hostileEnt.Parent as MyCubeBlock;
                            trueAttacker = (hostileCube ?? (hostileEnt as IMyGunBaseUser)?.Owner) ?? hostileEnt;
                        }
                        else trueAttacker = myGrid;

                        protectors.OriginBlock = block;
                        protectors.OriginBlock.ComputeWorldCenter(out protectors.OriginHit);
                        var line = new LineD(trueAttacker.PositionComp.WorldAABB.Center, protectors.OriginHit);
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
                            var noIntersect = ellipsoid <= 0 || line.Length < ellipsoid;
                            if (intersect && ellipsoid <= hitDist)
                            {
                                hitDist = ellipsoid;
                                shieldHitPos = line.From + (testDir * -ellipsoid);
                                protectors.BlockingShield = shield;
                                notBlockingShield = false;
                                blockingShield = true;
                            }
                            else if (noIntersect && protectors.BlockingShield == null)
                            {
                                protectors.NotBlockingShield = shield;
                                protectors.NotBlockingAttackerId = attackerId;
                                protectors.NotBlockingMainDamageType = damageType;
                                notBlockingShield = true;
                                blockingShield = false;
                            }
                        }
                        if (!blockingShield) protectors.BlockingShield = null;
                        if (!notBlockingShield) protectors.NotBlockingShield = null;
                    }

                    var activeProtector = protectors.BlockingShield != null && protectors.BlockingShield.DsState.State.Online && !protectors.BlockingShield.DsState.State.Lowered && protectors.Shields.Contains(protectors.BlockingShield);
                    if (activeProtector)
                    {
                        var shield = protectors.BlockingShield;

                        if (!IsServer && !shield.WarmedUp)
                        {
                            info.Amount = 0;
                            return;
                        }
                        var isExplosionDmg = damageType == MyDamageType.Explosion;
                        var isDeformationDmg = damageType == MyDamageType.Deformation;
                        if (trueAttacker is MyVoxelBase || (trueAttacker is MyCubeGrid && isDeformationDmg && shield.ModulateGrids))
                        {
                            shield.DeformEnabled = true;
                            return;
                        }
                        if (damageType == Bypass)
                        {
                            shield.DeformEnabled = true;
                            return;
                        }
                        if (damageType == DSdamage || damageType == DSheal || damageType == DSbypass)
                        {
                            info.Amount = 0f;
                            return;
                        }

                        if (!isDeformationDmg && !isExplosionDmg)
                        {
                            if (!IsServer) protectors.NotBlockingShield = null;
                            shield.ExplosionEnabled = false;
                            shield.DeformEnabled = false;
                            protectors.IgnoreAttackerId = -1;
                        }
                        else if (!shield.DeformEnabled && trueAttacker == null)
                        {
                            info.Amount = 0;
                            return;
                        }

                        var bullet = damageType == MyDamageType.Bullet;
                        if (bullet || isDeformationDmg) info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;
                        else info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;

                        if (!DedicatedServer && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                        {
                            shield.WorldImpactPosition = shieldHitPos;
                            shield.ImpactSize = info.Amount;
                        }
                        if (isDeformationDmg && trueAttacker != null) protectors.IgnoreAttackerId = attackerId;
                        shield.Absorb += info.Amount;
                        info.Amount = 0f;
                        return;
                    }

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
                                if (iShield.Absorb < 1 && iShield.WorldImpactPosition == Vector3D.NegativeInfinity && iShield.BulletCoolDown == -1)
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

                    if (IsServer)
                    {
                        if (protectors.NotBlockingShield != null && protectors.NotBlockingMainDamageType == MyDamageType.Explosion)
                        {
                            if (Enforced.Debug >= 2) Log.Line($"Sending origin explosion MpAllowDamage: {damageType} - {info.Amount} - {protectors.OriginBlock.Position} - {protectors.NotBlockingAttackerId} - {attackerId}");
                            protectors.NotBlockingShield.AddShieldHit(protectors.NotBlockingAttackerId, info.Amount, MpAllowDamage, block, true);
                        }
                    }

                    if (info.AttackerId == protectors.IgnoreAttackerId && damageType == MyDamageType.Deformation)
                    {
                        if (Enforced.Debug >= 2) Log.Line($"old Del/Mp Attacker, ignoring: {damageType} - {info.Amount} - attackerId:{attackerId}");
                        info.Amount = 0;
                        return;
                    }
                    protectors.IgnoreAttackerId = -1;

                    if (!IsServer && attackerId == 0 && (damageType == MyDamageType.Deformation || damageType == MyDamageType.Explosion))
                    {
                        LastMpEventTick = Tick;
                        _monitorBlocks.Enqueue(new MonitorBlock(block, info.Amount, damageType, Tick));
                        return;
                    }

                    if (Enforced.Debug >= 2) Log.Line($"[Uncaught Damage] Type:{damageType} - Amount:{info.Amount} - nullHostileEnt:{trueAttacker == null} - nullShield:{protectors.BlockingShield == null} - iShell:{protectors.IntegrityShield != null} - protectorShields:{protectors.Shields.Count} - attackerId:{info.AttackerId}");
                }
                else if (target is IMyCharacter) CharacterProtection(target, info);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }

        private void CharacterProtection(object target, MyDamageInformation info)
        {
            if (info.Type == MyDamageType.LowPressure) return;

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
            if (_backingDict.TryGetValue(attackerId, out _previousEnt))
            {
                ent = _previousEnt;
                _previousEntId = attackerId;
                return;
            }
            if (MyEntities.TryGetEntityById(attackerId, out _previousEnt))
            {
                if (_emptySpot + 1 >= _nodes.Length) _backingDict.Remove(_nodes[0]);
                _nodes[_emptySpot] = attackerId;
                _backingDict.Add(attackerId, _previousEnt);

                if (_emptySpot++ >= _nodes.Length) _emptySpot = 0;

                ent = _previousEnt;
                _previousEntId = attackerId;
                return;
            }
            _previousEnt = null;
            ent = null;
            _previousEntId = -1;
        }
        #endregion
    }
}

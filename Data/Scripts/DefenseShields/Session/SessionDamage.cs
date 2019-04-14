using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    public partial class Session
    {
        #region DamageHandler
        private readonly long[] _nodes = new long[1000];
        private readonly ConcurrentDictionary<long, MyEntity> _backingDict = new ConcurrentDictionary<long, MyEntity>();
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

                    if (!IsServer && attackerId != 0 && hostileEnt == null) ForceEntity(out hostileEnt);

                    MyEntity trueAttacker = null;
                    var isVoxelBase = false;

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
                                if (trueAttacker is MyVoxelBase) isVoxelBase = true;
                            }
                            else trueAttacker = grid;

                            protectors.LastAttackerWasInside = true;
                            Vector3D originHit;
                            block.ComputeWorldCenter(out originHit);

                            var line = new LineD(trueAttacker.PositionComp.WorldAABB.Center, originHit);
                            var lineLength = (float)line.Length;
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);
                            var hitDist = double.MaxValue;
                            foreach (var c in protectors.Controllers)
                            {
                                var b = c.Bus;
                                var f = b.Field;
                                c.Asleep = false;
                                c.LastWokenTick = Tick;

                                var shieldActive = c.State.Value.Online && !c.State.Value.Lowered;
                                if (!shieldActive) continue;
                                var intersectDist = CustomCollision.IntersectEllipsoid(f.DetectMatrixOutsideInv, f.DetectionMatrix, ray);

                                var ellipsoid = intersectDist ?? 0;

                                var notContained = isVoxelBase || ellipsoid <= 0 && f.ShieldIsMobile && !CustomCollision.PointInShield(trueAttacker.PositionComp.WorldAABB.Center, MatrixD.Invert(f.ShieldShapeMatrix * b.Spine.WorldMatrix));
                                if (notContained) ellipsoid = lineLength;

                                var intersect = ellipsoid > 0 && lineLength + 1 >= ellipsoid;
                                if (intersect && ellipsoid <= hitDist)
                                {
                                    protectors.LastAttackerWasInside = false;
                                    hitDist = ellipsoid;
                                    protectors.BlockingController = c;
                                    protectors.BlockingTick = Tick;
                                }
                            }
                        }
                        if (Tick - protectors.BlockingTick > 10 && protectors.LastAttackerWasInside) protectors.BlockingController = null;
                    }
                    catch (Exception ex) { Log.Line($"Exception in DamageFindShield: {ex}"); }

                    try
                    {
                        var activeProtector = protectors.BlockingController != null && protectors.BlockingController.State.Value.Online && !protectors.BlockingController.State.Value.Lowered;
                        if (activeProtector)
                        {
                            var c = protectors.BlockingController;
                            var b = c.Bus;
                            var f = b.Field;
                            if (!IsServer && !c.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }
                            var isExplosionDmg = damageType == MyDamageType.Explosion;
                            var isDeformationDmg = damageType == MyDamageType.Deformation;

                            if (isVoxelBase)
                            {
                                f.DeformEnabled = true;
                                return;
                            }
                            if (damageType == Bypass)
                            {
                                f.DeformEnabled = true;
                                return;
                            }

                            if (!isDeformationDmg && !isExplosionDmg)
                            {
                                f.DeformEnabled = false;
                                protectors.IgnoreAttackerId = -1;
                            }
                            else if (f.DeformEnabled && trueAttacker == null)
                            {
                                return;
                            }
                            else if (!f.DeformEnabled && trueAttacker == null)
                            {
                                info.Amount = 0;
                                return;
                            }

                            var bullet = damageType == MyDamageType.Bullet;
                            if (bullet || isDeformationDmg) info.Amount = info.Amount * c.State.Value.ModulateEnergy;
                            else info.Amount = info.Amount * c.State.Value.ModulateKinetic;

                            var noHits = !DedicatedServer && f.Absorb < 1;
                            var hitSlotAvailable = noHits & (bullet && f.KineticCoolDown == -1) || (!bullet && f.EnergyCoolDown == -1);
                            if (hitSlotAvailable)
                            {
                                lock (f.HandlerImpact)
                                {
                                    if (trueAttacker != null && block != null)
                                    {
                                        f.HandlerImpact.Attacker = trueAttacker;
                                        f.HandlerImpact.HitBlock = block;
                                        f.ImpactSize = info.Amount;
                                        f.HandlerImpact.Active = true;
                                        if (!bullet) f.EnergyHit = true;
                                    }
                                }
                            }
                            if (isDeformationDmg && trueAttacker != null) protectors.IgnoreAttackerId = attackerId;
                            f.Absorb += info.Amount;
                            info.Amount = 0f;
                            return;
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in DamageHandlerActive: {ex}"); }

                    var notBubble = protectors.NotBubble;
                    if (notBubble != null && notBubble.State.Value.Online && !notBubble.State.Value.Lowered)
                    {
                        var c = notBubble;
                        var b = c.Bus;
                        var f = b.Field;
                        var mode = notBubble.State.Value.ProtectMode;
                        if (mode == 1)
                        {
                            var attackingVoxel = trueAttacker as MyVoxelBase;
                            if (attackingVoxel != null || trueAttacker is MyCubeGrid) f.DeformEnabled = true;
                            else if (trueAttacker != null) f.DeformEnabled = false;

                            if (damageType == MyDamageType.Deformation && f.DeformEnabled)
                            {
                                if (attackingVoxel != null)
                                {
                                    if (f.Absorb < 1 && f.WorldImpactPosition == Vector3D.NegativeInfinity && f.KineticCoolDown == -1)
                                    {
                                        attackingVoxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One, f.DetectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
                                    }
                                }
                                var dmgAmount = info.Amount * 10;
                                if (IsServer)
                                {
                                    f.AddShieldHit(attackerId, dmgAmount, damageType, block, false);
                                    f.Absorb += dmgAmount;
                                }
                                info.Amount = 0;
                                return;
                            }
                        }
                        else if (mode == 2)
                        {
                            info.Amount *= 0.1f;
                            var regen = protectors.NotBubble;
                            regen.Bus.HitBlocks.Add(block);
                            regen.Bus.Regening = true;
                        }
                    }

                    if (info.AttackerId == protectors.IgnoreAttackerId && damageType == MyDamageType.Deformation)
                    {
                        if (Enforced.Debug >= 2) Log.Line($"old Del/Mp Attacker, ignoring: {damageType} - {info.Amount} - attackerId:{attackerId}");
                        info.Amount = 0;
                        return;
                    }
                    protectors.IgnoreAttackerId = -1;
                    if (Enforced.Debug >= 2) Log.Line($"[Uncaught Damage] Type:{damageType} - Amount:{info.Amount} - nullTrue:{trueAttacker == null} - nullHostile:{hostileEnt == null} - nullShield:{protectors.BlockingController == null} - notBubble:{protectors.NotBubble != null} - protectorShields:{protectors.Controllers.Count} - attackerId:{info.AttackerId}");
                }
                else if (target is IMyCharacter) CharacterProtection(target, info);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler {_previousEnt == null}: {ex}"); }
        }
        /*
        public void AfterDamage(object target, MyDamageInformation info)
        {
            try
            {
                var block = target as IMySlimBlock;
                var grid = block?.CubeGrid as MyCubeGrid;
                if (grid != null)
                {
                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(grid, out protectors);
                    if (protectors?.NotBubble == null || protectors.NotBubble.State.Value.ProtectMode != 2) return;
                    var regen = protectors.NotBubble;
                    regen.Bus.QueuedBlocks.Enqueue(block);
                    regen.Bus.Regening = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CheckDamage: {ex}"); }
        }
        */
        private void CharacterProtection(object target, MyDamageInformation info)
        {
            if (info.Type == MpIgnoreDamage || info.Type == MyDamageType.LowPressure) return;
            var myEntity = target as MyEntity;
            if (myEntity == null) return;

            MyProtectors protectors;
            GlobalProtect.TryGetValue(myEntity, out protectors);
            if (protectors == null) return;

            foreach (var c in protectors.Controllers)
            {
                var b = c.Bus;
                var f = b.Field;
                var controllerOnline = c.State.Value.Online && !c.State.Value.Lowered;
                if (!controllerOnline) continue;

                MyEntity hostileEnt;
                var attackerId = info.AttackerId;
                if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                else UpdatedHostileEnt(attackerId, out hostileEnt);

                var nullAttacker = hostileEnt == null;
                var playerProtected = false;

                ProtectCache protectedEnt;
                if (nullAttacker)
                {
                    f.ProtectedEntCache.TryGetValue(myEntity, out protectedEnt);
                    if (protectedEnt != null && protectedEnt.Relation == Fields.Ent.Protected) playerProtected = true;
                }
                else
                {
                    f.ProtectedEntCache.TryGetValue(hostileEnt, out protectedEnt);
                    if (protectedEnt != null && protectedEnt.Relation != Fields.Ent.Protected) playerProtected = true;
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
                _backingDict.TryAdd(attackerId, _previousEnt);

                if (_emptySpot++ >= _nodes.Length) _emptySpot = 0;

                _previousEntId = attackerId;
                ent = _previousEnt;
                return;
            }
            ent = null;
            _previousEntId = -1;
        }

        private static void ForceEntity(out MyEntity hostileEnt)
        {
            hostileEnt = MyAPIGateway.Session.ControlledObject?.Entity as MyEntity;
            if (hostileEnt?.Parent != null) hostileEnt = hostileEnt.Parent;
            if (hostileEnt == null) hostileEnt = MyAPIGateway.Session.Player.Character as MyEntity;
        }
        #endregion
    }
}

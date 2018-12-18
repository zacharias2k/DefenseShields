using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class Session
    {
        #region DamageHandler
        private readonly long[] _nodes = new long[1000];
        private int _emptySpot;
        private readonly Dictionary<long, MyEntity> _backingDict = new Dictionary<long, MyEntity>(1001);
        private MyEntity _previousEnt;
        private long _previousEntId = -1;

        private DefenseShields _blockingShield = null;
        private DefenseShields _clientExpShield = null;
        private IMySlimBlock _originBlock;
        private long _ignoreAttackerId;
        private Vector3D _originHit;

        public void UpdatedHostileEnt(long attackerId, out MyEntity ent)
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

        public void CheckDamage(object target, ref MyDamageInformation info)
        {
            try
            {
                //if (Enforced.Debug == 4) Log.Line($"CheckDamageRoot:{info.Type} - {info.Amount} - {info.AttackerId}");
                var block = target as IMySlimBlock;
                if (!IsServer && block != null && info.Type == MpDoExplosion) // I hate this damage handler!
                {
                    _clientExpShield = block.FatBlock?.GameLogic as DefenseShields;

                    if (_clientExpShield != null) _clientExpShield.ExplosionEnabled = true;
                    if (Enforced.Debug == 4) Log.Line($"[MpDoDamage] Type:{info.Type} - Amount:{info.Amount} - AttackerId:{info.AttackerId}");
                    info.Amount = 0;
                    return;
                }

                if (info.Type == MPdamage)
                {
                    if (block == null)
                    {
                        Log.Line($"MP-shield block is null - Amount:{info.Amount}");
                        info.Amount = 0;
                        return;
                    }

                    var myCube = block.FatBlock as MyCubeBlock;
                    if (myCube == null)
                    {
                        Log.Line($"MP-shield myCube is null - Amount:{info.Amount}");
                        info.Amount = 0;
                        return;
                    }

                    var ds = myCube.GameLogic as DefenseShields;

                    if (ds == null)
                    {
                        if (Enforced.Debug == 4) Log.Line($"MP-shield ds is null - Amount:{info.Amount}");
                        info.Amount = 0;
                        return;
                    }

                    if (!DedicatedServer)
                    {
                        var shieldActive = ds.DsState.State.Online && !ds.DsState.State.Lowered;
                        if (!shieldActive || ds.DsState.State.Buffer <= 0)
                        {
                            if (Enforced.Debug == 4) Log.Line($"MP-shield inactive or no buff - Active:{shieldActive} - Buffer:{ds.DsState.State.Buffer} - Amount:{info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (hostileEnt == null)
                        {
                            if (Enforced.Debug == 4) Log.Line($"MP-shield nullAttacker - Amount:{info.Amount} - Buffer:{ds.DsState.State.Buffer}");
                            info.Amount = 0;
                            return;
                        }
                        var missile = hostileEnt.DefinitionId != null && hostileEnt.DefinitionId.Value.TypeId == MissileObj;

                        var worldSphere = ds.ShieldSphere;
                        var hostileCenter = hostileEnt.PositionComp.WorldVolume.Center;
                        var hostileTestLoc = hostileCenter;
                        var line = new LineD(hostileTestLoc, ds.SOriBBoxD.Center);
                        var obbCheck = ds.SOriBBoxD.Intersects(ref line);
                        var testDir = line.From - line.To;
                        testDir.Normalize();
                        var ray = new RayD(line.From, -testDir);
                        var sphereCheck = worldSphere.Intersects(ray);
                        var obb = obbCheck ?? 0;
                        var sphere = sphereCheck ?? 0;
                        double furthestHit;
                        if (obb <= 0 && sphere <= 0) furthestHit = 0;
                        else if (obb > sphere) furthestHit = obb;
                        else furthestHit = sphere;
                        var hitPos = line.From + testDir * -furthestHit;
                        ds.WorldImpactPosition = hitPos;

                        if (missile)
                        {
                            _ignoreAttackerId = attackerId;
                            UtilsStatic.CreateFakeSmallExplosion(hitPos);
                            if (hostileEnt.InScene && !hostileEnt.MarkedForClose)
                            {
                                hostileEnt.Close();
                                hostileEnt.InScene = false;
                                ds.ImpactSize = info.Amount;
                            }
                        }
                        else if (hostileEnt is IMyWarhead)
                        {
                            var magicValue = info.Amount;
                            var empPos = hostileEnt.PositionComp.WorldAABB.Center;
                            ds.EmpDetonation = empPos;
                            ds.EmpSize = ds.EllipsoidVolume / magicValue;
                            info.Amount = ds.ShieldMaxBuffer * Enforced.Efficiency / magicValue;
                            UtilsStatic.CreateExplosion(empPos, 2.1f, 9999);
                        }
                        else ds.ImpactSize = info.Amount;
                    }
                    ds.Absorb += info.Amount;
                    info.Amount = 0f;
                }
                else if (block != null)
                {
                    if (info.Type == DelDamage)
                    {
                        if (Enforced.Debug == 4) Log.Line($"DelDamage ignoring attacker: {info.AttackerId}");
                        _ignoreAttackerId = info.AttackerId;
                        return;
                    }

                    if (info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind) return;
                    if (info.Type == MyDamageType.Destruction)
                    {
                        Log.Line($"OddDamageType:{info.Type}");
                        return;
                    }

                    var myEntity = block.CubeGrid as MyEntity;
                    if (myEntity == null) return;
                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;
                    MyEntity hostileEnt;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);
                    var shieldHitPos = Vector3D.NegativeInfinity;
                    var insideAttacker = false;
                    try
                    {
                        if (hostileEnt != null)
                        {
                            _blockingShield = null;

                            MyCubeGrid myGrid;
                            MyEntity trueAttacker;
                            if (info.Type != MyDamageType.Environment) myGrid = hostileEnt as MyCubeGrid;
                            else myGrid = hostileEnt.Parent as MyCubeGrid;
                            if (myGrid == null)
                            {
                                var hostileCube = hostileEnt.Parent as MyCubeBlock;
                                trueAttacker = (hostileCube ?? (hostileEnt as IMyGunBaseUser)?.Owner) ?? hostileEnt;
                            }
                            else trueAttacker = myGrid;

                            _originBlock = block;
                            _originBlock.ComputeWorldCenter(out _originHit);
                            var line = new LineD(trueAttacker.PositionComp.WorldAABB.Center, _originHit);
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);

                            var hitDist = double.MaxValue;
                            var distFromTarget = double.MaxValue;
                            DefenseShields zeroDistShield = null;
                                
                            foreach (var shield in protectors.Shields)
                            {
                                var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!shieldActive) continue;
                                ProtectCache protectInfo;
                                shield.ProtectedEntCache.TryGetValue(trueAttacker, out protectInfo);
                                if (protectInfo?.PreviousRelation == DefenseShields.Ent.Protected)
                                {
                                    if (shieldHitPos == Vector3D.NegativeInfinity)
                                    {
                                        insideAttacker = true;
                                        _clientExpShield = shield;
                                    }
                                    continue;
                                }
                                var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                                var obb = obbCheck ?? 0;
                                if (obb <= 0)
                                {
                                    var tmpDistFromTarget = Vector3D.DistanceSquared(_originHit, trueAttacker.WorldMatrix.Translation);
                                    if (tmpDistFromTarget <= distFromTarget)
                                    {
                                        distFromTarget = tmpDistFromTarget;
                                        zeroDistShield = shield;
                                    }
                                }
                                else if (obb <= hitDist)
                                {
                                    hitDist = obb;
                                    _blockingShield = shield;
                                    shieldHitPos = line.From + testDir * -obb;
                                    insideAttacker = false;
                                }
                            }

                            if (insideAttacker && info.Type == MyDamageType.Explosion)
                            {
                                if (Enforced.Debug == 4) Log.Line($"Sending origin explosion MpDoDamage: {info.Type} - {info.Amount} - {_originBlock.Position}");
                                _clientExpShield.DamageBlock(_clientExpShield.Shield.SlimBlock, info.Amount, attackerId, MpDoExplosion);
                            }
                            else if (_blockingShield == null && zeroDistShield != null)
                            {
                                Log.Line($"zero shield active");
                                hitDist = 0;
                                shieldHitPos = line.From + testDir * -0;
                                _blockingShield = zeroDistShield;
                            }
                            else if (_blockingShield != null)
                            {
                                var shield = _blockingShield;
                                var worldSphere = shield.ShieldSphere;
                                var sphereCheck = worldSphere.Intersects(ray);
                                if (sphereCheck.HasValue && sphereCheck.Value > hitDist)
                                {
                                    Log.Line($"sphere shield active - {sphereCheck.Value} - {hitDist}");
                                    hitDist = sphereCheck.Value;
                                    shieldHitPos = line.From + testDir * -hitDist;
                                }
                                else Log.Line($"obb shield active");
                            }
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageGetShield: {ex}"); }
                    try
                    {
                        var activeProtector = _blockingShield != null && _blockingShield.DsState.State.Online && !_blockingShield.DsState.State.Lowered && protectors.Shields.Contains(_blockingShield);
                        if (activeProtector)
                        {
                            var shield = _blockingShield;

                            if (!IsServer && !shield.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }
                            if (hostileEnt is MyVoxelBase)
                            {
                                shield.DeformEnabled = true;
                                return;
                            }
                            if (info.Type == Bypass)
                            {
                                shield.DeformEnabled = true;
                                return;
                            }
                            if (info.Type == DSdamage || info.Type == DSheal || info.Type == DSbypass)
                            {
                                if (info.Type == DSheal)
                                {
                                    info.Amount = 0f;
                                    return;
                                }

                                if (hostileEnt is IMyGunBaseUser && block.FatBlock != null && block.FatBlock == shield.Shield) //temp fix for GSF laser bug
                                {
                                    shield.Absorb += 1000;
                                    shield.WorldImpactPosition = shield.ShieldEnt.Render.ColorMaskHsv;
                                    info.Amount = 0f;
                                    return;
                                }
                                info.Amount = 0f;
                                return;
                            }

                            var isExplosionDmg = info.Type == MyDamageType.Explosion;
                            var isDeformationDmg = info.Type == MyDamageType.Deformation;
                            var isEnvironmentDmg = info.Type == MyDamageType.Environment;
                            /*
                            if (insideAttacker)
                            {
                                if (Enforced.Debug == 4) Log.Line($"[inside attacker] Type:{info.Type} - Amount:{info.Amount} - Name:{hostileEnt.DebugName}");
                                shield.ExplosionEnabled = true;
                                shield.DeformEnabled = true;
                                if (isExplosionDmg)
                                {
                                    if (Enforced.Debug == 4) Log.Line($"Sending origin explosion MpDoDamage: {info.Type} - {info.Amount} - {_originBlock.Position}");
                                    shield.DamageBlock(shield.Shield.SlimBlock, info.Amount, attackerId, MpDoExplosion);
                                }
                                return;
                            }
                            */
                            if (!isDeformationDmg && !isExplosionDmg)
                            {
                                if (Enforced.Debug == 4 && (shield.ExplosionEnabled || shield.DeformEnabled)) Log.Line($"resetting deform/explosion permission in handler due to other damage: {info.Type} - {info.Amount}");
                                if (!IsServer) _clientExpShield = null;
                                shield.ExplosionEnabled = false;
                                shield.DeformEnabled = false;
                                _ignoreAttackerId = -1;
                            }
                            else if (!shield.DeformEnabled && hostileEnt == null)
                            {
                                if (Enforced.Debug == 4) Log.Line($"[hostileNullSupressDeform] Type:{info.Type} - Amount:{info.Amount}");
                                info.Amount = 0;
                                return;
                            }

                            var gunBase = hostileEnt as IMyGunBaseUser;
                            MyCubeGrid myGrid;
                            var hostileCube = hostileEnt?.Parent as MyCubeBlock;
                            if (isEnvironmentDmg) myGrid = hostileEnt?.Parent as MyCubeGrid;
                            else myGrid = hostileEnt as MyCubeGrid;
                            if (myGrid != null)
                            {
                                if (shield.WebEnts.ContainsKey(myGrid) && shield.WebEnts[myGrid].Relation == DefenseShields.Ent.Protected)
                                {
                                    if (Enforced.Debug == 4) Log.Line($"[gridAllow] Type:{info.Type} - Amount:{info.Amount}");
                                    shield.DeformEnabled = true;
                                    return;
                                }
                            }
                            else if (gunBase != null)
                            {
                                if (hostileEnt.Parent != null)
                                {
                                    var hostilePos = !DedicatedServer ? hostileEnt.Parent.PositionComp.WorldAABB.Center : gunBase.Owner.PositionComp.WorldAABB.Center;
                                    if (CustomCollision.PointInShield(hostilePos, shield.DetectMatrixOutsideInv))
                                    {
                                        if (Enforced.Debug == 4) Log.Line($"[hostileAllow] Type:{info.Type} - Amount:{info.Amount}");
                                        shield.DeformEnabled = true;
                                        return;
                                    }
                                }
                                else
                                {
                                    var hostilePos = !DedicatedServer ? hostileEnt.PositionComp.WorldAABB.Center : gunBase.Owner.PositionComp.WorldAABB.Center;
                                    if (CustomCollision.PointInShield(hostilePos, shield.DetectMatrixOutsideInv))
                                    {
                                        if (Enforced.Debug == 4) Log.Line($"[hostileAllow] Type:{info.Type} - Amount:{info.Amount}");
                                        shield.DeformEnabled = true;
                                        return;
                                    }
                                }
                            }
                            else if (hostileCube != null)
                            {
                                if (CustomCollision.PointInShield(hostileCube.PositionComp.WorldAABB.Center, shield.DetectMatrixOutsideInv))
                                {
                                    /*
                                    if (isExplosionDmg)
                                    {
                                        if (Enforced.Debug == 4) Log.Line($"Sending origin explosion MpDoDamage: {info.Type} - {info.Amount} - {_originBlock.Position}");
                                        shield.ExplosionEnabled = true;
                                        shield.DamageBlock(shield.Shield.SlimBlock, info.Amount, attackerId, MpDoExplosion);
                                    }
                                    */
                                    if (Enforced.Debug == 4) Log.Line($"[CubeAllow] Type:{info.Type} - Amount:{info.Amount}");
                                    shield.DeformEnabled = true;
                                    return;
                                }
                            }

                            if (shield.DeformEnabled && (isDeformationDmg || isExplosionDmg))
                            {
                                if (Enforced.Debug == 4) Log.Line($"[areaAllow] Type:{info.Type} - Amount:{info.Amount}");
                                return;
                            }

                            var bullet = info.Type == MyDamageType.Bullet;
                            if (bullet || isDeformationDmg) info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;
                            else info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;

                            if (!DedicatedServer && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                            {
                                shield.WorldImpactPosition = shieldHitPos;
                                shield.ImpactSize = info.Amount;
                            }
                            if (Enforced.Debug == 4) Log.Line($"[Shield Absorb] Type:{info.Type} - Amount:{info.Amount} - shieldId:{shield.Shield.EntityId}");
                            if (isDeformationDmg && hostileEnt != null) _ignoreAttackerId = attackerId;
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            return;
                        }
                        if (!IsServer && (info.Type == MyDamageType.Deformation || info.Type == MyDamageType.Explosion)) // hack to handle interal explosions
                        {
                            if (Enforced.Debug == 4) Log.Line($"[Server damage] Active:{_clientExpShield != null && _clientExpShield.ExplosionEnabled} - Type:{info.Type} - Amount:{info.Amount}");
                            if (_clientExpShield != null && _clientExpShield.ExplosionEnabled) return;
                            info.Amount = 0;
                        }
                        if (info.AttackerId == _ignoreAttackerId && info.Type == MyDamageType.Deformation) //hack to supress double missile double events
                        {
                            if (Enforced.Debug == 4) Log.Line($"old Del/Mp Attacker, ignoring: {info.Type} - {info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        _ignoreAttackerId = -1;
                        if (Enforced.Debug == 4) Log.Line($"[Uncaught Damage] Type:{info.Type} - Amount:{info.Amount} - nullHostileEnt:{hostileEnt == null} - nullShield:{_blockingShield == null} - attackerId:{info.AttackerId}");
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageDoDamage: {ex}"); }
                }
                else if (target is IMyCharacter)
                {
                    var myEntity = target as MyEntity;
                    if (myEntity == null) return;

                    if (info.Type == DelDamage || info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind
                        || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure) return;

                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;

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
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }
        #endregion

    }
}

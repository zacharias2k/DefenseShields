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
                var block = target as IMySlimBlock;
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
                        if (Enforced.Debug >= 3) Log.Line($"MP-shield ds is null - Amount:{info.Amount}");
                        info.Amount = 0;
                        return;
                    }

                    if (!DedicatedServer)
                    {
                        var shieldActive = ds.DsState.State.Online && !ds.DsState.State.Lowered;
                        if (!shieldActive || ds.DsState.State.Buffer <= 0)
                        {
                            if (Enforced.Debug >= 3) Log.Line($"MP-shield inactive or no buff - Active:{shieldActive} - Buffer:{ds.DsState.State.Buffer} - Amount:{info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (hostileEnt == null)
                        {
                            if (Enforced.Debug >= 3) Log.Line($"MP-shield nullAttacker - Amount:{info.Amount} - Buffer:{ds.DsState.State.Buffer}");
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
                    var myEntity = block.CubeGrid as MyEntity;
                    if (myEntity == null) return;
                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;
                    if (info.Type == MyDamageType.Destruction)
                    {
                        Log.Line($"OddDamageType:{info.Type}");
                        return;
                    }
                    if (info.Type == DelDamage || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind) return;

                    MyEntity hostileEnt;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);
                    var shieldHitPos = Vector3D.NegativeInfinity;
                    try
                    {
                        if (hostileEnt != null)
                        {
                            var hitDist = double.MaxValue;
                            Vector3D tmpBlockPos;
                            block.ComputeWorldCenter(out tmpBlockPos);
                            var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, tmpBlockPos);
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);

                            foreach (var dict in protectors.Shields)
                            {
                                var shield = dict.Key;
                                var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!IsServer && shieldActive && !shield.WarmedUp)
                                {
                                    info.Amount = 0;
                                    return;
                                }
                                if (!shieldActive) continue;

                                var worldSphere = shield.ShieldSphere;
                                var sphereCheck = worldSphere.Intersects(ray);
                                if (!sphereCheck.HasValue) continue;
                                var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                                if (!obbCheck.HasValue) continue;
                                var obb = obbCheck ?? 0;
                                var sphere = sphereCheck ?? 0;
                                double furthestHit;
                                if (obb <= 0 && sphere <= 0) furthestHit = 0;
                                else if (obb > sphere) furthestHit = obb;
                                else furthestHit = sphere;
                                var tmphitPos = line.From + testDir * -furthestHit;
                                if (furthestHit < hitDist)
                                {
                                    hitDist = furthestHit;
                                    _blockingShield = shield;
                                    shieldHitPos = tmphitPos;
                                }
                            }
                        }
                        else if (_blockingShield != null && protectors.Shields.ContainsKey(_blockingShield) && _blockingShield.DsState.State.Online && !_blockingShield.DsState.State.Lowered)
                        {
                            if (!IsServer && !_blockingShield.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }
                        }
                        else
                        {
                            var foundBackupShield = false;
                            foreach (var dict in protectors.Shields)
                            {
                                var shield = dict.Key;
                                var shieldActive2 = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!IsServer && shieldActive2 && !shield.WarmedUp)
                                {
                                    info.Amount = 0;
                                    return;
                                }

                                if (!shieldActive2)
                                {
                                    if (Enforced.Debug >= 4) Log.Line($"cannot find active backup shield: Online:{shield.DsState.State.Online} - Lowered:{shield.DsState.State.Lowered} - ShieldCnt:{protectors.Shields.Keys.Count} - AttackerId:{info.AttackerId} - Asleep:{shield.Asleep} - active:{ActiveShields.Contains(shield)}");
                                    continue;
                                }

                                _blockingShield = shield;
                                foundBackupShield = true;
                                if (Enforced.Debug >= 4) Log.Line($"found backup shield");
                                break;
                            }

                            if (!foundBackupShield)
                            {
                                if (Enforced.Debug >= 3) Log.Line($"did not find backup shield: ShieldCnt:{protectors.Shields.Keys.Count} - DamageType:{info.Type} - AttackerId:{info.AttackerId}");
                                _blockingShield = null;
                                info.Amount = 0;
                                return;
                            }
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageGetShield: {ex}"); }

                    try
                    {
                        if (_blockingShield != null)
                        {
                            var shield = _blockingShield;
                            if (!info.IsDeformation) shield.DeformEnabled = false;
                            else if (!shield.DeformEnabled && hostileEnt == null)
                            {
                                info.Amount = 0;
                                return;
                            }
                            if (Enforced.Debug >= 4 && info.IsDeformation) Log.Line($"deform: {shield.DeformEnabled}");
                            if (info.Type == Bypass)
                            {
                                shield.DeformEnabled = true;
                                return;
                            }

                            if (hostileEnt is MyVoxelBase || hostileEnt != null)
                            {
                                ProtectCache protectedEnt = null;
                                shield.ProtectedEntCache?.TryGetValue(hostileEnt, out protectedEnt);
                                if (protectedEnt != null && protectedEnt.Relation == DefenseShields.Ent.Protected)
                                {
                                    shield.DeformEnabled = true;
                                    return;
                                }
                            }

                            var gunBase = hostileEnt as IMyGunBaseUser;

                            if (info.Type == DSdamage || info.Type == DSheal || info.Type == DSbypass)
                            {
                                if (info.Type == DSheal)
                                {
                                    info.Amount = 0f;
                                    return;
                                }

                                if (gunBase != null && block.FatBlock != null && block.FatBlock == shield.Shield) //temp fix for GSF laser bug
                                {
                                    shield.Absorb += 1000;
                                    shield.WorldImpactPosition = shield.ShieldEnt.Render.ColorMaskHsv;
                                    info.Amount = 0f;
                                    return;
                                }
                                info.Amount = 0f;
                                return;
                            }

                            if (gunBase != null)
                            {
                                var hostileParent = hostileEnt.Parent != null;
                                if (hostileParent && CustomCollision.PointInShield(hostileEnt.Parent.PositionComp.WorldAABB.Center, shield.DetectMatrixOutsideInv))
                                {
                                    shield.DeformEnabled = true;
                                    return;
                                }
                                var hostilePos = hostileEnt.PositionComp.WorldAABB.Center;

                                if (hostilePos == Vector3D.Zero && gunBase.Owner != null) hostilePos = gunBase.Owner.PositionComp.WorldAABB.Center;
                                if (!hostileParent && CustomCollision.PointInShield(hostilePos, shield.DetectMatrixOutsideInv))
                                {
                                    shield.DeformEnabled = true;
                                    return;
                                }
                            }

                            if (info.IsDeformation && shield.DeformEnabled) return;
                            var bullet = info.Type == MyDamageType.Bullet;
                            var deform = info.Type == MyDamageType.Deformation;
                            if (bullet || deform) info.Amount = info.Amount * shield.DsState.State.ModulateEnergy;
                            else info.Amount = info.Amount * shield.DsState.State.ModulateKinetic;

                            if (!DedicatedServer && shield.Absorb < 1 && shield.WorldImpactPosition == Vector3D.NegativeInfinity && shield.BulletCoolDown == -1)
                            {
                                shield.WorldImpactPosition = shieldHitPos;
                                shield.ImpactSize = info.Amount;
                            }

                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                        }
                        else if (hostileEnt != null && protectors.Shields != null && block != null) Log.Line($"No shield match, should never happen - attacker:{hostileEnt.DebugName} - protectors:{protectors.Shields.Keys.Count} - victim:{((MyCubeGrid)block.CubeGrid).DebugName} - Type:{info.Type}");
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageDoDamage {_blockingShield == null} - {hostileEnt == null} - {info.Type} - {block == null}: {ex}"); }
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

                    foreach (var dict in protectors.Shields)
                    {
                        if (!dict.Value.GridIsParent) continue;
                        var shield = dict.Key;

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

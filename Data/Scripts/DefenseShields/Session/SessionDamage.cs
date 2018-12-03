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
                    var ds = target as DefenseShields;
                    if (ds == null)
                    {
                        info.Amount = 0;
                        return;
                    }

                    if (!DedicatedServer)
                    {
                        var shieldActive = ds.DsState.State.Online && !ds.DsState.State.Lowered;
                        if (!shieldActive || ds.DsState.State.Buffer <= 0)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield inactive or no buff - Active:{shieldActive} - Buffer:{ds.DsState.State.Buffer} - Amount:{info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (hostileEnt == null)
                        {
                            if (Enforced.Debug == 1) Log.Line($"MP-shield nullAttacker - Amount:{info.Amount} - Buffer:{ds.DsState.State.Buffer}");
                            info.Amount = 0;
                            return;
                        }
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
                        var warHead = hostileEnt as IMyWarhead;
                        if (warHead != null)
                        {
                            var magicValue = info.Amount;
                            var empPos = warHead.PositionComp.WorldAABB.Center;
                            ds.EmpDetonation = empPos;
                            ds.EmpSize = ds.EllipsoidVolume / magicValue;
                            info.Amount = ds.ShieldMaxBuffer * Enforced.Efficiency / magicValue;
                            UtilsStatic.CreateExplosion(empPos, 2.1f, 9999);
                        }
                        else ds.ImpactSize = info.Amount;

                        if (hostileEnt.DefinitionId.HasValue && hostileEnt.DefinitionId.Value.TypeId == MissileObj)
                        {
                            UtilsStatic.CreateFakeSmallExplosion(hitPos);
                            if (hostileEnt.InScene && !hostileEnt.MarkedForClose)
                            {
                                hostileEnt.Close();
                                hostileEnt.InScene = false;
                            }
                        }
                    }
                    ds.Absorb += info.Amount;
                    info.Amount = 0f;
                }
                else if (block != null)
                {
                    var myEntity = block.CubeGrid as MyEntity;
                    if (myEntity == null) return;
                    MyProtectors protectors;
                    GlobalProtectDict.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;
                    if (info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure)
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
                    if (hostileEnt != null)
                    {
                        var shieldCnt = protectors.Shields.Count;
                        var hitDist = -1d;
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

                            var enclosed = dict.Value.FullCoverage;

                            Vector3D tmpBlockPos;
                            block.ComputeWorldCenter(out tmpBlockPos);
                            if (enclosed && shieldCnt == 1)
                            {
                                _blockingShield = shield;
                            }

                            if (!enclosed && Vector3D.Transform(tmpBlockPos, shield.DetectMatrixOutsideInv).LengthSquared() > 1)
                            {
                                //Log.Line($"no block hit: enclosed:{enclosed} - gridIsParent:{gridIsParent}");
                                continue;
                            }

                            var line = new LineD(hostileEnt.PositionComp.WorldAABB.Center, tmpBlockPos);
                            var testDir = Vector3D.Normalize(line.From - line.To);
                            var ray = new RayD(line.From, -testDir);
                            var worldSphere = shield.ShieldSphere;
                            var sphereCheck = worldSphere.Intersects(ray);
                            if (!sphereCheck.HasValue) continue;
                            var obbCheck = shield.SOriBBoxD.Intersects(ref line);
                            var obb = obbCheck ?? 0;
                            var sphere = sphereCheck ?? 0;
                            double furthestHit;
                            if (obb <= 0 && sphere <= 0) furthestHit = 0;
                            else if (obb > sphere) furthestHit = obb;
                            else furthestHit = sphere;
                            var tmphitPos = line.From + testDir * -furthestHit;

                            if (furthestHit > hitDist)
                            {
                                //Log.Line($"shield closer to attacker - dist: {furthestHit} - prevDist: {hitDist} - ShieldId:{shield.MyCube.EntityId}");
                                hitDist = furthestHit;
                                _blockingShield = shield;
                                shieldHitPos = tmphitPos;
                            }
                        }
                    }
                    else
                    {
                        var shieldActive = _blockingShield.DsState.State.Online && !_blockingShield.DsState.State.Lowered;
                        if (!IsServer && shieldActive && !_blockingShield.WarmedUp)
                        {
                            info.Amount = 0;
                            return;
                        }
                        if (!shieldActive || !protectors.Shields.ContainsKey(_blockingShield))
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

                                if (!shieldActive2) continue;
                                _blockingShield = shield;
                                foundBackupShield = true;
                                Log.Line($"found backup shield");
                                break;
                            }

                            if (!foundBackupShield)
                            {
                                Log.Line($"did not find backup shield");
                                _blockingShield = null;
                                return;
                            }
                        }
                    }

                    if (_blockingShield != null)
                    {
                        var shield = _blockingShield;
                        if (!info.IsDeformation) shield.DeformEnabled = false;
                        else if (!shield.DeformEnabled && hostileEnt == null)
                        {
                            info.Amount = 0;
                            return;
                        }

                        if (info.Type == Bypass)
                        {
                            shield.DeformEnabled = true;
                            return;
                        }

                        if (hostileEnt is MyVoxelBase || hostileEnt != null && shield.FriendlyCache.Contains(hostileEnt))
                        {
                            shield.DeformEnabled = true;
                            return;
                        }

                        var gunBase = hostileEnt as IMyGunBaseUser;

                        if (info.Type == DSdamage || info.Type == DSheal || info.Type == DSbypass)
                        {
                            if (info.Type == DSheal)
                            {
                                info.Amount = 0f;
                                return;
                            }

                            if (gunBase != null && block.FatBlock == shield.Shield) //temp fix for GSF laser bug
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
                            if (hostileParent && CustomCollision.PointInShield(hostileEnt.Parent.PositionComp.WorldVolume.Center, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
                                return;
                            }
                            var hostilePos = hostileEnt.PositionComp.WorldMatrix.Translation;

                            if (hostilePos == Vector3D.Zero && gunBase.Owner != null) hostilePos = gunBase.Owner.PositionComp.WorldMatrix.Translation;
                            if (!hostileParent && CustomCollision.PointInShield(hostilePos, shield.DetectMatrixOutsideInv))
                            {
                                shield.DeformEnabled = true;
                                shield.FriendlyCache.Add(hostileEnt);
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
                }
                else if (target is IMyCharacter)
                {
                    var myEntity = target as MyEntity;
                    if (myEntity == null) return;

                    if (info.Type == DelDamage || info.Type == MyDamageType.Destruction || info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind
                        || info.Type == MyDamageType.Environment || info.Type == MyDamageType.LowPressure) return;

                    MyProtectors protectors;
                    GlobalProtectDict.TryGetValue(myEntity, out protectors);
                    if (protectors.Shields == null) return;

                    foreach (var dict in protectors.Shields)
                    {
                        if (!dict.Value.GridIsParent) continue;
                        var shield = dict.Key;

                        var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;

                        MyEntity hostileEnt;
                        var attackerId = info.AttackerId;
                        if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                        else UpdatedHostileEnt(attackerId, out hostileEnt);

                        if (shieldActive && shield.FriendlyCache.Contains(myEntity) && hostileEnt == null || hostileEnt != null && !shield.FriendlyCache.Contains(hostileEnt))
                        {
                            info.Amount = 0f;
                            myEntity.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
                        }
                    }
                }

            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }
        #endregion

    }
}

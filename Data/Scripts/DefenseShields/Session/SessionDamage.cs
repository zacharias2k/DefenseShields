namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using global::DefenseShields.Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
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
                if (info.Type == MpDoExplosion && block != null) 
                {
                    if (IsServer)
                    {
                        info.Amount = 0;
                        return;
                    }

                    var ds = block.FatBlock?.GameLogic as DefenseShields;

                    if (ds == null)
                    {
                        if (Enforced.Debug == 4) Log.Line($"[MpDoDamage] clientExpShield was Null, not doing damage");
                        info.Amount = 0;
                        return;
                    }
                    MyEntity protectedGrid;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) protectedGrid = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out protectedGrid);
                    if (protectedGrid == null)
                    {
                        if (Enforced.Debug == 4) Log.Line($"[MpDoDamage] protectedGrid was Null, not doing damage");
                        info.Amount = 0;
                        return;
                    }
                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(protectedGrid, out protectors);
                    if (protectors != null) protectors.ClientExpShield = ds;
                    info.Amount = 0;
                    if (Enforced.Debug == 4) Log.Line($"[MpDoDamage] Type:{info.Type} - Amount:{info.Amount} - AttackerId:{info.AttackerId}");
                }

                if (info.Type == MPdamage && block != null)
                {   
                    var ds = block.FatBlock?.GameLogic as DefenseShields;
                    if (ds == null)
                    {
                        if (Enforced.Debug == 4) Log.Line($"MP-shield block is null - Amount:{info.Amount}");
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

                        var hostileCenter = hostileEnt.PositionComp.WorldVolume.Center;
                        var line = new LineD(hostileCenter, ds.SOriBBoxD.Center);
                        var testDir = Vector3D.Normalize(line.From - line.To);
                        var ray = new RayD(line.From, -testDir);
                        var ellipsoid = CustomCollision.IntersectEllipsoid(ds.DetectMatrixOutsideInv, ds.DetectionMatrix, ray) ?? 0;
                        var hitPos = line.From + (testDir * -ellipsoid);
                        ds.WorldImpactPosition = hitPos;

                        if (missile)
                        {
                            MyProtectors protectors;
                            GlobalProtect.TryGetValue(ds.MyGrid, out protectors);
                            if (protectors != null) protectors.IgnoreAttackerId = attackerId;
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
                    if (info.Type == MyDamageType.Drill || info.Type == MyDamageType.Grind) return;

                    var myEntity = block.CubeGrid as MyEntity;
                    if (myEntity == null) return;

                    MyProtectors protectors;
                    GlobalProtect.TryGetValue(myEntity, out protectors);
                    if (protectors == null) return;

                    if (info.Type == DelDamage)
                    {
                        protectors.IgnoreAttackerId = info.AttackerId;
                        return;
                    }

                    MyEntity hostileEnt;
                    var attackerId = info.AttackerId;
                    if (attackerId == _previousEntId) hostileEnt = _previousEnt;
                    else UpdatedHostileEnt(attackerId, out hostileEnt);

                    var shieldHitPos = Vector3D.NegativeInfinity;
                    MyEntity trueAttacker = null;
                    try
                    {
                        if (hostileEnt != null)
                        {
                            var blockingShield = false;
                            var clientShield = false;
                            protectors.BlockingShield = null;
                            protectors.ClientExpShield = null;

                            MyCubeGrid myGrid;
                            if (info.Type != MyDamageType.Environment) myGrid = hostileEnt as MyCubeGrid;
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
                            float? intersectDist = null;    
                            foreach (var shield in protectors.Shields)
                            {
                                var shieldActive = shield.DsState.State.Online && !shield.DsState.State.Lowered;
                                if (!shieldActive) continue;
                                intersectDist = CustomCollision.IntersectEllipsoid(shield.DetectMatrixOutsideInv, shield.DetectionMatrix, ray);
                                var ellipsoid = intersectDist ?? 0;
                                var intersect = ellipsoid > 0 && line.Length > ellipsoid;
                                var noIntersect = ellipsoid <= 0 || line.Length < ellipsoid;
                                if (intersect && ellipsoid <= hitDist)
                                {
                                    hitDist = ellipsoid;
                                    shieldHitPos = line.From + (testDir * -ellipsoid);
                                    protectors.BlockingShield = shield;
                                    clientShield = false;
                                    blockingShield = true;
                                }
                                else if (noIntersect && protectors.BlockingShield == null)
                                {
                                    protectors.ClientExpShield = shield;
                                    clientShield = true;
                                    blockingShield = false;
                                }
                            }
                            if (!blockingShield) protectors.BlockingShield = null;
                            if (!clientShield) protectors.ClientExpShield = null;

                            if (Enforced.Debug == 4)
                            {
                                if (protectors.BlockingShield != null)
                                {
                                    var distanceReprot = intersectDist?.ToString() ?? "null";
                                    if (Enforced.Debug == 4) Log.Line($"blockingshield found: trueAttacker:{trueAttacker.DebugName} - hostileEnt:{hostileEnt.DebugName} - eDist:{distanceReprot} - lDist:{line.Length} - clientShield:{protectors.ClientExpShield != null}");
                                }
                                else 
                                {
                                    var distanceReprot = intersectDist?.ToString() ?? "null";
                                    if (Enforced.Debug == 4) Log.Line($"no blockingshield found: trueAttacker:{trueAttacker.DebugName} - hostileEnt:{hostileEnt.DebugName} - eDist:{distanceReprot} - lDist:{line.Length} - clientShield:{protectors.ClientExpShield != null}");
                                }
                            }

                            if (protectors.ClientExpShield != null && info.Type == MyDamageType.Explosion)
                            {
                                if (Enforced.Debug == 4) Log.Line($"Sending origin explosion MpDoDamage: {info.Type} - {info.Amount} - {protectors.OriginBlock.Position}");
                                protectors.ClientExpShield.DamageBlock(protectors.ClientExpShield.Shield.SlimBlock, info.Amount, block.CubeGrid.EntityId, MpDoExplosion);
                            }
                        }
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageGetShield: {ex}"); }
                    try
                    {
                        var activeProtector = protectors.BlockingShield != null && protectors.BlockingShield.DsState.State.Online && !protectors.BlockingShield.DsState.State.Lowered && protectors.Shields.Contains(protectors.BlockingShield);
                        if (activeProtector)
                        {
                            var shield = protectors.BlockingShield;

                            if (!IsServer && !shield.WarmedUp)
                            {
                                info.Amount = 0;
                                return;
                            }
                            var isExplosionDmg = info.Type == MyDamageType.Explosion;
                            var isDeformationDmg = info.Type == MyDamageType.Deformation;
                            if (trueAttacker is MyVoxelBase || (trueAttacker is MyCubeGrid && isDeformationDmg && shield.ModulateGrids))
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
                                info.Amount = 0f;
                                return;
                            }

                            if (!isDeformationDmg && !isExplosionDmg)
                            {
                                if (!IsServer) protectors.ClientExpShield = null;
                                shield.ExplosionEnabled = false;
                                shield.DeformEnabled = false;
                                protectors.IgnoreAttackerId = -1;
                            }
                            else if (!shield.DeformEnabled && trueAttacker == null)
                            {
                                info.Amount = 0;
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
                            if (isDeformationDmg && trueAttacker != null) protectors.IgnoreAttackerId = attackerId;
                            shield.Absorb += info.Amount;
                            info.Amount = 0f;
                            if (Enforced.Debug == 4) Log.Line($"[Shield Absorb] Type:{info.Type} - Amount:{info.Amount} - shieldId:{shield.Shield.EntityId}");
                            return;
                        }

                        var iShield = protectors.IntegrityShield;
                        if (iShield != null && iShield.DsState.State.Online && !iShield.DsState.State.Lowered)
                        {
                            var attackingVoxel = trueAttacker as MyVoxelBase;
                            if (attackingVoxel != null || trueAttacker is MyCubeGrid) iShield.DeformEnabled = true;
                            else if (trueAttacker != null) iShield.DeformEnabled = false;

                            if (info.Type == MyDamageType.Deformation && iShield.DeformEnabled)
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
                                    iShield.AddShieldHit(attackerId, dmgAmount, info.Type, block);
                                    iShield.Absorb += dmgAmount;
                                }
                                info.Amount = 0;
                                return;
                            }
                        } 
                        if (!IsServer && (info.Type == MyDamageType.Deformation || info.Type == MyDamageType.Explosion)) 
                        {
                            if (Enforced.Debug == 4) Log.Line($"[Server damage] Active:{protectors.ClientExpShield != null} - Type:{info.Type} - Amount:{info.Amount}");
                            if (protectors.ClientExpShield != null) return;
                            info.Amount = 0;
                        }
                        if (info.AttackerId == protectors.IgnoreAttackerId && info.Type == MyDamageType.Deformation) 
                        {
                            if (Enforced.Debug == 4) Log.Line($"old Del/Mp Attacker, ignoring: {info.Type} - {info.Amount}");
                            info.Amount = 0;
                            return;
                        }
                        protectors.IgnoreAttackerId = -1;
                        if (Enforced.Debug == 4) Log.Line($"[Uncaught Damage] Type:{info.Type} - Amount:{info.Amount} - nullHostileEnt:{trueAttacker == null} - nullShield:{protectors.BlockingShield == null} - iShell:{protectors.IntegrityShield != null} - protectorShields:{protectors.Shields.Count} - attackerId:{info.AttackerId}");
                    }
                    catch (Exception ex) { Log.Line($"Exception in SessionDamageDoDamage: {ex}"); }
                }
                else if (target is IMyCharacter)
                {
                    var myEntity = target as MyEntity;
                    if (myEntity == null) return;

                    if (info.Type == DelDamage || info.Type == MpDoExplosion || info.Type == MPdamage) return;

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
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDamageHandler: {ex}"); }
        }
        #endregion

    }
}

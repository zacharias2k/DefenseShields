using VRage.Game.Components;

namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.Game.Entities.Character.Components;
    using Sandbox.ModAPI;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class DefenseShields
    {
        #region Intersect
        internal void EntIntersectSelector(KeyValuePair<MyEntity, EntIntersectInfo> pair)
        {
            var entInfo = pair.Value;
            var webent = pair.Key;

            if (entInfo == null || webent == null || webent.MarkedForClose) return;
            var relation = entInfo.Relation;

            var tick = Session.Instance.Tick;
            var tick25 = tick % 25 == 0;
            var entCenter = webent.PositionComp.WorldVolume.Center;
            
            if (entInfo.LastTick != tick) return;
            if (entInfo.RefreshNow && (relation == Ent.NobodyGrid || relation == Ent.EnemyGrid))
            {
                entInfo.CacheBlockList.Clear();
                (webent as IMyCubeGrid)?.GetBlocks(null, block =>
                {
                    entInfo.CacheBlockList.Add(new CubeAccel(block));
                    return false;
                });
            }
            entInfo.RefreshNow = false;

            switch (relation)
            {
                case Ent.EnemyPlayer:
                    {
                        if (tick25 && CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                        {
                            if (Session.Enforced.Debug == 3) Log.Line($"Ent EnemyPlayer: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            PlayerIntersect(webent);
                        }
                        return;
                    }
                case Ent.NobodyGrid:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent NobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        GridIntersect(webent);
                        return;
                    }
                case Ent.EnemyGrid:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent LargeEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        GridIntersect(webent);
                        return;
                    }
                case Ent.Shielded:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent Shielded: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        ShieldIntersect(webent);
                        return;
                    }
                case Ent.Floater:
                    {
                        if (!_isServer || webent.MarkedForClose) return;
                        if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                        {
                            Session.Instance.ThreadEvents.Enqueue(new FloaterThreadEvent(webent, this));
                        }
                        return;
                    }
                case Ent.Other:
                    {
                        if (!_isServer) return;
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent Other: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        if (webent.MarkedForClose || !webent.InScene) return;
                        var meteor = webent as IMyMeteor;
                        if (meteor != null)
                        {
                            if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv)) Session.Instance.ThreadEvents.Enqueue(new MeteorDmgThreadEvent(meteor, this));
                        }
                        else
                        {
                            var predictedHit = CustomCollision.FutureIntersect(this, webent, DetectionMatrix, DetectMatrixOutsideInv);
                            if (predictedHit != null) Session.Instance.ThreadEvents.Enqueue(new MissileThreadEvent(webent, this));
                        }
                        return;
                    }

                default:
                    if (Session.Enforced.Debug == 3) Log.Line($"Ent default: {webent.DebugName} - relation:{entInfo.Relation} - ShieldId [{Shield.EntityId}]");
                    return;
            }
        }

        private bool EntInside(MyEntity entity, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (entity != null && CustomCollision.PointInShield(entity.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
            {
                if (CustomCollision.ObbCornersInShield(bOriBBoxD, DetectMatrixOutsideInv, _obbCorners))
                {
                    var bPhysics = entity.Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sLSpeed = sPhysics.LinearVelocity;
                    var sASpeed = sPhysics.AngularVelocity * 50;
                    var sLSpeedLen = sLSpeed.LengthSquared();
                    var sASpeedLen = sASpeed.LengthSquared();
                    var sSpeedLen = sLSpeedLen > sASpeedLen ? sLSpeedLen : sASpeedLen;
                    var forceData = new MyForceData { Entity = entity, Force = -(entity.PositionComp.WorldAABB.Center - sPhysics.CenterOfMassWorld) * -int.MaxValue, MaxSpeed = sSpeedLen + 3 };
                    if (!bPhysics.IsStatic) Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(forceData, this));
                    return true;
                }
            }
            return false;
        }

        private void GridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (grid == null) return;

            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB);
            if (entInfo.Relation != Ent.EnemyGrid && EntInside(grid, bOriBBoxD)) return;
            BlockIntersect(grid, bOriBBoxD, ref entInfo);

            if (!_isServer) return;

            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            entInfo.EmpDetonation = Vector3D.NegativeInfinity;
            entInfo.Damage = 0;
            entInfo.EmpSize = 0;
            if (contactpoint == Vector3D.NegativeInfinity) return;
            entInfo.Touched = true;
        }

        private void ShieldIntersect(MyEntity ent)
        {
            var grid = ent as MyCubeGrid;
            if (grid == null) return;

            if (EntInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB))) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);
            if (shieldComponent?.DefenseShields == null) return;

            var ds = shieldComponent.DefenseShields;
            if (!ds.WasOnline)
            {
                EntIntersectInfo entInfo;
                WebEnts.TryRemove(ent, out entInfo);
            }
            var dsVerts = ds.ShieldComp.PhysicsOutside;
            var dsMatrixInv = ds.DetectMatrixOutsideInv;
            var myGrid = Shield.CubeGrid;

            var insidePoints = new List<Vector3D>();
            if (_isServer) CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, ShieldComp.PhysicsOutside, DetectMatrixOutsideInv, insidePoints);
            else CustomCollision.ClientShieldX2PointsInside(dsVerts, dsMatrixInv, ShieldComp.PhysicsOutsideLow, DetectMatrixOutsideInv, insidePoints);

            var bPhysics = ((IMyCubeGrid)grid).Physics;
            var sPhysics = myGrid.Physics;

            var bMass = grid.GetCurrentMass();
            var sMass = ((MyCubeGrid)myGrid).GetCurrentMass();

            if (bMass <= 0) bMass = int.MaxValue;
            if (sMass <= 0) sMass = int.MaxValue;

            var bVel = bPhysics.LinearVelocity;
            var bVelLen = bVel.Length();
            var momentum = (bMass * bVel) + (sMass * sPhysics.LinearVelocity);
            var resultVelocity = momentum / (bMass + sMass);

            var collisionAvg = Vector3D.Zero;
            var numOfPointsInside = insidePoints.Count;
            for (int i = 0; i < numOfPointsInside; i++) collisionAvg += insidePoints[i];

            collisionAvg /= numOfPointsInside;

            if (numOfPointsInside > 0 && !bPhysics.IsStatic)
            {
                var ejectorAccel = numOfPointsInside > 10 ? numOfPointsInside : 10;
                var impulseData = new MyImpulseData { Entity = ent, Direction = (resultVelocity - bVel) * bMass, Position = bPhysics.CenterOfMassWorld };
                var forceData = new MyForceData { Entity = ent, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * bMass * ejectorAccel, MaxSpeed = MathHelper.Clamp(bVelLen, 1f, 50f) };
                Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(impulseData, this));
                Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(forceData, this));
            }
            if (!_isServer || numOfPointsInside <= 0) return;

            var shieldMaxChargeRate = ds._shieldMaxChargeRate;
            var damage = ((shieldMaxChargeRate * ConvToHp) * DsState.State.ModulateKinetic) * 0.01666666666f;
            if (_mpActive)
            {
                if (_isServer) AddShieldHit(ds.Shield.EntityId, damage, Session.Instance.MPEnergy, null, false, collisionAvg);
            }
            else
            {
                EnergyHit = true;
                WorldImpactPosition = collisionAvg;

                ds.EnergyHit = true;
                ds.WorldImpactPosition = collisionAvg;

                Absorb += damage;
                ImpactSize = damage;
                WebDamage = true;
            }
        }

        internal void VoxelIntersect()
        {
            foreach (var item in VoxelsToIntersect)
            {
                var voxelBase = item.Key;
                var newVoxel = item.Value == 1;
                var stage1Check = false;

                if (item.Value > 1) stage1Check = true;
                else if (newVoxel)
                {
                    var aabb = (BoundingBox)ShieldEnt.PositionComp.WorldAABB;
                    aabb.Translate(-voxelBase.RootVoxel.PositionLeftBottomCorner);
                    if (voxelBase.RootVoxel.Storage.Intersect(ref aabb, false) != ContainmentType.Disjoint) stage1Check = true;
                }

                if (!stage1Check)
                {
                    int oldValue;
                    VoxelsToIntersect.TryRemove(voxelBase, out oldValue);
                    continue;
                }

                var collision = CustomCollision.VoxelEllipsoidCheck(MyGrid, ShieldComp.PhysicsOutsideLow, voxelBase);
                if (collision.HasValue)
                {
                    VoxelsToIntersect[voxelBase]++;
                    if (_isServer)
                    {
                        var mass = MyGrid.GetCurrentMass();
                        var sPhysics = Shield.CubeGrid.Physics;
                        var momentum = mass * sPhysics.LinearVelocity;
                        Absorb += (momentum.Length() / 500) * DsState.State.ModulateEnergy;
                    }
                    ImpactSize = 12000;
                    WorldImpactPosition = collision.Value;
                    WebDamage = true;
                }
                else VoxelsToIntersect[voxelBase] = 0;
            }
        }

        private void PlayerIntersect(MyEntity ent)
        {
            var character = ent as IMyCharacter;
            if (character == null) return;

            var npcname = character.ToString();
            if (npcname.Equals(SpaceWolf))
            {
                if (_isServer) Session.Instance.ThreadEvents.Enqueue(new CharacterEffectThreadEvent(character, this));
                return;
            }

            var player = MyAPIGateway.Multiplayer.Players.GetPlayerControllingEntity(ent);
            if (player == null || player.PromoteLevel == MyPromoteLevel.Owner || player.PromoteLevel == MyPromoteLevel.Admin) return;

            if (!_isServer)
            {
                if (character.EnabledDamping) character.SwitchDamping();
                return;
            }

            if (character.EnabledDamping) character.SwitchDamping();
            if (!character.EnabledThrusts) return;

            var playerInfo = WebEnts[ent];
            var insideTime = (int)playerInfo.LastTick - (int)playerInfo.FirstTick;
            if (insideTime < 3000) return;
            EntIntersectInfo playerRemoved;
            WebEnts.TryRemove(ent, out playerRemoved);

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;
            Session.Instance.ThreadEvents.Enqueue(new CharacterEffectThreadEvent(character, this));
        }

        private void BlockIntersect(MyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, ref EntIntersectInfo entInfo)
        {
            try
            {
                if (entInfo == null || breaching == null || breaching.MarkedForClose) return;

                if (bOriBBoxD.Intersects(ref SOriBBoxD))
                {
                    var collisionAvg = Vector3D.Zero;
                    var damageBlocks = Session.Enforced.DisableBlockDamage == 0;
                    var bQuaternion = Quaternion.CreateFromRotationMatrix(breaching.WorldMatrix);

                    var blockDmgNum = 50;
                    if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) blockDmgNum = 250;

                    Vector3D bBlockCenter;
                    var rawDamage = 0f;
                    var blockSize = breaching.GridSize;
                    var scaledBlockSize = blockSize * 3;
                    var gc = breaching.WorldToGridInteger(DetectionCenter);
                    var rc = ShieldSize.AbsMax() / blockSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var hits = 0;
                    var blockPoints = new Vector3D[9];

                    var cloneCacheList= new List<CubeAccel>(entInfo.CacheBlockList);
                    var cubeHitSet = new HashSet<CubeAccel>();

                    for (int i = 0; i < cloneCacheList.Count; i++)
                    {
                        var accel = cloneCacheList[i];
                        var blockPos = accel.BlockPos;
                        var num1 = gc.X - blockPos.X;
                        var num2 = gc.Y - blockPos.Y;
                        var num3 = gc.Z - blockPos.Z;
                        var result = (num1 * num1) + (num2 * num2) + (num3 * num3);

                        if (_isServer)
                        {
                            if (result > rc || accel.CubeExists && result > rc + scaledBlockSize) continue;
                            if (accel.Block == null || accel.Block.CubeGrid != breaching) continue;
                        }
                        else
                        {
                            if (hits > blockDmgNum) break;
                            if (result > rc || accel.CubeExists && result > rc + scaledBlockSize || accel.Block == null || accel.Block.CubeGrid != breaching || accel.Block.IsDestroyed) continue;
                        }

                        var block = accel.Block;
                        var point = CustomCollision.BlockIntersect(block, accel.CubeExists, bQuaternion, DetectMatrixOutside, DetectMatrixOutsideInv, ref blockPoints);
                        if (point == null) continue;
                        collisionAvg += (Vector3D)point;
                        hits++;
                        if (!_isServer) continue;

                        if (hits > blockDmgNum) break;

                        rawDamage += MathHelper.Clamp(block.Integrity, 0, 350);
                        if (damageBlocks)
                        {
                            cubeHitSet.Add(accel);
                        }
                    }

                    if (collisionAvg != Vector3D.Zero)
                    {
                        collisionAvg /= hits;
                        if (Session.Instance.Tick != entInfo.LastCollision && !entInfo.ActiveCollision)
                        {
                            entInfo.ActiveCollision = true;
                            ComputeGridCollisionPhysics(breaching, collisionAvg);
                        }

                        WebDamage = true;
                        bBlockCenter = collisionAvg;
                    }
                    else return;
                    if (!_isServer) return;

                    Session.Instance.ThreadEvents.Enqueue(new ManyBlocksThreadEvent(cubeHitSet, this));

                    var damage = rawDamage * DsState.State.ModulateEnergy;

                    entInfo.Damage = damage;
                    if (_mpActive)
                    {
                        if (_isServer && bBlockCenter != Vector3D.NegativeInfinity) AddShieldHit(breaching.EntityId, damage, Session.Instance.MPKinetic, null, true, collisionAvg);
                    }
                    else
                    {
                        if (bBlockCenter != Vector3D.NegativeInfinity)
                        {
                            entInfo.ContactPoint = bBlockCenter;
                            ImpactSize = entInfo.Damage;
                            entInfo.Damage = 0;
                            WorldImpactPosition = bBlockCenter;
                        }
                    }
                    Absorb += damage;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}"); }
        }

        private void ComputeGridCollisionPhysics(MyCubeGrid breaching, Vector3D collisionAvg)
        {
            var sGrid = (MyCubeGrid)Shield.CubeGrid;
            var bPhysics = ((IMyCubeGrid)breaching).Physics;
            var sPhysics = Shield.CubeGrid.Physics;
            var breachingIsStatic = bPhysics.IsStatic;
            var shieldIsStatic = sPhysics.IsStatic;

            float bMass;
            if (breachingIsStatic) bMass = float.MaxValue * 0.001f;
            else bMass = breaching.GetCurrentMass();

            float sMass;
            if (shieldIsStatic) sMass = float.MaxValue * 0.001f;
            else sMass = sGrid.GetCurrentMass();

            var bCom = bPhysics.CenterOfMassWorld;
            var bMassRelation = bMass / sMass;
            var bRelationClamp = MathHelper.Clamp(bMassRelation, 0, 1);
            var bCollisionCorrection = Vector3D.Lerp(bCom, collisionAvg, bRelationClamp);
            var bVelAtPoint = bPhysics.GetVelocityAtPoint(bCollisionCorrection);

            var sCom = shieldIsStatic ? DetectionCenter : sPhysics.CenterOfMassWorld;
            var sMassRelation = sMass / bMass;
            var sRelationClamp = MathHelper.Clamp(sMassRelation, 0, 1);
            var sCollisionCorrection = Vector3D.Lerp(sCom, collisionAvg, sRelationClamp);
            var sVelAtPoint = sPhysics.GetVelocityAtPoint(sCollisionCorrection);

            var momentum = (bMass * bVelAtPoint) + (sMass * sVelAtPoint);
            var resultVelocity = momentum / (bMass + sMass);

            var bDir = (resultVelocity - bVelAtPoint) * bMass;
            var bForce = Vector3D.Normalize(bCom - collisionAvg);

            var sDir = (resultVelocity - sVelAtPoint) * sMass;
            var sforce = Vector3D.Normalize(sCom - collisionAvg);

            var collisionData = new MyCollisionPhysicsData
            {
                Entity1 = breaching,
                Entity2 = MyGrid,
                E1IsStatic = breachingIsStatic,
                E2IsStatic = shieldIsStatic,
                E1IsHeavier = breachingIsStatic || bMass > sMass,
                E2IsHeavier = shieldIsStatic || sMass > bMass,
                Mass1 = bMass,
                Mass2 = sMass,
                Com1 = bCom,
                Com2 = sCom,
                CollisionCorrection1 = bCollisionCorrection,
                CollisionCorrection2 = sCollisionCorrection,
                ImpDirection1 = bDir,
                ImpDirection2 = sDir,
                ImpPosition1 = bCollisionCorrection,
                ImpPosition2 = sCollisionCorrection,
                Force1 = bForce,
                Force2 = sforce,
                ForcePos1 = null,
                ForcePos2 = null,
                ForceTorque1 = null,
                ForceTorque2 = null,
                CollisionAvg = collisionAvg,
                Immediate = false
            };
            Session.Instance.ThreadEvents.Enqueue(new CollisionDataThreadEvent(collisionData, this));
        }
        #endregion
    }
}

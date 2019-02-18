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
            if (entInfo.BlockUpdateTick == tick && (relation == Ent.NobodyGrid || relation == Ent.EnemyGrid))
            {
                (webent as IMyCubeGrid)?.GetBlocks(null, block =>
                {
                    entInfo.CacheBlockList.Add(new CubeAccel(block));
                    return false;
                });
            }
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

                        if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv)) Session.Instance.ThreadEvents.Enqueue(new FloaterThreadEvent(webent, this));
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

        private bool GridInside(MyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (grid != null && CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
            {
                if (CustomCollision.ObbCornersInShield(bOriBBoxD, DetectMatrixOutsideInv, _obbCorners))
                {
                    var sMass = ((MyCubeGrid)Shield.CubeGrid).GetCurrentMass();
                    var bPhysics = ((IMyCubeGrid)grid).Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sLSpeed = sPhysics.LinearVelocity;
                    var sASpeed = sPhysics.AngularVelocity * 50;
                    var sLSpeedLen = sLSpeed.LengthSquared();
                    var sASpeedLen = sASpeed.LengthSquared();
                    var sSpeedLen = sLSpeedLen > sASpeedLen ? sLSpeedLen : sASpeedLen;
                    var forceData = new MyForceData { MyGrid = grid, Force = -(grid.PositionComp.WorldAABB.Center - sPhysics.CenterOfMassWorld) * -sMass, MaxSpeed = sSpeedLen + 3 };
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
            if (entInfo.Relation != Ent.EnemyGrid && GridInside(grid, bOriBBoxD)) return;
            BlockIntersect(grid, bOriBBoxD, entInfo);

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

            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB))) return;
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
                var impulseData = new MyImpulseData { MyGrid = grid, Direction = (resultVelocity - bVel) * bMass, Position = bPhysics.CenterOfMassWorld };
                var forceData = new MyForceData { MyGrid = grid, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * bMass * ejectorAccel, MaxSpeed = MathHelper.Clamp(bVelLen, 1f, 50f) };
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

        private void BlockIntersect(MyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            try
            {

                /*
                if (collisionAvg != Vector3D.Zero)
                {
                    collisionAvg /= hits;

                    if (sPhysics.IsStatic && !bPhysics.IsStatic)
                    {
                        var bLSpeed = bPhysics.LinearVelocity;
                        var bASpeed = bPhysics.AngularVelocity * 100;
                        var bLSpeedLen = bLSpeed.Length();
                        var bASpeedLen = bASpeed.Length();
                        bASpeedLen = MathHelper.Clamp(bASpeedLen, 0, 50);
                        var bSpeedLen = bLSpeedLen > bASpeedLen ? bLSpeedLen : bASpeedLen;
                        var surfaceMass = (bMass > sMass) ? sMass : bMass;

                        var surfaceMulti = (hits > 5) ? 5 : hits;
                        var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                        var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

                        var impulseData1 = new MyImpulseData { MyGrid = breaching, Direction = (resultVelocity - bLSpeed) * bMass, Position = bPhysics.CenterOfMassWorld };
                        var impulseData2 = new MyImpulseData { MyGrid = breaching, Direction = surfaceMulti * (surfaceMass * 0.025) * -Vector3D.Dot(bLSpeed, surfaceNormal) * surfaceNormal, Position = collisionAvg };
                        var forceData = new MyForceData { MyGrid = breaching, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * (bMass * bSpeedLen), MaxSpeed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f) };
                        Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(impulseData1, this));
                        Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(impulseData2, this));
                        Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(forceData, this));
                    }
                    else
                    {
                        if (!bPhysics.IsStatic)
                        {
                            var com = bPhysics.CenterOfMassWorld;
                            var massRelation = bMass / sMass;
                            var relationClamp = MathHelper.Clamp(massRelation, 0, 1);
                            //Log.Line($"breaching: relationClamp:{relationClamp} - bMass:{bMass} - sMass:{sMass} - m/s:{bMass / sMass}");
                            var collisionCorrection = Vector3D.Lerp(com, collisionAvg, relationClamp);

                            var bImpulseData = new MyImpulseData { MyGrid = breaching, Direction = (resultVelocity - bPhysics.LinearVelocity) * bMass, Position = collisionCorrection };
                            Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(bImpulseData, this));
                        }

                        if (!sPhysics.IsStatic)
                        {
                            var com = sPhysics.CenterOfMassWorld;
                            var massRelation = sMass / bMass;
                            var relationClamp = MathHelper.Clamp(massRelation, 0, 1);
                            //Log.Line($"shield: relationClamp:{relationClamp} - bMass:{bMass} - sMass:{sMass} - s/m:{sMass / bMass}");
                            var collisionCorrection = Vector3D.Lerp(com, collisionAvg, relationClamp);
                            var sImpulseData = new MyImpulseData { MyGrid = sGrid, Direction = (resultVelocity - sPhysics.LinearVelocity) * sMass, Position = collisionCorrection };
                            Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(sImpulseData, this));
                        }
                    }
                    WebDamage = true;
                    bBlockCenter = collisionAvg;
                }
                else return;
                */

                if (entInfo == null || breaching == null || breaching.MarkedForClose) return;

                if (bOriBBoxD.Intersects(ref SOriBBoxD))
                {
                    var collisionAvg = Vector3D.Zero;
                    var transformInv = DetectMatrixOutsideInv;
                    var normalMat = MatrixD.Transpose(transformInv);
                    var damageBlocks = Session.Enforced.DisableBlockDamage == 0;
                    var bQuaternion = Quaternion.CreateFromRotationMatrix(breaching.WorldMatrix);

                    var blockDmgNum = 99999;
                    if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) blockDmgNum = 99999;

                    var bPhysics = ((IMyCubeGrid)breaching).Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sGrid = (MyCubeGrid)Shield.CubeGrid;
                    var bMass = (float)breaching.GetCurrentMass();
                    var sMass = (float)sGrid.GetCurrentMass();
                    var momentum = (bMass * bPhysics.LinearVelocity) + (sMass * sPhysics.LinearVelocity);
                    var resultVelocity = momentum / (bMass + sMass);
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

                        if (sPhysics.IsStatic && !bPhysics.IsStatic)
                        {
                            var bLSpeed = bPhysics.LinearVelocity;
                            var bASpeed = bPhysics.AngularVelocity * 100;
                            var bLSpeedLen = bLSpeed.Length();
                            var bASpeedLen = bASpeed.Length();
                            bASpeedLen = MathHelper.Clamp(bASpeedLen, 0, 50);
                            var bSpeedLen = bLSpeedLen > bASpeedLen ? bLSpeedLen : bASpeedLen;
                            var surfaceMass = (bMass > sMass) ? sMass : bMass;

                            var surfaceMulti = (hits > 5) ? 5 : hits;
                            var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

                            var impulseData1 = new MyImpulseData { MyGrid = breaching, Direction = (resultVelocity - bLSpeed) * bMass, Position = bPhysics.CenterOfMassWorld };
                            var impulseData2 = new MyImpulseData { MyGrid = breaching, Direction = surfaceMulti * (surfaceMass * 0.025) * -Vector3D.Dot(bLSpeed, surfaceNormal) * surfaceNormal, Position = collisionAvg };
                            var forceData = new MyForceData { MyGrid = breaching, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * (bMass * bSpeedLen), MaxSpeed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f) };
                            Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(impulseData1, this));
                            Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(impulseData2, this));
                            Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(forceData, this));
                        }
                        else
                        {
                            var bLSpeed = bPhysics.LinearVelocity;
                            var bASpeed = bPhysics.AngularVelocity * 100;
                            var bLSpeedLen = bLSpeed.Length();
                            var bASpeedLen = bASpeed.Length();
                            bASpeedLen = MathHelper.Clamp(bASpeedLen, 0, 50);
                            var bSpeedLen = bLSpeedLen > bASpeedLen ? bLSpeedLen : bASpeedLen;
                            float? speed;


                            if (!bPhysics.IsStatic)
                            {
                                var bImpulseData = new MyImpulseData { MyGrid = breaching, Direction = (resultVelocity - bLSpeed) * bMass, Position = bPhysics.CenterOfMassWorld };
                                Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(bImpulseData, this));
                            }

                            if (!sPhysics.IsStatic)
                            {
                                var sImpulseData = new MyImpulseData { MyGrid = sGrid, Direction = (resultVelocity - sPhysics.LinearVelocity) * sMass, Position = sPhysics.CenterOfMassWorld };
                                Session.Instance.ThreadEvents.Enqueue(new ImpulseDataThreadEvent(sImpulseData, this));
                            }

                            if (!sPhysics.IsStatic)
                            {
                                if (bMass / sMass > 20)
                                {
                                    speed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f);
                                }
                                else speed = null;

                                var sForceData = new MyForceData { MyGrid = sGrid, Force = (sPhysics.CenterOfMassWorld - collisionAvg) * bMass, MaxSpeed = speed };
                                Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(sForceData, this));
                            }

                            if (!bPhysics.IsStatic)
                            {
                                if (sMass / bMass > 20)
                                {
                                    speed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f);
                                }
                                else speed = null;

                                var bForceData = new MyForceData { MyGrid = breaching, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * sMass, MaxSpeed = speed };
                                Session.Instance.ThreadEvents.Enqueue(new ForceDataThreadEvent(bForceData, this));
                            }
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
                        if (_isServer && bBlockCenter != Vector3D.NegativeInfinity) AddShieldHit(breaching.EntityId, damage, Session.Instance.MPKinetic, null, false, collisionAvg);
                    }
                    else
                    {
                        if (bBlockCenter != Vector3D.NegativeInfinity)
                        {
                            entInfo.ContactPoint = bBlockCenter;
                            ImpactSize = entInfo.Damage;

                            entInfo.Damage = 0;
                            entInfo.EmpSize = 0;
                            WorldImpactPosition = bBlockCenter;
                        }
                    }
                    Absorb += damage;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}"); }
        }
        #endregion
    }
}

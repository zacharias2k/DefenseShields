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
    using VRage.ModAPI;
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
            if (entInfo.BlockUpdateTick == tick && (relation == Ent.LargeNobodyGrid || relation == Ent.LargeEnemyGrid))
                (webent as IMyCubeGrid)?.GetBlocks(entInfo.CacheBlockList);
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
                case Ent.SmallNobodyGrid:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent SmallNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        SmallGridIntersect(webent);
                        return;
                    }
                case Ent.LargeNobodyGrid:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent LargeNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        GridIntersect(webent);
                        return;
                    }
                case Ent.SmallEnemyGrid:
                    {
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent SmallEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        SmallGridIntersect(webent);
                        return;
                    }
                case Ent.LargeEnemyGrid:
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
                case Ent.Other:
                    {
                        if (!_isServer) return;
                        if (Session.Enforced.Debug == 3) Log.Line($"Ent Other: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        if (webent.MarkedForClose || !webent.InScene || webent.Closed) return;
                        var meteor = webent as IMyMeteor;
                        if (meteor != null)
                        {
                            if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv)) MeteorDmg.Enqueue(meteor);
                        }
                        else
                        {
                            var predictedHit = CustomCollision.MissileIntersect(this, webent, DetectionMatrix, DetectMatrixOutsideInv);
                            if (predictedHit != null) MissileDmg.Enqueue(webent);
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
                    var forceData = new MyAddForceData { MyGrid = grid, Force = -(grid.PositionComp.WorldAABB.Center - sPhysics.CenterOfMassWorld) * -sMass, MaxSpeed = sSpeedLen + 3 };
                    if (!bPhysics.IsStatic) ForceData.Enqueue(forceData);
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
            if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD)) return;
            BlockIntersect(grid, bOriBBoxD, entInfo);

            if (entInfo.MarkForClose)
            {
                EntIntersectInfo gridRemoved;
                WebEnts.TryRemove(grid, out gridRemoved);
                return;
            }

            if (!_isServer) return;

            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            entInfo.EmpDetonation = Vector3D.NegativeInfinity;
            entInfo.Damage = 0;
            entInfo.EmpSize = 0;
            if (contactpoint == Vector3D.NegativeInfinity) return;
            entInfo.Touched = true;
        }

        private void SmallGridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (ent == null || grid == null || grid.MarkedForClose || grid.Closed) return;
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB))) return;
            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            if (_isServer) CustomCollision.SmallIntersect(entInfo, FewDmgBlocks, DestroyedBlocks, ForceData, ImpulseData, grid, DetectMatrixOutside, DetectMatrixOutsideInv, Session.Enforced.DisableBlockDamage == 0);
            else CustomCollision.ClientSmallIntersect(entInfo, grid, DetectMatrixOutside, DetectMatrixOutsideInv, Eject);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                entInfo.Touched = true;
                WebDamage = true;
                if (!_isServer) return;

                var damage = entInfo.Damage * DsState.State.ModulateEnergy;
                if (_mpActive)
                {
                    if (_isServer) AddShieldHit(grid.EntityId, damage, Session.Instance.MPKinetic, null, false, contactpoint);
                }
                else
                {
                    Absorb += damage;
                    ImpactSize = entInfo.Damage;
                    WorldImpactPosition = contactpoint;
                }
                entInfo.Damage = 0;
            }
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
                var forceData = new MyAddForceData { MyGrid = grid, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * bMass * ejectorAccel, MaxSpeed = MathHelper.Clamp(bVelLen, 1f, 50f) };
                ImpulseData.Enqueue(impulseData);
                ForceData.Enqueue(forceData);
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
                if (_isServer) CharacterDmg.Enqueue(character);
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
            CharacterDmg.Enqueue(character);
        }

        private void BlockIntersect(MyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            var collisionAvg = Vector3D.Zero;
            var transformInv = DetectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);
            var damageBlocks = Session.Enforced.DisableBlockDamage == 0;

            var blockDmgNum = 5;
            if (ShieldMode == ShieldType.Station && DsState.State.Enhancer) blockDmgNum = 50;
            var intersection = bOriBBoxD.Intersects(ref SOriBBoxD);
            try
            {
                if (intersection)
                {
                    var cacheBlockList = entInfo.CacheBlockList;
                    var bPhysics = ((IMyCubeGrid)breaching).Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sGrid = (MyCubeGrid)Shield.CubeGrid;
                    var bMass = (float)breaching.GetCurrentMass();
                    var sMass = (float)sGrid.GetCurrentMass();
                    var momentum = (bMass * bPhysics.LinearVelocity) + (sMass * sPhysics.LinearVelocity);
                    var resultVelocity = momentum / (bMass + sMass);
                    Vector3D bBlockCenter;
                    var stale = false;
                    var rawDamage = 0f;
                    var blockSize = breaching.GridSize;
                    var gc = breaching.WorldToGridInteger(DetectionCenter);
                    var rc = ShieldSize.AbsMax() / blockSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var hits = 0;
                    var blockPoints = new Vector3D[9];
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        var blockPos = block.Position;
                        var num1 = gc.X - blockPos.X;
                        var num2 = gc.Y - blockPos.Y;
                        var num3 = gc.Z - blockPos.Z;
                        var result = (num1 * num1) + (num2 * num2) + (num3 * num3);

                        if (_isServer)
                        {
                            if (result > rc) continue;
                            if (damageBlocks && block.IsDestroyed)
                            {
                                DestroyedBlocks.Enqueue(block);
                                continue;
                            }
                            if (block.CubeGrid != breaching)
                            {
                                stale = true;
                                continue;
                            }
                        }
                        else
                        {
                            if (hits > blockDmgNum) break;
                            if (result > rc || block.IsDestroyed || block.CubeGrid != breaching) continue;
                        }
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;
                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, DetectMatrixOutsideInv).LengthSquared() > 1) continue;
                            collisionAvg += point;
                            hits++;
                            if (!_isServer) break;

                            if (!damageBlocks)
                            {
                                if (hits > blockDmgNum) break;
                            }
                            else if (CollidingBlocks.Count > blockDmgNum) break;

                            rawDamage += MathHelper.Clamp(block.Integrity, 0, 350);
                            if (damageBlocks) CollidingBlocks.Enqueue(block);
                            break;
                        }
                    }

                    entInfo.MarkForClose = stale;

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
                            var forceData = new MyAddForceData { MyGrid = breaching, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * (bMass * bSpeedLen), MaxSpeed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f) };
                            ImpulseData.Enqueue(impulseData1);
                            ImpulseData.Enqueue(impulseData2);
                            ForceData.Enqueue(forceData);
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
                                ImpulseData.Enqueue(bImpulseData);
                            }

                            if (!sPhysics.IsStatic)
                            {
                                var com = sPhysics.CenterOfMassWorld;
                                var massRelation = sMass / bMass;
                                var relationClamp = MathHelper.Clamp(massRelation, 0, 1);
                                //Log.Line($"shield: relationClamp:{relationClamp} - bMass:{bMass} - sMass:{sMass} - s/m:{sMass / bMass}");
                                var collisionCorrection = Vector3D.Lerp(com, collisionAvg, relationClamp);
                                var sImpulseData = new MyImpulseData { MyGrid = sGrid, Direction = (resultVelocity - sPhysics.LinearVelocity) * sMass, Position = collisionCorrection };
                                ImpulseData.Enqueue(sImpulseData);
                            }
                            /*

                            if (!sPhysics.IsStatic)
                            {
                                if (bMass / sMass > 20)
                                {
                                    speed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f);
                                }
                                else speed = null;

                                var sForceData = new MyAddForceData { MyGrid = sGrid, Force = (sPhysics.CenterOfMassWorld - collisionAvg) * bMass, MaxSpeed = speed };
                                ForceData.Enqueue(sForceData);
                            }

                            if (!bPhysics.IsStatic)
                            {
                                if (sMass / bMass > 20)
                                {
                                    speed = MathHelper.Clamp(bSpeedLen, 1f, bSpeedLen * 0.5f);
                                }
                                else speed = null;

                                var bForceData = new MyAddForceData { MyGrid = breaching, Force = (bPhysics.CenterOfMassWorld - collisionAvg) * sMass, MaxSpeed = speed };
                                ForceData.Enqueue(bForceData);
                            }
                            // Fix this if broke

                            Vector3 bVel = bPhysics.LinearVelocity;
                            Vector3 sVel = sPhysics.LinearVelocity;
                            Vector3 relVel = bVel - sVel;

                            // Assuming headon collision for simplicity
                            Vector3 bVelFinal = (bMass - sMass) / (bMass + sMass) * relVel;
                            Vector3 sVelFinal = (sMass - bMass) / (bMass + sMass) * relVel;

                            Vector3 bImpulse = bMass * bVelFinal;
                            Vector3 sImpulse = sMass * sVelFinal;

                            if (!bPhysics.IsStatic)
                            {
                                var bImpulseData = new MyImpulseData { MyGrid = breaching, Direction = bImpulse, Position = collisionAvg };
                                ImpulseData.Enqueue(bImpulseData);
                            }

                            if (!sPhysics.IsStatic)
                            {
                                var sImpulseData = new MyImpulseData { MyGrid = sGrid, Direction = sImpulse, Position = collisionAvg };
                                ImpulseData.Enqueue(sImpulseData);
                            }
                            Log.Line($"sImpulse:{sImpulse} -sMass:{sMass} - sVel:{sVel.Length()} - bImpulse:{bImpulse} - bMass:{bMass} - bVel:{bVel.Length()} - Position:{collisionAvg}");
                            */

                        }
                        WebDamage = true;
                        bBlockCenter = collisionAvg;
                    }
                    else return;
                    if (!_isServer) return;

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
        private float ComputeAmmoDamage(IMyEntity ammoEnt)
        {
            AmmoInfo ammoInfo;
            Session.Instance.AmmoCollection.TryGetValue(ammoEnt.Model.AssetName, out ammoInfo);
            var damage = 10f;
            if (ammoInfo == null)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - No Missile Ammo Match Found for {((MyEntity)ammoEnt).DebugName}! Let wepaon mod author know their ammo definition has improper model path");
                return damage;
            }
            var dmgMulti = UtilsStatic.GetDmgMulti(ammoInfo.BackKickForce);
            if (dmgMulti > 0)
            {
                if (ammoInfo.Explosive) damage = (ammoInfo.Damage * (ammoInfo.Radius * 0.5f)) * 7.5f * dmgMulti;
                else damage = ammoInfo.Mass * ammoInfo.Speed * dmgMulti;
                return damage;
            }
            if (dmgMulti.Equals(-1f))
            {
                damage = -damage;
                return damage;
            }
            if (ammoInfo.BackKickForce < 0 && dmgMulti.Equals(0)) damage = float.NegativeInfinity; 
            else if (ammoInfo.Explosive) damage = ammoInfo.Damage * (ammoInfo.Radius * 0.5f) * 7.5f;
            else damage = ammoInfo.Mass * ammoInfo.Speed;

            if (ammoInfo.Mass < 0 && ammoInfo.Radius <= 0) damage = -damage;
            return damage;
        }

        private float ComputeAmmoDamage2(IMyEntity ammoEnt)
        {
            AmmoInfo ammoInfo;
            Session.Instance.AmmoCollection.TryGetValue(ammoEnt.Model.AssetName, out ammoInfo);
            var damage = 10f;
            if (ammoInfo == null)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - No Missile Ammo Match Found for {((MyEntity)ammoEnt).DebugName}! Let wepaon mod author know their ammo definition has improper model path");
                return damage;
            }
            /*
            if (ammoInfo.KineticWeapon)
                damage = ammoInfo.shieldDamage * DsState.State.ModulateEnergy; //kinetic weapon
            else
                damage = ammoInfo.shieldDamage * DsState.State.ModulateKinetic; //energy weapon
            */
            return damage;
        }
    }
}

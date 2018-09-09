using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Intersect
        private bool GridInside(MyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD, MyEntity ent)
        {
            if (grid != null && CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, DetectMatrixInInv))
            {
                if (CustomCollision.AllCornersInShield(bOriBBoxD, DetectMatrixInInv))
                {
                    var sMass = ((MyCubeGrid)Shield.CubeGrid).GetCurrentMass();
                    var bPhysics = ((IMyCubeGrid)grid).Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var sLSpeed = sPhysics.LinearVelocity;
                    var sASpeed = sPhysics.AngularVelocity * 50;
                    var sLSpeedLen = sLSpeed.LengthSquared();
                    var sASpeedLen = sASpeed.LengthSquared();
                    var sSpeedLen = sLSpeedLen > sASpeedLen ? sLSpeedLen : sASpeedLen;
                    if (!bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(grid.PositionComp.WorldAABB.Center - sPhysics.CenterOfMassWorld) * -sMass, null, Vector3D.Zero, sSpeedLen + 3);

                    return true;
                }
            }
            return false;
        }

        private void SmallGridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (ent == null || grid == null || grid.MarkedForClose || grid.Closed) return;

            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB), ent)) return;
            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            CustomCollision.SmallIntersect(entInfo, _fewDmgBlocks, _destroyedBlocks, grid, DetectMatrixOutside, DetectMatrixOutsideInv);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                entInfo.Touched = true;
                var damage = entInfo.Damage * DsState.State.ModulateKinetic;
                if (Session.MpActive)
                {
                    if (Session.IsServer)
                    {
                        ShieldDoDamage(damage, grid.EntityId);
                    }
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

        private void GridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (grid == null) return;

            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB);
            if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD, ent)) return;
            BlockIntersect(grid, bOriBBoxD, entInfo);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint == Vector3D.NegativeInfinity) return;

            entInfo.Touched = true;
            ImpactSize = entInfo.Damage;

            entInfo.Damage = 0;
            WorldImpactPosition = contactpoint;
        }

        private void ShieldIntersect(MyEntity ent)
        {
            var grid = ent as MyCubeGrid;
            if (grid == null) return;

            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB), ent)) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);
            var ds = shieldComponent.DefenseShields;
            var dsVerts = ds.ShieldComp.PhysicsOutside;
            var dsMatrixInv = ds.DetectMatrixOutsideInv;
            var myGrid = Shield.CubeGrid;

            var insidePoints = new List<Vector3D>();
            CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, ShieldComp.PhysicsOutside, DetectMatrixOutsideInv, insidePoints);

            var bPhysics = ((IMyCubeGrid)grid).Physics;
            var sPhysics = myGrid.Physics;
            var bMass = grid.GetCurrentMass();
            var sMass = ((MyCubeGrid)myGrid).GetCurrentMass();

            if (bMass <= 0) bMass = int.MaxValue;
            if (sMass <= 0) sMass = int.MaxValue;

            var momentum = bMass * bPhysics.LinearVelocity + sMass * sPhysics.LinearVelocity;
            var resultVelocity = momentum / (bMass + sMass);


            var collisionAvg = Vector3D.Zero;
            for (int i = 0; i < insidePoints.Count; i++)
            {
                collisionAvg += insidePoints[i];
            }

            if (insidePoints.Count > 0 && !bPhysics.IsStatic) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bMass, bPhysics.CenterOfMassWorld);
            //if (insidePoints.Count > 0 && !sPhysics.IsStatic) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sMass, sPhysics.CenterOfMassWorld);

            collisionAvg /= insidePoints.Count;
            //if (insidePoints.Count > 0 && !sPhysics.IsStatic) sPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * sMass, null, Vector3D.Zero, MathHelper.Clamp(sPhysics.LinearVelocity.Length(), 10f, 50f));
            if (insidePoints.Count > 0 && !bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, (bPhysics.CenterOfMassWorld - collisionAvg) * bMass * 10, null, Vector3D.Zero, MathHelper.Clamp(bPhysics.LinearVelocity.Length(), 1f, 50f));

            if (insidePoints.Count <= 0) return;

            var gridMaxCharge = ds._shieldChargeRate;
            var damage = gridMaxCharge * Session.Enforced.Efficiency * DsState.State.ModulateEnergy * 0.05f;
            if (Session.MpActive)
            {
                if (Session.IsServer)
                {
                    ShieldDoDamage(damage, grid.EntityId);
                }
            }
            else
            {
                WorldImpactPosition = collisionAvg;
                ds.WorldImpactPosition = collisionAvg;
                Absorb += damage;
                ImpactSize = damage;
            }
        }

        private void VoxelIntersect(MyVoxelBase voxelBase)
        {
            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(voxelBase, out entInfo);
            var myGrid = (MyCubeGrid)Shield.CubeGrid;
            var collision = CustomCollision.VoxelCollisionSphere(myGrid, ShieldComp.PhysicsOutsideLow, voxelBase, SOriBBoxD, DetectMatrixOutside);
            if (collision != Vector3D.NegativeInfinity)
            {
                var mass = myGrid.GetCurrentMass();
                var sPhysics = Shield.CubeGrid.Physics;
                var momentum = mass * sPhysics.LinearVelocity;
                Absorb += (momentum.Length() / 500) * DsState.State.ModulateKinetic;
                ImpactSize = 12000;
                WorldImpactPosition = collision;
                //if (!Session.MpActive && !(voxelBase is MyPlanet)) _voxelDmg.Enqueue(voxelBase);
            }
        }

        private void PlayerIntersect(MyEntity ent)
        {
            var playerInfo = WebEnts[ent];
            var character = ent as IMyCharacter;
            if (character == null) return;
            var npcname = character.ToString();
            if (npcname.Equals(SpaceWolf))
            {
                _characterDmg.Enqueue(character);
                return;
            }
            if (character.EnabledDamping) character.SwitchDamping();
            if (!character.EnabledThrusts) return;

            var insideTime = (int)playerInfo.LastTick - (int)playerInfo.FirstTick;
            if (insideTime < 3000) return;
            EntIntersectInfo playerRemoved;
            WebEnts.TryRemove(ent, out playerRemoved);

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;
            _characterDmg.Enqueue(character);
        }

        private void BlockIntersect(MyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            var collisionAvg = Vector3D.Zero;
            var transformInv = DetectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);
            var massMulti = 1000f;
            var blockDmgNum = 5;
            if (ShieldMode == ShieldType.Station && DsState.State.Enhancer)
            {
                blockDmgNum = 50;
                massMulti = massMulti / DsState.State.EnhancerProtMulti;
            }
            var intersection = bOriBBoxD.Intersects(ref SOriBBoxD);
            try
            {
                if (intersection)
                {
                    var cacheBlockList = entInfo.CacheBlockList;
                    var bPhysics = ((IMyCubeGrid)breaching).Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var bMass = breaching.GetCurrentMass();
                    var sMass = ((MyCubeGrid)Shield.CubeGrid).GetCurrentMass();
                    var momentum = bMass * bPhysics.LinearVelocity + sMass * sPhysics.LinearVelocity;
                    var resultVelocity = momentum / (bMass + sMass);
                    var bBlockCenter = Vector3D.NegativeInfinity;

                    var stale = false;
                    var rawDamage = 0f;
                    Vector3I gc = breaching.WorldToGridInteger(DetectionCenter);
                    double rc = ShieldSize.AbsMax() / breaching.GridSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var c1 = 0;
                    var c2 = 0;
                    var c3 = 0;
                    var c4 = 0;
                    var c5 = 0;
                    var c6 = 0;
                    Vector3D[] blockPoints = new Vector3D[9];
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        Vector3I blockPos = block.Position;
                        int num1 = gc.X - blockPos.X;
                        int num2 = gc.Y - blockPos.Y;
                        int num3 = gc.Z - blockPos.Z;
                        int result = num1 * num1 + num2 * num2 + num3 * num3;

                        if (result > rc) continue;
                        c1++;
                        if (block.IsDestroyed)
                        {
                            c6++;
                            _destroyedBlocks.Enqueue(block);
                            continue;
                        }
                        if (block.CubeGrid != breaching)
                        {
                            if (!stale) _staleGrids.Enqueue(breaching);
                            stale = true;
                            continue;
                        }
                        c2++;
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;
                        //var point2 = Vector3D.Clamp(_detectMatrixOutsideInv.Translation, blockBox.Min, blockBox.Max);
                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, DetectMatrixOutsideInv).LengthSquared() > 1) continue;

                            collisionAvg += point;
                            c3++;

                            if (_dmgBlocks.Count > blockDmgNum) break;
                            c4++;
                            rawDamage += block.Mass * massMulti;
                            _dmgBlocks.Enqueue(block);
                            break;
                        }
                    }
                    if (collisionAvg != Vector3D.Zero)
                    {
                        collisionAvg /= c3;

                        var sLSpeed = sPhysics.LinearVelocity;
                        var sASpeed = sPhysics.AngularVelocity * 50;
                        var sLSpeedLen = sLSpeed.Length();
                        var sASpeedLen = sASpeed.Length();
                        var sSpeedLen = sLSpeedLen > sASpeedLen ? sLSpeedLen : sASpeedLen;

                        if (!bPhysics.IsStatic) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bMass, bPhysics.CenterOfMassWorld);
                        if (!sPhysics.IsStatic) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sMass, sPhysics.CenterOfMassWorld);
                        var surfaceMass = (bMass > sMass) ? sMass : bMass;
                        var surfaceMulti = (c3 > 5) ? 5 : c3;
                        var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                        var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                        if (!bPhysics.IsStatic) bPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 40) * -Vector3D.Dot(bPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        if (!sPhysics.IsStatic) sPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 40) * -Vector3D.Dot(sPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        if (!bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * -sMass, null, Vector3D.Zero, sSpeedLen + 3);

                        bBlockCenter = collisionAvg;
                    }
                    var damage = rawDamage / 100 * DsState.State.ModulateKinetic;
                    entInfo.Damage = damage;
                    if (Session.MpActive)
                    {
                        if (Session.IsServer && bBlockCenter != Vector3D.NegativeInfinity) ShieldDoDamage(damage, breaching.EntityId);
                    }
                    else
                    {
                        Absorb += damage;
                        if (bBlockCenter != Vector3D.NegativeInfinity) entInfo.ContactPoint = bBlockCenter;
                    }
                    //Log.Line($"[status] obb: true - blocks:{cacheBlockList.Count.ToString()} - sphered:{c1.ToString()} [{c5.ToString()}] - IsDestroyed:{c6.ToString()} not:[{c2.ToString()}] - bCenter Inside Ellipsoid:{c3.ToString()} - Damaged:{c4.ToString()}");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}"); }
        }
        #endregion

        #region Compute Missile Intersect Damage
        private float ComputeAmmoDamage(IMyEntity ammoEnt)
        {
            //bypass < 0 kickback
            //Ignores Shield entirely.
            //
            //healing < 0 mass ,  radius 0
            //Heals Shield, converting weapon damage to healing value.
            //Values as close to Zero (0) as possible, to best results, and less unintentional Results.
            //Shield-Damage: All values such as projectile Velocity & Mass for non-explosive types and Explosive-damage when dealing with Explosive-types.
            AmmoInfo ammoInfo;
            Session.AmmoCollection.TryGetValue(ammoEnt.Model.AssetName, out ammoInfo);
            var damage = 10f;
            if (ammoInfo == null)
            {
                Log.Line($"ShieldId:{Shield.EntityId.ToString()} - No Missile Ammo Match Found for {((MyEntity)ammoEnt).DebugName}! Let wepaon mod author know their ammo definition has improper model path");
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
        #endregion
    }
}

using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Intersect
        private bool GridInside(IMyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, _detectInsideInv))
            {
                if (CustomCollision.AllCornersInShield(bOriBBoxD, _detectMatrixOutsideInv)) return true;

                var ejectDir = CustomCollision.EjectDirection(grid, SGridComponent.PhysicsOutside, _dataStructures.p3VertTris, bOriBBoxD, _detectMatrixOutsideInv);
                if (ejectDir == Vector3D.NegativeInfinity) return false;
                Eject.TryAdd(grid, ejectDir);

                return true;
            }
            return false;
        }

        private void SmallGridIntersect(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;

            EntIntersectInfo entInfo;
            _webEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            CustomCollision.SmallIntersect(entInfo, _fewDmgBlocks, grid, _detectMatrixOutside, _detectMatrixOutsideInv);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                Absorb += entInfo.Damage;
                ImpactSize += entInfo.Damage;

                entInfo.Damage = 0;
                WorldImpactPosition = contactpoint;
            }
        }

        private void GridIntersect(IMyEntity ent)
        {
            lock (_webEnts)
            {
                var grid = (IMyCubeGrid)ent;
                EntIntersectInfo entInfo;
                _webEnts.TryGetValue(ent, out entInfo);
                if (entInfo == null) return;

                var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB);
                if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD)) return;
                BlockIntersect(grid, bOriBBoxD, entInfo);
                var contactpoint = entInfo.ContactPoint;
                entInfo.ContactPoint = Vector3D.NegativeInfinity;
                if (contactpoint == Vector3D.NegativeInfinity) return;
                ImpactSize += entInfo.Damage;

                entInfo.Damage = 0;
                WorldImpactPosition = contactpoint;
            }
        }

        private void ShieldIntersect(IMyCubeGrid grid)
        {
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);

            var dsVerts = shieldComponent.DefenseShields.SGridComponent.PhysicsOutside;
            var dsMatrixInv = shieldComponent.DefenseShields._detectMatrixOutsideInv;
            var myGrid = Shield.CubeGrid;

            var insidePoints = new List<Vector3D>();
            CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, SGridComponent.PhysicsOutside, _detectMatrixOutsideInv, insidePoints);

            var bPhysics = grid.Physics;
            var sPhysics = myGrid.Physics;
            var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
            var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);

            var collisionAvg = Vector3D.Zero;
            for (int i = 0; i < insidePoints.Count; i++)
            {
                collisionAvg += insidePoints[i];
            }

            if (insidePoints.Count > 0 && !bPhysics.IsStatic) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, bPhysics.CenterOfMassWorld);
            if (insidePoints.Count > 0 && !sPhysics.IsStatic) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, sPhysics.CenterOfMassWorld);

            collisionAvg /= insidePoints.Count;
            if (insidePoints.Count > 0 && !sPhysics.IsStatic) sPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * sPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(sPhysics.LinearVelocity.Length(), 10f, 50f));
            if (insidePoints.Count > 0 && !bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - bPhysics.CenterOfMassWorld) * bPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(bPhysics.LinearVelocity.Length(), 10f, 50f));

            if (insidePoints.Count <= 0) return;

            var contactPoint = DSUtils.CreateFromPointsList(insidePoints).Center; // replace with average
            WorldImpactPosition = contactPoint;
            shieldComponent.DefenseShields.WorldImpactPosition = contactPoint;
            var damage = 1f;
            var bDamage = (bPhysics.Mass * bPhysics.LinearVelocity).Length();
            var sDamage = (sPhysics.Mass * sPhysics.LinearVelocity).Length();
            damage = bDamage < sDamage ? bDamage : sDamage;
            Absorb += damage / 1000;
        }

        private void VoxelIntersect(MyVoxelBase voxelBase)
        {
            EntIntersectInfo entInfo;
            _webEnts.TryGetValue(voxelBase, out entInfo);
            var collision = CustomCollision.VoxelCollisionSphere(Shield.CubeGrid, SGridComponent.PhysicsOutsideLow, voxelBase, _sOriBBoxD, entInfo.TempStorage, _detectMatrixOutside);

            if (collision != Vector3D.NegativeInfinity)
            {
                var sPhysics = Shield.CubeGrid.Physics;
                var momentum = sPhysics.Mass * sPhysics.LinearVelocity;
                Absorb += momentum.Length() / 500;
                WorldImpactPosition = collision;
                _voxelDmg.Enqueue(voxelBase);
            }
        }

        private void PlayerIntersect(IMyEntity ent)
        {
            var playerInfo = _webEnts[ent];
            var character = ent as IMyCharacter;
            if (character == null) return;
            var npcname = character.ToString();
            if (npcname.Equals("Space_Wolf"))
            {
                _characterDmg.Enqueue(character);
                return;
            }
            if (character.EnabledDamping) character.SwitchDamping();
            if (!character.EnabledThrusts) return;

            var insideTime = (int)playerInfo.LastTick - (int)playerInfo.FirstTick;
            if (insideTime < 3000) return;
            _webEnts.Remove(ent);

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;
            _characterDmg.Enqueue(character);
        }

        private void BlockIntersect(IMyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            var collisionAvg = Vector3D.Zero;
            var transformInv = _detectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);
            var intersection = bOriBBoxD.Intersects(ref _sOriBBoxD);
            try
            {
                if (intersection)
                {
                    var cacheBlockList = entInfo.CacheBlockList;
                    var bPhysics = breaching.Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
                    var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);
                    var bBlockCenter = Vector3D.NegativeInfinity;

                    var stale = false;
                    var damage = 0f;
                    Vector3I gc = breaching.WorldToGridInteger(_detectionCenter);
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
                            if (Vector3.Transform(point, _detectMatrixOutsideInv).LengthSquared() > 1) continue;

                            collisionAvg += point;
                            c3++;

                            if (_dmgBlocks.Count > 50) break;
                            c4++;
                            damage += block.Mass;
                            _dmgBlocks.Enqueue(block);
                            break;
                        }
                    }
                    if (collisionAvg != Vector3D.Zero)
                    {
                        collisionAvg /= c3;
                        if (!bPhysics.IsStatic) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, bPhysics.CenterOfMassWorld);
                        if (!sPhysics.IsStatic) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, sPhysics.CenterOfMassWorld);
                        var surfaceMass = (bPhysics.Mass > sPhysics.Mass) ? sPhysics.Mass : bPhysics.Mass;
                        var surfaceMulti = (c3 > 5) ? 5 : c3;
                        var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                        var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                        if (!bPhysics.IsStatic) bPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 20) * -Vector3D.Dot(bPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        if (!sPhysics.IsStatic) sPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 20) * -Vector3D.Dot(sPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        bBlockCenter = collisionAvg;
                    }
                    entInfo.Damage = damage;
                    Absorb += damage;
                    if (bBlockCenter != Vector3D.NegativeInfinity) entInfo.ContactPoint = bBlockCenter;
                    //if (_count == 58) Log.Line($"[status] obb: true - blocks:{cacheBlockList.Count.ToString()} - sphered:{c1.ToString()} [{c5.ToString()}] - IsDestroyed:{c6.ToString()} not:[{c2.ToString()}] - bCenter Inside Ellipsoid:{c3.ToString()} - Damaged:{c4.ToString()}");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}"); }
        }
        #endregion
    }
}

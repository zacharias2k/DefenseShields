using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Web Entities
        private void WebEntitiesClient()
        {
            if (Session.Enforced.Debug >= 1) Dsutil2.Sw.Restart();
            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange + 5);
            var pruneList = new List<MyEntity>();

            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);

            foreach (var eShield in EnemyShields) pruneList.Add(eShield);

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
            var entChanged = false;

            _enablePhysics = false;
            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                var voxel = ent as MyVoxelBase;

                if (ent == null || ent.MarkedForClose || !GridIsMobile && voxel != null || disableVoxels && voxel != null || voxel != null && voxel != voxel.RootVoxel || !(ent is MyVoxelBase) && ent.Physics == null) continue;

                if (FriendlyCache.Contains(ent) || IgnoreCache.Contains(ent) || PartlyProtectedCache.Contains(ent) || AuthenticatedCache.Contains(ent) || !(ent is MyCubeGrid) && !(ent is MyVoxelBase) && !(ent is IMyCharacter)) continue;
                EntIntersectInfo entInfo;
                WebEnts.TryGetValue(ent, out entInfo);
                var relation = entInfo?.Relation ?? EntType(ent);
                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Friend:
                        if (relation == Ent.Friend)
                        {
                            var grid = ent as MyCubeGrid;
                            if (grid != null)
                            {
                                if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                                {
                                    var cornersInShield = CustomCollision.NotAllCornersInShield(grid, DetectMatrixOutsideInv);
                                    if (cornersInShield > 0 && cornersInShield != 8) PartlyProtectedCache.Add(ent);
                                    else if (cornersInShield == 8) FriendlyCache.Add(ent);
                                }
                            }
                            else if (CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                            {
                                FriendlyCache.Add(ent);
                                continue;
                            }
                            IgnoreCache.Add(ent);
                        }
                        continue;
                }
                if (entInfo != null)
                {
                    var interestingEnts = relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid || relation == Ent.SmallEnemyGrid || relation == Ent.SmallNobodyGrid || relation == Ent.Shielded;
                    if (ent.Physics != null && ent.Physics.IsMoving) entChanged = true;
                    else if (entInfo.Touched || _count == 0 && interestingEnts && !ent.PositionComp.LocalAABB.Equals(entInfo.Box))
                    {
                        entInfo.Box = ent.PositionComp.LocalAABB;
                        entChanged = true;
                    }

                    _enablePhysics = true;
                    entInfo.LastTick = _tick;
                }
                else
                {
                    if (relation == Ent.Other && CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                    {
                        IgnoreCache.Add(ent);
                        continue;
                    }
                    if ((relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid) && CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv))
                    {
                        FriendlyCache.Add(ent);
                        WebEnts.Remove(ent);
                        continue;
                    }
                    entChanged = true;
                    _enablePhysics = true;
                    WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, _tick, _tick, relation, new List<IMySlimBlock>()));
                }
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (_enablePhysics && !ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                if (!disableVoxels) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (_enablePhysics && (ShieldComp.GridIsMoving || entChanged)) MyAPIGateway.Parallel.Start(WebDispatchClient);

            if (Session.Enforced.Debug >= 1) Dsutil2.StopWatchReport($"WebClient: ShieldId [{Shield.EntityId}]", 3);
        }

        private void WebDispatchClient()
        {
            if (Session.Enforced.Debug >= 1) Dsutil3.Sw.Restart();
            foreach (var webent in WebEnts.Keys)
            {
                var entInfo = WebEnts[webent];
                if (entInfo.LastTick != _tick) continue;
                if (entInfo.FirstTick == _tick && (WebEnts[webent].Relation == Ent.LargeNobodyGrid || WebEnts[webent].Relation == Ent.LargeEnemyGrid))
                    (webent as IMyCubeGrid)?.GetBlocks(WebEnts[webent].CacheBlockList, CollectCollidableBlocks);
                switch (WebEnts[webent].Relation)
                {
                    case Ent.EnemyPlayer:
                    {
                        if ((_count == 2 || _count == 17 || _count == 32 || _count == 47) && CustomCollision.PointInShield(webent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent EnemyPlayer: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => PlayerIntersectClient(webent));
                        }
                        continue;
                    }
                    case Ent.SmallNobodyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientSmallGridIntersect(webent));
                            continue;
                        }
                    case Ent.LargeNobodyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientGridIntersect(webent));
                            continue;
                        }
                    case Ent.SmallEnemyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientSmallGridIntersect(webent));
                            continue;
                        }
                    case Ent.LargeEnemyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientGridIntersect(webent));
                            continue;
                        }
                    case Ent.Shielded:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent Shielded: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientShieldIntersect(webent as MyCubeGrid));
                            continue;
                        }
                    case Ent.VoxelBase:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent VoxelBase: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ClientVoxelIntersect(webent as MyVoxelBase));
                            continue;
                        }
                    default:
                        continue;
                }
            }

            if (Session.Enforced.Debug >= 1 && _lCount == 5 && _count == 5)
            if (Session.Enforced.Debug >= 1) Dsutil3.StopWatchReport($"webDispatch: ShieldId [{Shield.EntityId}]:", 3);
        }
        #endregion

        #region Intersect
        private void ClientSmallGridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (ent == null || grid == null || grid.MarkedForClose || grid.Closed) return;

            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB), ent)) return;
            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            CustomCollision.ClientSmallIntersect(entInfo, grid, DetectMatrixOutside, DetectMatrixOutsideInv);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                entInfo.Touched = true;
            }

        }

        private void ClientGridIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;
            if (grid == null) return;

            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB);
            if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD, ent)) return;
            ClientBlockIntersect(grid, bOriBBoxD, entInfo);
        }

        private void ClientShieldIntersect(MyEntity ent)
        {
            var grid = (MyCubeGrid)ent;

            if (grid == null) return;

            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.PositionComp.WorldAABB), ent)) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);

            var dsVerts = shieldComponent.DefenseShields.ShieldComp.PhysicsOutside;
            var dsMatrixInv = shieldComponent.DefenseShields.DetectMatrixOutsideInv;
            var myGrid = Shield.CubeGrid;

            var insidePoints = new List<Vector3D>();
            CustomCollision.ClientShieldX2PointsInside(dsVerts, dsMatrixInv, ShieldComp.PhysicsOutsideLow, DetectMatrixOutsideInv, insidePoints);

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
            if (insidePoints.Count > 0 && !bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - bPhysics.CenterOfMassWorld) * bMass, null, Vector3D.Zero, MathHelper.Clamp(bPhysics.LinearVelocity.Length(), 10f, 50f));
        }

        private void ClientVoxelIntersect(MyVoxelBase voxelBase)
        {
            EntIntersectInfo entInfo;
            WebEnts.TryGetValue(voxelBase, out entInfo);
            var myGrid = (MyCubeGrid)Shield.CubeGrid;
            var collision = CustomCollision.VoxelCollisionSphere(myGrid, ShieldComp.PhysicsOutsideLow, voxelBase, SOriBBoxD, DetectMatrixOutside);
            if (collision != Vector3D.NegativeInfinity)
            {
                ImpactSize = 12000;
                WorldImpactPosition = collision;
            }
        }

        private void PlayerIntersectClient(MyEntity ent)
        {
            var character = ent as IMyCharacter;
            if (character == null) return;
            if (character.EnabledDamping) character.SwitchDamping();
        }

        private void ClientBlockIntersect(MyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            var collisionAvg = Vector3D.Zero;
            var transformInv = DetectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);

            var blockDmgNum = 50;
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

                    Vector3I gc = breaching.WorldToGridInteger(DetectionCenter);
                    double rc = ShieldSize.AbsMax() / breaching.GridSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var c1 = 0;
                    var c2 = 0;

                    Vector3D[] blockPoints = new Vector3D[9];
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        Vector3I blockPos = block.Position;
                        int num1 = gc.X - blockPos.X;
                        int num2 = gc.Y - blockPos.Y;
                        int num3 = gc.Z - blockPos.Z;
                        int result = num1 * num1 + num2 * num2 + num3 * num3;

                        if (result > rc || block.IsDestroyed || block.CubeGrid != breaching) continue;
                        c1++;
                        if (c1 > blockDmgNum) break;
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;

                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, DetectMatrixOutsideInv).LengthSquared() > 1) continue;

                            collisionAvg += point;
                            c2++;
                            break;
                        }
                    }
                    if (collisionAvg != Vector3D.Zero)
                    {
                        collisionAvg /= c2;
                        if (sPhysics.IsStatic && !bPhysics.IsStatic)
                        {
                            var bLSpeed = bPhysics.LinearVelocity;
                            var bASpeed = bPhysics.AngularVelocity * 50;
                            var bLSpeedLen = bLSpeed.LengthSquared();
                            var bASpeedLen = bASpeed.LengthSquared();
                            var bSpeedLen = bLSpeedLen > bASpeedLen ? bLSpeedLen : bASpeedLen;

                            var surfaceMass = (bMass > sMass) ? sMass : bMass;

                            var surfaceMulti = (c2 > 5) ? 5 : c2;
                            var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                            bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bMass, bPhysics.CenterOfMassWorld);
                            bPhysics.ApplyImpulse(surfaceMulti * (surfaceMass * 0.025) * -Vector3D.Dot(bPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                            bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, (bPhysics.CenterOfMassWorld - collisionAvg) * (bMass * bSpeedLen), null, Vector3D.Zero, MathHelper.Clamp(bSpeedLen, 1f, 8f));
                        }
                        else
                        {
                            var surfaceMass = bMass > sMass ? bMass : sMass;
                            if (!bPhysics.IsStatic) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bMass, bPhysics.CenterOfMassWorld);
                            if (!sPhysics.IsStatic) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sMass, sPhysics.CenterOfMassWorld);
                            if (!sPhysics.IsStatic) sPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, (sPhysics.CenterOfMassWorld - collisionAvg) * surfaceMass, null, Vector3D.Zero, null, false);
                            if (!bPhysics.IsStatic) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, (bPhysics.CenterOfMassWorld - collisionAvg) * surfaceMass, null, Vector3D.Zero, null, false);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}"); }
        }
        #endregion
    }
}

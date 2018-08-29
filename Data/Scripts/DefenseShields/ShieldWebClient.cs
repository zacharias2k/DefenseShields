using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
            if (Session.Enforced.Debug == 1) Dsutil2.Sw.Restart();
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
                var relation = EntType(ent);
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
                lock (WebEnts)
                {
                    EntIntersectInfo entInfo;
                    WebEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
                        var interestingEnts = relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid || relation == Ent.SmallEnemyGrid || relation == Ent.SmallNobodyGrid;
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
                        WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, _tick, _tick, relation, new List<IMySlimBlock>()));
                    }
                }
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (_enablePhysics && !ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                if (!disableVoxels) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (_enablePhysics && (ShieldComp.GridIsMoving || entChanged)) MyAPIGateway.Parallel.Start(WebDispatchClient);

            if (Session.Enforced.Debug == 1) Dsutil2.StopWatchReport($"WebClient: ShieldId [{Shield.EntityId}]", 3);
        }

        private void WebDispatchClient()
        {
            if (Session.Enforced.Debug == 1) Dsutil3.Sw.Restart();
            lock (WebEnts)
            {
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
            }

            if (Session.Enforced.Debug == 1 && _lCount == 5 && _count == 5)
            if (Session.Enforced.Debug == 1) Dsutil3.StopWatchReport($"webDispatch: ShieldId [{Shield.EntityId}]:", 3);
        }
        #endregion
    }
}

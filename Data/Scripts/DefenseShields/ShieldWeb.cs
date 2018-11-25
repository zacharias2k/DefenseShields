using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Web Entities
        public void ProtectMyself()
        {
            FriendlyCache.Clear();
            foreach (var sub in ShieldComp.GetSubGrids)
            {
                if (sub == null) continue;
                FriendlyCache.Add(sub);

                var protectors = Session.Instance.GlobalProtectDict[sub] = new MyProtectors(Session.Instance.ProtDicts.Get(), LogicSlot, Tick);

                if (!GridIsMobile && ShieldEnt.PositionComp.WorldVolume.Intersects(sub.PositionComp.WorldVolume))
                {
                    var cornersInShield = CustomCollision.NotAllCornersInShield(sub, DetectMatrixOutsideInv);
                    if (cornersInShield != 8) protectors.Shields[this] = new ProtectorInfo(true, false);
                    else if (cornersInShield == 8) protectors.Shields[this] = new ProtectorInfo(true, true);
                    continue;
                }
                protectors.Shields[this] = new ProtectorInfo(true, true);
            }
        }

        public void WebEntities()
        {
            var sleeping = true;
            PruneList.Clear();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref PruneSphere2, PruneList);
            foreach (var eShield in EnemyShields) PruneList.Add(eShield);
            if (Missiles.Count > 0)
            {
                sleeping = false;
                var missileSphere = PruneSphere2;
                missileSphere.Radius = BoundingRange + 50;
                foreach (var missile in Missiles)
                    if (missile.InScene && !missile.MarkedForClose && missileSphere.Intersects(missile.PositionComp.WorldVolume)) PruneList.Add(missile);
            }

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
            var entChanged = false;

            EnablePhysics = false;
            for (int i = 0; i < PruneList.Count; i++)
            {
                var ent = PruneList[i];
                var voxel = ent as MyVoxelBase;
                if (ent == null || ent.MarkedForClose || voxel == null && (ent.Physics == null || ent.DefinitionId == null) || !GridIsMobile && voxel != null || disableVoxels && voxel != null || voxel != null && voxel != voxel.RootVoxel) continue;
                if (ent is IMyFloatingObject || ent is IMyEngineerToolBase || FriendlyCache.Contains(ent) || FriendlyMissileCache.Contains(ent) || AuthenticatedCache.Contains(ent)) continue;
                EntIntersectInfo entInfo;
                WebEnts.TryGetValue(ent, out entInfo);
                Ent relation;
                if (entInfo != null)
                {
                    if (Tick600) entInfo.Relation = EntType(ent);
                    relation = entInfo.Relation;
                }
                else relation = EntType(ent);

                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Friend:
                        if (relation == Ent.Friend)
                        {
                            MyProtectors protectors;
                            Session.Instance.GlobalProtectDict.TryGetValue(ent, out protectors);
                            if (protectors.Shields == null) protectors = Session.Instance.GlobalProtectDict[ent] = new MyProtectors(Session.Instance.ProtDicts.Get(), LogicSlot, Tick);

                            var grid = ent as MyCubeGrid;
                            var parent = ShieldComp.GetLinkedGrids.Contains(grid);
                            if (grid != null)
                            {
                                if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                                {
                                    var cornersInShield = CustomCollision.NotAllCornersInShield(grid, DetectMatrixOutsideInv);
                                    if (cornersInShield > 0 && cornersInShield != 8)
                                    {
                                        FriendlyCache.Add(ent);
                                        protectors.Shields[this] = new ProtectorInfo(parent, false);
                                    }
                                    else if (cornersInShield == 8)
                                    {
                                        FriendlyCache.Add(ent);
                                        protectors.Shields[this] = new ProtectorInfo(parent, true);
                                    }
                                }
                            }
                            else if (CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                            {
                                FriendlyCache.Add(ent);
                                protectors.Shields[this] = new ProtectorInfo(parent, true);
                            }
                        }
                        continue;
                }
                if (entInfo != null)
                {
                    var interestingEnts = relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid || relation == Ent.SmallEnemyGrid || relation == Ent.SmallNobodyGrid || relation == Ent.Shielded;
                    if (ent.Physics != null && ent.Physics.IsMoving)
                    {
                        entChanged = true;
                        sleeping = false;
                    }
                    else if (entInfo.Touched || _count == 0 && interestingEnts && !ent.PositionComp.LocalAABB.Equals(entInfo.Box))
                    {
                        entInfo.Box = ent.PositionComp.LocalAABB;
                        entChanged = true;
                        sleeping = false;
                    }

                    EnablePhysics = true;
                    entInfo.LastTick = Tick;
                    if (Tick600)
                    {
                        if ((relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid) && entInfo.CacheBlockList.Count != (ent as MyCubeGrid).BlocksCount)
                        {
                            entInfo.RefreshTick = Tick;
                            entInfo.CacheBlockList.Clear();
                        }
                    }
                }
                else
                {
                    if (relation == Ent.Other)
                    {
                        var missilePast = -Vector3D.Normalize(ent.Physics.LinearVelocity) * 6;
                        var missileTestLoc = ent.PositionComp.WorldVolume.Center + missilePast;
                        var centerStep = -Vector3D.Normalize(missileTestLoc - DetectionCenter) * 2f;
                        var counterDrift = centerStep + missileTestLoc;
                        if (CustomCollision.PointInShield(counterDrift, DetectMatrixOutsideInv))
                        {
                            FriendlyMissileCache.Add(ent);
                            continue;
                        }
                    }
                    if ((relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid) && CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv))
                    {
                        FriendlyCache.Add(ent);
                        EntIntersectInfo gridRemoved;
                        WebEnts.TryRemove(ent, out gridRemoved);
                        continue;
                    }
                    entChanged = true;
                    EnablePhysics = true;
                    sleeping = false;
                    WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, Tick, Tick, Tick, relation, new List<IMySlimBlock>()));
                }
            }

            if (sleeping)
            {
                if (!WebSuspend)
                {
                    Session.Instance.SleepingShields.Add(this);
                    //Log.Line($"Tick:{(uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS} - Sleep");
                    WebSuspend = true;
                }
            }
            else
            {
                if (WebSuspend)
                {
                    Session.Instance.SleepingShields.Remove(this);
                    WebSuspend = false;
                    Log.Line($"Tick:{(uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS} - Awoke");
                }
            }

            if (!EnablePhysics) return;

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (!ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutside);
                if (!disableVoxels) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }
            if (ShieldComp.GridIsMoving || entChanged) MyAPIGateway.Parallel.Start(WebDispatch);
        }

        public void WebDispatch()
        {
            foreach (var webent in WebEnts.Keys)
            {
                var entCenter = webent.PositionComp.WorldVolume.Center;
                var entInfo = WebEnts[webent];
                if (entInfo.LastTick != Tick) continue;
                if (entInfo.RefreshTick == Tick && (WebEnts[webent].Relation == Ent.LargeNobodyGrid || WebEnts[webent].Relation == Ent.LargeEnemyGrid))
                    (webent as IMyCubeGrid)?.GetBlocks(WebEnts[webent].CacheBlockList, CollectCollidableBlocks);
                switch (WebEnts[webent].Relation)
                {
                    case Ent.EnemyPlayer:
                        {
                            if ((_count == 2 || _count == 17 || _count == 32 || _count == 47) && CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                            {
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent EnemyPlayer: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => PlayerIntersect(webent));
                            }
                            continue;
                        }
                    case Ent.SmallNobodyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                            continue;
                        }
                    case Ent.LargeNobodyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeNobodyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                            continue;
                        }
                    case Ent.SmallEnemyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                            continue;
                        }
                    case Ent.LargeEnemyGrid:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeEnemyGrid: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                            continue;
                        }
                    case Ent.Shielded:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent Shielded: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent));
                            continue;
                        }
                    case Ent.Other:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent Other: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            if (webent.MarkedForClose || !webent.InScene || webent.Closed) continue;
                            var meteor = webent as IMyMeteor;
                            if (meteor != null)
                            {
                                if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv)) _meteorDmg.Enqueue(meteor);
                            }
                            else
                            {
                                var predictedHit = CustomCollision.MissileIntersect(this, webent, DetectionMatrix, DetectMatrixOutsideInv);
                                if (predictedHit != null) _missileDmg.Enqueue(webent);
                            }
                            continue;
                        }
                    case Ent.VoxelBase:
                        {
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent VoxelBase: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                            MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as MyVoxelBase));
                            continue;
                        }
                    default:
                        if (Session.Enforced.Debug >= 2) Log.Line($"Ent default: {webent.DebugName} - ShieldId [{Shield.EntityId}]");
                        continue;
                }
            }
        }
        #endregion

        #region Gather Entity Information
        public enum Ent
        {
            Unknown,
            Ignore,
            Friend,
            EnemyPlayer,
            SmallNobodyGrid,
            LargeNobodyGrid,
            SmallEnemyGrid,
            LargeEnemyGrid,
            Shielded,
            Other,
            VoxelBase,
            Weapon,
            Authenticated
        };

        public Ent EntType(MyEntity ent)
        {
            if (ent == null) return Ent.Ignore;
            var voxel = ent as MyVoxelBase;
            if (voxel != null && (Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels || !GridIsMobile)) return Ent.Ignore;

            var player = ent as IMyCharacter;
            if (player != null)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = MyCube.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return Ent.Friend;
                return player.IsDead ? Ent.Ignore : Ent.EnemyPlayer;
            }
            var grid = ent as MyCubeGrid;
            if (grid != null)
            {
                if (ShieldComp.Modulator != null && ShieldComp.Modulator.ModSet.Settings.ModulateGrids || Session.Enforced.DisableGridDamageSupport == 1) return Ent.Ignore;

                ModulatorGridComponent modComp;
                grid.Components.TryGet(out modComp);
                if (!string.IsNullOrEmpty(modComp?.ModulationPassword) && modComp.ModulationPassword == Shield.CustomData)
                {
                    foreach (var subGrid in modComp.GetSubGrids)
                    {
                        if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                        {
                            if (CustomCollision.NotAllCornersInShield(grid, DetectMatrixOutsideInv) > 0) FriendlyCache.Add(subGrid);
                            else AuthenticatedCache.Add(subGrid);
                        }
                        else AuthenticatedCache.Add(subGrid);
                    }
                    return Ent.Authenticated;
                }
                var bigOwners = grid.BigOwners.Count;
                var blockCnt = grid.BlocksCount;
                if (blockCnt < 10 && bigOwners == 0) return Ent.SmallNobodyGrid;
                if (bigOwners == 0) return Ent.LargeNobodyGrid;
                var enemy = GridEnemy(grid);

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null && shieldComponent.DefenseShields.WasOnline)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var shieldEntity = MyCube.Parent;
                    if (!enemy) return Ent.Friend;
                    dsComp.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;    
                }
                return enemy ? Ent.LargeEnemyGrid : Ent.Friend;
            }

            if (ent is IMyMeteor || ent.DefinitionId.HasValue && ent.DefinitionId.Value.TypeId == MissileObj) return Ent.Other;
            if (voxel != null && GridIsMobile) return Ent.VoxelBase;
            return 0;
        }

        public bool GridEnemy(MyCubeGrid grid)
        {
            var owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = MyCube.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }

        public bool GridEnemy2(MyCubeGrid grid)
        {
            var smallOwners = grid.SmallOwners;
            var ownerCount = smallOwners.Count;
            if (ownerCount == 0) return true;
            var friend = false;
            if (ownerCount == 1)
            {
                var relation = MyCube.GetUserRelationToOwner(smallOwners[0]);
                if (relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare) friend = true;
            }
            else
            {
                foreach (var s in smallOwners)
                {
                    var relation = MyCube.GetUserRelationToOwner(s);
                    if (relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        friend = true;
                        break;
                    }
                }
            }
            return !friend;
        }

        private static bool CollectCollidableBlocks(IMySlimBlock mySlimBlock)
        {
            return mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_TextPanel)
                   && mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_ButtonPanel)
                   && mySlimBlock.BlockDefinition.Id.SubtypeId != MyStringHash.TryGet("SmallLight");
        }
        #endregion
    }
}

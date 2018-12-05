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
            foreach (var sub in ShieldComp.GetSubGrids)
            {
                MyProtectors myProtectors;
                Session.GlobalProtect.TryGetValue(sub, out myProtectors);
                if (myProtectors.Shields != null && myProtectors.Shields.ContainsKey(this)) continue;
                var tick = Session.Tick;
                WebEnts.TryAdd(sub, new EntIntersectInfo(sub.EntityId, 0f, 0f, false, sub.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, tick, tick, tick, tick, Ent.Protected, null));
                var protectors = Session.GlobalProtect[sub] = new MyProtectors(Session.ProtDicts.Get(), LogicSlot, Session.Tick);
                if (!GridIsMobile)
                {
                    var cornersInShield = CustomCollision.NotAllCornersInShield(sub, DetectMatrixOutsideInv);
                    switch (cornersInShield)
                    {
                        case 8:
                            protectors.Shields.Add(this, new ProtectorInfo(true, true), true);
                            break;
                        default:
                            protectors.Shields.Add(this, new ProtectorInfo(true, false), true);
                            break;
                    }
                }
                protectors.Shields.Add(this, new ProtectorInfo(true, true), true);
            }
        }

        private readonly Vector3D[] _resetEntCorners = new Vector3D[8];
        public bool ResetEnts(MyEntity ent, uint tick)
        {
            if (!ent.InScene) return false;
            MyProtectors protectors;
            Session.GlobalProtect.TryGetValue(ent, out protectors);
            if (protectors.Shields == null) protectors = Session.GlobalProtect[ent] = new MyProtectors(Session.ProtDicts.Get(), LogicSlot, tick);

            var grid = ent as MyCubeGrid;
            var parent = ShieldComp.GetLinkedGrids.Contains(grid);
            if (grid != null)
            {
                var cornersInShield = CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners);

                switch (cornersInShield)
                {
                    case 0:
                        return false;
                    case 8:
                        protectors.Shields.Add(this, new ProtectorInfo(parent, true));
                        break;
                    default:
                        protectors.Shields.Add(this, new ProtectorInfo(parent, false));
                        break;
                }
                return true;
            }

            if (!CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv)) return false;
            protectors.Shields.Add(this, new ProtectorInfo(parent, true));
            return true;
        }

        public void WebEntities()
        {
            PruneList.Clear();
            MyGamePruningStructure.GetTopMostEntitiesInBox(ref ShieldWorldAabb, PruneList);
            foreach (var eShield in EnemyShields) PruneList.Add(eShield);
            if (Missiles.Count > 0)
            {
                var missileSphere = WebSphere;
                missileSphere.Radius = BoundingRange + 50;
                foreach (var missile in Missiles)
                    if (missile.InScene && !missile.MarkedForClose && missileSphere.Intersects(missile.PositionComp.WorldVolume)) PruneList.Add(missile);
            }

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
            var entChanged = false;
            var tick = Session.Tick;

            EnablePhysics = false;
            for (int i = 0; i < PruneList.Count; i++)
            {
                var ent = PruneList[i];
                var voxel = ent as MyVoxelBase;
                if (ent == null || ent.MarkedForClose || voxel == null && (ent.Physics == null || ent.DefinitionId == null) || !GridIsMobile && voxel != null || disableVoxels && voxel != null || voxel != null && voxel != voxel.RootVoxel) continue;
                if (ent is IMyFloatingObject || ent is IMyEngineerToolBase || IgnoreCache.Contains(ent) || FriendlyMissileCache.Contains(ent) || AuthenticatedCache.Contains(ent)) continue;
                EntIntersectInfo entInfo;
                WebEnts.TryGetValue(ent, out entInfo);
                Ent relation;

                bool refreshInfo = false;
                if (entInfo != null)
                {
                    var last = entInfo.LastTick;
                    var refresh = entInfo.RefreshTick;
                    refreshInfo = tick - last > 180 || tick - last == 180 && tick - refresh >= 3600 || tick - last == 1 && tick - refresh >= 60;
                    if (refreshInfo)
                    {
                        entInfo.RefreshTick = tick;
                        entInfo.Relation = EntType(ent);
                    }
                    relation = entInfo.Relation;
                    entInfo.LastTick = tick;
                }
                else relation = EntType(ent);

                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Protected:
                        if (relation == Ent.Protected)
                        {
                            if (entInfo != null)
                            {
                                if (Session.GlobalProtect.ContainsKey(ent)) continue;
                            }
                            else WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, tick, tick ,tick, tick, relation, null));
                            MyProtectors protectors;
                            Session.GlobalProtect.TryGetValue(ent, out protectors);
                            if (protectors.Shields == null) protectors = Session.GlobalProtect[ent] = new MyProtectors(Session.ProtDicts.Get(), LogicSlot, tick);

                            var grid = ent as MyCubeGrid;
                            var parent = ShieldComp.GetLinkedGrids.Contains(grid);
                            if (grid != null)
                            {
                                var cornersInShield = CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners);
                                switch (cornersInShield)
                                {
                                    case 0:
                                        continue;
                                    case 8:
                                        protectors.Shields.Add(this, new ProtectorInfo(parent, true), true);
                                        break;
                                    default:
                                        protectors.Shields.Add(this, new ProtectorInfo(parent, false), true);
                                        break;
                                }
                            }
                            else if (CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv)) protectors.Shields.Add(this, new ProtectorInfo(parent, true), true);
                            continue;
                        }
                        IgnoreCache.Add(ent);
                        continue;
                }
                if (entInfo != null)
                {
                    var interestingEnts = relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid || relation == Ent.SmallEnemyGrid || relation == Ent.SmallNobodyGrid || relation == Ent.Shielded;
                    if (ent.Physics != null && ent.Physics.IsMoving) entChanged = true;
                    else if (entInfo.Touched || refreshInfo && interestingEnts && !ent.PositionComp.LocalAABB.Equals(entInfo.Box))
                    {
                        entInfo.RefreshTick = tick;
                        entInfo.Box = ent.PositionComp.LocalAABB;
                        entChanged = true;
                    }

                    EnablePhysics = true;
                    if (refreshInfo)
                    {
                        if ((relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid) && entInfo.CacheBlockList.Count != (ent as MyCubeGrid).BlocksCount)
                        {
                            entInfo.BlockUpdateTick = tick;
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
                    entChanged = true;
                    EnablePhysics = true;
                    WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, tick, tick, tick, tick, relation, new List<IMySlimBlock>()));
                }
            }
            if (!EnablePhysics)
            {
                Asleep = true;
                return;
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (!ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutside);
                if (!disableVoxels) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (ShieldComp.GridIsMoving || entChanged)
            {
                Asleep = false;
                LastWokenTick = tick;
                MyAPIGateway.Parallel.Start(WebDispatch);
            }
        }

        public void WebDispatch()
        {
            var tick = Session.Tick;
            foreach (var webent in WebEnts.Keys)
            {
                var entCenter = webent.PositionComp.WorldVolume.Center;
                var entInfo = WebEnts[webent];
                if (entInfo?.LastTick != tick) continue;
                if (entInfo.BlockUpdateTick == tick && (entInfo.Relation == Ent.LargeNobodyGrid || entInfo.Relation == Ent.LargeEnemyGrid))
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
            Protected,
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
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return Ent.Protected;
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
                            if (CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners) > 0) return Ent.Protected;
                            AuthenticatedCache.Add(subGrid);
                        }
                        else AuthenticatedCache.Add(subGrid);
                    }
                    return Ent.Authenticated;
                }
                var bigOwners = grid.BigOwners;
                var bigOwnersCnt = bigOwners.Count;
                var blockCnt = grid.BlocksCount;
                if (blockCnt < 10 && bigOwnersCnt == 0) return CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv) ? Ent.Protected : Ent.SmallNobodyGrid;
                if (bigOwnersCnt == 0) return CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv) ? Ent.Protected : Ent.LargeNobodyGrid;
                var enemy = GridEnemy(grid, bigOwners);

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null && shieldComponent.DefenseShields.WasOnline)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var shieldEntity = MyCube.Parent;
                    if (!enemy) return Ent.Protected;
                    dsComp.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;    
                }
                return enemy ? Ent.LargeEnemyGrid : Ent.Protected;
            }

            if (ent is IMyMeteor || ent.DefinitionId.HasValue && ent.DefinitionId.Value.TypeId == MissileObj) return Ent.Other;
            if (voxel != null && GridIsMobile) return Ent.VoxelBase;
            return 0;
        }

        public bool GridEnemy(MyCubeGrid grid, List<long> owners = null)
        {
            if (owners == null) owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = MyCube.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
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

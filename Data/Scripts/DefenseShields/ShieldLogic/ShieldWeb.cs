using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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
                ProtectedEntCache[sub] = new ProtectCache(tick, tick, tick, Ent.Protected);
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
            MyGamePruningStructure.GetTopMostEntitiesInBox(ref WebBox, PruneList);
            foreach (var eShield in EnemyShields) PruneList.Add(eShield);
            if (Missiles.Count > 0)
            {
                var missileBox = WebBox;
                foreach (var missile in Missiles)
                    if (missile.InScene && !missile.MarkedForClose && missileBox.Intersects(missile.PositionComp.WorldAABB)) PruneList.Add(missile);
            }

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
            var voxelFound = false;
            var shieldFound = false;
            var entChanged = false;
            var tick = Session.Tick;

            EnablePhysics = false;
            for (int i = 0; i < PruneList.Count; i++)
            {
                var ent = PruneList[i];
                var voxel = ent as MyVoxelBase;
                if (ent == null || ent.MarkedForClose || voxel == null && (ent.Physics == null || ent.DefinitionId == null) || !GridIsMobile && voxel != null || disableVoxels && voxel != null || voxel != null && voxel != voxel.RootVoxel) continue;
                bool quickReject;
                if (_isServer) quickReject = ent is IMyFloatingObject || ent is IMyEngineerToolBase || IgnoreCache.Contains(ent) || FriendlyMissileCache.Contains(ent) || AuthenticatedCache.Contains(ent);
                else quickReject = !(ent is MyCubeGrid) && voxel == null && !(ent is IMyCharacter) || IgnoreCache.Contains(ent) || AuthenticatedCache.Contains(ent);
                if (quickReject || !WebSphere.Intersects(ent.PositionComp.WorldVolume)) continue;
                if (voxel != null)
                {
                    VoxelsToIntersect[voxel] = true;
                    voxelFound = true;
                    entChanged = true;
                    EnablePhysics = true;
                    continue;
                }
                Ent relation;

                ProtectCache protectedEnt;
                EntIntersectInfo entInfo = null;
                ProtectedEntCache.TryGetValue(ent, out protectedEnt);

                var refreshInfo = false;
                if (protectedEnt == null)
                {
                    WebEnts.TryGetValue(ent, out entInfo);
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
                }
                else
                {
                    var last = protectedEnt.LastTick;
                    var refresh = protectedEnt.RefreshTick;
                    refreshInfo = tick - last > 180 || tick - last == 180 && tick - refresh >= 3600 || tick - last == 1 && tick - refresh >= 60;
                    if (refreshInfo)
                    {
                        protectedEnt.RefreshTick = tick;
                        protectedEnt.Relation = EntType(ent);
                    }
                    relation = protectedEnt.Relation;
                    protectedEnt.LastTick = tick;
                }
                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Protected:
                        if (relation == Ent.Protected)
                        {
                            if (protectedEnt != null)
                            {
                                if (Session.GlobalProtect.ContainsKey(ent)) continue;
                            }
                            else ProtectedEntCache[ent] = new ProtectCache(tick, tick, tick, Ent.Protected);

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
                if (relation == Ent.Shielded) shieldFound = true;
                try
                {
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
                            if ((relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid) && entInfo.CacheBlockList != null && entInfo.CacheBlockList.Count != (ent as MyCubeGrid).BlocksCount)
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
                    //if (entInfo == null) WebEnts.TryGetValue(ent, out entInfo);
                }
                catch (Exception ex) { Log.Line($"Exception in WebEntities entInfo: {ex}"); }
            }
            if (!EnablePhysics)
            {
                if (_isServer) Asleep = true;
                return;
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (!ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                if (shieldFound) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutside);
                if (voxelFound) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (ShieldComp.GridIsMoving || entChanged)
            {
                Asleep = false;
                LastWokenTick = tick;
                if (!Dispatched) MyAPIGateway.Parallel.Start(WebDispatch, DispatchDone);
                Dispatched = true;
            }
        }

        private void DispatchDone()
        {
            Dispatched = false;
        }

        public void WebDispatch()
        {
            if (VoxelsToIntersect.Count > 0) MyAPIGateway.Parallel.Start(VoxelIntersect);
            MyAPIGateway.Parallel.ForEach(WebEnts, pair =>
            {
                var relation = pair.Value.Relation;
                if (relation == Ent.Protected || relation == Ent.Authenticated) return;
                EntIntersectSelector(pair);
            });
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
        #endregion
    }
}

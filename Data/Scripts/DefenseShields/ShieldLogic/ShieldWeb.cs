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
        private readonly Vector3D[] _resetEntCorners = new Vector3D[8];
        public bool ResetEnts(MyEntity ent, uint tick)
        {
            MyProtectors protectors;
            Session.Instance.GlobalProtect.TryGetValue(ent, out protectors);
            if (protectors.Shields == null) protectors = Session.Instance.GlobalProtect[ent] = new MyProtectors(Session.ProtSets.Get(), LogicSlot, tick);

            var grid = ent as MyCubeGrid;
            if (grid != null)
            {
                if (CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners, true) == 0) return false;

                protectors.Shields.Add(this);
                protectors.Shields.ApplyAdditions();
                return true;
            }

            if (!CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv)) return false;
            protectors.Shields.Add(this);
            protectors.Shields.ApplyAdditions();
            return true;
        }

        private bool _needPhysics;
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
            var tick = Session.Instance.Tick;

            _enablePhysics = false;
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
                    _enablePhysics = true;
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
                        protectedEnt.PreviousRelation = protectedEnt.Relation;
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
                    case Ent.Friendly:
                    case Ent.Protected:
                        if (relation == Ent.Protected)
                        {
                            if (protectedEnt == null) ProtectedEntCache[ent] = new ProtectCache(tick, tick, tick, relation, relation);
                            MyProtectors protectors;
                            Session.Instance.GlobalProtect.TryGetValue(ent, out protectors);
                            if (protectors.Shields == null) protectors = Session.Instance.GlobalProtect[ent] = new MyProtectors(Session.ProtSets.Get(), LogicSlot, tick);
                            if (protectors.Shields.Contains(this)) continue;

                            protectors.Shields.Add(this);
                            protectors.Shields.ApplyAdditions();
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

                        _enablePhysics = true;
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
                        _enablePhysics = true;
                        WebEnts.TryAdd(ent, new EntIntersectInfo(ent.EntityId, 0f, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, Vector3D.NegativeInfinity, tick, tick, tick, tick, relation, new List<IMySlimBlock>()));
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in WebEntities entInfo: {ex}"); }
            }
            if (!_enablePhysics)
            {
                if (_isServer) Asleep = true;
                return;
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (_needPhysics && shieldFound || !ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                if (shieldFound)
                {
                    _needPhysics = false;
                    Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutside);
                }
                else _needPhysics = true;
                if (voxelFound) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (ShieldComp.GridIsMoving || entChanged)
            {
                Asleep = false;
                LastWokenTick = tick;
                if (!Dispatched) MyAPIGateway.Parallel.Start(WebDispatch, DispatchDone);
                Session.Instance.ThreadPeak++;
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
            MyAPIGateway.Parallel.ForEach(WebEnts, EntIntersectSelector);
        }
        #endregion

        #region Gather Entity Information
        public enum Ent
        {
            Unknown,
            Ignore,
            Protected,
            Friendly,
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

            var character = ent as IMyCharacter;
            if (character != null)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = MyCube.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare)
                {
                    var playerInShield = CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv);
                    if (playerInShield)
                    {
                        return Ent.Protected;
                    }
                    return Ent.Friendly;
                }
                return character.IsDead ? Ent.Ignore : Ent.EnemyPlayer;
            }
            var grid = ent as MyCubeGrid;
            if (grid != null)
            {
                ModulateGrids = ShieldComp.Modulator != null && ShieldComp.Modulator.ModSet.Settings.ModulateGrids || Session.Enforced.DisableGridDamageSupport == 1;
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
                if (CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv)) return Ent.Protected;
                if (!ModulateGrids && blockCnt < 10 && bigOwnersCnt == 0) return  Ent.SmallNobodyGrid;
                if (!ModulateGrids && bigOwnersCnt == 0) return Ent.LargeNobodyGrid;
                var enemy = !ModulateGrids && GridEnemy(grid, bigOwners);
                if (!enemy)
                {
                    if (ShieldComp.GetSubGrids.Contains(grid)) return Ent.Protected;
                    var pointsInShield = CustomCollision.ObbPointsInShield(grid, DetectMatrixOutsideInv);
                    return pointsInShield > 0 ? Ent.Protected : Ent.Friendly;
                }

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null && shieldComponent.DefenseShields.WasOnline)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var shieldEntity = MyCube.Parent;
                    dsComp.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;    
                }
                return Ent.LargeEnemyGrid;
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

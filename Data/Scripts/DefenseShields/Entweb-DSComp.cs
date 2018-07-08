using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Web Entities
        private void WebEntities()
        {
            if (Session.Enforced.Debug == 1) Dsutil2.Sw.Restart();
            var pruneSphere = new BoundingSphereD(DetectionCenter, ShieldComp.BoundingRange);
            var pruneList = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);
            foreach (var eShield in EnemyShields) pruneList.Add(eShield);

            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                if (ent == null || FriendlyCache.Contains(ent) || IgnoreCache.Contains(ent) || PartlyProtectedCache.Contains(ent) || AuthenticatedCache.Contains(ent)) continue;

                var entCenter = ent.PositionComp.WorldVolume.Center;
                if (ent.Physics == null && !(ent is IMyAutomaticRifleGun) || ent.MarkedForClose || ent is MyVoxelBase && !GridIsMobile
                    || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || ent.GetType().Name == MyDebrisBase) continue;

                var relation = EntType(ent);
                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Friend:
                    case Ent.Weapon:
                        if (relation == Ent.Friend || relation == Ent.Weapon)
                        {
                            if (ent is MyCubeGrid)
                            {
                                var cornersInShield = CustomCollision.CornersInShield(ent as MyCubeGrid, DetectMatrixOutsideInv);
                                if (cornersInShield > 0 && cornersInShield != 8)
                                {
                                    PartlyProtectedCache.Add(ent);
                                    continue;
                                }
                            }
                            if (CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                            {
                                FriendlyCache.Add(ent);
                                continue;
                            }
                            IgnoreCache.Add(ent);
                        }
                        continue;
                }
                _enablePhysics = true;
                lock (WebEnts)
                {
                    EntIntersectInfo entInfo;
                    WebEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
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
                        WebEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, Vector3D.NegativeInfinity, _tick, _tick, relation, new List<IMySlimBlock>(), new MyStorageData()));
                    }
                }
            }
            if (_enablePhysics || ShieldComp.GridIsMoving || _shapeAdjusted)
            {
                Icosphere.ReturnPhysicsVerts(_detectMatrixOutside, ShieldComp.PhysicsOutside);
                Icosphere.ReturnPhysicsVerts(_detectMatrixOutside, ShieldComp.PhysicsOutsideLow);
                Icosphere.ReturnPhysicsVerts(_detectMatrixInside, ShieldComp.PhysicsInside);
            }
            if (_enablePhysics) MyAPIGateway.Parallel.Start(WebDispatch);

            if (Session.Enforced.Debug == 1) Dsutil2.StopWatchReport($"ShieldId:{Shield.EntityId.ToString()} - Web", 3);
        }

        private void WebDispatch()
        {
            if (Session.Enforced.Debug == 1) Dsutil3.Sw.Restart();
            var ep = 0;
            var ns = 0;
            var nl = 0;
            var es = 0;
            var el = 0;
            var ss = 0;
            var oo = 0;
            var vv = 0;
            var xx = 0;
            lock (WebEnts)
            {
                foreach (var webent in WebEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = WebEnts[webent];
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (WebEnts[webent].Relation == Ent.LargeNobodyGrid || WebEnts[webent].Relation == Ent.LargeEnemyGrid))
                        ((IMyCubeGrid)webent).GetBlocks(WebEnts[webent].CacheBlockList, CollectCollidableBlocks);
                    switch (WebEnts[webent].Relation)
                    {
                        case Ent.EnemyPlayer:
                            {
                                ep++;
                                if ((_count == 2 || _count == 17 || _count == 32 || _count == 47) && CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                                {
                                    if (Session.Enforced.Debug == 1) Log.Line($"Ent: EnemyPlayer {((MyEntity)webent).DebugName}");
                                    MyAPIGateway.Parallel.Start(() => PlayerIntersect(webent));
                                }
                                continue;
                            }
                        case Ent.SmallNobodyGrid:
                            {
                                ns++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: SmallNobodyGrid {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeNobodyGrid:
                            {
                                nl++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: LargeNobodyGrid {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.SmallEnemyGrid:
                            {
                                es++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: SmallEnemyGrid {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeEnemyGrid:
                            {
                                el++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: LargeEnemyGrid {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.Shielded:
                            {
                                ss++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: Shielded {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent as IMyCubeGrid));
                                continue;
                            }
                        case Ent.Other:
                            {
                                oo++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: Other {((MyEntity)webent).DebugName}");
                                if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                                {
                                    if (webent.MarkedForClose || webent.Closed) continue;
                                    if (webent is IMyMeteor) _meteorDmg.Enqueue(webent as IMyMeteor);
                                    else _missileDmg.Enqueue(webent);
                                }
                                continue;
                            }
                        case Ent.VoxelBase:
                            {
                                vv++;
                                if (Session.Enforced.Debug == 1) Log.Line($"Ent: VoxelBase {((MyEntity)webent).DebugName}");
                                MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as MyVoxelBase));
                                continue;
                            }
                        default:
                            if (Session.Enforced.Debug == 1) Log.Line($"Ent: default {((MyEntity)webent).DebugName}");
                            xx++;
                            continue;
                    }
                }
            }

            if (Session.Enforced.Debug == 1 && _lCount == 5 && _count == 5)
                lock (WebEnts) if (WebEnts.Count > 7 || FriendlyCache.Count > 15 || IgnoreCache.Count > 15) Log.Line($"ShieldId:{Shield.EntityId.ToString()} - friend:{FriendlyCache.Count} - ignore:{IgnoreCache.Count} - total:{WebEnts.Count} ep:{ep} ns:{ns} nl:{nl} es:{es} el:{el} ss:{ss} oo:{oo} vv:{vv} xx:{xx}");
            if (Session.Enforced.Debug == 1) Dsutil3.StopWatchReport($"ShieldId:{Shield.EntityId.ToString()} - webDispatch", 3);
        }
        #endregion

        #region Gather Entity Information
        public enum Ent
        {
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

        private Ent EntType(IMyEntity ent)
        {
            if (ent == null) return Ent.Ignore;
            if (ent is MyVoxelBase && (Session.Enforced.DisableVoxelSupport == 1 || ModulateVoxels || !GridIsMobile)) return Ent.Ignore;
            if (ent is IMyAutomaticRifleGun) return Ent.Weapon;

            if (ent is IMyCharacter)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = Shield.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return Ent.Friend;
                return (ent as IMyCharacter).IsDead ? Ent.Ignore : Ent.EnemyPlayer;
            }
            if (ent is IMyCubeGrid)
            {
                if (ModulateGrids) return Ent.Ignore;

                var grid = ent as IMyCubeGrid;
                ModulatorGridComponent modComp;
                grid.Components.TryGet(out modComp);
                if (modComp?.ModulationPassword != null)
                {
                    if (modComp.ModulationPassword.Equals(Shield.CustomData))
                    {
                        foreach (var subGrid in modComp.GetSubGrids)
                        {
                            var cornersInShield = CustomCollision.CornersInShield(subGrid, DetectMatrixOutsideInv);
                            if (cornersInShield > 0 && cornersInShield != 8)
                            {
                                PartlyProtectedCache.Add(ent);
                            }
                            else if (cornersInShield == 8)
                            {
                                FriendlyCache.Add(ent);
                            }
                            else AuthenticatedCache.Add(subGrid);
                        }
                        return Ent.Authenticated;
                    }
                }

                if (((MyCubeGrid)grid).BlocksCount < 3 && grid.BigOwners.Count == 0) return Ent.SmallNobodyGrid;
                if (grid.BigOwners.Count <= 0) return Ent.LargeNobodyGrid;

                var enemy = GridEnemy(grid);
                if (enemy && ((MyCubeGrid)grid).BlocksCount < 3) return Ent.SmallEnemyGrid;

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var shieldEntity = (MyEntity)Shield.Parent;
                    if (!enemy) return Ent.Friend;
                    if (!dsComp.ShieldComp.ShieldActive)
                    {
                        lock (WebEnts) if (WebEnts.Remove(ent))
                        return Ent.LargeEnemyGrid;
                    }
                    dsComp.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;
                }

                return enemy ? Ent.LargeEnemyGrid : Ent.Friend;
            }

            if (ent is IMyMeteor || ent.GetType().Name.StartsWith(MyMissile)) return Ent.Other;
            if (ent is MyVoxelBase && GridIsMobile) return Ent.VoxelBase;
            return 0;
        }

        private bool GridEnemy(IMyCubeGrid grid)
        {
            var owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = Shield.GetUserRelationToOwner(owners[0]);
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
            if (ammoInfo.BackKickForce < 0 && dmgMulti.Equals(0)) damage = float.NegativeInfinity;
            else if (ammoInfo.Explosive) damage = (ammoInfo.Damage * (ammoInfo.Radius * 0.5f)) * 7.5f;
            else damage = ammoInfo.Mass * ammoInfo.Speed;

            if (ammoInfo.Mass < 0 && ammoInfo.Radius <= 0) damage = -damage;
            return damage;
        }
        #endregion
    }
}

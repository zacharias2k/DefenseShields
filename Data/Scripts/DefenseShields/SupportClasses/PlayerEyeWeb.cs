using System.Collections.Concurrent;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace DefenseSystems.Support
{
    public interface ITurretThreadHits
    {
        void Execute();
    }

    public class TurretGridEvent : ITurretThreadHits
    {
        public readonly IMySlimBlock Block;
        public readonly List<LineD> Beams;
        public readonly float Damage;
        public readonly long AttackerId;

        public TurretGridEvent(IMySlimBlock block, float damage, long attackerId, List<LineD> beams)
        {
            Block = block;
            Damage = damage;
            AttackerId = attackerId;
            Beams = beams;
        }

        public void Execute()
        {
            // damage block, apply force, send lines for draw.
        }
    }

    public class TurretDestroyableEvent : ITurretThreadHits
    {
        public void Execute()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TurretGVoxelEvent : ITurretThreadHits
    {
        public void Execute()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TurretThreading
    {
        public enum TurretType
        {
            Pulse,
            Constant
        }

        private readonly List<MyLineSegmentOverlapResult<MyEntity>> _overlapResults = new List<MyLineSegmentOverlapResult<MyEntity>>();
        private readonly Work _work = new Work();
        internal Dictionary<MyEntity, TurretWeb> HitEntities = new Dictionary<MyEntity, TurretWeb>();
        internal readonly ConcurrentQueue<FiredTurret> FiredTurrets = new ConcurrentQueue<FiredTurret>();
        internal readonly ConcurrentQueue<ITurretThreadHits> TurretHits = new ConcurrentQueue<ITurretThreadHits>();
        internal readonly Pool<List<LineD>> Beams = new Pool<List<LineD>>();
        internal readonly Pool<Dictionary<long, CheckBeam>> CheckBeams = new Pool<Dictionary<long, CheckBeam>>();

        internal void WebEnts()
        {
            while (FiredTurrets.TryDequeue(out _work.Turret))
            {
                MyAPIGateway.Parallel.For(0, _work.Turret.Beams.Count, x =>
                {
                    var beam = _work.Turret.Beams[x];
                    _overlapResults.Clear();
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, _overlapResults);
                    for (int i = 0; i < _overlapResults.Count; i++)
                    {
                        var result = _overlapResults[i];
                        var ent = result.Element;
                        var grid = ent as MyCubeGrid;
                        var voxel = ent as MyVoxelBase;
                        var destroyable = ent as IMyDestroyableObject;
                        if (grid != null)
                        {
                            if (!HitEntities.ContainsKey(ent))
                            {
                                HitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Grid, grid, null, null, CheckBeams.Get(null)));
                            }

                            TurretWeb turretWeb;
                            if (HitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, Beams.Get(null)));
                        }
                        else if (destroyable != null)
                        {
                            if (!HitEntities.ContainsKey(ent))
                                HitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Destroyable, null, destroyable, null, CheckBeams.Get(null)));

                            TurretWeb turretWeb;
                            if (HitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, Beams.Get(null)));
                        }
                        else if (voxel != null)
                        {
                            if (!HitEntities.ContainsKey(ent))
                                HitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Voxel, null,null, voxel, CheckBeams.Get(null)));

                            TurretWeb turretWeb;
                            if (HitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, Beams.Get(null)));
                        }
                    }
                });
            }

            foreach (var pair in HitEntities)
            {
                var web = pair.Value;
                if (web.Target == TurretWeb.TargetType.Grid)
                {
                    var grid = web.Grid;
                    for (int i = 0; i < web.Turret.Count; i++)
                    {
                        foreach (var turret in web.Turret)
                        {
                            var beams = turret.Value.Beams;
                            var beamCnt = beams.Count;
                            var beamType = turret.Value.TurretType;
                            var damage = beamType == TurretType.Constant ? 100 : 1000;

                            int hits = 0;
                            IMySlimBlock hitBlock = null;

                            for (int j = 0; j < beamCnt; j++)
                            {
                                var beam = beams[j];
                                double distanceToHit;

                                if (grid.GetLineIntersectionExactAll(ref beam, out distanceToHit, out hitBlock) != null)
                                {
                                    hits++;
                                }
                            }
                            if (hits > 0) TurretHits.Enqueue(new TurretGridEvent(hitBlock, damage * hits, turret.Key, beams));
                        }
                    }
                }
            }
        }

        internal class Work
        {
            internal TurretThreading.FiredTurret Turret;
        }

        public struct TurretWeb
        {
            public enum TargetType
            {
                Grid,
                Destroyable,
                Voxel,
            }

            public readonly TargetType Target;
            public readonly IMyCubeGrid Grid;
            public readonly IMyDestroyableObject Destroyable;
            public readonly MyVoxelBase Voxel;
            public readonly Dictionary<long, CheckBeam> Turret;

            public TurretWeb(TargetType target, MyCubeGrid grid, IMyDestroyableObject destroyable, MyVoxelBase voxel, Dictionary<long, CheckBeam> turret)
            {
                Target = target;
                Grid = grid;
                Destroyable = destroyable;
                Voxel = voxel;
                Turret = turret;
            }
        }

        public struct CheckBeam
        {
            public readonly TurretType TurretType;
            public readonly List<LineD> Beams;

            public CheckBeam(TurretType turretType, List<LineD> beams)
            {
                TurretType = turretType;
                Beams = beams;
            }
        }

        public struct FiredTurret
        {
            public readonly CachingList<LineD> Beams;
            public readonly long TurretId;
            public readonly TurretType TurretType;

            public FiredTurret(long turretId, TurretType turretType, CachingList<LineD> beams)
            {
                TurretId = turretId;
                TurretType = turretType;
                Beams = beams;
            }
        }
    }
}

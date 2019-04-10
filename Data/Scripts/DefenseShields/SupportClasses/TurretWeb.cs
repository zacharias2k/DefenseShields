using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
        public readonly float Damage;
        public readonly long AttackerId;

        public TurretGridEvent(IMySlimBlock block, float damage, long attackerId)
        {
            Block = block;
            Damage = damage;
            AttackerId = attackerId;
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
        private readonly Dictionary<MyEntity, TurretWeb> _hitEntities = new Dictionary<MyEntity, TurretWeb>();
        private readonly MyConcurrentPool<List<LineD>> _beams = new MyConcurrentPool<List<LineD>>();
        private readonly MyConcurrentPool<Dictionary<long, CheckBeam>> _checkBeams = new MyConcurrentPool<Dictionary<long, CheckBeam>>();

        internal readonly ConcurrentQueue<FiredTurret> FiredTurrets = new ConcurrentQueue<FiredTurret>();
        internal readonly ConcurrentQueue<ITurretThreadHits> TurretHits = new ConcurrentQueue<ITurretThreadHits>();
        internal readonly ConcurrentQueue<UpdateBeams> UpdatedBeams = new ConcurrentQueue<UpdateBeams>();

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
                            if (!_hitEntities.ContainsKey(ent))
                            {
                                _hitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Grid, grid, null, null, _checkBeams.Get()));
                            }

                            TurretWeb turretWeb;
                            if (_hitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                        }
                        else if (destroyable != null)
                        {
                            if (!_hitEntities.ContainsKey(ent))
                                _hitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Destroyable, null, destroyable, null, _checkBeams.Get()));

                            TurretWeb turretWeb;
                            if (_hitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                        }
                        else if (voxel != null)
                        {
                            if (!_hitEntities.ContainsKey(ent))
                                _hitEntities.Add(ent, new TurretWeb(TurretWeb.TargetType.Voxel, null,null, voxel, _checkBeams.Get()));

                            TurretWeb turretWeb;
                            if (_hitEntities.TryGetValue(ent, out turretWeb))
                                turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                        }
                    }
                });
            }

            foreach (var pair in _hitEntities)
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
                                    var from = beam.From;
                                    var to = beam.To;
                                    var newTo = Vector3D.Normalize(from - to) * distanceToHit;
                                    UpdatedBeams.Enqueue(new UpdateBeams(turret.Key, new LineD(from, newTo)));
                                }
                            }
                            _beams.Return(beams);
                            if (hits > 0) TurretHits.Enqueue(new TurretGridEvent(hitBlock, damage * hits, turret.Key));
                        }
                    }
                }
                _checkBeams.Return(web.Turret);
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
            public readonly List<LineD> Beams;
            public readonly long TurretId;
            public readonly TurretType TurretType;

            public FiredTurret(long turretId, TurretType turretType, List<LineD> beams)
            {
                TurretId = turretId;
                TurretType = turretType;
                Beams = beams;
            }
        }

        public struct UpdateBeams
        {
            public readonly LineD Beam;
            public readonly long TurretId;

            public UpdateBeams(long turretId, LineD beam)
            {
                TurretId = turretId;
                Beam = beam;
            }
        }
    }
}

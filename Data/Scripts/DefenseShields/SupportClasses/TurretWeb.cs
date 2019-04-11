using System.Collections.Concurrent;
using System.Collections.Generic;
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
        }
    }

    public class TurretVoxelEvent : ITurretThreadHits
    {
        public void Execute()
        {
        }
    }

    internal class Work
    {
        internal List<List<MyLineSegmentOverlapResult<MyEntity>>> Webbed = new List<List<MyLineSegmentOverlapResult<MyEntity>>>();
        internal TurretThreading.FiredTurret Turret;

        internal void Reset(int turretCount)
        {
            Webbed.Clear();
            Webbed.Capacity = turretCount;
        }
    }

    public class TurretThreading
    {
        private readonly Work _work = new Work();
        private readonly MyConcurrentPool<List<LineD>> _beams = new MyConcurrentPool<List<LineD>>();
        private readonly MyConcurrentPool<Dictionary<long, CheckBeam>> _checkBeams = new MyConcurrentPool<Dictionary<long, CheckBeam>>();
        private readonly ConcurrentDictionary<MyEntity, EntityHit> _hitEntities = new ConcurrentDictionary<MyEntity, EntityHit>();
        internal readonly ConcurrentQueue<FiredTurret> FiredTurrets = new ConcurrentQueue<FiredTurret>();
        internal readonly ConcurrentQueue<ITurretThreadHits> TurretHits = new ConcurrentQueue<ITurretThreadHits>();
        internal readonly ConcurrentQueue<UpdateBeams> UpdatedBeams = new ConcurrentQueue<UpdateBeams>();

        public enum TurretType
        {
            Pulse,
            Constant
        }

        public enum TargetType
        {
            Grid,
            Destroyable,
            Voxel,
        }

        internal void WebEnts()
        {
            _hitEntities.Clear();
            _work.Reset(FiredTurrets.Count);
            while (FiredTurrets.TryDequeue(out _work.Turret))
            {
                MyAPIGateway.Parallel.For(0, _work.Turret.Beams.Count, x =>
                {
                    var beam = _work.Turret.Beams[x];
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, _work.Webbed[x]);
                    for (int i = 0; i < _work.Webbed[x].Count; i++)
                    {
                        var result = _work.Webbed[x][i];
                        var ent = result.Element;
                        var grid = ent as MyCubeGrid;
                        var voxel = ent as MyVoxelBase;
                        var destroyable = ent as IMyDestroyableObject;
                        if (grid != null)
                        {
                            var turretWeb = new EntityHit(TargetType.Grid, grid, null, null, _checkBeams.Get());
                            turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                            _hitEntities.TryAdd(ent, turretWeb);
                        }
                        else if (destroyable != null)
                        {
                            var turretWeb = new EntityHit(TargetType.Destroyable, null, destroyable, null, _checkBeams.Get());
                            turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                            _hitEntities.TryAdd(ent, turretWeb);

                        }
                        else if (voxel != null)
                        {
                            var turretWeb = new EntityHit(TargetType.Voxel, null, null, voxel, _checkBeams.Get());
                            turretWeb.Turret.Add(_work.Turret.TurretId, new CheckBeam(TurretType.Pulse, _beams.Get()));
                            _hitEntities.TryAdd(ent, turretWeb);
                        }
                    }
                });
            }

            foreach (var pair in _hitEntities)
            {
                var web = pair.Value;
                if (web.Target == TargetType.Grid)
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

                            var hits = 0;
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

        public struct EntityHit
        {
            public readonly TargetType Target;
            public readonly IMyCubeGrid Grid;
            public readonly IMyDestroyableObject Destroyable;
            public readonly MyVoxelBase Voxel;
            public readonly Dictionary<long, CheckBeam> Turret;

            public EntityHit(TargetType target, MyCubeGrid grid, IMyDestroyableObject destroyable, MyVoxelBase voxel, Dictionary<long, CheckBeam> turret)
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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EmitterL", "EmitterS", "EmitterST")]
    public class Emitters : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool Suspended;
        internal bool Prime;
        internal bool Alpha;
        internal bool Beta;
        internal bool Zeta;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        internal bool ShieldLineOfSight;
        public bool EmitterOnline;
        private Vector3D _sightPos;

        public Vector3[] Directions = new Vector3[6] {Vector3.Forward, Vector3.Backward, Vector3.Left, Vector3.Right, Vector3.Up, Vector3.Down};
        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        internal EmitterGridComponent EGridComp;
        private MyEntitySubpart _subpartRotor;
        private MyParticleEffect _effect = new MyParticleEffect();

        internal Definition Definition;
        internal DSUtils Dsutil1 = new DSUtils();

        private IMyUpgradeModule Emitter => (IMyUpgradeModule)Entity;
        public EmitterType EmitterMode;

        private readonly Dictionary<long, Emitters> _emitters = new Dictionary<long, Emitters>();
        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();
        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();

        public enum EmitterType
        {
            Station,
            Large,
            Small,
        };

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

                Session.Instance.Emitters.Add(this);
                if (!_emitters.ContainsKey(Entity.EntityId)) _emitters.Add(Entity.EntityId, this);
                StorageSetup();
                SetEmitterType();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Emitter.Storage;
        }

        private void SetEmitterType()
        {
            switch (Emitter.BlockDefinition.SubtypeId)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    break;
                case "EmitterL":
                    EmitterMode = EmitterType.Large;
                    break;
                default:
                    EmitterMode = EmitterType.Small;
                    break;
            }
        }

        private bool InitStation()
        {
            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp == null || !ShieldComp.Warming) return false;

            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            if (!Emitter.CubeGrid.Components.Has<EmitterGridComponent>())
            {
                EGridComp = new EmitterGridComponent(this, true);
                Emitter.CubeGrid.Components.Add(EGridComp);
                EGridComp.PrimeComp = this;
                Prime = true;
            }
            else
            {
                Emitter.CubeGrid.Components.TryGet(out EGridComp);
                if (EGridComp.PrimeComp != null) EGridComp.PrimeComp.Alpha = true;
                EGridComp.PrimeComp = this;
                Prime = true;
            }
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            CheckShieldLineOfSight();
            EGridComp.RegisteredComps.Add(this);
            AllInited = true;

            return true;
        }

        private bool InitOther()
        {
            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            var hasEComp = Emitter.CubeGrid.Components.Has<EmitterGridComponent>();
            if (ShieldComp == null || IsStatic && !hasEComp || !ShieldComp.Warming) return false;

            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            if (!hasEComp && !IsStatic)
            {
                EGridComp = new EmitterGridComponent(this, false);
                Emitter.CubeGrid.Components.Add(EGridComp);
                EGridComp.BetaComp = this;
                Beta = true;
            }
            else
            {
                Emitter.CubeGrid.Components.TryGet(out EGridComp);
                if (EGridComp.BetaComp != null) EGridComp.BetaComp.Zeta = true;
                EGridComp.BetaComp = this;
                Beta = true;
            }
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            CheckShieldLineOfSight();
            EGridComp.RegisteredComps.Add(this);
            AllInited = true;
            return true;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Dsutil1.Sw.Restart();
                IsStatic = Emitter.CubeGrid.Physics.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                if (Suspended && _effect != null && !Session.DedicatedServer) BlockParticleStop();
                if (Suspended || Prime && !IsStatic || NotWorking()) return;

                if (!AllInited && EmitterMode == EmitterType.Station && !InitStation()) return;
                if (!AllInited && !InitOther()) return;

                if (Prime && EGridComp?.PrimeComp == null || Beta && EGridComp?.BetaComp == null) MasterElection();

                Timing();
                if (!BlockWorking()) return;

                if (Prime & ShieldComp.CheckEmitters || Beta && !IsStatic && ShieldComp.CheckEmitters)
                {
                    ShieldComp.CheckEmitters = false;
                    EGridComp.PerformEmitterDiagnostic = true;
                }

                if (ShieldComp.ShieldActive && !Session.DedicatedServer && UtilsStatic.ShieldDistanceCheck(Emitter, 1000, ShieldComp.BoundingRange))
                {
                    var leader = Beta && !Zeta;
                    var primeTock = Prime && EGridComp.EmitterOnScreenTick >= _tick - 60;
                    var leaderTock =  leader && EGridComp.EmitterOnScreenTick >= _tick - 60;
                    if (ShieldComp.GridIsMoving || ShieldComp.GridIsMoving) BlockParticleUpdate();
                    var blockCam = Emitter.PositionComp.WorldVolume;

                    if (Prime || !IsStatic && leader) EmitterSlaveAssignments();
                    if (Prime) SetStationShieldDims();

                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) || primeTock || leaderTock)
                    {
                        //Log.Line($"name:{Definition.Name} - id:{Emitter.EntityId} - p:{Prime} - pt:{primeTock} - lt:{leaderTock} - b:{Beta} - z:{Zeta}");
                        if (leader && !leaderTock) EGridComp.EmitterOnScreenTick = _tick;
                        else if (Prime && !primeTock) EGridComp.EmitterOnScreenTick = _tick;
                        else if (Zeta) EGridComp.EmitterOnScreenTick = _tick;

                        if (_effect == null && ShieldComp.ShieldPercent <= 97) BlockParticleStart();
                        else if (_effect != null && ShieldComp.ShieldPercent > 97f) BlockParticleStop();

                        BlockMoveAnimation();
                    }
                }
                else if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
                if (_count == 0) Dsutil1.StopWatchReport("Emitter", 4);
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }
        }

        private bool BlockWorking()
        {
            if (Alpha || IsStatic && Beta && EGridComp.PrimeComp == null || ShieldComp.DefenseShields == null) return false;

            BlockIsWorking = ShieldLineOfSight && Emitter.IsWorking;
            var nowOnline = BlockIsWorking && !BlockWasWorking;
            var wasOnline = !BlockIsWorking && BlockWasWorking;

            if (!BlockIsWorking && _tick % 300 == 0 || nowOnline) CheckShieldLineOfSight();
            if (!BlockIsWorking && !Session.DedicatedServer) DrawHelper();

            BlockWasWorking = BlockIsWorking;

            var isPrimed = IsStatic && Prime && BlockIsWorking;
            var notPrimed = Prime && !BlockIsWorking;
            var isBetaing = !IsStatic && Beta && BlockIsWorking;
            var notBetaing = !IsStatic && Beta && !BlockIsWorking;

            if (isPrimed) ShieldComp.EmittersWorking = true;
            else if (notPrimed) ShieldComp.EmittersWorking = false;
            else if (isBetaing) ShieldComp.EmittersWorking = true;
            else if (notBetaing) ShieldComp.EmittersWorking = false;
            if (!BlockIsWorking)
            {
                if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
                return false;
            }
            return true;
        }

        private bool NotWorking()
        {
            if (!Emitter.IsFunctional && BlockIsWorking)
            {
                BlockIsWorking = false;
                if (ShieldComp != null && (IsStatic && this == EGridComp?.PrimeComp || !IsStatic && this == EGridComp?.BetaComp)) ShieldComp.EmittersWorking = false;
                return true;
            }
            return !Emitter.IsFunctional;
        }

        private void SetStationShieldDims()
        {
            foreach (var keypair in EGridComp.OriginatedEmitters)
            {
                var mPos = EGridComp.PrimeComp.Emitter.PositionComp.WorldAABB.Center;
                var sPos = keypair.Value.Emitter.PositionComp.WorldAABB.Center;
                var findMax = Vector3D.Dot(sPos - mPos, keypair.Key);
                //Log.Line($"{keypair.Key.ToString()} - {findMax}");
            }
        }

        private void MasterElection()
        {
            var hasEComp = Emitter.CubeGrid.Components.Has<EmitterGridComponent>();
            if (!hasEComp)
            {
                if (IsStatic && EmitterMode == EmitterType.Station)
                {
                    EGridComp = new EmitterGridComponent(this, true);
                    Emitter.CubeGrid.Components.Add(EGridComp);
                    EGridComp.PrimeComp = this;
                    Prime = true;
                    Alpha = false;
                }
                else if (!IsStatic && EmitterMode != EmitterType.Station)
                {
                    EGridComp = new EmitterGridComponent(this, false);
                    Emitter.CubeGrid.Components.Add(EGridComp);
                    EGridComp.BetaComp = this;
                    Beta = true;
                    Zeta = false;
                }
            }
            else 
            {
                if (IsStatic && EmitterMode == EmitterType.Station)
                {
                    Emitter.CubeGrid.Components.TryGet(out EGridComp);
                    if (EGridComp.PrimeComp != null) EGridComp.PrimeComp.Alpha = true;
                    EGridComp.PrimeComp = this;
                    Prime = true;
                    Alpha = false;
                }
                else if (EmitterMode != EmitterType.Station)
                {
                    Emitter.CubeGrid.Components.TryGet(out EGridComp);
                    if (EGridComp.BetaComp != null) EGridComp.BetaComp.Zeta = true;
                    EGridComp.BetaComp = this;
                    Beta = true;
                    Zeta = false;
                }
            }
            CheckShieldLineOfSight();
        }

        private void EmitterSlaveAssignments()
        {
            Vector3I mPos;
            if (Beta) mPos = (Vector3I) Emitter.CubeGrid.PositionComp.LocalAABB.Center;
            else mPos = EGridComp.PrimeComp.Emitter.Position;

            var dirDefs = new EmitterDefinition();
            var reassign = new Dictionary<Vector3, Emitters>();
            var oriEmitters = EGridComp.OriginatedEmitters;
            oriEmitters.Clear();
            
            foreach (var rse in EGridComp.RegisteredComps)
            {
                var sPos = rse.Emitter.Position;
                foreach (var dir in Directions)
                {
                    var myDir = dirDefs.Get(dir);
                    var findMax = Vector3.Dot(sPos - mPos, dir);

                    if (findMax > myDir.Max)
                    {
                        myDir.Max = findMax;
                        myDir.Primary = rse;
                        myDir.Assigned = rse;
                    }
                    else if (findMax.Equals(myDir.Max)) myDir.Secondary = rse;
                }
            }

            foreach (var val in dirDefs.Def)
            {
                oriEmitters.Add(val.Key, val.Value.Primary);
                Emitters first = null;
                Emitters second = null;

                foreach (var val2 in dirDefs.Def.Values)
                {
                    if (val.Value.Primary == val2.Primary)
                    {
                        if (first == null) first = val2.Primary;
                        else second = val2.Primary;
                    }
                }
                if (second != null) reassign.Add(val.Key, val.Value.Secondary);
            }
            foreach (var keypair in reassign)
            {
                var dir = keypair.Key;
                var emitter = keypair.Value;
                if (!oriEmitters.ContainsValue(emitter))
                {
                    oriEmitters[dir] = emitter;
                    dirDefs.Get(dir).Assigned = emitter;
                }
            }

            foreach (var emitter in EGridComp.RegisteredComps)
            {
                if (emitter.Prime || emitter.Alpha) continue;
                if (!oriEmitters.ContainsValue(emitter) && emitter.Beta && !emitter.Zeta)
                {
                    emitter._subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.White, 0f);
                    emitter.Suspended = true;
                    EGridComp.BetaComp = null;
                }
                else if (!oriEmitters.ContainsValue(emitter))
                {
                    emitter._subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.White, 0f);
                    emitter.Suspended = true;
                }
                else emitter.Suspended = false;
            }

            //Log.Line($"fMax:{dirDefs.Get(Vector3.Forward).Max} - bMax:{dirDefs.Get(Vector3.Backward).Max} - lMax:{dirDefs.Get(Vector3.Left).Max} - rMax:{dirDefs.Get(Vector3.Right).Max} - uMax:{dirDefs.Get(Vector3.Up).Max} - dMax:{dirDefs.Get(Vector3.Down).Max}");
            //Log.Line($"Primary   : fId:{dirDefs.Get(Vector3.Forward).Primary?.Emitter?.NumberInGrid} - bId:{dirDefs.Get(Vector3.Backward).Primary?.Emitter?.NumberInGrid} - lId:{dirDefs.Get(Vector3.Left).Primary?.Emitter?.NumberInGrid} - rId:{dirDefs.Get(Vector3.Right).Primary?.Emitter?.NumberInGrid} - uId:{dirDefs.Get(Vector3.Up).Primary?.Emitter?.NumberInGrid} - dId:{dirDefs.Get(Vector3.Down).Primary?.Emitter?.NumberInGrid}");
            //Log.Line($"Secondary : fId:{dirDefs.Get(Vector3.Forward).Secondary?.Emitter?.NumberInGrid} - bId:{dirDefs.Get(Vector3.Backward).Secondary?.Emitter?.NumberInGrid} - lId:{dirDefs.Get(Vector3.Left).Secondary?.Emitter?.NumberInGrid} - rId:{dirDefs.Get(Vector3.Right).Secondary?.Emitter?.NumberInGrid} - uId:{dirDefs.Get(Vector3.Up).Secondary?.Emitter?.NumberInGrid} - dId:{dirDefs.Get(Vector3.Down).Secondary?.Emitter?.NumberInGrid}");
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Forward).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.White);
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Backward).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.Black);
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Left).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.Blue);
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Right).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.Red);
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Up).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.Green);
            //DsDebugDraw.DrawSingleVec(dirDefs.Get(Vector3.Down).Assigned.Emitter.PositionComp.WorldVolume.Center, 1.5f, Color.Purple);
        }

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
            if ((IsStatic && Prime && !Alpha) || (!IsStatic && Beta && !Zeta))
            {
                EGridComp.RotationTime -= 1;
                if (EGridComp.AnimationLoop == 0) EGridComp.TranslationTime = 0;
                if (EGridComp.AnimationLoop < 299) EGridComp.TranslationTime += 1;
                else EGridComp.TranslationTime -= 1;
                if (_count == 0) EGridComp.EmissiveIntensity = 2;
                if (_count < 30) EGridComp.EmissiveIntensity += 1;
                else EGridComp.EmissiveIntensity -= 1;

                EGridComp.RotationMatrix = MatrixD.CreateRotationY(0.05f * EGridComp.RotationTime);
                if (Prime) EGridComp.PrimeMatrix = EGridComp.RotationMatrix * MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * EGridComp.TranslationTime, 0);
                else EGridComp.BetaMatrix = EGridComp.RotationMatrix * MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * EGridComp.TranslationTime, 0);
            }
            else if (IsStatic && Beta && !Zeta) EGridComp.BetaMatrix = EGridComp.RotationMatrix * MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * EGridComp.TranslationTime, 0);

            var matrix = Prime ? EGridComp.PrimeMatrix : EGridComp.BetaMatrix;

            _subpartRotor.PositionComp.LocalMatrix = matrix;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", UtilsStatic.GetEmissiveColorFromFloatEmitter(ShieldComp.ShieldPercent), 0.1f * EGridComp.EmissiveIntensity);

            if ((IsStatic && Prime && !Alpha || !IsStatic && Beta && !Zeta) && EGridComp.AnimationLoop++ == 599) EGridComp.AnimationLoop = 0;
        }

        private void BlockParticleUpdate()
        {
            if (_effect == null) return;
            var testDist = Definition.ParticleDist;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = Emitter.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = Emitter.PositionComp.WorldMatrix;
            predictedMatrix.Translation = spawnPos;
            if (ShieldComp.ShieldVelocitySqr > 4000) predictedMatrix.Translation = spawnPos + Emitter.CubeGrid.Physics.GetVelocityAtPoint(Emitter.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            _effect.WorldMatrix = predictedMatrix;
        }

        private void BlockParticleStop()
        {
            if (_effect == null) return;

            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private void BlockParticleStart()
        {
            var scale = Definition.ParticleScale;
            MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effect);
            _effect.UserScale = 1f;
            _effect.UserRadiusMultiplier = scale;
            _effect.UserEmitterScale = 1f;
            BlockParticleUpdate();
        }
        #endregion

        private void CheckShieldLineOfSight()
        {
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            var testDist = Definition.FieldDist;

            var testDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = Emitter.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;
            MyAPIGateway.Parallel.For(0, ShieldComp.PhysicsOutside.Length, i =>
            {
                var hit = Emitter.CubeGrid.RayCastBlocks(testPos, ShieldComp.PhysicsOutside[i]);
                if (hit.HasValue)
                {
                    _blocksLos.Add(i);
                    return;
                }
                _noBlocksLos.Add(i);
            });
            if (ShieldComp.GridIsMobile)
            {
                MyAPIGateway.Parallel.For(0, _noBlocksLos.Count, i =>
                {
                    const int filter = CollisionLayers.VoxelCollisionLayer;
                    IHitInfo hitInfo;
                    var hit = MyAPIGateway.Physics.CastRay(testPos, ShieldComp.PhysicsOutside[_noBlocksLos[i]], out hitInfo, filter);
                    if (hit) _blocksLos.Add(_noBlocksLos[i]);
                });
            }
            for (int i = 0; i < ShieldComp.PhysicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
            ShieldLineOfSight = _blocksLos.Count < 500;
            if (Session.Enforced.Debug == 1) Log.Line($"EmitterId:{Emitter.EntityId.ToString()} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {ShieldLineOfSight.ToString()}");
        }

        private void DrawHelper()
        {
            const float lineWidth = 0.025f;
            var lineDist = Definition.HelperDist;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = ShieldComp.PhysicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = ShieldComp.PhysicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("The shield emitter DOES NOT have a CLEAR ENOUGH LINE OF SIGHT to the shield, SHUTTING DOWN.", 960, "Red", Emitter.OwnerId);
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("Blue means clear line of sight, black means blocked......................................................................", 960, "Red", Emitter.OwnerId);
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                if (EGridComp?.PrimeComp == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.PrimeComp = null;
                }
                else if (EGridComp?.BetaComp == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.BetaComp = null;
                }
                Session.Instance.Emitters.Remove(this);
                BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_emitters.ContainsKey(Entity.EntityId)) _emitters.Remove(Entity.EntityId);
                if (Session.Instance.Emitters.Contains(this)) Session.Instance.Emitters.Remove(this);
                if (EGridComp?.PrimeComp == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.PrimeComp = null;
                }
                else if (EGridComp?.BetaComp == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.BetaComp = null;
                }
                BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
    }
}
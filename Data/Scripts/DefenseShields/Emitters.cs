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
        private int _animationLoop;
        private int _rotationTime;
        private int _translationTime;
        private int _emissiveIntensity;

        private double _sVelSqr;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool ShieldInit;
        internal bool Master;
        internal bool StandbyMaster;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        internal bool ShieldLineOfSight;
        public bool EmitterOnline;
        private bool _blockParticleStopped;
        private Vector3D _sightPos;

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
            Master,
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

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;

                if (!Emitter.IsFunctional) return;

                if (!AllInited && EmitterMode == EmitterType.Master) InitMaster();
                else if (!AllInited) InitSlave();

                if (Master && !Emitter.CubeGrid.Physics.IsStatic) return;

                if (_count++ == 59)
                {
                    _count = 0;
                    _lCount++;
                    if (_lCount == 10) _lCount = 0;
                }
                BlockIsWorking = Emitter.IsFunctional && Emitter.IsWorking;

                if (Master && EGridComp?.MasterComp == null) MasterElection();

                if (Master && !StandbyMaster && ShieldLineOfSight && BlockIsWorking) ShieldComp.EmittersWorking = true;
                else if (Master && !StandbyMaster) ShieldComp.EmittersWorking = false;

                if (!Emitter.Enabled || !ShieldComp.ControlBlockWorking || StandbyMaster)
                {
                    if (!_blockParticleStopped && !Session.DedicatedServer) BlockParticleStop();
                    if (StandbyMaster) return;
                    //ShieldComp.EmittersWorking = false;
                    //ShieldComp.EmitterEvent = true;
                    return;
                }

                if (Master && ShieldComp.CheckEmitters)
                {
                    ShieldComp.CheckEmitters = false;
                    EGridComp.PerformEmitterDiagnostic = true;
                }

                if (!ShieldLineOfSight && !Session.DedicatedServer) DrawHelper();

                if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();

                if (!Session.DedicatedServer && UtilsStatic.ShieldDistanceCheck(Emitter, 1000, ShieldComp.BoundingRange))
                {
                    _sVelSqr = Emitter.CubeGrid.Physics.LinearVelocity.LengthSquared();
                    if (ShieldComp.GridIsMoving || ShieldComp.GridIsMoving) BlockParticleUpdate();
                    var blockCam = Emitter.PositionComp.WorldVolume;

                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam))
                    {
                        if (_blockParticleStopped) BlockParticleStart();
                        _blockParticleStopped = false;
                        BlockMoveAnimation();
                        if (_animationLoop++ == 599) _animationLoop = 0;
                    }
                }
                else if (!_blockParticleStopped && !Session.DedicatedServer) BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void InitMaster()
        {
            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp == null) return;
            ShieldInit = true;

            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            if (!Emitter.CubeGrid.Components.Has<EmitterGridComponent>())
            {
                EGridComp = new EmitterGridComponent(this);
                Emitter.CubeGrid.Components.Add(EGridComp);
                EGridComp.MasterComp = this;
                Master = true;
            }
            else 
            {
                Emitter.CubeGrid.Components.TryGet(out EGridComp);
                Master = true;
            }

            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            CheckShieldLineOfSight();
            if (!Session.DedicatedServer) BlockParticleCreate();
            Log.Line($"Master Emitter initted");
            AllInited = true;
        }

        private void InitSlave()
        {
            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp == null) return;
            ShieldInit = true;

            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            if (!Emitter.CubeGrid.Components.Has<EmitterGridComponent>())
            {
                EGridComp = new EmitterGridComponent(this);
                Emitter.CubeGrid.Components.Add(EGridComp);
                EGridComp.MasterComp = this;
            }
            else Emitter.CubeGrid.Components.TryGet(out EGridComp);

            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            CheckShieldLineOfSight();
            if (!Session.DedicatedServer) BlockParticleCreate();
            Log.Line($"Slave Emitter initted");
            AllInited = true;
        }

        private void MasterElection()
        {
            if (!Emitter.CubeGrid.Components.Has<EmitterGridComponent>() && EmitterMode == EmitterType.Master)
            {
                EGridComp = new EmitterGridComponent(this);
                Emitter.CubeGrid.Components.Add(EGridComp);
                EGridComp.MasterComp = this;
                Master = true;
                StandbyMaster = false;
            }
            else if (EmitterMode == EmitterType.Master)
            {
                Log.Line($"Existing Master");
                Emitter.CubeGrid.Components.TryGet(out EGridComp);
                if (EGridComp.MasterComp != null) EGridComp.MasterComp.StandbyMaster = true;
                EGridComp.MasterComp = this;
                Master = true;
                StandbyMaster = false;
            }
            CheckShieldLineOfSight();
        }

        private void SetEmitterType()
        {
            switch (Emitter.BlockDefinition.SubtypeId)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Master;
                    break;
                case "EmitterL":
                    EmitterMode = EmitterType.Large;
                    break;
                default:
                    EmitterMode = EmitterType.Small;
                    break;
            }
        }

        private bool UpdateEmitterState()
        {
            if (BlockIsWorking)
            {
                CheckShieldLineOfSight();

                EmitterOnline = true;
                if (Master)
                {
                    ShieldComp.EmittersWorking = true;
                    ShieldComp.EmitterEvent = true;
                }

                if (ShieldLineOfSight) return true;

                DrawHelper();
                return false;
            }
            if (BlockWasWorking)
            {
                EmitterOnline = false;
                if (!Master) return false;

                Log.Line($"Turned off");
                ShieldComp.EmittersWorking = false;
                ShieldComp.EmitterEvent = true;
            }
            else if (!ShieldLineOfSight)
            {
                DrawHelper();
            }
            return false;
        }

        private void EmitterSlaveAssignments()
        {
            var mPos = EGridComp.MasterComp.Emitter.Position;
            var forwardMax = float.MinValue;
            var backwardMax = float.MinValue;
            var leftMax = float.MinValue;
            var rightMax = float.MinValue;
            var upMax = float.MinValue;
            var downMax = float.MinValue;
            Emitters forwardEmitter = null;
            Emitters backwardEmitter = null;
            Emitters leftEmitter = null;
            Emitters rightEmitter = null;
            Emitters upEmitter = null;
            Emitters downEmitter = null;

            foreach (var rse in EGridComp.RegisteredSlaveComps)
            {
                var sPos = rse.Emitter.Position;
                var findfMax = Vector3.Dot(sPos - mPos, Vector3.Forward);
                var findbMax = Vector3.Dot(sPos - mPos, Vector3.Backward);
                var findlMax = Vector3.Dot(sPos - mPos, Vector3.Left);
                var findrMax = Vector3.Dot(sPos - mPos, Vector3.Right);
                var finduMax = Vector3.Dot(sPos - mPos, Vector3.Up);
                var finddMax = Vector3.Dot(sPos - mPos, Vector3.Down);

                if (findfMax > forwardMax)
                {
                    forwardMax = findfMax;
                    forwardEmitter = rse;
                }

                if (findbMax > backwardMax)
                {
                    backwardMax = findbMax;
                    backwardEmitter = rse;
                }

                if (findlMax > leftMax)
                {
                    leftMax = findlMax;
                    leftEmitter = rse;
                }

                if (findrMax > rightMax)
                {
                    rightMax = findrMax;
                    rightEmitter = rse;
                }

                if (finduMax > upMax)
                {
                    upMax = finduMax;
                    upEmitter = rse;
                }

                if (finddMax > downMax)
                {
                    downMax = finddMax;
                    downEmitter = rse;
                }
            }
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
            _rotationTime -= 1;
            if (_animationLoop == 0) _translationTime = 0;
            if (_animationLoop < 299) _translationTime += 1;
            else _translationTime -= 1;
            if (_count == 0) _emissiveIntensity = 2;
            if (_count < 30) _emissiveIntensity += 1;
            else _emissiveIntensity -= 1;

            var rotation = MatrixD.CreateRotationY(0.05f * _rotationTime);
            var translation = MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * _translationTime, 0);

            _subpartRotor.PositionComp.LocalMatrix = rotation * translation;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }

        private void BlockParticleCreate()
        {
            if (_effect == null)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Particle is null, creating - tick:{_tick.ToString()}");
                var scale = Definition.ParticleScale;
                MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effect);
                _effect.UserScale = 1f;
                _effect.UserRadiusMultiplier = scale;
                _effect.UserEmitterScale = 1f;
            }
            else
            {
                _effect.WorldMatrix = _subpartRotor.WorldMatrix;
                _effect.Stop();
                _blockParticleStopped = true;
            }
        }

        private void BlockParticleUpdate()
        {
            if (_effect == null || !_effect.IsStopped) return; // added IsStopped might prevent from looping.

            var testDist = Definition.ParticleDist;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = Emitter.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = Emitter.PositionComp.WorldMatrix;
            predictedMatrix.Translation = spawnPos;
            if (_sVelSqr > 4000) predictedMatrix.Translation = spawnPos + Emitter.CubeGrid.Physics.GetVelocityAtPoint(Emitter.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            _effect.WorldMatrix = predictedMatrix;
        }

        private void BlockParticleStop()
        {
            _blockParticleStopped = true;
            if (_effect == null) return;

            _effect.Stop();
            _effect.Close(false, true);
        }

        private void BlockParticleStart()
        {
            if (_effect == null || !_effect.IsStopped) return;

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
                Log.Line($"OnRemovedFromScene");
                if (EGridComp?.MasterComp == this)
                {
                    Log.Line($"Master removed from scene");
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.MasterComp = null;
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
                Log.Line($"Close");
                if (_emitters.ContainsKey(Entity.EntityId)) _emitters.Remove(Entity.EntityId);
                if (Session.Instance.Emitters.Contains(this)) Session.Instance.Emitters.Remove(this);
                if (EGridComp?.MasterComp == this)
                {
                    Log.Line($"Master removed on close");
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    EGridComp.MasterComp = null;
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
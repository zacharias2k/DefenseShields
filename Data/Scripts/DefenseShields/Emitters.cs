using System;
using System.Collections.Generic;
using System.Text;
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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EmitterL", "EmitterS", "EmitterST", "EmitterLA", "EmitterSA")]
    public class Emitters : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;

        internal float EmissiveIntensity;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool Suspended;
        internal bool Prime;
        internal bool Alpha;
        internal bool Beta;
        internal bool Zeta;
        internal bool Armored;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        internal bool ShieldLineOfSight;
        internal bool TookControl;
        public bool EmitterOnline;

        private const string PlasmaEmissive = "PlasmaEmissive";
        private const string EmitterEffect = "EmitterEffect";

        private Vector3D _sightPos;

        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        private MyEntitySubpart _subpartRotor;
        private MyParticleEffect _effect = new MyParticleEffect();

        internal Definition Definition;
        internal DSUtils Dsutil1 = new DSUtils();

        public IMyUpgradeModule Emitter => (IMyUpgradeModule)Entity;
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
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                Session.Instance.Emitters.Add(this);
                if (!_emitters.ContainsKey(Entity.EntityId)) _emitters.Add(Entity.EntityId, this);
                Storage = Emitter.Storage;

            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Dsutil1.Sw.Restart();
                IsStatic = Emitter.CubeGrid.Physics.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (!AllInited && !InitEmitter() || Suspend()) return;
                Timing();
                if (!BlockWorking()) return;

                if (ShieldComp.ShieldActive && !Session.DedicatedServer && UtilsStatic.DistanceCheck(Emitter, 1000, ShieldComp.BoundingRange))
                {
                    if (ShieldComp.GridIsMoving && !Armored) BlockParticleUpdate();

                    var blockCam = Emitter.PositionComp.WorldVolume;
                    var onCam = MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam);
                    if (onCam)
                    {
                        if (_effect == null && ShieldComp.ShieldPercent <= 97 && !Armored) BlockParticleStart();
                        else if (_effect != null && ShieldComp.ShieldPercent > 97f && !Armored) BlockParticleStop();
                        BlockMoveAnimation();
                    }
                }
                else if (_effect != null && !Session.DedicatedServer && !Armored) BlockParticleStop();
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"Emitter - {EmitterMode}", 4);
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
            if (_count == 29 && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Emitter.RefreshCustomInfo();
                Emitter.ShowInToolbarConfig = false;
                Emitter.ShowInToolbarConfig = true;
            }
            //else if (_lCount == 0 && _count == 0) Emitter.RefreshCustomInfo();
        }

        private bool InitEmitter()
        {
            if (Definition == null) SetEmitterType();

            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp == null) return false;

            ShieldComp.EmitterMode = (int)EmitterMode;
            if (!ShieldComp.Starting) return false;

            if (!AllInited && EmitterMode == EmitterType.Station)
            {
                if (ShieldComp.EmitterPrime != null)
                {
                    ShieldComp.EmitterPrime.Alpha = true;
                    TookControl = true;
                }
                ShieldComp.EmitterPrime = this;
                Prime = true;

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                ShieldComp.EmitterEvent = true;
                BlockWasWorking = true;
                Emitter.AppendingCustomInfo += AppendingCustomInfo;
                Emitter.RefreshCustomInfo();
                AllInited = true;
                return !Suspend();
            }
            if (!AllInited)
            {
                if (ShieldComp.EmitterBeta != null)
                {
                    ShieldComp.EmitterBeta.Zeta = true;
                    TookControl = true;
                    Suspended = true;
                }
                ShieldComp.EmitterBeta = this;
                Beta = true;

                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                ShieldComp.EmitterEvent = true;
                BlockWasWorking = true;
                Emitter.AppendingCustomInfo += AppendingCustomInfo;
                Emitter.RefreshCustomInfo();
                AllInited = true;
                return !Suspend();
            }
            return false;
        }

        private bool Suspend()
        {
            var funtional = Emitter.IsFunctional;
            var working = Emitter.IsWorking;
            if (Suspended && funtional && working && (Alpha && ShieldComp.EmitterPrime == null || Zeta && ShieldComp.EmitterBeta == null))
            {
                ShieldComp.EmitterMode = (int)EmitterMode;
                if (Prime)
                {
                    Alpha = false;
                    ShieldComp.EmitterPrime = this;
                    TookControl = true;
                }
                else
                {
                    Zeta = false;
                    ShieldComp.EmitterBeta = this;
                    TookControl = true;
                }

                ShieldComp.EmitterEvent = true;
            }
            else if (!funtional || !working || Alpha || Zeta || Prime && !IsStatic || Beta && IsStatic || Prime && ShieldComp.EmitterPrime != this || Beta && ShieldComp.EmitterBeta != this)
            {
                if (_effect != null && !Session.DedicatedServer && !Armored) BlockParticleStop();
                if (_tick % 600 == 0) Emitter.RefreshCustomInfo();
                if (Prime && ShieldComp.EmittersWorking && ShieldComp.EmitterPrime == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                }
                else if (Beta && ShieldComp.EmittersWorking && ShieldComp.EmitterBeta == this)
                {
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                }
                Suspended = true;
                return Suspended;
            }

            if (Suspended) ShieldComp.EmitterMode = (int)EmitterMode;
            Suspended = false;
            return Suspended;
        }

        private bool BlockWorking()
        {
            if (ShieldComp.DefenseShields == null || !ShieldComp.Warming) return false;

            if (ShieldComp.CheckEmitters || TookControl) CheckShieldLineOfSight();
            if (!ShieldLineOfSight && !Session.DedicatedServer) DrawHelper();

            BlockIsWorking = ShieldLineOfSight && Emitter.IsWorking;
            var isPrimed = IsStatic && Prime && BlockIsWorking;
            var notPrimed = Prime && !BlockIsWorking;
            var isBetaing = !IsStatic && Beta && BlockIsWorking;
            var notBetaing = !IsStatic && Beta && !BlockIsWorking;

            if (isPrimed) ShieldComp.EmittersWorking = true;
            else if (notPrimed) ShieldComp.EmittersWorking = false;
            else if (isBetaing) ShieldComp.EmittersWorking = true;
            else if (notBetaing) ShieldComp.EmittersWorking = false;

            BlockWasWorking = BlockIsWorking;

            if (!BlockIsWorking)
            {
                if (_effect != null && !Session.DedicatedServer && !Armored) BlockParticleStop();
                return false;
            }
            return true;
        }

        private void SetEmitterType()
        {
            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            switch (Definition.Name)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    break;
                case "EmitterL":
                case "EmitterLA":
                    EmitterMode = EmitterType.Large;
                    if (Definition.Name == "EmitterLA") Armored = true;
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") Armored = true;
                    break;
            }
        }

        #region Block Animation
        private void BlockMoveAnimationReset(bool isNull)
        {
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
            if (isNull) Entity.TryGetSubpart("Rotor", out _subpartRotor);
            else
            {
                _subpartRotor.Subparts.Clear();
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
            }
        }

        private void BlockMoveAnimation()
        {
            if (Armored)
            {
                if (_count == 0) EmissiveIntensity = 2;
                if (_count < 30) EmissiveIntensity += 1;
                else EmissiveIntensity -= 1;
                Emitter.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetShieldColorFromFloat(ShieldComp.ShieldPercent), 0.1f * EmissiveIntensity);
                return;
            }

            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(false);
            RotationTime -= 1;
            if (AnimationLoop == 0) TranslationTime = 0;
            if (AnimationLoop < 299) TranslationTime += 1;
            else TranslationTime -= 1;
            if (_count == 0) EmissiveIntensity = 2;
            if (_count < 30) EmissiveIntensity += 1;
            else EmissiveIntensity -= 1;

            var rotationMatrix = MatrixD.CreateRotationY(0.025f * RotationTime);
            var matrix = rotationMatrix * MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * TranslationTime, 0);

            _subpartRotor.PositionComp.LocalMatrix = matrix;
            _subpartRotor.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetShieldColorFromFloat(ShieldComp.ShieldPercent), 0.1f * EmissiveIntensity);

            if (AnimationLoop++ == 599) AnimationLoop = 0;
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
            MyParticlesManager.TryCreateParticleEffect(EmitterEffect, out _effect);
            _effect.UserScale = 1f;
            _effect.UserRadiusMultiplier = scale;
            _effect.UserEmitterScale = 1f;
            BlockParticleUpdate();
        }
        #endregion

        private void CheckShieldLineOfSight()
        {
            var subNull = _subpartRotor == null;
            if (subNull || _subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(subNull);
            TookControl = false;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            var testDist = Definition.FieldDist;
            var testDir = Emitter.PositionComp.WorldMatrix.Up;
            if (!Armored) testDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
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
            if (!IsStatic)
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
            ShieldLineOfSight = _blocksLos.Count < 510;
            ShieldComp.CheckEmitters = false;
            if (Session.Enforced.Debug == 1) Log.Line($"EmitterId: {Emitter.EntityId.ToString()} - Mode: {EmitterMode} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {ShieldLineOfSight.ToString()}");
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

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (!ShieldComp.ShieldActive)
            {
                stringBuilder.Append("[ Shield Offline ]");
            }
            else
            {
                stringBuilder.Append("[Emitter Type]: " + (EmitterMode) +
                                     "\n[Line of Sight]: " + ShieldLineOfSight +
                                     "\n[Is a Backup]: " + (Alpha || Zeta) +
                                     "\n[Is Suspended]: " + Suspended);
            }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                if (ShieldComp?.EmitterPrime == this)
                {
                    if (ShieldComp != null)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterPrime = null;
                }
                else if (ShieldComp?.EmitterBeta == this)
                {
                    if (ShieldComp != null)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterBeta = null;
                }
                if (Session.Instance.Emitters.Contains(this)) Session.Instance.Emitters.Remove(this);
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
                if (ShieldComp?.EmitterPrime == this)
                {
                    if (ShieldComp != null)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterPrime = null;
                }
                else if (ShieldComp?.EmitterBeta == this)
                {
                    if (ShieldComp != null)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterBeta = null;
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
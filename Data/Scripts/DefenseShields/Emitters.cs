using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
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
        internal bool GoToSleep;
        internal bool Prime;
        internal bool Alpha;
        internal bool Beta;
        internal bool Zeta;
        internal bool Compact;
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
            Unknown
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
                _emitters.Add(Entity.EntityId, this);
                Storage = Emitter.Storage;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Dsutil1.Sw.Restart();
                IsStatic = Emitter.CubeGrid.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                Timing();

                if (!AllInited && !InitEmitter() || Suspend() || !BlockWorking()) return;

                if (ShieldComp.ShieldActive && !Session.DedicatedServer && UtilsStatic.DistanceCheck(Emitter, 1000, ShieldComp.BoundingRange))
                {
                    if (ShieldComp.GridIsMoving && !Compact) BlockParticleUpdate();

                    var blockCam = Emitter.PositionComp.WorldVolume;
                    var onCam = MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam);
                    if (onCam)
                    {
                        if (_effect == null && ShieldComp.ShieldPercent <= 97 && !Compact) BlockParticleStart();
                        else if (_effect != null && ShieldComp.ShieldPercent > 97f && !Compact) BlockParticleStop();
                        BlockMoveAnimation();
                    }
                }
                else if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
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
        }

        private bool InitEmitter()
        {
            Emitter.CubeGrid.Components.TryGet(out ShieldComp);
            if (ShieldComp == null) Log.Line($"Init: Name:{Definition?.Name} - Mode:({EmitterMode}) - CompNull:{ShieldComp == null} F:({!Emitter.IsFunctional}) - EW:{!ShieldComp?.EmittersWorking} - ES:{ShieldComp?.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended}");

            if (!Emitter.IsFunctional || ShieldComp == null) return false;

            if (Definition == null) SetEmitterType();

            if (!ShieldComp.Starting)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"InitEmitter: shield ({Emitter.EntityId} - {EmitterMode}) is not starting, setting mode and looping");
                ShieldComp.EmitterMode = (int)EmitterMode;
                //return false;
            }

            if (!AllInited && EmitterMode == EmitterType.Station)
            {
                if (ShieldComp.EmitterPrime != null) ShieldComp.EmitterPrime.Alpha = true;

                if ((int) EmitterMode == ShieldComp.EmitterMode)
                {
                    TookControl = true;
                    ShieldComp.EmitterEvent = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"InitEmitter: ({Emitter.EntityId} - {EmitterMode}) is taking control of prime");
                }

                ShieldComp.EmitterPrime = this;
                Prime = true;

                if (!Compact) Entity.TryGetSubpart("Rotor", out _subpartRotor);
                BlockWasWorking = true;
                BlockMoveAnimationReset(true);
                AllInited = true;
                return true;
            }
            if (!AllInited)
            {
                if (ShieldComp.EmitterBeta != null) ShieldComp.EmitterBeta.Zeta = true;

                if ((int)EmitterMode == ShieldComp.EmitterMode)
                {
                    TookControl = true;
                    ShieldComp.EmitterEvent = true;
                    if (Session.Enforced.Debug == 1) Log.Line($"InitEmitter: ({Emitter.EntityId} - {EmitterMode}) is taking control of beta");
                }

                ShieldComp.EmitterBeta = this;
                Beta = true;

                if (!Compact) Entity.TryGetSubpart("Rotor", out _subpartRotor);
                BlockWasWorking = true;
                BlockMoveAnimationReset(true);
                AllInited = true;
                return true;
            }
            return false;
        }

        private bool Suspend()
        {
            var working = Emitter.IsWorking && Emitter.IsFunctional;

            var otherMode = Prime && !ShieldComp.Station || Beta && ShieldComp.Station;
            var modeSwitch = working && otherMode && (IsStatic && EmitterMode == EmitterType.Station || !IsStatic && EmitterMode != EmitterType.Station);
            var modeOpen = working && Suspended && (Prime && ShieldComp.EmitterPrime == null || Beta && ShieldComp.EmitterBeta == null);
            var terminalConnected = ShieldComp.GetLinkedGrids.Count - ShieldComp.GetSubGrids.Count > 0;

            if (!IsStatic && ShieldComp.Starting && terminalConnected && !GoToSleep || GoToSleep && _count == 0 && _lCount % 2 == 0)
            {
                var foundStatic = false;
                foreach (var sub in ShieldComp.GetLinkedGrids)
                {
                    if (sub == Emitter.CubeGrid) continue;

                    if (sub.IsStatic)
                    {
                        foundStatic = true;
                        break;
                    }
                }

                if (foundStatic)
                {
                    if (!GoToSleep)
                    {
                        if (Session.Enforced.Debug == 1) Log.Line($"Emitter: Going to sleep");
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = true;
                        ShieldComp.DefenseShields.Shield.RefreshCustomInfo();
                    }
                }
                else if (GoToSleep && ShieldComp.EmittersSuspended)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Emitter: Waking Up");
                    ShieldComp.EmitterEvent = true;
                    ShieldComp.EmittersSuspended = false;
                    GoToSleep = false;
                }
                GoToSleep = foundStatic;
                Suspended = GoToSleep;
                if (Suspended)
                {
                    if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
                    if (!Session.DedicatedServer && !EmissiveIntensity.Equals(0)) BlockMoveAnimationReset(true);
                    return Suspended;
                }
            }
            else if (GoToSleep) return GoToSleep;

            if (ShieldComp == null)
            {
                Emitter.CubeGrid.Components.TryGet(out ShieldComp);
                if (Session.Enforced.Debug == 1) Log.Line($"Emitter Suspend: {Emitter.EntityId} had a null ShieldComp, trying.... {ShieldComp != null}");
                if (ShieldComp == null) return true;
            }
            if (modeOpen)
            {
                Log.Line($"Emitter-Suspend-to-Active: Name:{Definition?.Name} - Mode:({EmitterMode}) - CompNull:{ShieldComp == null} F:({!Emitter.IsFunctional}) - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended}");
                if (Prime)
                {
                    Alpha = false;
                    ShieldComp.EmitterPrime = this;
                    TookControl = true;
                    if (modeSwitch)
                    {
                        if (ShieldComp.EmitterBeta != null) ShieldComp.EmitterBeta.Suspended = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.Station = EmitterMode == EmitterType.Station;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        Suspended = false;
                    }

                    if (Session.Enforced.Debug == 1) Log.Line($"Emitter Suspend: {Definition.Name} is taking control of prime - M-Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended}");
                }
                else
                {
                    Zeta = false;
                    ShieldComp.EmitterBeta = this;
                    TookControl = true;

                    if (modeSwitch)
                    {
                        if (ShieldComp.EmitterPrime != null) ShieldComp.EmitterPrime.Suspended = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.Station = EmitterMode == EmitterType.Station;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        Suspended = false;
                    }

                    if (Session.Enforced.Debug == 1) Log.Line($"Emitter Suspend: {Definition.Name} is taking control of beta - modeSwitch {modeSwitch} - {IsStatic} - Mode:{EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeM:{(int)EmitterMode == ShieldComp.EmitterMode} - S:{Suspended}");
                }
            }
            else if (otherMode)
            {
                //if (Session.Enforced.Debug == 1) Log.Line($"Emitter OtherMode: {Definition.Name} suspending - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended}");
                if (!modeSwitch)
                {
                    if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
                    if (!Session.DedicatedServer && !EmissiveIntensity.Equals(0)) BlockMoveAnimationReset(true);
                    Suspended = true;
                    return Suspended;
                }

                ShieldComp.EmitterMode = (int)EmitterMode;
                ShieldComp.Station = EmitterMode == EmitterType.Station;
                ShieldComp.EmitterEvent = true;
                ShieldComp.EmittersSuspended = false;
            }

            if (!otherMode && Suspended)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Emitter Unsuspend: this is otherMode");
                ShieldComp.EmittersSuspended = false;
                ShieldComp.EmitterMode = (int)EmitterMode;
                ShieldComp.Station = EmitterMode == EmitterType.Station;
                if (Session.Enforced.Debug == 1) Log.Line($"Emitter Unsuspend: modeOpen:{modeOpen} - modeSwitch {modeSwitch} - Mode:{EmitterMode} - Station: {ShieldComp.Station} - CompMode: {ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended}");
            }

            Suspended = false;
            return Suspended;
        }

        private bool BlockWorking()
        {
            if (!Emitter.IsFunctional || !Emitter.IsWorking || ShieldComp?.DefenseShields == null || !ShieldComp.Warming) return false;

            if (ShieldComp.CheckEmitters || TookControl) CheckShieldLineOfSight();
            if (!ShieldLineOfSight && !Session.DedicatedServer) DrawHelper();

            BlockIsWorking = ShieldLineOfSight && Emitter.IsWorking && Emitter.IsFunctional;

            ShieldComp.EmittersWorking = BlockIsWorking;

            BlockWasWorking = BlockIsWorking;

            if (!BlockIsWorking)
            {
                if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
                if (!Session.DedicatedServer && !EmissiveIntensity.Equals(0)) BlockMoveAnimationReset(true);
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
                    if (Definition.Name == "EmitterLA") Compact = true;
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") Compact = true;
                    break;
                default:
                    EmitterMode = EmitterType.Unknown;
                    break;
            }
            Emitter.AppendingCustomInfo += AppendingCustomInfo;
            Emitter.RefreshCustomInfo();
        }

        #region Block Animation
        private void BlockMoveAnimationReset(bool clearAnimation)
        {
            if (clearAnimation)
            {
                RotationTime = 0;
                TranslationTime = 0;
                AnimationLoop = 0;
                EmissiveIntensity = 0;

                if (_subpartRotor != null)
                {
                    var rotationMatrix = MatrixD.CreateRotationY(0);
                    var matrix = rotationMatrix * MatrixD.CreateTranslation(0, 0, 0);
                    _subpartRotor.PositionComp.LocalMatrix = matrix;
                    _subpartRotor.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetShieldColorFromFloat(ShieldComp.ShieldPercent), 0.1f * EmissiveIntensity);
                }
                else Emitter.SetEmissiveParts(PlasmaEmissive, Color.Transparent, 0);
                return;
            }

            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement, [EmitterType: {Definition.Name} - Compact({Compact})] - not null - Tick:{_tick.ToString()}");
        }

        private void BlockMoveAnimation()
        {
            if (Compact)
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
            if (!Compact && _subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(false);
            TookControl = false;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            var testDist = Definition.FieldDist;
            var testDir = Emitter.PositionComp.WorldMatrix.Up;
            if (!Compact) testDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
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
                                     "\n[Is Suspended]: " + Suspended +
                                     "\n[Is Initted]: " + AllInited);

            }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (ShieldComp?.EmitterPrime == this)
                {
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterPrime = null;
                }
                else if (ShieldComp?.EmitterBeta == this)
                {
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
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
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.EmitterPrime = null;
                }
                else if (ShieldComp?.EmitterBeta == this)
                {
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
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
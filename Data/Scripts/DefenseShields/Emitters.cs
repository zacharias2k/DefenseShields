using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EmitterL", "EmitterS", "EmitterST", "EmitterLA", "EmitterSA")]
    public class Emitters : MyGameLogicComponent
    {
        private uint _tick;
        private uint _myRenderId;
        private int _count = -1;
        private int _lCount;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;

        private float _power = 0.01f;
        internal float EmissiveIntensity;

        internal bool Online;
        public bool ServerUpdate;
        internal bool Suspended;
        internal bool GoToSleep;
        internal bool Backup;
        internal bool Compact;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool TookControl;
        internal bool ControllerFound;

        private const string PlasmaEmissive = "PlasmaEmissive";

        private Vector3D _sightPos;

        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        private MyEntitySubpart _subpartRotor;
        private MyParticleEffect _effect = new MyParticleEffect();
        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        internal Definition Definition;
        internal DSUtils Dsutil1 = new DSUtils();

        public IMyUpgradeModule Emitter => (IMyUpgradeModule)Entity;
        public EmitterType EmitterMode;

        private readonly Dictionary<long, Emitters> _emitters = new Dictionary<long, Emitters>();
        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();
        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

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
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Emitter.CubeGrid.Physics == null) return;
                Session.Instance.Emitters.Add(this);
                _emitters.Add(Entity.EntityId, this);
                Storage = Emitter.Storage;
                PowerInit();
                _myRenderId = Emitter.Render.GetRenderObjectID();
                SetEmitterType();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Emitter.CubeGrid.Physics == null) return;
                if (Session.Enforced.Debug == 1) Dsutil1.Sw.Restart();
                IsStatic = Emitter.CubeGrid.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                Timing();

                if (Suspend() || !BlockWorking()) return;

                if (ShieldComp.ShieldActive && !Session.DedicatedServer && UtilsStatic.DistanceCheck(Emitter, 1000, ShieldComp.DefenseShields.BoundingRange))
                {
                    //if (ShieldComp.GridIsMoving && !Compact) BlockParticleUpdate();

                    var blockCam = Emitter.PositionComp.WorldVolume;
                    var onCam = MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam);
                    if (onCam)
                    {
                        //if (_effect == null && ShieldComp.ShieldPercent <= 97 && !Compact) BlockParticleStart();
                        //else if (_effect != null && ShieldComp.ShieldPercent > 97f && !Compact) BlockParticleStop();
                        BlockMoveAnimation();
                    }
                }
                else BlockReset();
                if (Session.Enforced.Debug == 1) Dsutil1.StopWatchReport($"PerfMod: EmitterId [{Emitter.EntityId}]", 4);
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

        #region Block Animation

        private void BlockReset(bool force = false)
        {
            //if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
            if (!Session.DedicatedServer && !EmissiveIntensity.Equals(0) || !Session.DedicatedServer && force) BlockMoveAnimationReset(true);
        }

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
                    _subpartRotor.SetEmissiveParts(PlasmaEmissive, Color.Transparent, 0);
                }
                else Emitter.SetEmissiveParts(PlasmaEmissive, Color.Transparent, 0);
                return;
            }

            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
            if (Session.Enforced.Debug == 1) Log.Line($"MoveReset: [EmitterType: {Definition.Name} - Compact({Compact})] - not null - Tick:{_tick.ToString()} - EmitterId [{Emitter.EntityId}]");
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
            _effect?.Stop();
            _effect?.Close(false, true);
            _effect = null;
        }

        private void BlockParticleStart()
        {
            var scale = Definition.ParticleScale;
            var matrix = Emitter.WorldMatrix;
            var pos = Emitter.WorldVolume.Center;
            MyParticlesManager.TryCreateParticleEffect(6666, out _effect, ref matrix, ref pos, _myRenderId, true); // 15, 16, 17, 24, 25, 28, (31, 32) 211 215 53
            _effect.UserRadiusMultiplier = scale;
            _effect.Play();
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
            ShieldComp.DefenseShields.ResetShape(false, false);

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
            ShieldComp.EmittersLos = _blocksLos.Count < 530;
            ShieldComp.CheckEmitters = false;
            if (Session.Enforced.Debug == 1) Log.Line($"LOS: Mode: {EmitterMode} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {ShieldComp.EmittersLos.ToString()} - EmitterId [{Emitter.EntityId}]");
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
            var compatible = IsStatic && EmitterMode == EmitterType.Station || !IsStatic && EmitterMode != EmitterType.Station;

            if (ShieldComp == null || !ControllerFound)
            {
                stringBuilder.Append("[ No Valid Controller ]" +
                                     "\n" +
                                     "\n[Emitter Type]: " + EmitterMode +
                                     "\n[Grid Compatible]: " + compatible +
                                     "\n[Invalid Protocol]: " + (ShieldComp?.DefenseShields == null) +
                                     "\n[Controller Link]: " + ControllerFound);
            }
            else if (!ShieldComp.ShieldActive)
            {
                stringBuilder.Append("[ Emitter Offline ]" +
                                     "\n" +
                                     "\n[Emitter Type]: " + EmitterMode +
                                     "\n[Grid Compatible]: " + compatible +
                                     "\n[Controller Link]: " + ShieldComp?.DefenseShields?.Shield?.IsWorking +
                                     "\n[Line of Sight]: " + ShieldComp.EmittersLos +
                                     "\n[Grid Access]: " + ShieldComp?.DefenseShields?.ControllerGridAccess +
                                     "\n[Is Suspended]: " + Suspended +
                                     "\n[Is a Backup]: " + Backup +
                                     "\n[Is Docked]: " + GoToSleep);
            }
            else
            {
                stringBuilder.Append("[ Emitter Online ]" +
                                     "\n" +
                                     "\n[Emitter Type]: " + EmitterMode +
                                     "\n[Grid Compatible]: " + compatible +
                                     "\n[Controller Link]: " + ControllerFound +
                                     "\n[Line of Sight]: " + ShieldComp.EmittersLos +
                                     "\n[Grid Access]: " + ShieldComp?.DefenseShields?.ControllerGridAccess +
                                     "\n[Is Suspended]: " + Suspended +
                                     "\n[Is a Backup]: " + Backup +
                                     "\n[Is Docked]: " + GoToSleep);
            }
        }

        private void PowerPreInit()
        {
            try
            {
                if (Sink == null)
                {
                    Sink = new MyResourceSinkComponent();
                }
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
                Sink.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Emitter.Enabled;
                if (enableState)
                {
                    Emitter.Enabled = false;
                    Emitter.Enabled = true;
                }
                Sink.Update();
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit complete - EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private void SetEmitterType()
        {
            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            switch (Definition.Name)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
                case "EmitterL":
                case "EmitterLA":
                    EmitterMode = EmitterType.Large;
                    if (Definition.Name == "EmitterLA") Compact = true;
                    else Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") Compact = true;
                    else Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
            }
            Emitter.AppendingCustomInfo += AppendingCustomInfo;
        }

        private bool BlockWorking()
        {
            if (Sink.CurrentInputByType(GId) < 0.01f || Emitter.CubeGrid == null || !Emitter.Enabled || !Emitter.IsFunctional)
            {
                Online = false;
            }
            else Online = true;

            var logic = ShieldComp.DefenseShields;
            var losCheckReq = Online && logic.Starting && !logic.Suspended && logic.UnsuspendTick == uint.MinValue;
            if (losCheckReq && (ShieldComp.CheckEmitters || TookControl)) CheckShieldLineOfSight();
            if (losCheckReq && !ShieldComp.EmittersLos && !Session.DedicatedServer) DrawHelper();

            BlockIsWorking = ShieldComp.EmittersLos && Emitter.IsWorking && Emitter.IsFunctional;

            ShieldComp.EmittersWorking = BlockIsWorking && Online;

            if (!BlockIsWorking || !ShieldComp.ShieldActive)
            {
                BlockReset();
                return false;
            }
            ControllerFound = true;
            return true;
        }

        private bool Suspend()
        {
            ControllerFound = false;
            if (!Compact && _subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null)
                {
                    Suspended = true;
                    return true;
                }
                return true;
            }

            if (ShieldComp?.DefenseShields == null)
            {
                Emitter.CubeGrid.Components.TryGet(out ShieldComp);
                if (ShieldComp?.DefenseShields == null || !ShieldComp.DefenseShields.ControllerGridAccess)
                {
                    if (!Suspended)
                    {
                        BlockReset(true);
                        if (Session.Enforced.Debug == 1) Log.Line($"Suspend: null detected - ShieldComp: {ShieldComp != null} - DefenseComp: {ShieldComp?.DefenseShields != null} - EmitterId [{Emitter.EntityId}]");
                    }
                    Suspended = true;
                    return true;
                }
            }
            else if (!ShieldComp.DefenseShields.ControllerGridAccess)
            {
                if (!Suspended)
                {
                    BlockReset(true);
                    if (Session.Enforced.Debug == 1) Log.Line($"No Access: EmitterId [{Emitter.EntityId}]");
                }
                Suspended = true;
                return true;
            }

            var working = Emitter.IsWorking && Emitter.IsFunctional;
            var stationMode = EmitterMode == EmitterType.Station;
            var shipMode = EmitterMode != EmitterType.Station;
            var modes = IsStatic && stationMode || !IsStatic && shipMode;
            var mySlotNull = stationMode && ShieldComp.StationEmitter == null || shipMode && ShieldComp.ShipEmitter == null;
            var myComp = stationMode && ShieldComp.StationEmitter == this || shipMode && ShieldComp.ShipEmitter == this;

            var myMode = working && modes;
            var mySlotOpen = working && mySlotNull;
            var myShield = myMode && myComp;
            var iStopped = !working && myComp && modes;
            //Log.Line($"!myMode: {Definition.Name} suspending - iStopped:{iStopped} - myMode: {myMode} - myShield: {myShield} - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");

            var terminalConnected = ShieldComp.GetLinkedGrids.Count - ShieldComp.GetSubGrids.Count > 0;

            if (!IsStatic && ShieldComp.DefenseShields.Starting && terminalConnected && !GoToSleep || GoToSleep && _count == 0 && _lCount % 2 == 0)
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
                        if (Session.Enforced.Debug == 1) Log.Line($"Sleep: Going to sleep - EmitterId [{Emitter.EntityId}]");
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = true;
                        BlockReset(true);
                    }
                }
                else if (GoToSleep && ShieldComp.EmittersSuspended)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Sleep: Waking Up - EmitterId [{Emitter.EntityId}]");
                    ShieldComp.EmitterEvent = true;
                    ShieldComp.EmittersSuspended = false;
                    GoToSleep = false;
                }
                GoToSleep = foundStatic;
                Suspended = GoToSleep;
                if (Suspended) return Suspended;
            }
            else if (GoToSleep) return GoToSleep;

            if (mySlotOpen)
            {
                if (stationMode)
                {
                    Backup = false;
                    ShieldComp.StationEmitter = this;
                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        Suspended = false;
                        myShield = true;
                        Backup = false;
                    }
                    else
                    {
                        Suspended = true;
                        BlockReset(true);
                    }
                }
                else
                {
                    Backup = false;
                    ShieldComp.ShipEmitter = this;

                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        Suspended = false;
                        myShield = true;
                        Backup = false;
                    }
                    else
                    {
                        Suspended = true;
                        BlockReset(true);
                    }
                }
                if (Session.Enforced.Debug == 1) Log.Line($"mySlotOpen: {Definition.Name} - myMode:{myMode} - MyShield:{myShield} - Mode:{EmitterMode} - Static:{IsStatic} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeM:{(int)EmitterMode == ShieldComp.EmitterMode} - S:{Suspended} - EmitterId [{Emitter.EntityId}]");
            }
            else if (!myMode)
            {
                var compMode = ShieldComp.EmitterMode;
                if (!Suspended && (compMode == 0 && !IsStatic || compMode != 0 && IsStatic) || iStopped)
                {
                    ShieldComp.EmittersSuspended = true;
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    BlockReset(true);
                    if (Session.Enforced.Debug == 1) Log.Line($"!myMode: {Definition.Name} suspending - myMode: {myMode} - myShield: {myShield} - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                else if (!Suspended)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"!myMode: {Definition.Name} suspending - myMode: {myMode} - myShield: {myShield} - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{Suspended} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                    BlockReset(true);
                }
                Suspended = true;
            }

            if (!myShield)
            {
                if (!Suspended)
                {
                    Backup = true;
                    BlockReset(true);
                }
                Suspended = true;
            }

            if (myShield && Suspended)
            {
                ShieldComp.EmittersSuspended = false;
                ShieldComp.EmitterMode = (int)EmitterMode;
                ShieldComp.EmitterEvent = true;
                Backup = false;
                //if (!Compact && _subpartRotor == null) Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (Session.Enforced.Debug == 1) Log.Line($"Unsuspend - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - Station: {ShieldComp.Station} - CompMode: {ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
            }
            else if (Suspended) return Suspended;

            Suspended = false;
            return Suspended;
        }

        public override void OnAddedToScene()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"OnAddedToScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"OnRemovedFromScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
                BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Close: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
                if (_emitters.ContainsKey(Entity.EntityId)) _emitters.Remove(Entity.EntityId);
                if (Session.Instance.Emitters.Contains(this)) Session.Instance.Emitters.Remove(this);
                if (ShieldComp?.StationEmitter == this)
                {
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.StationEmitter = null;
                }
                else if (ShieldComp?.ShipEmitter == this)
                {
                    if (ShieldComp != null && (int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.ShipEmitter = null;
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
                if (Session.Enforced.Debug == 1) Log.Line($"MarkForClose: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
    }
}
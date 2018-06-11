using System;
using System.Collections.Generic;
using DefenseShields.Control;
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
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "DefenseShieldsLS", "DefenseShieldsSS", "DefenseShieldsST")]
    public class Emitters : MyGameLogicComponent
    {
        public bool ServerUpdate;
        internal bool MainInit;
        private bool _shieldLineOfSight;
        private uint _tick;
        private int _count = -1;
        private int _lCount;

        private readonly Dictionary<long, Emitters> _emitters = new Dictionary<long, Emitters>();

        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();
        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();
        private Vector3D _sightPos;

        public MyModStorageComponentBase Storage { get; set; }
        internal EmitterSettings Settings = new EmitterSettings();

        internal ShieldGridComponent SGridComponent;

        private IMyUpgradeModule Emitter => (IMyUpgradeModule)Entity;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateVoxels;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule> _modulateGrids;

        private MyEntitySubpart _subpartRotor;
        private int _animationLoop;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private readonly MyParticleEffect[] _effects = new MyParticleEffect[1];
        internal Definition Definition;
        private bool _blockParticleStopped;
        private double _sVelSqr;

        internal DSUtils Dsutil1 = new DSUtils();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

                Session.Instance.Emitters.Add(this);
                if (!_emitters.ContainsKey(Entity.EntityId)) _emitters.Add(Entity.EntityId, this);
                StorageSetup();
                CreateUi();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Emitter.Storage;
            LoadSettings();
            UpdateSettings(Settings, false);
        }

        public override void UpdateBeforeSimulation()
        {
            _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (SGridComponent == null) MainInit = false;
            if (!MainInit)
            {
                Emitter.CubeGrid.Components.TryGet(out SGridComponent);
                if (SGridComponent == null)
                {
                    return;
                }
                MainInit = true;
                Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (!Session.DedicatedServer) BlockParticleCreate();
                Log.Line($"Emitter initted");
            }

            _sVelSqr = Emitter.CubeGrid.Physics.LinearVelocity.LengthSquared();
            if (SGridComponent.IsStarting)
            {
                CheckShieldLineOfSight();
                if (!_shieldLineOfSight) SGridComponent.OffEmitters.Add(this);
            }
            if (_shieldLineOfSight == false && !Session.DedicatedServer) DrawHelper();

            if (SGridComponent.ControlBlockWorking)
            {
                if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(); //if shield is active

                if ((!Session.DedicatedServer) && UtilsStatic.ShieldDistanceCheck(Emitter, 1000, SGridComponent.BoundingRange))
                {
                    if (SGridComponent.IsMoving || SGridComponent.IsStarting) BlockParticleUpdate();
                    var blockCam = Emitter.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam))
                    {
                        if (_blockParticleStopped) BlockParticleStart();
                        _blockParticleStopped = false;
                        BlockMoveAnimation();

                        if (_animationLoop++ == 599) _animationLoop = 0;
                    }
                }
            }
            else
            {
                if (!_blockParticleStopped) BlockParticleStop();
            }

            if (ServerUpdate) SyncControlsServer();
            SyncControlsClient();
        }

        #region Create UI
        private void CreateUi()
        {
            Session.Instance.ControlsLoaded = true;
            _modulateVoxels = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Emitter, "AllowVoxels", "Voxels may pass", true);
            _modulateGrids = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(Emitter, "AllowGrids", "Grids may pass", false);
        }
        #endregion

        #region Settings
        public bool Enabled
        {
            get { return Settings.Enabled; }
            set { Settings.Enabled = value; }
        }

        public bool ModulateVoxels
        {
            get { return Settings.ModulateVoxels; }
            set { Settings.ModulateVoxels = value; }
        }

        public bool ModulateGrids
        {
            get { return Settings.ModulateGrids; }
            set { Settings.ModulateGrids = value; }
        }

        public void UpdateSettings(EmitterSettings newSettings, bool localOnly = true)
        {
            Enabled = newSettings.Enabled;
            ModulateVoxels = newSettings.ModulateVoxels;
            ModulateGrids = newSettings.ModulateGrids;
            if (Session.Enforced.Debug == 1) Log.Line($"UpdateSettings for emitter");
        }

        public void SaveSettings()
        {
            if (Emitter.Storage == null)
            {
                Log.Line($"EmitterId:{Emitter.EntityId.ToString()} - Storage = null");
                Emitter.Storage = new MyModStorageComponent();
            }
            Emitter.Storage[Session.Instance.EmitterGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);
        }

        public bool LoadSettings()
        {
            if (Emitter.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Emitter.Storage.TryGetValue(Session.Instance.EmitterGuid, out rawData))
            {
                EmitterSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<EmitterSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"EmitterId:{Emitter.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }
            }
            return loadedSomething;
        }

        private void SyncControlsServer()
        {
            if (Emitter != null && !Emitter.Enabled.Equals(Settings.Enabled))
            {
                Enabled = Settings.Enabled;
            }

            if (_modulateVoxels != null && !_modulateVoxels.Getter(Emitter).Equals(Settings.ModulateVoxels))
            {
                _modulateVoxels.Setter(Emitter, Settings.ModulateVoxels);
            }

            if (_modulateGrids != null && !_modulateGrids.Getter(Emitter).Equals(Settings.ModulateGrids))
            {
                _modulateGrids.Setter(Emitter, Settings.ModulateGrids);
            }

            ServerUpdate = false;
            SaveSettings();
            if (Session.Enforced.Debug == 1) Log.Line($"SyncControlsServer (emitter)");
        }

        private void SyncControlsClient()
        {
            var needsSync = false;
            if (!Enabled.Equals(Enabled) 
                || !_modulateVoxels.Getter(Emitter).Equals(ModulateVoxels)
                || !_modulateGrids.Getter(Emitter).Equals(ModulateGrids))
            {
                needsSync = true;
                Enabled = Settings.Enabled;
                ModulateVoxels = _modulateVoxels.Getter(Emitter);
                ModulateGrids = _modulateGrids.Getter(Emitter);
            }

            if (needsSync)
            {
                NetworkUpdate();
                SaveSettings();
                if (Session.Enforced.Debug == 1) Log.Line($"Needed sync for emitter");
            }
        }
        #endregion

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            _time -= 1;
            if (_animationLoop == 0) _time2 = 0;
            if (_animationLoop < 299) _time2 += 1;
            else _time2 -= 1;
            if (_count == 0) _emissiveIntensity = 2;
            if (_count < 30) _emissiveIntensity += 1;
            else _emissiveIntensity -= 1;

            var temp1 = MatrixD.CreateRotationY(0.05f * _time);
            var temp2 = MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * _time2, 0);

            _subpartRotor.PositionComp.LocalMatrix = temp1 * temp2;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }

        private void BlockParticleCreate()
        {
            var scale = Definition.ParticleScale;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] == null)
                {
                    if (Session.Enforced.Debug == 1) Log.Line($"Particle #{i.ToString()} is null, creating - tick:{_tick.ToString()}");
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    if (_effects[i] == null) continue;
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = scale;
                    _effects[i].UserEmitterScale = 1f;
                }

                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
                    _effects[i].Stop();
                    _blockParticleStopped = true;
                }
            }
        }

        private void BlockParticleUpdate()
        {

            var testDist = Definition.ParticleDist;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = Emitter.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = Emitter.PositionComp.WorldMatrix;
            predictedMatrix.Translation = spawnPos;
            if (_sVelSqr > 4000) predictedMatrix.Translation = spawnPos + Emitter.CubeGrid.Physics.GetVelocityAtPoint(Emitter.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            for (int i = 0; i < _effects.Length; i++)
                if (_effects[i] != null)
                {
                    _effects[i].WorldMatrix = predictedMatrix;
                }
        }

        private void BlockParticleStop()
        {
            _blockParticleStopped = true;
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                {
                    _effects[i].Stop();
                    _effects[i].Close(false, true);
                }
            }

        }

        private void BlockParticleStart()
        {
            var scale = Definition.ParticleScale;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (!_effects[i].IsStopped) continue;

                MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                _effects[i].UserScale = 1f;
                _effects[i].UserRadiusMultiplier = scale;
                _effects[i].UserEmitterScale = 1f;
                BlockParticleUpdate();
            }
        }
        #endregion

        private void CheckShieldLineOfSight()
        {

            // Get If Grid is Mobile and PhysicsVerts

            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            var testDist = Definition.FieldDist;

            var testDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = Emitter.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;

            MyAPIGateway.Parallel.For(0, SGridComponent.PhysicsOutside.Length, i =>
            {
                var hit = Emitter.CubeGrid.RayCastBlocks(testPos, SGridComponent.PhysicsOutside[i]);
                if (hit.HasValue)
                {
                    _blocksLos.Add(i);
                    return;
                }
                _noBlocksLos.Add(i);
            });
            if (SGridComponent.GridIsMobile)
            {
                MyAPIGateway.Parallel.For(0, _noBlocksLos.Count, i =>
                {
                    const int filter = CollisionLayers.VoxelCollisionLayer;
                    IHitInfo hitInfo;
                    var hit = MyAPIGateway.Physics.CastRay(testPos, SGridComponent.PhysicsOutside[_noBlocksLos[i]], out hitInfo, filter);
                    if (hit) _blocksLos.Add(_noBlocksLos[i]);
                });
            }

            for (int i = 0; i < SGridComponent.PhysicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
            _shieldLineOfSight = _blocksLos.Count < 500;
            if (Session.Enforced.Debug == 1) Log.Line($"EmitterId:{Emitter.EntityId.ToString()} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {_shieldLineOfSight.ToString()}");
        }

        private void DrawHelper()
        {
            const float lineWidth = 0.025f;
            var lineDist = Definition.HelperDist;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = SGridComponent.PhysicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = SGridComponent.PhysicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("The shield emitter DOES NOT have a CLEAR ENOUGH LINE OF SIGHT to the shield, SHUTTING DOWN.", 960, "Red", Emitter.OwnerId);
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("Blue means clear line of sight, black means blocked......................................................................", 960, "Red", Emitter.OwnerId);
        }

        #region Network
        private void NetworkUpdate()
        {

            if (Session.IsServer)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"server relaying network settings update for emitter {Emitter.EntityId}");
                Session.PacketizeEmitterSettings(Emitter, Settings); // update clients with server's settings
            }
            else // client, send settings to server
            {
                if (Session.Enforced.Debug == 1) Log.Line($"client sent network settings update for emitter {Emitter.EntityId}");
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new EmitterData(MyAPIGateway.Multiplayer.MyId, Emitter.EntityId, Settings));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID_EMITTER, bytes);
            }
        }
        #endregion
        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
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

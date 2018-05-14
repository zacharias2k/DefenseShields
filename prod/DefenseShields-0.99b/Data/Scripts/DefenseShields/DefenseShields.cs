using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
using System;
using Sandbox.ModAPI.Weapons;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Entity;
using System.Linq;					   
using DefenseShields.Control;
using VRage.Collections;
using Sandbox.Game.Entities.Character.Components;
using DefenseShields.Support;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Definitions;							 
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.Models;
using VRage.Voxels;
using VRageRender;
using VRageRender.Utils;				  						
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;


public static class MathematicalConstants
{
    public const double Sqrt2 = 1.414213562373095048801688724209698078569671875376948073176679737990732478462107038850387534327641573;
    public const double Sqrt3 = 1.7320508075689d;
}

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "DefenseShieldsLS", "DefenseShieldsSS", "DefenseShieldsST")]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private const ulong ModId = 41000;
										 
        private uint _tick;

        public float ImpactSize { get; set; } = 9f;
        public float Absorb { get; set; }
        private float _power = 0.0001f;
        private float _width;
        private float _height;
        private float _depth;
        private float _gridMaxPower;
        private float _gridCurrentPower;
        private float _gridAvailablePower;
        private float _shieldMaxBuffer;
        private float _shieldBuffer;
        private float _shieldMaxChargeRate;
        private float _shieldChargeRate;
		private float _shieldDps;						 
        private float _shieldEfficiency;
        private float _shieldCurrentPower;
        private float _shieldMaintaintPower;
        private float _shieldPercent;									 

        internal double Range;							  
        private double _sAvelSqr;
        private double _sVelSqr;
        private double _ellipsoidSurfaceArea;

        public int BulletCoolDown { get; private set; }= -1;
        public int EntityCoolDown { get; private set; } = -1;
        private int _count = -1;
        private int _shieldDownLoop = -1;
        private int _longLoop;
        private int _animationLoop;
        private int _time;
        private int _time2;
        private int _emissiveIntensity;
        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;

        internal const bool Debug = true;
        internal bool MainInit;
        internal bool AnimateInit;
        internal bool DefinitionsLoaded;					 
        internal bool GridIsMobile;
        internal bool ShieldActive;
        internal bool BlockWorking;
        internal bool HardDisable { get; private set; }
        internal bool NoPower;
        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _enablePhysics = true;
        private bool _shieldMoving = true;
        private bool _blocksChanged = true;
        private bool _blockParticleStopped;
        private bool _shieldLineOfSight;
        private bool _prevShieldActive;
        private bool _shieldStarting;
        private bool _enemy;
        private bool _effectsCleanup;

        internal Vector3D ShieldSize { get; set; }
        public Vector3D WorldImpactPosition { get; set; } = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _localImpactPosition;
        private Vector3D _detectionCenter;
        private Vector3D _sightPos;

        public readonly Vector3D[] PhysicsOutside = new Vector3D[642];
        public readonly Vector3D[] PhysicsOutsideLow = new Vector3D[162];
        public readonly Vector3D[] PhysicsInside = new Vector3D[642];

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;									  										 
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixOutsideInv;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectInsideInv;

        private BoundingBox _oldGridAabb;
        private BoundingBox _shieldAabb;
        private BoundingSphereD _shieldSphere;
        private MyOrientedBoundingBoxD _sOriBBoxD;
        private Quaternion _sQuaternion;

        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<KeyValuePair<IMyEntity, EntIntersectInfo>> _webEntsTmp = new List<KeyValuePair<IMyEntity, EntIntersectInfo>>();
        private ListReader<MyTransparentMaterialDefinition> _transMatDef = new List<MyTransparentMaterialDefinition>();

        static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly DataStructures _dataStructures = new DataStructures();
        private readonly StructureBuilder _structureBuilder = new StructureBuilder();
        private readonly ResourceTracker _resourceTracker = new ResourceTracker(MyResourceDistributorComponent.ElectricityId);

        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();
		
        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();
        public readonly MyConcurrentHashSet<IMyEntity> FriendlyCache = new MyConcurrentHashSet<IMyEntity>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();
        private readonly Dictionary<string, AmmoInfo> _ammoInfo = new Dictionary<string, AmmoInfo>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private readonly MyConcurrentQueue<IMySlimBlock> _dmgBlocks  = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyEntity> _missileDmg = new MyConcurrentQueue<IMyEntity>();
        private readonly MyConcurrentQueue<IMyMeteor> _meteorDmg = new MyConcurrentQueue<IMyMeteor>();
        private readonly MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyCubeGrid> _staleGrids = new MyConcurrentQueue<IMyCubeGrid>();
        private readonly MyConcurrentQueue<IMyCharacter> _characterDmg = new MyConcurrentQueue<IMyCharacter>();
		private readonly MyConcurrentQueue<MyVoxelBase> _voxelDmg = new MyConcurrentQueue<MyVoxelBase>();																								 


        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;

        private readonly MyParticleEffect[] _effects = new MyParticleEffect[1];
        private MyEntitySubpart _subpartRotor;

        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _chargeSlider;
        private RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> _visablilityCheckBox;

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        public IMyOreDetector Shield => (IMyOreDetector)Entity;
        public MyEntity _shield;
        private MyEntity _shellPassive;
        private MyEntity _shellActive;									   


        private DSUtils _dsutil1 = new DSUtils();
        private DSUtils _dsutil2 = new DSUtils();
        private DSUtils _dsutil3 = new DSUtils();

        // tem
        private bool needsMatrixUpdate = false;
        internal DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        private bool blocksNeedRefresh = false;
        public const float MIN_SCALE = 15f; // Scale slider min/max
        public const float MAX_SCALE = 300f;
        public float LargestGridLength = 2.5f;
        public static MyModStorageComponent Storage { get; set; } // broken, shouldn't be static.  Move to Session if possible.
        private HashSet<ulong> playersToReceive = null;
        // 			  
        #endregion
		
        #region Cleanup
        public override void OnAddedToScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    Log.Line("Entity not closed in OnAddedToScene - gridSplit?.");
                    return;
                }
                Log.Line("Entity closed in OnAddedToScene.");
                DefenseShieldsBase.Instance.Components.Add(this);
                _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere);
                Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                Log.Line("OnremoveFromScene");
                if (!Entity.MarkedForClose)
                {
                    Log.Line("Entity not closed in OnRemovedFromScene- gridSplit?.");
                    return;
                }
                Log.Line("Entity closed in OnRemovedFromScene.");
                _power = 0f;
                if (MainInit) Sink.Update();
                _icosphere = null;
                _shield.Close();								
                BlockParticleStop();
                Shield.CubeGrid.Components.Remove(typeof(ShieldGridComponent), this);
                DefenseShieldsBase.Instance.Components.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { Log.Line("OnBeforeRemovedFromContainer"); if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                Log.Line("Close");
                if (DefenseShieldsBase.Instance.Components.Contains(this)) DefenseShieldsBase.Instance.Components.Remove(this);
                //DefenseShieldsBase.Instance.Components.RemoveAt(DefenseShieldsBase.Instance.Components.IndexOf(this));
                _power = 0f;
                _icosphere = null;
                _shield.Close();								  

                if (MainInit) Sink.Update();
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
        #endregion

        #region constructors and Enums
        private MatrixD DetectionMatrix
        {
            get { return _detectMatrixOutside; }
            set
            {									 														 
                _detectMatrixOutside = value;
                _detectMatrixOutsideInv = MatrixD.Invert(value);
                _detectMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
                _detectInsideInv = MatrixD.Invert(_detectMatrixInside);
            }
        }

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
            VoxelBase
        };
        #endregion

        #region Startup Logic
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                Log.Line($"Starting Init for {Entity.EntityId.ToString()}");
                Entity.Components.TryGet(out Sink);
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.RemoveType(ref ResourceInfo.ResourceTypeId);
                Sink.Init(MyStringHash.GetOrCompute("Defense"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);

                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                if (!(_shields).ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!DefinitionsLoaded && AnimateInit && _tick > 300)
                {
                    DefinitionsLoaded = true;
                    GetDefinitons();
                }										  
                if (AnimateInit && MainInit || !Shield.IsFunctional) return;

                HardDisable = false || (Shield.EntityId != ThereCanBeOnlyOne() || (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" && !Shield.CubeGrid.Physics.IsStatic) 
                                                                               || (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic));
                NoPower = false;
                if (!HardDisable) AddResourceSourceComponent();

                if (HardDisable)
                {
                    var realPlayerIds = new List<long>();
                    DsUtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds)
                    {
                        if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST" && !Shield.CubeGrid.Physics.IsStatic)
                            MyVisualScriptLogicProvider.ShowNotification("Station shields only allowed on stations", 1600, "Red", id);
                        else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && Shield.CubeGrid.Physics.IsStatic)
                            MyVisualScriptLogicProvider.ShowNotification("Large Ship Shields only allowed on ships, not stations", 1600, "Red", id);
                        else if (NoPower) MyVisualScriptLogicProvider.ShowNotification("Insufficent power to bring Shield online", 1600, "Red", id);
                    }
                    return;
                }

                if (_icosphere == null) 
                {
                    Log.Line($"_icosphere Null!");
                    _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere);
                    if (!DefenseShieldsBase.Instance.Components.Contains(this)) DefenseShieldsBase.Instance.Components.Add(this);
                    if (!Shield.CubeGrid.Components.Has<ShieldGridComponent>()) Shield.CubeGrid.Components.Add(new ShieldGridComponent(this));
                }

                if (!MainInit && Shield.IsFunctional)
                {
                    Log.Line($"Initting {Shield.BlockDefinition.SubtypeId} - tick:{_tick.ToString()}");
                    if (Shield.CubeGrid.Physics.IsStatic) GridIsMobile = false;
                    else if (!Shield.CubeGrid.Physics.IsStatic) GridIsMobile = true;

                    CreateUi();
					
                    _shield = _spawn.EmptyEntity("dShield", null, (MyEntity)Shield, false);
                    _shield.Render.CastShadows = false;
                    _shield.Render.RemoveRenderObjects();
                    _shield.Render.UpdateRenderObject(true);
                    _shield.Render.Visible = true;
                    _shield.Save = false;

                    Shield.AppendingCustomInfo += AppendingCustomInfo;
                    Shield.RefreshCustomInfo();

                    MainInit = true;
                }

                if (AnimateInit || !MainInit || !Shield.IsFunctional) return;

                if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                {
                    _blocksChanged = true;
                    Log.Line($"{Shield.BlockDefinition.SubtypeId} is functional - tick:{_tick.ToString()}");
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);

                    BlockParticleCreate();
                    if (GridIsMobile) MobileUpdate();
                    else RefreshDimensions();

                    _icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);

                    AnimateInit = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation100: {ex}"); }
        }

        private void AddResourceSourceComponent()
        {
            try
            {
                if (!Sink.IsPowerAvailable(GId, _power))
                {
                    Log.Line($"no power to init resourceSink: {_power.ToString()}");
                    NoPower = true;
                    HardDisable = true;
                    return;
                }
                HardDisable = false;
                NoPower = false;
                UpdateGridPower();
                CalculatePowerCharge();
                SetPower();
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private long ThereCanBeOnlyOne()
        {
            var gridStatic = Shield.CubeGrid.Physics.IsStatic;
            var shieldBlocks = new List<MyCubeBlock>();
            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                if (block.BlockDefinition.BlockPairName.Equals("DefenseShield") || block.BlockDefinition.BlockPairName.Equals("StationShield"))
                {
                    if (gridStatic && Shield.BlockDefinition.SubtypeId == "DefenseShieldsST")
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                    else if (!gridStatic && (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS"))
                    {
                        if (block.IsWorking) return block.EntityId;
                        shieldBlocks.Add(block);
                    }
                }
            }
            var shieldDistFromCenter = double.MinValue;
            var shieldId = long.MinValue;
            foreach (var shield in shieldBlocks)
            {
                var dist = Vector3D.DistanceSquared(shield.PositionComp.WorldVolume.Center, Shield.CubeGrid.WorldVolume.Center);
                if (dist > shieldDistFromCenter)
                {
                    shieldDistFromCenter = dist;
                    shieldId = shield.EntityId;
                }
            }
            return shieldId;
        }

        private void CheckShieldLineOfSight()
        {
            if (GridIsMobile)
            {
                MobileUpdate();
                _icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);
            }
            else RefreshDimensions();

            var testDist = 0d;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") testDist = 4.5d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") testDist = 0.8d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") testDist = 8.0d;

            var testDir = _subpartRotor.PositionComp.WorldVolume.Center - Shield.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = Shield.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;
            
            MyAPIGateway.Parallel.For(0, PhysicsOutside.Length, i =>
            {
                var hit = Shield.CubeGrid.RayCastBlocks(testPos, PhysicsOutside[i]);
                if (hit.HasValue)
                {
                    _blocksLos.Add(i);
                    return;
                }
                _noBlocksLos.Add(i);
            });
            if (GridIsMobile)
            {
                MyAPIGateway.Parallel.For(0, _noBlocksLos.Count, i =>
                {
                    const int filter = CollisionLayers.VoxelCollisionLayer;
                    IHitInfo hitInfo;
                    var hit = MyAPIGateway.Physics.CastRay(testPos, PhysicsOutside[_noBlocksLos[i]], out hitInfo, filter);
                    if (hit) _blocksLos.Add(_noBlocksLos[i]);
                });
            }

            for (int i = 0; i < PhysicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
            _shieldLineOfSight = _blocksLos.Count < 500;
            Log.Line($"blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {_shieldLineOfSight.ToString()}");
        }

        private void DrawHelper()
        {
            var lineDist = 0d;
            const float lineWidth = 0.025f;
            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS") lineDist = 5.0d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") lineDist = 3d;
            else if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsST") lineDist = 7.5d;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = PhysicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = PhysicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("The shield emitter DOES NOT have a CLEAR ENOUGH LINE OF SIGHT to the shield, SHUTTING DOWN.", 960, "Red", Shield.OwnerId);
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("Blue means clear line of sight, black means blocked......................................................................", 960, "Red", Shield.OwnerId);
        }
		
		private bool FieldShapeBlocked()
        {
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            MobileUpdate();
            _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null) continue;

                if (!CustomCollision.VoxelContact(Shield.CubeGrid, PhysicsOutsideLow, voxel, new MyStorageData(), _detectMatrixOutside)) continue;

                Shield.Enabled = false;
                MyVisualScriptLogicProvider.ShowNotification("The shield's field cannot form when in contact with a solid body", 6720, "Blue", Shield.OwnerId);
                return true;
            }
            return false;
        }

        private void GetDefinitons()
        {
            try
            {
                var defintions = MyDefinitionManager.Static.GetAllDefinitions();
                Log.Line($"Getting Definitions");
                foreach (var def in defintions)
                {
                    if (!(def is MyAmmoMagazineDefinition)) continue;
                    var ammoDef = def as MyAmmoMagazineDefinition;
                    //if (ammoDef.Context.IsBaseGame) continue;
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(ammoDef.AmmoDefinitionId);
                    if (!(ammo is MyMissileAmmoDefinition)) continue;					
                    var shot = ammo as MyMissileAmmoDefinition;
                    if (_ammoInfo.ContainsKey(shot.MissileModelName)) continue;
                    _ammoInfo.Add(shot.MissileModelName, new AmmoInfo(shot.IsExplosive, shot.MissileExplosionDamage, shot.MissileExplosionRadius, shot.DesiredSpeed, shot.MissileMass, shot.BackkickForce));
                }
            }
            catch (Exception ex) { Log.Line($"Exception in GetAmmoDefinitions: {ex}"); }
        }		
        #endregion
		
        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void RemoveOreUi()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;
        }

        private void CreateUi()
        {
            Log.Line($"Create UI - Tick:{_tick.ToString()}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _chargeSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "ChargeRate", "Shield Charge Rate", 20, 95, 50);
            _visablilityCheckBox = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "Visability", "Hide Shield From Allied", false);

            if (Shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" || Shield.BlockDefinition.SubtypeId == "DefenseShieldsSS") return;

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "WidthSlider", "Shield Size Width", 30, 300, 100);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "HeightSlider", "Shield Size Height", 30, 300, 100);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Shield, "DepthSlider", "Shield Size Depth", 30, 300, 100);
        }				 
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
				_dsutil2.Sw.Restart();					  
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (!BlockFunctional()) return;

                if (GridIsMobile) MobileUpdate();


                if (_longLoop == 0 && _blocksChanged)
                {
                    MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
                    CheckShieldLineOfSight();
                    _blocksChanged = false;
                }

                if (_shieldLineOfSight == false) DrawHelper();

                ShieldActive = BlockWorking && _shieldLineOfSight;
                if (_prevShieldActive == false && BlockWorking) _shieldStarting = true;
                else if (_shieldStarting && _prevShieldActive && ShieldActive) _shieldStarting = false;
                _prevShieldActive = ShieldActive;

                if (_staleGrids.Count != 0) CleanUp(0);
                if (_longLoop == 9 && _count == 58) CleanUp(1);
                if (_effectsCleanup && (_count == 1 || _count == 21 || _count == 41)) CleanUp(2);
                if ((_longLoop == 8 && _count == 58) || (_longLoop == 3 && _count == 58 && FriendlyCache.Count > 50)) CleanUp(3); 

                if (_count++ == 59)
                {
                    _count = 0;
                    _longLoop++;
                    if (_longLoop == 10) _longLoop = 0;
                }

                UpdateGridPower();
                CalculatePowerCharge();
                SetPower();

                if (_count == 29)
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                    {
                        Shield.ShowInToolbarConfig = false;
                        Shield.ShowInToolbarConfig = true;
                    }
                    else if (_longLoop == 0 || _longLoop == 5) Shield.ShowInToolbarConfig = false; Shield.ShowInToolbarConfig = true; Shield.RefreshCustomInfo();
					_shieldDps = 0f;				
                }
				if (_shieldStarting && GridIsMobile && FieldShapeBlocked()) return;																   

                if (ShieldActive)
                {
                    if (_shieldStarting)
                    {
                        _shield.Render.Visible = true;
                        _shield.Render.UpdateRenderObject(true);
                        Log.Line($"starting");
                    }							  
                    if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
                    if (Distance(1000))
                    {
                        if (_shieldMoving || _shieldStarting) BlockParticleUpdate();
                        var blockCam = Shield.PositionComp.WorldVolume;
                        if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam))
                        {
                            if (_blockParticleStopped) BlockParticleStart();
                            _blockParticleStopped = false;
                            BlockMoveAnimation();

                            if (_animationLoop++ == 599) _animationLoop = 0;
                        }
                    }
                    SyncThreadedEnts();
                    _enablePhysics = false;
                    WebEntities();
                }
                else
                {
                    SyncThreadedEnts();
                    if (!_blockParticleStopped) BlockParticleStop();
                }
                _dsutil2.StopWatchReport("main-loop perf", 4);

            }
            catch (Exception ex) {Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }
        #endregion

        #region Shield Shape
        private void MobileUpdate()
        {
            _sVelSqr = Shield.CubeGrid.Physics.LinearVelocity.LengthSquared();
            _sAvelSqr = Shield.CubeGrid.Physics.AngularVelocity.LengthSquared();
            if (_sVelSqr > 0.00001 || _sAvelSqr > 0.00001 || _shieldStarting) _shieldMoving = true;
            else _shieldMoving = false;

            _gridChanged = _oldGridAabb != Shield.CubeGrid.LocalAABB;
            _oldGridAabb = Shield.CubeGrid.LocalAABB;
            _entityChanged = Shield.CubeGrid.Physics.IsMoving || _gridChanged || _shieldStarting;
            if (_entityChanged || Range <= 0 || _shieldStarting) CreateShieldShape();
        }

        private void CreateShieldShape()
        {
            if (GridIsMobile)
            {
                _shieldGridMatrix = Shield.CubeGrid.WorldMatrix;
                if (_gridChanged) CreateMobileShape();
                DetectionMatrix = _shieldShapeMatrix * _shieldGridMatrix;
                _detectionCenter = Shield.CubeGrid.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
                _sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, ShieldSize, _sQuaternion);
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(_detectionCenter, ShieldSize.AbsMax());
                _ellipsoidSurfaceArea = new EllipsoidSA(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z).Surface;
            }
            else
            {
                _shieldGridMatrix = Shield.WorldMatrix;
                DetectionMatrix = MatrixD.Rescale(_shieldGridMatrix, new Vector3D(_width, _height, _depth));
                ShieldSize = DetectionMatrix.Scale;
                _detectionCenter = Shield.PositionComp.WorldVolume.Center;
                _sQuaternion = Quaternion.CreateFromRotationMatrix(Shield.CubeGrid.WorldMatrix);
                _sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, ShieldSize, _sQuaternion);
                _shieldAabb = new BoundingBox(ShieldSize, -ShieldSize);
                _shieldSphere = new BoundingSphereD(_detectionCenter, ShieldSize.AbsMax());
                _ellipsoidSurfaceArea = new EllipsoidSA(_detectMatrixOutside.Scale.X, _detectMatrixOutside.Scale.Y, _detectMatrixOutside.Scale.Z).Surface;																																						  
            }
            Range = ShieldSize.AbsMax() + 7.5f;
            SetShieldShape();
        }

        private void CreateMobileShape()
        {
            Vector3D gridHalfExtents = Shield.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const double ellipsoidAdjust = MathematicalConstants.Sqrt2;
            const double buffer = 2.5d;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            ShieldSize = shieldSize;
            var gridLocalCenter = Shield.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize) * MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Shield.CubeGrid.PositionComp.LocalVolume.Center;
            _shieldShapeMatrix = mobileMatrix;
        }

        private void SetShieldShape()
        {
            _shield.PositionComp.LocalMatrix = Matrix.Zero;

            _shield.PositionComp.LocalMatrix = _shieldShapeMatrix;														   
            _shield.PositionComp.LocalAABB = _shieldAabb;

            var matrix = _shieldShapeMatrix * Shield.WorldMatrix;																			
            _shield.PositionComp.SetWorldMatrix(matrix);											
            _shield.PositionComp.SetPosition(_detectionCenter);
        }

        private void RefreshDimensions()
        {
            var width = _widthSlider.Getter(Shield);
            var height = _heightSlider.Getter(Shield);
            var depth = _depthSlider.Getter(Shield);
            var oWidth = _width;
            var oHeight = _height;
            var oDepth = _depth;
            _width = width;
            _height = height;
            _depth = depth;
            var changed = (int)oWidth != (int)width || (int)oHeight != (int)height || (int)oDepth != (int)depth;
            if (!changed) return;
            CreateShieldShape();

            _icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);																		   
            _entityChanged = true;
        }
        #endregion
		
        #region Block Power Logic
        private bool BlockFunctional()
        {

            if (!MainInit || !AnimateInit || NoPower || HardDisable) return false;
            var shieldPowerUsed = Sink.CurrentInputByType(GId);

            if (((MyCubeGrid)Shield.CubeGrid).GetFatBlocks().Count < 2 && ShieldActive)
            {
                MyVisualScriptLogicProvider.CreateExplosion(Shield.PositionComp.WorldVolume.Center, (float)Shield.PositionComp.WorldVolume.Radius * 1.25f, 2500);
                return false;
            }

            if (!Shield.IsWorking && Shield.Enabled && Shield.IsFunctional && shieldPowerUsed > 0)
            {
                Log.Line($"fixing shield state power: {_power.ToString()})");
                Shield.Enabled = false;
                Shield.Enabled = true;
                return true;
            }

            if ((!Shield.IsWorking || !Shield.IsFunctional || _shieldDownLoop > -1))
            {   
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                UpdateGridPower();

                BlockParticleStop();
                ShieldActive = false;
                BlockWorking = false;
				_prevShieldActive = false;						  
                _shield.Render.Visible = false;
                _shield.Render.UpdateRenderObject(false);														 
                Absorb = 0;
                _shieldBuffer = 0;
                _shieldChargeRate = 0;
                _shieldMaxChargeRate = 0;
                if (_shieldDownLoop > -1)
                {
                    _power = _gridMaxPower * _shieldMaintaintPower;
                    if (_power < 0 || float.IsNaN(_power)) _power = 0.0001f; // temporary definitely 100% will fix this to do - Find ThE NaN!
                    Sink.Update();
                    if (_shieldDownLoop == 0)
                    {
                        Log.Line($"Shield restart");
                        var realPlayerIds = new List<long>();
                        DsUtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                        foreach (var id in realPlayerIds)
                        {
                            MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 19200, "Red", id);
                        }
                    }

                    _shieldDownLoop++;
                    if (_shieldDownLoop == 1200)
                    {
                        _shieldDownLoop = -1;
                        _shieldBuffer = _shieldMaxBuffer / 25; // replace this with something that scales based on charge rate
                    }
                    return false;
                }
                _power = 0.0001f;
                Sink.Update();
                return false;
            }

            var blockCount = ((MyCubeGrid)Shield.CubeGrid).BlocksCount;
            if (!_blocksChanged) _blocksChanged = blockCount != _oldBlockCount;
            _oldBlockCount = blockCount;

            BlockWorking = MainInit && AnimateInit && Shield.IsWorking && Shield.IsFunctional;
            return BlockWorking;
        }

        private void BackGroundChecks()
        {
            lock (_powerSources) _powerSources.Clear();
            lock (_functionalBlocks) _functionalBlocks.Clear();

            foreach (var block in ((MyCubeGrid)Shield.CubeGrid).GetFatBlocks())
            {
                lock (_functionalBlocks) if (block.IsFunctional) _functionalBlocks.Add(block);
                var source = block.Components.Get<MyResourceSourceComponent>();
                if (source == null) continue;
                foreach (var type in source.ResourceTypes)
                {
                    if (type != MyResourceDistributorComponent.ElectricityId) continue;
                    lock (_powerSources) _powerSources.Add(source);
                    break;
                }
            }
            Log.Line($"powerCnt: {_powerSources.Count.ToString()}");
        }

        private void UpdateGridPower()
        {
            _gridMaxPower = 0;
            _gridCurrentPower = 0;

            lock (_powerSources)
                for (int i = 0; i < _powerSources.Count; i++)
                {
                    var source = _powerSources[i];
                    if (!source.Enabled || !source.ProductionEnabled) continue;
                    _gridMaxPower += source.MaxOutput;
                    _gridCurrentPower += source.CurrentOutput;
                }
            _gridAvailablePower = _gridMaxPower - _gridCurrentPower;
        }

        private void CalculatePowerCharge()
        {
            var powerForShield = 0f;
            const float ratio = 1.25f;
            var rate = _chargeSlider?.Getter(Shield) ?? 0f;
            var percent = rate * ratio;
            var shieldMaintainCost = 1 / percent;
            _shieldMaintaintPower = shieldMaintainCost;
            var fPercent = (percent / ratio) / 100;
            var baseScale = 30;
            var sizeScaler = (_detectMatrixOutside.Scale.Volume / _ellipsoidSurfaceArea) / 2.40063050674088;
            _shieldEfficiency = 100f;

            if (_shieldBuffer > 0 && _shieldCurrentPower < 0.00000000001f) // is this even needed anymore?
            {
                Log.Line($"if u see this it is needed");
                if (_shieldBuffer > _gridMaxPower * shieldMaintainCost) _shieldBuffer -= _gridMaxPower * shieldMaintainCost;
                else _shieldBuffer = 0f;
            }

            _shieldCurrentPower = Sink.CurrentInputByType(GId);

            var otherPower = _gridMaxPower - _gridAvailablePower - _shieldCurrentPower;
            var cleanPower = _gridMaxPower - otherPower;
            powerForShield = cleanPower * fPercent;

            _shieldMaxChargeRate = powerForShield > 0 ? powerForShield : 0f;
            _shieldMaxBuffer = (_gridMaxPower * (100 / percent) * baseScale) / (float)sizeScaler;

            if (_shieldBuffer + _shieldMaxChargeRate < _shieldMaxBuffer) _shieldChargeRate = _shieldMaxChargeRate;
            else
            {
                if (_shieldMaxBuffer - _shieldBuffer > 0) _shieldChargeRate = _shieldMaxBuffer - _shieldBuffer;
                else _shieldMaxChargeRate = 0f;
            }

            if (_shieldMaxChargeRate < 0.001f)
            {
                _shieldChargeRate = 0f;
                if (_shieldBuffer > _shieldMaxBuffer)  _shieldBuffer = _shieldMaxBuffer;
                return;
            }

            if (_shieldBuffer < _shieldMaxBuffer && _count == 29)
            {
                _shieldBuffer += _shieldChargeRate;
            }
            if (_count == 29)
            {
                _shieldPercent = 100f;
                if (_shieldBuffer < _shieldMaxBuffer) _shieldPercent = (_shieldBuffer / _shieldMaxBuffer) * 100;
                else _shieldPercent = 100f;
            }
        }

        private double PowerCalculation(IMyEntity breaching)
        {
            var bPhysics = breaching.Physics;
            var sPhysics = Shield.CubeGrid.Physics;

            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = sPhysics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = sPhysics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = bPhysics.LinearVelocity;
            var linearImpulse = bPhysics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }															

        private void SetPower()
        {
            _power = _shieldChargeRate + _gridMaxPower * _shieldMaintaintPower;
            if (_power <= 0 || float.IsNaN(_power)) _power = 0.0001f; // temporary definitely 100% will fix this to do - Find ThE NaN!

            Sink.Update();

            _shieldCurrentPower = Sink.CurrentInputByType(GId);
            if (Absorb > 0)
            {
				_shieldDps += Absorb;					 
                //Log.Line($"Absorb Damage: {(Absorb).ToString()} - old: {_shieldBuffer.ToString()} - new: {(_shieldBuffer - (Absorb / _shieldEfficiency)).ToString()} - max: {_shieldMaxBuffer * _shieldEfficiency} - fracOfMax: {_shieldMaxBuffer / Absorb}");
                _effectsCleanup = true;
                _shieldBuffer -= (Absorb / _shieldEfficiency);
            }
			else if (Absorb < 0) _shieldBuffer += (Absorb / _shieldEfficiency);																   

            if (_shieldBuffer < 0) _shieldDownLoop = 0;
            else if (_shieldBuffer > _shieldMaxBuffer) _shieldBuffer = _shieldMaxBuffer;			

            Absorb = 0f;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (!GridIsMobile && ShieldActive)RefreshDimensions();
            var shieldPercent = 100f;
            var secToFull = 0;
            if (_shieldBuffer < _shieldMaxBuffer) shieldPercent = (_shieldBuffer / _shieldMaxBuffer) * 100;
            if (_shieldChargeRate > 0) secToFull = (int) ((_shieldMaxBuffer - _shieldBuffer) / _shieldChargeRate);
            stringBuilder.Append("[Shield Status] MaxHP: " + (_shieldMaxBuffer * _shieldEfficiency).ToString("N0") +
                                 "\n" +
                                 "\n[Shield HP__]: " + (_shieldBuffer * _shieldEfficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                 "\n[HP Per Sec_]: " + (_shieldChargeRate * _shieldEfficiency).ToString("N0") +
                                 "\n[DPS_______]: " + (_shieldDps).ToString("N0") +																				   
                                 "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                 "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                 "\n[Efficiency__]: " + _shieldEfficiency.ToString("0.0") +
                                 "\n[Maintenance]: " + (_gridMaxPower * _shieldMaintaintPower).ToString("0.0") + " Mw" +
                                 "\n[Availabile]: " + _gridAvailablePower.ToString("0.0") + " Mw" +
                                 "\n[Current__]: " + Sink.CurrentInputByType(GId).ToString("0.0"));
        }
        #endregion				  

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
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
            var temp2 = MatrixD.CreateTranslation(0, 0.002f * _time2, 0);
            _subpartRotor.PositionComp.LocalMatrix = temp1 * temp2;
            _subpartRotor.SetEmissiveParts("PlasmaEmissive", Color.Aqua, 0.1f * _emissiveIntensity);
        }

        private void BlockParticleCreate()
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] == null)
                {
                    Log.Line($"Particle #{i.ToString()} is null, creating - tick:{_tick.ToString()}");
                    MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                    _effects[i].UserScale = 1f;
                    _effects[i].UserRadiusMultiplier = 10f;
                    _effects[i].UserEmitterScale = 1f;
                }

                if (_effects[i] != null)
                {
                    Log.Line($"Particle #{i.ToString()} exists, updating - tick:{_tick.ToString()}");

                    _effects[i].WorldMatrix = _subpartRotor.WorldMatrix;
                    _effects[i].Stop();
                    _blockParticleStopped = true;
                }
            }
        }

        private void BlockParticleUpdate()
        {
            var predictedMatrix = Shield.PositionComp.WorldMatrix;
            if (_sVelSqr > 4000) predictedMatrix.Translation = Shield.PositionComp.WorldMatrix.Translation + Shield.CubeGrid.Physics.GetVelocityAtPoint(Shield.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
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
            for (int i = 0; i < _effects.Length; i++)
            {
                if (!_effects[i].IsStopped) continue;

                MyParticlesManager.TryCreateParticleEffect("EmitterEffect", out _effects[i]);
                _effects[i].UserScale = 1f;
                _effects[i].UserRadiusMultiplier = 10f;
                _effects[i].UserEmitterScale = 1f;
                BlockParticleUpdate();
            }
        }
        #endregion

        #region Sync Entiteis
        private void CleanUp(int task)
        {
            try
            {
                switch (task)
                {
                    case 0:
                        IMyCubeGrid grid;
                        while (_staleGrids.TryDequeue(out grid)) lock (_webEnts) _webEnts.Remove(grid);

                        Log.Line($"Stale grid - tick:{_tick.ToString()}");
                        break;
                    case 1:
                        lock (_webEnts)
                        {
                            if (Debug) Log.Line($"_webEnts # {_webEnts.Count.ToString()}");
                            _webEntsTmp.AddRange(_webEnts.Where(info => _tick - info.Value.FirstTick > 599 && _tick - info.Value.LastTick > 1));							 
                            foreach (var webent in _webEntsTmp) _webEnts.Remove(webent.Key);
                        }
                        break;
                    case 2:
                        lock (_functionalBlocks)
                        {
                            foreach (var funcBlock in _functionalBlocks) funcBlock.SetDamageEffect(false);
                            _effectsCleanup = false;
                        }
                        break;
                    case 3:
                        {
                            Log.Line($"FriendlyCache {FriendlyCache.Count.ToString()}");
                            FriendlyCache.Clear();
                        }
                        break;						   
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CleanUp: {ex}"); }
        }

        private void SyncThreadedEnts()
        {
            try
            {
                if (Eject.Count == 0 && _destroyedBlocks.Count == 0 && _missileDmg.Count == 0 &&
                    _meteorDmg.Count == 0 && _characterDmg.Count == 0 && _fewDmgBlocks.Count == 0 && _dmgBlocks.Count == 0) return;
                _dsutil1.Sw.Restart();																								
                if (Eject.Count != 0)
                {
                    foreach (var e in Eject) e.Key.SetPosition(Vector3D.Lerp(e.Key.GetPosition(), e.Value, 0.1d));
                    Eject.Clear();
                }

                var destroyedLen = _destroyedBlocks.Count;
                try
                {
                    if (destroyedLen != 0)
                    {
                        lock (_webEnts)
                        {
                            IMySlimBlock block;
                            var nullCount = 0;
                            while (_destroyedBlocks.TryDequeue(out block))
                            {
                                if (block?.CubeGrid == null) continue;
                                EntIntersectInfo entInfo;
                                _webEnts.TryGetValue(block.CubeGrid, out entInfo);
                                if (entInfo == null)
                                {
                                    nullCount++;
                                    ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                    continue;
                                }
                                if (nullCount > 0) _webEnts.Remove(block.CubeGrid);
                                entInfo.CacheBlockList.Remove(block);
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in destroyedBlocks: {ex}"); }

                try
                {
                    if (_missileDmg.Count != 0)
                    {
                        IMyEntity ent;
                        while (_missileDmg.TryDequeue(out ent))
                        {
                            if (ent == null || ent.MarkedForClose || ent.Closed) continue;
                            var destObj = ent as IMyDestroyableObject;
                            if (destObj == null) continue;
                            var damage = ComputeAmmoDamage(ent);
                            if (damage <= float.NegativeInfinity)
                            {
                                FriendlyCache.Add(ent);
                                continue;
                            }
							//Log.Line($"{((MyEntity)ent).DebugName} damage: {damage}");														  
                            WorldImpactPosition = ent.PositionComp.WorldVolume.Center;
                            Absorb += damage;
                            destObj.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_meteorDmg.Count != 0)
                    {
                        IMyMeteor meteor;
                        while (_meteorDmg.TryDequeue(out meteor))
                        {
                            if (meteor == null || meteor.MarkedForClose || meteor.Closed) continue;
                            WorldImpactPosition = meteor.PositionComp.WorldVolume.Center;
                            Absorb += 5000;
                            meteor.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

				try
                {
                    if (_voxelDmg.Count != 0)
                    {
                        MyVoxelBase voxel;
                        while (_voxelDmg.TryDequeue(out voxel))
                        {
                            if (voxel == null || voxel.RootVoxel.MarkedForClose || voxel.RootVoxel.Closed) continue;
                            voxel.RootVoxel.RequestVoxelOperationElipsoid(Vector3.One * 1.0f, _detectMatrixOutside, 0, MyVoxelBase.OperationType.Cut);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }
				
                try
                {
                    if (_characterDmg.Count != 0)
                    {
                        IMyCharacter character;
                        while (_characterDmg.TryDequeue(out character))
                        {
                            var npcname = character.ToString();
                            if (npcname.Equals("Space_Wolf"))
                            {
                                character.Delete();
                                continue;
                            }
                            var hId = MyCharacterOxygenComponent.HydrogenId;
                            var playerGasLevel = character.GetSuitGasFillLevel(hId);
                            character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hId, (playerGasLevel * -0.0001f) + .002f);
                            MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
                            character.DoDamage(50f, MyDamageType.Fire, true);
                            var vel = character.Physics.LinearVelocity;
                            if (vel == new Vector3D(0, 0, 0)) vel = MyUtils.GetRandomVector3Normalized();
                            var speedDir = Vector3D.Normalize(vel);
                            var rnd = new Random();
                            var randomSpeed = rnd.Next(10, 20);
                            var additionalSpeed = vel + speedDir * randomSpeed;
                            character.Physics.LinearVelocity = additionalSpeed;
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in missileDmg: {ex}"); }

                try
                {
                    if (_fewDmgBlocks.Count != 0)
                    {
                        IMySlimBlock block;
                        while (_fewDmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }

                            block.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set sync to true for multiplayer?
                            if (((MyCubeGrid)block.CubeGrid).BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in fewBlocks: {ex}"); }
				
                try
                {
                    if (_dmgBlocks.Count != 0)
                    {
                        IMySlimBlock block;
                        while (_dmgBlocks.TryDequeue(out block))
                        {
                            if (block == null) continue;
                            if (block.IsDestroyed)
                            {
                                ((MyCubeGrid)block.CubeGrid).EnqueueDestroyedBlock(block.Position);
                                continue;
                            }
                            block.DoDamage(10000f, MyDamageType.Explosion, true, null, Shield.CubeGrid.EntityId); // set  to true for multiplayer?
                            if (((MyCubeGrid)block.CubeGrid).BlocksCount == 0) block.CubeGrid.SyncObject.SendCloseRequest();
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in dmgBlocks: {ex}"); }
                //dsutil1.StopWatchReport("SyncEnts", -1);														 
            }
            catch (Exception ex) { Log.Line($"Exception in DamageGrids: {ex}"); }
        }
		#endregion
		
        #region Web Entities
        private void WebEntities()
        {
            //_dsutil1.Sw.Start();
            var pruneSphere = new BoundingSphereD(_detectionCenter, Range);
            var pruneList = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);
            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                if (ent == null) continue;
                var entCenter = ent.PositionComp.WorldVolume.Center;

                if (ent == _shield || ent as IMyCubeGrid == Shield.CubeGrid || ent.Physics == null || ent.MarkedForClose || ent is MyVoxelBase && !GridIsMobile
                    || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || FriendlyCache.Contains(ent) || ent.GetType().Name == "MyDebrisBase") continue;

                var relation = EntType(ent);
                if ((relation == Ent.Ignore || relation == Ent.Friend) && CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, _detectInsideInv))
                {
                    FriendlyCache.Add(ent);
                    continue;
                }

                _enablePhysics = true;
                lock (_webEnts)
                {
                    EntIntersectInfo entInfo;
                    _webEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
                        entInfo.LastTick = _tick;
                        if (entInfo.SpawnedInside) FriendlyCache.Add(ent);
                    }
                    else
                    {
                        var inside = false;
                        if ((relation == Ent.Other && CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, _detectMatrixOutsideInv)) ||  ((relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid) && CustomCollision.AllAabbInShield(((IMyEntity) ent).WorldAABB, _detectMatrixOutsideInv)))
                        {
                            inside = true;
                            FriendlyCache.Add(ent);
                        }
                        _webEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, Vector3D.NegativeInfinity, _tick, _tick, relation, inside, new List<IMySlimBlock>(), new MyStorageData()));
                    }
                }
            }
            if (_enablePhysics || _shieldMoving || _gridChanged)
            {
                _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, PhysicsOutside);
                _icosphere.ReturnPhysicsVerts(_detectMatrixOutside, PhysicsOutsideLow);
                _icosphere.ReturnPhysicsVerts(_detectMatrixInside, PhysicsInside);
            }
            if (_enablePhysics) MyAPIGateway.Parallel.Start(WebDispatch);

            //_dsutil1.StopWatchReport("web", 1);
        }

        private void WebDispatch()
        {
            _dsutil3.Sw.Start();
            lock(_webEnts)
            {
                foreach (var webent in _webEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = _webEnts[webent];
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (_webEnts[webent].Relation == Ent.LargeNobodyGrid || _webEnts[webent].Relation == Ent.LargeEnemyGrid)) ((IMyCubeGrid)webent).GetBlocks(_webEnts[webent].CacheBlockList, CollectCollidableBlocks);
                    switch (_webEnts[webent].Relation)
                    {
                        case Ent.EnemyPlayer:
                            {
                                if ((_count == 2 || _count == 17 || _count == 32 || _count == 47) && CustomCollision.PointInShield(entCenter, _detectMatrixOutsideInv))
                                {
                                    MyAPIGateway.Parallel.Start(() => PlayerIntersect(webent));
                                }
                                continue;
                            }
                        case Ent.SmallNobodyGrid:
                            {
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeNobodyGrid:
                            {
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.SmallEnemyGrid:
                            {
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeEnemyGrid:
                            {
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.Shielded:
                            {
                                MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent as IMyCubeGrid));
                                continue;
                            }
                        case Ent.Other:
                            {
                                if (entInfo.LastTick == _tick && CustomCollision.PointInShield(entCenter, _detectMatrixOutsideInv) && !entInfo.SpawnedInside)
                                {
                                    if (webent.MarkedForClose || webent.Closed) continue;
                                    if (webent is IMyMeteor) _meteorDmg.Enqueue(webent as IMyMeteor);
                                    else _missileDmg.Enqueue(webent);
                                }
                                continue;
                            }
                        case Ent.VoxelBase:
                            {
                                MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as MyVoxelBase));
                                //VoxelIntersect(webent as MyVoxelBase);																		
                                continue;
                            }
                        default:
                            continue;
                    }
                }
            }
            _dsutil3.StopWatchReport("webDispatch", 1);
        }
        #endregion

        #region Gather Entity Information		 						
        private Ent EntType(IMyEntity ent)
        {
            if (ent == null) return Ent.Ignore;
            if (ent is MyVoxelBase && !GridIsMobile) return Ent.Ignore;

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
                var grid = ent as IMyCubeGrid;
                if (((MyCubeGrid)grid).BlocksCount < 3 && grid.BigOwners.Count == 0) return Ent.SmallNobodyGrid;
                if (grid.BigOwners.Count <= 0) return Ent.LargeNobodyGrid;

                var enemy = GridEnemy(grid);
                if (enemy && ((MyCubeGrid)grid).BlocksCount < 3) return Ent.SmallEnemyGrid;

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent != null && !enemy) return Ent.Friend;
                if (shieldComponent != null && !shieldComponent.DefenseShields.ShieldActive) return Ent.LargeEnemyGrid;
                if (shieldComponent != null && Entity.EntityId > shieldComponent.DefenseShields.Entity.EntityId) return Ent.Shielded;
                if (shieldComponent != null) return Ent.Ignore; //only process the higher EntityID
                return enemy ? Ent.LargeEnemyGrid : Ent.Friend;
            }

            if (ent is IMyMeteor || ent.GetType().Name.StartsWith("MyMissile")) return Ent.Other;
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

        private bool GridFriendly(IMyCubeGrid grid)
        {
            var owners = grid.BigOwners;
            if (owners.Count == 0) return false;
            var relationship = Shield.GetUserRelationToOwner(owners[0]);
            var friend = relationship == MyRelationsBetweenPlayerAndBlock.Owner || relationship == MyRelationsBetweenPlayerAndBlock.FactionShare;
            return friend;
        }

        private bool MovingCheck(IMyEntity ent)
        {
            float bVelSqr = 0;
            float bAvelSqr = 0;
            if (ent.Physics.IsMoving)
            {
                bVelSqr = ent.Physics.LinearVelocity.LengthSquared();
                bAvelSqr = ent.Physics.AngularVelocity.LengthSquared();
            }
            else if (!_shieldMoving) return false;
            return _shieldMoving || bVelSqr > 0.00001 || bAvelSqr > 0.00001;
        }

        private static bool CollectCollidableBlocks(IMySlimBlock mySlimBlock)
        {
            return mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_TextPanel) 
                   && mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_ButtonPanel) 
                   && mySlimBlock.BlockDefinition.Id.SubtypeId != MyStringHash.TryGet("SmallLight");
        }																				 
        #endregion
		
        #region Intersect
        private bool GridInside(IMyCubeGrid grid, MyOrientedBoundingBoxD bOriBBoxD)
        {
            if (CustomCollision.PointInShield(grid.PositionComp.WorldVolume.Center, _detectInsideInv))
            {
                if (CustomCollision.AllCornersInShield(bOriBBoxD, _detectMatrixOutsideInv)) return true;

                var ejectDir = CustomCollision.EjectDirection(grid, PhysicsOutside, _dataStructures.p3VertTris, bOriBBoxD, _detectMatrixOutsideInv);
                if (ejectDir == Vector3D.NegativeInfinity) return false;
                Eject.Add(grid, ejectDir);
                return true;
            }
            return false;
        }

        private void SmallGridIntersect(IMyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;

            EntIntersectInfo entInfo;
            _webEnts.TryGetValue(ent, out entInfo);
            if (entInfo == null) return;

            CustomCollision.SmallIntersect(entInfo, _fewDmgBlocks, grid, _detectMatrixOutside, _detectMatrixOutsideInv);
            var contactpoint = entInfo.ContactPoint;
            entInfo.ContactPoint = Vector3D.NegativeInfinity;
            if (contactpoint != Vector3D.NegativeInfinity)
            {
                Absorb += entInfo.Damage;
                ImpactSize += entInfo.Damage;

                entInfo.Damage = 0;
                WorldImpactPosition = contactpoint;
            }
        }

        private void GridIntersect(IMyEntity ent)
        {
            lock (_webEnts)
            {
                var grid = (IMyCubeGrid)ent;
                EntIntersectInfo entInfo;
                _webEnts.TryGetValue(ent, out entInfo);
                if (entInfo == null) return;

                var bOriBBoxD = MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB);
                if (entInfo.Relation != Ent.LargeEnemyGrid && GridInside(grid, bOriBBoxD)) return;
                BlockIntersect(grid, bOriBBoxD, entInfo);
                var contactpoint = entInfo.ContactPoint;
                entInfo.ContactPoint = Vector3D.NegativeInfinity;
                if (contactpoint == Vector3D.NegativeInfinity) return;

                ImpactSize += entInfo.Damage;

                entInfo.Damage = 0;
                WorldImpactPosition = contactpoint;
            }
        }

        private void ShieldIntersect(IMyCubeGrid grid)
        {
            if (GridInside(grid, MyOrientedBoundingBoxD.CreateFromBoundingBox(grid.WorldAABB))) return;
            ShieldGridComponent shieldComponent;
            grid.Components.TryGet(out shieldComponent);

            var dsVerts = shieldComponent.DefenseShields.PhysicsOutside;
            var dsMatrixInv = shieldComponent.DefenseShields._detectMatrixOutsideInv;
            var myGrid = Shield.CubeGrid;

            var insidePoints = new List<Vector3D>();
            CustomCollision.ShieldX2PointsInside(dsVerts, dsMatrixInv, PhysicsOutside, _detectMatrixOutsideInv, insidePoints);

            var bPhysics = grid.Physics;
            var sPhysics = myGrid.Physics;
            var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
            var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);

            var collisionAvg = Vector3D.Zero;
            for (int i = 0; i < insidePoints.Count; i++)
            {
                collisionAvg += insidePoints[i];
            }

            if (insidePoints.Count > 0) bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, bPhysics.CenterOfMassWorld);
            if (insidePoints.Count > 0) sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, sPhysics.CenterOfMassWorld);

            collisionAvg /= insidePoints.Count;
            if (insidePoints.Count > 0) sPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - sPhysics.CenterOfMassWorld) * sPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(sPhysics.LinearVelocity.Length(), 10f, 50f));
            if (insidePoints.Count > 0) bPhysics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -(collisionAvg - bPhysics.CenterOfMassWorld) * bPhysics.Mass, null, Vector3D.Zero, MathHelper.Clamp(bPhysics.LinearVelocity.Length(), 10f, 50f));

            if (insidePoints.Count <= 0) return;

            var contactPoint = DSUtils.CreateFromPointsList(insidePoints).Center; // replace with average
            WorldImpactPosition = contactPoint;
            shieldComponent.DefenseShields.WorldImpactPosition = contactPoint;
            var damage = 1f;
            var bDamage = (bPhysics.Mass * bPhysics.LinearVelocity).Length();
            var sDamage = (sPhysics.Mass * sPhysics.LinearVelocity).Length();
            damage = bDamage < sDamage ? bDamage : sDamage;
            Absorb += damage / 1000;
        }

        private void VoxelIntersect(MyVoxelBase voxelBase)
        {
            EntIntersectInfo entInfo;
            _webEnts.TryGetValue(voxelBase, out entInfo);
            var collision = CustomCollision.VoxelCollisionSphere(Shield.CubeGrid, PhysicsOutsideLow, voxelBase, _sOriBBoxD, entInfo.TempStorage, _detectMatrixOutside);

            if (collision != Vector3D.NegativeInfinity)
            {
                var sPhysics = Shield.CubeGrid.Physics;
                var momentum = sPhysics.Mass * sPhysics.LinearVelocity;
                Absorb += momentum.Length() / 500;
                WorldImpactPosition = collision;
				_voxelDmg.Enqueue(voxelBase);							 
            } 
        }

        private void PlayerIntersect(IMyEntity ent)
        {
            var playerInfo = _webEnts[ent];
            var rnd = new Random();
            var character = ent as IMyCharacter;
            if (character == null) return;
            var npcname = character.ToString();
            if (npcname.Equals("Space_Wolf"))
            {
                Log.Line($"playerEffect: Killing {character}");
                _characterDmg.Enqueue(character);
                //Absorb += 500;
                //WorldImpactPosition = ent.PositionComp.WorldVolume.Center;
                return;
            }
            if (character.EnabledDamping) character.SwitchDamping();
            if (!character.EnabledThrusts) return;

            var insideTime = (int)playerInfo.LastTick - (int)playerInfo.FirstTick;
            var explodeRollChance = rnd.Next(0 - insideTime, insideTime);
            if (explodeRollChance <= 666) return;
            _webEnts.Remove(ent);

            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
            var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
            if (!(playerGasLevel > 0.01f)) return;
            _characterDmg.Enqueue(character);
        }

        private void BlockIntersect(IMyCubeGrid breaching, MyOrientedBoundingBoxD bOriBBoxD, EntIntersectInfo entInfo)
        {
            var dsutil = new DSUtils();

            var collisionAvg = Vector3D.Zero;
            var transformInv = _detectMatrixOutsideInv;
            var normalMat = MatrixD.Transpose(transformInv);
            var intersection = bOriBBoxD.Intersects(ref _sOriBBoxD);
            try
            {
                if (intersection)
                {
                    var cacheBlockList = entInfo.CacheBlockList;
                    var bPhysics = breaching.Physics;
                    var sPhysics = Shield.CubeGrid.Physics;
                    var momentum = bPhysics.Mass * bPhysics.LinearVelocity + sPhysics.Mass * sPhysics.LinearVelocity;
                    var resultVelocity = momentum / (bPhysics.Mass + sPhysics.Mass);
                    var bBlockCenter = Vector3D.NegativeInfinity;

                    var stale = false;
                    var damage = 0f;
                    Vector3I gc = breaching.WorldToGridInteger(_detectionCenter);
                    double rc = ShieldSize.AbsMax() / breaching.GridSize;
                    rc *= rc;
                    rc = rc + 1;
                    rc = Math.Ceiling(rc);
                    var c1 = 0;
                    var c2 = 0;
                    var c3 = 0;
                    var c4 = 0;
                    var c5 = 0;
                    var c6 = 0;
                    Vector3D[] blockPoints = new Vector3D[9];
                    for (int i = 0; i < cacheBlockList.Count; i++)
                    {
                        var block = cacheBlockList[i];
                        Vector3I blockPos = block.Position;
                        int num1 = gc.X - blockPos.X;
                        int num2 = gc.Y - blockPos.Y;
                        int num3 = gc.Z - blockPos.Z;
                        int result = num1 * num1 + num2 * num2 + num3 * num3;

                        if (result > rc) continue;
                        c1++;
                        if (block.IsDestroyed)
                        {
                            c6++;
                            _destroyedBlocks.Enqueue(block);
                            continue;
                        }
                        if (block.CubeGrid != breaching)
                        {
                            if (!stale) _staleGrids.Enqueue(breaching);
                            stale = true;
                            continue;
                        }
                        c2++;
                        BoundingBoxD blockBox;
                        block.GetWorldBoundingBox(out blockBox);

                        blockBox.GetCorners(blockPoints);
                        blockPoints[8] = blockBox.Center;
                        //var point2 = Vector3D.Clamp(_detectMatrixOutsideInv.Translation, blockBox.Min, blockBox.Max);
                        for (int j = 8; j > -1; j--)
                        {
                            var point = blockPoints[j];
                            if (Vector3.Transform(point, _detectMatrixOutsideInv).LengthSquared() > 1) continue;

                            collisionAvg += point;
                            c3++;

                            if (_dmgBlocks.Count > 50) break;
                            c4++;
                            damage += block.Mass;
                            _dmgBlocks.Enqueue(block);
                            break;
                        }
                    }
                    if (collisionAvg != Vector3D.Zero)
                    {
                        collisionAvg /= c3;
                        bPhysics.ApplyImpulse((resultVelocity - bPhysics.LinearVelocity) * bPhysics.Mass, bPhysics.CenterOfMassWorld);
                        sPhysics.ApplyImpulse((resultVelocity - sPhysics.LinearVelocity) * sPhysics.Mass, sPhysics.CenterOfMassWorld);
                        var surfaceMass = (bPhysics.Mass > sPhysics.Mass) ? sPhysics.Mass : bPhysics.Mass;
                        var surfaceMulti = (c3 > 5) ? 5 : c3;
                        var localNormal = Vector3D.Transform(collisionAvg, transformInv);
                        var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));
                        bPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 5) * -Vector3D.Dot(bPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        sPhysics.ApplyImpulse(surfaceMulti * (surfaceMass / 5) * -Vector3D.Dot(sPhysics.LinearVelocity, surfaceNormal) * surfaceNormal, collisionAvg);
                        bBlockCenter = collisionAvg;
                    }
                    entInfo.Damage = damage;
                    Absorb += damage;
                    if (bBlockCenter != Vector3D.NegativeInfinity) entInfo.ContactPoint = bBlockCenter;
                    //if (_count == 58) Log.Line($"[status] obb: true - blocks:{cacheBlockList.Count.ToString()} - sphered:{c1.ToString()} [{c5.ToString()}] - IsDestroyed:{c6.ToString()} not:[{c2.ToString()}] - bCenter Inside Ellipsoid:{c3.ToString()} - Damaged:{c4.ToString()}");
                    //if (_count == 0) dsutil.StopWatchReport("[perform]", -1);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockIntersect: {ex}");}
        }				 
        #endregion				  
		
        #region Shield Draw
        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            _enemy = enemy;
            var visable = !(_visablilityCheckBox.Getter(Shield).Equals(true) && !enemy);

			if (BulletCoolDown > -1) BulletCoolDown++;
			if (BulletCoolDown > 19) BulletCoolDown = -1;
			if (EntityCoolDown > -1) EntityCoolDown++;
			if (EntityCoolDown > 7) EntityCoolDown = -1;
				
            var impactPos = WorldImpactPosition;
            if (impactPos != Vector3D.NegativeInfinity & ((BulletCoolDown == -1 || EntityCoolDown == -1)))
            {
                if (EntityCoolDown == -1 && ImpactSize > 5) EntityCoolDown = 0;
                else BulletCoolDown = 0;

                var cubeBlockLocalMatrix = Shield.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                impactPos = localPosition;
            }
            _localImpactPosition = impactPos;
            WorldImpactPosition = Vector3D.NegativeInfinity;

            if (Shield.IsWorking) PrepareSphere();
            if (sphereOnCamera && Shield.IsWorking) _icosphere.Draw(GetRenderId(), visable);
        }

        private void PrepareSphere()
        {
            var prevlod = _prevLod;
            var lod = CalculateLod(_onCount);
            if (_gridChanged || lod != prevlod) _icosphere.CalculateTransform(_shieldShapeMatrix, lod);
            _icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, ImpactSize, _entityChanged, _enemy, _shield, prevlod);
            _entityChanged = false;
        }
		
        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Shield.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + Range) * (x + Range);
            return range;
        }

        private int CalculateLod(int onCount)
        {
            var lod = 2;

            if (Distance(2500) && onCount == 1) lod = 3;
            //if (Distance(300) && onCount == 1) lod = 4;
            //else if (Distance(2500) && onCount <= 2) lod = 3;
            else if (Distance(8000) && onCount < 7) lod = 2;
            else lod = 1;

            _prevLod = lod;
            return lod;
        }

        private uint GetRenderId()
        {
            return Shield.CubeGrid.Render.GetRenderObjectID();
        }
        #endregion

        #region DSModAPI
        /// <summary>
        /// RayCast against shielded targets.  If returns null proceed with normal raycast,
        /// but do not normal cast against entities in _shielded (hashset).
        /// </summary>
        /// 
        /// <param name="shield">the active shield to attack</param>
        /// <param name="line">Ray to check for shield contact</param>
        /// <param name="attackerId">You must pass the EntityID of the attacker</param>
        /// <param name="damage">the amount of damage to do</param>
        /// <param name="effect">optional effects, "DSdamage" is default, "DSheal"and "DSbypass" are possible</param>
        private Vector3D? DsRayCast(IMyEntity shield, LineD line, long attackerId, float damage, MyStringId effect)
        {
            var sphere = new BoundingSphereD(shield.PositionComp.WorldVolume.Center, shield.PositionComp.LocalAABB.HalfExtents.AbsMax());
            var obb = MyOrientedBoundingBoxD.Create(shield.PositionComp.LocalAABB, shield.PositionComp.WorldMatrix.GetOrientation());
            obb.Center = shield.PositionComp.WorldVolume.Center;

            // DsDebugDraw.DrawSphere(sphere, Color.Red);
            DsDebugDraw.DrawOBB(obb, Color.Blue, MySimpleObjectRasterizer.Wireframe, 0.1f);
            var obbCheck = obb.Intersects(ref line);
            if (obbCheck == null) return null;

            var testDir = line.From - line.To;
            testDir.Normalize();
            var ray = new RayD(line.From, -testDir);
            var sphereCheck = sphere.Intersects(ray);
            if (sphereCheck == null) return null;

            var furthestHit = obbCheck < sphereCheck ? sphereCheck : obbCheck;
            Vector3 hitPos = line.From + testDir * -(double)furthestHit;

            var parent = MyAPIGateway.Entities.GetEntityById(long.Parse(shield.Name));
            var cubeBlock = (MyCubeBlock)parent;
            var block = (IMySlimBlock)cubeBlock.SlimBlock;

            if (block == null) return null;
           // _shielded.Add(parent);

            if (Debug)
            {
                DsDebugDraw.DrawSingleVec(hitPos, 1f, Color.Gold);
                var c = new Vector4(15, 0, 0, 10);
                var rnd = new Random();
                var lineWidth = 0.2f;
                if (rnd.Next(0, 5) > 2) lineWidth = 1;
                if (_count % 2 == 0) DsDebugDraw.DrawLineToVec(line.From, hitPos, c, lineWidth);
            }

            block.DoDamage(damage, MyStringHash.GetOrCompute(effect.ToString()), true, null, attackerId);
            shield.Render.ColorMaskHsv = hitPos;
            if (effect.ToString() == "bypass") return null;

            return hitPos;
        }
		
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
            _ammoInfo.TryGetValue(ammoEnt.Model.AssetName, out ammoInfo);
            var damage = 10f;
            if (ammoInfo == null)
            {
                Log.Line($"No Missile Ammo Match Found for {((MyEntity)ammoEnt).DebugName}! Let wepaon mod author know their ammo definition has improper model path");
                return damage;
            }

            if (ammoInfo.BackKickForce < 0) damage = float.NegativeInfinity;
            else if (ammoInfo.Explosive) damage = (ammoInfo.Damage * (ammoInfo.Radius * 0.5f)) * 7.5f;
            else damage = ammoInfo.Mass * ammoInfo.Speed;

            if (ammoInfo.Mass < 0 && ammoInfo.Radius <= 0) damage = -damage;
            return damage;
        }	  
        #endregion

        #region Settings
        public void UpdateSettings(DefenseShieldsModSettings newSettings)
        {
            ShieldVisable = newSettings.Enabled;
            Width = newSettings.Width;
            Height = newSettings.Height;
            Depth = newSettings.Depth;
        }

        public void SaveSettings()
        {
            if (DefenseShields.Storage == null)
                DefenseShields.Storage = new MyModStorageComponent();

            DefenseShields.Storage[DefenseShieldsBase.Instance.SETTINGS_GUID] = MyAPIGateway.Utilities.SerializeToXML(Settings);

            Log.Line("SaveSettings()");
        }

        private bool LoadSettings()
        {
            Log.Line("LoadSettings");

            if (DefenseShields.Storage == null)
                return false;

            string rawData;
            bool loadedSomething = false;

            if (DefenseShields.Storage.TryGetValue(DefenseShieldsBase.Instance.SETTINGS_GUID, out rawData))
            {
                DefenseShieldsModSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsModSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Log.Line($"Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    loadedSomething = true;
                }

                Log.Line($"  Loaded settings:\n{Settings.ToString()}");
            }

            return loadedSomething;
        }

        public bool ShieldVisable
        {
            get { return Settings.Enabled; }
            set
            {
                Settings.Enabled = value;
                RefreshControls(refeshCustomInfo: true);
            }
        }

        public float Width
        {
            get { return Settings.Width; }
            set
            {
                Settings.Width = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Height
        {
            get { return Settings.Height; }
            set
            {
                Settings.Height = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        public float Depth
        {
            get { return Settings.Depth; }
            set
            {
                Settings.Depth = (float)Math.Round(MathHelper.Clamp(value, MIN_SCALE, Math.Min(LargestGridLength, MAX_SCALE)), 3);
                needsMatrixUpdate = true;
            }
        }

        private void RefreshControls(bool refreshRemoveButton = false, bool refeshCustomInfo = false)
        {
        }

        public void UseThisShip_Receiver(bool fix)
        {
            Log.Line($"UseThisShip_Receiver({fix.ToString()})");

            //UseThisShip_Internal(fix);
        }
		#endregion
    }
}
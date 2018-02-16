using Sandbox.Game;
using VRage.ObjectBuilders;
using VRageMath;
using ProtoBuf;
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
using ParallelTasks;
using Sandbox.Game.Entities;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "StationDefenseShield")]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;
        private const float InOutSpace = 15f;

        private float _power = 0.0001f;
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;
        private float _recharge;
        private float _absorb;
        private float _impactSize;

        private int _count = -1;
        private int _explodeCount;
        private int _playercount = 600;
        private int _gridcount = 600;
        private int _time;
        private int _playertime;
        private int _prevLod;

        private bool _entityChanged = true;
        private bool _gridChanged = true;
        private bool _initialized;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _closegrids;
        private bool _playerkill;
        private bool _gridIsMobile;
        private bool _explode;

        private const ushort ModId = 50099;

        private Vector3D _worldImpactPosition = new Vector3D(Vector3D.NegativeInfinity);
        private Vector3D _detectionCenter;
        private Vector3D _shieldSize;

        private Vector3D[] _collisionTris;

        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectionMatrix;
        private MatrixD _detectionMatrixInv;
        private MatrixD _mobileMatrix;

        private BoundingBox _oldGridAabb;

        private IMyOreDetector Block => (IMyOreDetector)Entity;
        private IMyEntity _shield;

        private readonly Spawn _spawn = new Spawn();
        private Icosphere.Instance _icosphere;

        private MyEntitySubpart _subpartRotor;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _widthSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _heightSlider;
        private RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> _depthSlider;

        private MyResourceSinkComponent _sink;
        private readonly MyDefinitionId _powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash { get; } = new MyConcurrentHashSet<IMyEntity>();
        private MyConcurrentHashSet<IMySlimBlock> DmgBlocks { get; } = new MyConcurrentHashSet<IMySlimBlock>();

        private List<IMyCubeGrid> GridIsColliding = new List<IMyCubeGrid>();

        public static HashSet<IMyEntity> DestroyPlayerHash { get; } = new HashSet<IMyEntity>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private MatrixD DetectionMatrix
        {
            get { return _detectionMatrix; }
            set
            {
                _detectionMatrix = value;
                _detectionMatrixInv = MatrixD.Invert(value);
            }
        }

        public MyResourceSinkComponent Sink { get { return _sink; } set { _sink = value; } }

        public override void OnAddedToScene() { DefenseShieldsBase.Instance.Components.Add(this); _icosphere = new Icosphere.Instance(DefenseShieldsBase.Instance.Icosphere); }
        public override void OnRemovedFromScene() { DefenseShieldsBase.Instance.Components.Remove(this); _icosphere = null; } 
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }
        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        #endregion

        // temp
        private bool needsMatrixUpdate = false;
        public DefenseShieldsModSettings Settings = new DefenseShieldsModSettings();
        private bool blocksNeedRefresh = false;
        public const float MIN_SCALE = 15f; // Scale slider min/max
        public const float MAX_SCALE = 300f;
        public float LargestGridLength = 2.5f;
        public static MyModStorageComponent Storage { get; set; }
        private HashSet<ulong> playersToReceive = null;
        // 

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out _sink);
            _sink.SetRequiredInputFuncByType(_powerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!_shields.ContainsKey(Entity.EntityId)) _shields.Add(Entity.EntityId, this);
        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_count++ == 59) _count = 0;
                if (_explode && _explodeCount++ == 14) _explodeCount = 0;
                if (_explodeCount == 0 && _explode) _explode = false;

                if (_count <= 0)
                {
                    if (!_initialized)
                    {
                        _count = -1;
                        InHashBuilder();
                        return;
                    }
                    InHashBuilder();
                }
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && _initialized && Block.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (_playercount < 600) _playercount++;
                if (_gridcount < 600) _gridcount++;
                if (_count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    Block.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_gridIsMobile)
                {
                    var entAngularVelocity = !Vector3D.IsZero(Block.CubeGrid.Physics.AngularVelocity); 
                    var entLinVel = !Vector3D.IsZero(Block.CubeGrid.Physics.GetVelocityAtPoint(Block.CubeGrid.PositionComp.WorldMatrix.Translation));
                    _gridChanged = _oldGridAabb != Block.CubeGrid.LocalAABB;
                    _oldGridAabb = Block.CubeGrid.LocalAABB;
                    _entityChanged = entAngularVelocity || entLinVel || _gridChanged;
                    //if (_entityChanged || _gridChanged) Log.Line($"Entity Change Loop ec:{_entityChanged} gc:{_gridChanged} vel:{entLinVel} avel:{entAngularVelocity}");
                    if (_entityChanged || _range <= 0) CreateShieldMatrices();
                }
                if (Block.CubeGrid.Physics.IsStatic) _entityChanged = RefreshDimensions();
                if (_playerkill || _playercount == 479)
                {
                    if (_playerkill) _playercount = -1;
                    _playerkill = false;
                    //if (DestroyPlayerHash.Count > 0) DestroyEntity.PlayerKill(Playercount);
                }
                if (_closegrids || _gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419 || _gridcount == 479 || _gridcount == 599)
                {
                    if (_closegrids) _gridcount = -1;
                    _closegrids = false;
                    //if (DestroyGridHash.Count > 0) DestroyEntity.GridClose(Gridcount);
                }
                if (!_initialized || !Block.IsWorking) return;
                //GridKillField();
                DamageGrids();
                MyAPIGateway.Parallel.StartBackground(WebEntities);
                if (_playerwebbed) PlayerEffects();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_initialized) return;
            Log.Line($"Initting entity");
            if (Block.CubeGrid.Physics.IsStatic) _gridIsMobile = false;
            else if (!Block.CubeGrid.Physics.IsStatic) _gridIsMobile = true;

            CreateUi();
            Block.AppendingCustomInfo += AppendingCustomInfo;
            Block.RefreshCustomInfo();
            _absorb = 150f;

            _shield = _spawn.EmptyEntity("Field", $"{DefenseShieldsBase.Instance.ModPath()}\\Models\\LargeField0.mwm");
            _shield.Render.Visible = false;

            DefenseShieldsBase.Instance.Shields.Add(this);
            _initialized = true;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_animInit) return;
                if (Block.BlockDefinition.SubtypeId == "StationDefenseShield")
                {
                    if (!Block.IsFunctional) return;
                    BlockAnimationInit();
                    Log.Line($" BlockAnimation {_count}");
                    _animInit = true;
                }
                else
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateAfterSimulation: {ex}"); }
        }
        #endregion

        #region Block Power and Entity Config Logic
        private float CalcRequiredPower()
        {
            if (!_initialized || !Block.IsWorking) return _power;
            if (_absorb >= 0.1)
            {
                _absorb = _absorb - _recharge;
                _recharge = _absorb / 10f;
            }
            else if (_absorb < 0.1f)
            {
                _recharge = 0f;
                _absorb = 0f;
            }
            var radius = GetRadius();
            var sustaincost = radius * 0.001f;
            _power = _recharge + sustaincost;

            return _power;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            if (!_gridIsMobile)RefreshDimensions();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");
        }

        private bool RefreshDimensions()
        {
            var width = _widthSlider.Getter(Block);
            var height = _heightSlider.Getter(Block);
            var depth = _depthSlider.Getter(Block);
            var oWidth = _width;
            var oHeight = _height;
            var oDepth = _depth;
            _width = width;
            _height = height;
            _depth = depth;
            var changed = (int)oWidth != (int)width || (int)oHeight != (int)height || (int)oDepth != (int)depth;
            if (!changed) return false;
            CreateShieldMatrices();
            return true;
        }

        private float GetRadius()
        {
            float radius;
            if (_gridIsMobile)
            {
                var p = (float)_shieldShapeMatrix.Scale.Sum / 3 / 2;
                radius = p * p * 4 * (float)Math.PI;
                return radius;
            }
            var r = (_width + _height + _depth) / 3 / 2;
            var r2 = r * r;
            var r3 = r2 * 4;
            radius = r3 * (float)Math.PI;

            return radius;
        }
        #endregion

        #region Create UI
        private bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        private void RemoveOreUi()
        {
            List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;
        }

        private void CreateUi()
        {
            Log.Line($"Create UI - c:{_count}");
            DefenseShieldsBase.Instance.ControlsLoaded = true;
            RemoveOreUi();

            _widthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "WidthSlider", "Shield Size Width", 35, 300, 300);
            _heightSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "HeightSlider", "Shield Size Height", 35, 300, 300);
            _depthSlider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>(Block, "DepthSlider", "Shield Size Depth", 35, 300, 300);
        }
        #endregion

        #region Block Animation
        private void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {_count}");
            _subpartRotor.Subparts.Clear();
            _subpartsArms.Clear();
            _subpartsReflectors.Clear();
            BlockAnimationInit();
        }

        private void BlockAnimationInit()
        {
            try
            {
                _animStep = 0f;

                _matrixArmsOff = new List<Matrix>();
                _matrixArmsOn = new List<Matrix>();
                _matrixReflectorsOff = new List<Matrix>();
                _matrixReflectorsOn = new List<Matrix>();

                Entity.TryGetSubpart("Rotor", out _subpartRotor);

                for (var i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    _subpartRotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    _matrixArmsOff.Add(temp1.PositionComp.LocalMatrix);
                    var temp2 = temp1.PositionComp.LocalMatrix.GetOrientation();
                    switch (i)
                    {
                        case 1:
                        case 5:
                            temp2 *= Matrix.CreateRotationZ(0.98f);
                            break;
                        case 2:
                        case 6:
                            temp2 *= Matrix.CreateRotationX(-0.98f);
                            break;
                        case 3:
                        case 7:
                            temp2 *= Matrix.CreateRotationZ(-0.98f);
                            break;
                        case 4:
                        case 8:
                            temp2 *= Matrix.CreateRotationX(0.98f);
                            break;
                    }
                    temp2.Translation = temp1.PositionComp.LocalMatrix.Translation;
                    _matrixArmsOn.Add(temp2);
                    _subpartsArms.Add(temp1);
                }

                for (var i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    _subpartsArms[i].TryGetSubpart("Reflector", out temp3);
                    _subpartsReflectors.Add(temp3);
                    _matrixReflectorsOff.Add(temp3.PositionComp.LocalMatrix);
                    var temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    _matrixReflectorsOn.Add(temp4);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in BlockAnimation: {ex}"); }
        }

        private void BlockAnimation()
        {
            if (Block.Enabled && Block.IsFunctional && Block.IsWorking)
            {
                _subpartRotor.SetEmissiveParts("Emissive", Color.White, 1);
                _time += 1;
                var temp1 = Matrix.CreateRotationY(0.1f * _time);
                temp1.Translation = _subpartRotor.PositionComp.LocalMatrix.Translation;
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {
                //_subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
                if (_animStep > 0f)
                {
                    _animStep -= 0.05f;
                }
            }
            for (var i = 0; i < 8; i++)
            {
                if (i < 4)
                {
                    _subpartsReflectors[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixReflectorsOff[i], _matrixReflectorsOn[i], _animStep);
                }
                _subpartsArms[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixArmsOff[i], _matrixArmsOn[i], _animStep);
            }
        }
        #endregion

        #region Shield Draw
        private Task? _prepareTask = null;
        public void Draw()
        {
            try
            {
                if (!_initialized) return;

                SetShieldShapeMatrix();
                var drawShapeChanged = _entityChanged;

                var prevlod = _prevLod;
                var lod = CalculateLod();
                var shield = _shield;
                var impactPos = _worldImpactPosition;

                var referenceWorldPosition = _shieldGridMatrix.Translation; 
                var worldDirection = impactPos - referenceWorldPosition; 
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(_shieldGridMatrix));
                if (impactPos != Vector3D.NegativeInfinity) impactPos = localPosition;
                //if (impactpos != Vector3D.NegativeInfinity) impactpos = Vector3D.Transform(impactpos, Block.CubeGrid.WorldMatrixInvScaled);
                _worldImpactPosition = Vector3D.NegativeInfinity;

                var impactSize = _impactSize;

                var shapeMatrix = _shieldShapeMatrix;
                var enemy = IsEnemy(null);
                var renderId = GetRenderId();

                //Log.Line($"ent: {this.Entity.EntityId} - changed?:{_entityChanged} - is onCam:{_sphereOnCamera} - RenderID {renderId}");
                var sp = new BoundingSphereD(Entity.GetPosition(), _range);
                var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);
                if (_prepareTask.HasValue && !_prepareTask.Value.IsComplete) _prepareTask.Value.Wait();
                if (_prepareTask.HasValue && _prepareTask.Value.IsComplete && sphereOnCamera && Block.IsWorking) _icosphere.Draw(renderId);
                if (sphereOnCamera && Block.IsWorking || drawShapeChanged) _prepareTask = MyAPIGateway.Parallel.Start(() => PrepareSphere(drawShapeChanged, enemy, lod, prevlod, impactPos, impactSize, shapeMatrix, shield));

            }
            catch (Exception ex) { Log.Line($"Exception in Entity Draw: {ex}"); }
        }

        private void PrepareSphere(bool drawShapeChanged, bool enemy, int lod, int prevlod, Vector3D impactPos, float impactSize, MatrixD shapeMatrix,  IMyEntity shield)
        {
            if (drawShapeChanged || lod != prevlod) _icosphere.CalculateTransform(shapeMatrix, lod);
            _icosphere.CalculateColor(shapeMatrix, impactPos, impactSize, drawShapeChanged, enemy, shield);
        }
        #endregion

        #region Shield Draw Prep
        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = Block.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + _range) * (x + _range);
            return range;
        }

        private int CalculateLod()
        {
            int lod;

            if (Distance(650)) lod = 3;
            else if (Distance(2250)) lod = 3;
            else if (Distance(4500)) lod = 2;
            else if (Distance(15000)) lod = 1;
            else if (Distance(25000)) lod = 1;
            else lod = 1;

            _prevLod = lod;
            return lod;
        }

        private void CreateShieldMatrices()
        {
            if (_gridIsMobile)
            {
                _shieldGridMatrix = Block.CubeGrid.WorldMatrix;
                CreateMobileShape();
                var mobileMatrix = _mobileMatrix;
                DetectionMatrix = mobileMatrix * _shieldGridMatrix;
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                _detectionCenter = Block.CubeGrid.PositionComp.WorldVolume.Center;

                _collisionTris = _icosphere.CalculatePhysics(DetectionMatrix, 4);
                //Log.Line($"mobile dims {_range} - {_width} - {_height} - {_depth} - changed: {_entityChanged}");
            }
            else
            {
                _shieldGridMatrix = Block.CubeGrid.WorldMatrix;
                _detectionCenter = Block.PositionComp.WorldVolume.Center;
                DetectionMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
                _range = (float)DetectionMatrix.Scale.AbsMax() + 15f;
                //Log.Line($"static dims {_range} - {_width} - {_height} - {_depth}");
            }
        }


        private void CreateMobileShape()
        {
            if (!_gridChanged) return;

            var gridHalfExtents = Block.CubeGrid.PositionComp.LocalAABB.HalfExtents;

            const float ellipsoidAdjust = (float)MathHelper.Sqrt2;
            var buffer = 5f;
            var shieldSize = gridHalfExtents * ellipsoidAdjust + buffer;
            _shieldSize = shieldSize;
            //var gridLocalCenter = Block.CubeGrid.PositionComp.LocalAABB.Center;
            var mobileMatrix = MatrixD.CreateScale(shieldSize); //* MatrixD.CreateTranslation(gridLocalCenter);
            mobileMatrix.Translation = Block.CubeGrid.PositionComp.LocalVolume.Center;
            _mobileMatrix = mobileMatrix;
        }

        private void SetShieldShapeMatrix()
        {
            if (Block.CubeGrid.Physics.IsStatic)
            {
                _shieldShapeMatrix = MatrixD.Rescale(Block.WorldMatrix, new Vector3D(_width, _height, _depth));
                _shield.SetWorldMatrix(_shieldShapeMatrix);
            }
            if (!_entityChanged || Block.CubeGrid.Physics.IsStatic) return;
            CreateMobileShape();
            var mobileMatrix = _mobileMatrix;

            _shieldShapeMatrix = mobileMatrix;
            _shield.SetWorldMatrix(_shieldShapeMatrix);
        }

        private bool IsEnemy(IMyEntity enemy)
        {
            if (enemy != null)
            {
                if (enemy is IMyCharacter)
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(enemy).IdentityId;
                    var playerrelationship = Block.GetUserRelationToOwner(dude);
                    return playerrelationship != MyRelationsBetweenPlayerAndBlock.Owner && playerrelationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                }
                if (enemy is IMyCubeGrid)
                {
                    var grid = enemy as IMyCubeGrid;
                    var owners = grid.BigOwners;
                    if (owners.Count > 0)
                    {
                        var relationship = Block.GetUserRelationToOwner(owners[0]);
                        return relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                    }
                }
            }
            var relations = Block.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            return relations != MyRelationsBetweenPlayerAndBlock.Owner && relations != MyRelationsBetweenPlayerAndBlock.FactionShare;
        }

        private uint GetRenderId()
        {
            var renderId = Block.CubeGrid.Render.GetRenderObjectID();
            if (!_gridIsMobile) renderId = Block.Render.GetRenderObjectID();
            return renderId;
        }
        #endregion

        #region Detect Intersection
        private Vector3D ContactPoint(IMyEntity breaching)
        {
            var wVol = breaching.PositionComp.WorldVolume;
            var wDir = DetectionMatrix.Translation - wVol.Center;
            var wLen = wDir.Length();
            var contactPoint = wVol.Center + (wDir / wLen * Math.Min(wLen, wVol.Radius));
            return contactPoint;
        }

        private Vector3D ContactPointObb(IMyEntity breaching)
        {
            var bWorldAABB = breaching.WorldAABB;
            var bLocalAABB = breaching.LocalAABB;
            var bLocalAABBs = (BoundingBoxD)breaching.LocalAABB;

            var bWorldMatrix = breaching.WorldMatrix;
            var bWorldCenter = bWorldAABB.Center;
            var bOriBBoxD = new MyOrientedBoundingBoxD(bLocalAABB, breaching.WorldMatrix);
            var bLength = breaching.PositionComp.LocalAABB.Size.Max() / 2 + 2;
            List<Vector3D> rangedVectors = new List<Vector3D>();
            List<Vector3D> boxedVectors = new List<Vector3D>();
            List<Vector3D> hitVectors = new List<Vector3D>();

            //var sOriBBoxD = new MyOrientedBoundingBoxD(_detectionCenter, _shieldSize, Quaternion.CreateFromRotationMatrix(_shieldGridMatrix));
            //var collision = sOriBBoxD.Intersects(ref bOriBBoxD);
            var rangeSphere = new BoundingSphereD(bWorldCenter, bLength);

            var collision = new Vector3D(Vector3D.NegativeInfinity);

            //if (_count == 0) DSUtils.Sw.Start();
            for (int i = 0, j = 0; i < _collisionTris.Length; i += 3, j++)
            {
                var v0 = _collisionTris[i];
                var v1 = _collisionTris[i + 1];
                var v2 = _collisionTris[i + 2];
                var test1 = Vector3D.DistanceSquared(v0, bWorldCenter);
                var test2 = Vector3D.DistanceSquared(v1, bWorldCenter);
                var test3 = Vector3D.DistanceSquared(v2, bWorldCenter);


                if (test1 < bLength * bLength && test2 < bLength * bLength && test3 < bLength * bLength)
                {
                    rangedVectors.Add(v0);
                    rangedVectors.Add(v1);
                    rangedVectors.Add(v2);
                }
            }
            //if (_count == 0) DSUtils.StopWatchReport("Range Check", -1);

            //if (_count == 2) DSUtils.Sw.Start();
            for (int i = 0, j = 0; i < rangedVectors.Count; i += 3, j++)
            {
                var v0 = rangedVectors[i];
                var v1 = rangedVectors[i + 1];
                var v2 = rangedVectors[i + 2];
                var test1 = bWorldAABB.Contains(v0);
                var test2 = bWorldAABB.Contains(v1);
                var test3 = bWorldAABB.Contains(v2);

                if (test1 == ContainmentType.Contains && test2 == ContainmentType.Contains && test3 == ContainmentType.Contains)
                {
                    boxedVectors.Add(v0);
                    boxedVectors.Add(v1);
                    boxedVectors.Add(v2);
                }
            }
            //if (_count == 2) DSUtils.StopWatchReport("Point Check", -1);

            //if (_count == 4) DSUtils.Sw.Start();
            for (int i = 0; i < boxedVectors.Count; i += 3)
            {
                var line1 = boxedVectors[i];
                var line2 = boxedVectors[i + 1];
                var line3 = boxedVectors[i + 2];
                var lineTest1 = new LineD(line1, line2);
                var lineTest2 = new LineD(line2, line3);
                var lineTest3 = new LineD(line3, line1);
                if (bOriBBoxD.Intersects(ref lineTest1).HasValue || bOriBBoxD.Intersects(ref lineTest2).HasValue || bOriBBoxD.Intersects(ref lineTest3).HasValue)
                {
                    hitVectors.Add(line1);
                    hitVectors.Add(line2);
                    hitVectors.Add(line3);
                }
            }
            //if (_count == 4) DSUtils.StopWatchReport("Line Check", -1);
            if (_count == 0) Log.Line($"total triangles: {_collisionTris.Length / 3} - Ranged {rangedVectors.Count / 3} - Box Check: {boxedVectors.Count / 3} - Obb Collision {hitVectors.Count / 3}");

            if (_count == 6) DSUtils.Sw.Start();
            var vecArray = hitVectors.ToArray();
            var posCollectionSphere = BoundingSphereD.CreateFromPoints(vecArray);
            if (_count == 6) DSUtils.StopWatchReport("posCollection Check", -1);

            var grid = breaching as IMyCubeGrid;
            if (grid != null)
            {
                try
                {
                    var getBlocks = grid.GetBlocksInsideSphere(ref posCollectionSphere);
                    lock (DmgBlocks)
                    {
                        foreach (var block in getBlocks)
                        {
                            DmgBlocks.Add(block);
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in getBlocks: {ex}"); }
            }
            var posMatCenter = MatrixD.CreateTranslation(posCollectionSphere.Center);
            var posMatScaled = MatrixD.Rescale(posMatCenter, posCollectionSphere.Radius);
            var c3 = Color.Yellow;
            var c4 = Color.Red;
            MyStringId rangeGridResourceId = MyStringId.GetOrCompute("Build new");
            //MySimpleObjectDraw.DrawTransparentSphere(ref posMatScaled, 1f, ref c3, MySimpleObjectRasterizer.Solid, 20, null, rangeGridResourceId, 0.25f, -1);
            var rangeSphereCenter = rangeSphere.Center;
            var rangeSphereRadius = rangeSphere.Radius;
            var rangeSphereMatrix = MatrixD.CreateWorld(rangeSphereCenter);
            var rsm = MatrixD.Rescale(rangeSphereMatrix, rangeSphereRadius);
            //MySimpleObjectDraw.DrawTransparentSphere(ref rsm, 1f, ref c4, MySimpleObjectRasterizer.Solid, 20, null, rangeGridResourceId, 0.25f, -1);
            //MySimpleObjectDraw.DrawTransparentBox(ref bWorldMatrix, ref bLocalAABBs, ref c4, MySimpleObjectRasterizer.Solid, 1, 1f, null, null, true);

            var c1 = Color.Red;
            var c2 = Color.Blue;
            //DrawBox(sOriBBoxD, c1, false);
            //DrawBox(bOriBBoxD, c2, false);
            if (hitVectors.Count > 0)
            {
                collision = posCollectionSphere.Center;
                _worldImpactPosition = posCollectionSphere.Center;
            }
            return collision;

        }
        
        private void DamageGrids()
        {
            try
            {
                lock (DmgBlocks)
                {
                    foreach (var block in DmgBlocks)
                    {
                        //block.DoDamage(100f, MyDamageType.Fire, true, null, Block.EntityId);
                    }
                    DmgBlocks.Clear();
                }
                //if (_count == 0) Log.Line($"Block Count {DmgBlocks.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in DamgeGrids: {ex}"); }
        }

        public void DrawBox(MyOrientedBoundingBoxD obb, Color color, bool matrix)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromTransformScale(obb.Orientation, obb.Center, Vector3D.One);
            if (matrix) wm = wm * _shieldGridMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, MySimpleObjectRasterizer.Solid, 1, 1f, null, null, true);
        }

        private bool IntersectOld(IMyEntity ent, bool impactcheck)
        {
            var contactpoint = ContactPoint(ent);

            if (Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared() <= 1)
            {
                //Log.Line($"GridIsColliding {GridIsColliding.Count} - check {impactcheck} - containsEnt {GridIsColliding.Contains(ent as IMyCubeGrid)}");
                _impactSize = ent.Physics.Mass;
                if (impactcheck && !GridIsColliding.Contains(ent as IMyCubeGrid))
                {
                    //Log.Line($"ContactPoint to WorldImpact: {contactpoint}");
                    //_worldImpactPosition = contactpoint;
                }
                if (impactcheck && ent is IMyCubeGrid && !GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Add(ent as IMyCubeGrid);
                //if (impactcheck && _worldImpactPosition != Vector3D.NegativeInfinity) Log.Line($"intersect true: {ent} - ImpactSize: {_impactSize} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()} - _worldImpactPosition: {_worldImpactPosition}");
                return true;
            }
            //if (impactcheck) Log.Line($"intersect false: {ent.GetFriendlyName()} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
            if (ent is IMyCubeGrid && GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Remove(ent as IMyCubeGrid);
            return false;
        }

        private Vector3D Intersect(IMyEntity ent, bool impactcheck)
        {
            var contactpoint = ContactPointObb(ent);

            if (contactpoint != Vector3D.NegativeInfinity) 
            {
                //Log.Line($"GridIsColliding {GridIsColliding.Count} - check {impactcheck} - containsEnt {GridIsColliding.Contains(ent as IMyCubeGrid)}");
                _impactSize = ent.Physics.Mass;
                if (impactcheck && !GridIsColliding.Contains(ent as IMyCubeGrid))
                {
                    //Log.Line($"ContactPoint to WorldImpact: {contactpoint}");
                    _worldImpactPosition = contactpoint;
                }
                if (impactcheck && ent is IMyCubeGrid && !GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Add(ent as IMyCubeGrid);
                //if (impactcheck && _worldImpactPosition != Vector3D.NegativeInfinity) Log.Line($"intersect true: {ent} - ImpactSize: {_impactSize} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()} - _worldImpactPosition: {_worldImpactPosition}");
                return contactpoint;
            }
            //if (impactcheck) Log.Line($"intersect false: {ent.GetFriendlyName()} - {Vector3D.Transform(contactpoint, _detectionMatrixInv).LengthSquared()}");
            if (ent is IMyCubeGrid && GridIsColliding.Contains(ent as IMyCubeGrid)) GridIsColliding.Remove(ent as IMyCubeGrid);
            return Vector3D.NegativeInfinity;
        }
        #endregion

        private double ContainmentField(IMyEntity breaching, IMyEntity field, Vector3D intersect)
        {
            //var direction = Vector3D.Normalize(grid.Center() - grid.Center);
            //Vector3D velocity = grid.Physics.LinearVelocity;
            //if (Vector3D.IsZero(velocity)) velocity += direction;
            //
            //Vector3D forceDir = Vector3D.Reflect(Vector3D.Normalize(velocity), direction);
            //grid.Physics.SetSpeeds(velocity * forceDir, grid.Physics.AngularVelocity);
            //var dist = Vector3D.Distance(grid.GetPosition(), websphere.Center);
            //
            //var d = grid.Physics.CenterOfMass - thingRepellingYou;
            //var v = d * repulsionVelocity / d.Length();
            //grid.Physics.AddForce((v - grid.Physics.LinearVelocity) * grid.Physics.Mass / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);

            /*
            // local velocity of dest
            var velTarget = field.Physics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var distanceFromTargetCom = breaching.Physics.CenterOfMassWorld - field.Physics.CenterOfMassWorld;

            var accelLinear = field.Physics.LinearAcceleration;
            var omegaVector = field.Physics.AngularVelocity + field.Physics.AngularAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var omegaSquared = omegaVector.LengthSquared();
            // omega^2 * r == a
            var accelRotational = omegaSquared * -distanceFromTargetCom;
            var accelTarget = accelLinear + accelRotational;

            var velTargetNext = velTarget + accelTarget * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = breaching.Physics.LinearVelocity;// + modify.Physics.LinearAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

            var linearImpulse = breaching.Physics.Mass * (velTargetNext - velModifyNext);

            // Angular matching.
            // (dAA*dt + dAV) == (mAA*dt + mAV + tensorInverse*mAI)
            var avelModifyNext = breaching.Physics.AngularVelocity + breaching.Physics.AngularAcceleration * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var angularDV = omegaVector - avelModifyNext;
            //var angularImpulse = Vector3.Zero;
            var angularImpulse = Vector3.TransformNormal(angularDV, breaching.Physics.RigidBody.InertiaTensor); //not accessible :/

            // based on the large grid, small ion thruster.
            const double wattsPerNewton = (3.36e6 / 288000);
            // based on the large grid gyro
            const double wattsPerNewtonMeter = (0.00003 / 3.36e7);
            // (W/N) * (N*s) + (W/(N*m))*(N*m*s) == W
            var powerCorrectionInJoules = (wattsPerNewton * linearImpulse.Length()) + (wattsPerNewtonMeter * angularImpulse.Length());
            breaching.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, linearImpulse, breaching.Physics.CenterOfMassWorld, angularImpulse);
            if (recoil) field.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -linearImpulse, field.Physics.CenterOfMassWorld, -angularImpulse);

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            */

            // Calculate Power
            
            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = field.Physics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = field.Physics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = breaching.Physics.LinearVelocity;
            var linearImpulse = breaching.Physics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            // ApplyImpulse
            //var contactPoint = ContactPoint(breaching);
            var contactPoint = intersect;

            var transformInv = MatrixD.Invert(DetectionMatrix);
            var normalMat = MatrixD.Transpose(transformInv);
            var localNormal = Vector3D.Transform(contactPoint, transformInv);
            var surfaceNormal = Vector3D.Normalize(Vector3D.TransformNormal(localNormal, normalMat));

            var bmass = -breaching.Physics.Mass;
            var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
            var expelForce = (bmass / 50) / Math.Pow(cpDist, 8);
            if (expelForce < -9999000000f || bmass >= -67f) expelForce = -9999000000f;

            var worldPosition = breaching.WorldMatrix.Translation; 
            var worldDirection = contactPoint - worldPosition; 

            //breaching.Physics.ApplyImpulse(worldDirection * (expelForce / 2), contactPoint);
            //Block.CubeGrid.Physics.ApplyImpulse(Vector3D.Negate(worldDirection) * (expelForce / 2), contactPoint);

            //if (cpDist > 0.987f) breaching.Physics.ApplyImpulse((breaching.Physics.Mass / 500) * -0.055f * Vector3D.Dot(breaching.Physics.LinearVelocity, surfaceNormal) * surfaceNormal, contactPoint);
            //Log.Line($"cpDist:{cpDist} pow:{expelForce} bmass:{bmass} adjbmass{bmass / 50}");

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }

        private void GridKillField()
        {
            try
            {
                var bigkillSphere = new BoundingSphereD(_detectionCenter, _range);
                var killList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref bigkillSphere);
                if (killList.Count == 0) return;
                MyAPIGateway.Parallel.ForEach(killList, killent =>
                {
                    var grid = killent as IMyCubeGrid;
                    if (grid == null || grid == Block.CubeGrid || !IsEnemy(killent) || Intersect(killent, false) == Vector3D.NegativeInfinity) return;

                    var contactPoint = ContactPoint(killent);
                    var cpDist = Vector3D.Transform(contactPoint, _detectionMatrixInv).LengthSquared();
                    //var worldPosition = killent.WorldVolume.Center;
                    //var worldDirection = contactPoint - worldPosition;
                    //var worldDirection = worldPosition - contactPoint;


                    var killSphere = new BoundingSphereD(contactPoint, 5f);
                    if (cpDist > 0.95f && _explode == false && _explodeCount == 0)
                    {
                        //Log.Line($"EXPLOSION! - dist:{cpDist}");
                        _explode = true;
                        MyVisualScriptLogicProvider.CreateExplosion(killSphere.Center, (float) killSphere.Radius, 20000);
                    }

                    if (!(cpDist <= 0.99)) return;
                    //Log.Line($"DoDamage - dist:{cpDist}");
                    var killBlocks = grid.GetBlocksInsideSphere(ref killSphere);
                    MyAPIGateway.Parallel.ForEach(killBlocks, block =>
                    {
                        block.DoDamage(99999f, MyDamageType.Fire, true, null, Block.EntityId);
                    });
                });

            } catch (Exception ex) { Log.Line($"Exception in GridKillField: {ex}"); }
        }

        #region Build inside HashSet
        private void InHashBuilder()
        {
            var insphere = new BoundingSphereD(_detectionCenter, _range - InOutSpace);
            var inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (!(inent is IMyCubeGrid) && (!(inent is IMyCharacter) || Intersect(inent, false) == Vector3D.NegativeInfinity)) return;
                lock (InHash)
                {
                    if (inent is IMyCubeGrid && IsEnemy(inent)) return;
                    InHash.Add(inent);
                }
            });
        }
        #endregion

        #region Web and dispatch all intersecting entities
        private void WebEntities()
        {
            var websphere = new BoundingSphereD(_detectionCenter, _range);
            var webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (Intersect(webent, true) != Vector3D.NegativeInfinity)
                    {
                        _absorb += Shotdmg;
                        Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {webent} in loop {_count}");
                        if (webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo")) MyVisualScriptLogicProvider.CreateExplosion(webent.GetPosition(), 0, 0);
                        webent.Close();
                    }
                    return;
                }
                if (webent is IMyCharacter && (_count == 2 || _count == 17 || _count == 32 || _count == 47) && IsEnemy(webent) && Intersect(webent, true) != Vector3D.NegativeInfinity)
                {
                    Log.Line($"Enemy Player Intersected");
                }

                if (webent is IMyCharacter) return; //|| InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == Block.CubeGrid || grid == null || !IsEnemy(webent)) return;

                var intersect = Intersect(grid, true);
                if (intersect != Vector3D.NegativeInfinity)
                {
                    ContainmentField(grid, Block.CubeGrid, intersect);
                }
                //Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
        }
        #endregion

        #region player effects
        private void PlayerEffects()
        {
            var rnd = new Random();
            foreach (var playerent in InHash)
            {
                if (!(playerent is IMyCharacter)) continue;
                try
                {
                    var playerid = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                    var relationship = Block.GetUserRelationToOwner(playerid);
                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        var character = playerent as IMyCharacter;

                        var npcname = character.ToString();
                        //Log.Line($"playerEffect: Enemy {character} detected at loop {Count} - relationship: {relationship}");
                        if (npcname.Equals("Space_Wolf"))
                        {
                            Log.Line($"playerEffect: Killing {character}");
                            character.Kill();
                            return;
                        }
                        if (character.EnabledDamping) character.SwitchDamping();
                        if (character.SuitEnergyLevel > 0.5f) MyVisualScriptLogicProvider.SetPlayersEnergyLevel(playerid, 0.49f);
                        if (character.EnabledThrusts)
                        {
                            _playertime++;
                            var explodeRollChance = rnd.Next(0 - _playertime, _playertime);
                            if (explodeRollChance > 666)
                            {
                                _playertime = 0;
                                var hydrogenId = MyCharacterOxygenComponent.HydrogenId;
                                var playerGasLevel = character.GetSuitGasFillLevel(hydrogenId);
                                if (playerGasLevel > 0.01f)
                                {
                                    character.Components.Get<MyCharacterOxygenComponent>().UpdateStoredGasLevel(ref hydrogenId, (playerGasLevel * -0.0001f) + .002f);
                                    MyVisualScriptLogicProvider.CreateExplosion(character.GetPosition(), 0, 0);
                                    character.DoDamage(50f, MyDamageType.Fire, true);
                                    var vel = character.Physics.LinearVelocity;
                                    if (vel == new Vector3D(0, 0, 0))
                                    {
                                        vel = MyUtils.GetRandomVector3Normalized();
                                    }
                                    var speedDir = Vector3D.Normalize(vel);
                                    var randomSpeed = rnd.Next(10, 20);
                                    var additionalSpeed = vel + speedDir * randomSpeed;
                                    character.Physics.LinearVelocity = additionalSpeed;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in playerEffects: {ex}"); }
            }
            _playerwebbed = false;
        }
        #endregion

        #region Cleanup
        public override void Close()
        {
            try
            {
                DefenseShieldsBase.Instance.Shields.RemoveAt(DefenseShieldsBase.Instance.Shields.IndexOf(this));
            }
            catch { }
            base.Close();
        }

        public override void MarkForClose()
        {
            try { }
            catch { }
            base.MarkForClose();
        }
        #endregion

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
            Log.Line($"UseThisShip_Receiver({fix})");

            //UseThisShip_Internal(fix);
        }
    }
}
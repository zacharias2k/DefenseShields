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
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using DefenseShields.Support;
using Sandbox.Game.Gui;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, new string[] { "StationDefenseShield" })]
    public class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;							   															   							   
        private float _recharge;
        private float _absorb;
        private float _power = 0.0001f;
        private const float Shotdmg = 1f;
        private const float Bulletdmg = 0.1f;
        private const float Massdmg = 0.0025f;
        private const float InOutSpace = 15f;

        public int Count = -1;
        public int Playercount = 600;
        public int Gridcount = 600;
        private int _time;
        private int _playertime;
        private int _impact = 60;

        public bool Initialized = true;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _shotwebbed;
        private bool _shotlocked;
        private bool _closegrids;
        private bool _playerkill;

        private const ushort ModId = 50099;

        private readonly Icosphere _sphere = new Icosphere(6);
        private Vector3D _worldImpactPosition;

        private MatrixD _worldMatrix;
        public MatrixD DetectMatrix;
        //MatrixD _detectMatrix = MatrixD.Identity;
        private MyEntitySubpart _subpartRotor;
        public RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> Slider;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> Ellipsoid;
        public MyResourceSinkComponent Sink;
        public MyDefinitionId PowerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private List<Matrix> _matrixArmsOff = new List<Matrix>();
        private List<Matrix> _matrixArmsOn = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        public MyConcurrentHashSet<IMyEntity> InHash = new MyConcurrentHashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyGridHash = new HashSet<IMyEntity>();
        public static HashSet<IMyEntity> DestroyPlayerHash = new HashSet<IMyEntity>();

        private readonly MyStringId _faceId = MyStringId.GetOrCompute("Build new");
        private readonly MyStringId _lineId = MyStringId.GetOrCompute("Square");


        public static readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();

        private IMyOreDetector _oblock;
        private IMyFunctionalBlock _fblock;
        private IMyTerminalBlock _tblock;
        private MyCubeBlock _cblock;
        private IMyEntity _shield1;
        private IMyEntity _shield2;
        #endregion

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            //this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            _cblock = (MyCubeBlock)Entity;
            _oblock = Entity as IMyOreDetector; 
            _fblock = Entity as IMyFunctionalBlock;
            _tblock = Entity as IMyTerminalBlock;
        }
        #endregion

        #region Interfaces
        public interface IPlayerKill{ void PlayerKill(); }
        public interface IGridClose { void GridClose(); }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_animInit)
                {
                    if (_subpartRotor.Closed.Equals(true) && !Initialized && _cblock.IsWorking)
                    {
                        BlockAnimationReset();
                    }
                    BlockAnimation();
                }
                if (Count++ == 59) Count = 0;
                if (Count <= 0)
                {
                    if (Initialized)
                    {
                        //Log.Line($" InHash Count? {InHash.Count} - Inited? {!Initialized}");
                        Count = -1;
                        InHashBuilder();
                        return;
                    }
                    MyAPIGateway.Parallel.Do(InHashBuilder);
                }
                if (Playercount < 600) Playercount++;
                if (Gridcount < 600) Gridcount++;
                //if (_impact == Count) WorldImpactPosition = new Vector3D(0, 0, 0);
                if (Count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    _tblock.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_playerkill || Playercount == 479)
                {
                    if (_playerkill) Playercount = -1;
                    _playerkill = false;
                    if (DestroyPlayerHash.Count > 0) DestroyEntity.PlayerKill(Playercount);
                }
                if (_closegrids || Gridcount == 59 || Gridcount == 179 || Gridcount == 299 || Gridcount == 419 || Gridcount == 479|| Gridcount == 599)
                {
                    if (_closegrids) Gridcount = -1;
                    _closegrids = false;
                    if (DestroyGridHash.Count > 0) DestroyEntity.GridClose(Gridcount);
                }
                if (!Initialized && _cblock.IsWorking)
                {
                    //if (!MyAPIGateway.Utilities.IsDedicated) MyAPIGateway.Parallel.Do(() => DrawShield(_range)); //Check
                    if (!MyAPIGateway.Utilities.IsDedicated) DrawShield(_range); //Check
                    else SendPoke(_range); //Check
                    MyAPIGateway.Parallel.StartBackground(WebEntities);
                    if (_shotwebbed && !_shotlocked) MyAPIGateway.Parallel.Do(ShotEffects);
                    if (_playerwebbed) MyAPIGateway.Parallel.Do(PlayerEffects);
                }
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in UpdateBeforeSimulation");
                Log.Line($" {ex}");
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (Initialized)
            {
                Log.Line($" Create UI {Count}");
                CreateUi();
                ((IMyFunctionalBlock)_cblock).AppendingCustomInfo += AppendingCustomInfo;
                _tblock.RefreshCustomInfo();
                _absorb = 150f;

                _shield1 = Spawn.Utils.SpawnShield("LargeField", "", true, false, false, false, false, _cblock.IDModule.Owner);
                _shield2 = Spawn.Utils.SpawnShield("ImpactField", "", true, false, false, false, false, _cblock.IDModule.Owner);
                _shield1.Render.Visible = false;
                _shield2.Render.Visible = false;
                Initialized = false;
                #region Voxel Code
                /*
                var diameter = 600;
                double maxDiameter = 0;
                maxDiameter = Math.Max(maxDiameter, diameter);

                var position = _tblock.GetPosition();
                var layers = new List<Voxels.Voxels.AsteroidSphereLayer>();

                byte material = 1;
                var materialName = "Stone_01";
                var name = "test";
                var length = (int)((maxDiameter / 2) + 4).RoundUpToCube();
                var size = new Vector3I(length, length, length);
                var origin = new Vector3I(size.X / 2, size.Y / 2, size.Z / 2);
                //layers = layers.OrderByDescending(e => e.Diameter).ToList();
                layers.Add(new Voxels.Voxels.AsteroidSphereLayer() { Diameter = diameter, Material = material, MaterialName = materialName });
                layers.Add(new Voxels.Voxels.AsteroidSphereLayer() { Diameter = 590f, Material = 255, MaterialName = materialName });
                Voxels.Voxels.CreateNewAsteroid(name, size, position);

                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(origin.X - 2, origin.Y - 2, origin.Z - 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(-origin.X + 2, origin.Y - 2, origin.Z - 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(origin.X - 2, -origin.Y + 2, origin.Z - 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(-origin.X + 2, -origin.Y + 2, origin.Z - 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(origin.X - 2, origin.Y - 2, -origin.Z + 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(-origin.X + 2, origin.Y - 2, -origin.Z + 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(origin.X - 2, -origin.Y + 2, -origin.Z + 2), origin, layers);
                Voxels.Voxels.ProcessAsteroid(name, size, position, new Vector3D(-origin.X + 2, -origin.Y + 2, -origin.Z + 2), origin, layers);
                */
                #endregion
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (!_animInit)
                {
                    if (_oblock.BlockDefinition.SubtypeId == "StationDefenseShield")
                    {
                        if (!_oblock.IsFunctional) return;
                        BlockAnimationInit();
                        Log.Line($" BlockAnimation {Count}");
                        _animInit = true;
                    }
                    else
                    {
                        NeedsUpdate = MyEntityUpdateEnum.NONE;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in UpdateAfterSimulation");
                Log.Line($"{ex}");
            }
        }
        #endregion

        #region Block Animation
        public void BlockAnimationReset()
        {
            Log.Line($"Resetting BlockAnimation in loop {Count}");
            _subpartRotor.Subparts.Clear();
            _subpartsArms.Clear();
            _subpartsReflectors.Clear();
            BlockAnimationInit();
        }

        public void BlockAnimationInit()
        {
            try
            {
                _animStep = 0f;

                _matrixArmsOff = new List<Matrix>();
                _matrixArmsOn = new List<Matrix>();
                _matrixReflectorsOff = new List<Matrix>();
                _matrixReflectorsOn = new List<Matrix>();

                _worldMatrix = Entity.WorldMatrix;
                _worldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;

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
            catch (Exception ex)
            {
                Log.Line($"Exception in BlockAnimation");
                Log.Line($"{ex}");
            }
        }

        public void BlockAnimation()
        {
            _worldMatrix = Entity.WorldMatrix;
            _worldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;
            //Animations
            if (_fblock.Enabled && _fblock.IsFunctional && _cblock.IsWorking)
            {
                //Color change for on =-=-=-=-
                _subpartRotor.SetEmissiveParts("Emissive", Color.White, 1);
                _time += 1;
                Matrix temp1 = Matrix.CreateRotationY(0.1f * _time);
                temp1.Translation = _subpartRotor.PositionComp.LocalMatrix.Translation;
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {
                //Color change for off =-=-=-=-
                _subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
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

        #region Power Logic
        float GetRadius()
        {
            return Slider.Getter((IMyTerminalBlock)_cblock);
        }

        public float CalcRequiredPower()
        {

            if (!Initialized)
            {
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
                var radius = Slider.Getter((IMyFunctionalBlock)_cblock);
                var sustaincost = radius * 0.01f;
                _power = _recharge + sustaincost;
                //Log.Line(String.Format("{0} - Sustain cost is {1}MW this and recharge cost is {2}MW", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), sustaincost, _recharge));
                return _power;
            }
            return _power;
        }

        void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");

            _range = GetRadius();
            if (Ellipsoid.Getter(block).Equals(true))
            {
                _width = _range * 0.5f;
                _height = _range * 0.35f;
                _depth = _range;
            }
            else
            {
                _width = _range;
                _height = _range;
                _depth = _range;
            }
        }
        #endregion

        #region Cleanup
        public override void Close()
        {
            try
            {
                //MyAPIGateway.Entities.RemoveEntity(Shield);
            }
            catch{}
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
                //MyAPIGateway.Entities.RemoveEntity(Shield);
            }
            catch {}
            base.MarkForClose();
        }
        #endregion

        #region Create UI
        void RemoveOreUi()
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

        bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        public void CreateUi()
        {
            DefenseShieldsBase.ControlsLoaded = true;
            RemoveOreUi();

            
            Ellipsoid = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)_cblock,
                "Ellipsoid",
                "Switch to Ellipsoid",
                false);
            
            Slider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyFunctionalBlock)_cblock,
                "RadiusSlider",
                "Shield Size",
                50,
                300,
                300);
        }
        #endregion

        #region Server-client comms
        [ProtoContract(UseProtoMembersOnly = true)]
        public class Poke
        {
            [ProtoMember(1)] public ushort ModId;
            [ProtoMember(2)]
            public float Size { get; set; }
        }

        public void SendPoke(float size)
        {
            bool sent;
            Poke info = new Poke();
            info.ModId = ModId;
            info.Size = size;
            sent = MyAPIGateway.Multiplayer.SendMessageToOthers(ModId, MyAPIGateway.Utilities.SerializeToBinary(info), true);
        }

        public void GetPoke(byte[] data)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<Poke>(data);
            Poke info = new Poke();
            try
            {
                info = message;
                if (info.ModId == ModId)
                {
                    DrawShield(info.Size);
                }
            }
            catch (Exception ex)
            {
                Log.Line($"Exception in getPoke");
                Log.Line($"{ex}");
            }
        }
        #endregion

        private bool Distance(int x)
        {
            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = _cblock.CubeGrid.PositionComp.GetPosition();
            var range = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + _range) * (x + _range);
            if (range)
            {
                return true;
            }
            return false;
        }

        #region Draw Shield
        public void DrawShield(float size)
        {

            var sp = new BoundingSphereD(_cblock.Position, _range);
            var sphereOnCamera = MyAPIGateway.Session.Camera.IsInFrustum(ref sp);
            int lod = 1;

            if (Distance(400)) lod = 5;
            else if (Distance(2250)) lod = 4;
            else if (Distance(4500)) lod = 3;
            else if (Distance(7000)) lod = 2;
            else lod = 1;

            //var sphereOnCamera = MyAPIGateway.Session.Camera.WorldToScreen(ref test).AbsMax() < 1;
            //_toDraw.GetAllInSphere(tmpToDraw, ref sp);
            //CenterDot = new HudAPIv2.BillBoardHUDMessage(MyAPIGateway.Session.Player, Vector2D.Zero, Color.Blue);
            //CenterDot.Options |= HudAPIv2.Options.Shadowing | HudAPIv2.Options.Fixed;
            //CenterDot.Scale = 0.0012d;
            //CenterDot.Visible = false;

            //var wiredraw = 1 << Math.Clamp((int) (5 * _range / Math.Sqrt(_cblock.WorldMatrix.Translation.Length(MyAPIGateway.Session.Camera.Position)), 1, 5));
            var relations = _tblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            bool enemy;
            var edgeMatrix1 = MatrixD.Rescale(_worldMatrix, new Vector3D(_width, _height, _depth));
            var edgeMatrix2 = MatrixD.Rescale(_worldMatrix, new Vector3D(_width, _height, _depth));
            DetectMatrix = edgeMatrix2;

            if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) enemy = false;
            else enemy = true;
            var colour = Color.FromNonPremultiplied(255, 80, 16, 72);

            if (!_shield1.WorldMatrix.Equals(edgeMatrix2)) _shield1.SetWorldMatrix(edgeMatrix2);
            if (!_shield2.WorldMatrix.Equals(edgeMatrix2)) _shield2.SetWorldMatrix(edgeMatrix2);
            if (sphereOnCamera)
            {
            _sphere.Draw(edgeMatrix1, _range, lod, Count, enemy, _worldImpactPosition, DetectMatrix, _shield1, _shield2, _faceId, _lineId, -1);
            }
        }
        #endregion

        #region Impact
        private void ImpactTimer(IMyEntity ent)
        {
            _worldImpactPosition = ent.GetPosition();
            //if (Count != 0) _impact = Count - 1;
            //else _impact = 59;
        }
        #endregion

        #region Detect Intersection
        private bool Detectedge(IMyEntity ent, float f)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / ((_width - f) * (_width - f)) + (y * y) / ((_depth - f) * (_depth - f)) + (z * z) / ((_height - f) * (_height - f));
            if (detect <= 1)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Build inside HashSet
        public void InHashBuilder()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            var insphere = new BoundingSphereD(pos, _range - InOutSpace);
            List<IMyEntity> inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            InHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (inent is IMyCubeGrid || inent is IMyCharacter && Detectedge(inent, InOutSpace))
                {
                    lock (InHash)
                    {
                        InHash.Add(inent);
                    }
                }
            });
            //Log.Line($" inHash {InHash.Couno st} l:{Count}");
        }
        #endregion

        #region Web and dispatch all intersecting entities
        public void WebEntities()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);

            var websphere = new BoundingSphereD(pos, _range);
            List<IMyEntity> webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent.Physics.IsStatic)
                {
                    Log.Line($"webEffect static {webent}");
                    return;
                }
                if (webent is IMyMeteor  || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (_shotwebbed) return;
                    if (Detectedge(webent, 0f))
                    {
                        _shotwebbed = true;
                    }
                    return;
                }
                if (webent is IMyCharacter && (Count == 2 || Count == 17 || Count == 32 || Count == 47) && Detectedge(webent, 0f))
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(webent).IdentityId;
                    var playerrelationship = _tblock.GetUserRelationToOwner(dude);
                    if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                    _playerwebbed = true;
                }
                
                if (webent is IMyCharacter || InHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == _tblock.CubeGrid || DestroyGridHash.Contains(grid) || grid == null) return;

                List<long> owners = grid.BigOwners;
                if (owners.Count > 0)
                {
                    var relations = _tblock.GetUserRelationToOwner(owners[0]);
                    //Log.Line(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                    if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                }
                if (Detectedge(grid, 0f))
                {
                    ImpactTimer(grid);
                    float griddmg = grid.Physics.Mass * Massdmg;
                    _absorb += griddmg;
                    Log.Line($" gridEffect: {grid} Shield Strike by a {(griddmg / Massdmg)}kilo grid, absorbing {griddmg}MW of energy in loop {Count}");

                    _closegrids = true;
                    DestroyGridHash.Add(grid);

                    var vel = grid.Physics.LinearVelocity;
                    vel.SetDim(0, (int)(vel.GetDim(0) * -8.0f));
                    vel.SetDim(1, (int)(vel.GetDim(1) * -8.0f));
                    vel.SetDim(2, (int)(vel.GetDim(2) * -8.0f));
                    grid.Physics.LinearVelocity = vel;
                    /*
                    var direction = Vector3D.Normalize(grid.Center() - grid.Center);
                    Vector3D velocity = grid.Physics.LinearVelocity;
                    if (Vector3D.IsZero(velocity))
                        velocity += direction;
                    Vector3D forceDir = Vector3D.Reflect(Vector3D.Normalize(velocity), direction);
                    grid.Physics.SetSpeeds(velocity * forceDir, grid.Physics.AngularVelocity);
                    var dist = Vector3D.Distance(grid.GetPosition(), websphere.Center);

                    var d = grid.Physics.CenterOfMass - thingRepellingYou;
                    var v = d * repulsionVelocity / d.Length();
                    grid.Physics.AddForce((v - grid.Physics.LinearVelocity) * grid.Physics.Mass / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                    */
                    var playerchar = MyAPIGateway.Players.GetPlayerControllingEntity(grid).Character;
                    if (playerchar != null)
                    {
                        DestroyPlayerHash.Add(playerchar);
                        _playerkill = true;
                    }
                    return;
                }
                Log.Line($"webEffect unmatched {webent.GetFriendlyName()} {webent.Name} {webent.DisplayName} {webent.EntityId} {webent.Parent} {webent.Components}");
            });
        }
        #endregion

        #region shot effects
        public void ShotEffects()
        {
            _shotlocked = true;
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            var shotHash = new HashSet<IMyEntity>();
            var shotsphere = new BoundingSphereD(pos, _range);
            MyAPIGateway.Entities.GetEntities(shotHash, ent => shotsphere.Intersects(ent.WorldAABB) && ent is IMyMeteor || ent.ToString().Contains("Missile") || ent.ToString().Contains("Torpedo"));

            MyAPIGateway.Parallel.ForEach(shotHash, shotent =>
            {
                if (shotent == null || !Detectedge(shotent, 0f)) return;
                try
                {
                    ImpactTimer(shotent);
                    _absorb += Shotdmg;
                    Log.Line($"shotEffect: Shield absorbed {Shotdmg}MW of energy from {shotent} in loop {Count}");
                    shotent.Close();
                }
                catch (Exception ex)
                {
                    Log.Line($"Exception in shotEffects");
                    Log.Line($"{ex}");
                }
            });
            _shotwebbed = false;
            _shotlocked = false;
        }
        #endregion

        #region player effects
        public void PlayerEffects()
        {
            var rnd = new Random();
            //MyAPIGateway.Parallel.ForEach(InHash, playerent =>
            foreach (var playerent in InHash)
            {
                if (!(playerent is IMyCharacter)) continue;
                try
                {
                    var playerid = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                    var relationship = _tblock.GetUserRelationToOwner(playerid);
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
                catch (Exception ex)
                {
                    Log.Line($" Exception in playerEffects");
                    Log.Line($" {ex}");
                }
                //});
            }
            _playerwebbed = false;
        }
        #endregion
    }
}
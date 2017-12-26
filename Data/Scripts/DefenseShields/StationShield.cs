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
using VRage.Collections;
using SpaceEngineers.Game.ModAPI;
using VRage;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using VRage.Game.ModAPI.Interfaces;
using Sandbox.Definitions;
using Sandbox.Common;
using Sandbox.Engine;
using Sandbox.Engine.Multiplayer;

namespace DefenseShields.Station
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, new string[] { "StationDefenseShield" })]
    class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        private float _animStep;
        private float _range;
        private float _width;
        private float _height;
        private float _depth;
        private float _inWidth;
        private float _inHeight;
        private float _inDepth;
        private float _inRange;
        private float _recharge;
        private float _power = 0.0001f;
        private float _absorb;
        private float _shotdmg = 1f;
        private float _bulletdmg = 0.1f;
        private float _massdmg = 0.0025f;

        private readonly float _inOutSpace = 15f;

        public int Count = -301;
        public int GenCount = -1;
        private int _colourRand = 32;
        private int _time;
        private int _playertime;

        public bool Initialized = true;
        private bool _animInit;
        private bool _playerwebbed;
        private bool _gridwebbed;
        private bool _gridlocked;
        private bool _shotwebbed;
        private bool _shotlocked;
        private bool _closegrids;
        private bool _playerkill;
        public bool _insideReady;

        private ushort _modId = 50099;

        private static Random _random = new Random();
        private MatrixD _worldMatrix;
        //MatrixD _detectMatrix = MatrixD.Identity;
        private Vector3D _edgeVectors;
        private Vector3D _inVectors;
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

        public MyConcurrentHashSet<IMyEntity> _inHash = new MyConcurrentHashSet<IMyEntity>();
        public MyConcurrentHashSet<IMyEntity> _gridCloseHash = new MyConcurrentHashSet<IMyEntity>();
        private List<long?> _playerKillList = new List<long?>();


        public static readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();

        private IMyOreDetector _oblock; 
        private IMyFunctionalBlock _fblock;
        private IMyTerminalBlock _tblock;
        private IMyCubeBlock _cblock;
        #endregion

        #region Init
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.Components.TryGet<MyResourceSinkComponent>(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);

            this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            //this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            _cblock = (IMyCubeBlock)Entity;
            _oblock = Entity as IMyOreDetector; 
            _fblock = Entity as IMyFunctionalBlock;
            _tblock = Entity as IMyTerminalBlock;
        }
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
                if (GenCount < 600) GenCount++;
                if (Count++ == 59) Count = 0;
                if (Count % 3 == 0)
                {
                    _colourRand += (16 - _random.Next(1, 32));
                    if (_colourRand < 0) _colourRand = 0;
                    else if (_colourRand > 64) _colourRand = 64;
                }
                if (!MyAPIGateway.Utilities.IsDedicated) DrawShield(_range); //Check
                else SendPoke(_range); //Check
                if (Count == 29 && _absorb > 0)
                {
                    CalcRequiredPower();
                    _tblock.GameLogic.GetAs<DefenseShields>().Sink.Update();
                }
                if (_playerkill || GenCount == 299)
                {
                    if (_playerkill) GenCount = -1;
                    _playerkill = false;
                    PlayerKill();
                }
                if (_closegrids || GenCount == 599)
                {
                    if (_closegrids) GenCount = -1;
                    _closegrids = false;
                    GridClose();
                }
                if (!Initialized && _cblock.IsWorking)
                {
                    if (Count <= 0) MyAPIGateway.Parallel.Do(InHashBuilder);
                    MyAPIGateway.Parallel.StartBackground(WebEffects);
                    if (_shotwebbed && !_shotlocked) MyAPIGateway.Parallel.Do(ShotEffects);
                    if (_gridwebbed && !_gridlocked) MyAPIGateway.Parallel.Do(GridEffects);
                    if (_playerwebbed) MyAPIGateway.Parallel.Do(PlayerEffects);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in UpdateBeforeSimulation", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (Initialized)
            {
                Logging.WriteLine(String.Format("{0} - Create UI {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), Count));
                CreateUi();
                ((IMyFunctionalBlock)_cblock).AppendingCustomInfo += AppendingCustomInfo;
                _tblock.RefreshCustomInfo();
                _absorb = 150f;
                Initialized = false;

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
                        Logging.WriteLine(String.Format("{0} - BlockAnimation {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), Count));

                        _animInit = true;
                    }
                    else
                    {
                        this.NeedsUpdate = MyEntityUpdateEnum.NONE;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in UpdateAfterSimulation", DateTime.Now));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }
        #endregion

        #region Block Animation
        public void BlockAnimationReset()
        {
            Logging.WriteLine(String.Format("{0} - Resetting BlockAnimation in loop {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), Count));
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

                for (int i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    _subpartRotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    _matrixArmsOff.Add(temp1.PositionComp.LocalMatrix);
                    Matrix temp2 = temp1.PositionComp.LocalMatrix.GetOrientation();
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
                            temp2 *= Matrix.CreateRotationX(0.98f); ;
                            break;
                    }
                    temp2.Translation = temp1.PositionComp.LocalMatrix.Translation;
                    _matrixArmsOn.Add(temp2);
                    _subpartsArms.Add(temp1);
                }

                for (int i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    _subpartsArms[i].TryGetSubpart("Reflector", out temp3);
                    _subpartsReflectors.Add(temp3);
                    _matrixReflectorsOff.Add(temp3.PositionComp.LocalMatrix);
                    Matrix temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    _matrixReflectorsOn.Add(temp4);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in BlockAnimation", DateTime.Now));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
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
            for (int i = 0; i < 8; i++)
            {
                if (i < 4)
                {
                    _subpartsReflectors[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixReflectorsOff[i], _matrixReflectorsOn[i], _animStep);
                }
                _subpartsArms[i].PositionComp.LocalMatrix = Matrix.Slerp(_matrixArmsOff[i], _matrixArmsOn[i], _animStep);
            }
        }
        #endregion

        #region Update Power+Range
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
                Logging.WriteLine(String.Format("{0} - Sustain cost is {1}MW this and recharge cost is {2}MW", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), sustaincost, _recharge));
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
                _inDepth = _depth - _inOutSpace;
                _inHeight = _height - _inOutSpace;
                _inWidth = _width - _inOutSpace;
                _inRange = _range - _inOutSpace;
            }
            else
            {
                _width = _range;
                _height = _range;
                _depth = _range;
                _inDepth = _depth - _inOutSpace;
                _inHeight = _height - _inOutSpace;
                _inWidth = _width - _inOutSpace;
                _inRange = _range - _inOutSpace;
            }
        }
        #endregion

        #region Cleanup
        public override void Close()
        {
            try
            {
            }
            catch
            {
            }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch
            {
            }
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
            info.ModId = _modId;
            info.Size = size;
            sent = MyAPIGateway.Multiplayer.SendMessageToOthers(_modId, MyAPIGateway.Utilities.SerializeToBinary(info), true);
        }

        public void GetPoke(byte[] data)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<Poke>(data);
            Poke info = new Poke();
            try
            {
                info = message;
                if (info.ModId == _modId)
                {
                    DrawShield(info.Size);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in getPoke", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
            }
        }
        #endregion

        #region Draw Shield
        public void DrawShield(float size)
        {
            if (!Initialized && _cblock.IsWorking)
            {
                Color colour;
                var relations = _tblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
                if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    colour = Color.FromNonPremultiplied(16, 255 - _colourRand, 16 + _colourRand, 72);
                else
                    colour = Color.FromNonPremultiplied(255 - _colourRand, 80 + _colourRand, 16, 72);
                //var matrix = MatrixD.Rescale(_worldMatrix, new Vector3D(_width, _height, _depth));
                //MySimpleObjectDraw.DrawTransparentSphere(ref matrix, 1f, ref colour, MySimpleObjectRasterizer.Solid, 24, MyStringId.GetOrCompute("Square"));
                _edgeVectors = new Vector3(_depth, _height, _width);
                //_inVectors = new Vector3(_inDepth, _inHeight, _inWidth);
                MatrixD edgeMatrix = MatrixD.CreateFromTransformScale(Quaternion.CreateFromRotationMatrix(_worldMatrix.GetOrientation()), _worldMatrix.Translation, _edgeVectors);
                MySimpleObjectDraw.DrawTransparentSphere(ref edgeMatrix, 1f, ref colour, MySimpleObjectRasterizer.Solid, 24, null, MyStringId.GetOrCompute("Build new"), 0.25f, -1);
                //MatrixD inMatrix = MatrixD.CreateFromTransformScale(Quaternion.CreateFromRotationMatrix(_worldMatrix.GetOrientation()), _worldMatrix.Translation, _inVectors);
                //MySimpleObjectDraw.DrawTransparentSphere(ref inMatrix, 1f, ref colour, MySimpleObjectRasterizer.Solid, 24, null, MyStringId.GetOrCompute("Build new"), 0.25f, -1);
            }

        }
        #endregion

        #region Detection Methods
        private bool Detectin(IMyEntity ent)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_inWidth  * _inWidth) + (y * y) / (_inDepth * _inDepth) + (z * z) / (_inHeight * _inHeight);
            if (detect <= 1)
            {
                //Logging.WriteLine(String.Format("{0} - {1} in-t: x:{2} y:{3} z:{4} d:{5} l:{6}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, x, y, z, detect, Count));
                return true;
            }
            //Logging.WriteLine(String.Format("{0} - {1} in-f - d:{5} l:{6}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
            return false;
        }

        private bool Detectedge(IMyEntity ent)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_width * _width) + (y * y) / (_depth * _depth) + (z * z) / (_height * _height);
            if (detect <= 1)
            {
                //Logging.WriteLine(String.Format("{0} - {1} edge-t - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
                return true;
            }
            //if (detect <= 1.1) Logging.WriteLine(String.Format("{0} - {1} edge-f - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
            return false;
        }

        private bool Detectgridedge(IMyCubeGrid grid)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_width * _width) + (y * y) / (_depth * _depth) + (z * z) / (_height * _height);
            if (detect <= 1)
            {
                Logging.WriteLine(String.Format("{0} - {1} grid-t - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, detect, Count));
                return true;
            }
            Logging.WriteLine(String.Format("{0} - {1} grid-f - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, detect, Count));
            return false;
        }
        #endregion

        #region Build inside HashSet
        public void InHashBuilder()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD insphere = new BoundingSphereD(pos, _inRange);
            List<IMyEntity> inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            _inHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (!(inent is IMyCubeGrid) && !(inent is IMyCharacter)) return;
                if (Detectin(inent)) _inHash.Add(inent);
            });
        }
        #endregion

        #region Webing effects
        public void WebEffects()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);

            BoundingSphereD websphere = new BoundingSphereD(pos, _range);
            List<IMyEntity> webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor  && !_shotwebbed) _shotwebbed = true;
                if (webent is IMyMeteor) return;
                
                if (webent is IMyCharacter && (Count == 14 || Count == 29 || Count == 44 || Count == 59) && Detectedge(webent))
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(webent).IdentityId;
                    var playerrelationship = _tblock.GetUserRelationToOwner(dude);
                    if (playerrelationship != MyRelationsBetweenPlayerAndBlock.Owner && playerrelationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        _playerwebbed = true;
                        return;
                    }
                    return;
                }
                
                if (webent is IMyCharacter) return;
                if (_inHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == _tblock.CubeGrid || _gridwebbed) return;
                if (grid != null)
                {
                    List<long> owners = grid.BigOwners;
                    if (owners.Count > 0)
                    {
                        var relations = _tblock.GetUserRelationToOwner(owners[0]);
                        //Logging.WriteLine(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                        if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                    }
                    //double abs = Math.Abs(grid.WorldAABB.HalfExtents.Dot(grid.WorldAABB.Center - websphere.Center) * 2);
                    //double abs = Math.Abs(grid.WorldAABB.HalfExtents.Dot(grid.WorldAABB.Max - websphere.Center) * 2);
                    if (Detectgridedge(grid))
                    {
                        Logging.WriteLine(String.Format("{0} - webEffect-grid: pass grid: {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName));
                        _gridwebbed = true;
                        return;
                    }
                    return;
                }
                if (_shotwebbed) return;
                if (webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo")) //&& Detectedge(webent))
                {
                    _shotwebbed = true;
                }
                Logging.WriteLine(String.Format("{0} - webEffect unmatched: {1} {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), webent.GetFriendlyName(), webent.DisplayName, webent.Name));
            });
        }
        #endregion

        #region shot effects

        public void ShotEffects()
        {
            _shotlocked = true;
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            HashSet<IMyEntity> shotHash = new HashSet<IMyEntity>();
            BoundingSphereD shotsphere = new BoundingSphereD(pos, _range);
            MyAPIGateway.Entities.GetEntities(shotHash, ent => shotsphere.Intersects(ent.WorldAABB) && ent is IMyMeteor || ent.ToString().Contains("Missile") || ent.ToString().Contains("Torpedo"));

            MyAPIGateway.Parallel.ForEach(shotHash, shotent =>
            {
                if (shotent == null || !Detectedge(shotent)) return;
                try
                {
                    _absorb += _shotdmg;
                    Logging.WriteLine(String.Format("{0} - shotEffect: Shield absorbed {1}MW of energy from {2} in loop {3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), shotent, _shotdmg, Count));
                    shotent.Close();
                }
                catch (Exception ex)
                {
                    Logging.WriteLine(String.Format("{0} - Exception in shotEffects", DateTime.Now));
                    Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
                }
            });
            _shotwebbed = false;
            _shotlocked = false;
        }
        #endregion

        #region Grid effects
        public void GridEffects()
        {
            _gridlocked = true;
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD gridsphere = new BoundingSphereD(pos, _range);
            List<IMyEntity> gridList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref gridsphere);
            MyAPIGateway.Parallel.ForEach(gridList, grident =>
            {
                var grid = grident as IMyCubeGrid;
                if ((grid != null) && !_inHash.Contains(grid))
                {
                    if (grid == _tblock.CubeGrid) return;
                    try
                    {
                        List<long> owners = grid.BigOwners;
                        if (owners.Count > 0)
                        {
                            var relations = _tblock.GetUserRelationToOwner(owners[0]);
                            //Logging.WriteLine(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                            if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                        }
                        if (Detectgridedge(grid))
                        {

                            float griddmg = grid.Physics.Mass * _massdmg;
                            _absorb += griddmg;
                            Logging.WriteLine(String.Format("{0} - gridEffect: {1} Shield Strike by a {2}kilo grid, absorbing {3}MW of energy in loop {4}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid, (griddmg / _massdmg), griddmg, Count));

                            long? dude = MyAPIGateway.Players.GetPlayerControllingEntity(grid)?.IdentityId;
                            if (dude != null)
                            {
                                _playerKillList.Add(dude);
                                _playerkill = true;
                            }
                            _closegrids = true;
                            _gridCloseHash.Add(grid);
                        }

                    }
                    catch (Exception ex)
                    {
                        Logging.WriteLine(string.Format("{0} - Exception in gridEffects", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                        Logging.WriteLine(string.Format("{0} - {1}", DateTime.Now, ex));
                    }
                }
            });
            _gridwebbed = false;
            _gridlocked = false;
        }
        #endregion

        #region player effects

        public void PlayerEffects()
        {
            Random rnd = new Random();
            MyAPIGateway.Parallel.ForEach(_inHash, playerent =>
            {
                if (!(playerent is IMyCharacter)) return;
                    try
                    {   
                        var dude = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                        var relationship = _tblock.GetUserRelationToOwner(dude);
                        if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                        {
                            Logging.WriteLine(String.Format("{0} - playerEffect: Enemy {1} detected at loop {2} - relationship: {3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), playerent, Count, relationship));
                            string s = playerent.ToString();
                            if (s.Equals("Space_Wolf"))
                            {
                                Logging.WriteLine(String.Format("{0} - playerEffect: Killing {1} ", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), playerent));
                                ((IMyCharacter) playerent).Kill();
                                return;
                            }
                            if (MyAPIGateway.Session.Player.Character.Equals(playerent))
                            {
                                if (MyAPIGateway.Session.Player.Character.EnabledDamping) MyAPIGateway.Session.Player.Character.SwitchDamping();
                            }
                            if (MyVisualScriptLogicProvider.GetPlayersEnergyLevel(dude) > 0.5f)
                            {
                                MyVisualScriptLogicProvider.SetPlayersEnergyLevel(dude, 0.49f);
                            }
                            if (MyVisualScriptLogicProvider.IsPlayersJetpackEnabled(dude))
                            {
                                _playertime++;
                                int explodeRollChance = rnd.Next(0 - _playertime, _playertime);
                                if (explodeRollChance > 666)
                                {
                                    _playertime = 0;

                                    if (MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(dude) > 0.01f)
                                    {
                                        var dudepos = MyAPIGateway.Players.GetPlayerControllingEntity(playerent);
                                        MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(dude, 0.01f);
                                        MyVisualScriptLogicProvider.CreateExplosion(dudepos.GetPosition(), 0, 0);
                                        float dudehealth = MyVisualScriptLogicProvider.GetPlayersHealth(dude);
                                        MyVisualScriptLogicProvider.SetPlayersHealth(dude, dudehealth - 50f);

                                        Vector3D playerCurrentSpeed = MyVisualScriptLogicProvider.GetPlayersSpeed(dude);

                                        if (playerCurrentSpeed == new Vector3D(0, 0, 0))
                                        {

                                            playerCurrentSpeed = (Vector3D) MyUtils.GetRandomVector3Normalized();

                                        }

                                        Vector3D speedDir = Vector3D.Normalize(playerCurrentSpeed);
                                        int randomSpeed = rnd.Next(10, 20);
                                        Vector3D additionalSpeed = speedDir * (double) randomSpeed;
                                        MyVisualScriptLogicProvider.SetPlayersSpeed(playerCurrentSpeed + additionalSpeed, dude);
                                    }

                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteLine(String.Format("{0} - Exception in playerEffects", DateTime.Now));
                        Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
                    }
            });
            _playerwebbed = false;
        }
        #endregion

        #region Close flagged grids
        public void GridClose()
        {
            if (GenCount == -1)
            {
                MyAPIGateway.Parallel.ForEach(_gridCloseHash, grident =>
                {
                    var grid = grident as IMyCubeGrid;
                    if (grid == null) return;
                    var gridpos = grid.GetPosition();
                    MyVisualScriptLogicProvider.CreateExplosion(gridpos, 100, 0);
                    var vel = grid.Physics.LinearVelocity;
                    vel.SetDim(0, -2f);
                    vel.SetDim(1, 20f);
                    vel.SetDim(2, -2f);
                    grid.Physics.LinearVelocity = vel;
                });
                return;
            }
            if (GenCount != 599) return;
            MyAPIGateway.Parallel.ForEach(_gridCloseHash, grident =>
            {
                var grid = grident as IMyCubeGrid;
                grid?.Close();
            });
            _gridCloseHash.Clear();
        }
        #endregion

        #region Kill flagged players
        public void PlayerKill()
        {
            if (GenCount != 239) return;
            MyAPIGateway.Parallel.ForEach(_playerKillList, playerent =>
            {
                if (playerent != null)
                {
                    var player = MyAPIGateway.Entities.GetEntityById(playerent);
                    var playerpos = player.GetPosition();
                    MyVisualScriptLogicProvider.CreateExplosion(playerpos, 35, 5000);
                }
            });
            _playerKillList.Clear();
        }
        #endregion
    }
    #region Controls Class
    public class RefreshCheckbox<T> : Control.Checkbox<T>
    {
        public RefreshCheckbox(IMyTerminalBlock block,
            string internalName,
            string title,
            bool defaultValue = true) : base(block, internalName, title, defaultValue)
        {
        }
        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            base.Setter(block, newState);
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
            block.RefreshCustomInfo();
        }
    }

    class RangeSlider<T> : Control.Slider<T>
    {

        public RangeSlider(
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 50.0f,
            float max = 300.0f,
            float standard = 10.0f)            
            : base(block, internalName, title, min, max, standard)
        {
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            try
            {
                builder.Clear();
                var distanceString = Getter(block).ToString("0") + "m";
                builder.Append(distanceString);
                block.RefreshCustomInfo();
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in Writer", DateTime.Now));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }

        public void SetterOutside(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            //var message = new shieldNetwork.MessageSync() { Value = value, EntityId = block.EntityId };
            //shieldNetwork.MessageUtils.SendMessageToAll(message);
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
        }
    }
    #endregion

    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public static bool IsInit;
        private static List<DefenseShields> _bulletShields = new List<DefenseShields>(); // check 
        public static bool ControlsLoaded;

        // Initialisation

        protected override void UnloadData()
        {
            Logging.WriteLine("Logging stopped.");
            Logging.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            if (IsInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public static void Init()
        {
            Logging.Init("debugdevelop.log");
            Logging.WriteLine(String.Format("{0} - Logging Started", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            IsInit = true;
        }

        // Prevent damage by bullets fired from outside zone.

        public static void CheckDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // move below, modify match Type to 
            {
            }

            if (_bulletShields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            DefenseShields generator = _bulletShields[0];
            IMyEntity ent = block as IMyEntity;
            var slimBlock = block as IMySlimBlock;
            if (slimBlock != null) ent = slimBlock.CubeGrid;
            var dude = block as IMyCharacter;
            if (dude != null) ent = dude;
            if (ent == null) return;
            bool isProtected = false;
            foreach (var shield in _bulletShields)
                if (shield._inHash.Contains(ent))
                {
                    isProtected = true;
                    generator = shield;
                }
            if (!isProtected) return;
            IMyEntity attacker;
            if (!MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out attacker)) return;
            if (generator._inHash.Contains(attacker)) return;
            info.Amount = 0f;
        }
    }
    #endregion
}
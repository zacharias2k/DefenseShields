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

using DefenseShields.Base;
using DefenseShields.Destroy;
using UnityEngine;
using Color = VRageMath.Color;
using Quaternion = VRageMath.Quaternion;
using Random = System.Random;
using Vector3 = VRageMath.Vector3;

namespace DefenseShields.Station
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
        private readonly float _shotdmg = 1f;
        private readonly float _bulletdmg = 0.1f;
        private readonly float _massdmg = 0.0025f;
        private readonly float _inOutSpace = 15f;

        public int Count = -301;
        public int _playercount = 600;
        public int _gridcount = 600;
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
        private bool _pkilllock;
        private bool _gcloselock;
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
        public MyConcurrentHashSet<IMyEntity> _inCacheHash = new MyConcurrentHashSet<IMyEntity>();
        public static HashSet<IMyEntity> _destroyGridHash = new HashSet<IMyEntity>();
        public static HashSet<IMyEntity> _destroyPlayerHash = new HashSet<IMyEntity>();

        readonly MyStringId RangeGridResourceId = MyStringId.GetOrCompute("Build new");


        public static readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();

        private IMyOreDetector _oblock;
        private IMyFunctionalBlock _fblock;
        private IMyTerminalBlock _tblock;
        private MyCubeBlock _cblock;
        private IMyEntity Shield;
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

            _cblock = (MyCubeBlock)Entity;
            _oblock = Entity as IMyOreDetector;
            _fblock = Entity as IMyFunctionalBlock;
            _tblock = Entity as IMyTerminalBlock;
        }
        #endregion

        #region Interfaces
        public interface IPlayerKill { void PlayerKill(); }
        public interface IGridClose { void GridClose(); }
        public interface IEnemyDetect { void EnemyDetect(); }
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
                if (_playercount < 600) _playercount++;
                if (_gridcount < 600) _gridcount++;
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
                if (_playerkill || _playercount == 479)
                {
                    if (_playerkill) _playercount = -1;
                    _playerkill = false;
                    if (_destroyPlayerHash.Count > 0) DestroyEntity.PlayerKill(_playercount);

                }
                if (_closegrids || _gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419 || _gridcount == 479 || _gridcount == 599)
                {
                    if (_closegrids) _gridcount = -1;
                    _closegrids = false;
                    if (_destroyGridHash.Count > 0) DestroyEntity.GridClose(_gridcount);
                }
                if (!Initialized && _cblock.IsWorking)
                {
                    if (Count == 1)
                    {
                        InHashBuilder();

                    }
                    if (Count == 0) MyAPIGateway.Parallel.Do(InHashBuilder);
                    MyAPIGateway.Parallel.StartBackground(WebEntities);
                    if (_shotwebbed && !_shotlocked) MyAPIGateway.Parallel.Do(ShotEffects);
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
                        //Shield = Utils.Spawn("LargeField", "", true, false, false, false, false, _cblock.IDModule.Owner);
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
                //Logging.WriteLine(String.Format("{0} - Sustain cost is {1}MW this and recharge cost is {2}MW", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), sustaincost, _recharge));
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
            catch { }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
                //MyAPIGateway.Entities.RemoveEntity(Shield);
            }
            catch { }
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
            //var wiredraw = 1 << Math.Clamp((int) (5 * _range / Math.Sqrt(_cblock.WorldMatrix.Translation.Length(MyAPIGateway.Session.Camera.Position)), 1, 5));
            if (!Initialized && _cblock.IsWorking)
            {
                Color colour;
                var relations = _tblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
                if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    colour = Color.FromNonPremultiplied(16, 255 - _colourRand, 16 + _colourRand, 72);
                else
                    colour = Color.FromNonPremultiplied(255 - _colourRand, 80 + _colourRand, 16, 72);

                _edgeVectors = new Vector3(_depth, _height, _width);
                MatrixD edgeMatrix = MatrixD.CreateFromTransformScale(Quaternion.CreateFromRotationMatrix(_worldMatrix.GetOrientation()), _worldMatrix.Translation, _edgeVectors);
                //Shield.SetWorldMatrix(edgeMatrix);
                MySimpleObjectDraw.DrawTransparentSphere(ref edgeMatrix, 1f, ref colour, MySimpleObjectRasterizer.Solid, 20, null, RangeGridResourceId, 0.25f, -1);
                //var matrix = MatrixD.Rescale(_worldMatrix, new Vector3D(_width, _height, _depth));
                //MySimpleObjectDraw.DrawTransparentSphere(ref matrix, 1f, ref colour, MySimpleObjectRasterizer.Solid, 24, MyStringId.GetOrCompute("Square"));
            }
        }
        #endregion

        #region Detect Intersection
        private bool Detectedge(IMyEntity ent, float f)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / ((_width - f) * (_width - f)) + (y * y) / ((_depth - f) * (_depth - f)) + (z * z) / ((_height - f) * (_height - f));
            if (detect <= 1)
            {
                //Logging.WriteLine(String.Format("{0} - {1} edge-t - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
                return true;
            }
            //if (detect <= 1.1) Logging.WriteLine(String.Format("{0} - {1} edge-f - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
            return false;
        }
        #endregion

        #region Build inside HashSet and Cache
        public void InHashBuilder() // Runs in Count 0
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD insphere = new BoundingSphereD(pos, _range - _inOutSpace);
            List<IMyEntity> inList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref insphere);

            _inHash.Clear();
            MyAPIGateway.Parallel.ForEach(inList, inent =>
            {
                if (inent is IMyCubeGrid || inent is IMyCharacter && Detectedge(inent, _inOutSpace))
                {
                    _inHash.Add(inent);
                }
            });
        }

        public void InCacheBuilder() // Runs early during Count 1, after InHashBuilder()
        {
            _inCacheHash.Clear();
            MyAPIGateway.Parallel.ForEach(_inHash, ent =>
            {
                _inCacheHash.Add(ent);
            });
            Logging.WriteLine(String.Format("{0} - inHash {1} _inCacheHash {2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), _inHash.Count, _inCacheHash.Count, Count));
        }
        #endregion
        /*
        public void RepelGrid()
        {
            foreach (var ent in _inHash)
            {
                var grid = ent as IMyCubeGrid;
                if (grid == null) continue;
                var direction = Vector3D.Normalize(_tblock.Physics.Center - grid.PositionComp.GetPosition());
                Wrapper.BeginGameAction(() => grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -direction * grid.Physics.LinearVelocity.Length() * grid.Physics.Mass * 10, grid.Physics.CenterOfMassWorld, null));
                var d = grid.WorldAABB.Center - thingRepellingYou;
                var v = d * repulsionVelocity / d.Length();
                grid.Physics.AddForce((v - grid.Physics.LinearVelocity) * grid.Physics.Mass / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
            }
        }
        */
        #region Web and dispatch all intersecting entities
        public void WebEntities()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);

            BoundingSphereD websphere = new BoundingSphereD(pos, _range);
            List<IMyEntity> webList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref websphere);
            MyAPIGateway.Parallel.ForEach(webList, webent =>
            {
                if (webent == null || webent is IMyVoxelBase || webent is IMyFloatingObject || webent is IMyEngineerToolBase) return;
                if (webent is IMyMeteor && !_shotwebbed) _shotwebbed = true;
                if (webent is IMyMeteor) return;

                if (webent is IMyCharacter && (Count == 2 || Count == 17 || Count == 32 || Count == 47) && Detectedge(webent, 0f))
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

                if (webent is IMyCharacter || _inCacheHash.Contains(webent)) return;

                var grid = webent as IMyCubeGrid;
                if (grid == _tblock.CubeGrid || _gridwebbed || _destroyGridHash.Contains(grid) || grid == null) return;

                List<long> owners = grid.BigOwners;
                if (owners.Count > 0)
                {
                    var relations = _tblock.GetUserRelationToOwner(owners[0]);
                    //Logging.WriteLine(String.Format("{0} - grid: {1} tblock: {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, owners.Count, relations, relations == MyRelationsBetweenPlayerAndBlock.Owner, relations == MyRelationsBetweenPlayerAndBlock.FactionShare));
                    if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                }
                if (Detectedge(grid, 0f))
                {
                    float griddmg = grid.Physics.Mass * _massdmg;
                    _absorb += griddmg;
                    Logging.WriteLine(String.Format("{0} - gridEffect: {1} Shield Strike by a {2}kilo grid, absorbing {3}MW of energy in loop {4}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid, (griddmg / _massdmg), griddmg, Count));

                    _closegrids = true;
                    _destroyGridHash.Add(grid);
                    //var dist = Vector3D.Distance(ent.GetPosition(), sphere.Center);
                    //var test = Vector4.
                    var vel = grid.Physics.LinearVelocity;
                    vel.SetDim(0, (int)(vel.GetDim(0) * -8.0f));
                    vel.SetDim(1, (int)(vel.GetDim(1) * -8.0f));
                    vel.SetDim(2, (int)(vel.GetDim(2) * -8.0f));
                    grid.Physics.LinearVelocity = vel;

                    //var playerentid = MyVisualScriptLogicProvider.GetPlayersEntityId(playerid);
                    //var player = MyAPIGateway.Entities.GetEntityById(playerentid);
                    //var playerent = (IMyCharacter)player;
                    //long? dude = MyAPIGateway.Players.GetPlayerControllingEntity(grid)?.IdentityId;
                    var playerchar = MyAPIGateway.Players.GetPlayerControllingEntity(grid).Character;
                    if (playerchar != null)
                    {
                        //_destroyPlayerHash.Add(playerchar);
                        //_playerkill = true;
                    }
                    return;
                }
                if (_shotwebbed) return;
                if (webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    if (Detectedge(webent, 0f))
                    {
                        _shotwebbed = true;
                    }
                }
                //Logging.WriteLine(String.Format("{0} - webEffect unmatched: {1} {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), webent.GetFriendlyName(), webent.DisplayName, webent.Name));
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
                if (shotent == null || !Detectedge(shotent, 0f)) return;
                try
                {
                    _absorb += _shotdmg;
                    Logging.WriteLine(String.Format("{0} - shotEffect: Shield absorbed {1}MW of energy from {2} in loop {3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), _shotdmg, shotent, Count));
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

        #region player effects
        public void PlayerEffects()
        {
            var rnd = new Random();
            //MyAPIGateway.Parallel.ForEach(_inCacheHash, playerent =>
            foreach (var playerent in _inCacheHash)
            {
                if (!(playerent is IMyCharacter)) return;
                try
                {
                    var playerid = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                    var relationship = _tblock.GetUserRelationToOwner(playerid);
                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        var character = playerent as IMyCharacter;
                        var npcname = character.ToString();
                        Logging.WriteLine(String.Format("{0} - playerEffect: Enemy {1} detected at loop {2} - relationship: {3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), character, Count, relationship));
                        if (npcname.Equals("Space_Wolf"))
                        {
                            Logging.WriteLine(String.Format("{0} - playerEffect: Killing {1} ", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), character));
                            character.Kill();
                            return;
                        }
                        if (character.EnabledDamping) character.SwitchDamping();
                        if (character.SuitEnergyLevel > 0.5f)
                            MyVisualScriptLogicProvider.SetPlayersEnergyLevel(playerid, 0.49f);
                        if (MyVisualScriptLogicProvider.IsPlayersJetpackEnabled(playerid))
                        {
                            _playertime++;
                            var explodeRollChance = rnd.Next(0 - _playertime, _playertime);
                            if (explodeRollChance > 666)
                            {
                                _playertime = 0;
                                if (MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(playerid) > 0.01f)
                                {
                                    var characterpos = character.GetPosition();
                                    MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(playerid, 0.01f);
                                    MyVisualScriptLogicProvider.CreateExplosion(characterpos, 0, 0);
                                    var characterhealth = MyVisualScriptLogicProvider.GetPlayersHealth(playerid);
                                    MyVisualScriptLogicProvider.SetPlayersHealth(playerid, characterhealth - 50f);
                                    var playerCurrentSpeed = MyVisualScriptLogicProvider.GetPlayersSpeed(playerid);
                                    if (playerCurrentSpeed == new Vector3D(0, 0, 0))
                                    {
                                        playerCurrentSpeed = MyUtils.GetRandomVector3Normalized();
                                    }
                                    var speedDir = Vector3D.Normalize(playerCurrentSpeed);
                                    var randomSpeed = rnd.Next(10, 20);
                                    var additionalSpeed = speedDir * randomSpeed;
                                    MyVisualScriptLogicProvider.SetPlayersSpeed(playerCurrentSpeed + additionalSpeed, playerid);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteLine(String.Format("{0} - Exception in playerEffects",
                        DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                    Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
                }
                //});
            }
            _playerwebbed = false;
        }
        #endregion
    }
    #region Cube+subparts Class
    public class Utils
    {
        //SPAWN METHOD
        public static IMyEntity Spawn(string subtypeId, string name = "", bool isVisible = true, bool hasPhysics = false, bool isStatic = false, bool toSave = false, bool destructible = false, long ownerId = 0)
        {
            try
            {
                CubeGridBuilder.Name = name;
                CubeGridBuilder.CubeBlocks[0].SubtypeName = subtypeId;
                CubeGridBuilder.CreatePhysics = hasPhysics;
                CubeGridBuilder.IsStatic = isStatic;
                CubeGridBuilder.DestructibleBlocks = destructible;
                IMyEntity ent = MyAPIGateway.Entities.CreateFromObjectBuilder(CubeGridBuilder);

                ent.Flags &= ~EntityFlags.Save;
                ent.Visible = isVisible;
                MyAPIGateway.Entities.AddEntity(ent, true);

                return ent;
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in Spawn", DateTime.Now));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now, ex));
                return null;
            }
        }

        private static readonly SerializableBlockOrientation EntityOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up);

        //OBJECTBUILDERS
        private static readonly MyObjectBuilder_CubeGrid CubeGridBuilder = new MyObjectBuilder_CubeGrid()
        {

            EntityId = 0,
            GridSizeEnum = MyCubeSize.Large,
            IsStatic = true,
            Skeleton = new List<BoneInfo>(),
            LinearVelocity = Vector3.Zero,
            AngularVelocity = Vector3.Zero,
            ConveyorLines = new List<MyObjectBuilder_ConveyorLine>(),
            BlockGroups = new List<MyObjectBuilder_BlockGroup>(),
            Handbrake = false,
            XMirroxPlane = null,
            YMirroxPlane = null,
            ZMirroxPlane = null,
            PersistentFlags = MyPersistentEntityFlags2.InScene,
            Name = "ArtificialCubeGrid",
            DisplayName = "FieldGenerator",
            CreatePhysics = false,
            DestructibleBlocks = true,
            PositionAndOrientation = new MyPositionAndOrientation(Vector3D.Zero, Vector3D.Forward, Vector3D.Up),

            CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
                {
                    new MyObjectBuilder_CubeBlock()
                    {
                        EntityId = 0,
                        BlockOrientation = EntityOrientation,
                        SubtypeName = "",
                        Name = "Field",
                        Min = Vector3I.Zero,
                        Owner = 0,
                        ShareMode = MyOwnershipShareModeEnum.None,
                        DeformationRatio = 0,
                    }
                }
        };
    }
    #endregion
}
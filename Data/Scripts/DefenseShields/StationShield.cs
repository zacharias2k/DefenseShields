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
using VRage;
using System.Linq;
using Sandbox.Game.Entities;
using TExtensions = Sandbox.ModAPI.Interfaces.TerminalPropertyExtensions;

namespace DefenseShields.Station
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, new string[] { "StationDefenseShield" })]
    class DefenseShields : MyGameLogicComponent
    {
        #region Setup
        public bool Initialized = true;
        private bool _anim_init = false;
        private float anim_step = 0f;
        private float _range =50f;
        private float _width = 50f;
        private float _height = 50f;
        private float _depth = 50f; 
        private int _time;
        public int _count;
        public int _oddeven;
        private int _playertime;
        private bool _playerwebbed = false;
        private bool _gridwebbed = false;
        private bool _shotwebbed = false;
        private bool _insideReady = false;
        private ushort _modID = 50099;

        private MatrixD WorldMatrix;
        private Vector3D Scale;
        private BoundingSphereD sphere_min;
        private BoundingSphereD sphere_max;
        private MyEntitySubpart subpart_Rotor;
        public RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector> Slider;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> Ellipsoid;
        public MyResourceSinkComponent Sink;
        public MyDefinitionId PowerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly List<MyEntitySubpart> subparts_Arms = new List<MyEntitySubpart>();
        private readonly List<MyEntitySubpart> subparts_Reflectors = new List<MyEntitySubpart>();
        private List<Matrix> matrix_Arms_Off = new List<Matrix>();
        private List<Matrix> matrix_Arms_On = new List<Matrix>();
        private List<Matrix> matrix_Reflectors_Off = new List<Matrix>();
        private List<Matrix> matrix_Reflectors_On = new List<Matrix>();

        public HashSet<IMyEntity> _inHash = new HashSet<IMyEntity>();

        public static readonly Dictionary<long, DefenseShields> Shields = new Dictionary<long, DefenseShields>();

        protected IMyOreDetector _ublock; //change to _oblock
        private IMyFunctionalBlock _fblock;
        private IMyTerminalBlock _tblock;
        private IMyCubeBlock _cblock;
        private MyCubeBlock _animblock;
        private IMyEntity AnimShield;
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
            _ublock = Entity as IMyOreDetector; 
            _fblock = Entity as IMyFunctionalBlock;
            _tblock = Entity as IMyTerminalBlock;
            _animblock = Entity as MyCubeBlock;
        }
        #endregion

        #region Simulation
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (_anim_init)
                {
                    WorldMatrix = Entity.WorldMatrix;
                    WorldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;
                    //Animations
                    if (_fblock.Enabled && _fblock.IsFunctional && _cblock.IsWorking)
                    {
                        //Color change for on =-=-=-=-
                        subpart_Rotor.SetEmissiveParts("Emissive", Color.White, 1);
                        _time += 1;
                        Matrix temp1 = Matrix.CreateRotationY(0.1f * _time);
                        temp1.Translation = subpart_Rotor.PositionComp.LocalMatrix.Translation;
                        subpart_Rotor.PositionComp.LocalMatrix = temp1;
                        if (anim_step < 1f)
                        {
                            anim_step += 0.05f;
                        }
                    }
                    else
                    {
                        //Color change for off =-=-=-=-
                        subpart_Rotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
                        if (anim_step > 0f)
                        {
                            anim_step -= 0.05f;
                        }
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        if (i < 4)
                        {
                            subparts_Reflectors[i].PositionComp.LocalMatrix = Matrix.Slerp(matrix_Reflectors_Off[i], matrix_Reflectors_On[i], anim_step);
                        }
                        subparts_Arms[i].PositionComp.LocalMatrix = Matrix.Slerp(matrix_Arms_Off[i], matrix_Arms_On[i], anim_step);
                    }
                    MatrixD matrix = MatrixD.CreateFromTransformScale(Quaternion.CreateFromRotationMatrix(WorldMatrix.GetOrientation()), WorldMatrix.Translation, Scale);
                    AnimShield.SetWorldMatrix(matrix);
                }
                if (!MyAPIGateway.Utilities.IsDedicated) showRange(_range); //Check
                else sendPoke(_range); //Check
                MyAPIGateway.Parallel.StartBackground(webEffects);
                if (_shotwebbed) MyAPIGateway.Parallel.Do(shotEffects);
                if (_gridwebbed) MyAPIGateway.Parallel.Do(gridEffects);
                if (_playerwebbed) MyAPIGateway.Parallel.Do(playerEffects);
                if (_count++ == 59 || _count == 159) _count = 0;
            }
            catch (Exception ex)
            {
                Logging.writeLine(String.Format("{0} - Exception in UpdateBeforeSimulation", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (Initialized)
            {
                //this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
                Logging.writeLine(String.Format("{0} - Create UI {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), _count));
                if (!DefenseShieldsBase.ControlsLoaded)
                {
                    CreateUI();
                }
                ((IMyFunctionalBlock)_cblock).AppendingCustomInfo += AppendingCustomInfo;
                //_tblock.RefreshCustomInfo(); //Check
                Initialized = false;

            }
            _tblock.RefreshCustomInfo(); //Check
            //ShieldList.Add(this);
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (!_anim_init)
                {
                    if (_ublock.BlockDefinition.SubtypeId == "StationDefenseShield")
                    {
                        if (!_ublock.IsFunctional) return;
                        AnimShield = Utils.Spawn("LargeField", "", true, false, false, false, false, _animblock.IDModule.Owner); // removed IDmodule.Owner validate still wroks
                        BlockAnimation();

                        _anim_init = true;
                    }
                    else
                    {
                        this.NeedsUpdate = MyEntityUpdateEnum.NONE;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.writeLine(String.Format("{0} - Exception in UpdateAfterSimulation", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }
        #endregion

        #region Block Animation
        public void BlockAnimation()
        {
            try
            {
                anim_step = 0f;

                matrix_Arms_Off = new List<Matrix>();
                matrix_Arms_On = new List<Matrix>();
                matrix_Reflectors_Off = new List<Matrix>();
                matrix_Reflectors_On = new List<Matrix>();

                WorldMatrix = Entity.WorldMatrix;
                WorldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;

                Entity.TryGetSubpart("Rotor", out subpart_Rotor);

                for (int i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    subpart_Rotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    matrix_Arms_Off.Add(temp1.PositionComp.LocalMatrix);
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
                    matrix_Arms_On.Add(temp2);
                    subparts_Arms.Add(temp1);
                }

                for (int i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    subparts_Arms[i].TryGetSubpart("Reflector", out temp3);
                    subparts_Reflectors.Add(temp3);
                    matrix_Reflectors_Off.Add(temp3.PositionComp.LocalMatrix);
                    Matrix temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    matrix_Reflectors_On.Add(temp4);
                }
                //Scale = new Vector3(_depth / 150f, _height / 150f, _width / 150f); //Might not need
                TExtensions.SetValueBool(_tblock, "ShowOnHUD", true);
                AnimShield.Render.Visible = _fblock.ShowOnHUD;

                //_anim_init = true;
            }
            catch (Exception ex)
            {
                Logging.writeLine(String.Format("{0} - Exception in BlockAnimation", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
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

            float power = 0.0001f;
            if (!Initialized && _cblock.IsWorking)
            {
                var radius = Slider.Getter((IMyFunctionalBlock)_cblock);
                power = (float)(4.0 * Math.PI * Math.Pow(radius, 3) / 3.0 / 1000.0 / 1000.0);
            }
            return power;
        }

        void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            stringBuilder.Clear();
            stringBuilder.Append("Required Power: " + shield.CalcRequiredPower().ToString("0.00") + "MW");

            AnimShield.Render.Visible = _fblock.ShowOnHUD; //Might not need
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
            Scale = new Vector3(_depth / 150f, _height / 150f, _width / 150f); 
        }
        #endregion

        #region Cleanup
        public override void Close()
        {
            try
            {
                //DefenseShields.ShieldList.Remove(this);
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
                //DefenseShields.ShieldList.Remove(this);
            }
            catch
            {
            }
            base.MarkForClose();
        }
        #endregion

        #region Create UI
        void RemoveOreUI()
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

        public void CreateUI()
        {
            DefenseShieldsBase.ControlsLoaded = true;
            RemoveOreUI();

            
            Ellipsoid = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)_cblock,
                "Ellipsoid",
                "Switch to Ellipsoid",
                true);
            
            Slider = new RangeSlider<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyFunctionalBlock)_cblock,
                "RadiusSlider",
                "Shield Size",
                50,
                300,
                50);
        }
        #endregion

        #region Server-client comms
        [ProtoContract(UseProtoMembersOnly = true)]
        public class Poke
        {
            [ProtoMember(1)] public ushort ModID;
            [ProtoMember(2)]
            public float Size { get; set; }
        }

        public void sendPoke(float size)
        {
            bool sent;
            Poke info = new Poke();
            info.ModID = _modID;
            info.Size = size;
            sent = MyAPIGateway.Multiplayer.SendMessageToOthers(_modID, MyAPIGateway.Utilities.SerializeToBinary(info), true);
        }

        public void getPoke(byte[] data)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<Poke>(data);
            Poke info = new Poke();
            try
            {
                info = message;
                if (info.ModID == _modID)
                {
                    showRange(info.Size);
                }
            }
            catch (Exception ex)
            {
                Logging.writeLine(String.Format("{0} - Exception in getPoke", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }

        public void showRange(float size)
        {
            Color colour;
            var relations = _tblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.IdentityId);
            if (relations == MyRelationsBetweenPlayerAndBlock.Owner || relations == MyRelationsBetweenPlayerAndBlock.FactionShare)
               colour = Color.FromNonPremultiplied(0, 60, 0, 64);
            else
                colour = Color.FromNonPremultiplied(1, 0, 0, 52);
            MyStringId RangeGridResourceId = MyStringId.GetOrCompute("Build new");
            var matrix = _tblock.WorldMatrix;
            //MySimpleObjectDraw.DrawTransparentSphere(ref matrix, size, ref colour, MySimpleObjectRasterizer.Solid, 20, null, RangeGridResourceId, 0.25f, -1);
        }

        #endregion

        #region Detect intersection
        private bool Detect(ref IMyEntity ent)
        {
            float x = Vector3Extensions.Project(WorldMatrix.Forward, ent.GetPosition() - WorldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(WorldMatrix.Left, ent.GetPosition() - WorldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(WorldMatrix.Up, ent.GetPosition() - WorldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_width * _width) + (y * y) / (_depth * _depth) + (z * z) / (_height * _height);
            if (detect > 1) return false;
            return true;
        }
        #endregion

        #region Webing effects

        public void webEffects()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            if (_count == 0)
            {
                _insideReady = false;
                _inHash.Clear();
                BoundingSphereD insphere = new BoundingSphereD(pos, _range - 13.3f);
                MyAPIGateway.Entities.GetEntities(_inHash, ent => insphere.Intersects(ent.WorldAABB) && Detect(ref ent) && !(ent is IMyVoxelBase) && !(ent is IMyCubeBlock)
                && !(ent is IMyFloatingObject) && !(ent is MyHandToolBase) && ent != AnimShield && !ent.Transparent && !(ent is IMyCharacter)
                && !(ent is IMyWelder) && !(ent is IMyHandDrill) && !(ent is IMyAngleGrinder) && !(ent is IMyAutomaticRifleGun) && !(ent is IMyInventoryBag));
                MyAPIGateway.Parallel.ForEach(_inHash, outent =>
                {
                    var grid = outent as IMyCubeGrid;
                    if (grid != null)
                    {
                        if (!_inHash.Contains(outent)) _inHash.Add(outent);
                    }
                });
                _insideReady = true;

            }
            HashSet<IMyEntity> webHash = new HashSet<IMyEntity>();
            BoundingSphereD websphere = new BoundingSphereD(pos, _range);
            MyAPIGateway.Entities.GetEntities(webHash, ent => websphere.Intersects(ent.WorldAABB) && Detect(ref ent) && !_inHash.Contains(ent) && !(ent is IMyVoxelBase) && !(ent is IMyCubeBlock)
            && !(ent is IMyFloatingObject) && !(ent is MyHandToolBase) && ent != _tblock.CubeGrid && ent != AnimShield && !ent.Transparent && !(ent is IMyWelder)
            && !(ent is IMyHandDrill) && !(ent is IMyAngleGrinder) && !(ent is IMyAutomaticRifleGun) && !(Entity is IMyInventoryBag));

            MyAPIGateway.Parallel.ForEach(webHash, webent =>
            {
                if (webent == null) return;

                if (webent is IMyCharacter && _count == 14 || _count == 29 || _count == 44 || _count == 59)
                {
                    var dude = MyAPIGateway.Players.GetPlayerControllingEntity(webent).IdentityId;
                    var relationship = _tblock.GetUserRelationToOwner(dude);
                    if (relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        _playerwebbed = true;
                        return;
                    }
                    return;
                }
                var grid = webent as IMyCubeGrid;
                if (grid != null)
                {
                    if (_gridwebbed) return;
                    List<long> owners = grid.BigOwners;
                    if (owners.Count > 0)
                    {
                        var relations = _tblock.GetUserRelationToOwner(0);
                        if (relations == MyRelationsBetweenPlayerAndBlock.Owner ||
                            relations == MyRelationsBetweenPlayerAndBlock.FactionShare)
                            return;
                    }
                    _gridwebbed = true; 
                    return;
                }
                if (_shotwebbed) return;
                if (webent is IMyMeteor || webent.ToString().Contains("Missile") || webent.ToString().Contains("Torpedo"))
                {
                    _shotwebbed = true;
                }
                Logging.writeLine(String.Format("{0} - webEffect unmatched: {1} {2} {3} {4} {5}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), webent.GetFriendlyName(), webent.DisplayName, webent.Name));
            });
        }

        #endregion

        #region shot effects

        public void shotEffects()
        {
            HashSet<IMyEntity> shotHash = new HashSet<IMyEntity>();
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD shotsphere = new BoundingSphereD(pos, _range);
            MyAPIGateway.Entities.GetEntities(shotHash, ent => shotsphere.Intersects(ent.WorldAABB) && ent is IMyMeteor && Detect(ref ent)  || ent.ToString().Contains("Missile") || ent.ToString().Contains("Torpedo"));

            MyAPIGateway.Parallel.ForEach(shotHash, shotent =>
            {
                if (shotent == null) return;
                try
                {
                    Logging.writeLine(String.Format("{0} - shotEffect ent found: {1} in loop {2}",
                        DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), shotent, _count));
                    if (shotent is MyMeteor || shotent.ToString().Contains("Missile") ||
                        shotent.ToString().Contains("Torpedo"))
                    {
                        shotent.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logging.writeLine(String.Format("{0} - Exception in shotEffects", DateTime.Now));
                    Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
                }
            });
            _shotwebbed = false;
        }

        #endregion

        #region player effects

        public void playerEffects()
        {
            HashSet<IMyEntity> playerHash = new HashSet<IMyEntity>();
            Random rnd = new Random();
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD playersphere = new BoundingSphereD(pos, _range);
            MyAPIGateway.Entities.GetEntities(playerHash, ent => playersphere.Intersects(ent.WorldAABB) && ent is IMyCharacter && Detect(ref ent));
            MyAPIGateway.Parallel.ForEach(playerHash, playerent =>
            {
                if (playerent == null) return;
                if (playerent is IMyCharacter)
                    try
                    {   
                        var dude = MyAPIGateway.Players.GetPlayerControllingEntity(playerent).IdentityId;
                        var relationship = _tblock.GetUserRelationToOwner(dude);
                        if (relationship != MyRelationsBetweenPlayerAndBlock.Owner &&
                            relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
                        {
                            Logging.writeLine(String.Format(
                                "{0} - playerEffect: Enemy {1} detected at loop {2} - relationship: {3}",
                                DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), playerent, _count, relationship));
                            string s = playerent.ToString();
                            if (s.Equals("Space_Wolf"))
                            {
                                Logging.writeLine(String.Format("{0} - playerEffect: Killing {1} ",
                                    DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), playerent));
                                ((IMyCharacter) playerent).Kill();
                                return;
                            }
                            if (MyAPIGateway.Session.Player.Character.Equals(playerent))
                            {
                                if (MyAPIGateway.Session.Player.Character.EnabledDamping)
                                    MyAPIGateway.Session.Player.Character.SwitchDamping();
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
                                        MyVisualScriptLogicProvider.SetPlayersSpeed(
                                            playerCurrentSpeed + additionalSpeed, dude);
                                    }

                                }
                            }
                        }
                        else if (!_inHash.Contains(playerent)) _inHash.Add(playerent);
                    }
                    catch (Exception ex)
                    {
                        Logging.writeLine(String.Format("{0} - Exception in playerEffects", DateTime.Now));
                        Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
                    }
            });
            _playerwebbed = false;
        }
        #endregion

        #region Grid effects
        public void gridEffects()
        {
            var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
            BoundingSphereD gridsphere = new BoundingSphereD(pos, _range);
            List<IMyEntity> gridList = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref gridsphere);
            Logging.writeLine(String.Format("{0} - gridEffect: loop is v3 {1}", DateTime.Now, _count));
            MyAPIGateway.Parallel.ForEach(gridList, ent =>
            {
                if (ent == null || _inHash.Contains(ent) || ent.Transparent) return;
                var grid = ent as IMyCubeGrid;
                if (grid != null && _insideReady && Detect(ref ent))
                {
                    try
                    {
                        if (_inHash.Count == 0)
                            Logging.writeLine(string.Format("!!!!!Alert!!!!! {0} - gridEffect: _inList empty in loop {1}", DateTime.Now, _count));

                        Logging.writeLine(string.Format("{0} - passing grid - Name: {1}", DateTime.Now, ent.DisplayName));
                        if (grid == _tblock.CubeGrid || _inHash.Contains(grid) || grid.DisplayName == "FieldGenerator") return;
                        Logging.writeLine(string.Format("{0} - passing grid - CustomName: {1}", DateTime.Now, grid.CustomName));
                        List<long> owners = grid.BigOwners;
                        if (owners.Count > 0)
                        {
                            var relations = _tblock.GetUserRelationToOwner(owners[0]);
                            if (relations == MyRelationsBetweenPlayerAndBlock.Owner ||
                                relations == MyRelationsBetweenPlayerAndBlock.FactionShare) return;
                        }
                        var dude = MyAPIGateway.Players.GetPlayerControllingEntity(grid).IdentityId;
                        var gridpos = grid.GetPosition();
                        MyVisualScriptLogicProvider.CreateExplosion(gridpos, 0, 0);
                        MyVisualScriptLogicProvider.SetPlayersHealth(dude, -100);
                        grid.Delete();
                    }
                    catch (Exception ex)
                    {
                        Logging.writeLine(string.Format("{0} - Exception in gridEffects", DateTime.Now));
                        Logging.writeLine(string.Format("{0} - {1}", DateTime.Now, ex));
                    }
                }

            });
            _gridwebbed = false;
        }
    }
    #endregion

    #region Controls Class
    public class RefreshCheckbox<T> : Control.Checkbox<T>
    {
        public RefreshCheckbox(IMyTerminalBlock block,
            string internalName,
            string title,
            bool defaultValue = true) : base(block, internalName, title, defaultValue)
        {
            //block.RefreshCustomInfo();
        }
        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            base.Setter(block, newState);

            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            block.RefreshCustomInfo();
            shield.Sink.Update();
            block.RefreshCustomInfo();
            //block.RefreshCustomInfo();
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
            //block.RefreshCustomInfo();
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            try
            {
                builder.Clear();
                var distanceString = Getter(block).ToString("0") + "m";
                builder.Append(distanceString);
                //block.RefreshCustomInfo();
            }
            catch (Exception ex)
            {
                Logging.writeLine(String.Format("{0} - Exception in Writer", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
            }
        }

        public void SetterOutside(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            block.RefreshCustomInfo();
            shield.Sink.Update();
            block.RefreshCustomInfo();
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            //var message = new shieldNetwork.MessageSync() { Value = value, EntityId = block.EntityId };
            //shieldNetwork.MessageUtils.SendMessageToAll(message);
            var shield = block.GameLogic.GetAs<DefenseShields>();
            if (shield == null) { return; }
            block.RefreshCustomInfo();
            shield.Sink.Update();
            block.RefreshCustomInfo();
        }
    }
    #endregion

    #region Cube+subparts Class
    public class Utils
    {
        //SPAWN METHOD
        public static IMyEntity Spawn(string subtypeId, string name = "", bool isVisible = true, bool hasPhysics = false, bool isStatic = false, bool toSave = false, bool destructible = false, long OwnerId = 0)
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
                Logging.writeLine(String.Format("{0} - Exception in Spawn", DateTime.Now));
                Logging.writeLine(String.Format("{0} - {1}", DateTime.Now, ex));
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

    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public static bool _isInit;
        private static List<DefenseShields> BulletShields = new List<DefenseShields>(); // check 
        public static bool ControlsLoaded;

        // Initialisation

        protected override void UnloadData()
        {
            Logging.writeLine("Logging stopped.");
            Logging.close();
        }

        public override void UpdateBeforeSimulation()
        {
            if (_isInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public static void Init()
        {
            Logging.init("debugdevelop.log");
            Logging.writeLine(String.Format("{0} - Logging Started", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, checkDamage);
            _isInit = true;
        }

        // Prevent damage by bullets fired from outside zone.

        public static void checkDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // move below, modify match Type to 
            {
            }

            if (BulletShields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            DefenseShields generator = BulletShields[0];
            IMyEntity ent = block as IMyEntity;
            var slimBlock = block as IMySlimBlock;
            if (slimBlock != null) ent = slimBlock.CubeGrid as IMyEntity;
            var dude = block as IMyCharacter;
            if (dude != null) ent = dude as IMyEntity;
            if (ent == null) return;
            bool isProtected = false;
            foreach (var shield in BulletShields)
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
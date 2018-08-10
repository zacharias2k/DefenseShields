using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeEnhancer", "SmallEnhancer")]
    public class Enhancers : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;
        internal int RotationTime;

        private float _power = 0.01f;

        private readonly Dictionary<long, Enhancers> _enhancers = new Dictionary<long, Enhancers>();
        public IMyUpgradeModule Enhancer => (IMyUpgradeModule)Entity;
        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        //internal EnhancerSettings EnhSet;
        internal EnhancerState EnhState;

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        private MyEntitySubpart _subpartRotor;

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (Enhancer.CubeGrid.Physics == null) return;
                _tick = Session.Instance.Tick;
                var isServer = Session.IsServer;

                if (!EnhancerReady(isServer)) return;

                Timing();

                if (!Session.DedicatedServer && UtilsStatic.DistanceCheck(Enhancer, 1000, 1))
                {
                    var blockCam = Enhancer.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && Enhancer.IsWorking) BlockMoveAnimation();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private bool EnhancerReady(bool server)
        {
            if (server)
            {
                if (ShieldComp?.DefenseShields == null || ShieldComp.Enhancer != null && ShieldComp.Enhancer != this ||Sink.CurrentInputByType(GId) < 0.01f ||
                    Enhancer?.CubeGrid == null || !Enhancer.Enabled || !Enhancer.IsFunctional)
                {
                    if (Enhancer != null && _tick % 300 == 0)
                    {
                        Enhancer.RefreshCustomInfo();
                        Enhancer.ShowInToolbarConfig = false;
                        Enhancer.ShowInToolbarConfig = true;
                    }

                    if (EnhState.State.Online)
                    {
                        EnhState.State.Online = false;
                        NeedUpdate(true);
                    }

                    return false;
                }
                if (ShieldComp.Enhancer == null) ShieldComp.Enhancer = this;
                EnhState.State.Online = true;
            }
            else if (!EnhState.State.Online || !Enhancer.IsFunctional) return false;

            return _subpartRotor != null || BlockMoveAnimationReset();
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Enhancer.RefreshCustomInfo();
                Enhancer.ShowInToolbarConfig = false;
                Enhancer.ShowInToolbarConfig = true;
            }
        }

        private void NeedUpdate(bool force = false)
        {
            if (!force) EnhState.State.Online = true;
            EnhState.NetworkUpdate();
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                _enhancers.Add(Entity.EntityId, this);
                Session.Instance.Enhancers.Add(this);
                StorageSetup();
                PowerInit();
                Enhancer.CubeGrid.Components.TryGet(out ShieldComp);
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                Enhancer.AppendingCustomInfo += AppendingCustomInfo;
                Enhancer.RefreshCustomInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = Enhancer.Storage;
            //if (EnhSet == null) EnhSet = new EnhancerSettings(Enhancer);
            if (EnhState == null) EnhState = new EnhancerState(Enhancer);
            EnhState.LoadState();
            
            //EnhSet.LoadSettings();
            //UpdateSettings(EnhSet.Settings);
            //EnhState.LoadSettings();
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
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Enhancer.Enabled;
                if (enableState)
                {
                    Enhancer.Enabled = false;
                    Enhancer.Enabled = true;
                }
                Sink.Update();
                if (Session.Enforced.Debug == 1) Log.Line($"PowerInit: EnhancerId [{Enhancer.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool BlockMoveAnimationReset()
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            _subpartRotor.Subparts.Clear();
            return true;
        }

        private void BlockMoveAnimation()
        {
            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.05f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        #region Create UI
        private void CreateUi()
        {
            //EnhUi.CreateUi(Enhancer);
        }
        #endregion

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (ShieldComp?.DefenseShields == null)
            {
                stringBuilder.Append("[Controller Link]: False");
            }
            else if (!EnhState.State.Backup && ShieldComp.DefenseShields.ShieldMode == DefenseShields.ShieldType.Station)
            {
                stringBuilder.Append("[Online]: " + EnhState.State.Online +
                                     "\n" +
                                     "\n[Amplifying Shield]: " + EnhState.State.Online +
                                     "\n[Enhancer Mode]: Fortress" +
                                     "\n[Bonsus] MaxHP, Repel Grids");
            }
            else if (!EnhState.State.Backup)
            {
                stringBuilder.Append("[Online]: " + EnhState.State.Online +
                                     "\n" +
                                     "\n[Amplifying Shield]: " + EnhState.State.Online +
                                     "\n[Enhancer Mode]: " + _power.ToString("0") + "%");
            }
            else
            {
                stringBuilder.Append("[Backup]: " + EnhState.State.Backup);
            }
        }

        public void UpdateState(ProtoEnhancerState newState)
        {
            EnhState.State = newState;

            if (Session.Enforced.Debug == 1) Log.Line($"UpdateState: EnhancerId [{Enhancer.EntityId}]");
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Instance.Enhancers.Contains(this)) Session.Instance.Enhancers.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (Session.Instance.Enhancers.Contains(this)) Session.Instance.Enhancers.Remove(this);
                if (ShieldComp?.Enhancer == this)
                {
                    ShieldComp.Enhancer = null;
                }
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

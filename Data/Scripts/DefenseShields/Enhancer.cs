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
        internal bool ContainerInited;
        private float _power = 0.01f;

        private readonly Dictionary<long, Enhancers> _enhancers = new Dictionary<long, Enhancers>();
        public IMyUpgradeModule Enhancer => (IMyUpgradeModule)Entity;
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

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (_count == 29 && !Session.DedicatedServer && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Enhancer.RefreshCustomInfo();
                Enhancer.ShowInToolbarConfig = false;
                Enhancer.ShowInToolbarConfig = true;
            }
        }

        private bool EnhancerReady(bool server)
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }

            if (server)
            {
                if (!BlockWorking()) return false;
            }
            else
            {
                if (ShieldComp?.DefenseShields == null)
                {
                    Enhancer.CubeGrid.Components.TryGet(out ShieldComp);
                    if (ShieldComp?.DefenseShields == null) return false;
                }
                if (!EnhState.State.Backup && ShieldComp.Enhancer != this) ShieldComp.Enhancer = this;

                if (!EnhState.State.Online) return false;
            }
            return BlockMoveAnimationReset();
        }

        private bool BlockWorking()
        {
            if (Enhancer?.CubeGrid == null || Sink.CurrentInputByType(GId) < 0.01f || !Enhancer.Enabled || !Enhancer.IsFunctional)
            {
                NeedUpdate(EnhState.State.Online, false);
                return false;
            }

            if (ShieldComp?.DefenseShields == null || ShieldComp.Enhancer != this)
            {
                if (ShieldComp?.DefenseShields == null)
                {
                    Enhancer?.CubeGrid?.Components.TryGet(out ShieldComp);
                    if (ShieldComp?.DefenseShields == null)
                    {
                        NeedUpdate(EnhState.State.Online, false);
                        return false;
                    }
                }

                if (ShieldComp.Enhancer == null)
                {
                    ShieldComp.Enhancer = this;
                    EnhState.State.Backup = false;
                }
                else if (ShieldComp.Enhancer != this)
                {
                    EnhState.State.Backup = true;
                    EnhState.State.Online = false;
                }

                if (Enhancer != null && _tick % 300 == 0)
                {
                    Enhancer.RefreshCustomInfo();
                    Enhancer.ShowInToolbarConfig = false;
                    Enhancer.ShowInToolbarConfig = true;
                }

            }

            if (!EnhState.State.Backup && ShieldComp?.Enhancer == this)
            {
                NeedUpdate(EnhState.State.Online, true);
                return true;
            }

            NeedUpdate(EnhState.State.Online, false);
            return false;
        }

        private void NeedUpdate(bool onState, bool turnOn)
        {
            if (!onState && turnOn)
            {
                EnhState.State.Online = true;
                EnhState.SaveState();
                EnhState.NetworkUpdate();
            }
            else if (onState & !turnOn)
            {
                EnhState.State.Online = false;
                EnhState.SaveState();
                EnhState.NetworkUpdate();
            }
        }

        public void UpdateState(ProtoEnhancerState newState)
        {
            EnhState.State = newState;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateState: EnhancerId [{Enhancer.EntityId}]");
        }

        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                ContainerInited = true;
                if (Session.Enforced.Debug >= 1) Log.Line($"ContainerInited:  EmitterId [{Enhancer.EntityId}]");
            }
            if (Entity.InScene) OnAddedToScene();
        }


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                StorageSetup();
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
                PowerInit();
                Enhancer.CubeGrid.Components.TryGet(out ShieldComp);
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                Enhancer.AppendingCustomInfo += AppendingCustomInfo;
                Enhancer.RefreshCustomInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (Session.IsServer)
            {
                if (Enhancer.Storage != null) EnhState.SaveState();
            }
            return false;
        }

        private void StorageSetup()
        {
            if (EnhState == null) EnhState = new EnhancerState(Enhancer);
            EnhState.StorageInit();
            EnhState.LoadState();
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
                if (Session.Enforced.Debug >= 1) Log.Line($"PowerInit: EnhancerId [{Enhancer.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool BlockMoveAnimationReset()
        {
            if (!Enhancer.IsFunctional) return false;
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            if (_subpartRotor.Closed) _subpartRotor.Subparts.Clear();
            return true;
        }

        private void BlockMoveAnimation()
        {
            if (!BlockMoveAnimationReset()) return;
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
    }
}

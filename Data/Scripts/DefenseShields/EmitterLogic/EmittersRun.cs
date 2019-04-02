namespace DefenseSystems
{
    using System;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EmitterL", "EmitterS", "EmitterST", "EmitterLA", "EmitterSA")]
    public partial class Emitters : MyGameLogicComponent
    {
        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                if (!MyAPIGateway.Utilities.IsDedicated) NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                else NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                Emitter = (IMyUpgradeModule)Entity;
                ContainerInited = true;
                if (Session.Enforced.Debug == 3) Log.Line($"ContainerInited: EmitterId [{Emitter.EntityId}]");
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

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Emitter.Storage != null) EmiState.SaveState();
            }
            return false;
        }

        public override void OnAddedToScene()
        {
            try
            {
                LocalGrid = (MyCubeGrid)Emitter.CubeGrid;
                MyCube = Emitter as MyCubeBlock;
                SetEmitterType();
                //RegisterEvents();
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (!_bInit) BeforeInit();
                else if (_bCount < SyncCount * _bTime)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    if (DefenseBus?.MasterGrid == LocalGrid) _bCount++;
                }
                else _readyToSync = true;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = Session.Instance.Tick;
                _tick60 = _tick % 60 == 0;
                var wait = _isServer && !_tick60 && EmiState.State.Backup;

                LocalGrid = MyCube.CubeGrid;
                if (wait || LocalGrid?.Physics == null) return;

                IsStatic = LocalGrid.IsStatic;
                Timing();
                if (!ControllerLink()) return;

                if (!_isDedicated && UtilsStatic.DistanceCheck(Emitter, 1000, EmiState.State.BoundingRange))
                {
                    var blockCam = MyCube.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam)) BlockMoveAnimation();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                if (_count++ == 5) _count = 0;
                var wait = _isServer && _count != 0 && EmiState.State.Backup;

                LocalGrid = MyCube.CubeGrid;
                if (wait || LocalGrid?.Physics == null) return;
                IsStatic = LocalGrid.IsStatic;

                ControllerLink();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation10: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Enforced.Debug == 3) Log.Line($"OnRemovedFromScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
                //if (DefenseBus?.ActiveEmitter == this) DefenseBus.ActiveEmitter = null;
                //if (DefenseBus?.ShipEmitter == this) DefenseBus.ShipEmitter = null;
                Registry.RegisterWithBus(this, LocalGrid, false, DefenseBus, out DefenseBus);
                IsWorking = false;
                IsFunctional = false;
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void Close()
        {
            try
            {
                base.Close();
                if (Session.Enforced.Debug == 3) Log.Line($"Close: {EmitterMode} - EmitterId [{Entity.EntityId}]");
                if (Session.Instance.Emitters.Contains(this)) Session.Instance.Emitters.Remove(this);
                Registry.RegisterWithBus(this, LocalGrid, false, DefenseBus, out DefenseBus);

                /*
                if (DefenseBus?.ActiveEmitter == this)
                {
                    if ((int)EmitterMode == DefenseBus.EmitterMode)
                    {
                        DefenseBus.EmitterLos = false;
                        DefenseBus.EmitterEvent = true;
                    }
                    DefenseBus.ActiveEmitter = null;
                }
                */
                /*
                else if (DefenseBus?.ShipEmitter == this)
                {
                    if ((int)EmitterMode == DefenseBus.EmitterMode)
                    {
                        DefenseBus.EmitterLos = false;
                        DefenseBus.EmitterEvent = true;
                    }
                    DefenseBus.ShipEmitter = null;
                }
                */
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
                if (Session.Enforced.Debug == 3) Log.Line($"MarkForClose: {EmitterMode} - EmitterId [{Entity.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }
    }
}
using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "DSSupergen")]
    public class O2Generators : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _airIPercent = -1;
        private int _lCount;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;

        private double _shieldVolFilled;

        internal float EmissiveIntensity;

        public bool ServerUpdate;
        internal bool AllInited;
        internal bool Suspended;
        internal bool Prime;
        internal bool Alpha;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        public bool O2Online;

        private const string PlasmaEmissive = "PlasmaEmissive";
        private const string EmitterEffect = "EmitterEffect";

        public MyModStorageComponentBase Storage { get; set; }
        internal ShieldGridComponent ShieldComp;
        internal O2GeneratorGridComponent OGridComp;
        internal MyResourceSourceComponent Source;
        private MyEntitySubpart _subpartRotor;
        private MyParticleEffect _effect = new MyParticleEffect();

        internal DSUtils Dsutil1 = new DSUtils();

        public IMyGasGenerator O2Generator => (IMyGasGenerator)Entity;
        private IMyInventory _inventory;

        private readonly Dictionary<long, O2Generators> _o2Generator = new Dictionary<long, O2Generators>();

        public override void UpdateBeforeSimulation()
        {
            try
            {
                IsStatic = O2Generator.CubeGrid.Physics.IsStatic;
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (Suspend() || StoppedWorking() || !AllInited && !InitO2Generator()) return;
                if (Prime && OGridComp?.Comp == null) MasterElection();
                Timing();

                if (!BlockWorking()) return;

                if (ShieldComp.ShieldActive && BlockIsWorking)
                {
                    var blockCam = O2Generator.PositionComp.WorldVolume;
                    var onCam = MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam);
                    if (!Session.DedicatedServer && onCam && UtilsStatic.ShieldDistanceCheck(O2Generator, 1000, ShieldComp.BoundingRange))
                    {
                        //if (_effect == null && ShieldComp.ShieldPercent <= 97) BlockParticleStart();
                        //else if (_effect != null && ShieldComp.ShieldPercent > 97f) BlockParticleStop();

                        //BlockMoveAnimation();
                    }
                }
                else if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (Suspended || Alpha || !AllInited || !ShieldComp.ShieldActive || !BlockIsWorking) return;

                var sc = ShieldComp;
                var shieldFullVol = sc.ShieldVolume;

                var startingO2Fpercent = sc.DefaultO2 + sc.IncreaseO2ByFPercent;
                _shieldVolFilled = shieldFullVol * startingO2Fpercent;
                UpdateAirEmissives(startingO2Fpercent);

                var shieldVolStillEmpty = shieldFullVol - _shieldVolFilled;
                if (!(shieldVolStillEmpty > 0)) return;

                var amount = _inventory.CurrentVolume.RawValue;
                if (amount <= 0) return;
                if (amount - 1000 > 0)
                {
                    _inventory.RemoveItems(0, 1000);
                    _shieldVolFilled += 1000 * 261.333333333;
                }
                else
                {
                    _inventory.RemoveItems(0, _inventory.CurrentVolume);
                    _shieldVolFilled += amount * 261.333333333;
                }
                if (_shieldVolFilled > shieldFullVol) _shieldVolFilled = shieldFullVol;

                var shieldVolPercentFull = _shieldVolFilled * 100.0;
                var fPercentToAddToDefaultO2Level = shieldVolPercentFull / shieldFullVol * 0.01 - sc.DefaultO2;

                sc.IncreaseO2ByFPercent = fPercentToAddToDefaultO2Level;
                sc.O2Updated = true;

                //Log.Line($"default:{ShieldComp.DefaultO2} - Filled/(Max):{_shieldVolFilled}/({shieldFullVol}) - ShieldO2Level:{sc.IncreaseO2ByFPercent} - O2Before:{MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(O2Generator.PositionComp.WorldVolume.Center)}");
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
        }

        private bool BlockWorking()
        {
            if (Alpha || !IsStatic || ShieldComp.DefenseShields == null || !ShieldComp.Warming) return false;

            BlockIsWorking = O2Generator.IsWorking;
            BlockWasWorking = BlockIsWorking;

            if (!BlockIsWorking)
            {
                //if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
                return false;
            }
            return true;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
                Session.Instance.O2Generators.Add(this);
                if (!_o2Generator.ContainsKey(Entity.EntityId)) _o2Generator.Add(Entity.EntityId, this);
                //StorageSetup();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        private void StorageSetup()
        {
            Storage = O2Generator.Storage;
        }

        private bool InitO2Generator()
        {
            if (!AllInited)
            {
                O2Generator.CubeGrid.Components.TryGet(out ShieldComp);
                Source = O2Generator.Components.Get<MyResourceSourceComponent>();
                if (ShieldComp == null || Source == null || !ShieldComp.Starting) return false;

                Source.Enabled = false;
                O2Generator.AutoRefill = false;
                _inventory = O2Generator.GetInventory();
                if (!O2Generator.CubeGrid.Components.Has<O2GeneratorGridComponent>())
                {
                    OGridComp = new O2GeneratorGridComponent(this);
                    O2Generator.CubeGrid.Components.Add(OGridComp);
                    OGridComp.Comp = this;
                    Prime = true;
                }
                else
                {
                    O2Generator.CubeGrid.Components.TryGet(out OGridComp);
                    if (OGridComp.Comp != null) OGridComp.Comp.Alpha = true;
                    OGridComp.Comp = this;
                    Prime = true;
                }

                ResetAirEmissives("");
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                OGridComp.RegisteredComps.Add(this);
                BlockWasWorking = true;
                AllInited = true;
                return !Suspend();
            }
            return false;
        }

        private bool Suspend()
        {
            if (Prime && !IsStatic)
            {
                if (_effect != null && !Session.DedicatedServer) BlockParticleStop();
                return true;
            }

            return false;
        }

        private bool StoppedWorking()
        {
            if (!O2Generator.IsFunctional && BlockIsWorking)
            {
                BlockIsWorking = false;
                return true;
            }
            return !O2Generator.IsFunctional;
        }

        private void MasterElection()
        {
            var hasOComp = O2Generator.CubeGrid.Components.Has<O2GeneratorGridComponent>();
            if (!hasOComp)
            {
                if (!IsStatic) return;
                OGridComp = new O2GeneratorGridComponent(this);
                O2Generator.CubeGrid.Components.Add(OGridComp);
                _inventory = O2Generator.GetInventory();
                OGridComp.Comp = this;
                Prime = true;
                Alpha = false;
            }
            else 
            {
                if (!IsStatic) return;
                O2Generator.CubeGrid.Components.TryGet(out OGridComp);
                if (OGridComp.Comp != null) OGridComp.Comp.Alpha = true;
                _inventory = O2Generator.GetInventory();
                OGridComp.Comp = this;
                Prime = true;
                Alpha = false;
            }
            ResetAirEmissives("");
        }

        private void UpdateAirEmissives(double fPercent)
        {
            var tenPercent = fPercent * 10;
            if (tenPercent < 10 && (int)tenPercent != _airIPercent) _airIPercent = (int)tenPercent;
            else return;
            var airString = "Emissive" + _airIPercent;
            ResetAirEmissives(airString);
            O2Generator.SetEmissiveParts(airString, UtilsStatic.GetAirEmissiveColorFromDouble(tenPercent * 10), 1f);
        }

        private void ResetAirEmissives(string airString)
        {
            if (airString != "Emissive9") O2Generator.SetEmissiveParts("Emissive9", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive8") O2Generator.SetEmissiveParts("Emissive8", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive7") O2Generator.SetEmissiveParts("Emissive7", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive6") O2Generator.SetEmissiveParts("Emissive6", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive5") O2Generator.SetEmissiveParts("Emissive5", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive4") O2Generator.SetEmissiveParts("Emissive4", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive3") O2Generator.SetEmissiveParts("Emissive3", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive2") O2Generator.SetEmissiveParts("Emissive2", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive1") O2Generator.SetEmissiveParts("Emissive1", Color.Transparent, 0f);
            else return;
            if (airString != "Emissive0") O2Generator.SetEmissiveParts("Emissive0", Color.Transparent, 0f);
        }

        #region Block Animation
        private void BlockMoveAnimationReset()
        {
            if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
        }

        private void BlockMoveAnimation()
        {
            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
            RotationTime -= 1;
            if (AnimationLoop == 0) TranslationTime = 0;
            if (AnimationLoop < 299) TranslationTime += 1;
            else TranslationTime -= 1;
            if (_count == 0) EmissiveIntensity = 2;
            if (_count < 30) EmissiveIntensity += 1;
            else EmissiveIntensity -= 1;

            var rotationMatrix = MatrixD.CreateRotationY(0.05f * RotationTime);
            var matrix = rotationMatrix * MatrixD.CreateTranslation(0, 99999 * TranslationTime, 0);

            _subpartRotor.PositionComp.LocalMatrix = matrix;
            _subpartRotor.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetEmissiveColorFromFloat(ShieldComp.ShieldPercent), 0.1f * EmissiveIntensity);

            if (AnimationLoop++ == 599) AnimationLoop = 0;
        }

        private void BlockParticleUpdate()
        {
            if (_effect == null) return;
            var testDist  =9999;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - O2Generator.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = O2Generator.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = O2Generator.PositionComp.WorldMatrix;

            predictedMatrix.Translation = spawnPos;
            if (ShieldComp.ShieldVelocitySqr > 4000) predictedMatrix.Translation = spawnPos + O2Generator.CubeGrid.Physics.GetVelocityAtPoint(O2Generator.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            _effect.WorldMatrix = predictedMatrix;

        }

        private void BlockParticleStop()
        {
            if (_effect == null) return;

            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private void BlockParticleStart()
        {
            var scale = 9999f;
            MyParticlesManager.TryCreateParticleEffect(EmitterEffect, out _effect);
            _effect.UserScale = 1f;
            _effect.UserRadiusMultiplier = scale;
            _effect.UserEmitterScale = 1f;
            BlockParticleUpdate();
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
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
                BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_o2Generator.ContainsKey(Entity.EntityId)) _o2Generator.Remove(Entity.EntityId);
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
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
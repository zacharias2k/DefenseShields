using System;
using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        internal Fields(Bus bus)
        {
            Bus = bus;
            Run();
        }

        internal void Run(bool run = true)
        {
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;
            if (run)
            {
                MyAPIGateway.Session.OxygenProviderSystem.AddOxygenGenerator(_ellipsoidOxyProvider);
                Inited = true;
            }
            else
            {
                MyAPIGateway.Session.OxygenProviderSystem.RemoveOxygenGenerator(_ellipsoidOxyProvider);
                Inited = false;
            }
        }

        internal Controllers.Status Status()
        {
            //if (UpdateDimensions) RefreshDimensions();

            //if (Bus.Tick >= LosCheckTick) LosCheck();
            //if (EmitterEvent) EmitterEventDetected();
            //if (ShapeEvent || FitChanged) CheckExtents();
            //if (AdjustShape) ReAdjustShape(true);

            if (!ServerShieldUp())
            {
                if (Bus.ActiveController.State.Value.Lowered) return Controllers.Status.Lowered;
                if (_overLoadLoop > -1 || _reModulationLoop > -1 || _empOverLoadLoop > -1) FailureDurations();
                return Controllers.Status.Failure;
            }

            return Controllers.Status.Active;

        }

        internal Controllers.Status ClientStatus()
        {
            var a = Bus.ActiveController;
            var state = a.State;
            //if (Bus.Field.UpdateDimensions) Bus.Field.RefreshDimensions();

            if (!Bus.Field.ShieldIsMobile && !state.Value.IncreaseO2ByFPercent.Equals(_ellipsoidOxyProvider.O2Level))
                _ellipsoidOxyProvider.UpdateOxygenProvider(Bus.Field.DetectMatrixOutsideInv, state.Value.IncreaseO2ByFPercent);

            a.PowerOnline();
            Bus.Field.StepDamageState();

            if (!ClientShieldShieldRaised()) return Controllers.Status.Lowered;
            else return Controllers.Status.Active;
        }

        internal void Up()
        {
            _firstLoop = false;
            if (!_isDedicated) ShellVisibility();
            ShieldEnt.Render.Visible = true;
            UpdateRender = true;
            UpdateMobileShape = true;
            //ShapeEvent = true;
            Bus.DelayEvents(Bus.Events.ShapeEvent);

            if (_isServer)
            {
                CleanWebEnts();
            }
            else CheckBlocksAndNewShape(false);

            if (!_isDedicated) Bus.ResetDamageEffects();
        }

        internal void Sleeping()
        {
            if (!ShieldIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

            Bus.ActiveController.State.Value.IncreaseO2ByFPercent = 0f;
            if (!_isDedicated) ShellVisibility(true);
        }

        internal void Suspend(Controllers controller)
        {
            /*
            if (Bus.ActiveController == controller && Bus.Field.EmitterEvent)
            {
                Bus.Field.EmitterEventDetected();
            }
            */
            Bus.Field.OfflineShield(true, true, Controllers.Status.Suspend, true);
        }

        internal void ClientDown()
        {
            if (FieldMaxPower <= 0) Bus.BroadcastMessage(true);
            if (!Bus.Field.ShieldIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
            Bus.Field.ShellVisibility(true);
        }

        private bool ServerShieldUp()
        {
            var a = Bus.ActiveController;
            var state = a.State;

            var notFailing = _overLoadLoop == -1 && _empOverLoadLoop == -1 && _reModulationLoop == -1;
            FieldActive = ShieldRaised() && state.Value.EmitterLos && notFailing && a.PowerOnline();
            if (!FieldActive) return false;
            var prevOnline = state.Value.Online;
            if (!prevOnline && ShieldIsMobile && FieldShapeBlocked()) return false;

            Bus.Starting = !prevOnline || _firstLoop;

            state.Value.Online = true;

            if (!ShieldIsMobile && (Bus.Starting || O2Updated))
            {
                _ellipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, state.Value.IncreaseO2ByFPercent);
                O2Updated = false;
            }

            StepDamageState();
            return true;
        }

        private void FailureDurations()
        {
            var a = Bus.ActiveController;
            var state = a.State;
            if (_overLoadLoop == 0 || _empOverLoadLoop == 0 || _reModulationLoop == 0)
            {
                if (state.Value.Online || !a.WarmedUp)
                {
                    if (_overLoadLoop != -1)
                    {
                        state.Value.Overload = true;
                        state.Value.Message = true;
                    }

                    if (_empOverLoadLoop != -1)
                    {
                        state.Value.EmpOverLoad = true;
                        state.Value.Message = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        state.Value.Remodulate = true;
                        state.Value.Message = true;
                    }
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    state.Value.Remodulate = false;
                    _reModulationLoop = -1;
                }
            }

            if (_overLoadLoop > -1)
            {
                _overLoadLoop++;
                if (_overLoadLoop == ShieldDownCount - 1) CheckEmitters = true;
                if (_overLoadLoop == ShieldDownCount)
                {
                    if (!state.Value.EmitterLos)
                    {
                        state.Value.Overload = false;
                        _overLoadLoop = -1;
                    }
                    else
                    {
                        state.Value.Overload = false;
                        _overLoadLoop = -1;
                        var recharged = ShieldChargeRate * ShieldDownCount / 60;
                        state.Value.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.10f, ShieldMaxCharge * 0.25f);
                    }
                }
            }

            if (_empOverLoadLoop > -1)
            {
                _empOverLoadLoop++;
                if (_empOverLoadLoop == EmpDownCount - 1) CheckEmitters = true;
                if (_empOverLoadLoop == EmpDownCount)
                {
                    if (!state.Value.EmitterLos)
                    {
                        state.Value.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                    }
                    else
                    {
                        state.Value.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                        _empOverLoad = false;
                        var recharged = ShieldChargeRate * EmpDownCount / 60;
                        state.Value.Charge = MathHelper.Clamp(recharged, ShieldMaxCharge * 0.25f, ShieldMaxCharge * 0.62f);
                    }
                }
            }
        }

        private bool FieldShapeBlocked()
        {
            if (Bus.ActiveModulator == null || Bus.ActiveModulator.ModSet.Settings.ModulateVoxels || Session.Enforced.DisableVoxelSupport == 1) return false;
            var a = Bus.ActiveController;
            var state = a.State;

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(PhysicsOutsideLow, voxel)) continue;

                a.Controller.Enabled = false;
                state.Value.FieldBlocked = true;
                state.Value.Message = true;
                if (Session.Enforced.Debug == 3) Log.Line($"Field blocked: - ControllerId [{a.Controller.EntityId}]");
                return true;
            }
            state.Value.FieldBlocked = false;
            return false;
        }

        internal void OfflineShield(bool clear, bool resetShape, Controllers.Status reason, bool keepCharge = false)
        {
            var a = Bus.ActiveController;
            DefaultShieldState(clear, keepCharge, resetShape);

            if (_isServer) a.ProtChangedState();
            else a.TerminalRefresh();

            if (!_isDedicated) ShellVisibility(true);

            if (Session.Enforced.Debug >= 2) Log.Line($"[ShieldOff] reason:{reason} - clear:{clear} - resetShape:{resetShape} - keepCharge:{keepCharge} - ControllerId [{a.Controller.EntityId}]");
        }

        private void DefaultShieldState(bool clear, bool keepCharge, bool resetShape = true)
        {
            var a = Bus.ActiveController;
            var state = a.State;
            a.NotFailed = false;
            if (clear)
            {
                a.SinkPower = 0.001f;
                a.Sink.Update();

                if (_isServer && !keepCharge)
                {
                    state.Value.Charge = 0f;
                    state.Value.ShieldPercent = 0f;
                }
                if (resetShape)
                {
                    ResetShape(true, true);
                    //ShapeEvent = true;
                    Bus.DelayEvents(Bus.Events.ShapeEvent);
                }
            }
            if (_isServer)
            {
                state.Value.IncreaseO2ByFPercent = 0f;
                state.Value.Heat = 0;
                state.Value.Online = false;
            }

            _currentHeatStep = 0;
            _accumulatedHeat = 0;
            _heatCycle = -1;

            Absorb = 0f;
            EnergyHit = false;
            WorldImpactPosition = Vector3D.NegativeInfinity;
            ShieldEnt.Render.Visible = false;

            a.TerminalRefresh(false);
            CleanWebEnts();
            lock (Session.Instance.ActiveProtection) Session.Instance.ActiveProtection.Remove(Bus.ActiveController);
        }

        private bool ShieldRaised()
        {
            var a = Bus.ActiveController;
            var set = a.Set;
            var state = a.State;

            if (!set.Value.RaiseShield)
            {
                if (!state.Value.Lowered)
                {
                    if (!ShieldIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    state.Value.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    state.Value.Lowered = true;
                    a.ProtChangedState();
                }

                a.PowerOnline();
                return false;
            }
            if (state.Value.Lowered)
            {
                if (!ShieldIsMobile)
                {
                    _ellipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, state.Value.IncreaseO2ByFPercent);
                    O2Updated = false;
                }
                state.Value.Lowered = false;
                if (!_isDedicated) ShellVisibility();
                a.ProtChangedState();
            }

            return true;
        }


        private bool ClientShieldShieldRaised()
        {
            var a = Bus.ActiveController;
            var state = a.State;
            if (a.WarmedUp && state.Value.Lowered)
            {
                if (!_clientAltered)
                {
                    if (!Bus.Field.ShieldIsMobile) _ellipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    Bus.Field.ShellVisibility(true);
                    _clientAltered = true;
                }
                return false;
            }

            if (_clientAltered)
            {
                ShellVisibility();
                _clientAltered = false;
            }

            return true;
        }

    }
}

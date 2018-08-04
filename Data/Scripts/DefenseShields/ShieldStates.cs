using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void EmitterEventDetected()
        {
            if (!GridIsMobile)
            {
                UpdateDimensions = true;
                if (UpdateDimensions) RefreshDimensions();
            }
            ShieldComp.EmitterEvent = false;
            if (!ShieldComp.EmittersWorking)
            {
                _genericDownLoop = 0;
                if (Session.Enforced.Debug == 1) Log.Line($"EmitterEvent: detected an emitter event and no emitter is working, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
        }

        private void FailureConditions()
        {
            if (_overLoadLoop == 0 || _reModulationLoop == 0 || _genericDownLoop == 0) ResetShape(true, true);
            if (_overLoadLoop == 0 || _reModulationLoop == 0)
            {
                if (!ShieldOffline) OfflineShield();
                var realPlayerIds = new HashSet<long>();
                UtilsStatic.GetRealPlayers(Shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
                foreach (var id in realPlayerIds)
                {
                    if (_overLoadLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red", id);
                    if (_reModulationLoop == 0) MyVisualScriptLogicProvider.ShowNotification("[ " + Shield.CubeGrid.DisplayName + " ]" + " -- shield remodremodulating, restarting in 5 seconds.", 4800, "White", id);
                }

            }
            else if (_genericDownLoop == 0 && !ShieldOffline) OfflineShield();

            if (_shapeChanged && !_ellipsoidAdjust.Equals(_oldEllipsoidAdjust) || !_gridHalfExtents.Equals(_oldGridHalfExtents)) ResetShape(false, false);

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    ShieldOffline = false;
                    _reModulationLoop = -1;
                    return;
                }
                return;
            }

            if (_genericDownLoop > -1)
            {
                _genericDownLoop++;
                if (_genericDownLoop == GenericDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_genericDownLoop == GenericDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        _genericDownLoop = 0;
                    }
                    else
                    {
                        ShieldOffline = false;
                        _genericDownLoop = -1;
                    }
                    return;
                }
                return;
            }

            _overLoadLoop++;
            if (_overLoadLoop == ShieldDownCount - 1) ShieldComp.CheckEmitters = true;
            if (_overLoadLoop == ShieldDownCount)
            {
                if (!ShieldComp.EmittersWorking)
                {
                    _genericDownLoop = 0;
                }
                else
                {
                    ShieldOffline = false;
                    _overLoadLoop = -1;
                }
                var nerf = Session.Enforced.Nerf > 0 && Session.Enforced.Nerf < 1;
                var nerfer = nerf ? Session.Enforced.Nerf : 1f;
                ShieldBuffer = (_shieldMaxBuffer / 25) * nerfer; // replace this with something that scales based on charge rate
            }
        }

        private void OfflineShield()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                if (Session.Enforced.Debug == 1) Log.Line($"Offline count: {_offlineCnt} - resetting all - was: Buffer:{ShieldBuffer} - Absorb:{Absorb} - Percent:{ShieldComp.ShieldPercent} - O2:{ShieldComp.IncreaseO2ByFPercent} - Lowered:{ShieldWasLowered}");

                if (!_power.Equals(0.0001f)) _power = 0.001f;
                Sink.Update();
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                ResetShape(true, true);
                ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                CleanUp(0);
                CleanUp(1);
                CleanUp(3);
                CleanUp(4);
            }
            Absorb = 0f;
            ShieldBuffer = 0f;
            ShieldComp.ShieldPercent = 0f;
            ShieldComp.IncreaseO2ByFPercent = 0f;
            Shield.RefreshCustomInfo();
            Shield.ShowInToolbarConfig = false;
            Shield.ShowInToolbarConfig = true;
            ShieldComp.ShieldActive = false;
            PrevShieldActive = false;
            ShieldWasLowered = false;
            _shellPassive.Render.UpdateRenderObject(false);
            _shellActive.Render.UpdateRenderObject(false);
            ShieldOffline = true;
            if (Session.Enforced.Debug == 1) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {_shieldCurrentPower} - gridMax: {_gridMaxPower} - currentPower: {_gridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private bool ShieldLowered()
        {
            if (!ShieldComp.RaiseShield && WarmedUp && ShieldComp.ShieldActive)
            {
                Timing(false);
                if (!ShieldWasLowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    ShieldComp.IncreaseO2ByFPercent = 0f;
                    _shellPassive.Render.UpdateRenderObject(false);
                    _shellActive.Render.UpdateRenderObject(false);
                    DsSet.NetworkUpdate();
                    DsSet.SaveSettings();
                    ShieldWasLowered = true;
                }
                PowerOnline();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!Shield.IsWorking || !ShieldComp.EmittersWorking)
                {
                    _genericDownLoop = 0;
                    return false;
                }

                if (GridIsMobile && _lCount == 0 && _count == 0)
                {
                    _updateMobileShape = true;
                    MobileUpdate();
                }
                else if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }
            if (ShieldWasLowered && ShieldComp.ShieldActive && Shield.IsWorking)
            {
                if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                _shellActive.Render.UpdateRenderObject(false);
                DsSet.NetworkUpdate();
                DsSet.SaveSettings();
                ShieldWasLowered = false;
            }
            return false;
        }

        private bool ShieldSleeping()
        {
            if (ShieldComp.EmittersSuspended)
            {
                if (!ShieldWasSleeping)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    ShieldEnt.PositionComp.SetWorldMatrix(MatrixD.Zero);
                    ShieldComp.IncreaseO2ByFPercent = 0f;
                    _shellPassive.Render.UpdateRenderObject(false);
                    _shellActive.Render.UpdateRenderObject(false);
                    DsSet.NetworkUpdate();
                    DsSet.SaveSettings();
                    ShieldWasSleeping = true;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 1) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }

                ShieldWasSleeping = true;
                return ShieldWasSleeping;
            }

            if (ShieldWasSleeping)
            {
                ShieldWasSleeping = false;
                if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);
                _blockChanged = true;
                _functionalChanged = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                _shellActive.Render.UpdateRenderObject(false);
                DsSet.NetworkUpdate();
                DsSet.SaveSettings();
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 1) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            ShieldWasSleeping = false;
            return ShieldWasSleeping;
        }
    }
}

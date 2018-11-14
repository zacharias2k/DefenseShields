using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        public enum PlayerNotice
        {
            EmitterInit,
            FieldBlocked,
            OverLoad,
            EmpOverLoad,
            Remodulate,
            NoPower,
            NoLos
        };

        private void ShieldChangeState()
        {
            if (!WarmedUp && !DsState.State.Message)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"ChangeStateSupression: WarmedUp:{WarmedUp} - Message:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
                return;
            }
            if (Session.Enforced.Debug >= 2) Log.Line($"ShieldChangeState: Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
            else if (Session.Enforced.Debug >= 1 && DsState.State.Message) Log.Line($"ShieldChangeState: Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
            if (Session.MpActive)
            {
                if (Session.Enforced.Debug >= 2) Log.Line($"ServerUpdate: Broadcast:{DsState.State.Message}");
                DsState.NetworkUpdate();
            }
            if (!_isDedicated && DsState.State.Message)
            {
                BroadcastMessage();
                Shield.RefreshCustomInfo();
            }
            DsState.State.Message = false;
            DsState.SaveState();
        }

        public void UpdateSettings(ProtoControllerSettings newSettings)
        {
            var newShape = newSettings.ExtendFit != DsSet.Settings.ExtendFit || newSettings.FortifyShield != DsSet.Settings.FortifyShield || newSettings.SphereFit != DsSet.Settings.SphereFit;
            DsSet.Settings = newSettings;
            SettingsUpdated = true;
            if (newShape) FitChanged = true;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateSettings - server:{Session.IsServer} - ShieldId [{Shield.EntityId}]:\n{newSettings}");
        }

        public void UpdateState(ProtoControllerState newState)
        {
            DsState.State = newState;
            if (!_isServer && Session.Enforced.Debug >= 1 && (_clientNotReady || DsState.State.Mode < 0)) Log.Line($"UpdateState - ClientAndReady:{!_clientNotReady} - Mode:{DsState.State.Mode} - server:{Session.IsServer} - ShieldId [{Shield.EntityId}]:\n{newState}");
            _clientNotReady = false;
        }

        private bool EntityAlive()
        {
            _tick = Session.Instance.Tick;
            _tick60 = _tick % 60 == 0;
            _tick600 = _tick % 600 == 0;
            var wait = _isServer && !_tick60 && DsState.State.Suspended;

            MyGrid = MyCube.CubeGrid;
            if (MyGrid?.Physics == null) return false;

            if (_resetEntity) ResetEntity();

            if (wait ||!AllInited && !PostInit()) return false;
            if (Session.Enforced.Debug >= 1) Dsutil1.Sw.Restart();

            IsStatic = MyGrid.IsStatic;

            if (!Warming) WarmUpSequence();

            if (_subUpdate && _tick >= _subTick) HierarchyUpdate();
            if (_blockEvent && _tick >= _funcTick) BlockChanged(true);

            return true;
        }

        private bool ShieldOn()
        {
            if (_isServer)
            {
                if (!ControllerFunctional() || ShieldWaking())
                {
                    if (ShieldFailing()) return false;
                    _prevShieldActive = false;
                    return false;
                }
                var powerState = PowerOnline();
                if (!powerState && _genericDownLoop == -1) _genericDownLoop = 0;

                if (_tick60)
                {
                    GetModulationInfo();
                    GetEnhancernInfo();
                }

                if (ShieldFailing())
                {
                    _prevShieldActive = false;
                    return false;
                }
                SetShieldServerStatus(powerState);
                Timing(true);
                if (!DsState.State.Online || ComingOnline && (!GridOwnsController() || GridIsMobile && FieldShapeBlocked()))
                {
                    _prevShieldActive = false;
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
                    ShieldFailing();
                    return false;
                }
                _syncEnts = _forceData.Count != 0 || _impulseData.Count != 0 || _missileDmg.Count != 0 ||
                            _fewDmgBlocks.Count != 0 || _dmgBlocks.Count != 0 || _meteorDmg.Count != 0 ||
                            _empDmg.Count != 0 || _eject.Count != 0 || _destroyedBlocks.Count != 0 ||
                            _voxelDmg.Count != 0 || _characterDmg.Count != 0;
            }
            else
            {
                if (_blockChanged) BlockMonitor();
                SetShieldClientStatus();
                if (ClientOfflineStates() || ClientShieldLowered()) return false;
                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();
                PowerOnline();
                Timing(true);
                _clientOn = true;
                _clientLowered = false;
            }

            return true;
        }

        private void ComingOnlineSetup()
        {
            if (!_isDedicated) ShellVisibility();
            ShieldEnt.Render.Visible = true;
            _updateRender = true;
            ComingOnline = false;
            WasOnline = true;
            WarmedUp = true;

            if (_isServer)
            {
                CleanAll();
                _offlineCnt = -1;
                ShieldChangeState();
                if (Session.Enforced.Debug >= 1) Log.Line($"StateUpdate: ComingOnlineSetup - ShieldId [{Shield.EntityId}]");
            }
            else
            {
                UpdateSubGrids();
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug >= 1) Log.Line($"StateUpdate: ComingOnlineSetup - ShieldId [{Shield.EntityId}]");
            }
        }

        private bool ShieldFailing()
        {
            if (_overLoadLoop > -1 || _reModulationLoop > -1 || _genericDownLoop > -1 || _empOverLoadLoop > -1)
            {
                FailureConditions();
                return true;
            }
            return false;
        }

        private void OfflineShield()
        {
            _power = 0.001f;
            Sink.Update();
            WasOnline = false;
            ShieldEnt.Render.Visible = false;
            ShieldEnt.PositionComp.SetPosition(Vector3D.Zero);
            if (_isServer && !DsState.State.Lowered && !DsState.State.Sleeping)
            {
                DsState.State.ShieldPercent = 0f;
                DsState.State.Buffer = 0f;
            }

            if (_isServer)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"StateUpdate: ShieldOff - ShieldId [{Shield.EntityId}]");
                ShieldChangeState();
            }
            else
            {
                UpdateSubGrids();
                Shield.RefreshCustomInfo();
            }
        }

        private bool ControllerFunctional()
        {
            if (_blockChanged) BlockMonitor();

            if (_tick >= LosCheckTick) LosCheck();
            if (Suspend() || ShieldSleeping() || ShieldLowered()) return false;
            if (ShieldComp.EmitterEvent) EmitterEventDetected();

            _controlBlockWorking = MyCube.IsWorking && MyCube.IsFunctional;
            if (!_controlBlockWorking || !ShieldComp.EmittersWorking)
            {
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                return false;
            }
            if (_controlBlockWorking)
            {
                if (GridIsMobile) MobileUpdate();
                if (UpdateDimensions) RefreshDimensions();
            }
            return _controlBlockWorking;
        }

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
                DsState.State.EmitterWorking = false;
                if (GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los) DsState.State.Message = true;
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) DsState.State.Message = true;
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                if (Session.Enforced.Debug >= 1) Log.Line($"EmitterEvent: detected an emitter event and no emitter is working, shield mode: {ShieldMode} - Broadcast:{DsState.State.Message} - ShieldId [{Shield.EntityId}]");
                return;
            }
            DsState.State.EmitterWorking = true;
        }

        private void FailureConditions()
        {
            if (_overLoadLoop == 0 || _empOverLoadLoop == 0 || _reModulationLoop == 0 || _genericDownLoop == 0)
            {
                _prevShieldActive = false;
                if (DsState.State.Online)
                {
                    DsState.State.Online = false;
                    if (_overLoadLoop != -1)
                    {
                        DsState.State.Overload = true;
                        DsState.State.Message = true;
                    }

                    if (_empOverLoadLoop != -1)
                    {
                        DsState.State.EmpOverLoad = true;
                        DsState.State.Message = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        DsState.State.Remodulate = true;
                        DsState.State.Message = true;
                    }
                    ShieldFailed();
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    DsState.State.Remodulate = false;
                    _reModulationLoop = -1;
                }
            }

            if (_genericDownLoop > -1)
            {
                _genericDownLoop++;
                if (_genericDownLoop == GenericDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        DsState.State.EmitterWorking = false;
                        _genericDownLoop = 0;
                    }
                    else
                    {
                        DsState.State.EmitterWorking = true;
                        _genericDownLoop = -1;
                    }
                }
            }

            if (_overLoadLoop > -1)
            {
                _overLoadLoop++;
                if (_overLoadLoop == ShieldDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_overLoadLoop == ShieldDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                        var recharged = _shieldChargeRate * ShieldDownCount / 60;
                        DsState.State.Buffer = MathHelper.Clamp(recharged, ShieldMaxBuffer * 0.10f, ShieldMaxBuffer * 0.25f);
                    }
                }
            }

            if (_empOverLoadLoop > -1)
            {
                _empOverLoadLoop++;
                if (_empOverLoadLoop == EmpDownCount - 1) ShieldComp.CheckEmitters = true;
                if (_empOverLoadLoop == EmpDownCount)
                {
                    if (!ShieldComp.EmittersWorking)
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.EmpOverLoad = false;
                        _empOverLoadLoop = -1;
                        var recharged = _shieldChargeRate * EmpDownCount / 60;
                        DsState.State.Buffer = MathHelper.Clamp(recharged, ShieldMaxBuffer * 0.25f, ShieldMaxBuffer * 0.62f);
                    }
                }
            }
        }

        private void ShieldFailed()
        {
            _offlineCnt++;
            if (_offlineCnt == 0)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"Offline count: {_offlineCnt} - resetting all - was: Buffer:{DsState.State.Buffer} - Absorb:{Absorb} - Percent:{DsState.State.ShieldPercent} - O2:{DsState.State.IncreaseO2ByFPercent} - Lowered:{DsState.State.Lowered}");

                _power = 0.001f;
                Sink.Update();
                _shieldCurrentPower = Sink.CurrentInputByType(GId);
                ResetShape(true, true);
                CleanUp(0);
                CleanUp(1);
                CleanUp(3);
                CleanUp(4);

                _currentHeatStep = 0;
                _accumulatedHeat = 0;
                _heatCycle = -1;
                Absorb = 0f;
                DsState.State.Buffer = 0f;
                DsState.State.ShieldPercent = 0f;
                DsState.State.IncreaseO2ByFPercent = 0f;

                DsState.State.Heat = 0;

                if (!_isDedicated) ShellVisibility(true);
                SyncThreadedEnts();
            }

            _prevShieldActive = false;
            DsState.State.Online = false;

            if (!_isDedicated)
            {
                Shield.RefreshCustomInfo();
                ((MyCubeBlock)Shield).UpdateTerminal();
            }
            if (Session.Enforced.Debug >= 1) Log.Line($"ShieldDown: Count: {_offlineCnt} - ShieldPower: {_shieldCurrentPower} - gridMax: {_gridMaxPower} - currentPower: {_gridCurrentPower} - maint: {_shieldMaintaintPower} - ShieldId [{Shield.EntityId}]");
        }

        private void LosCheck()
        {
            LosCheckTick = uint.MaxValue;
            ShieldComp.CheckEmitters = true;
        }

        private void SetShieldServerStatus(bool powerState)
        {
            DsState.State.Online = _controlBlockWorking && powerState;
            ComingOnline = !_prevShieldActive && DsState.State.Online;

            _prevShieldActive = DsState.State.Online;

            if (!GridIsMobile && (ComingOnline || ShieldComp.O2Updated))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
                ShieldComp.O2Updated = false;
            }
        }

        private void SetShieldClientStatus()
        {
            ComingOnline = !_prevShieldActive && DsState.State.Online;

            _prevShieldActive = DsState.State.Online;
            if (!GridIsMobile && (ComingOnline || !DsState.State.IncreaseO2ByFPercent.Equals(EllipsoidOxyProvider.O2Level)))
            {
                EllipsoidOxyProvider.UpdateOxygenProvider(DetectMatrixOutsideInv, DsState.State.IncreaseO2ByFPercent);
            }
        }

        private bool ShieldWaking()
        {
            if (_tick < UnsuspendTick)
            {
                if (!DsState.State.Waking)
                {
                    DsState.State.Waking = true;
                    DsState.State.Message = true;
                    if (Session.Enforced.Debug >= 1) Log.Line($"Waking: ShieldId [{Shield.EntityId}]");
                }
                if (_genericDownLoop == -1) _genericDownLoop = 0;
                return true;
            }
            if (UnsuspendTick != uint.MinValue && _tick >= UnsuspendTick)
            {
                ResetShape(false, false);
                _updateRender = true;
                UnsuspendTick = uint.MinValue;
            }
            else if (_shapeTick != uint.MinValue && _tick >= _shapeTick)
            {
                _shapeEvent = true;
                _shapeTick = uint.MinValue;
            }
            DsState.State.Waking = false;
            return false;
        }

        private bool FieldShapeBlocked()
        {
            ModulatorGridComponent modComp;
            MyGrid.Components.TryGet(out modComp);
            if (ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels || Session.Enforced.DisableVoxelSupport == 1) return false;

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange);
            var pruneList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref pruneSphere, pruneList);

            if (pruneList.Count == 0) return false;
            MobileUpdate();
            Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            foreach (var voxel in pruneList)
            {
                if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                if (!CustomCollision.VoxelContact(ShieldComp.PhysicsOutsideLow, voxel)) continue;

                Shield.Enabled = false;
                DsState.State.FieldBlocked = true;
                DsState.State.Message = true;
                if (Session.Enforced.Debug >= 1)Log.Line($"Field blocked: - ShieldId [{Shield.EntityId}]");
                return true;
            }
            DsState.State.FieldBlocked = false;
            return false;
        }

        private bool ShieldLowered()
        {
            if (!DsSet.Settings.RaiseShield && WarmedUp && DsState.State.Online)
            {
                Timing(false);
                if (!DsState.State.Lowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    DsState.State.Lowered = true;
                }
                PowerOnline();

                if (ShieldComp.EmitterEvent) EmitterEventDetected();
                if (!MyCube.IsWorking || !ShieldComp.EmittersWorking)
                {
                    if (_genericDownLoop == -1) _genericDownLoop = 0;
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
            if (DsState.State.Lowered && DsState.State.Online && MyCube.IsWorking)
            {
                if (!_isDedicated) ShellVisibility();
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Lowered = false;
            }
            return false;
        }

        private bool ClientShieldLowered()
        {
            if (WarmedUp && DsState.State.Lowered)
            {
                Timing(false);
                if (!_clientLowered)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientLowered = true;
                    if (Session.Enforced.Debug >= 1) Log.Line($"Lowered: shield lowered - ShieldId [{Shield.EntityId}]");
                }
                PowerOnline();

                if (_lCount == 0 && _count == 0) RefreshDimensions();
                return true;
            }

            if (_clientLowered)
            {
                ShellVisibility();
                _prevShieldActive = false;
            }
            return false;
        }

        private bool ShieldSleeping()
        {
            if (ShieldComp.EmittersSuspended || SlaveControllerLink(IsStatic))
            {
                if (!DsState.State.Sleeping)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);

                    DsState.State.IncreaseO2ByFPercent = 0f;
                    if (!_isDedicated) ShellVisibility(true);
                    DsState.State.Sleeping = true;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug >= 1) Log.Line($"Sleep: controller detected sleeping emitter, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Sleeping = true;
                return true;
            }

            if (DsState.State.Sleeping)
            {
                DsState.State.Sleeping = false;
                if (!_isDedicated) ShellVisibility();
                _blockChanged = true;
                _functionalChanged = true;
                UpdateSubGrids();
                BlockMonitor();
                BlockChanged(false);
                if (GridIsMobile) _updateMobileShape = true;
                else UpdateDimensions = true;

                DsState.State.Sleeping = false;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug >= 1) Log.Line($"Sleep: Controller was sleeping but is now waking, shield mode: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }

            DsState.State.Sleeping = false;
            return false;
        }

        private bool SlaveControllerLink(bool isStatic)
        {
            var notTime = _tick != 0 && _tick % 120 != 0;

            if (notTime && _slaveLink) return true;
            if (notTime || isStatic) return false;
            var mySize = MyGrid.PositionComp.WorldAABB.Size.Volume;
            var myEntityId = MyGrid.EntityId;
            foreach (var grid in ShieldComp.GetLinkedGrids)
            {
                if (grid == MyGrid) continue;   
                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                var ds = shieldComponent?.DefenseShields;
                if (ds?.ShieldComp != null && ds.WasOnline && ds.MyCube.IsWorking)
                {
                    var otherSize = ds.MyGrid.PositionComp.WorldAABB.Size.Volume;
                    var otherEntityId = ds.MyGrid.EntityId;
                    if (!IsStatic && ds.IsStatic || mySize < otherSize || mySize.Equals(otherEntityId) && myEntityId < otherEntityId)
                    {
                        _slaveLink = true;
                        return true;
                    }
                }
            }
            _slaveLink = false;
            return false;
        }

        private void ResetComp()
        {
            ShieldGridComponent comp;
            Shield.CubeGrid.Components.TryGet(out comp);
            if (comp == null)
            {
                ShieldComp = new ShieldGridComponent(this);
                Shield.CubeGrid.Components.Add(ShieldComp);
            }
            else Shield.CubeGrid.Components.TryGet(out ShieldComp);
        }

        private bool Suspend()
        {
            var primeMode = ShieldMode == ShieldType.Station && IsStatic && ShieldComp.StationEmitter == null;
            var betaMode = ShieldMode != ShieldType.Station && !IsStatic && ShieldComp.ShipEmitter == null;
            if (ShieldMode != ShieldType.Station && IsStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Station && !IsStatic) InitSuspend();
            else if (ShieldMode == ShieldType.Unknown) InitSuspend();
            else if (ShieldComp.DefenseShields != this || primeMode || betaMode) InitSuspend(true);
            else if (!GridOwnsController()) InitSuspend(true);
            else
            {
                if (DsState.State.Suspended)
                {
                    if (Session.Enforced.Debug >= 1) Log.Line($"Suspend: controller unsuspending - ShieldId [{Shield.EntityId}]");
                    DsState.State.Suspended = false;
                    DsState.State.Heat = 0;
                    UnsuspendTick = _tick + 1800;

                    _currentHeatStep = 0;
                    _accumulatedHeat = 0;
                    _heatCycle = -1;

                    UpdateEntity();
                    GetEnhancernInfo();
                    GetModulationInfo();
                    if (Session.Enforced.Debug >= 1) Log.Line($"Unsuspended: CM:{ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - Range:{BoundingRange} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.Suspended = false;
            }
            if (DsState.State.Suspended) SetShieldType(true);
            return DsState.State.Suspended;
        }

        private void UpdateEntity()
        {
            ShieldComp.GetLinkedGrids.Clear();
            ShieldComp.GetSubGrids.Clear();
            _blockChanged = true;
            _functionalChanged = true;
            ResetShape(false, true);
            ResetShape(false, false);
            SetShieldType(false);
            if (!_isDedicated) ShellVisibility(true);
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateEntity: sEnt:{ShieldEnt == null} - sPassive:{_shellPassive == null} - controller mode is: {ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ShieldId [{Shield.EntityId}]");
            Icosphere.ShellActive = null;
            _updateRender = true;
        }

        private void InitSuspend(bool cleanEnts = false)
        {
            SetShieldType(true);
            if (!DsState.State.Suspended)
            {
                if (cleanEnts) InitEntities(false);
                DsState.State.Suspended = true;
                ShieldFailed();
                if (Session.Enforced.Debug >= 1) Log.Line($"Suspended: controller mode is: {ShieldMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ShieldId [{Shield.EntityId}]");
            }
            if (ShieldComp.DefenseShields == null) ShieldComp.DefenseShields = this;
            DsState.State.Suspended = true;
        }

        private bool GridOwnsController()
        {
            var notTime = _tick != 0 && _tick % 600 != 0;

            if (notTime && !DsState.State.ControllerGridAccess) return false;
            if (notTime) return true;
            if (MyGrid.BigOwners.Count == 0)
            {
                DsState.State.ControllerGridAccess = false;
                return false;
            }

            var controlToGridRelataion = ((MyCubeBlock)Shield).GetUserRelationToOwner(MyGrid.BigOwners[0]);
            const MyRelationsBetweenPlayerAndBlock faction = MyRelationsBetweenPlayerAndBlock.FactionShare;
            var owner = MyRelationsBetweenPlayerAndBlock.Owner;
            DsState.State.InFaction = controlToGridRelataion == faction;
            DsState.State.IsOwner = controlToGridRelataion == owner;
            
            if (controlToGridRelataion != owner && controlToGridRelataion != faction)
            {
                if (DsState.State.ControllerGridAccess)
                {
                    DsState.State.ControllerGridAccess = false;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug >= 1) Log.Line($"GridOwner: controller is not owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.ControllerGridAccess = false;
                return false;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug >= 1) Log.Line($"GridOwner: controller is owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            DsState.State.ControllerGridAccess = true;
            return true;
        }

        private void PlayerMessages(PlayerNotice notice)
        {
            var realPlayerIds = new HashSet<long>();

            var center = GridIsMobile ? MyGrid.PositionComp.WorldVolume.Center : OffsetEmitterWMatrix.Translation;
            switch (notice)
            {
                case PlayerNotice.EmitterInit:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius * 2, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield is reinitializing and checking LOS, attempting startup in 30 seconds!", 4816, "White");
                    break;
                case PlayerNotice.FieldBlocked:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius  * 2, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + "-- the shield's field cannot form when in contact with a solid body", 6720, "Blue");
                    break;
                case PlayerNotice.OverLoad:
                    UtilsStatic.GetRealPlayers(center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield has overloaded, restarting in 20 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.EmpOverLoad:
                    UtilsStatic.GetRealPlayers(center, 500f, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield was EMPed, restarting in 60 seconds!!", 8000, "Red");
                    break;
                case PlayerNotice.Remodulate:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius * 2, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- shield remodremodulating, restarting in 5 seconds.", 4800, "White");
                    break;
                case PlayerNotice.NoLos:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius * 2, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Emitter does not have line of sight, shield offline", 8000, "Red");
                    break;
                case PlayerNotice.NoPower:
                    UtilsStatic.GetRealPlayers(center, (float)ShieldEnt.PositionComp.WorldVolume.Radius * 2, realPlayerIds);
                    foreach (var id in realPlayerIds) if (id == MyAPIGateway.Session.Player.IdentityId) MyAPIGateway.Utilities.ShowNotification("[ " + MyGrid.DisplayName + " ]" + " -- Insufficient Power, shield is failing!", 5000, "Red");
                    break;
            }

        }

        private void BroadcastMessage()
        {
            if (Session.Enforced.Debug >= 1) Log.Line($"Broadcasting message to local playerId - Server:{_isServer} - Dedicated:{_isDedicated} - Id:{MyAPIGateway.Multiplayer.MyId}");
            var checkMobLos = GridIsMobile && ShieldComp.ShipEmitter != null && !ShieldComp.ShipEmitter.EmiState.State.Los;
            if (!DsState.State.EmitterWorking && (!DsState.State.Waking || checkMobLos && _genericDownLoop > -1 || checkMobLos && !_isServer))
            {
                if (checkMobLos) PlayerMessages(PlayerNotice.NoLos);
                else if (!GridIsMobile && ShieldComp.StationEmitter != null && !ShieldComp.StationEmitter.EmiState.State.Los) PlayerMessages(PlayerNotice.NoLos);
            }
            else if (DsState.State.NoPower) PlayerMessages(PlayerNotice.NoPower);
            else if (DsState.State.Overload) PlayerMessages(PlayerNotice.OverLoad);
            else if (DsState.State.EmpOverLoad) PlayerMessages(PlayerNotice.EmpOverLoad);
            else if (DsState.State.FieldBlocked) PlayerMessages(PlayerNotice.FieldBlocked);
            else if (DsState.State.Waking) PlayerMessages(PlayerNotice.EmitterInit);
            else if (DsState.State.Remodulate) PlayerMessages(PlayerNotice.Remodulate);
        }

        private bool ClientOfflineStates()
        {
            if (DsState.State.Message)
            {
                if (Session.Enforced.Debug >= 1) Log.Line($"ClientOffline: Broadcasting message");
                BroadcastMessage();
                DsState.State.Message = false;
            }

            if (ShieldComp.DefenseShields != this && DsState.State.Online && !DsState.State.Suspended)
            {
                ShieldComp.DefenseShields = this;
                _prevShieldActive = false;
            }

            var offline = DsState.State.Suspended || !DsState.State.Online || DsState.State.Sleeping || !DsState.State.ControllerGridAccess
                          || !DsState.State.EmitterWorking || DsState.State.Remodulate || DsState.State.Waking || DsState.State.Overload;
            if (offline)
            {
                if (_clientOn)
                {
                    if (!GridIsMobile) EllipsoidOxyProvider.UpdateOxygenProvider(MatrixD.Zero, 0);
                    ShellVisibility(true);
                    _clientOn = false;
                    Shield.RefreshCustomInfo();
                }
                _prevShieldActive = false;
                return true;
            }

            if (!_clientOn)
            {
                ShellVisibility();
                Shield.RefreshCustomInfo();
            }
            return false;
        }
    }
}

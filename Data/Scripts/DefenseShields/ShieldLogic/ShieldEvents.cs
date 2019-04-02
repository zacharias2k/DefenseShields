namespace DefenseSystems
{
    using System;
    using System.Text;
    using Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Weapons;
    using VRage.Game.Entity;

    public partial class Controllers
    {
        internal void RegisterEvents(MyCubeGrid grid, bool register = true)
        {
            if (register)
            {
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyEntities.OnEntityAdd += OnEntityAdd;
                    MyEntities.OnEntityRemove += OnEntityRemove;
                }

                Shield.AppendingCustomInfo += AppendingCustomInfo;
                _sink.CurrentInputChanged += CurrentInputChanged;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

            }
            else
            {
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyEntities.OnEntityAdd -= OnEntityAdd;
                    MyEntities.OnEntityRemove -= OnEntityRemove;
                }
                BusEvents.OnCheckBus -= DefenseBus.NodeChange;
                BusEvents.OnBusSplit -= DefenseBus.BusSplit;
                Shield.AppendingCustomInfo -= AppendingCustomInfo;
                _sink.CurrentInputChanged -= CurrentInputChanged;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsWorking = myCubeBlock.IsWorking;
            IsFunctional = myCubeBlock.IsFunctional;
        }

        private void OnEntityAdd(MyEntity myEntity)
        {
            try
            {
                if (DsState.State.ReInforce) return;
                if (myEntity?.Physics == null || !myEntity.InScene || myEntity.MarkedForClose || myEntity is MyFloatingObject || myEntity is IMyEngineerToolBase) return;
                var isMissile = myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile);
                if (!isMissile && !(myEntity is MyCubeGrid)) return;

                var aabb = myEntity.PositionComp.WorldAABB;
                if (!ShieldBox3K.Intersects(ref aabb)) return;

                Asleep = false;
                if (_isServer && isMissile) Missiles.Add(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityAdd: {ex}"); }
        }

        private void OnEntityRemove(MyEntity myEntity)
        {
            try
            {
                if (myEntity == null || !_isServer || DsState.State.ReInforce) return;

                if (!(myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile))) return;

                Missiles.Remove(myEntity);
                FriendlyMissileCache.Remove(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityRemove: {ex}"); }
        }

        private string GetShieldStatus()
        {
            if (!DsState.State.Online && !MyCube.IsFunctional) return "[Controller Faulty]";
            if (!DsState.State.Online && !MyCube.IsWorking) return "[Controller Offline]";
            if (!DsState.State.Online && DsState.State.NoPower) return "[Insufficient Power]";
            if (!DsState.State.Online && DsState.State.Overload) return "[Overloaded]";
            if (!DsState.State.Online && DsState.State.EmpOverLoad) return "[Emp Overload]";
            if (!DsState.State.ControllerGridAccess) return "[Invalid Owner]";
            if (DsState.State.Waking) return "[Coming Online]";
            if (DsState.State.Suspended || DsState.State.Mode == 4) return "[Controller Standby]";
            if (DsState.State.Lowered) return "[Shield Down]";
            if (DsState.State.Sleeping) return "[Suspended]";
            if (!DsState.State.EmitterLos || DsState.State.ActiveEmitterId == 0) return "[Emitter Failure]";
            if (!DsState.State.Online) return "[Shield Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !DsState.State.Online ? 0f : 100f;

                if (DsState.State.Charge < ShieldMaxCharge) shieldPercent = DsState.State.Charge / ShieldMaxCharge * 100;
                if (_shieldChargeRate > 0)
                {
                    var toMax = ShieldMaxCharge - DsState.State.Charge;
                    var secs = toMax / _shieldChargeRate;
                    if (secs.Equals(1)) secToFull = 0;
                    else secToFull = (int)secs;
                }

                var shieldPowerNeeds = _powerNeeded;
                var powerUsage = shieldPowerNeeds;
                var gridMaxPower = GridMaxPower;
                var initStage = 1;
                var validEmitterId = DsState.State.ActiveEmitterId != 0;
                if (WarmedUp) initStage = 4;
                else if (Warming) initStage = 3;
                else if (_allInited) initStage = 2;

                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]")
                {
                    stringBuilder.Append(status + " MaxHP: " + (ShieldMaxCharge * ConvToHp).ToString("N0") +
                                         "\n" +
                                         "\n[Shield HP__]: " + (DsState.State.Charge * ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[HP Per Sec_]: " + (_shieldChargeRate * ConvToHp).ToString("N0") +
                                         "\n[Damage In__]: " + _damageReadOut.ToString("N0") +
                                         "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                         "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                         "\n[Over Heated]: " + DsState.State.Heat.ToString("0") + "%" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Power Usage]: " + powerUsage.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ")Mw" +
                                         "\n[Shield Power]: " + _sink.CurrentInputByType(GId).ToString("0.0") + " Mw");
                }
                else
                {


                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Init Stage]: " + initStage + " of 4" +
                                         "\n[Emitter Ok]: " + validEmitterId +
                                         "\n[HP Stored]: " + (DsState.State.Charge * ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Shield Mode]: " + ShieldMode +
                                         "\n[Emitter LoS]: " + (DsState.State.EmitterLos) +
                                         "\n[Last Woken]: " + LastWokenTick + "/" + _tick +
                                         "\n[Waking Up]: " + DsState.State.Waking +
                                         "\n[Grid Owns Controller]: " + DsState.State.IsOwner +
                                         "\n[In Grid's Faction]: " + DsState.State.InFaction);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}
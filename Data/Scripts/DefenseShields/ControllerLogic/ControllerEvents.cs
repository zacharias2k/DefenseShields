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
        internal void RegisterEvents(MyCubeGrid grid, Bus bus, bool register = true)
        {
            if (register)
            {
                bus.OnBusSplit += OnBusSplit;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyEntities.OnEntityAdd += OnEntityAdd;
                    MyEntities.OnEntityRemove += OnEntityRemove;
                }

                Controller.AppendingCustomInfo += AppendingCustomInfo;
                Sink.CurrentInputChanged += CurrentInputChanged;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);

            }
            else
            {
                bus.OnBusSplit -= OnBusSplit;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyEntities.OnEntityAdd -= OnEntityAdd;
                    MyEntities.OnEntityRemove -= OnEntityRemove;
                }
                Controller.AppendingCustomInfo -= AppendingCustomInfo;
                Sink.CurrentInputChanged -= CurrentInputChanged;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void OnBusSplit<T>(T type, Bus.LogicState state)
        {
            var grid = type as MyCubeGrid;
            if (grid == null) return;
            if (state == Bus.LogicState.Leave)
            {
                var onMyBus = Bus.SubGrids.Contains(grid);
                if (!onMyBus && Bus.ActiveController == null)
                {
                    IsAfterInited = false;
                    Bus.Inited = false;
                }
                Log.Line($"[cId:{MyCube.EntityId}] [Splitter - gId:{grid.EntityId} - bCnt:{grid.BlocksCount}] - [Receiver - gId:{MyCube.CubeGrid.EntityId} - OnMyBus:{onMyBus} - iMaster:{MyCube.CubeGrid == Bus.Spine} - mSize:{Bus.Spine.BlocksCount}]");
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
                if (State.Value.ProtectMode > 0) return;
                if (myEntity?.Physics == null || !myEntity.InScene || myEntity.MarkedForClose || myEntity is MyFloatingObject || myEntity is IMyEngineerToolBase) return;
                var isMissile = myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile);
                if (!isMissile && !(myEntity is MyCubeGrid)) return;

                var aabb = myEntity.PositionComp.WorldAABB;
                if (!Bus.Field.ShieldBox3K.Intersects(ref aabb)) return;

                Asleep = false;
                if (_isServer && isMissile) Bus.Field.Missiles.Add(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityAdd: {ex}"); }
        }

        private void OnEntityRemove(MyEntity myEntity)
        {
            try
            {
                if (myEntity == null || !_isServer || State.Value.ProtectMode > 0) return;

                if (!(myEntity.DefinitionId.HasValue && myEntity.DefinitionId.Value.TypeId == typeof(MyObjectBuilder_Missile))) return;

                Bus.Field.Missiles.Remove(myEntity);
                Bus.Field.FriendlyMissileCache.Remove(myEntity);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OnEntityRemove: {ex}"); }
        }

        internal string GetShieldStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Controller Faulty]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Controller Offline]";
            if (!State.Value.Online && State.Value.NoPower) return "[Insufficient Power]";
            if (!State.Value.Online && State.Value.Overload) return "[Overloaded]";
            if (!State.Value.Online && State.Value.EmpOverLoad) return "[Emp Overload]";
            if (!State.Value.ControllerGridAccess) return "[Invalid Owner]";
            if (State.Value.Waking) return "[Coming Online]";
            if (State.Value.Suspended || State.Value.Mode == 4) return "[Controller Standby]";
            if (State.Value.Lowered) return "[Shield Down]";
            if (State.Value.Sleeping) return "[Suspended]";
            if (!State.Value.EmitterLos || State.Value.ActiveEmitterId == 0) return "[Emitter Failure]";
            if (!State.Value.Online) return "[Shield Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !State.Value.Online ? 0f : 100f;

                if (State.Value.Charge < Bus.Field.ShieldMaxCharge) shieldPercent = State.Value.Charge / Bus.Field.ShieldMaxCharge * 100;
                if (Bus.Field.ShieldChargeRate > 0)
                {
                    var toMax = Bus.Field.ShieldMaxCharge - State.Value.Charge;
                    var secs = toMax / Bus.Field.ShieldChargeRate;
                    if (secs.Equals(1)) secToFull = 0;
                    else secToFull = (int)secs;
                }

                var shieldPowerNeeds = Bus.Field.PowerNeeds;
                var powerUsage = shieldPowerNeeds;
                var validEmitterId = State.Value.ActiveEmitterId != 0;

                var initStage = 1;
                var stage2 = _allInited;
                var stage3 = State.Value.Mode >= 0;
                if (WarmedUp && stage2 && stage3) initStage = 4;
                else if (stage2 && stage3) initStage = 3;
                else if (stage2) initStage = 2;
                const string maxString = " MaxHp: ";
                var hpValue = (Bus.Field.ShieldMaxCharge * Fields.ConvToHp);													

                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]" || status == "[Insufficient Power]")
                {
                    stringBuilder.Append(status + maxString + hpValue.ToString("N0") +
                                         "\n" +
                                         "\n[Shield HP__]: " + (State.Value.Charge * Fields.ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[HP Per Sec_]: " + (Bus.Field.ShieldChargeRate * Fields.ConvToHp).ToString("N0") +
                                         "\n[Damage In__]: " + Bus.Field.DamageReadOut.ToString("N0") +
                                         "\n[Charge Rate]: " + Bus.Field.ShieldChargeRate.ToString("0.0") + " Mw" +
                                         "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                         "\n[Over Heated]: " + State.Value.Heat.ToString("0") + "%" +
                                         "\n[Maintenance]: " + Bus.Field.ShieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Shield Power]: " + SinkCurrentPower.ToString("0.0") + " Mw" +
                                         "\n[Power Use]: " + powerUsage.ToString("0.0") + " (" + Bus.SpineMaxPower.ToString("0.0") + ")Mw");
                }
                else
                {


                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Init Stage]: " + initStage + " of 4" +
                                         "\n[Emitter Ok]: " + validEmitterId +
                                         "\n[HP Stored]: " + (State.Value.Charge * Fields.ConvToHp).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Emitter Mode]: " + Bus.EmitterMode +
                                         "\n[Emitter LoS]: " + (State.Value.EmitterLos) +
                                         "\n[Last Woken]: " + LastWokenTick + "/" + Bus.Tick +
                                         "\n[Waking Up]: " + State.Value.Waking +
                                         "\n[Grid Owns Controller]: " + State.Value.IsOwner +
                                         "\n[In Grid's Faction]: " + State.Value.InFaction);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}
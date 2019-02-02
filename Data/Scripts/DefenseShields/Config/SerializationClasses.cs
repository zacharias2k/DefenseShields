using System;
using System.ComponentModel;
using DefenseShields.Support;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    [ProtoContract]
    public class DefenseShieldsEnforcement
    {
        [ProtoMember(1), DefaultValue(-1)] public float HeatScaler = -1f;
        [ProtoMember(2), DefaultValue(-1)] public int BaseScaler = -1;
        [ProtoMember(3), DefaultValue(-1)] public float Unused = -1f;
        [ProtoMember(4), DefaultValue(-1)] public int StationRatio = -1;
        [ProtoMember(5), DefaultValue(-1)] public int LargeShipRatio = -1;
        [ProtoMember(6), DefaultValue(-1)] public int SmallShipRatio = -1;
        [ProtoMember(7), DefaultValue(-1)] public int DisableVoxelSupport = -1;
        [ProtoMember(8), DefaultValue(-1)] public int DisableEntityBarrier = -1;
        [ProtoMember(9), DefaultValue(-1)] public int Debug = -1;
        [ProtoMember(10)] public bool AltRecharge = false;
        [ProtoMember(11), DefaultValue(-1)] public int Version = -1;
        [ProtoMember(12)] public ulong SenderId = 0;
        [ProtoMember(13), DefaultValue(-1)] public float CapScaler = -1f;
        [ProtoMember(14), DefaultValue(-1)] public float HpsEfficiency = -1f;
        [ProtoMember(15), DefaultValue(-1)] public float MaintenanceCost = -1f;
        [ProtoMember(16), DefaultValue(-1)] public int DisableBlockDamage = -1;
        [ProtoMember(17), DefaultValue(-1)] public int DisableLineOfSight = -1;

        public override string ToString()
        {
            return "";
        }

    }

    [ProtoContract]
    public class ProtoControllerState
    {
        [ProtoMember(1), DefaultValue(-1)] public float Charge;
        [ProtoMember(2), DefaultValue(-1)] public double IncreaseO2ByFPercent = 0f;
        [ProtoMember(3), DefaultValue(1f)] public float ModulateEnergy = 1f;
        [ProtoMember(4), DefaultValue(1f)] public float ModulateKinetic = 1f;
        [ProtoMember(5), DefaultValue(-1)] public int EnhancerPowerMulti = 1;
        [ProtoMember(6), DefaultValue(-1)] public int EnhancerProtMulti = 1;
        [ProtoMember(7)] public bool Online = false;
        [ProtoMember(8)] public bool Overload = false;
        [ProtoMember(9)] public bool Remodulate = false;
        [ProtoMember(10)] public bool Lowered = false;
        [ProtoMember(11)] public bool Sleeping = false;
        [ProtoMember(12)] public bool Suspended = false;
        [ProtoMember(13)] public bool Waking = false;
        [ProtoMember(14)] public bool FieldBlocked = false;
        [ProtoMember(15)] public bool InFaction = false;
        [ProtoMember(16)] public bool IsOwner = false;
        [ProtoMember(17)] public bool ControllerGridAccess = true;
        [ProtoMember(18)] public bool NoPower = false;
        [ProtoMember(19)] public bool Enhancer = false;
        [ProtoMember(20), DefaultValue(-1)] public double EllipsoidAdjust = Math.Sqrt(2);
        [ProtoMember(21)] public Vector3D GridHalfExtents;
        [ProtoMember(22), DefaultValue(-1)] public int Mode = -1;
        [ProtoMember(23)] public bool EmitterWorking = false;
        [ProtoMember(24)] public float ShieldFudge;
        [ProtoMember(25)] public bool Message;
        [ProtoMember(26)] public int Heat;
        [ProtoMember(27), DefaultValue(-1)] public float ShieldPercent;
        [ProtoMember(28)] public bool EmpOverLoad = false;
        [ProtoMember(29)] public bool EmpProtection = false;
        [ProtoMember(30)] public float GridIntegrity;
        [ProtoMember(31)] public bool ReInforce = false;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoControllerSettings
    {
        [ProtoMember(1), DefaultValue(true)] public bool RefreshAnimation = true;
        [ProtoMember(2), DefaultValue(-1)] public float Width = 30f;
        [ProtoMember(3), DefaultValue(-1)] public float Height = 30f;
        [ProtoMember(4), DefaultValue(-1)] public float Depth = 30f;
        [ProtoMember(5)] public bool NoWarningSounds = false;
        [ProtoMember(6)] public bool ActiveInvisible = false;
        [ProtoMember(7), DefaultValue(-1)] public float Rate = 50f;
        [ProtoMember(8)] public bool ExtendFit = false;
        [ProtoMember(9)] public bool SphereFit = false;
        [ProtoMember(10)] public bool FortifyShield = false;
        [ProtoMember(11), DefaultValue(true)] public bool SendToHud = true;
        [ProtoMember(12), DefaultValue(true)] public bool UseBatteries = true;
        [ProtoMember(13), DefaultValue(true)] public bool DimShieldHits = true;
        [ProtoMember(14), DefaultValue(true)] public bool RaiseShield = true;
        [ProtoMember(15)] public long ShieldShell = 0;
        [ProtoMember(16), DefaultValue(true)] public bool HitWaveAnimation = true;
        [ProtoMember(17)] public long Visible = 0;
        [ProtoMember(18)] public Vector3I ShieldOffset = Vector3I.Zero;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoModulatorState
    {
        [ProtoMember(1)] public bool Online;
        [ProtoMember(2), DefaultValue(1f)] public float ModulateEnergy = 1f;
        [ProtoMember(3), DefaultValue(1f)] public float ModulateKinetic = 1f;
        [ProtoMember(4), DefaultValue(100)] public int ModulateDamage = 100;
        [ProtoMember(5)] public bool Backup;
        [ProtoMember(6)] public bool Link;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoModulatorSettings
    {
        [ProtoMember(1)] public bool EmpEnabled = false;
        [ProtoMember(2), DefaultValue(true)] public bool ModulateVoxels = true;
        [ProtoMember(3)] public bool ModulateGrids = false;
        [ProtoMember(4), DefaultValue(-1)] public int ModulateDamage = 100;
        [ProtoMember(5)] public bool ReInforceEnabled = false;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoPlanetShieldState
    {
        [ProtoMember(1)] public bool Online;
        [ProtoMember(2)] public bool Backup;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoPlanetShieldSettings
    {
        [ProtoMember(1)] public bool ShieldActive = false;
        [ProtoMember(2)] public long ShieldShell = 0;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoO2GeneratorState
    {
        [ProtoMember(1)] public bool Pressurized = false;
        [ProtoMember(2), DefaultValue(-1)] public float DefaultO2 = 0;
        [ProtoMember(3), DefaultValue(-1)] public double ShieldVolume = 0;
        [ProtoMember(4), DefaultValue(-1)] public double VolFilled = 0;
        [ProtoMember(5), DefaultValue(-1)] public double O2Level = 0;
        [ProtoMember(6)] public bool Backup = false;

        public override string ToString()
        {
            return $"";

        }
    }

    [ProtoContract]
    public class ProtoO2GeneratorSettings
    {
        [ProtoMember(1)] public bool FixRoomPressure;
        [ProtoMember(2), DefaultValue(true)] public bool Unused2 = true;
        [ProtoMember(3)] public bool Unused3 = false;
        [ProtoMember(4), DefaultValue(-1)] public int Unused4 = 100;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerState
    {
        [ProtoMember(1)] public bool Online;
        [ProtoMember(2)] public bool Backup;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerSettings
    {
        [ProtoMember(1)] public bool Unused;
        [ProtoMember(2), DefaultValue(true)] public bool ModulateVoxels = true;
        [ProtoMember(3)] public bool ModulateGrids = false;
        [ProtoMember(4), DefaultValue(-1)] public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEmitterState
    {
        [ProtoMember(1)] public bool Online;
        [ProtoMember(2), DefaultValue(true)] public bool Los = true;
        [ProtoMember(3)] public bool Link;
        [ProtoMember(4)] public bool Suspend;
        [ProtoMember(5)] public bool Backup;
        [ProtoMember(6)] public bool Compatible;
        [ProtoMember(7), DefaultValue(-1)] public int Mode;
        [ProtoMember(8), DefaultValue(-1)] public double BoundingRange;
        [ProtoMember(9)] public bool Compact;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoShieldHit
    {
        [ProtoMember(1)] public long AttackerId;
        [ProtoMember(2)] public float Amount;
        [ProtoMember(3)] public string DamageType;
        [ProtoMember(4)] public Vector3D HitPos;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoInclude(3, typeof(DataControllerState))]
    [ProtoInclude(4, typeof(DataControllerSettings))]
    [ProtoInclude(5, typeof(DataModulatorState))]
    [ProtoInclude(6, typeof(DataModulatorSettings))]
    [ProtoInclude(7, typeof(DataPlanetShieldState))]
    [ProtoInclude(8, typeof(DataPlanetShieldSettings))]
    [ProtoInclude(9, typeof(DataO2GeneratorState))]
    [ProtoInclude(10, typeof(DataO2GeneratorSettings))]
    [ProtoInclude(11, typeof(DataEnhancerState))]
    [ProtoInclude(12, typeof(DataEnhancerSettings))]
    [ProtoInclude(13, typeof(DataEmitterState))]
    [ProtoInclude(14, typeof(DataShieldHit))]
    [ProtoInclude(15, typeof(DataEnforce))]

    [ProtoContract]
    public abstract class PacketBase
    {
        [ProtoMember(1)] public ulong SenderId;

        [ProtoMember(2)] public long EntityId;

        private MyEntity _ent;

        internal MyEntity Entity
        {
            get
            {
                if (EntityId == 0) return null;

                if (_ent == null) _ent = MyEntities.GetEntityById(EntityId, true);

                if (_ent == null || _ent.MarkedForClose) return null;
                return _ent;
            }
        }

        public PacketBase(long entityId = 0)
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
            EntityId = entityId;
        }

        /// <summary>
        /// Called when this packet is received on this machine
        /// </summary>
        /// <param name="rawData">the bytes from the packet, useful for relaying or other stuff without needing to re-serialize the packet</param>
        public abstract bool Received(bool isServer);
    }

    [ProtoContract]
    public class DataControllerState : PacketBase
    {
        public DataControllerState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoControllerState State = null;

        public DataControllerState(long entityId, ProtoControllerState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<DefenseShields>();
                logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataControllerSettings : PacketBase
    {
        public DataControllerSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoControllerSettings Settings = null;

        public DataControllerSettings(long entityId, ProtoControllerSettings settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<DefenseShields>();
            logic?.UpdateSettings(Settings);
            return isServer;
        }
    }

    [ProtoContract]
    public class DataModulatorState : PacketBase
    {
        public DataModulatorState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoModulatorState State = null;

        public DataModulatorState(long entityId, ProtoModulatorState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<Modulators>();
                logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataModulatorSettings : PacketBase
    {
        public DataModulatorSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoModulatorSettings Settings = null;

        public DataModulatorSettings(long entityId, ProtoModulatorSettings settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<Modulators>();
            logic?.UpdateSettings(Settings);
            return isServer;
        }
    }

    [ProtoContract]
    public class DataPlanetShieldState : PacketBase
    {
        public DataPlanetShieldState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoPlanetShieldState State = null;

        public DataPlanetShieldState(long entityId, ProtoPlanetShieldState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<PlanetShields>();
                //logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataPlanetShieldSettings : PacketBase
    {
        public DataPlanetShieldSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoPlanetShieldSettings Settings = null;

        public DataPlanetShieldSettings(long entityId, ProtoPlanetShieldSettings settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<PlanetShields>();
            //logic?.UpdateSettings(Settings);
            return isServer;
        }
    }

    [ProtoContract]
    public class DataO2GeneratorState : PacketBase
    {
        public DataO2GeneratorState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoO2GeneratorState State = null;

        public DataO2GeneratorState(long entityId, ProtoO2GeneratorState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<O2Generators>();
                logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataO2GeneratorSettings : PacketBase
    {
        public DataO2GeneratorSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoO2GeneratorSettings Settings = null;

        public DataO2GeneratorSettings(long entityId, ProtoO2GeneratorSettings settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<O2Generators>();
            logic?.UpdateSettings(Settings);
            return isServer;
        }
    }

    [ProtoContract]
    public class DataEnhancerState : PacketBase
    {
        public DataEnhancerState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoEnhancerState State = null;

        public DataEnhancerState(long entityId, ProtoEnhancerState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<Enhancers>();
                logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataEnhancerSettings : PacketBase
    {
        public DataEnhancerSettings()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoEnhancerSettings Settings = null;

        public DataEnhancerSettings(long entityId, ProtoEnhancerSettings settings) : base(entityId)
        {
            Settings = settings;
        }

        public override bool Received(bool isServer)
        {
            if (Entity?.GameLogic == null) return false;
            var logic = Entity.GameLogic.GetAs<Enhancers>();
            //logic?.UpdateSettings(Settings);
            return isServer;
        }
    }

    [ProtoContract]
    public class DataEmitterState : PacketBase
    {
        public DataEmitterState()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoEmitterState State = null;

        public DataEmitterState(long entityId, ProtoEmitterState state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                if (Entity?.GameLogic == null) return false;
                var logic = Entity.GameLogic.GetAs<Emitters>();
                logic?.UpdateState(State);
                return false;
            }
            return true;
        }
    }

    [ProtoContract]
    public class DataShieldHit : PacketBase
    {
        public DataShieldHit()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public ProtoShieldHit State = null;

        public DataShieldHit(long entityId, ProtoShieldHit state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (isServer || Entity?.GameLogic == null) return false;
            var shield = Entity.GameLogic.GetAs<DefenseShields>();
            if (shield == null) return false;

            var attacker = MyEntities.GetEntityById(State.AttackerId);
            shield.ShieldHits.Add(new ShieldHit(attacker, State.Amount, MyStringHash.GetOrCompute(State.DamageType), State.HitPos));
            return false;
        }
    }

    [ProtoContract]
    public class DataEnforce : PacketBase
    {
        public DataEnforce()
        {
        } // Empty constructor required for deserialization

        [ProtoMember(1)] public DefenseShieldsEnforcement State = null;

        public DataEnforce(long entityId, DefenseShieldsEnforcement state) : base(entityId)
        {
            State = state;
        }

        public override bool Received(bool isServer)
        {
            if (!isServer)
            {
                var logic = Entity?.GameLogic?.GetAs<DefenseShields>();
                if (logic == null) return false;
                Log.Line($"Saving Enforcement");
                Enforcements.SaveEnforcement(logic.Shield, State);
                Session.EnforceInit = true;
                return false;
            }
            Log.Line($"Sending Enforcement");
            var data = new DataEnforce(EntityId, State);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageTo(Session.PACKET_ID, bytes, SenderId);
            return false;
        }
    }
}

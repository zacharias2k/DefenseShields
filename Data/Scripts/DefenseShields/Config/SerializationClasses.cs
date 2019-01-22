using System;
using System.ComponentModel;
using ProtoBuf;
using VRageMath;

namespace DefenseShields
{
    [ProtoContract]
    public class DefenseShieldsEnforcement
    {
        [ProtoMember(1), DefaultValue(-1)]
        public float HeatScaler = -1f;

        [ProtoMember(2), DefaultValue(-1)]
        public int BaseScaler = -1;

        [ProtoMember(3), DefaultValue(-1)]
        public float Unused = -1f;

        [ProtoMember(4), DefaultValue(-1)]
        public int StationRatio = -1;

        [ProtoMember(5), DefaultValue(-1)]
        public int LargeShipRatio = -1;

        [ProtoMember(6), DefaultValue(-1)]
        public int SmallShipRatio = -1;

        [ProtoMember(7), DefaultValue(-1)]
        public int DisableVoxelSupport = -1;

        [ProtoMember(8), DefaultValue(-1)]
        public int DisableEntityBarrier = -1;

        [ProtoMember(9), DefaultValue(-1)]
        public int Debug = -1;

        [ProtoMember(10)]
        public bool AltRecharge = false;

        [ProtoMember(11), DefaultValue(-1)]
        public int Version = -1;

        [ProtoMember(12)]
        public ulong SenderId = 0;

        [ProtoMember(13), DefaultValue(-1)]
        public float CapScaler = -1f;

        [ProtoMember(14), DefaultValue(-1)]
        public float HpsEfficiency = -1f;

        [ProtoMember(15), DefaultValue(-1)]
        public float MaintenanceCost = -1f;

        [ProtoMember(16), DefaultValue(-1)]
        public int DisableBlockDamage = -1;

        [ProtoMember(17), DefaultValue(-1)]
        public int DisableLineOfSight = -1;

        public override string ToString()
        {
            return "";
        }

    }

    [ProtoContract]
    public class ProtoControllerState
    {
        [ProtoMember(1), DefaultValue(-1)]
        public float Charge;
        [ProtoMember(2), DefaultValue(-1)]
        public double IncreaseO2ByFPercent = 0f;
        [ProtoMember(3), DefaultValue(1f)]
        public float ModulateEnergy = 1f;
        [ProtoMember(4), DefaultValue(1f)]
        public float ModulateKinetic = 1f;
        [ProtoMember(5), DefaultValue(-1)]
        public int EnhancerPowerMulti = 1;
        [ProtoMember(6), DefaultValue(-1)]
        public int EnhancerProtMulti = 1;
        [ProtoMember(7)]
        public bool Online = false;
        [ProtoMember(8)]
        public bool Overload = false;
        [ProtoMember(9)]
        public bool Remodulate = false;
        [ProtoMember(10)]
        public bool Lowered = false;
        [ProtoMember(11)]
        public bool Sleeping = false;
        [ProtoMember(12)]
        public bool Suspended = false;
        [ProtoMember(13)]
        public bool Waking = false;
        [ProtoMember(14)]
        public bool FieldBlocked = false;
        [ProtoMember(15)]
        public bool InFaction = false;
        [ProtoMember(16)]
        public bool IsOwner = false;
        [ProtoMember(17)]
        public bool ControllerGridAccess = true;
        [ProtoMember(18)]
        public bool NoPower = false;
        [ProtoMember(19)]
        public bool Enhancer = false;
        [ProtoMember(20), DefaultValue(-1)]
        public double EllipsoidAdjust = Math.Sqrt(2);
        [ProtoMember(21)]
        public Vector3D GridHalfExtents;
        [ProtoMember(22), DefaultValue(-1)]
        public int Mode = -1;
        [ProtoMember(23)]
        public bool EmitterWorking = false;
        [ProtoMember(24)]
        public float ShieldFudge;
        [ProtoMember(25)]
        public bool Message;
        [ProtoMember(26)]
        public int Heat;
        [ProtoMember(27), DefaultValue(-1)]
        public float ShieldPercent;
        [ProtoMember(28)]
        public bool EmpOverLoad = false;
        [ProtoMember(29)]
        public bool EmpProtection = false;
        [ProtoMember(30)]
        public float GridIntegrity;
        [ProtoMember(31)]
        public bool ReInforce = false;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoControllerSettings
    {
        [ProtoMember(1), DefaultValue(true)]
        public bool RefreshAnimation = true;

        [ProtoMember(2), DefaultValue(-1)]
        public float Width = 30f;

        [ProtoMember(3), DefaultValue(-1)]
        public float Height = 30f;

        [ProtoMember(4), DefaultValue(-1)]
        public float Depth = 30f;

        [ProtoMember(5)]
        public bool Unused = false;

        [ProtoMember(6)]
        public bool ActiveInvisible = false;

        [ProtoMember(7), DefaultValue(-1)]
        public float Rate = 50f;

        [ProtoMember(8)]
        public bool ExtendFit = false;

        [ProtoMember(9)]
        public bool SphereFit = false;

        [ProtoMember(10)]
        public bool FortifyShield = false;

        [ProtoMember(11), DefaultValue(true)]
        public bool SendToHud = true;

        [ProtoMember(12), DefaultValue(true)]
        public bool UseBatteries = true;

        [ProtoMember(13)]
        public bool Unused2 = false;

        [ProtoMember(14), DefaultValue(true)]
        public bool RaiseShield = true;

        [ProtoMember(15)]
        public long ShieldShell = 0;

        [ProtoMember(16), DefaultValue(true)]
        public bool HitWaveAnimation = true;

        [ProtoMember(17)]
        public long Visible = 0;

        [ProtoMember(18)]
        public Vector3I ShieldOffset = Vector3I.Zero;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoModulatorState
    {
        [ProtoMember(1)]
        public bool Online;

        [ProtoMember(2), DefaultValue(1f)]
        public float ModulateEnergy = 1f;

        [ProtoMember(3), DefaultValue(1f)]
        public float ModulateKinetic = 1f;

        [ProtoMember(4), DefaultValue(100)]
        public int ModulateDamage = 100;

        [ProtoMember(5)]
        public bool Backup;

        [ProtoMember(6)]
        public bool Link;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoModulatorSettings
    {
        [ProtoMember(1)]
        public bool EmpEnabled = false;

        [ProtoMember(2), DefaultValue(true)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        [ProtoMember(5)]
        public bool ReInforceEnabled = false;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoPlanetShieldState
    {
        [ProtoMember(1)]
        public bool Online;

        [ProtoMember(2)]
        public bool Backup;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoPlanetShieldSettings
    {
        [ProtoMember(1)]
        public bool ShieldActive = false;

        public override string ToString()
        {
            return "";
        }
    }

    [ProtoContract]
    public class ProtoO2GeneratorState
    {
        [ProtoMember(1)]
        public bool Pressurized = false;

        [ProtoMember(2), DefaultValue(-1)]
        public float DefaultO2 = 0;

        [ProtoMember(3), DefaultValue(-1)]
        public double ShieldVolume = 0;

        [ProtoMember(4), DefaultValue(-1)]
        public double VolFilled = 0;

        [ProtoMember(5), DefaultValue(-1)]
        public double O2Level = 0;

        [ProtoMember(6)]
        public bool Backup = false;

        public override string ToString()
        {
            return $"";

        }
    }

    [ProtoContract]
    public class ProtoO2GeneratorSettings
    {
        [ProtoMember(1)]
        public bool FixRoomPressure;

        [ProtoMember(2), DefaultValue(true)]
        public bool Unused2 = true;

        [ProtoMember(3)]
        public bool Unused3 = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int Unused4 = 100;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerState
    {
        [ProtoMember(1)]
        public bool Online;

        [ProtoMember(2)]
        public bool Backup;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerSettings
    {
        [ProtoMember(1)]
        public bool Unused;

        [ProtoMember(2), DefaultValue(true)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoEmitterState
    {
        [ProtoMember(1)]
        public bool Online;

        [ProtoMember(2), DefaultValue(true)]
        public bool Los = true;

        [ProtoMember(3)]
        public bool Link;

        [ProtoMember(4)]
        public bool Suspend;

        [ProtoMember(5)]
        public bool Backup;

        [ProtoMember(6)]
        public bool Compatible;

        [ProtoMember(7), DefaultValue(-1)]
        public int Mode;

        [ProtoMember(8), DefaultValue(-1)]
        public double BoundingRange;

        [ProtoMember(9)]
        public bool Compact;

        public override string ToString()
        {
            return $"";
        }
    }

    [ProtoContract]
    public class ProtoShieldHit
    {
        [ProtoMember(1)]
        public long AttackerId;

        [ProtoMember(2)]
        public float Amount;

        [ProtoMember(3)]
        public string DamageType;

        [ProtoMember(4)]
        public Vector3D HitPos;

        public override string ToString()
        {
            return $"";
        }

    }

    [ProtoContract]
    public class DataEnforce
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Enforce;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public DefenseShieldsEnforcement Enforce = null;

        public DataEnforce() { } // empty ctor is required for deserialization

        public DataEnforce(ulong sender, long entityId, DefenseShieldsEnforcement enforce)
        {
            Type = PacketType.Enforce;
            Sender = sender;
            EntityId = entityId;
            Enforce = enforce;
        }

        public DataEnforce(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Enforce = null;
        }
    }

    [ProtoContract]
    public class DataControllerState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Controllerstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoControllerState State = null;

        public DataControllerState() { } // empty ctor is required for deserialization

        public DataControllerState(ulong sender, long entityId, ProtoControllerState state)
        {
            Type = PacketType.Controllerstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataControllerState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataControllerSettings
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Controllersettings;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoControllerSettings Settings = null;

        public DataControllerSettings() { } // empty ctor is required for deserialization

        public DataControllerSettings(ulong sender, long entityId, ProtoControllerSettings settings)
        {
            Type = PacketType.Controllersettings;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public DataControllerSettings(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class DataModulatorState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Modulatorstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoModulatorState State = null;

        public DataModulatorState() { } // empty ctor is required for deserialization

        public DataModulatorState(ulong sender, long entityId, ProtoModulatorState state)
        {
            Type = PacketType.Modulatorstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataModulatorState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataModulatorSettings
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Modulatorsettings;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoModulatorSettings Settings = null;

        public DataModulatorSettings() { } // empty ctor is required for deserialization

        public DataModulatorSettings(ulong sender, long entityId, ProtoModulatorSettings settings)
        {
            Type = PacketType.Modulatorsettings;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public DataModulatorSettings(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class DataPlanetShieldState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.PlanetShieldstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoPlanetShieldState State = null;

        public DataPlanetShieldState() { } // empty ctor is required for deserialization

        public DataPlanetShieldState(ulong sender, long entityId, ProtoPlanetShieldState state)
        {
            Type = PacketType.PlanetShieldstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataPlanetShieldState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataPlanetShieldSettings
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.PlanetShieldsettings;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoPlanetShieldSettings Settings = null;

        public DataPlanetShieldSettings() { } // empty ctor is required for deserialization

        public DataPlanetShieldSettings(ulong sender, long entityId, ProtoPlanetShieldSettings settings)
        {
            Type = PacketType.PlanetShieldsettings;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public DataPlanetShieldSettings(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class DataO2GeneratorState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.O2Generatorstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoO2GeneratorState State = null;

        public DataO2GeneratorState() { } // empty ctor is required for deserialization

        public DataO2GeneratorState(ulong sender, long entityId, ProtoO2GeneratorState state)
        {
            Type = PacketType.O2Generatorstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataO2GeneratorState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataO2GeneratorSettings
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.O2Generatorsettings;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoO2GeneratorSettings Settings = null;

        public DataO2GeneratorSettings() { } // empty ctor is required for deserialization

        public DataO2GeneratorSettings(ulong sender, long entityId, ProtoO2GeneratorSettings settings)
        {
            Type = PacketType.O2Generatorsettings;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public DataO2GeneratorSettings(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class DataEnhancerState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Enhancerstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoEnhancerState State = null;

        public DataEnhancerState() { } // empty ctor is required for deserialization

        public DataEnhancerState(ulong sender, long entityId, ProtoEnhancerState state)
        {
            Type = PacketType.Enhancerstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataEnhancerState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataEnhancerSettings
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Enhancersettings;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoEnhancerSettings Settings = null;

        public DataEnhancerSettings() { } // empty ctor is required for deserialization

        public DataEnhancerSettings(ulong sender, long entityId, ProtoEnhancerSettings settings)
        {
            Type = PacketType.Enhancersettings;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public DataEnhancerSettings(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class DataEmitterState
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Emitterstate;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoEmitterState State = null;

        public DataEmitterState() { } // empty ctor is required for deserialization

        public DataEmitterState(ulong sender, long entityId, ProtoEmitterState state)
        {
            Type = PacketType.Emitterstate;
            Sender = sender;
            EntityId = entityId;
            State = state;
        }

        public DataEmitterState(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            State = null;
        }
    }

    [ProtoContract]
    public class DataShieldHit
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.Shieldhit;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ProtoShieldHit ShieldHit = null;

        public DataShieldHit() { } // empty ctor is required for deserialization

        public DataShieldHit(ulong sender, long entityId, ProtoShieldHit shieldHit)
        {
            Type = PacketType.Shieldhit;
            Sender = sender;
            EntityId = entityId;
            ShieldHit = shieldHit;
        }

        public DataShieldHit(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            ShieldHit = null;
        }
    }

    public enum PacketType : byte
    {
        Enforce,
        Controllerstate,
        Controllersettings,
        Modulatorsettings,
        Modulatorstate,
        PlanetShieldsettings,
        PlanetShieldstate,
        O2Generatorsettings,
        O2Generatorstate,
        Enhancersettings,
        Enhancerstate,
        Emitterstate,
        Shieldhit
    }
}

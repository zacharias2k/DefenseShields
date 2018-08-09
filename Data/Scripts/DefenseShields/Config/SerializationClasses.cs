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
        public float Nerf = -1f;

        [ProtoMember(2), DefaultValue(-1)]
        public int BaseScaler = -1;

        [ProtoMember(3), DefaultValue(-1)]
        public float Efficiency = -1f;

        [ProtoMember(4), DefaultValue(-1)]
        public int StationRatio = -1;

        [ProtoMember(5), DefaultValue(-1)]
        public int LargeShipRatio = -1;

        [ProtoMember(6), DefaultValue(-1)]
        public int SmallShipRatio = -1;

        [ProtoMember(7), DefaultValue(-1)]
        public int DisableVoxelSupport = -1;

        [ProtoMember(8), DefaultValue(-1)]
        public int DisableGridDamageSupport = -1;

        [ProtoMember(9), DefaultValue(-1)]
        public int Debug = -1;

        [ProtoMember(10)]
        public bool AltRecharge = false;

        [ProtoMember(11), DefaultValue(-1)]
        public int Version = -1;

        [ProtoMember(12)]
        public ulong SenderId = 0;

        public override string ToString()
        {
            return $"Nerf = {Math.Round(Nerf, 4)}\nBaseScaler = {BaseScaler}\nEfficiency = {Math.Round(Efficiency, 4)}\nStationRatio = {StationRatio}\nLargeShipRatio = {LargeShipRatio}" +
                   $"\nSmallShipRatio = {SmallShipRatio}\nDisableVoxelSupport = {DisableVoxelSupport}\nDisableGridDamageSupport = {DisableGridDamageSupport}" +
                   $"\nDebug = {Debug}\nAltRecharge = {AltRecharge}\nVersion = {Version}\nSenderId = {SenderId}";
        }

    }

    [ProtoContract]
    public class ProtoControllerState
    {
        [ProtoMember(1), DefaultValue(-1)]
        public float Buffer;
        [ProtoMember(2), DefaultValue(-1)]
        public double IncreaseO2ByFPercent = 0f;
        [ProtoMember(3), DefaultValue(-1)]
        public float ModulateEnergy = 1f;
        [ProtoMember(4), DefaultValue(-1)]
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
        [ProtoMember(22)]
        public int Mode = 4;
        [ProtoMember(23)]
        public bool EmitterWorking = false;

        public override string ToString()
        {
            return $"Buffer = {Math.Round(Buffer, 4)}";
        }
    }

    [ProtoContract]
    public class ProtoControllerSettings
    {
        [ProtoMember(1)]
        public bool Enabled = false;

        [ProtoMember(2), DefaultValue(-1)]
        public float Width = 30f;

        [ProtoMember(3), DefaultValue(-1)]
        public float Height = 30f;

        [ProtoMember(4), DefaultValue(-1)]
        public float Depth = 30f;

        [ProtoMember(5)]
        public bool PassiveInvisible = false;

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

        [ProtoMember(11)]
        public bool SendToHud = true;

        [ProtoMember(12)]
        public bool UseBatteries = true;

        [ProtoMember(13)]
        public bool ShieldActive = false;

        [ProtoMember(14)]
        public bool RaiseShield = true;

        [ProtoMember(15)]
        public long ShieldShell = 0;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nIdleVisible = {PassiveInvisible}\nActiveVisible = {ActiveInvisible}\nWidth = {Math.Round(Width, 4)}" +
                   $"\nHeight = {Math.Round(Height, 4)}\nDepth = {Math.Round(Depth, 4)}\nRate = {Math.Round(Rate, 4)}" +
                   $"\nExtendFit = {ExtendFit}\nSphereFit = {SphereFit}" +
                   $"\nFortifyShield = {FortifyShield}\nSendToHud = {SendToHud}\nUseBatteries = {UseBatteries}" +
                   $"\nShieldActive = {ShieldActive}\nRaiseShield = {RaiseShield}\n ShieldShell = {ShieldShell}";
        }
    }

    [ProtoContract]
    public class ProtoModulatorState
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
        }
    }

    [ProtoContract]
    public class ProtoModulatorSettings
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
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

        public override string ToString()
        {
            return $"";

        }
    }

    [ProtoContract]
    public class ProtoO2GeneratorSettings
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerState
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
        }
    }

    [ProtoContract]
    public class ProtoEnhancerSettings
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public bool ModulateVoxels = true;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = 100;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
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
            Type = PacketType.Modulatorsettings;
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

    public enum PacketType : byte
    {
        Enforce,
        Controllerstate,
        Controllersettings,
        Modulatorsettings,
        Modulatorstate,
        O2Generatorsettings,
        O2Generatorstate,
        Enhancersettings,
        Enhancerstate,
    }
}

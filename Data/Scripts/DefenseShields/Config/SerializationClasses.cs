using System;
using System.ComponentModel;
using ProtoBuf;

namespace DefenseShields
{
    [ProtoContract]
    public class DisplaySettings
    {
        [ProtoMember(1)]
        public bool Enabled = false;

        [ProtoMember(2)]
        public bool ModulateVoxels = false;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}";
        }
    }

    [ProtoContract]
    public class DefenseShieldsModSettings
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

        [ProtoMember(8), DefaultValue(-1)]
        public float Buffer = 0f;

        [ProtoMember(9)]
        public bool ModulateVoxels = true;

        [ProtoMember(10)]
        public bool ModulateGrids = false;

        [ProtoMember(11)]
        public bool ExtendFit = false;

        [ProtoMember(12)]
        public bool SphereFit = false;

        [ProtoMember(13)]
        public bool FortifyShield = false;

        [ProtoMember(14)]
        public bool SendToHud = true;

        [ProtoMember(15)]
        public bool UseBatteries = true;

        [ProtoMember(16), DefaultValue(-1)]
        public double IncreaseO2ByFPercent = 0f;

        [ProtoMember(17)]
        public bool ShieldActive = false;

        [ProtoMember(18)]
        public bool RaiseShield = false;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nIdleVisible = {PassiveInvisible}\nActiveVisible = {ActiveInvisible}\nWidth = {Math.Round(Width, 4)}" +
                   $"\nHeight = {Math.Round(Height, 4)}\nDepth = {Math.Round(Depth, 4)}\nRate = {Math.Round(Rate, 4)}" +
                   $"\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nExtendFit = {ExtendFit}\nSphereFit = {SphereFit}" +
                   $"\nFortifyShield = {FortifyShield}\nSendToHud = {SendToHud}\nUseBatteries = {UseBatteries}\nIncreaseO2ByFPercent = {Math.Round(IncreaseO2ByFPercent, 4)}" +
                   $"\nShieldActive = {ShieldActive}\nRaiseShield = {RaiseShield}";
        }
    }

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
    public class ModulatorSettings
    {
        [ProtoMember(1)]
        public bool Enabled = false;

        [ProtoMember(2)]
        public bool ModulateVoxels = false;

        [ProtoMember(3)]
        public bool ModulateGrids = false;

        [ProtoMember(4), DefaultValue(-1)]
        public int ModulateDamage = -1;

        public override string ToString()
        {
            return $"Enabled = {Enabled}\nModulateVoxels = {ModulateVoxels}\nModulateGrids = {ModulateGrids}\nModulateDamage = {ModulateDamage}";
        }
    }

    [ProtoContract]
    public class PacketData
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.SETTINGS;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public DefenseShieldsModSettings Settings = null;

        public PacketData() { } // empty ctor is required for deserialization

        public PacketData(ulong sender, long entityId, DefenseShieldsModSettings settings)
        {
            Type = PacketType.SETTINGS;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public PacketData(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }

    [ProtoContract]
    public class EnforceData
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.ENFORCE;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public DefenseShieldsEnforcement Enforce = null;

        public EnforceData() { } // empty ctor is required for deserialization

        public EnforceData(ulong sender, long entityId, DefenseShieldsEnforcement enforce)
        {
            Type = PacketType.ENFORCE;
            Sender = sender;
            EntityId = entityId;
            Enforce = enforce;
        }

        public EnforceData(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Enforce = null;
        }
    }

    [ProtoContract]
    public class ModulatorData
    {
        [ProtoMember(1)]
        public PacketType Type = PacketType.MODULATOR;

        [ProtoMember(2)]
        public long EntityId = 0;

        [ProtoMember(3)]
        public ulong Sender = 0;

        [ProtoMember(4)]
        public ModulatorSettings Settings = null;

        public ModulatorData() { } // empty ctor is required for deserialization

        public ModulatorData(ulong sender, long entityId, ModulatorSettings settings)
        {
            Type = PacketType.MODULATOR;
            Sender = sender;
            EntityId = entityId;
            Settings = settings;
        }

        public ModulatorData(ulong sender, long entityId, PacketType action)
        {
            Type = action;
            Sender = sender;
            EntityId = entityId;
            Settings = null;
        }
    }
    public enum PacketType : byte
    {
        SETTINGS,
        ENFORCE,
        MODULATOR,
    }
}

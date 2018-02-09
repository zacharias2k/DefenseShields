using System;
using ProtoBuf;

namespace DefenseShields
{
    /// Used for serializing the settings.
    [ProtoContract]
    public class DefenseShieldsModSettings
    {
        [ProtoMember(1)]
        public bool Enabled = false;

        [ProtoMember(2)]
        public float Width = -1;

        [ProtoMember(3)]
        public float Height = -1;

        [ProtoMember(4)]
        public float Depth = -1;


        public override string ToString()
        {
            return $"Enabled = {Enabled}\nWidth = {Math.Round(Width, 4)}\nHeight = {Math.Round(Height, 4)}\nDepth = {Math.Round(Depth, 4)}";
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

    public enum PacketType : byte
    {
        SETTINGS,
        //REMOVE,
        //RECEIVED_BP,
        USE_THIS_AS_IS,
        USE_THIS_FIX,
    }
}

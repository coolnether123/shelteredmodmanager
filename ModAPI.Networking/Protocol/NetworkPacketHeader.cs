using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Protocol
{
    public struct NetworkPacketHeader
    {
        public ushort Magic;
        public byte Version;
        public PacketFlags Flags;
        public ushort Sequence;
        public ushort Ack;
        public uint AckBits;
        public byte MessageCount;

        public static NetworkPacketHeader Create(ushort sequence, ushort ack, uint ackBits, PacketFlags flags, byte messageCount)
        {
            NetworkPacketHeader header = new NetworkPacketHeader();
            header.Magic = NetworkDefaults.PacketMagic;
            header.Version = NetworkDefaults.ProtocolVersion;
            header.Flags = flags;
            header.Sequence = sequence;
            header.Ack = ack;
            header.AckBits = ackBits;
            header.MessageCount = messageCount;
            return header;
        }

        public void WriteTo(ref BitWriter writer)
        {
            writer.WriteUInt16(Magic);
            writer.WriteByte(Version);
            writer.WriteByte((byte)Flags);
            writer.WriteUInt16(Sequence);
            writer.WriteUInt16(Ack);
            writer.WriteUInt32(AckBits);
            writer.WriteByte(MessageCount);
        }

        public static NetworkPacketHeader ReadFrom(ref BitReader reader)
        {
            NetworkPacketHeader header = new NetworkPacketHeader();
            header.Magic = reader.ReadUInt16();
            header.Version = reader.ReadByte();
            header.Flags = (PacketFlags)reader.ReadByte();
            header.Sequence = reader.ReadUInt16();
            header.Ack = reader.ReadUInt16();
            header.AckBits = reader.ReadUInt32();
            header.MessageCount = reader.ReadByte();
            return header;
        }

        public bool IsValid
        {
            get { return Magic == NetworkDefaults.PacketMagic && Version == NetworkDefaults.ProtocolVersion; }
        }
    }
}

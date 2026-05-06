using System;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Snapshots
{
    public sealed class NetworkSnapshotChunk
    {
        public string TransferId = string.Empty;
        public ushort ChunkIndex;
        public ushort ChunkCount;
        public int TotalLength;
        public byte[] Payload = new byte[0];

        public void WriteTo(ref BitWriter writer)
        {
            Validate();
            writer.WriteString(TransferId);
            writer.WriteUInt16(ChunkIndex);
            writer.WriteUInt16(ChunkCount);
            writer.WriteInt32(TotalLength);
            writer.WriteUInt16((ushort)(Payload != null ? Payload.Length : 0));
            if (Payload != null && Payload.Length > 0)
                writer.WriteBytes(Payload, 0, Payload.Length);
        }

        public static NetworkSnapshotChunk ReadFrom(ref BitReader reader)
        {
            NetworkSnapshotChunk chunk = new NetworkSnapshotChunk();
            chunk.TransferId = reader.ReadString();
            chunk.ChunkIndex = reader.ReadUInt16();
            chunk.ChunkCount = reader.ReadUInt16();
            chunk.TotalLength = reader.ReadInt32();
            ushort payloadLength = reader.ReadUInt16();
            chunk.Payload = new byte[payloadLength];
            if (payloadLength > 0)
                reader.ReadBytes(chunk.Payload, 0, payloadLength);
            chunk.Validate();
            return chunk;
        }

        public void Validate()
        {
            if (TransferId == null)
                TransferId = string.Empty;
            if (TransferId.Length == 0)
                throw new InvalidOperationException("Snapshot transfer id is required.");
            if (TransferId.Length > NetworkDefaults.MaxHandshakeStringLength)
                throw new InvalidOperationException("Snapshot transfer id is too long.");
            if (ChunkCount == 0)
                throw new InvalidOperationException("Snapshot chunk count must be greater than zero.");
            if (ChunkIndex >= ChunkCount)
                throw new InvalidOperationException("Snapshot chunk index is outside the transfer.");
            if (TotalLength < 0)
                throw new InvalidOperationException("Snapshot total length cannot be negative.");
            if (Payload == null)
                Payload = new byte[0];
            if (Payload.Length > ushort.MaxValue)
                throw new InvalidOperationException("Snapshot chunk payload is too large.");
        }
    }
}

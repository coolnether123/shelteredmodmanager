using System;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Stable wire envelope for event-driven application messages.
    /// Payload bytes are intentionally opaque to keep this project independent of game-specific types.
    /// </summary>
    public sealed class NetworkEventEnvelope
    {
        private const uint Magic = 0x5445564E; // NVET
        private const ushort WireVersion = 1;

        public NetworkEventEnvelope()
        {
            EventName = string.Empty;
            EventId = string.Empty;
            CorrelationId = string.Empty;
            Payload = new byte[0];
        }

        public string EventName { get; set; }
        public ushort EventVersion { get; set; }
        public string EventId { get; set; }
        public string CorrelationId { get; set; }
        public NetworkEventPhase Phase { get; set; }
        public byte SenderPeerId { get; set; }
        public uint WorldTick { get; set; }
        public byte[] Payload { get; set; }

        public static NetworkEventEnvelope Create(
            string eventName,
            ushort eventVersion,
            NetworkEventPhase phase,
            byte senderPeerId,
            uint worldTick,
            byte[] payload)
        {
            NetworkEventEnvelope envelope = new NetworkEventEnvelope();
            envelope.EventName = eventName ?? string.Empty;
            envelope.EventVersion = eventVersion;
            envelope.Phase = phase;
            envelope.SenderPeerId = senderPeerId;
            envelope.WorldTick = worldTick;
            envelope.EventId = Guid.NewGuid().ToString("N");
            envelope.Payload = payload ?? new byte[0];
            return envelope;
        }

        public byte[] ToPayload()
        {
            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(buffer);
            WriteTo(ref writer);

            byte[] payload = new byte[writer.Position];
            Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
            return payload;
        }

        public void WriteTo(ref BitWriter writer)
        {
            byte[] payload = Payload ?? new byte[0];
            if (payload.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("Payload", "Event payload is too large.");

            writer.WriteUInt32(Magic);
            writer.WriteUInt16(WireVersion);
            writer.WriteString(EventName ?? string.Empty);
            writer.WriteUInt16(EventVersion);
            writer.WriteByte((byte)Phase);
            writer.WriteByte(SenderPeerId);
            writer.WriteUInt32(WorldTick);
            writer.WriteString(EventId ?? string.Empty);
            writer.WriteString(CorrelationId ?? string.Empty);
            writer.WriteUInt16((ushort)payload.Length);
            if (payload.Length > 0)
                writer.WriteBytes(payload, 0, payload.Length);
        }

        public static NetworkEventEnvelope FromPayload(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException("payload");

            BitReader reader = new BitReader(payload, 0, payload.Length);
            return ReadFrom(ref reader);
        }

        public static NetworkEventEnvelope ReadFrom(ref BitReader reader)
        {
            uint magic = reader.ReadUInt32();
            if (magic != Magic)
                throw new InvalidOperationException("Network event payload had an invalid header.");

            ushort wireVersion = reader.ReadUInt16();
            if (wireVersion != WireVersion)
                throw new InvalidOperationException("Network event payload version is not supported.");

            NetworkEventEnvelope envelope = new NetworkEventEnvelope();
            envelope.EventName = reader.ReadString();
            envelope.EventVersion = reader.ReadUInt16();
            envelope.Phase = (NetworkEventPhase)reader.ReadByte();
            envelope.SenderPeerId = reader.ReadByte();
            envelope.WorldTick = reader.ReadUInt32();
            envelope.EventId = reader.ReadString();
            envelope.CorrelationId = reader.ReadString();

            ushort payloadLength = reader.ReadUInt16();
            envelope.Payload = new byte[payloadLength];
            if (payloadLength > 0)
                reader.ReadBytes(envelope.Payload, 0, payloadLength);

            return envelope;
        }

        public NetworkEventEnvelope AsAuthoritative(byte authorityPeerId, uint worldTick)
        {
            NetworkEventEnvelope envelope = Clone();
            envelope.Phase = NetworkEventPhase.Authoritative;
            envelope.SenderPeerId = authorityPeerId;
            envelope.WorldTick = worldTick;
            envelope.CorrelationId = string.IsNullOrEmpty(EventId) ? CorrelationId : EventId;
            envelope.EventId = Guid.NewGuid().ToString("N");
            return envelope;
        }

        public NetworkEventEnvelope Clone()
        {
            NetworkEventEnvelope clone = new NetworkEventEnvelope();
            clone.EventName = EventName;
            clone.EventVersion = EventVersion;
            clone.EventId = EventId;
            clone.CorrelationId = CorrelationId;
            clone.Phase = Phase;
            clone.SenderPeerId = SenderPeerId;
            clone.WorldTick = WorldTick;
            clone.Payload = Payload != null ? (byte[])Payload.Clone() : new byte[0];
            return clone;
        }
    }
}

using System;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Protocol
{
    /// <summary>
    /// Builds one MTU-sized packet containing one or more framed messages.
    /// </summary>
    public sealed class MessageBatchBuilder
    {
        private readonly byte[] _buffer;
        private int _position;
        private byte _messageCount;
        private PacketFlags _flags;

        public MessageBatchBuilder(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            _buffer = buffer;
            Reset();
        }

        public int Length { get { return _position; } }
        public byte MessageCount { get { return _messageCount; } }
        public PacketFlags Flags { get { return _flags; } }
        public bool HasMessages { get { return _messageCount > 0; } }

        public void AddFlags(PacketFlags flags)
        {
            _flags |= flags;
        }

        public void Reset()
        {
            _position = NetworkDefaults.HeaderSize;
            _messageCount = 0;
            _flags = PacketFlags.None;
        }

        public bool TryAdd(NetworkMessage message)
        {
            if (message.Payload == null)
                return false;
            if (message.Offset < 0 || message.Length < 0 || message.Offset + message.Length > message.Payload.Length)
                return false;
            if (_messageCount == byte.MaxValue)
                return false;
            if (message.Length > ushort.MaxValue)
                return false;

            int frameLength = 1 + 2 + 2 + message.Length;
            if (_position + frameLength > _buffer.Length)
                return false;

            BitWriter writer = new BitWriter(_buffer, _position, _buffer.Length - _position);
            writer.WriteByte((byte)message.Channel);
            writer.WriteUInt16(message.MessageType);
            writer.WriteUInt16((ushort)message.Length);
            writer.WriteBytes(message.Payload, message.Offset, message.Length);

            _position = writer.Position;
            _messageCount++;

            if (message.Channel == NetworkChannel.Reliable)
                _flags |= PacketFlags.HasReliableMessages;

            return true;
        }

        public void WriteHeader(ushort sequence, ushort ack, uint ackBits)
        {
            BitWriter writer = new BitWriter(_buffer, 0, NetworkDefaults.HeaderSize);
            NetworkPacketHeader header = NetworkPacketHeader.Create(sequence, ack, ackBits, _flags, _messageCount);
            header.WriteTo(ref writer);
        }
    }
}

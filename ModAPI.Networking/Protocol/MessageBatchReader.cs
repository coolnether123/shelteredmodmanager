using System;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Protocol
{
    /// <summary>
    /// Reads one packet header and iterates framed messages without copying payload bytes.
    /// </summary>
    public struct MessageBatchReader
    {
        private readonly byte[] _buffer;
        private readonly int _limit;
        private int _position;
        private int _messagesRead;

        public MessageBatchReader(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < NetworkDefaults.HeaderSize || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            _buffer = buffer;
            _limit = offset + count;
            _position = offset;
            _messagesRead = 0;

            BitReader headerReader = new BitReader(buffer, offset, NetworkDefaults.HeaderSize);
            Header = NetworkPacketHeader.ReadFrom(ref headerReader);
            _position = offset + NetworkDefaults.HeaderSize;
        }

        public NetworkPacketHeader Header { get; private set; }

        public bool TryReadNext(out NetworkMessage message)
        {
            message = new NetworkMessage();
            if (!Header.IsValid)
                return false;
            if (_messagesRead >= Header.MessageCount)
                return false;
            if (_position + 5 > _limit)
                return false;

            BitReader reader = new BitReader(_buffer, _position, _limit - _position);
            NetworkChannel channel = (NetworkChannel)reader.ReadByte();
            ushort messageType = reader.ReadUInt16();
            ushort length = reader.ReadUInt16();
            int payloadOffset = reader.Position;
            if (payloadOffset + length > _limit)
                return false;

            message = new NetworkMessage(messageType, channel, _buffer, payloadOffset, length);
            _position = payloadOffset + length;
            _messagesRead++;
            return true;
        }
    }
}

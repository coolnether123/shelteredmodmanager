using System;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Reliability
{
    internal sealed class ReliableSentPacket
    {
        private readonly byte[] _buffer;
        private readonly PacketFlags _flags;
        private readonly byte _messageCount;

        public ReliableSentPacket(
            ushort sequence,
            byte[] source,
            int offset,
            int length,
            PacketFlags flags,
            byte messageCount,
            DateTime sentUtc)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (offset < 0 || length < NetworkDefaults.HeaderSize || offset + length > source.Length)
                throw new ArgumentOutOfRangeException("offset");

            Sequence = sequence;
            _buffer = new byte[length];
            System.Buffer.BlockCopy(source, offset, _buffer, 0, length);
            _flags = flags;
            _messageCount = messageCount;
            FirstSentUtc = sentUtc;
            LastSentUtc = sentUtc;
        }

        public ushort Sequence { get; private set; }
        public int Length { get { return _buffer.Length; } }
        public byte[] Buffer { get { return _buffer; } }
        public DateTime FirstSentUtc { get; private set; }
        public DateTime LastSentUtc { get; private set; }
        public int RetryCount { get; private set; }

        public bool IsDue(DateTime utcNow, int resendMilliseconds)
        {
            return (utcNow - LastSentUtc).TotalMilliseconds >= resendMilliseconds;
        }

        public bool IsExpired(DateTime utcNow, int timeoutMilliseconds)
        {
            return (utcNow - FirstSentUtc).TotalMilliseconds >= timeoutMilliseconds;
        }

        public void RefreshHeader(ushort ack, uint ackBits)
        {
            BitWriter writer = new BitWriter(_buffer, 0, NetworkDefaults.HeaderSize);
            NetworkPacketHeader header = NetworkPacketHeader.Create(Sequence, ack, ackBits, _flags, _messageCount);
            header.WriteTo(ref writer);
        }

        public void MarkResent(DateTime utcNow)
        {
            RetryCount++;
            LastSentUtc = utcNow;
        }
    }
}

using System;
using System.Collections.Generic;
using ModAPI.Networking.Protocol;

namespace ModAPI.Networking.Reliability
{
    internal sealed class ReliableOutboundQueue
    {
        private readonly List<ReliableSentPacket> _sentPackets = new List<ReliableSentPacket>();

        public int Count { get { return _sentPackets.Count; } }

        public void TrackSent(
            ushort sequence,
            byte[] buffer,
            int offset,
            int length,
            PacketFlags flags,
            byte messageCount,
            DateTime sentUtc)
        {
            Remove(sequence);
            _sentPackets.Add(new ReliableSentPacket(sequence, buffer, offset, length, flags, messageCount, sentUtc));
        }

        public int ProcessAcks(ushort ack, uint ackBits)
        {
            int removed = 0;
            for (int i = _sentPackets.Count - 1; i >= 0; i--)
            {
                if (IsAcked(_sentPackets[i].Sequence, ack, ackBits))
                {
                    _sentPackets.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        public ReliableSentPacket[] GetDuePackets(DateTime utcNow, int resendMilliseconds)
        {
            List<ReliableSentPacket> due = null;
            for (int i = 0; i < _sentPackets.Count; i++)
            {
                ReliableSentPacket packet = _sentPackets[i];
                if (!packet.IsDue(utcNow, resendMilliseconds))
                    continue;

                if (due == null)
                    due = new List<ReliableSentPacket>();
                due.Add(packet);
            }

            return due != null ? due.ToArray() : new ReliableSentPacket[0];
        }

        public ReliableSentPacket FindExpired(DateTime utcNow, int timeoutMilliseconds)
        {
            for (int i = 0; i < _sentPackets.Count; i++)
            {
                if (_sentPackets[i].IsExpired(utcNow, timeoutMilliseconds))
                    return _sentPackets[i];
            }

            return null;
        }

        public void Clear()
        {
            _sentPackets.Clear();
        }

        private void Remove(ushort sequence)
        {
            for (int i = _sentPackets.Count - 1; i >= 0; i--)
            {
                if (_sentPackets[i].Sequence == sequence)
                    _sentPackets.RemoveAt(i);
            }
        }

        private static bool IsAcked(ushort sequence, ushort ack, uint ackBits)
        {
            if (sequence == ack)
                return true;

            int back = ack - sequence;
            if (back < 0)
                back += 65536;
            if (back <= 0 || back > 32)
                return false;

            return (ackBits & (uint)(1 << (back - 1))) != 0;
        }
    }
}

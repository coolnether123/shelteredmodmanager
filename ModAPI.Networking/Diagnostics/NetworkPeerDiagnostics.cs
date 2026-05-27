using System;
using ModAPI.Networking.Reliability;

namespace ModAPI.Networking.Diagnostics
{
    internal sealed class NetworkPeerDiagnostics
    {
        private const double LatencySmoothingFactor = 0.2;

        private bool _hasPendingHeartbeat;
        private ushort _pendingHeartbeatSequence;
        private DateTime _pendingHeartbeatSentUtc;
        private bool _hasHeartbeatLatency;
        private double _heartbeatLatencyMilliseconds;

        public long PacketsSent;
        public long PacketsReceived;
        public long BytesSent;
        public long BytesReceived;

        public void RecordPacketSent(int bytes)
        {
            PacketsSent++;
            BytesSent += bytes;
        }

        public void RecordPacketReceived(int bytes)
        {
            PacketsReceived++;
            BytesReceived += bytes;
        }

        public void RecordHeartbeatSent(ushort sequence, DateTime utcNow)
        {
            _pendingHeartbeatSequence = sequence;
            _pendingHeartbeatSentUtc = utcNow;
            _hasPendingHeartbeat = true;
        }

        public void RecordInboundAck(ushort ack, uint ackBits, DateTime utcNow)
        {
            if (!_hasPendingHeartbeat)
                return;

            AckWindow window = new AckWindow();
            if (!window.IsAcked(_pendingHeartbeatSequence, ack, ackBits))
                return;

            double sample = (utcNow - _pendingHeartbeatSentUtc).TotalMilliseconds;
            if (sample < 0)
                sample = 0;

            if (_hasHeartbeatLatency)
                _heartbeatLatencyMilliseconds = (_heartbeatLatencyMilliseconds * (1.0 - LatencySmoothingFactor)) + (sample * LatencySmoothingFactor);
            else
                _heartbeatLatencyMilliseconds = sample;

            _hasHeartbeatLatency = true;
            _hasPendingHeartbeat = false;
        }

        public double? HeartbeatLatencyMilliseconds
        {
            get { return _hasHeartbeatLatency ? (double?)_heartbeatLatencyMilliseconds : null; }
        }
    }
}

using System;
using System.Net;

namespace ModAPI.Networking.Diagnostics
{
    public sealed class NetworkDiagnosticsEvent
    {
        public NetworkDiagnosticsEvent(
            DateTime timestampUtc,
            NetworkDiagnosticsEventKind kind,
            byte peerId,
            IPEndPoint endPoint,
            int bytes,
            ushort sequence,
            byte messageCount,
            string summary)
        {
            TimestampUtc = timestampUtc;
            Kind = kind;
            PeerId = peerId;
            EndPoint = endPoint;
            Bytes = bytes;
            Sequence = sequence;
            MessageCount = messageCount;
            Summary = summary ?? string.Empty;
        }

        public DateTime TimestampUtc { get; private set; }
        public NetworkDiagnosticsEventKind Kind { get; private set; }
        public byte PeerId { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        public int Bytes { get; private set; }
        public ushort Sequence { get; private set; }
        public byte MessageCount { get; private set; }
        public string Summary { get; private set; }
    }
}

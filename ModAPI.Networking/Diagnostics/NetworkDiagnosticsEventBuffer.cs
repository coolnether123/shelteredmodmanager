using System;
using System.Net;

namespace ModAPI.Networking.Diagnostics
{
    internal sealed class NetworkDiagnosticsEventBuffer
    {
        private readonly NetworkDiagnosticsEvent[] _events;
        private int _nextIndex;
        private int _count;

        public NetworkDiagnosticsEventBuffer(int capacity)
        {
            _events = capacity > 0 ? new NetworkDiagnosticsEvent[capacity] : new NetworkDiagnosticsEvent[0];
        }

        public void Add(
            NetworkDiagnosticsEventKind kind,
            byte peerId,
            IPEndPoint endPoint,
            int bytes,
            ushort sequence,
            byte messageCount,
            string summary)
        {
            if (_events.Length == 0)
                return;

            _events[_nextIndex] = new NetworkDiagnosticsEvent(
                DateTime.UtcNow,
                kind,
                peerId,
                endPoint,
                bytes,
                sequence,
                messageCount,
                summary);

            _nextIndex = (_nextIndex + 1) % _events.Length;
            if (_count < _events.Length)
                _count++;
        }

        public NetworkDiagnosticsEvent[] ToArray()
        {
            NetworkDiagnosticsEvent[] snapshot = new NetworkDiagnosticsEvent[_count];
            if (_count == 0)
                return snapshot;

            int start = _count == _events.Length ? _nextIndex : 0;
            for (int i = 0; i < _count; i++)
                snapshot[i] = _events[(start + i) % _events.Length];

            return snapshot;
        }
    }
}

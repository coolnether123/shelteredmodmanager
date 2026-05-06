using System;
using System.Net;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Reliability;

namespace ModAPI.Networking.Connections
{
    public sealed class NetworkPeer
    {
        private readonly AckWindow _ackWindow = new AckWindow();
        private readonly ReliableOutboundQueue _reliableOutbound = new ReliableOutboundQueue();

        internal NetworkPeer(byte peerId, IPEndPoint endPoint, bool isHost, NetworkConnectionState state)
        {
            PeerId = peerId;
            EndPoint = endPoint;
            IsHost = isHost;
            Connection = new NetworkConnectionInfo(endPoint, state);
            Diagnostics = new NetworkPeerDiagnostics();
        }

        public byte PeerId { get; internal set; }
        public IPEndPoint EndPoint { get; internal set; }
        public bool IsHost { get; internal set; }
        public string DisplayName { get; internal set; }
        public string ApplicationId { get; internal set; }
        public string SessionId { get; internal set; }
        public string SessionNonce { get; internal set; }
        public string ContentSchemaHash { get; internal set; }
        public string ModContentHash { get; internal set; }
        public string StablePeerId { get; internal set; }
        public string ReconnectToken { get; internal set; }
        public string LastError { get; internal set; }
        public NetworkConnectionInfo Connection { get; private set; }
        public NetworkConnectionState State { get { return Connection.State; } }

        internal NetworkPeerDiagnostics Diagnostics { get; private set; }
        internal AckWindow AckWindow { get { return _ackWindow; } }
        internal ReliableOutboundQueue ReliableOutbound { get { return _reliableOutbound; } }

        internal ushort NextSequence()
        {
            ushort sequence = Connection.LocalSequence;
            Connection.LocalSequence = SequenceUtil.Next(Connection.LocalSequence);
            return sequence;
        }

        internal bool TouchReceive(DateTime utcNow, ushort sequence)
        {
            Connection.LastReceiveUtc = utcNow;
            Connection.RemoteSequence = sequence;
            return _ackWindow.MarkReceived(sequence);
        }

        internal void TouchSend(DateTime utcNow)
        {
            Connection.LastSendUtc = utcNow;
        }

        internal void SetState(NetworkConnectionState state, DateTime utcNow)
        {
            Connection.SetState(state, utcNow);
            if (state == NetworkConnectionState.Disconnected || state == NetworkConnectionState.TimedOut)
                _reliableOutbound.Clear();
        }
    }
}

using System;
using System.Net;
using ModAPI.Networking.Reliability;

namespace ModAPI.Networking.Connections
{
    public sealed class NetworkPeer
    {
        private readonly AckWindow _ackWindow = new AckWindow();

        internal NetworkPeer(byte peerId, IPEndPoint endPoint, bool isHost, NetworkConnectionState state)
        {
            PeerId = peerId;
            EndPoint = endPoint;
            IsHost = isHost;
            Connection = new NetworkConnectionInfo(endPoint, state);
        }

        public byte PeerId { get; internal set; }
        public IPEndPoint EndPoint { get; internal set; }
        public bool IsHost { get; internal set; }
        public string DisplayName { get; internal set; }
        public string ApplicationId { get; internal set; }
        public string SessionId { get; internal set; }
        public string ContentSchemaHash { get; internal set; }
        public string ModContentHash { get; internal set; }
        public string ReconnectToken { get; internal set; }
        public string LastError { get; internal set; }
        public NetworkConnectionInfo Connection { get; private set; }
        public NetworkConnectionState State { get { return Connection.State; } }

        internal AckWindow AckWindow { get { return _ackWindow; } }

        internal ushort NextSequence()
        {
            ushort sequence = Connection.LocalSequence;
            Connection.LocalSequence = SequenceUtil.Next(Connection.LocalSequence);
            return sequence;
        }

        internal void TouchReceive(DateTime utcNow, ushort sequence)
        {
            Connection.LastReceiveUtc = utcNow;
            Connection.RemoteSequence = sequence;
            _ackWindow.MarkReceived(sequence);
        }

        internal void TouchSend(DateTime utcNow)
        {
            Connection.LastSendUtc = utcNow;
        }

        internal void SetState(NetworkConnectionState state, DateTime utcNow)
        {
            Connection.SetState(state, utcNow);
        }
    }
}

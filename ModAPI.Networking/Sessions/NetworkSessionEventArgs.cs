using System;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Protocol;

namespace ModAPI.Networking.Sessions
{
    public sealed class NetworkPeerEventArgs : EventArgs
    {
        public NetworkPeerEventArgs(NetworkPeer peer)
        {
            Peer = peer;
        }

        public NetworkPeer Peer { get; private set; }
    }

    public sealed class NetworkPeerDisconnectedEventArgs : EventArgs
    {
        public NetworkPeerDisconnectedEventArgs(NetworkPeer peer, NetworkDisconnectReason reason, string message)
        {
            Peer = peer;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public NetworkPeer Peer { get; private set; }
        public NetworkDisconnectReason Reason { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class NetworkConnectionFailedEventArgs : EventArgs
    {
        public NetworkConnectionFailedEventArgs(NetworkPeer peer, HandshakeRejectReason reason, string message, Exception exception)
        {
            Peer = peer;
            Reason = reason;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public NetworkPeer Peer { get; private set; }
        public HandshakeRejectReason Reason { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }

    public sealed class NetworkMessageReceivedEventArgs : EventArgs
    {
        public NetworkMessageReceivedEventArgs(NetworkPeer peer, ushort messageType, NetworkChannel channel, byte[] payload)
        {
            Peer = peer;
            MessageType = messageType;
            Channel = channel;
            Payload = payload ?? new byte[0];
        }

        public NetworkPeer Peer { get; private set; }
        public ushort MessageType { get; private set; }
        public NetworkChannel Channel { get; private set; }
        public byte[] Payload { get; private set; }
    }

    public sealed class NetworkSessionErrorEventArgs : EventArgs
    {
        public NetworkSessionErrorEventArgs(string context, Exception exception, bool isFatal)
        {
            Context = context ?? string.Empty;
            Exception = exception;
            IsFatal = isFatal;
        }

        public string Context { get; private set; }
        public Exception Exception { get; private set; }
        public bool IsFatal { get; private set; }
    }
}

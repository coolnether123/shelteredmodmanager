using System;
using ModAPI.Networking.Connections;

namespace ModAPI.Networking.Events
{
    public sealed class NetworkEventReceivedEventArgs : EventArgs
    {
        public NetworkEventReceivedEventArgs(NetworkPeer peer, NetworkEventEnvelope envelope)
        {
            Peer = peer;
            Envelope = envelope;
        }

        public NetworkPeer Peer { get; private set; }
        public NetworkEventEnvelope Envelope { get; private set; }
    }
}

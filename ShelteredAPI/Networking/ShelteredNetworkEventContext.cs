using ModAPI.Networking.Connections;
using ModAPI.Networking.Events;

namespace ShelteredAPI.Networking
{
    public sealed class ShelteredNetworkEventContext
    {
        internal ShelteredNetworkEventContext(
            NetworkPeer peer,
            NetworkEventEnvelope envelope,
            ShelteredNetworkGameplayEvent gameplayEvent)
        {
            Peer = peer;
            Envelope = envelope;
            GameplayEvent = gameplayEvent;
        }

        public NetworkPeer Peer { get; private set; }
        public NetworkEventEnvelope Envelope { get; private set; }
        public ShelteredNetworkGameplayEvent GameplayEvent { get; private set; }
        public bool Accepted { get; private set; }
        public ShelteredNetworkGameplayEvent AcceptedEvent { get; private set; }
        public string RejectionReason { get; private set; }

        public void Accept()
        {
            Accept(GameplayEvent);
        }

        public void Accept(ShelteredNetworkGameplayEvent authoritativeEvent)
        {
            Accepted = true;
            AcceptedEvent = authoritativeEvent ?? GameplayEvent;
            RejectionReason = string.Empty;
        }

        public void Reject(string reason)
        {
            Accepted = false;
            AcceptedEvent = null;
            RejectionReason = reason ?? string.Empty;
        }
    }
}

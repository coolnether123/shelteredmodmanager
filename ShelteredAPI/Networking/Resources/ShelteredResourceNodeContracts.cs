using System;

namespace ShelteredAPI.Networking.Resources
{
    [Serializable]
    internal sealed class ResourceNode
    {
        public ResourceNode()
        {
            NodeId = string.Empty;
            Kind = string.Empty;
            OwnerFactionId = string.Empty;
            DiscoveredBy = string.Empty;
        }

        public string NodeId { get; set; }
        public string Kind { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Capacity { get; set; }
        public int Remaining { get; set; }
        public int RegenPerDay { get; set; }
        public int OwnerPlayerId { get; set; }
        public string OwnerFactionId { get; set; }
        public string DiscoveredBy { get; set; }
        public bool IsDepleted { get; set; }
        public long LastUpdatedTick { get; set; }

        public ResourceNode Copy()
        {
            return new ResourceNode
            {
                NodeId = NodeId ?? string.Empty,
                Kind = Kind ?? string.Empty,
                GridX = GridX,
                GridY = GridY,
                Capacity = Capacity,
                Remaining = Remaining,
                RegenPerDay = RegenPerDay,
                OwnerPlayerId = OwnerPlayerId,
                OwnerFactionId = OwnerFactionId ?? string.Empty,
                DiscoveredBy = DiscoveredBy ?? string.Empty,
                IsDepleted = IsDepleted,
                LastUpdatedTick = LastUpdatedTick
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredResourceNodeEvent
    {
        public ShelteredResourceNodeEvent()
        {
            Node = new ResourceNode();
            Reason = string.Empty;
            EventCorrelationId = string.Empty;
        }

        public ResourceNode Node { get; set; }
        public int PlayerId { get; set; }
        public int Delta { get; set; }
        public long WorldTick { get; set; }
        public string Reason { get; set; }
        public string EventCorrelationId { get; set; }

        public ShelteredResourceNodeEvent Copy()
        {
            return new ShelteredResourceNodeEvent
            {
                Node = Node != null ? Node.Copy() : new ResourceNode(),
                PlayerId = PlayerId,
                Delta = Delta,
                WorldTick = WorldTick,
                Reason = Reason ?? string.Empty,
                EventCorrelationId = EventCorrelationId ?? string.Empty
            };
        }
    }
}

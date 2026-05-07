using ModAPI.Networking.Events;
using ModAPI.Networking.Serialization;

namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Sheltered-facing POCO for gameplay events that need network synchronization.
    /// EventKind is the stable subscription key, for example "Expedition.Started" or "Location.Searched".
    /// </summary>
    public sealed class ShelteredNetworkGameplayEvent
    {
        public const string EnvelopeEventName = "Sheltered.GameplayEvent";
        public const ushort CurrentVersion = 1;

        public ShelteredNetworkGameplayEvent()
        {
            EventKind = string.Empty;
            ActorId = string.Empty;
            TargetId = string.Empty;
            Details = string.Empty;
        }

        public string EventKind { get; set; }
        public string ActorId { get; set; }
        public string TargetId { get; set; }
        public string Details { get; set; }
    }

    internal sealed class ShelteredNetworkGameplayEventSerializer
        : NetworkEventPayloadSerializer<ShelteredNetworkGameplayEvent>
    {
        public ShelteredNetworkGameplayEventSerializer()
            : base(ShelteredNetworkGameplayEvent.EnvelopeEventName, ShelteredNetworkGameplayEvent.CurrentVersion)
        {
        }

        protected override void Write(ShelteredNetworkGameplayEvent value, ref BitWriter writer)
        {
            if (value == null)
                value = new ShelteredNetworkGameplayEvent();

            writer.WriteString(value.EventKind ?? string.Empty);
            writer.WriteString(value.ActorId ?? string.Empty);
            writer.WriteString(value.TargetId ?? string.Empty);
            writer.WriteString(value.Details ?? string.Empty);
        }

        protected override ShelteredNetworkGameplayEvent Read(ref BitReader reader)
        {
            ShelteredNetworkGameplayEvent value = new ShelteredNetworkGameplayEvent();
            value.EventKind = reader.ReadString();
            value.ActorId = reader.ReadString();
            value.TargetId = reader.ReadString();
            value.Details = reader.ReadString();
            return value;
        }
    }
}

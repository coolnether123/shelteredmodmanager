using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Serializes one named event payload without coupling the network layer to application models.
    /// </summary>
    public interface INetworkEventPayloadSerializer
    {
        string EventName { get; }
        ushort EventVersion { get; }
        void WritePayload(object value, ref BitWriter writer);
        object ReadPayload(ref BitReader reader);
    }

    public abstract class NetworkEventPayloadSerializer<TPayload> : INetworkEventPayloadSerializer
    {
        protected NetworkEventPayloadSerializer(string eventName, ushort eventVersion)
        {
            EventName = eventName ?? string.Empty;
            EventVersion = eventVersion;
        }

        public string EventName { get; private set; }
        public ushort EventVersion { get; private set; }

        public void WritePayload(object value, ref BitWriter writer)
        {
            Write((TPayload)value, ref writer);
        }

        public object ReadPayload(ref BitReader reader)
        {
            return Read(ref reader);
        }

        protected abstract void Write(TPayload value, ref BitWriter writer);
        protected abstract TPayload Read(ref BitReader reader);
    }
}

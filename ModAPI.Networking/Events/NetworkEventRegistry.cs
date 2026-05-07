using System;
using System.Collections.Generic;
using ModAPI.Networking.Serialization;

namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Maps stable event names and versions to payload serializers.
    /// </summary>
    public sealed class NetworkEventRegistry
    {
        private readonly Dictionary<string, INetworkEventPayloadSerializer> _serializers =
            new Dictionary<string, INetworkEventPayloadSerializer>(StringComparer.Ordinal);

        public void Register(INetworkEventPayloadSerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException("serializer");
            if (string.IsNullOrEmpty(serializer.EventName))
                throw new ArgumentException("Event serializer must define an event name.", "serializer");

            _serializers[BuildKey(serializer.EventName, serializer.EventVersion)] = serializer;
        }

        public bool TryGet(string eventName, ushort eventVersion, out INetworkEventPayloadSerializer serializer)
        {
            return _serializers.TryGetValue(BuildKey(eventName, eventVersion), out serializer);
        }

        public byte[] SerializePayload(string eventName, ushort eventVersion, object payload)
        {
            INetworkEventPayloadSerializer serializer;
            if (!TryGet(eventName, eventVersion, out serializer))
                throw new InvalidOperationException("No serializer is registered for network event '" + eventName + "' v" + eventVersion + ".");

            byte[] buffer = new byte[NetworkDefaults.MaxPacketSize];
            BitWriter writer = new BitWriter(buffer);
            serializer.WritePayload(payload, ref writer);

            byte[] bytes = new byte[writer.Position];
            Buffer.BlockCopy(buffer, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public bool TryDeserializePayload(NetworkEventEnvelope envelope, out object payload, out string error)
        {
            payload = null;
            error = string.Empty;

            if (envelope == null)
            {
                error = "Event envelope was null.";
                return false;
            }

            INetworkEventPayloadSerializer serializer;
            if (!TryGet(envelope.EventName, envelope.EventVersion, out serializer))
            {
                error = "No serializer is registered for network event '" + envelope.EventName + "' v" + envelope.EventVersion + ".";
                return false;
            }

            try
            {
                byte[] bytes = envelope.Payload ?? new byte[0];
                BitReader reader = new BitReader(bytes, 0, bytes.Length);
                payload = serializer.ReadPayload(ref reader);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string BuildKey(string eventName, ushort eventVersion)
        {
            return (eventName ?? string.Empty) + "#" + eventVersion;
        }
    }
}

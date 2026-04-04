using System.IO;
using System.Xml.Serialization;
using Cortex.Bridge;

namespace Cortex.Host.Avalonia.Bridge
{
    internal static class BridgeMessageSerializer
    {
        private static readonly XmlSerializer EnvelopeSerializer = new XmlSerializer(typeof(BridgeMessageEnvelope));

        public static byte[] Serialize(BridgeMessageEnvelope envelope)
        {
            using (var stream = new MemoryStream())
            {
                EnvelopeSerializer.Serialize(stream, envelope ?? new BridgeMessageEnvelope());
                return stream.ToArray();
            }
        }

        public static BridgeMessageEnvelope Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            using (var stream = new MemoryStream(payload))
            {
                return EnvelopeSerializer.Deserialize(stream) as BridgeMessageEnvelope;
            }
        }
    }
}

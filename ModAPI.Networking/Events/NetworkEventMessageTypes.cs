using ModAPI.Networking.Protocol;

namespace ModAPI.Networking.Events
{
    /// <summary>
    /// Application message IDs reserved by the generic event-sync layer.
    /// </summary>
    public static class NetworkEventMessageTypes
    {
        public const ushort DefaultEventEnvelope = SessionMessageTypes.FirstApplicationMessageType + 64;

        public static bool IsEventEnvelope(ushort messageType)
        {
            return messageType == DefaultEventEnvelope;
        }
    }
}

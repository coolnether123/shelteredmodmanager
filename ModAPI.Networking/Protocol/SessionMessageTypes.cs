namespace ModAPI.Networking.Protocol
{
    public static class SessionMessageTypes
    {
        public const ushort HandshakeRequest = 0x0001;
        public const ushort HandshakeAccept = 0x0002;
        public const ushort HandshakeReject = 0x0003;
        public const ushort Heartbeat = 0x0004;
        public const ushort Disconnect = 0x0005;
        public const ushort FirstApplicationMessageType = 0x0100;

        public static bool IsReserved(ushort messageType)
        {
            return messageType < FirstApplicationMessageType;
        }
    }
}

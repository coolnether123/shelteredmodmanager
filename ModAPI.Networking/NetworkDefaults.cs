namespace ModAPI.Networking
{
    /// <summary>
    /// Shared protocol defaults for the host-neutral ModAPI networking layer.
    /// </summary>
    public static class NetworkDefaults
    {
        public const ushort PacketMagic = 0x4D4E;
        public const byte ProtocolVersion = 1;
        public const int DefaultPort = 7777;
        public const int MaxPacketSize = 1200;
        public const int HeaderSize = 11;
        public const int MaxMessagesPerPacket = 255;
        public const int DefaultFlushIntervalMilliseconds = 50;
        public const int DefaultReceiveBufferPoolSize = 64;
        public const int DefaultSendBufferPoolSize = 64;
        public const int DefaultReliableResendMilliseconds = 250;
        public const int DefaultConnectionTimeoutMilliseconds = 3000;
    }
}

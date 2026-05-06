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
        public const int HeaderSize = 13;
        public const int MaxMessagesPerPacket = 255;
        public const int DefaultFlushIntervalMilliseconds = 50;
        public const int DefaultReceiveBufferPoolSize = 64;
        public const int DefaultSendBufferPoolSize = 64;
        public const int DefaultReliableResendMilliseconds = 250;
        public const int DefaultConnectionTimeoutMilliseconds = 3000;
        public const int DefaultHandshakeRetryMilliseconds = 500;
        public const int DefaultHandshakeTimeoutMilliseconds = 5000;
        public const int DefaultDiscoveryTimeoutMilliseconds = 750;
        public const int DefaultHeartbeatIntervalMilliseconds = 1000;
        public const int DefaultDiagnosticsEventCapacity = 64;
        public const int DefaultMaxPeers = 4;
        public const byte HostPeerId = 0;
        public const byte UnassignedPeerId = 255;
        public const int MaxHandshakeStringLength = 128;
        public const int MaxDiscoveryStringLength = 128;
        public const string DefaultApplicationId = "ModAPI.Networking";
    }
}

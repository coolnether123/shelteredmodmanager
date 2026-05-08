using System;

namespace ModAPI.Networking
{
    /// <summary>
    /// Runtime configuration for transport and packet behavior.
    /// </summary>
    public sealed class NetworkConfig
    {
        public int Port = NetworkDefaults.DefaultPort;
        public int MaxPacketSize = NetworkDefaults.MaxPacketSize;
        public int FlushIntervalMilliseconds = NetworkDefaults.DefaultFlushIntervalMilliseconds;
        public int ReceiveBufferPoolSize = NetworkDefaults.DefaultReceiveBufferPoolSize;
        public int SendBufferPoolSize = NetworkDefaults.DefaultSendBufferPoolSize;
        public int ReliableResendMilliseconds = NetworkDefaults.DefaultReliableResendMilliseconds;
        public int AckFlushMilliseconds = NetworkDefaults.DefaultAckFlushMilliseconds;
        public int ConnectionTimeoutMilliseconds = NetworkDefaults.DefaultConnectionTimeoutMilliseconds;
        public int HandshakeRetryMilliseconds = NetworkDefaults.DefaultHandshakeRetryMilliseconds;
        public int HandshakeTimeoutMilliseconds = NetworkDefaults.DefaultHandshakeTimeoutMilliseconds;
        public int HeartbeatIntervalMilliseconds = NetworkDefaults.DefaultHeartbeatIntervalMilliseconds;
        public int DiscoveryTimeoutMilliseconds = NetworkDefaults.DefaultDiscoveryTimeoutMilliseconds;
        public int DiagnosticsEventCapacity = NetworkDefaults.DefaultDiagnosticsEventCapacity;
        public bool EnableBroadcastDiscovery = true;
        public bool AllowBroadcast;
        public int SimulatedPacketLossPercent;
        public int SimulatedLatencyMilliseconds;
        public int SimulatedJitterMilliseconds;

        public void Validate()
        {
            if (Port < 0 || Port > 65535)
                throw new ArgumentOutOfRangeException("Port", "Port must be between 0 and 65535.");
            if (MaxPacketSize < NetworkDefaults.HeaderSize + 4)
                throw new ArgumentOutOfRangeException("MaxPacketSize", "MaxPacketSize is too small for packet headers.");
            if (MaxPacketSize > 65535)
                throw new ArgumentOutOfRangeException("MaxPacketSize", "MaxPacketSize must fit in a UDP datagram.");
            if (FlushIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("FlushIntervalMilliseconds");
            if (ReceiveBufferPoolSize <= 0)
                throw new ArgumentOutOfRangeException("ReceiveBufferPoolSize");
            if (SendBufferPoolSize <= 0)
                throw new ArgumentOutOfRangeException("SendBufferPoolSize");
            if (ReliableResendMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("ReliableResendMilliseconds");
            if (AckFlushMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("AckFlushMilliseconds");
            if (ConnectionTimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("ConnectionTimeoutMilliseconds");
            if (HandshakeRetryMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("HandshakeRetryMilliseconds");
            if (HandshakeTimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("HandshakeTimeoutMilliseconds");
            if (HeartbeatIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("HeartbeatIntervalMilliseconds");
            if (DiscoveryTimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("DiscoveryTimeoutMilliseconds");
            if (DiagnosticsEventCapacity < 0)
                throw new ArgumentOutOfRangeException("DiagnosticsEventCapacity");
            if (SimulatedPacketLossPercent < 0 || SimulatedPacketLossPercent > 100)
                throw new ArgumentOutOfRangeException("SimulatedPacketLossPercent", "Simulated packet loss must be between 0 and 100 percent.");
            if (SimulatedLatencyMilliseconds < 0)
                throw new ArgumentOutOfRangeException("SimulatedLatencyMilliseconds");
            if (SimulatedJitterMilliseconds < 0)
                throw new ArgumentOutOfRangeException("SimulatedJitterMilliseconds");
        }

        public static NetworkConfig CreateDefault()
        {
            return new NetworkConfig();
        }
    }
}

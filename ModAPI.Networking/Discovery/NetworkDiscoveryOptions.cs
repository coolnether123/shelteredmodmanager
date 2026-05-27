using System;
using System.Net;

namespace ModAPI.Networking.Discovery
{
    public sealed class NetworkDiscoveryOptions
    {
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;
        public int Port = NetworkDefaults.DefaultPort;
        public int TimeoutMilliseconds = NetworkDefaults.DefaultDiscoveryTimeoutMilliseconds;
        public IPAddress BroadcastAddress = IPAddress.Broadcast;

        public static NetworkDiscoveryOptions CreateDefault()
        {
            return new NetworkDiscoveryOptions();
        }

        internal void Validate()
        {
            ApplicationId = Normalize(ApplicationId, NetworkDefaults.DefaultApplicationId);
            SessionId = Normalize(SessionId, string.Empty);
            if (Port < 0 || Port > 65535)
                throw new ArgumentOutOfRangeException("Port", "Port must be between 0 and 65535.");
            if (TimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("TimeoutMilliseconds");
            if (BroadcastAddress == null)
                throw new ArgumentNullException("BroadcastAddress");

            RequireLength(ApplicationId, "ApplicationId");
            RequireLength(SessionId, "SessionId");
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;
            return value;
        }

        private static void RequireLength(string value, string fieldName)
        {
            if (value != null && value.Length > NetworkDefaults.MaxDiscoveryStringLength)
                throw new ArgumentOutOfRangeException(fieldName, "Discovery text fields must stay compact.");
        }
    }
}

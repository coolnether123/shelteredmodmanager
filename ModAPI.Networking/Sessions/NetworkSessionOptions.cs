using System;

namespace ModAPI.Networking.Sessions
{
    public sealed class NetworkSessionOptions
    {
        public string ApplicationId = NetworkDefaults.DefaultApplicationId;
        public string SessionId = string.Empty;
        public string SessionNonce = string.Empty;
        public string ContentSchemaHash = string.Empty;
        public string ModContentHash = string.Empty;
        public string DisplayName = string.Empty;
        public string StablePeerId = string.Empty;
        public string ReconnectToken = string.Empty;
        public int MaxPeers = NetworkDefaults.DefaultMaxPeers;

        public static NetworkSessionOptions CreateDefault()
        {
            return new NetworkSessionOptions();
        }

        internal void Validate()
        {
            ApplicationId = Normalize(ApplicationId, NetworkDefaults.DefaultApplicationId);
            SessionId = Normalize(SessionId, string.Empty);
            SessionNonce = Normalize(SessionNonce, string.Empty);
            ContentSchemaHash = Normalize(ContentSchemaHash, string.Empty);
            ModContentHash = Normalize(ModContentHash, string.Empty);
            DisplayName = Normalize(DisplayName, string.Empty);
            StablePeerId = Normalize(StablePeerId, string.Empty);
            ReconnectToken = Normalize(ReconnectToken, string.Empty);

            RequireLength(ApplicationId, "ApplicationId");
            RequireLength(SessionId, "SessionId");
            RequireLength(SessionNonce, "SessionNonce");
            RequireLength(ContentSchemaHash, "ContentSchemaHash");
            RequireLength(ModContentHash, "ModContentHash");
            RequireLength(DisplayName, "DisplayName");
            RequireLength(StablePeerId, "StablePeerId");
            RequireLength(ReconnectToken, "ReconnectToken");

            if (MaxPeers <= 0 || MaxPeers > byte.MaxValue)
                throw new ArgumentOutOfRangeException("MaxPeers", "MaxPeers must fit in one byte.");
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;
            return value;
        }

        private static void RequireLength(string value, string fieldName)
        {
            if (value != null && value.Length > NetworkDefaults.MaxHandshakeStringLength)
                throw new ArgumentOutOfRangeException(fieldName, "Handshake text fields must stay compact.");
        }
    }
}

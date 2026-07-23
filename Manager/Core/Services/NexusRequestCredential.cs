using Manager.Core.Models;

namespace Manager.Core.Services
{
    internal sealed class NexusRequestCredential
    {
        internal string ApiKey;
        internal string BearerToken;

        internal bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(BearerToken) || !string.IsNullOrEmpty(ApiKey); }
        }

        internal static NexusRequestCredential FromApiKey(string apiKey)
        {
            return new NexusRequestCredential { ApiKey = apiKey ?? string.Empty };
        }
    }

    internal interface INexusCredentialProvider
    {
        NexusRequestCredential GetCredential(out string errorMessage);
        bool HasConfiguredCredential { get; }
    }

    internal sealed class StaticNexusCredentialProvider : INexusCredentialProvider
    {
        private readonly string _apiKey;

        internal StaticNexusCredentialProvider(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
        }

        public NexusRequestCredential GetCredential(out string errorMessage)
        {
            errorMessage = null;
            return NexusRequestCredential.FromApiKey(_apiKey);
        }

        public bool HasConfiguredCredential
        {
            get { return !string.IsNullOrEmpty(_apiKey); }
        }
    }
}

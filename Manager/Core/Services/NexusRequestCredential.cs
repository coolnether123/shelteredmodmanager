using System.Security.Cryptography;
using System.Text;

namespace Manager.Core.Services
{
    internal sealed class NexusRequestCredential
    {
        internal string ApiKey;
        internal string BearerToken;
        internal string RateLimitScope;

        internal bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(BearerToken) || !string.IsNullOrEmpty(ApiKey); }
        }

        internal static NexusRequestCredential FromApiKey(string apiKey)
        {
            string normalized = apiKey ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
                return new NexusRequestCredential { ApiKey = normalized, RateLimitScope = "api:anonymous" };

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var builder = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                    builder.Append(digest[i].ToString("x2"));
                return new NexusRequestCredential
                {
                    ApiKey = normalized,
                    RateLimitScope = "api:" + builder.ToString()
                };
            }
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

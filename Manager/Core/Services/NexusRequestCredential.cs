namespace Manager.Core.Services
{
    internal sealed class NexusRequestCredential
    {
        internal string BearerToken;
        internal string RateLimitScope;

        internal bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(BearerToken); }
        }
    }

    internal interface INexusCredentialProvider
    {
        NexusRequestCredential GetCredential(out string errorMessage);
        bool HasConfiguredCredential { get; }
        void InvalidateCredential();
    }
}

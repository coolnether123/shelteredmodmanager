namespace Manager.Core.Security
{
    /// <summary>
    /// Protects Nexus API keys at rest using Windows DPAPI (CurrentUser scope).
    /// </summary>
    public static class NexusApiKeyProtector
    {
        private const string Purpose = "NexusApiKey.v1";

        public static string Protect(string plainText)
        {
            return DpapiSecretProtector.Protect(
                string.IsNullOrEmpty(plainText) ? string.Empty : plainText.Trim(),
                Purpose);
        }

        public static bool TryUnprotect(string protectedBase64, out string plainText)
        {
            return DpapiSecretProtector.TryUnprotect(protectedBase64, Purpose, out plainText);
        }
    }
}

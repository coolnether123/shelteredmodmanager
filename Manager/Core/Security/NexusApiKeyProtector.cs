using System;
using System.Security.Cryptography;
using System.Text;

namespace Manager.Core.Security
{
    /// <summary>
    /// Protects Nexus API keys at rest using Windows DPAPI (CurrentUser scope).
    /// </summary>
    public static class NexusApiKeyProtector
    {
        // Stable entropy string scoped to this application and secret purpose.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ShelteredModManager.NexusApiKey.v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText.Trim());
                byte[] protectedData = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedData);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool TryUnprotect(string protectedBase64, out string plainText)
        {
            plainText = string.Empty;

            if (string.IsNullOrEmpty(protectedBase64))
                return true;

            try
            {
                byte[] protectedData = Convert.FromBase64String(protectedBase64.Trim());
                byte[] data = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
                plainText = Encoding.UTF8.GetString(data);
                return true;
            }
            catch
            {
                plainText = string.Empty;
                return false;
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace Manager.Core.Security
{
    /// <summary>
    /// Shared DPAPI primitive for SMM secrets. Each secret purpose receives
    /// distinct entropy so protected values cannot be substituted across uses.
    /// </summary>
    internal static class DpapiSecretProtector
    {
        internal static string Protect(string plainText, string purpose)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] entropy = Encoding.UTF8.GetBytes("ShelteredModManager." + purpose);
                byte[] protectedData = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedData);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool TryUnprotect(string protectedBase64, string purpose, out string plainText)
        {
            plainText = string.Empty;
            if (string.IsNullOrEmpty(protectedBase64))
                return true;

            try
            {
                byte[] protectedData = Convert.FromBase64String(protectedBase64.Trim());
                byte[] entropy = Encoding.UTF8.GetBytes("ShelteredModManager." + purpose);
                byte[] data = ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser);
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

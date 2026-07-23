namespace Manager.Core.Security
{
    internal static class NexusOAuthTokenProtector
    {
        private const string AccessTokenPurpose = "NexusOAuth.AccessToken.v1";
        private const string RefreshTokenPurpose = "NexusOAuth.RefreshToken.v1";

        internal static string ProtectAccessToken(string token)
        {
            return DpapiSecretProtector.Protect(token, AccessTokenPurpose);
        }

        internal static string ProtectRefreshToken(string token)
        {
            return DpapiSecretProtector.Protect(token, RefreshTokenPurpose);
        }

        internal static bool TryUnprotectAccessToken(string protectedValue, out string token)
        {
            return DpapiSecretProtector.TryUnprotect(protectedValue, AccessTokenPurpose, out token);
        }

        internal static bool TryUnprotectRefreshToken(string protectedValue, out string token)
        {
            return DpapiSecretProtector.TryUnprotect(protectedValue, RefreshTokenPurpose, out token);
        }
    }
}

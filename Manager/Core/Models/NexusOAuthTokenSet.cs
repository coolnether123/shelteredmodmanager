using System;

namespace Manager.Core.Models
{
    /// <summary>
    /// OAuth tokens issued to the current Nexus user. Token values are kept in
    /// memory as plain text and are persisted only through Windows DPAPI.
    /// </summary>
    public sealed class NexusOAuthTokenSet
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public NexusOAuthTokenSet()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            ExpiresAtUtc = DateTime.MinValue;
        }

        public bool HasAccessToken
        {
            get { return !string.IsNullOrEmpty(AccessToken); }
        }

        public bool HasRefreshToken
        {
            get { return !string.IsNullOrEmpty(RefreshToken); }
        }

        public bool IsAccessTokenUsable(DateTime utcNow, TimeSpan refreshBuffer)
        {
            return HasAccessToken && ExpiresAtUtc > utcNow.Add(refreshBuffer);
        }

        public void Clear()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            ExpiresAtUtc = DateTime.MinValue;
        }
    }
}

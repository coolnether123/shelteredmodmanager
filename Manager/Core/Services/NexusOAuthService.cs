using System;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Coordinates interactive sign-in, token refresh, secure persistence, and
    /// API-key fallback for all Nexus API clients.
    /// </summary>
    internal sealed class NexusOAuthService : INexusCredentialProvider
    {
        private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CallbackTimeout = TimeSpan.FromMinutes(5);
        private readonly object _sync = new object();
        private readonly SettingsService _settingsService;
        private readonly NexusOAuthClient _client;
        private AppSettings _settings;
        private string _oauthRateLimitScope = "oauth-session:" + Guid.NewGuid().ToString("N");

        internal NexusOAuthService(SettingsService settingsService, AppSettings settings)
        {
            _settingsService = settingsService;
            _settings = settings;
            _client = new NexusOAuthClient();
        }

        internal bool IsRegistrationAvailable
        {
            get { return NexusOAuthConfiguration.IsRegistered; }
        }

        internal string RedirectUri
        {
            get { return NexusOAuthConfiguration.RedirectUri; }
        }

        internal void UpdateSettings(AppSettings settings)
        {
            lock (_sync)
            {
                NexusOAuthTokenSet currentTokens = _settings != null ? _settings.NexusOAuthTokens : null;
                NexusOAuthTokenSet nextTokens = settings != null ? settings.NexusOAuthTokens : null;
                string currentIdentity = currentTokens != null && currentTokens.HasRefreshToken
                    ? currentTokens.RefreshToken
                    : (currentTokens != null ? currentTokens.AccessToken : string.Empty);
                string nextIdentity = nextTokens != null && nextTokens.HasRefreshToken
                    ? nextTokens.RefreshToken
                    : (nextTokens != null ? nextTokens.AccessToken : string.Empty);
                if (!string.Equals(currentIdentity, nextIdentity, StringComparison.Ordinal))
                    _oauthRateLimitScope = "oauth-session:" + Guid.NewGuid().ToString("N");

                _settings = settings;
            }
        }

        internal bool SignIn(out string errorMessage)
        {
            errorMessage = null;
            if (!NexusOAuthConfiguration.IsRegistered)
            {
                errorMessage = "Nexus OAuth registration is pending. The callback is ready at " +
                    NexusOAuthConfiguration.RedirectUri + ", but Nexus must issue the application client ID first.";
                return false;
            }

            NexusOAuthAuthorizationRequest authorization = NexusOAuthProtocol.CreateAuthorizationRequest();
            var listener = new NexusLoopbackCallbackListener();
            NexusOAuthCallbackResult callback = listener.WaitForCallback(
                authorization,
                CallbackTimeout,
                out errorMessage);
            if (callback == null || !callback.Success)
            {
                if (string.IsNullOrEmpty(errorMessage) && callback != null)
                    errorMessage = callback.ErrorMessage;
                return false;
            }

            NexusOAuthTokenSet tokens = _client.ExchangeAuthorizationCode(
                callback.AuthorizationCode,
                authorization.CodeVerifier,
                out errorMessage);
            if (tokens == null)
                return false;

            lock (_sync)
            {
                if (_settings == null)
                {
                    errorMessage = "Manager settings were unavailable after Nexus sign-in.";
                    return false;
                }

                _settings.NexusOAuthTokens = tokens;
                _oauthRateLimitScope = "oauth-session:" + Guid.NewGuid().ToString("N");
                PersistSettings();
            }
            return true;
        }

        internal void SignOut()
        {
            lock (_sync)
            {
                if (_settings == null)
                    return;

                _settings.NexusOAuthTokens.Clear();
                _oauthRateLimitScope = "oauth-session:" + Guid.NewGuid().ToString("N");
                PersistSettings();
            }
        }

        public NexusRequestCredential GetCredential(out string errorMessage)
        {
            lock (_sync)
            {
                errorMessage = null;
                if (_settings == null)
                    return new NexusRequestCredential();

                NexusOAuthTokenSet tokens = _settings.NexusOAuthTokens;
                if (tokens != null && tokens.IsAccessTokenUsable(DateTime.UtcNow, RefreshBuffer))
                {
                    return new NexusRequestCredential
                    {
                        BearerToken = tokens.AccessToken,
                        RateLimitScope = _oauthRateLimitScope
                    };
                }

                if (tokens != null && tokens.HasRefreshToken && NexusOAuthConfiguration.IsRegistered)
                {
                    NexusOAuthTokenSet refreshed = _client.Refresh(tokens.RefreshToken, out errorMessage);
                    if (refreshed != null)
                    {
                        if (!refreshed.HasRefreshToken)
                            refreshed.RefreshToken = tokens.RefreshToken;
                        _settings.NexusOAuthTokens = refreshed;
                        PersistSettings();
                        errorMessage = null;
                        return new NexusRequestCredential
                        {
                            BearerToken = refreshed.AccessToken,
                            RateLimitScope = _oauthRateLimitScope
                        };
                    }
                }

                if (!string.IsNullOrEmpty(_settings.NexusApiKey))
                {
                    errorMessage = null;
                    return NexusRequestCredential.FromApiKey(_settings.NexusApiKey);
                }

                if (tokens != null && tokens.HasAccessToken && string.IsNullOrEmpty(errorMessage))
                    errorMessage = "The Nexus sign-in session expired. Sign in again.";

                return new NexusRequestCredential();
            }
        }

        public bool HasConfiguredCredential
        {
            get
            {
                lock (_sync)
                {
                    return _settings != null && _settings.HasNexusCredential;
                }
            }
        }

        private void PersistSettings()
        {
            if (_settingsService != null && _settings != null)
                _settingsService.Save(_settings);
        }
    }
}

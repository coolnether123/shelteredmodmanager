using System;
using System.IO;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    internal static class NexusOAuthSessionHarness
    {
        private static int _failures;

        private static void Main()
        {
            TestFreshInstallationStartsDisconnected();
            TestValidAccessTokenIsUsed();
            TestExpiredAccessTokenRefreshesAndPersists();
            TestFailedRefreshDisconnectsWithoutFallback();
            TestLogoutClearsOAuthState();
            TestLegacyPersonalKeyStateIsScrubbed();

            if (_failures > 0)
            {
                Console.Error.WriteLine("Nexus OAuth session checks failed: " + _failures + ".");
                Environment.Exit(1);
            }

            Console.WriteLine("Nexus OAuth session checks passed.");
        }

        private static void TestFreshInstallationStartsDisconnected()
        {
            AppSettings settings = new AppSettings();
            var service = new NexusOAuthService(null, settings, new FakeTokenClient());
            string error;
            NexusRequestCredential credential = service.GetCredential(out error);
            Assert(!service.HasConfiguredCredential, "A fresh installation did not start disconnected.");
            Assert(credential != null && !credential.IsConfigured, "A fresh installation produced an authenticated credential.");
            Assert(string.IsNullOrEmpty(error), "A fresh anonymous metadata session produced an authentication error.");
        }

        private static void TestValidAccessTokenIsUsed()
        {
            AppSettings settings = NewSettings("valid-access", "valid-refresh", DateTime.UtcNow.AddHours(1));
            var service = new NexusOAuthService(null, settings, new FakeTokenClient());
            string error;
            NexusRequestCredential credential = service.GetCredential(out error);
            Assert(string.IsNullOrEmpty(error), "A valid OAuth access token produced an error.");
            Assert(credential != null && credential.BearerToken == "valid-access", "A valid OAuth access token was not selected.");
        }

        private static void TestExpiredAccessTokenRefreshesAndPersists()
        {
            string path = TempSettingsPath();
            try
            {
                var settingsService = new SettingsService(path);
                AppSettings settings = NewSettings("expired-access", "refresh-one", DateTime.UtcNow.AddMinutes(-5));
                var refreshed = NewSettings("replacement-access", "replacement-refresh", DateTime.UtcNow.AddHours(2)).NexusOAuthTokens;
                var client = new FakeTokenClient { RefreshResult = refreshed };
                var service = new NexusOAuthService(settingsService, settings, client);

                string error;
                NexusRequestCredential credential = service.GetCredential(out error);
                Assert(client.RefreshCalls == 1, "An expired OAuth access token did not invoke refresh.");
                Assert(string.IsNullOrEmpty(error) && credential.BearerToken == "replacement-access", "OAuth refresh did not return the replacement bearer token.");

                AppSettings loaded = settingsService.Load();
                Assert(loaded.NexusOAuthTokens.AccessToken == "replacement-access", "The refreshed OAuth access token was not persisted.");
                Assert(loaded.NexusOAuthTokens.RefreshToken == "replacement-refresh", "The refreshed OAuth refresh token was not persisted.");
            }
            finally
            {
                DeleteIfPresent(path);
            }
        }

        private static void TestFailedRefreshDisconnectsWithoutFallback()
        {
            string path = TempSettingsPath();
            try
            {
                var settingsService = new SettingsService(path);
                AppSettings settings = NewSettings("expired-access", "revoked-refresh", DateTime.UtcNow.AddMinutes(-5));
                var client = new FakeTokenClient { RefreshError = "refresh rejected" };
                var service = new NexusOAuthService(settingsService, settings, client);

                string error;
                NexusRequestCredential credential = service.GetCredential(out error);
                Assert(client.RefreshCalls == 1, "A failed OAuth refresh was not attempted exactly once.");
                Assert(credential != null && !credential.IsConfigured, "A failed OAuth refresh produced a fallback credential.");
                Assert(!service.HasConfiguredCredential, "A failed OAuth refresh did not disconnect the session.");
                Assert(error == "refresh rejected", "The OAuth refresh failure was not returned clearly.");

                AppSettings loaded = settingsService.Load();
                Assert(!loaded.HasNexusOAuthSession, "A failed OAuth refresh remained persisted as an authenticated session.");
            }
            finally
            {
                DeleteIfPresent(path);
            }
        }

        private static void TestLogoutClearsOAuthState()
        {
            string path = TempSettingsPath();
            try
            {
                var settingsService = new SettingsService(path);
                AppSettings settings = NewSettings("logout-access", "logout-refresh", DateTime.UtcNow.AddHours(1));
                var service = new NexusOAuthService(settingsService, settings, new FakeTokenClient());
                service.SignOut();
                Assert(!service.HasConfiguredCredential, "Logout did not clear the in-memory OAuth session.");
                Assert(!settingsService.Load().HasNexusOAuthSession, "Logout did not clear the persisted OAuth session.");
            }
            finally
            {
                DeleteIfPresent(path);
            }
        }

        private static void TestLegacyPersonalKeyStateIsScrubbed()
        {
            string path = TempSettingsPath();
            const string oldSecret = "legacy-secret-must-not-survive";
            try
            {
                File.WriteAllText(path,
                    "DarkMode=False\r\n" +
                    "NexusGameDomain=fallout4\r\n" +
                    "NexusApiKey=" + oldSecret + "\r\n" +
                    "NexusApiKeyProtected=obsolete-protected-value\r\n");

                var settingsService = new SettingsService(path);
                AppSettings settings = settingsService.Load();
                string persisted = File.ReadAllText(path);

                Assert(typeof(AppSettings).GetProperty("NexusApiKey") == null, "The production settings model still exposes a personal-key property.");
                Assert(!settings.HasNexusOAuthSession, "Legacy personal-key state authenticated the upgraded installation.");
                Assert(!persisted.Contains("NexusApiKey") && !persisted.Contains(oldSecret), "Legacy personal-key material was not removed from disk.");
                Assert(!settings.DarkMode && settings.NexusGameDomain == "fallout4", "Legacy credential migration damaged unrelated settings.");
            }
            finally
            {
                DeleteIfPresent(path);
            }
        }

        private static AppSettings NewSettings(string accessToken, string refreshToken, DateTime expiresAtUtc)
        {
            var settings = new AppSettings();
            settings.NexusOAuthTokens.AccessToken = accessToken;
            settings.NexusOAuthTokens.RefreshToken = refreshToken;
            settings.NexusOAuthTokens.ExpiresAtUtc = expiresAtUtc;
            return settings;
        }

        private static string TempSettingsPath()
        {
            return Path.Combine(Path.GetTempPath(), "smm-oauth-session-" + Guid.NewGuid().ToString("N") + ".ini");
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void Assert(bool condition, string message)
        {
            if (condition)
                return;
            _failures++;
            Console.Error.WriteLine(message);
        }

        private sealed class FakeTokenClient : INexusOAuthTokenClient
        {
            internal NexusOAuthTokenSet RefreshResult;
            internal string RefreshError;
            internal int RefreshCalls;

            public NexusOAuthTokenSet ExchangeAuthorizationCode(string authorizationCode, string codeVerifier, out string errorMessage)
            {
                errorMessage = "Interactive exchange is not used by this session harness.";
                return null;
            }

            public NexusOAuthTokenSet Refresh(string refreshToken, out string errorMessage)
            {
                RefreshCalls++;
                errorMessage = RefreshError;
                return RefreshResult;
            }
        }
    }
}

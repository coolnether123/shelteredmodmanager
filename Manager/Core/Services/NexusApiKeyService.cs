using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Manager.Core.Models;
using Manager.Core.Security;

namespace Manager.Core.Services
{
    internal sealed class NexusApiKeyService : INexusCredentialProvider
    {
        private const string SecretPurpose = "Nexus.ApiKey.v1";
        private readonly string _path;
        private readonly NexusRateLimitTracker _rateLimits;
        private readonly string _apiBaseUrl;
        private volatile NexusRequestCredential _credential;

        internal NexusApiKeyService(string path, NexusRateLimitTracker rateLimits, string apiBaseUrl)
        {
            _path = path;
            _rateLimits = rateLimits;
            _apiBaseUrl = apiBaseUrl;
            try
            {
                string key;
                if (File.Exists(path) && DpapiSecretProtector.TryUnprotect(File.ReadAllText(path), SecretPurpose, out key)
                    && !string.IsNullOrEmpty(key))
                    _credential = CreateCredential(key);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal NexusAccountStatus SignIn(string input, out string errorMessage)
        {
            string key = (input ?? string.Empty).Trim();
            if (key.Length == 0 || key.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                errorMessage = "Paste your personal Nexus API key.";
                return null;
            }

            var candidate = new CandidateCredentialProvider(CreateCredential(key));
            var nexus = new NexusModsService(candidate, _rateLimits, _apiBaseUrl);
            NexusAccountStatus status = nexus.GetAccountStatus(out errorMessage);
            if (!status.IsConnected)
                return status;

            try { StoreValidatedKey(key); }
            catch (IOException) { errorMessage = "The Nexus API key could not be saved."; return null; }
            catch (UnauthorizedAccessException) { errorMessage = "The Nexus API key could not be saved."; return null; }
            return status;
        }

        private void StoreValidatedKey(string key)
        {
            string protectedKey = DpapiSecretProtector.Protect(key, SecretPurpose);
            if (string.IsNullOrEmpty(protectedKey))
                throw new IOException("Windows could not protect the Nexus API key.");

            AtomicFile.WriteAllText(_path, protectedKey);
            _credential = CreateCredential(key);
        }

        internal void Clear()
        {
            File.Delete(_path);
            _credential = null;
        }

        public bool HasConfiguredCredential { get { return _credential != null; } }

        public NexusRequestCredential GetCredential(out string errorMessage)
        {
            errorMessage = null;
            return _credential;
        }

        // Keep a rejected key available for explicit replacement or removal.
        public void InvalidateCredential() { }

        private static NexusRequestCredential CreateCredential(string key)
        {
            using (var hash = SHA256.Create())
            {
                return new NexusRequestCredential
                {
                    ApiKey = key,
                    RateLimitScope = "api-key:" + Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(key)))
                };
            }
        }

        private sealed class CandidateCredentialProvider : INexusCredentialProvider
        {
            private readonly NexusRequestCredential _candidate;

            internal CandidateCredentialProvider(NexusRequestCredential candidate)
            {
                _candidate = candidate;
            }

            public bool HasConfiguredCredential { get { return true; } }

            public NexusRequestCredential GetCredential(out string errorMessage)
            {
                errorMessage = null;
                return _candidate;
            }

            public void InvalidateCredential() { }
        }
    }
}

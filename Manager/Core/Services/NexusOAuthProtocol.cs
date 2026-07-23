using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Manager.Core.Services
{
    internal sealed class NexusOAuthAuthorizationRequest
    {
        internal string State;
        internal string CodeVerifier;
        internal string CodeChallenge;
        internal Uri AuthorizationUri;
    }

    internal sealed class NexusOAuthCallbackResult
    {
        internal bool Success;
        internal string AuthorizationCode;
        internal string ErrorMessage;
    }

    /// <summary>
    /// Pure OAuth/PKCE protocol operations, isolated from browser, socket, and
    /// token-storage concerns so they can be tested without Nexus credentials.
    /// </summary>
    internal static class NexusOAuthProtocol
    {
        internal static NexusOAuthAuthorizationRequest CreateAuthorizationRequest()
        {
            string verifier = CreateRandomBase64Url(64);
            string state = CreateRandomBase64Url(32);
            string challenge;
            using (SHA256 sha = SHA256.Create())
            {
                challenge = ToBase64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            }

            var parameters = new Dictionary<string, string>();
            parameters["client_id"] = NexusOAuthConfiguration.ClientId;
            parameters["response_type"] = "code";
            parameters["scope"] = string.Empty;
            parameters["redirect_uri"] = NexusOAuthConfiguration.RedirectUri;
            parameters["state"] = state;
            parameters["code_challenge_method"] = "S256";
            parameters["code_challenge"] = challenge;

            return new NexusOAuthAuthorizationRequest
            {
                State = state,
                CodeVerifier = verifier,
                CodeChallenge = challenge,
                AuthorizationUri = new Uri(NexusOAuthConfiguration.AuthorizationEndpoint + "?" + BuildFormEncoded(parameters))
            };
        }

        internal static NexusOAuthCallbackResult ParseCallback(string requestTarget, string expectedState)
        {
            if (string.IsNullOrEmpty(requestTarget))
                return Failure("The OAuth callback request was empty.");

            Uri callback;
            try
            {
                callback = new Uri("http://127.0.0.1" + requestTarget);
            }
            catch
            {
                return Failure("The OAuth callback URI was malformed.");
            }

            if (!string.Equals(callback.AbsolutePath, NexusOAuthConfiguration.CallbackPath, StringComparison.Ordinal))
                return Failure("The OAuth callback path was not recognized.");

            Dictionary<string, string> query;
            if (!TryParseFormEncoded(callback.Query.TrimStart('?'), out query))
                return Failure("The OAuth callback parameters were malformed.");

            string returnedState;
            if (!query.TryGetValue("state", out returnedState) ||
                !FixedTimeEquals(returnedState, expectedState))
            {
                return Failure("The OAuth callback state did not match the sign-in request.");
            }

            string providerError;
            if (query.TryGetValue("error", out providerError) && !string.IsNullOrEmpty(providerError))
            {
                string description;
                query.TryGetValue("error_description", out description);
                return Failure(!string.IsNullOrEmpty(description)
                    ? "Nexus authorization was not completed: " + description
                    : "Nexus authorization was not completed: " + providerError);
            }

            string code;
            if (!query.TryGetValue("code", out code) || string.IsNullOrEmpty(code))
                return Failure("Nexus did not return an authorization code.");

            return new NexusOAuthCallbackResult
            {
                Success = true,
                AuthorizationCode = code
            };
        }

        internal static string BuildFormEncoded(IDictionary<string, string> values)
        {
            var builder = new StringBuilder();
            if (values == null)
                return string.Empty;

            foreach (KeyValuePair<string, string> pair in values)
            {
                if (builder.Length > 0)
                    builder.Append('&');
                builder.Append(Uri.EscapeDataString(pair.Key ?? string.Empty));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
            }

            return builder.ToString();
        }

        private static bool TryParseFormEncoded(string encoded, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string[] pairs = (encoded ?? string.Empty).Split('&');
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (pairs[i].Length == 0)
                        continue;

                    string[] parts = pairs[i].Split(new[] { '=' }, 2);
                    string key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
                    string value = parts.Length > 1
                        ? Uri.UnescapeDataString(parts[1].Replace("+", " "))
                        : string.Empty;
                    values[key] = value;
                }

                return true;
            }
            catch
            {
                values.Clear();
                return false;
            }
        }

        private static string CreateRandomBase64Url(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            RandomNumberGenerator random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return ToBase64Url(bytes);
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            byte[] rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            int difference = leftBytes.Length ^ rightBytes.Length;
            int length = Math.Max(leftBytes.Length, rightBytes.Length);
            for (int i = 0; i < length; i++)
            {
                byte leftByte = i < leftBytes.Length ? leftBytes[i] : (byte)0;
                byte rightByte = i < rightBytes.Length ? rightBytes[i] : (byte)0;
                difference |= leftByte ^ rightByte;
            }
            return difference == 0;
        }

        private static NexusOAuthCallbackResult Failure(string message)
        {
            return new NexusOAuthCallbackResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}

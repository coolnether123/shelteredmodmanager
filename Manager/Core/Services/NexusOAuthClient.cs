using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Exchanges authorization codes and refresh tokens with the Nexus user
    /// service. This public desktop client never uses or stores a client secret.
    /// </summary>
    internal sealed class NexusOAuthClient
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        internal NexusOAuthTokenSet ExchangeAuthorizationCode(
            string authorizationCode,
            string codeVerifier,
            out string errorMessage)
        {
            var fields = new Dictionary<string, string>();
            fields["grant_type"] = "authorization_code";
            fields["redirect_uri"] = NexusOAuthConfiguration.RedirectUri;
            fields["client_id"] = NexusOAuthConfiguration.ClientId;
            fields["code"] = authorizationCode ?? string.Empty;
            fields["code_verifier"] = codeVerifier ?? string.Empty;
            return RequestTokens(fields, out errorMessage);
        }

        internal NexusOAuthTokenSet Refresh(string refreshToken, out string errorMessage)
        {
            var fields = new Dictionary<string, string>();
            fields["grant_type"] = "refresh_token";
            fields["client_id"] = NexusOAuthConfiguration.ClientId;
            fields["refresh_token"] = refreshToken ?? string.Empty;
            return RequestTokens(fields, out errorMessage);
        }

        private NexusOAuthTokenSet RequestTokens(
            IDictionary<string, string> fields,
            out string errorMessage)
        {
            errorMessage = null;
            try
            {
                byte[] body = Encoding.UTF8.GetBytes(NexusOAuthProtocol.BuildFormEncoded(fields));
                var request = (HttpWebRequest)WebRequest.Create(NexusOAuthConfiguration.TokenEndpoint);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Accept = "application/json";
                request.UserAgent = Core.AppVersionInfo.UserAgent;
                request.Headers["Application-Name"] = Core.AppVersionInfo.ApplicationName;
                request.Headers["Application-Version"] = Core.AppVersionInfo.NexusHeader;
                request.ContentLength = body.Length;
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = stream != null ? new StreamReader(stream) : null)
                {
                    string json = reader != null ? reader.ReadToEnd() : string.Empty;
                    return ParseTokenResponse(json, null, out errorMessage);
                }
            }
            catch (WebException ex)
            {
                errorMessage = ReadOAuthError(ex);
                return null;
            }
            catch (Exception ex)
            {
                errorMessage = "Nexus OAuth token request failed: " + ex.Message;
                return null;
            }
        }

        private NexusOAuthTokenSet ParseTokenResponse(
            string json,
            string existingRefreshToken,
            out string errorMessage)
        {
            errorMessage = null;
            Dictionary<string, object> data;
            try
            {
                data = _serializer.DeserializeObject(json ?? string.Empty) as Dictionary<string, object>;
            }
            catch
            {
                data = null;
            }

            if (data == null)
            {
                errorMessage = "Nexus returned an invalid OAuth token response.";
                return null;
            }

            string accessToken = ReadString(data, "access_token");
            string refreshToken = ReadString(data, "refresh_token");
            int expiresIn = ReadInt(data, "expires_in");
            if (string.IsNullOrEmpty(accessToken) || expiresIn <= 0)
            {
                errorMessage = "Nexus did not return a usable OAuth access token.";
                return null;
            }

            return new NexusOAuthTokenSet
            {
                AccessToken = accessToken,
                RefreshToken = !string.IsNullOrEmpty(refreshToken) ? refreshToken : (existingRefreshToken ?? string.Empty),
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn)
            };
        }

        private string ReadOAuthError(WebException ex)
        {
            try
            {
                using (var response = ex.Response as HttpWebResponse)
                using (Stream stream = response != null ? response.GetResponseStream() : null)
                using (var reader = stream != null ? new StreamReader(stream) : null)
                {
                    string json = reader != null ? reader.ReadToEnd() : string.Empty;
                    var data = _serializer.DeserializeObject(json) as Dictionary<string, object>;
                    if (data != null)
                    {
                        string description = ReadString(data, "error_description");
                        if (!string.IsNullOrEmpty(description))
                            return "Nexus OAuth token request failed: " + description;

                        string error = ReadString(data, "error");
                        if (!string.IsNullOrEmpty(error))
                            return "Nexus OAuth token request failed: " + error;
                    }
                }
            }
            catch
            {
            }

            return "Nexus OAuth token request failed: " + ex.Message;
        }

        private static string ReadString(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value)
                : string.Empty;
        }

        private static int ReadInt(Dictionary<string, object> data, string key)
        {
            object value;
            int parsed;
            return data != null && data.TryGetValue(key, out value) &&
                int.TryParse(Convert.ToString(value), out parsed)
                ? parsed
                : 0;
        }
    }
}

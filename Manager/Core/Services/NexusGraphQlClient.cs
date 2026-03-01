using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Manager.Core.Services
{
    internal class NexusGraphQlResponse
    {
        public Dictionary<string, object> Data;
        public string ErrorMessage;
    }

    /// <summary>
    /// Minimal GraphQL client for Nexus API v2.
    /// </summary>
    internal sealed class NexusGraphQlClient
    {
        private const string Endpoint = "https://api.nexusmods.com/v2/graphql";
        private readonly string _apiKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public NexusGraphQlClient(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
        }

        public NexusGraphQlResponse Execute(string query, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(query))
            {
                return new NexusGraphQlResponse { ErrorMessage = "Query is required." };
            }

            try
            {
                var payload = new Dictionary<string, object>();
                payload["query"] = query;
                payload["variables"] = variables ?? new Dictionary<string, object>();

                var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.UserAgent = "ShelteredModManager/1.3.0";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;
                request.ProtocolVersion = HttpVersion.Version11;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.Headers["apikey"] = _apiKey;
                }

                var body = _serializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var root = _serializer.DeserializeObject(json) as Dictionary<string, object>;
                    if (root == null)
                    {
                        return new NexusGraphQlResponse { ErrorMessage = "Invalid response payload." };
                    }

                    object errorsObj;
                    if (root.TryGetValue("errors", out errorsObj))
                    {
                        var errors = errorsObj as object[];
                        if (errors != null && errors.Length > 0)
                        {
                            var firstError = errors[0] as Dictionary<string, object>;
                            if (firstError != null)
                            {
                                object msgObj;
                                if (firstError.TryGetValue("message", out msgObj))
                                {
                                    return new NexusGraphQlResponse { ErrorMessage = Convert.ToString(msgObj) };
                                }
                            }

                            return new NexusGraphQlResponse { ErrorMessage = "Nexus API returned an error." };
                        }
                    }

                    object dataObj;
                    if (!root.TryGetValue("data", out dataObj))
                    {
                        return new NexusGraphQlResponse { ErrorMessage = "Response did not contain data." };
                    }

                    var data = dataObj as Dictionary<string, object>;
                    if (data == null)
                    {
                        return new NexusGraphQlResponse { ErrorMessage = "Response data was invalid." };
                    }

                    return new NexusGraphQlResponse { Data = data };
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.SecureChannelFailure ||
                    ex.Status == WebExceptionStatus.TrustFailure ||
                    ex.Status == WebExceptionStatus.SendFailure)
                {
                    return new NexusGraphQlResponse
                    {
                        ErrorMessage = "Nexus request failed during HTTPS negotiation. Ensure TLS 1.2 is enabled on this system."
                    };
                }

                try
                {
                    using (var response = ex.Response as HttpWebResponse)
                    using (var stream = response != null ? response.GetResponseStream() : null)
                    using (var reader = stream != null ? new StreamReader(stream) : null)
                    {
                        var details = reader != null ? reader.ReadToEnd() : string.Empty;
                        if (!string.IsNullOrEmpty(details))
                        {
                            return new NexusGraphQlResponse { ErrorMessage = "Nexus request failed: " + details };
                        }
                    }
                }
                catch
                {
                    // Fall back to base exception text.
                }

                return new NexusGraphQlResponse { ErrorMessage = "Nexus request failed: " + ex.Message };
            }
            catch (Exception ex)
            {
                return new NexusGraphQlResponse { ErrorMessage = "Nexus request failed: " + ex.Message };
            }
        }
    }
}

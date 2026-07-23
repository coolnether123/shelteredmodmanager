using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Manager.Core.Services
{
    internal sealed class NexusGraphQlResponse
    {
        public Dictionary<string, object> Data;
        public string ErrorMessage;
    }

    /// <summary>
    /// Minimal GraphQL client for Nexus API v2 metadata reads.
    /// </summary>
    internal sealed class NexusGraphQlClient
    {
        private const string Endpoint = "https://api.nexusmods.com/v2/graphql";
        private readonly INexusCredentialProvider _credentialProvider;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public NexusGraphQlClient(string apiKey)
        {
            _credentialProvider = new StaticNexusCredentialProvider(apiKey);
        }

        internal NexusGraphQlClient(INexusCredentialProvider credentialProvider)
        {
            _credentialProvider = credentialProvider ?? new StaticNexusCredentialProvider(string.Empty);
        }

        public NexusGraphQlResponse Execute(string query, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(query))
                return new NexusGraphQlResponse { ErrorMessage = "Query is required." };

            try
            {
                var payload = new Dictionary<string, object>();
                payload["query"] = query;
                payload["variables"] = variables ?? new Dictionary<string, object>();

                var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.KeepAlive = false;
                request.ProtocolVersion = HttpVersion.Version11;
                string credentialError;
                NexusRequestCredential credential = _credentialProvider.GetCredential(out credentialError);
                if (!string.IsNullOrEmpty(credentialError) && (credential == null || !credential.IsConfigured))
                    return new NexusGraphQlResponse { ErrorMessage = credentialError };
                NexusRequestHeaders.ApplyJsonHeaders(request, credential);

                string body = _serializer.Serialize(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                {
                    string json = reader != null ? reader.ReadToEnd() : string.Empty;
                    var root = _serializer.DeserializeObject(json) as Dictionary<string, object>;
                    if (root == null)
                        return new NexusGraphQlResponse { ErrorMessage = "Invalid response payload." };

                    object errorsObj;
                    if (root.TryGetValue("errors", out errorsObj))
                    {
                        object[] errors = errorsObj as object[];
                        if (errors != null && errors.Length > 0)
                        {
                            var first = errors[0] as Dictionary<string, object>;
                            if (first != null)
                            {
                                object messageObj;
                                if (first.TryGetValue("message", out messageObj))
                                    return new NexusGraphQlResponse { ErrorMessage = Convert.ToString(messageObj) };
                            }

                            return new NexusGraphQlResponse { ErrorMessage = "Nexus API returned an error." };
                        }
                    }

                    object dataObj;
                    if (!root.TryGetValue("data", out dataObj))
                        return new NexusGraphQlResponse { ErrorMessage = "Response did not contain data." };

                    var data = dataObj as Dictionary<string, object>;
                    if (data == null)
                        return new NexusGraphQlResponse { ErrorMessage = "Response data was invalid." };

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
                    using (Stream stream = response != null ? response.GetResponseStream() : null)
                    using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                    {
                        string details = reader != null ? reader.ReadToEnd() : string.Empty;
                        if (!string.IsNullOrEmpty(details))
                            return new NexusGraphQlResponse { ErrorMessage = "Nexus request failed: " + details };
                    }
                }
                catch
                {
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

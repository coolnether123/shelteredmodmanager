using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Manager.Core;

namespace Manager.Core.Services
{
    internal sealed class NexusV3RestResult
    {
        public Dictionary<string, object> Data;
        public string ErrorMessage;
        public HttpStatusCode StatusCode;
    }

    internal sealed class NexusV3UploadResult
    {
        public bool Success;
        public string ErrorMessage;
        public string ETag;
    }

    internal sealed class NexusV3RestClient
    {
        private const string BaseUrl = "https://api.nexusmods.com/v3";
        private readonly string _apiKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public NexusV3RestClient(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
        }

        public NexusV3RestResult Get(string relativePath)
        {
            return Send("GET", relativePath, null);
        }

        public NexusV3RestResult Post(string relativePath, Dictionary<string, object> body)
        {
            return Send("POST", relativePath, body);
        }

        public NexusV3RestResult Put(string relativePath, Dictionary<string, object> body)
        {
            return Send("PUT", relativePath, body);
        }

        public NexusV3UploadResult PutFile(string presignedUrl, string filePath)
        {
            if (string.IsNullOrEmpty(presignedUrl))
                return new NexusV3UploadResult { ErrorMessage = "Upload URL is empty." };
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new NexusV3UploadResult { ErrorMessage = "Upload file does not exist." };

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                return PutBytes(presignedUrl, bytes);
            }
            catch (WebException ex)
            {
                return new NexusV3UploadResult { ErrorMessage = ReadWebException(ex, "Upload failed") };
            }
            catch (Exception ex)
            {
                return new NexusV3UploadResult { ErrorMessage = "Upload failed: " + ex.Message };
            }
        }

        public NexusV3UploadResult PutBytes(string presignedUrl, byte[] bytes)
        {
            if (string.IsNullOrEmpty(presignedUrl))
                return new NexusV3UploadResult { ErrorMessage = "Upload URL is empty." };
            if (bytes == null)
                return new NexusV3UploadResult { ErrorMessage = "Upload payload is empty." };

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(presignedUrl);
                request.Method = "PUT";
                request.ContentType = "application/octet-stream";
                request.ContentLength = bytes.Length;
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                request.KeepAlive = false;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return new NexusV3UploadResult
                    {
                        Success = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
                        ETag = response.Headers["ETag"]
                    };
                }
            }
            catch (WebException ex)
            {
                return new NexusV3UploadResult { ErrorMessage = ReadWebException(ex, "Upload failed") };
            }
            catch (Exception ex)
            {
                return new NexusV3UploadResult { ErrorMessage = "Upload failed: " + ex.Message };
            }
        }

        public NexusV3UploadResult PostXml(string presignedUrl, string xml)
        {
            if (string.IsNullOrEmpty(presignedUrl))
                return new NexusV3UploadResult { ErrorMessage = "Complete upload URL is empty." };

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(xml ?? string.Empty);
                var request = (HttpWebRequest)WebRequest.Create(presignedUrl);
                request.Method = "POST";
                request.ContentType = "application/xml";
                request.ContentLength = bytes.Length;
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                request.KeepAlive = false;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return new NexusV3UploadResult
                    {
                        Success = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent
                    };
                }
            }
            catch (WebException ex)
            {
                return new NexusV3UploadResult { ErrorMessage = ReadWebException(ex, "Complete upload failed") };
            }
            catch (Exception ex)
            {
                return new NexusV3UploadResult { ErrorMessage = "Complete upload failed: " + ex.Message };
            }
        }

        private NexusV3RestResult Send(string method, string relativePath, Dictionary<string, object> body)
        {
            try
            {
                string url = BaseUrl + relativePath;
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.KeepAlive = false;
                NexusRequestHeaders.ApplyJsonHeaders(request, _apiKey);

                if (body != null)
                {
                    string json = _serializer.Serialize(body);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    request.ContentType = "application/json";
                    request.ContentLength = bytes.Length;
                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                {
                    string json = reader != null ? reader.ReadToEnd() : string.Empty;
                    return new NexusV3RestResult
                    {
                        StatusCode = response.StatusCode,
                        Data = ParseData(json)
                    };
                }
            }
            catch (WebException ex)
            {
                HttpStatusCode status = 0;
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                    status = http.StatusCode;

                return new NexusV3RestResult
                {
                    StatusCode = status,
                    ErrorMessage = ReadWebException(ex, "Nexus v3 request failed")
                };
            }
            catch (Exception ex)
            {
                return new NexusV3RestResult { ErrorMessage = "Nexus v3 request failed: " + ex.Message };
            }
        }

        private Dictionary<string, object> ParseData(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, object>();

            var root = _serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
                return new Dictionary<string, object>();

            object data;
            if (root.TryGetValue("data", out data))
            {
                var dict = data as Dictionary<string, object>;
                if (dict != null)
                    return dict;
            }

            return root;
        }

        private static string ReadWebException(WebException ex, string prefix)
        {
            try
            {
                using (var response = ex.Response as HttpWebResponse)
                using (Stream stream = response != null ? response.GetResponseStream() : null)
                using (StreamReader reader = stream != null ? new StreamReader(stream) : null)
                {
                    string details = reader != null ? reader.ReadToEnd() : string.Empty;
                    if (!string.IsNullOrEmpty(details))
                        return prefix + ": " + details;
                }
            }
            catch
            {
            }

            return prefix + ": " + ex.Message;
        }
    }
}

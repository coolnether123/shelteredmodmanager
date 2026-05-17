using System.Net;
using Manager.Core;

namespace Manager.Core.Services
{
    internal static class NexusRequestHeaders
    {
        internal static void ApplyJsonHeaders(HttpWebRequest request, string apiKey)
        {
            if (request == null)
                return;

            request.Accept = "application/json";
            request.UserAgent = AppVersionInfo.UserAgent;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers["Application-Name"] = AppVersionInfo.ApplicationName;
            request.Headers["Application-Version"] = AppVersionInfo.NexusHeader;

            if (!string.IsNullOrEmpty(apiKey))
                request.Headers["APIKEY"] = apiKey;
        }
    }
}

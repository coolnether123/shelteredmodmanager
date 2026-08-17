using System.Net;
using Manager.Core;

namespace Manager.Core.Services
{
    internal static class NexusRequestHeaders
    {
        internal static void ApplyJsonHeaders(HttpWebRequest request, NexusRequestCredential credential)
        {
            if (request == null)
                return;

            request.Accept = "application/json";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            ApplyApplicationHeaders(request);

            if (credential == null)
                return;

            if (!string.IsNullOrEmpty(credential.BearerToken))
                request.Headers["Authorization"] = "Bearer " + credential.BearerToken;
        }

        internal static void ApplyApplicationHeaders(HttpWebRequest request)
        {
            if (request == null)
                return;

            request.UserAgent = AppVersionInfo.UserAgent;
            request.Headers["Application-Name"] = AppVersionInfo.ApplicationName;
            request.Headers["Application-Version"] = AppVersionInfo.NexusHeader;
        }
    }
}

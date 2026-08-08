using System.Net;

namespace Manager.Core.Services
{
    internal static class NexusRequestFailurePolicy
    {
        internal static bool IsDefinitelyUnsent(WebException exception)
        {
            if (exception == null || exception.Response != null)
                return false;

            return exception.Status == WebExceptionStatus.ConnectFailure ||
                exception.Status == WebExceptionStatus.NameResolutionFailure ||
                exception.Status == WebExceptionStatus.ProxyNameResolutionFailure ||
                exception.Status == WebExceptionStatus.SecureChannelFailure ||
                exception.Status == WebExceptionStatus.TrustFailure;
        }
    }
}

using System;
using System.Globalization;
using System.Net;

namespace Manager.Core.Services
{
    internal static class NexusRequestFailurePolicy
    {
        internal static string BuildRateLimitMessage(HttpWebResponse response, string quotaMessage)
        {
            string message = string.IsNullOrEmpty(quotaMessage)
                ? "Nexus rate limited the request."
                : quotaMessage;
            string retryAfter = response.Headers["Retry-After"];
            long seconds;
            if (long.TryParse(retryAfter, NumberStyles.None, CultureInfo.InvariantCulture, out seconds))
                return message + " Wait at least " + seconds.ToString(CultureInfo.InvariantCulture) + " seconds before retrying.";

            DateTime retryUtc;
            string[] httpDateFormats = { "r", "dddd, dd-MMM-yy HH':'mm':'ss 'GMT'", "ddd MMM d HH':'mm':'ss yyyy", "ddd MMM  d HH':'mm':'ss yyyy" };
            if (DateTime.TryParseExact(retryAfter, httpDateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out retryUtc))
                return message + " Retry no earlier than " + retryUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) + ".";

            return string.IsNullOrEmpty(quotaMessage) ? message + " Wait and try again." : message;
        }

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

using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    /// <summary>
    /// Parsed Nexus Mods manager-download authorization link.
    /// </summary>
    public sealed class NexusNxmLink
    {
        private const int MaximumLinkLength = 4096;

        public string GameDomain { get; private set; }
        public int ModId { get; private set; }
        public int FileId { get; private set; }
        public string DownloadKey { get; private set; }
        public long Expires { get; private set; }
        public int UserId { get; private set; }

        private NexusNxmLink()
        {
            GameDomain = string.Empty;
            DownloadKey = string.Empty;
        }

        public bool IsExpired
        {
            get { return Expires <= GetCurrentUnixTime(); }
        }

        public static bool TryParse(string rawUrl, out NexusNxmLink link, out string errorMessage)
        {
            link = null;
            errorMessage = null;

            string input = (rawUrl ?? string.Empty).Trim();
            if (input.Length == 0 || input.Length > MaximumLinkLength)
            {
                errorMessage = "The Nexus manager-download link is empty or too long.";
                return false;
            }

            Uri uri;
            if (!HasValidPercentEncoding(input) ||
                !Uri.TryCreate(input, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, "nxm", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "The link is not a valid nxm:// URL.";
                return false;
            }

            string[] segments = uri.AbsolutePath.Trim('/').Split('/');
            int modId;
            int fileId;
            if (segments.Length != 4 ||
                !string.Equals(segments[0], "mods", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[2], "files", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(segments[1], out modId) || modId <= 0 ||
                !int.TryParse(segments[3], out fileId) || fileId <= 0)
            {
                errorMessage = "The Nexus manager-download link does not identify a valid mod file.";
                return false;
            }

            string domain = (uri.Host ?? string.Empty).Trim().ToLowerInvariant();
            if (!IsSafeDomain(domain))
            {
                errorMessage = "The Nexus manager-download link has an invalid game domain.";
                return false;
            }

            Dictionary<string, string> query;
            try
            {
                query = ParseQuery(uri.Query);
            }
            catch (UriFormatException)
            {
                errorMessage = "The Nexus manager-download authorization is malformed.";
                return false;
            }
            string downloadKey;
            string expiresText;
            long expires;
            if (!query.TryGetValue("key", out downloadKey) || string.IsNullOrEmpty(downloadKey) ||
                downloadKey.Length > 512 ||
                !query.TryGetValue("expires", out expiresText) ||
                !long.TryParse(expiresText, out expires) || expires <= 0)
            {
                errorMessage = "The Nexus manager-download link is missing its short-lived authorization.";
                return false;
            }

            int userId = 0;
            string userIdText;
            if (query.TryGetValue("user_id", out userIdText) &&
                (!int.TryParse(userIdText, out userId) || userId < 0))
            {
                errorMessage = "The Nexus manager-download link has an invalid user id.";
                return false;
            }

            link = new NexusNxmLink
            {
                GameDomain = domain,
                ModId = modId,
                FileId = fileId,
                DownloadKey = downloadKey,
                Expires = expires,
                UserId = userId
            };

            if (link.IsExpired)
            {
                errorMessage = "The Nexus manager-download authorization has expired. Start the download again from the Nexus website.";
                link = null;
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> ParseQuery(string rawQuery)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string query = (rawQuery ?? string.Empty).TrimStart('?');
            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] parts = pairs[i].Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string name = Uri.UnescapeDataString(parts[0].Replace("+", " "));
                string value = Uri.UnescapeDataString(parts[1].Replace("+", " "));
                if (!string.IsNullOrEmpty(name))
                    values[name] = value;
            }
            return values;
        }

        private static bool IsSafeDomain(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }
            return true;
        }

        private static bool HasValidPercentEncoding(string value)
        {
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '%')
                    continue;
                if (i + 2 >= text.Length || !IsHex(text[i + 1]) || !IsHex(text[i + 2]))
                    return false;
                i += 2;
            }
            return true;
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }

        private static long GetCurrentUnixTime()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}

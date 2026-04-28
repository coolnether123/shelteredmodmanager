using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ModAPI.Saves
{
    internal static class NameSanitizer
    {
        // Avoid RegexOptions.Compiled for older Unity/Mono (throws ArgumentOutOfRangeException)
        private static readonly Regex Allowed = new Regex("[^a-zA-Z0-9 _-]");

        public static string SanitizeId(string input)
        {
            if (string.IsNullOrEmpty(input)) return "unnamed";
            var s = input.Trim();
            s = Allowed.Replace(s, "");
            if (s.Length > 64) s = s.Substring(0, 64);
            if (string.IsNullOrEmpty(s)) s = "unnamed";
            return s;
        }

        public static string SanitizeName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Unnamed";
            var s = input.Trim();
            s = Allowed.Replace(s, "");
            if (s.Length > 64) s = s.Substring(0, 64);
            if (string.IsNullOrEmpty(s)) s = "Unnamed";
            return s;
        }
    }
}

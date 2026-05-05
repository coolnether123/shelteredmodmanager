using System;
using System.Text;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioAuthoringActionCodec
    {
        public static string EncodeToken(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        public static string DecodeToken(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static string BuildTokenActionId(string prefix, string token)
        {
            if (string.IsNullOrEmpty(token))
                return prefix;

            return prefix + EncodeToken(token);
        }

        public static bool TryDecodeTokenActionId(string actionId, string prefix, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(prefix) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            token = DecodeToken(actionId.Substring(prefix.Length));
            return !string.IsNullOrEmpty(token);
        }
    }
}

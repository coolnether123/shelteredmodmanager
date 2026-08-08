using System;
using System.Text;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal static class ScenarioAutomationIdCodec
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

    }
}

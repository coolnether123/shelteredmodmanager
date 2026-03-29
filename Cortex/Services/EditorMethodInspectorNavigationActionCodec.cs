using System;
using System.Text;

namespace Cortex.Services
{
    internal static class EditorMethodInspectorNavigationActionCodec
    {
        private const string Prefix = "nav:symbol:";

        public static string Create(
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId)
        {
            return Prefix +
                Encode(symbolKind) + "|" +
                Encode(metadataName) + "|" +
                Encode(containingTypeName) + "|" +
                Encode(containingAssemblyName) + "|" +
                Encode(documentationCommentId);
        }

        public static bool TryParse(
            string activationId,
            out string symbolKind,
            out string metadataName,
            out string containingTypeName,
            out string containingAssemblyName,
            out string documentationCommentId)
        {
            symbolKind = string.Empty;
            metadataName = string.Empty;
            containingTypeName = string.Empty;
            containingAssemblyName = string.Empty;
            documentationCommentId = string.Empty;

            if (string.IsNullOrEmpty(activationId) || !activationId.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var payload = activationId.Substring(Prefix.Length);
            var parts = payload.Split('|');
            if (parts.Length != 5)
            {
                return false;
            }

            symbolKind = Decode(parts[0]);
            metadataName = Decode(parts[1]);
            containingTypeName = Decode(parts[2]);
            containingAssemblyName = Decode(parts[3]);
            documentationCommentId = Decode(parts[4]);
            return true;
        }

        private static string Encode(string value)
        {
            var safe = value ?? string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(safe));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

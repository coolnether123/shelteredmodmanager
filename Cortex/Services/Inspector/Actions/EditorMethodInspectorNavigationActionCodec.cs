using System;
using System.Text;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Inspector.Actions
{
    internal static class EditorMethodInspectorNavigationActionCodec
    {
        private const string SymbolPrefix = "nav:symbol:";
        private const string HarmonyPrefix = "nav:harmony:";

        public static string Create(
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId,
            string definitionDocumentPath,
            LanguageServiceRange definitionRange)
        {
            return SymbolPrefix +
                Encode(symbolKind) + "|" +
                Encode(metadataName) + "|" +
                Encode(containingTypeName) + "|" +
                Encode(containingAssemblyName) + "|" +
                Encode(documentationCommentId) + "|" +
                Encode(definitionDocumentPath) + "|" +
                Encode(definitionRange != null ? definitionRange.StartLine.ToString() : string.Empty) + "|" +
                Encode(definitionRange != null ? definitionRange.StartColumn.ToString() : string.Empty) + "|" +
                Encode(definitionRange != null ? definitionRange.EndLine.ToString() : string.Empty) + "|" +
                Encode(definitionRange != null ? definitionRange.EndColumn.ToString() : string.Empty);
        }

        public static string CreateHarmony(Cortex.Core.Models.HarmonyPatchNavigationTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            return HarmonyPrefix +
                Encode(target.AssemblyPath) + "|" +
                Encode(target.MetadataToken.ToString()) + "|" +
                Encode(target.DocumentPath) + "|" +
                Encode(target.CachePath) + "|" +
                Encode(target.DeclaringTypeName) + "|" +
                Encode(target.MethodName) + "|" +
                Encode(target.Signature) + "|" +
                Encode(target.DisplayName) + "|" +
                Encode(target.Line.ToString()) + "|" +
                Encode(target.Column.ToString()) + "|" +
                Encode(target.IsDecompilerTarget ? "1" : "0");
        }

        public static bool TryParse(
            string activationId,
            out string symbolKind,
            out string metadataName,
            out string containingTypeName,
            out string containingAssemblyName,
            out string documentationCommentId,
            out string definitionDocumentPath,
            out LanguageServiceRange definitionRange)
        {
            symbolKind = string.Empty;
            metadataName = string.Empty;
            containingTypeName = string.Empty;
            containingAssemblyName = string.Empty;
            documentationCommentId = string.Empty;
            definitionDocumentPath = string.Empty;
            definitionRange = null;

            if (string.IsNullOrEmpty(activationId) || !activationId.StartsWith(SymbolPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var payload = activationId.Substring(SymbolPrefix.Length);
            var parts = payload.Split('|');
            if (parts.Length != 5 && parts.Length != 10)
            {
                return false;
            }

            symbolKind = Decode(parts[0]);
            metadataName = Decode(parts[1]);
            containingTypeName = Decode(parts[2]);
            containingAssemblyName = Decode(parts[3]);
            documentationCommentId = Decode(parts[4]);
            if (parts.Length == 10)
            {
                definitionDocumentPath = Decode(parts[5]);
                definitionRange = new LanguageServiceRange
                {
                    StartLine = ParseInt(parts[6]),
                    StartColumn = ParseInt(parts[7]),
                    EndLine = ParseInt(parts[8]),
                    EndColumn = ParseInt(parts[9])
                };
            }
            return true;
        }

        public static bool TryParseHarmony(string activationId, out Cortex.Core.Models.HarmonyPatchNavigationTarget target)
        {
            target = null;
            if (string.IsNullOrEmpty(activationId) || !activationId.StartsWith(HarmonyPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var payload = activationId.Substring(HarmonyPrefix.Length);
            var parts = payload.Split('|');
            if (parts.Length != 11)
            {
                return false;
            }

            target = new Cortex.Core.Models.HarmonyPatchNavigationTarget
            {
                AssemblyPath = Decode(parts[0]),
                MetadataToken = ParseInt(parts[1]),
                DocumentPath = Decode(parts[2]),
                CachePath = Decode(parts[3]),
                DeclaringTypeName = Decode(parts[4]),
                MethodName = Decode(parts[5]),
                Signature = Decode(parts[6]),
                DisplayName = Decode(parts[7]),
                Line = ParseInt(parts[8]),
                Column = ParseInt(parts[9]),
                IsDecompilerTarget = string.Equals(Decode(parts[10]), "1", StringComparison.Ordinal)
            };
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

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(Decode(value), out parsed) ? parsed : 0;
        }
    }
}

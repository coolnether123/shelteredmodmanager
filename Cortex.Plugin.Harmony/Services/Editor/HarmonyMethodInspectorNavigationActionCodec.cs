using System;
using System.Text;
using Cortex.Core.Models;

namespace Cortex.Plugin.Harmony.Services.Editor
{
    internal static class HarmonyMethodInspectorNavigationActionCodec
    {
        private const string HarmonyPrefix = "nav:harmony:";

        public static string Create(HarmonyPatchNavigationTarget target)
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

        public static bool TryParse(string activationId, out HarmonyPatchNavigationTarget target)
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

            target = new HarmonyPatchNavigationTarget
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

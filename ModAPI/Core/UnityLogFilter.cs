using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Core
{
    internal static class UnityLogFilter
    {
        private const string DisableSuppressionOptionId = "ModAPI.DisableUnityLogSuppression";
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, int> SuppressedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static bool _optionRegistered;

        public static bool ShouldSuppress(string condition, LogType type)
        {
            if (IsAlwaysForwardedSeverity(type) || IsSuppressionDisabled())
                return false;

            string message = condition ?? string.Empty;
            if (message.Length == 0) return false;

            string key;
            if (!TryGetSuppressionKey(message, out key))
                return false;

            RecordSuppressed(key);
            return true;
        }

        public static bool ShouldSuppressNormalized(string condition, LogType type, UnityLogNormalization normalization)
        {
            if (normalization == null || !normalization.Suppress)
                return false;

            if (IsAlwaysForwardedSeverity(type) || IsSuppressionDisabled())
                return false;

            string key = !string.IsNullOrEmpty(normalization.OnceKey)
                ? normalization.OnceKey
                : (!string.IsNullOrEmpty(normalization.Source) ? normalization.Source : "UnityLog.Normalized");
            RecordSuppressed(key);
            return true;
        }

        public static void LogSuppressionSummary(string reason)
        {
            KeyValuePair<string, int>[] snapshot;
            lock (Sync)
            {
                if (SuppressedCounts.Count == 0)
                    return;

                snapshot = new KeyValuePair<string, int>[SuppressedCounts.Count];
                int index = 0;
                foreach (KeyValuePair<string, int> pair in SuppressedCounts)
                    snapshot[index++] = pair;
                SuppressedCounts.Clear();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                MMLog.WriteInfo("[UnityLogFilter] Suppressed " + snapshot[i].Value
                    + " benign Unity log message(s) for '" + snapshot[i].Key
                    + "' during " + (reason ?? "runtime") + ".");
            }
        }

        private static bool IsAlwaysForwardedSeverity(LogType type)
        {
            return type == LogType.Exception || type == LogType.Error || type == LogType.Assert;
        }

        private static bool IsSuppressionDisabled()
        {
            RegisterOption();
            return ManagerBooleanOptions.GetBool(DisableSuppressionOptionId, false);
        }

        private static void RegisterOption()
        {
            if (_optionRegistered)
                return;

            lock (Sync)
            {
                if (_optionRegistered)
                    return;

                ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
                {
                    Id = DisableSuppressionOptionId,
                    Owner = "ModAPI",
                    Label = "Disable Unity Log Suppression",
                    Description = "Mirrors all Unity warnings/logs into SMM logs. Errors, asserts, and exceptions are always mirrored regardless of this option.",
                    DefaultValue = false,
                    RequiresRestart = true,
                    SortOrder = 20
                });
                _optionRegistered = true;
            }
        }

        private static bool TryGetSuppressionKey(string message, out string key)
        {
            key = null;

            if (IsBenignAchievementNoise(message))
                key = "UnityLog.AchievementNoise";
            else if (IsBenignUnityAudioUpgradeNoise(message))
                key = "UnityLog.AudioUpgradeNoise";
            else if (IsBenignMissingPlatformScriptNoise(message))
                key = "UnityLog.MissingPlatformScriptNoise";
            else if (IsBenignEpicLoginMarker(message))
                key = "UnityLog.EpicLoginMarker";

            return !string.IsNullOrEmpty(key);
        }

        private static void RecordSuppressed(string key)
        {
            if (string.IsNullOrEmpty(key))
                key = "UnityLog.Unknown";

            lock (Sync)
            {
                int count;
                SuppressedCounts.TryGetValue(key, out count);
                SuppressedCounts[key] = count + 1;
            }
        }

        private static bool IsBenignAchievementNoise(string message)
        {
            return message.StartsWith("Achievement:", StringComparison.Ordinal)
                || message.StartsWith("Already achieved:", StringComparison.Ordinal);
        }

        private static bool IsBenignUnityAudioUpgradeNoise(string message)
        {
            return string.Equals(message, "minVolume is not supported anymore. Use min-, maxDistance and rolloffMode instead.", StringComparison.Ordinal)
                || string.Equals(message, "maxVolume is not supported anymore. Use min-, maxDistance and rolloffMode instead.", StringComparison.Ordinal)
                || string.Equals(message, "rolloffFactor is not supported anymore. Use min-, maxDistance and rolloffMode instead.", StringComparison.Ordinal);
        }

        private static bool IsBenignMissingPlatformScriptNoise(string message)
        {
            return string.Equals(message, "The referenced script on this Behaviour is missing!", StringComparison.Ordinal)
                || string.Equals(message, "The referenced script on this Behaviour (Game Object 'SteamWorks') is missing!", StringComparison.Ordinal);
        }

        private static bool IsBenignEpicLoginMarker(string message)
        {
            return message.StartsWith("Login callback ", StringComparison.Ordinal)
                && message.IndexOf("====", StringComparison.Ordinal) >= 0;
        }
    }
}

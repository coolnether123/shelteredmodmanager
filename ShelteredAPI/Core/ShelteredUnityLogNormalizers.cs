using System;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Core
{
    internal static class ShelteredUnityLogNormalizers
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            UnityLogNormalizationRegistry.Register(TryNormalizeShelteredKnownIssue);
            UnityLogNormalizationRegistry.Register(TrySuppressShelteredSettingsColliderNoise);
        }

        private static bool TryNormalizeShelteredKnownIssue(
            string condition,
            string stackTrace,
            LogType type,
            out UnityLogNormalization normalization)
        {
            normalization = null;

            if (!IsEpicAchievementStatsIssue(condition, stackTrace, type))
                return false;

            normalization = new UnityLogNormalization
            {
                Level = MMLog.LogLevel.Warning,
                Source = "Sheltered.EOS",
                Message = "Known Sheltered Epic achievement stats callback issue: EOS returned an unexpected stats result and the vanilla AchievementHandler_EOS.QueryStatsCallback threw IndexOutOfRangeException; mod loading is unaffected.",
                OnceKey = "ShelteredUnityLog.EpicAchievementStatsIssue"
            };
            return true;
        }

        private static bool TrySuppressShelteredSettingsColliderNoise(
            string condition,
            string stackTrace,
            LogType type,
            out UnityLogNormalization normalization)
        {
            normalization = null;

            string message = condition ?? string.Empty;
            if (!message.StartsWith("BoxColliders does not support negative scale or size.", StringComparison.Ordinal)
                || message.IndexOf("UI Root/" + "Settings" + "PCPanel/MenuParent/", StringComparison.Ordinal) < 0
                || message.IndexOf("/left_arrow", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            normalization = new UnityLogNormalization
            {
                Suppress = true,
                OnceKey = "ShelteredUnityLog.SettingsColliderNoise"
            };
            return true;
        }

        private static bool IsEpicAchievementStatsIssue(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return false;

            var message = condition ?? string.Empty;
            var stack = stackTrace ?? string.Empty;

            return message.IndexOf("IndexOutOfRangeException", StringComparison.Ordinal) >= 0
                && stack.IndexOf("AchievementHandler_EOS.QueryStatsCallback", StringComparison.Ordinal) >= 0
                && stack.IndexOf("EOSManager.Update", StringComparison.Ordinal) >= 0;
        }
    }
}

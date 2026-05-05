using System;
using UnityEngine;

namespace ModAPI.Core
{
    internal static class UnityLogFilter
    {
        public static bool ShouldSuppress(string condition, LogType type)
        {
            var message = condition ?? string.Empty;
            if (message.Length == 0) return false;

            return IsBenignAchievementNoise(message)
                || IsBenignUnityAudioUpgradeNoise(message)
                || IsBenignMissingPlatformScriptNoise(message)
                || IsBenignVanillaSettingsColliderNoise(message)
                || IsBenignEpicLoginMarker(message);
        }

        private static bool IsBenignAchievementNoise(string message)
        {
            return message.IndexOf("Achievement:", StringComparison.Ordinal) >= 0
                || message.IndexOf("Already achieved:", StringComparison.Ordinal) >= 0;
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

        private static bool IsBenignVanillaSettingsColliderNoise(string message)
        {
            return message.StartsWith("BoxColliders does not support negative scale or size.", StringComparison.Ordinal)
                && message.IndexOf("UI Root/" + "Settings" + "PCPanel/MenuParent/", StringComparison.Ordinal) >= 0
                && message.IndexOf("/left_arrow", StringComparison.Ordinal) >= 0;
        }

        private static bool IsBenignEpicLoginMarker(string message)
        {
            return message.StartsWith("Login callback ", StringComparison.Ordinal)
                && message.IndexOf("====", StringComparison.Ordinal) >= 0;
        }
    }
}

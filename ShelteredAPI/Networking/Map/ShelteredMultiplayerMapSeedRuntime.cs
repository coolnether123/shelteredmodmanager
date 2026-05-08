using System;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerMapSeedRuntime
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.MapSeed";

        private static int _lastLoggedMapSeed;
        private static string _lastLoggedMapReason = string.Empty;

        public static void ApplyMapSeed(ExpeditionMap map, string reason)
        {
            if (map == null)
                return;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive || string.IsNullOrEmpty(context.SessionId))
                return;

            int masterSeed;
            string error;
            if (!ShelteredMultiplayerSessionSeed.TryApply(context.SessionId, out masterSeed, out error))
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, LogSource,
                    "Could not apply multiplayer map seed for " + (reason ?? string.Empty) + ": " + error);
                return;
            }

            map.randomSeed = masterSeed;
            UnityEngine.Random.InitState(masterSeed);

            if (_lastLoggedMapSeed != masterSeed || !string.Equals(_lastLoggedMapReason, reason ?? string.Empty, StringComparison.Ordinal))
            {
                _lastLoggedMapSeed = masterSeed;
                _lastLoggedMapReason = reason ?? string.Empty;
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Applied multiplayer master seed " + masterSeed + " to ExpeditionMap.randomSeed for "
                    + _lastLoggedMapReason + ".");
            }
        }
    }
}

using System;
using ModAPI.Core;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerBunkerAnchorRuntime
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.BunkerAnchor";

        private static Vector2 _cachedShelterWorldPosition;
        private static bool _hasCachedShelterWorldPosition;
        private static Vector3 _cachedShelterMapPosition;
        private static bool _hasCachedShelterMapPosition;
        private static Vector2 _lastLoggedShelterWorldPosition = new Vector2(float.MinValue, float.MinValue);
        private static Vector3 _lastLoggedShelterMapPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        private static Vector2 _lastLoggedRedirectedWorldPosition = new Vector2(float.MinValue, float.MinValue);

        public static void CacheActiveBunkerPosition(string reason)
        {
            Vector2 worldPosition;
            if (TryGetActiveBunkerWorldPosition(out worldPosition))
            {
                _cachedShelterWorldPosition = worldPosition;
                _hasCachedShelterWorldPosition = true;

                if (Vector2.Distance(_lastLoggedShelterWorldPosition, worldPosition) > 0.001f)
                {
                    _lastLoggedShelterWorldPosition = worldPosition;
                    MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                        "Cached active multiplayer bunker world position (" + worldPosition.x.ToString("F1") + ", "
                        + worldPosition.y.ToString("F1") + "). Reason=" + (reason ?? string.Empty) + ".");
                }
            }

            Vector3 mapPosition;
            if (!TryGetActiveBunkerMapPosition(out mapPosition))
                return;

            _cachedShelterMapPosition = mapPosition;
            _hasCachedShelterMapPosition = true;

            if (Vector3.Distance(_lastLoggedShelterMapPosition, mapPosition) > 0.001f)
            {
                _lastLoggedShelterMapPosition = mapPosition;
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Cached active multiplayer bunker map position (" + mapPosition.x.ToString("F1") + ", "
                    + mapPosition.y.ToString("F1") + ") for vanilla shelter-anchor call sites. Reason="
                    + (reason ?? string.Empty) + ".");
            }
        }

        public static void RedirectShelterOriginWorldPosition(ref Vector2 worldPosition)
        {
            if (worldPosition.sqrMagnitude > 0.0001f)
                return;

            Vector2 activeWorldPosition;
            if (!TryGetActiveBunkerWorldPosition(out activeWorldPosition))
            {
                if (!_hasCachedShelterWorldPosition)
                    return;

                activeWorldPosition = _cachedShelterWorldPosition;
            }

            if (activeWorldPosition.sqrMagnitude <= 0.0001f)
                return;

            worldPosition = activeWorldPosition;
            if (Vector2.Distance(_lastLoggedRedirectedWorldPosition, activeWorldPosition) > 0.001f)
            {
                _lastLoggedRedirectedWorldPosition = activeWorldPosition;
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Redirected vanilla shelter origin to active multiplayer bunker world position ("
                    + activeWorldPosition.x.ToString("F1") + ", " + activeWorldPosition.y.ToString("F1") + ").");
            }
        }

        public static Vector3 GetActiveBunkerMapPixels()
        {
            if (ShouldSkipMapLookup())
                return _hasCachedShelterMapPosition ? _cachedShelterMapPosition : Vector3.zero;

            Vector3 mapPosition;
            if (TryGetActiveBunkerMapPosition(out mapPosition))
            {
                _cachedShelterMapPosition = mapPosition;
                _hasCachedShelterMapPosition = true;
                return mapPosition;
            }

            return _hasCachedShelterMapPosition ? _cachedShelterMapPosition : Vector3.zero;
        }

        internal static bool TryGetActiveBunkerWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            if (ModRuntime.IsQuitting)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive || context.BunkerAssignments.Length == 0)
                return false;

            int bunkerOwnerId = ShelteredMultiplayerBunkerAssignments.ResolveBunkerOwnerId(
                context.BunkerAssignments,
                context.LocalPlayerId);

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record != null && record.BunkerOwnerId == bunkerOwnerId)
                {
                    worldPosition = record.Position;
                    return true;
                }
            }

            worldPosition = ShelteredBunkers.GetActiveBunkerWorldPosition();
            return worldPosition.sqrMagnitude > 0.0001f;
        }

        private static bool TryGetActiveBunkerMapPosition(out Vector3 mapPosition)
        {
            mapPosition = Vector3.zero;

            if (ShouldSkipMapLookup())
                return false;

            Vector2 worldPosition;
            if (!TryGetActiveBunkerWorldPosition(out worldPosition))
                return false;

            if (ExplorationManager.Instance == null || ExplorationManager.Instance.mapSourceSprite == null)
                return false;

            mapPosition = ShelteredBunkers.GetActiveBunkerMapPixels();
            return true;
        }

        private static bool ShouldSkipMapLookup()
        {
            if (ModRuntime.IsQuitting)
                return true;

            try
            {
                return LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing;
            }
            catch
            {
                return false;
            }
        }
    }
}

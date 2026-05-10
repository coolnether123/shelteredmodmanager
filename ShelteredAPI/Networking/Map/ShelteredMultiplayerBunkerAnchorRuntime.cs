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
        private static Vector2 _lastLoggedMapGenerationWorldPosition = new Vector2(float.MinValue, float.MinValue);
        private static bool _hasValidatedAnchor;
        private static ShelteredMultiplayerMapAnchorValidationResult _validatedAnchor;
        private static bool _anchorOverrideDisabled;
        private static string _lastValidationLogKey = string.Empty;
        private static int _mapGenerationDepth;
        private static Vector2 _cachedMapGenerationWorldPosition;
        private static bool _hasCachedMapGenerationWorldPosition;

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

            ShelteredMultiplayerMapAnchorValidationResult validation;
            if (!TryGetValidatedActiveBunkerAnchor(out validation, reason))
                return;

            _cachedShelterWorldPosition = validation.ChosenWorldPosition;
            _hasCachedShelterWorldPosition = true;
            _cachedShelterMapPosition = validation.ChosenMapPixels;
            _hasCachedShelterMapPosition = true;
            _anchorOverrideDisabled = false;

            if (Vector3.Distance(_lastLoggedShelterMapPosition, validation.ChosenMapPixels) > 0.001f)
            {
                _lastLoggedShelterMapPosition = validation.ChosenMapPixels;
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Cached validated multiplayer bunker anchor. Assigned=("
                    + validation.AssignedWorldPosition.x.ToString("F1") + ", "
                    + validation.AssignedWorldPosition.y.ToString("F1") + "), requestedGrid=("
                    + validation.RequestedGridX + ", " + validation.RequestedGridY + "), chosenGrid=("
                    + validation.ChosenGridX + ", " + validation.ChosenGridY + "), mapPixels=("
                    + validation.ChosenMapPixels.x.ToString("F1") + ", "
                    + validation.ChosenMapPixels.y.ToString("F1") + "), fallback="
                    + validation.IsFallback + ", reason=" + validation.Reason + ", call="
                    + (reason ?? string.Empty) + ".");
            }
        }

        public static void ResetValidatedAnchor(string reason)
        {
            _hasValidatedAnchor = false;
            _validatedAnchor = null;
            _anchorOverrideDisabled = false;
            _lastValidationLogKey = string.Empty;
            _mapGenerationDepth = 0;
            _hasCachedMapGenerationWorldPosition = false;
        }

        public static void BeginMapGeneration(string reason)
        {
            if (!IsMultiplayerAnchorActive())
                return;

            _mapGenerationDepth++;

            Vector2 worldPosition;
            if (!TryGetCanonicalMapBunkerWorldPosition(out worldPosition))
                return;

            _cachedMapGenerationWorldPosition = worldPosition;
            _hasCachedMapGenerationWorldPosition = true;

            if (Vector2.Distance(_lastLoggedMapGenerationWorldPosition, worldPosition) > 0.001f)
            {
                _lastLoggedMapGenerationWorldPosition = worldPosition;
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Using canonical multiplayer map-generation bunker world position ("
                    + worldPosition.x.ToString("F1") + ", " + worldPosition.y.ToString("F1")
                    + "). Reason=" + (reason ?? string.Empty) + ".");
            }
        }

        public static void EndMapGeneration(string reason)
        {
            if (_mapGenerationDepth > 0)
                _mapGenerationDepth--;
        }

        public static void RedirectShelterOriginWorldPosition(ref Vector2 worldPosition)
        {
            if (worldPosition.sqrMagnitude > 0.0001f)
                return;

            if (!IsMultiplayerAnchorActive())
                return;

            if (_anchorOverrideDisabled)
                return;

            if (_mapGenerationDepth > 0)
            {
                Vector2 mapGenerationWorldPosition;
                if (_hasCachedMapGenerationWorldPosition)
                {
                    mapGenerationWorldPosition = _cachedMapGenerationWorldPosition;
                }
                else if (!TryGetCanonicalMapBunkerWorldPosition(out mapGenerationWorldPosition))
                {
                    return;
                }

                if (mapGenerationWorldPosition.sqrMagnitude <= 0.0001f)
                    return;

                worldPosition = mapGenerationWorldPosition;
                if (Vector2.Distance(_lastLoggedRedirectedWorldPosition, mapGenerationWorldPosition) > 0.001f)
                {
                    _lastLoggedRedirectedWorldPosition = mapGenerationWorldPosition;
                    MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                        "Redirected vanilla shelter origin to canonical multiplayer map-generation bunker world position ("
                        + mapGenerationWorldPosition.x.ToString("F1") + ", "
                        + mapGenerationWorldPosition.y.ToString("F1") + ").");
                }

                return;
            }

            if (_hasValidatedAnchor && _validatedAnchor != null && _validatedAnchor.IsValid)
            {
                worldPosition = _validatedAnchor.ChosenWorldPosition;
                return;
            }

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
            if (!IsMultiplayerAnchorActive())
                return Vector3.zero;

            if (ShouldSkipMapLookup())
                return _hasCachedShelterMapPosition ? _cachedShelterMapPosition : Vector3.zero;

            ShelteredMultiplayerMapAnchorValidationResult validation;
            if (TryGetValidatedActiveBunkerAnchor(out validation, "GetActiveBunkerMapPixels"))
            {
                _cachedShelterWorldPosition = validation.ChosenWorldPosition;
                _hasCachedShelterWorldPosition = true;
                _cachedShelterMapPosition = validation.ChosenMapPixels;
                _hasCachedShelterMapPosition = true;
                _anchorOverrideDisabled = false;
                return validation.ChosenMapPixels;
            }

            if (_anchorOverrideDisabled)
                return Vector3.zero;

            return _hasCachedShelterMapPosition ? _cachedShelterMapPosition : Vector3.zero;
        }

        internal static bool TryGetActiveBunkerWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            if (ModRuntime.IsQuitting)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive || context.BunkerAssignments == null || context.BunkerAssignments.Length == 0)
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

        internal static bool TryGetCanonicalMapBunkerWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            if (ModRuntime.IsQuitting)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive || context.BunkerAssignments == null || context.BunkerAssignments.Length == 0)
                return false;

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record != null && record.BunkerOwnerId == 0)
                {
                    worldPosition = record.Position;
                    return true;
                }
            }

            BunkerDefinition primary = ShelteredBunkers.GetBunker(0) ?? ShelteredBunkers.GetPrimaryBunker();
            if (primary != null)
            {
                worldPosition = primary.Position;
                return true;
            }

            return TryGetActiveBunkerWorldPosition(out worldPosition);
        }

        internal static bool TryGetValidatedActiveBunkerAnchor(
            out ShelteredMultiplayerMapAnchorValidationResult validation,
            string reason)
        {
            validation = null;

            if (!IsMultiplayerAnchorActive())
                return false;

            if (ShouldSkipMapLookup())
                return false;

            validation = ShelteredMultiplayerMapAnchorValidator.ValidateActiveBunker(reason);
            _validatedAnchor = validation;
            _hasValidatedAnchor = validation != null;

            if (validation == null || !validation.IsValid)
            {
                if (ShouldDisableAnchorOverride(validation))
                {
                    _anchorOverrideDisabled = true;
                    LogValidationFailure(validation, reason);
                }

                return false;
            }

            LogValidationFallback(validation, reason);
            return true;
        }

        internal static bool IsMultiplayerAnchorActive()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null && context.IsMultiplayerActive;
        }

        private static bool ShouldDisableAnchorOverride(ShelteredMultiplayerMapAnchorValidationResult validation)
        {
            if (validation == null)
                return false;

            return validation.HasExpeditionMap
                && validation.HasMapRegionSource
                && validation.ValidRegionCount == 0;
        }

        private static void LogValidationFallback(
            ShelteredMultiplayerMapAnchorValidationResult validation,
            string reason)
        {
            if (validation == null || !validation.IsFallback)
                return;

            string key = "fallback:" + validation.BunkerOwnerId + ":"
                + validation.RequestedGridX + "," + validation.RequestedGridY + "->"
                + validation.ChosenGridX + "," + validation.ChosenGridY + ":"
                + (validation.Reason ?? string.Empty);
            if (string.Equals(_lastValidationLogKey, key, StringComparison.Ordinal))
                return;

            _lastValidationLogKey = key;
            MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, LogSource,
                "Multiplayer bunker anchor fallback selected nearest valid map region. Assigned=("
                + validation.AssignedWorldPosition.x.ToString("F1") + ", "
                + validation.AssignedWorldPosition.y.ToString("F1") + "), requestedGrid=("
                + validation.RequestedGridX + ", " + validation.RequestedGridY + "), chosenGrid=("
                + validation.ChosenGridX + ", " + validation.ChosenGridY + "), validRegions="
                + validation.ValidRegionCount + ", reason=" + validation.Reason + ", call="
                + (reason ?? string.Empty) + ".");
        }

        private static void LogValidationFailure(
            ShelteredMultiplayerMapAnchorValidationResult validation,
            string reason)
        {
            string key = validation != null
                ? "disabled:" + validation.BunkerOwnerId + ":" + (validation.Reason ?? string.Empty)
                : "disabled:null";
            if (string.Equals(_lastValidationLogKey, key, StringComparison.Ordinal))
                return;

            _lastValidationLogKey = key;
            MMLog.WarnOnce("ShelteredMultiplayerBunkerAnchor.Disabled." + key,
                "Disabled multiplayer bunker anchor override because no valid ExpeditionMap regions were available. "
                + "Reason=" + (validation != null ? validation.Reason : "unknown")
                + ", validRegions=" + (validation != null ? validation.ValidRegionCount.ToString() : "0")
                + ", call=" + (reason ?? string.Empty) + ".");
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

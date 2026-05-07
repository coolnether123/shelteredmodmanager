using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class ShelteredMultiplayerMapSeedRuntime
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.MapSeed";

        private static int _lastLoggedMapSeed;
        private static string _lastLoggedMapReason = string.Empty;
        private static Vector2 _cachedShelterWorldPosition;
        private static bool _hasCachedShelterWorldPosition;
        private static Vector3 _cachedShelterMapPosition;
        private static bool _hasCachedShelterMapPosition;
        private static Vector2 _lastLoggedShelterWorldPosition = new Vector2(float.MinValue, float.MinValue);
        private static Vector3 _lastLoggedShelterMapPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        private static Vector2 _lastLoggedRedirectedWorldPosition = new Vector2(float.MinValue, float.MinValue);
        private static readonly FieldInfo MapScratchpadField =
            AccessTools.Field(typeof(ExpeditionMap), "m_mapScratchpad");

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

        public static void ForceActiveBunkerShelterCell(ExpeditionMap map)
        {
            if (map == null)
                return;

            Vector2 worldPosition;
            if (!TryGetActiveBunkerWorldPosition(out worldPosition))
                return;
            if (worldPosition.sqrMagnitude <= 0.0001f)
                return;

            Array scratchpad = MapScratchpadField != null ? MapScratchpadField.GetValue(map) as Array : null;
            if (scratchpad == null)
                return;

            ExpeditionMap.GridRef gridRef = map.WorldPosToGridRef(worldPosition);
            if (gridRef.x < 0 || gridRef.x >= scratchpad.GetLength(0) || gridRef.y < 0 || gridRef.y >= scratchpad.GetLength(1))
                return;

            object cell = scratchpad.GetValue(gridRef.x, gridRef.y);
            if (cell == null)
                return;

            FieldInfo typeField = AccessTools.Field(cell.GetType(), "type");
            FieldInfo categoryField = AccessTools.Field(cell.GetType(), "category");
            FieldInfo alwaysVisibleField = AccessTools.Field(cell.GetType(), "alwaysVisible");

            if (typeField != null)
                typeField.SetValue(cell, MapRegion.Topography.Shelter);
            if (categoryField != null)
                categoryField.SetValue(cell, "Shelter");
            if (alwaysVisibleField != null)
                alwaysVisibleField.SetValue(cell, true);
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

        private static bool TryGetActiveBunkerWorldPosition(out Vector2 worldPosition)
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

    internal static class ShelteredMultiplayerShelterAnchorTranspiler
    {
        private static readonly MethodInfo GameModeGetInstance =
            AccessTools.PropertyGetter(typeof(GameModeManager), "instance");
        private static readonly MethodInfo ShelterMapGetter =
            AccessTools.PropertyGetter(typeof(GameModeManager), "shelterMapWorldPosition");
        private static readonly MethodInfo GetActiveBunkerMapPixels =
            AccessTools.Method(typeof(ShelteredMultiplayerMapSeedRuntime),
                "GetActiveBunkerMapPixels",
                Type.EmptyTypes);

        public static IEnumerable<CodeInstruction> ReplaceShelterMapGetterPairs(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            for (int i = 0; i < code.Count - 1; i++)
            {
                if (!IsCallTo(code[i], GameModeGetInstance))
                    continue;
                if (!IsCallTo(code[i + 1], ShelterMapGetter))
                    continue;

                code[i] = CopyMeta(code[i], new CodeInstruction(OpCodes.Call, GetActiveBunkerMapPixels));
                code[i + 1] = CopyMeta(code[i + 1], new CodeInstruction(OpCodes.Nop));
                i++;
            }

            return code;
        }

        private static bool IsCallTo(CodeInstruction instruction, MethodInfo method)
        {
            if (instruction == null || method == null)
                return false;
            if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                return false;

            MethodInfo operand = instruction.operand as MethodInfo;
            return operand != null && operand == method;
        }

        private static CodeInstruction CopyMeta(CodeInstruction original, CodeInstruction replacement)
        {
            if (original == null || replacement == null)
                return replacement;
            if (original.labels != null && original.labels.Count > 0)
                replacement.labels.AddRange(original.labels);
            if (original.blocks != null && original.blocks.Count > 0)
                replacement.blocks.AddRange(original.blocks);
            return replacement;
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerMapSeed",
        TargetBehavior = "Active multiplayer sessions seed vanilla expedition map generation from the shared master seed.",
        FailureMode = "Peers can create maps from local or clock-derived random seeds before the session seed is active.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer map seed patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ShelteredMultiplayerMapSeedPatches
    {
        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPrefix]
        private static void CreateMapPrefix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateMap",
                    "Multiplayer map seed hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateStasisMap")]
        [HarmonyPrefix]
        private static void CreateStasisMapPrefix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateStasisMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateStasisMap",
                    "Multiplayer stasis map seed hook failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerMapShelterOrigin",
        TargetBehavior = "Vanilla expedition map generation treats the active multiplayer bunker as the shelter origin.",
        FailureMode = "Map generation paths hardcoded to world origin can still build shelter-adjacent regions around the center of the map.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer shelter-origin patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMap))]
    internal static class ShelteredMultiplayerMapShelterOriginPatches
    {
        [HarmonyPatch("WorldPosToGridRef")]
        [HarmonyPrefix]
        private static void WorldPosToGridRefPrefix(ref Vector2 worldPos)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.RedirectShelterOriginWorldPosition(ref worldPos);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapShelterOrigin.WorldPosToGridRef",
                    "Multiplayer shelter-origin redirect failed: " + ex.Message);
            }
        }

        [HarmonyPatch("PlaceShelters")]
        [HarmonyPostfix]
        private static void PlaceSheltersPostfix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.ForceActiveBunkerShelterCell(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapShelterOrigin.PlaceShelters",
                    "Multiplayer shelter-cell placement failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerShelterMapAnchor",
        TargetBehavior = "Vanilla map UI and expedition code read the active multiplayer bunker position instead of the default center shelter anchor.",
        FailureMode = "Every peer sees and routes from a center-map shelter even after multiplayer bunker assignments are applied.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer shelter map-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(UI_ExpeditionMap))]
    internal static class ShelteredMultiplayerExpeditionMapUiAnchorPatches
    {
        [HarmonyPatch("UpdateMapSymbols")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateMapSymbolsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }

        [HarmonyPatch("OnEnable")]
        [HarmonyPrefix]
        private static void ExpeditionMapOnEnablePrefix()
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.CacheActiveBunkerPosition("UI_ExpeditionMap.OnEnable");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.OnEnable",
                    "Multiplayer shelter map-anchor UI hook failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerExpeditionRouteAnchor",
        TargetBehavior = "Multiplayer expedition route calculations read the active bunker map position instead of the default center shelter anchor.",
        FailureMode = "Expedition routes and open-ground distance checks can still measure from the center-map shelter.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer expedition route-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExplorationParty))]
    internal static class ShelteredMultiplayerExplorationPartyAnchorPatches
    {
        [HarmonyPatch("SetRoute")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetRouteTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }

        [HarmonyPatch("OpenGroundEncounterCheck")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OpenGroundEncounterCheckTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerExpeditionPanelAnchor",
        TargetBehavior = "Multiplayer expedition route-distance UI reads the active bunker map position instead of the default center shelter anchor.",
        FailureMode = "The expedition setup panel can report distances from the center-map shelter.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer expedition panel-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMainPanelNew))]
    internal static class ShelteredMultiplayerExpeditionPanelAnchorPatches
    {
        [HarmonyPatch("CalculateRouteDistance")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CalculateRouteDistanceTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }
    }
}

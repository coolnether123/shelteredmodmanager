using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    [PatchPolicy(PatchDomain.World, "HomeShelterExpeditions",
        TargetBehavior = "Route expedition departures, returns, recalls and map UI through the registered home shelter.",
        FailureMode = "Parties leave from and return to the vanilla map center while the home shelter is elsewhere.",
        RollbackStrategy = "Disable the World patch domain or remove the home-shelter expedition patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch]
    internal static class HomeShelterExpeditionPatches
    {
        private static readonly MethodInfo ShelterMapPositionGetter =
            AccessTools.PropertyGetter(typeof(GameModeManager), "shelterMapWorldPosition");
        private static readonly MethodInfo ShelterMapPositionHook =
            AccessTools.Method(typeof(HomeShelterExpeditionRuntime), nameof(HomeShelterExpeditionRuntime.ShelterMapPosition));

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "CalculateRouteDistance")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CalculateRouteDistanceTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            return RedirectShelterMapPosition(instructions, original);
        }

        [HarmonyPatch(typeof(ExplorationParty), "SetRoute")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetRouteTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            return RedirectShelterMapPosition(instructions, original);
        }

        [HarmonyPatch(typeof(ExplorationParty), "OpenGroundEncounterCheck")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OpenGroundEncounterCheckTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = new List<CodeInstruction>(RedirectShelterMapPosition(instructions, original));
            MethodInfo distance = AccessTools.Method(typeof(HomeShelterExpeditionPatches), "SurvivalDistanceFromHome");
            var matches = new List<int>();
            for (int i = 0; i + 1 < code.Count; i++)
            {
                FieldInfo field = code[i].operand as FieldInfo;
                MethodInfo call = code[i + 1].operand as MethodInfo;
                if (code[i].opcode == System.Reflection.Emit.OpCodes.Ldflda && field != null && field.Name == "m_location"
                    && call != null && call.DeclaringType == typeof(Vector2) && call.Name == "get_magnitude")
                {
                    matches.Add(i + 1);
                }
            }
            if (matches.Count != 1)
            {
                MMLog.WarnOnce("HomeShelter.EncounterDistance", "Expected one Survival encounter distance read; leaving distance unchanged.");
                return code;
            }
            code[matches[0]].opcode = System.Reflection.Emit.OpCodes.Call;
            code[matches[0]].operand = distance;
            return code;
        }

        private static float SurvivalDistanceFromHome(ref Vector2 location)
        {
            Vector2 home;
            return HomeShelterExpeditionRuntime.TryGetHomeWorld(out home) ? (location - home).magnitude : location.magnitude;
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "UpdateMapSymbols")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateMapSymbolsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            return RedirectShelterMapPosition(instructions, original);
        }

        [HarmonyPatch(typeof(ExplorationParty), "Awake")]
        [HarmonyPostfix]
        private static void PartyAwakePostfix(ExplorationParty __instance)
        {
            HomeShelterExpeditionRuntime.OnPartyAwake(__instance);
        }

        [HarmonyPatch(typeof(ExplorationParty), "SetRoute")]
        [HarmonyPostfix]
        private static void SetRoutePostfix(ExplorationParty __instance)
        {
            HomeShelterExpeditionRuntime.OnRouteSet(__instance);
        }

        [HarmonyPatch(typeof(ExplorationManager), "RecallNearestExplorationParty")]
        [HarmonyPrefix]
        private static bool RecallNearestPrefix(ExplorationManager __instance, ref bool __result)
        {
            bool recalled;
            if (!HomeShelterExpeditionRuntime.TryRecallNearest(__instance, out recalled))
                return true;

            __result = recalled;
            return false;
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnEnable")]
        [HarmonyPostfix]
        private static void MapOnEnablePostfix(UI_ExpeditionMap __instance)
        {
            HomeShelterExpeditionRuntime.OnMapEnabled(__instance);
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnDisable")]
        [HarmonyPostfix]
        private static void MapOnDisablePostfix(UI_ExpeditionMap __instance)
        {
            HomeShelterExpeditionRuntime.OnMapDisabled(__instance);
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "Update")]
        [HarmonyPostfix]
        private static void MapUpdatePostfix(UI_ExpeditionMap __instance)
        {
            HomeShelterExpeditionRuntime.OnMapUpdate(__instance);
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "UpdateRubberBand")]
        [HarmonyPrefix]
        private static bool UpdateRubberBandPrefix(GameObject rubberBand, Vector3 start, Vector3 end)
        {
            return !HomeShelterExpeditionRuntime.TryUpdateRubberBand(rubberBand, start, end);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPostfix]
        private static void CreateMapPostfix()
        {
            HomeShelterExpeditionRuntime.ResetSession();
        }

        [HarmonyPatch(typeof(ExpeditionMap), "SaveLoad")]
        [HarmonyPostfix]
        private static void SaveLoadPostfix()
        {
            HomeShelterExpeditionRuntime.ResetSession();
        }

        /// <summary>
        /// Redirects every GameModeManager.shelterMapWorldPosition read to the registered home.
        /// The hook takes the same GameModeManager argument, so the stack shape is unchanged.
        /// </summary>
        private static IEnumerable<CodeInstruction> RedirectShelterMapPosition(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            FluentTranspiler transpiler = FluentTranspiler.For(instructions, original);
            FluentReplacementResult result = transpiler.ReplaceCalls(ShelterMapPositionGetter).WithCall(ShelterMapPositionHook);
            if (result != FluentReplacementResult.PatternReplaced)
            {
                // A game update moved this read; expeditions would fall back to the map center.
                MMLog.WriteWarning("[ShelteredMap] No shelterMapWorldPosition read to redirect in "
                    + original.DeclaringType.Name + "." + original.Name + " (" + result + ").");
            }

            return transpiler.Build(strict: false, validateStack: true);
        }
    }
}

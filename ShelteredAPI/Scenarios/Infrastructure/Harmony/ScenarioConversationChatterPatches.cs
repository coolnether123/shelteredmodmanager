using System;

using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioConversationChatter",
        TargetBehavior = "Authored conversations can replace vanilla member chatter and suppress vanilla chatter categories.",
        FailureMode = "Vanilla random chatter continues, but authored random conversations and chatter suppression do not run.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario conversation chatter patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ScenarioConversationChatterPatches
    {
        [HarmonyPatch(typeof(FamilyMember), "RandomIdle")]
        [HarmonyPrefix]
        private static bool RandomIdlePrefix(FamilyMember __instance)
        {
            try
            {
                if (__instance == null || __instance.isSpeaking || __instance.isSpeechBubbleActive || GameTime.Day <= 4)
                    return true;
                if (CutsceneManager.Instance != null && CutsceneManager.Instance.CutSceneActive)
                    return true;

                float roll = UnityEngine.Random.value;
                if (roll < 0.37f || roll >= 0.4f)
                    return true;

                ScenarioConversationRuntimeService service = ResolveService();
                if (service == null)
                    return true;

                string ignoredSpeech;
                if (!service.TryHandleRandomComment(__instance, out ignoredSpeech))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioConversationChatter] RandomIdle patch failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(FamilyMember), "SayRandomComment")]
        [HarmonyPrefix]
        private static bool SayRandomCommentPrefix(FamilyMember __instance, ref string __result)
        {
            try
            {
                ScenarioConversationRuntimeService service = ResolveService();
                if (service == null)
                    return true;

                string authoredSpeech;
                if (!service.TryHandleRandomComment(__instance, out authoredSpeech))
                    return true;

                __result = string.Empty;
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioConversationChatter] SayRandomComment patch failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(FamilyMember), "GetRandomBantzSpeech")]
        [HarmonyPrefix]
        private static bool GetRandomBantzSpeechPrefix(ref string speech)
        {
            try
            {
                ScenarioConversationRuntimeService service = ResolveService();
                if (service == null || !service.ShouldSuppressGenericBantz())
                    return true;

                speech = string.Empty;
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioConversationChatter] GetRandomBantzSpeech patch failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(FamilyMember), "GetRandomIllnessSpeech")]
        [HarmonyPrefix]
        private static bool GetRandomIllnessSpeechPrefix(ref string speech, ref FamilyMember interactWith)
        {
            try
            {
                ScenarioConversationRuntimeService service = ResolveService();
                if (service == null || !service.ShouldSuppressIllness(interactWith))
                    return true;

                speech = string.Empty;
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioConversationChatter] GetRandomIllnessSpeech patch failed: " + ex.Message);
                return true;
            }
        }

        private static ScenarioConversationRuntimeService ResolveService()
        {
            try
            {
                return ScenarioRuntimeCompositionRoot.Resolve<ScenarioConversationRuntimeService>();
            }
            catch
            {
                return null;
            }
        }
    }
}

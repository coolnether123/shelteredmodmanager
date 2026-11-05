using System;
using System.Collections.Generic;
using System.Reflection;

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
        private static readonly FieldInfo InConversationField = AccessTools.Field(typeof(BaseCharacter), "isInCoversation");
        private static readonly FieldInfo ConversationPartnerField = AccessTools.Field(typeof(FamilyMember), "interactWith");

        private sealed class ConversationState
        {
            internal bool InConversation;
            internal FamilyMember Partner;
        }

        // RandomIdle calls this after its native probability and conversation guards.
        [HarmonyPatch(typeof(FamilyMember), "SayRandomComment")]
        [HarmonyPrefix]
        private static bool SayRandomCommentPrefix(FamilyMember __instance, ref string __result,
            out Dictionary<FamilyMember, ConversationState> __state)
        {
            __state = null;
            try
            {
                ScenarioConversationRuntimeService service = ResolveService();
                if (service == null)
                    return true;

                string authoredSpeech;
                if (!service.TryHandleRandomComment(__instance, out authoredSpeech))
                {
                    if (service.ShouldSuppressGenericBantz() && FamilyManager.Instance != null)
                    {
                        __state = new Dictionary<FamilyMember, ConversationState>();
                        foreach (FamilyMember member in FamilyManager.Instance.GetAllFamilyMembers())
                        {
                            if (member != null)
                                __state[member] = new ConversationState { InConversation = (bool)InConversationField.GetValue(member), Partner = (FamilyMember)ConversationPartnerField.GetValue(member) };
                        }
                    }
                    return true;
                }

                __result = string.Empty;
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioConversationChatter] SayRandomComment patch failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(FamilyMember), "SayRandomComment")]
        [HarmonyPostfix]
        private static void SayRandomCommentPostfix(FamilyMember __instance, string __result,
            Dictionary<FamilyMember, ConversationState> __state)
        {
            if (__state == null || __result != string.Empty || __instance == null)
                return;

            // The illness fallback sets both flags even when suppressed bantz returns empty.
            // RandomIdle schedules no response for empty speech, so restore the prior state.
            FamilyMember partner = (FamilyMember)ConversationPartnerField.GetValue(__instance);
            ConversationState previous;
            if (partner != null && __state.TryGetValue(partner, out previous))
            {
                InConversationField.SetValue(partner, previous.InConversation);
                ConversationPartnerField.SetValue(partner, previous.Partner);
            }
            if (__state.TryGetValue(__instance, out previous))
            {
                InConversationField.SetValue(__instance, previous.InConversation);
                ConversationPartnerField.SetValue(__instance, previous.Partner);
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

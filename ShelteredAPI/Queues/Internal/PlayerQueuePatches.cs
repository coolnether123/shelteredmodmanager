using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Queues.Internal
{
    [PatchPolicy(PatchDomain.Characters, "PlayerQueueChanges",
        TargetBehavior = "Family-member player job queue membership, order, and cancellation notifications",
        FailureMode = "ShelteredQueues queries and restores remain available, but QueueChanged does not observe vanilla changes.",
        RollbackStrategy = "Disable the Characters patch domain or remove the player queue change bridge.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch]
    internal static class PlayerQueuePatches
    {
        [HarmonyPatch(typeof(JobQueue), "AddJob")]
        [HarmonyPrefix]
        private static void Prefix_AddJob(JobQueue __instance, out string __state)
        {
            __state = PlayerQueueRuntime.CaptureMutationStamp(__instance);
        }

        [HarmonyPatch(typeof(JobQueue), "AddJob")]
        [HarmonyPostfix]
        private static void Postfix_AddJob(JobQueue __instance, string __state)
        {
            PlayerQueueRuntime.CompleteMutation(__instance, __state, PlayerQueueChangeKind.Added);
        }

        [HarmonyPatch(typeof(JobQueue), "RemoveAt")]
        [HarmonyPrefix]
        private static void Prefix_RemoveAt(JobQueue __instance, out string __state)
        {
            __state = PlayerQueueRuntime.CaptureMutationStamp(__instance);
        }

        [HarmonyPatch(typeof(JobQueue), "RemoveAt")]
        [HarmonyPostfix]
        private static void Postfix_RemoveAt(JobQueue __instance, string __state)
        {
            PlayerQueueRuntime.CompleteMutation(__instance, __state, PlayerQueueChangeKind.Removed);
        }

        [HarmonyPatch(typeof(JobQueue), "ForceClear")]
        [HarmonyPrefix]
        private static void Prefix_ForceClear(JobQueue __instance, out string __state)
        {
            __state = PlayerQueueRuntime.CaptureMutationStamp(__instance);
        }

        [HarmonyPatch(typeof(JobQueue), "ForceClear")]
        [HarmonyPostfix]
        private static void Postfix_ForceClear(JobQueue __instance, string __state)
        {
            PlayerQueueRuntime.CompleteMutation(__instance, __state, PlayerQueueChangeKind.ClearedOrCancelled);
        }

        [HarmonyPatch(typeof(JobQueue), "IncreasePriority")]
        [HarmonyPrefix]
        private static void Prefix_IncreasePriority(JobQueue __instance, out string __state)
        {
            __state = PlayerQueueRuntime.CaptureMutationStamp(__instance);
        }

        [HarmonyPatch(typeof(JobQueue), "IncreasePriority")]
        [HarmonyPostfix]
        private static void Postfix_IncreasePriority(JobQueue __instance, string __state)
        {
            PlayerQueueRuntime.CompleteMutation(__instance, __state, PlayerQueueChangeKind.Reordered);
        }

        [HarmonyPatch(typeof(JobQueue), "DecreasePriority")]
        [HarmonyPrefix]
        private static void Prefix_DecreasePriority(JobQueue __instance, out string __state)
        {
            __state = PlayerQueueRuntime.CaptureMutationStamp(__instance);
        }

        [HarmonyPatch(typeof(JobQueue), "DecreasePriority")]
        [HarmonyPostfix]
        private static void Postfix_DecreasePriority(JobQueue __instance, string __state)
        {
            PlayerQueueRuntime.CompleteMutation(__instance, __state, PlayerQueueChangeKind.Reordered);
        }
    }
}

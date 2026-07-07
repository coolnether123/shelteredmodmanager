using System.Reflection;

using HarmonyLib;
using ModAPI.Harmony;

using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioJournalVanillaPolicy",
        TargetBehavior = "Custom scenario journal policy can suppress vanilla journal categories and the vanilla first entry.",
        FailureMode = "Authored journal entries still work, but vanilla journal suppression policy is ignored.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario journal policy patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ScenarioJournalPatches
    {
        private static readonly FieldInfo RecordFirstEntryField = typeof(JournalManager).GetField("m_recordFirstEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FirstEntryEnteredField = typeof(JournalManager).GetField("m_firstEntryEntered", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPatch(typeof(JournalManager), "CreateJournalEntry")]
        [HarmonyPrefix]
        private static bool CreateJournalEntryPrefix(JournalManager.JournalEntryType type)
        {
            return !ScenarioJournalVanillaPolicyRuntime.ShouldSuppressCategory(type.ToString());
        }

        [HarmonyPatch(typeof(JournalManager), "UpdateManager")]
        [HarmonyPrefix]
        private static void UpdateManagerPrefix(JournalManager __instance)
        {
            if (__instance == null || !ScenarioJournalVanillaPolicyRuntime.ShouldSuppressFirstEntry())
                return;

            if (RecordFirstEntryField != null)
                RecordFirstEntryField.SetValue(__instance, false);
            if (FirstEntryEnteredField != null)
                FirstEntryEnteredField.SetValue(__instance, true);
        }
    }
}

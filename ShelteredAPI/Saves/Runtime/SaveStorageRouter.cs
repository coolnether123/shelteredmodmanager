using System;

namespace ShelteredAPI.Saves.Runtime
{
    /// <summary>
    /// Resolves the registry that owns a save target without leaking Standard/scenario
    /// branching into patch adapters.
    /// </summary>
    internal static class SaveStorageRouter
    {
        internal static string NormalizeScenarioId(string scenarioId)
        {
            return string.IsNullOrEmpty(scenarioId) ? "Standard" : scenarioId;
        }

        internal static bool IsStandardScenario(string scenarioId)
        {
            return ExpandedVanillaSaves.IsStandardScenario(NormalizeScenarioId(scenarioId));
        }

        internal static ISaveApi GetApi(string scenarioId)
        {
            string storageScenarioId = NormalizeScenarioId(scenarioId);
            return IsStandardScenario(storageScenarioId)
                ? ExpandedVanillaSaves.Instance
                : ScenarioSaves.GetTrustedRegistry(storageScenarioId);
        }

        internal static SaveRegistryCore GetRegistry(string scenarioId)
        {
            string storageScenarioId = NormalizeScenarioId(scenarioId);
            return IsStandardScenario(storageScenarioId)
                ? (SaveRegistryCore)ExpandedVanillaSaves.Instance
                : ScenarioSaves.GetTrustedRegistry(storageScenarioId);
        }

        internal static SaveEntry Get(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
                return null;

            return GetApi(scenarioId).Get(saveId);
        }

        internal static SaveEntry Overwrite(string scenarioId, string saveId, SaveOverwriteOptions options, byte[] data)
        {
            if (string.IsNullOrEmpty(saveId))
                return null;

            return GetApi(scenarioId).Overwrite(saveId, options, data);
        }

        internal static bool DeleteBySlot(string scenarioId, int absoluteSlot)
        {
            if (absoluteSlot <= 0)
                return false;

            return GetRegistry(scenarioId).DeleteBySlot(absoluteSlot);
        }

        internal static void UpdateSlotManifest(string scenarioId, int absoluteSlot, SaveInfo info)
        {
            if (absoluteSlot <= 0)
                return;

            GetRegistry(scenarioId).UpdateSlotManifest(absoluteSlot, info);
        }
    }
}

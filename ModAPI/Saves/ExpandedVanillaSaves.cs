using System;

namespace ModAPI.Saves
{
    public static class ExpandedVanillaSaves
    {
        private const string StandardScenarioId = "Standard";
        private static readonly SaveRegistryCore _registry = new SaveRegistryCore(StandardScenarioId);

        /// <summary>
        /// Provides an ISaveApi-compliant instance for internal use by the save proxy.
        /// </summary>
        internal static ISaveApi Instance => _registry;

        /// <summary>
        /// Checks if a given scenario ID is the one used for expanded vanilla saves.
        /// </summary>
        internal static bool IsStandardScenario(string scenarioId) => scenarioId == StandardScenarioId;

        public static SaveEntry[] List() => _registry.ListSaves();
        public static SaveEntry[] List(int page, int pageSize) => _registry.ListSaves(page, pageSize);
        public static int Count() => _registry.CountSaves();
        public static SaveEntry Create(SaveCreateOptions options) => _registry.CreateSave(options);
        public static bool Delete(string saveId) => _registry.DeleteSave(saveId);
        public static SaveEntry Get(string saveId) => _registry.GetSave(saveId);
        public static SaveEntry Overwrite(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes) => _registry.OverwriteSave(saveId, opts, xmlBytes);

        internal static void Debug_ListAllSaves()
        {
            MMLog.Write("--- Debug Listing All Custom Saves ---");
            var allSaves = _registry.ListSaves();
            if (allSaves == null || allSaves.Length == 0)
            {
                MMLog.Write("No custom saves found.");
                return;
            }

            foreach (var entry in allSaves)
            {
                if (entry == null) continue;
                MMLog.Write($"  - ID: {entry.id}, Slot: {entry.absoluteSlot}, Name: '{entry.name}', Family: '{entry.saveInfo?.familyName}', Days: {entry.saveInfo?.daysSurvived}, Updated: {entry.updatedAt}");
            }
            MMLog.Write("--- End of List ---");
        }

        public static SaveEntry FindByUIPosition(int physicalSlot, int page, int pageSize)
        {
            int absoluteIndex = (page * pageSize) + (physicalSlot - 1);
            var allSaves = _registry.ListSaves(0, int.MaxValue);
            if (absoluteIndex >= 0 && absoluteIndex < allSaves.Length)
            {
                return allSaves[absoluteIndex];
            }
            return null;
        }
    }
}
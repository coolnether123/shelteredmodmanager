using System;
using ModAPI.Core;

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
        public static int GetMaxSlot() => _registry.GetMaxSlot();
        public static SaveEntry Create(SaveCreateOptions options) => _registry.CreateSave(options);
        public static bool Delete(string saveId) => _registry.DeleteSave(saveId);
        public static bool DeleteBySlot(int absoluteSlot) => _registry.DeleteBySlot(absoluteSlot);
        public static SaveEntry Get(string saveId) => _registry.GetSave(saveId);
        public static SaveEntry GetBySlot(int absoluteSlot) => _registry.GetSaveBySlot(absoluteSlot);
        public static SaveEntry Overwrite(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes) => _registry.OverwriteSave(saveId, opts, xmlBytes);
        
        internal static void UpdateManifest(int absoluteSlot, SaveInfo info) => _registry.UpdateSlotManifest(absoluteSlot, info);

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

        public static SaveEntry FindByUIPosition(int physicalSlot, int page, int pageSize, bool mustExist = true)
        {
            // page 0 is vanilla (slots 1-3), custom saves start at page 1 (slots 4+)
            if (page <= 0) return null;

            // Map UI position (page, slot) to absolute slot number
            int absoluteSlot = (page - 1) * pageSize + physicalSlot + 3;

            var entry = _registry.GetSaveBySlot(absoluteSlot);
            
            if (entry == null)
            {
                MMLog.WriteDebug($"[FindByUIPosition] No entry found for slot {absoluteSlot} (page={page}, physical={physicalSlot})");
            }
            else
            {
                var path = DirectoryProvider.EntryPath(StandardScenarioId, entry.absoluteSlot);
                bool exists = System.IO.File.Exists(path);
                MMLog.WriteDebug($"[FindByUIPosition] Slot {absoluteSlot} found. File exists: {exists}. Path: {path}");
                
                // If mustExist is true, filter out placeholders for UI
                if (mustExist && !exists) return null;
            }

            return entry;
        }
    }
}
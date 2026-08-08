using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;
namespace ShelteredAPI.Scenarios.Application.Selection{
    internal sealed class ScenarioSaveLibrary : IScenarioSaveLibrary
    {
        private const string VanillaSurroundedSaveId = ScenarioSaveIdGuards.VanillaSurroundedSaveId;
        private const string VanillaStasisSaveId = ScenarioSaveIdGuards.VanillaStasisSaveId;

        public ScenarioSaveLibrary()
        {
            ScenarioSelectionIds.RegisterVanillaDescriptors();
        }

        public string ToStorageScenarioId(string scenarioId)
        {
            return ScenarioSelectionIds.ToStorageScenarioId(scenarioId);
        }

        public SaveEntry[] ListSaves(string scenarioId)
        {
            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
                return ListUnlimitedBuiltInScenarioSaves(storageScenarioId);

            return GetRegistry(storageScenarioId).ListSaves();
        }

        public int CountSaves(string scenarioId)
        {
            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
                return ListUnlimitedBuiltInScenarioSaves(storageScenarioId).Length;

            return GetRegistry(storageScenarioId).CountSaves();
        }

        public int GetNextAvailableSlot(string scenarioId)
        {
            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
                return GetNextBuiltInScenarioSlot(storageScenarioId);

            return GetRegistry(storageScenarioId).GetNextCreatableSlot();
        }

        public SaveEntry Get(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
                return null;

            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
                return FindBuiltInScenarioSave(storageScenarioId, saveId);

            return GetRegistry(storageScenarioId).GetSave(saveId);
        }

        public SaveEntry GetBySlot(string scenarioId, int absoluteSlot)
        {
            if (absoluteSlot <= 0)
                return null;

            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
                return FindBuiltInScenarioSaveByDisplaySlot(storageScenarioId, absoluteSlot);

            return GetRegistry(storageScenarioId).GetSaveBySlot(absoluteSlot);
        }

        public SaveEntry CreateNext(string scenarioId, SaveCreateOptions options)
        {
            string storageScenarioId = ToStorageScenarioId(scenarioId);
            SaveCreateOptions normalized = NormalizeCreateOptions(storageScenarioId, options);
            SaveEntry entry = GetRegistry(storageScenarioId).CreateSave(normalized);
            if (entry != null)
                MMLog.WriteInfo("[ScenarioSaveLibrary] Created scenario save entry. scenarioId="
                    + storageScenarioId + " saveId=" + entry.id + " slot=" + entry.absoluteSlot + ".");

            return entry;
        }

        public bool Delete(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
                return false;

            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsVanillaScenarioSaveId(saveId))
            {
                MMLog.WriteInfo("[ScenarioSaveLibrary] Refusing to delete vanilla scenario save from custom scenario browser. scenarioId="
                    + storageScenarioId + " saveId=" + saveId + ".");
                return false;
            }

            SaveRegistryCore registry = GetRegistry(storageScenarioId);
            bool deleted = IsBuiltInScenarioStorage(storageScenarioId)
                ? registry.TryDeleteSave(saveId)
                : registry.DeleteSave(saveId);
            if (!deleted && IsBuiltInScenarioStorage(storageScenarioId))
            {
                MMLog.WriteWarning("[ScenarioSaveLibrary] Delete save failed in canonical scenario storage. scenarioId="
                    + storageScenarioId + " saveId=" + saveId + ".");
            }

            MMLog.WriteInfo("[ScenarioSaveLibrary] Delete save result=" + deleted
                + ". scenarioId=" + storageScenarioId + " saveId=" + saveId + ".");
            return deleted;
        }

        public bool DeleteBySlot(string scenarioId, int absoluteSlot)
        {
            if (absoluteSlot <= 0)
                return false;

            string storageScenarioId = ToStorageScenarioId(scenarioId);
            if (IsBuiltInScenarioStorage(storageScenarioId))
            {
                SaveEntry displayEntry = FindBuiltInScenarioSaveByDisplaySlot(storageScenarioId, absoluteSlot);
                if (displayEntry == null)
                {
                    MMLog.WriteWarning("[ScenarioSaveLibrary] Delete slot failed; display slot did not resolve to a scenario save. scenarioId="
                        + storageScenarioId + " displaySlot=" + absoluteSlot + ".");
                    return false;
                }

                if (IsVanillaScenarioSaveEntry(displayEntry))
                {
                    MMLog.WriteInfo("[ScenarioSaveLibrary] Refusing to delete vanilla scenario slot from custom scenario browser. scenarioId="
                        + storageScenarioId + " slot=" + absoluteSlot + ".");
                    return false;
                }

                string resolvedStorageScenarioId;
                SaveEntry storageEntry;
                if (TryResolveStorageSaveEntry(storageScenarioId, displayEntry, out resolvedStorageScenarioId, out storageEntry))
                {
                    bool deletedById = Delete(resolvedStorageScenarioId, storageEntry.id);
                    MMLog.WriteInfo("[ScenarioSaveLibrary] Delete display slot result=" + deletedById
                        + ". scenarioId=" + storageScenarioId
                        + " displaySlot=" + absoluteSlot
                        + " storageScenarioId=" + resolvedStorageScenarioId
                        + " storageSlot=" + storageEntry.absoluteSlot + ".");
                    return deletedById;
                }

                MMLog.WriteWarning("[ScenarioSaveLibrary] Delete slot failed; display slot could not be mapped to storage. scenarioId="
                    + storageScenarioId + " displaySlot=" + absoluteSlot + " saveId=" + displayEntry.id + ".");
                return false;
            }

            bool deleted = GetRegistry(storageScenarioId).DeleteBySlot(absoluteSlot);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Delete slot result=" + deleted
                + ". scenarioId=" + storageScenarioId + " slot=" + absoluteSlot + ".");
            return deleted;
        }

        public void QueueNewGameSaveTarget(string scenarioId, SaveEntry startupSave, SaveManager.SaveType saveType)
        {
            if (startupSave == null)
                throw new ArgumentNullException("startupSave");

            string storageScenarioId = ResolveStorageScenarioId(scenarioId, startupSave);
            SaveRuntimeState.ClearPendingSave(saveType);
            SaveRuntimeState.QueueSave(saveType, storageScenarioId, startupSave.id);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Queued new-game save target. scenarioId="
                + storageScenarioId + " saveId=" + startupSave.id + " slot=" + startupSave.absoluteSlot
                + " virtualSaveType=" + saveType + ".");
        }

        public void QueueLoadTarget(string scenarioId, SaveEntry save, SaveManager.SaveType saveType)
        {
            if (save == null)
                throw new ArgumentNullException("save");

            string storageScenarioId = ResolveStorageScenarioId(scenarioId, save);
            SaveRuntimeState.QueueLoad(saveType, storageScenarioId, save.id);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Queued load target. scenarioId="
                + storageScenarioId + " saveId=" + save.id + " slot=" + save.absoluteSlot
                + " virtualSaveType=" + saveType + ".");
        }

        public bool ClearQueuedNewGameSave(SaveManager.SaveType saveType)
        {
            return SaveRuntimeState.ClearPendingSave(saveType);
        }

        public bool ClearQueuedLoad(SaveManager.SaveType saveType)
        {
            return SaveRuntimeState.ClearPendingLoad(saveType);
        }

        public bool ClearQueuedNewGameSaveIfMatches(SaveManager.SaveType saveType, string scenarioId, string saveId)
        {
            return SaveRuntimeState.ClearPendingSaveIfMatches(saveType, CreateTarget(scenarioId, saveId));
        }

        public bool ClearQueuedLoadIfMatches(SaveManager.SaveType saveType, string scenarioId, string saveId)
        {
            return SaveRuntimeState.ClearPendingLoadIfMatches(saveType, CreateTarget(scenarioId, saveId));
        }

        private SaveCreateOptions NormalizeCreateOptions(string scenarioId, SaveCreateOptions options)
        {
            SaveCreateOptions normalized = new SaveCreateOptions();
            if (options != null)
            {
                normalized.name = options.name;
                normalized.extraJson = options.extraJson;
                normalized.absoluteSlot = options.absoluteSlot;
            }

            if (normalized.absoluteSlot <= 0)
                normalized.absoluteSlot = GetNextAvailableSlot(scenarioId);

            return normalized;
        }

        private static SaveRegistryCore GetRegistry(string scenarioId)
        {
            return SaveStorageRouter.GetRegistry(ScenarioSelectionIds.ToStorageScenarioId(scenarioId));
        }

        private static SaveRuntimeState.Target CreateTarget(string scenarioId, string saveId)
        {
            return new SaveRuntimeState.Target { ScenarioId = scenarioId, SaveId = saveId };
        }

        private static string ResolveStorageScenarioId(string scenarioId, SaveEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.scenarioId))
                return ScenarioSelectionIds.ToStorageScenarioId(entry.scenarioId);

            if (!string.IsNullOrEmpty(scenarioId))
                return ScenarioSelectionIds.ToStorageScenarioId(scenarioId);

            return ScenarioSelectionIds.StandardStorageScenarioId;
        }

        internal static bool IsVanillaScenarioSaveEntry(SaveEntry save)
        {
            return save != null && IsVanillaScenarioSaveId(save.id);
        }

        internal static bool TryGetVanillaScenarioSlotNumber(SaveEntry save, out int slotNumber)
        {
            slotNumber = 0;
            if (save == null || string.IsNullOrEmpty(save.id))
                return false;

            VanillaSaveRoute route;
            if (!VanillaSaveRouting.TryGetRouteBySaveId(save.id, out route))
                return false;

            slotNumber = route.VanillaSlotNumber;
            return true;
        }

        internal static bool TryResolveStorageSaveEntry(
            string requestedStorageScenarioId,
            SaveEntry save,
            out string storageScenarioId,
            out SaveEntry storageEntry)
        {
            storageScenarioId = ScenarioSelectionIds.ToStorageScenarioId(requestedStorageScenarioId);
            storageEntry = null;

            if (save == null || string.IsNullOrEmpty(save.id) || IsVanillaScenarioSaveEntry(save))
                return false;

            string preferredStorageScenarioId = !string.IsNullOrEmpty(save.scenarioId)
                ? ScenarioSelectionIds.ToStorageScenarioId(save.scenarioId)
                : storageScenarioId;

            if (TryGetStorageSave(preferredStorageScenarioId, save.id, out storageEntry))
            {
                storageScenarioId = preferredStorageScenarioId;
                return true;
            }

            if (!string.Equals(preferredStorageScenarioId, storageScenarioId, StringComparison.OrdinalIgnoreCase)
                && TryGetStorageSave(storageScenarioId, save.id, out storageEntry))
            {
                return true;
            }

            storageEntry = null;
            return false;
        }

        private static bool IsVanillaScenarioSaveId(string saveId)
        {
            return string.Equals(saveId, VanillaSurroundedSaveId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(saveId, VanillaStasisSaveId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetStorageSave(string storageScenarioId, string saveId, out SaveEntry save)
        {
            save = null;
            if (string.IsNullOrEmpty(storageScenarioId) || string.IsNullOrEmpty(saveId))
                return false;

            try
            {
                save = GetRegistry(storageScenarioId).GetSave(saveId);
                return save != null;
            }
            catch
            {
                save = null;
                return false;
            }
        }

        private static bool IsBuiltInScenarioStorage(string storageScenarioId)
        {
            return string.Equals(storageScenarioId, ScenarioSelectionIds.VanillaSurroundedStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(storageScenarioId, ScenarioSelectionIds.VanillaStasisStorageScenarioId, StringComparison.OrdinalIgnoreCase);
        }

        private static SaveEntry[] ListUnlimitedBuiltInScenarioSaves(string storageScenarioId)
        {
            List<SaveEntry> entries = new List<SaveEntry>();
            SaveEntry[] saves = GetRegistry(storageScenarioId).ListSaves();
            int displaySlot = 1;
            for (int i = 0; saves != null && i < saves.Length; i++)
            {
                SaveEntry save = saves[i];
                if (save == null || IsVanillaScenarioSaveId(save.id))
                    continue;

                entries.Add(new SaveEntry
                {
                    id = save.id,
                    absoluteSlot = displaySlot++,
                    name = save.name,
                    createdAt = save.createdAt,
                    updatedAt = save.updatedAt,
                    gameVersion = save.gameVersion,
                    modApiVersion = save.modApiVersion,
                    scenarioId = save.scenarioId,
                    scenarioVersion = save.scenarioVersion,
                    fileSize = save.fileSize,
                    crc32 = save.crc32,
                    previewPath = save.previewPath,
                    extra = save.extra,
                    saveInfo = save.saveInfo ?? new SaveInfo()
                });
            }

            return entries.ToArray();
        }

        private static SaveEntry FindBuiltInScenarioSave(string storageScenarioId, string saveId)
        {
            SaveEntry[] saves = ListUnlimitedBuiltInScenarioSaves(storageScenarioId);
            for (int i = 0; i < saves.Length; i++)
            {
                if (saves[i] != null && string.Equals(saves[i].id, saveId, StringComparison.OrdinalIgnoreCase))
                    return saves[i];
            }

            return null;
        }

        private static SaveEntry FindBuiltInScenarioSaveByDisplaySlot(string storageScenarioId, int displaySlot)
        {
            SaveEntry[] saves = ListUnlimitedBuiltInScenarioSaves(storageScenarioId);
            for (int i = 0; i < saves.Length; i++)
            {
                if (saves[i] != null && saves[i].absoluteSlot == displaySlot)
                    return saves[i];
            }

            return null;
        }

        private static int GetNextBuiltInScenarioSlot(string storageScenarioId)
        {
            int nextSlot = Math.Max(2, ListUnlimitedBuiltInScenarioSaves(storageScenarioId).Length + 1);

            SaveEntry[] current = GetRegistry(storageScenarioId).ListSaves();
            int maxCurrentSlot = 0;
            AddMaxSlot(current, ref maxCurrentSlot);
            if (maxCurrentSlot >= nextSlot)
                nextSlot = maxCurrentSlot + 1;

            return nextSlot;
        }

        private static void AddMaxSlot(SaveEntry[] saves, ref int maxSlot)
        {
            for (int i = 0; saves != null && i < saves.Length; i++)
            {
                SaveEntry save = saves[i];
                if (save != null && save.absoluteSlot > maxSlot)
                    maxSlot = save.absoluteSlot;
            }
        }

    }
}

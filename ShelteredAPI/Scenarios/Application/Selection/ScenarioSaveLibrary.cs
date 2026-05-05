using System;
using ModAPI.Core;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
namespace ShelteredAPI.Scenarios.Application.Selection{
    internal sealed class ScenarioSaveLibrary : IScenarioSaveLibrary
    {
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
            return GetRegistry(scenarioId).ListSaves();
        }

        public int CountSaves(string scenarioId)
        {
            return GetRegistry(scenarioId).CountSaves();
        }

        public int GetNextAvailableSlot(string scenarioId)
        {
            return GetRegistry(scenarioId).GetNextCreatableSlot();
        }

        public SaveEntry Get(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
                return null;

            return GetRegistry(scenarioId).GetSave(saveId);
        }

        public SaveEntry GetBySlot(string scenarioId, int absoluteSlot)
        {
            if (absoluteSlot <= 0)
                return null;

            return GetRegistry(scenarioId).GetSaveBySlot(absoluteSlot);
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
            bool deleted = GetRegistry(storageScenarioId).DeleteSave(saveId);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Delete save result=" + deleted
                + ". scenarioId=" + storageScenarioId + " saveId=" + saveId + ".");
            return deleted;
        }

        public bool DeleteBySlot(string scenarioId, int absoluteSlot)
        {
            if (absoluteSlot <= 0)
                return false;

            string storageScenarioId = ToStorageScenarioId(scenarioId);
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
            PlatformSaveProxy.ClearNextSave(saveType);
            PlatformSaveProxy.SetNextSave(saveType, storageScenarioId, startupSave.id);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Queued new-game save target. scenarioId="
                + storageScenarioId + " saveId=" + startupSave.id + " slot=" + startupSave.absoluteSlot
                + " virtualSaveType=" + saveType + ".");
        }

        public void QueueLoadTarget(string scenarioId, SaveEntry save, SaveManager.SaveType saveType)
        {
            if (save == null)
                throw new ArgumentNullException("save");

            string storageScenarioId = ResolveStorageScenarioId(scenarioId, save);
            PlatformSaveProxy.SetNextLoad(saveType, storageScenarioId, save.id);
            MMLog.WriteInfo("[ScenarioSaveLibrary] Queued load target. scenarioId="
                + storageScenarioId + " saveId=" + save.id + " slot=" + save.absoluteSlot
                + " virtualSaveType=" + saveType + ".");
        }

        public bool ClearQueuedNewGameSave(SaveManager.SaveType saveType)
        {
            return PlatformSaveProxy.ClearNextSave(saveType);
        }

        public bool ClearQueuedLoad(SaveManager.SaveType saveType)
        {
            return PlatformSaveProxy.ClearNextLoad(saveType);
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
            string storageScenarioId = ScenarioSelectionIds.ToStorageScenarioId(scenarioId);
            if (ExpandedVanillaSaves.IsStandardScenario(storageScenarioId))
                return (SaveRegistryCore)ExpandedVanillaSaves.Instance;

            return ScenarioSaves.GetTrustedRegistry(storageScenarioId);
        }

        private static string ResolveStorageScenarioId(string scenarioId, SaveEntry entry)
        {
            if (!string.IsNullOrEmpty(scenarioId))
                return ScenarioSelectionIds.ToStorageScenarioId(scenarioId);

            if (entry != null && !string.IsNullOrEmpty(entry.scenarioId))
                return ScenarioSelectionIds.ToStorageScenarioId(entry.scenarioId);

            return ScenarioSelectionIds.StandardStorageScenarioId;
        }

    }
}

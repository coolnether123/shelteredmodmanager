using System;
using System.Collections.Generic;
using ModAPI.Core;

using ShelteredAPI.Hooks;
namespace ShelteredAPI.Saves
{
    internal static class ScenarioSaves
    {
        private static readonly Dictionary<string, SaveRegistryCore> _registries = new Dictionary<string, SaveRegistryCore>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the registry for a specific scenario. Marked internal for use by PlatformSaveProxy.
        /// </summary>
        internal static SaveRegistryCore GetTrustedRegistry(string scenarioId)
        {
            lock (_lock)
            {
                if (!_registries.ContainsKey(scenarioId))
                {
                    _registries[scenarioId] = new SaveRegistryCore(scenarioId);
                }
                return _registries[scenarioId];
            }
        }

        public static SaveEntry[] List(string scenarioId, int page, int pageSize)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.List");
            return GetTrustedRegistry(scenarioId).ListSaves(page, pageSize);
        }

        public static SaveEntry Get(string scenarioId, string saveId)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.Get");
            return GetTrustedRegistry(scenarioId).GetSave(saveId);
        }

        public static SaveEntry Create(string scenarioId, SaveCreateOptions options)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.Create");
            return GetTrustedRegistry(scenarioId).CreateSave(options);
        }

        public static SaveEntry CreateNext(string scenarioId, SaveCreateOptions options)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.CreateNext");

            SaveCreateOptions normalized = options ?? new SaveCreateOptions();
            if (normalized.absoluteSlot <= 0)
                normalized.absoluteSlot = GetNextAvailableSlot(scenarioId);

            return GetTrustedRegistry(scenarioId).CreateSave(normalized);
        }

        public static int GetNextAvailableSlot(string scenarioId)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.GetNextAvailableSlot");

            return GetTrustedRegistry(scenarioId).GetNextCreatableSlot();
        }

        public static bool Delete(string scenarioId, string saveId)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.Delete");
            return GetTrustedRegistry(scenarioId).DeleteSave(saveId);
        }

        public static SaveEntry Overwrite(string scenarioId, string saveId, SaveOverwriteOptions opts, byte[] xmlBytes)
        {
            scenarioId = ScenarioSaveIdGuards.RequireCustomScenarioId(scenarioId, "ScenarioSaves.Overwrite");
            return GetTrustedRegistry(scenarioId).OverwriteSave(saveId, opts, xmlBytes);
        }
    }
}

using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Saves
{
    public static class ScenarioSaves
    {
        private static readonly Dictionary<string, SaveRegistryCore> _registries = new Dictionary<string, SaveRegistryCore>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the registry for a specific scenario. Marked internal for use by PlatformSaveProxy.
        /// </summary>
        internal static SaveRegistryCore GetRegistry(string scenarioId)
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
            if (string.IsNullOrEmpty(scenarioId) || scenarioId.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                MMLog.WriteError("ScenarioSaves.List: Invalid or reserved scenarioId provided.");
                return new SaveEntry[0];
            }
            return GetRegistry(scenarioId).ListSaves(page, pageSize);
        }

        public static SaveEntry Get(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(scenarioId)) return null;
            return GetRegistry(scenarioId).GetSave(saveId);
        }

        public static SaveEntry Create(string scenarioId, SaveCreateOptions options)
        {
            if (string.IsNullOrEmpty(scenarioId) || scenarioId.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                MMLog.WriteError("ScenarioSaves.Create: Invalid or reserved scenarioId provided.");
                return null;
            }
            return GetRegistry(scenarioId).CreateSave(options);
        }

        public static bool Delete(string scenarioId, string saveId)
        {
            if (string.IsNullOrEmpty(scenarioId)) return false;
            return GetRegistry(scenarioId).DeleteSave(saveId);
        }

        public static SaveEntry Overwrite(string scenarioId, string saveId, SaveOverwriteOptions opts, byte[] xmlBytes)
        {
            if (string.IsNullOrEmpty(scenarioId)) return null;
            return GetRegistry(scenarioId).OverwriteSave(saveId, opts, xmlBytes);
        }
    }
}
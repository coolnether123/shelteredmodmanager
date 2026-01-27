using System;
using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// Wrapper for persisting multiple mod-specific data objects in a single JSON file.
    /// Internal DTO for ModAPI persistence. (Renamed from ModSaveData to avoid collision with SaveProtection).
    /// </summary>
    [Serializable]
    public class ModPersistenceData
    {
        public List<ModDataEntry> entries = new List<ModDataEntry>();
    }

    [Serializable]
    public class ModDataEntry
    {
        public string key;
        public string json;
    }
}

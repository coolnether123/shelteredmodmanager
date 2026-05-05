using System;
using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// JSON root for the per-save mod persistence file.
    /// Mod authors normally use <see cref="ISaveSystem"/> instead of constructing this DTO directly.
    /// </summary>
    [Serializable]
    public class ModPersistenceData
    {
        public List<ModDataEntry> entries = new List<ModDataEntry>();
    }

    /// <summary>
    /// One named JSON payload inside the per-save mod persistence file.
    /// </summary>
    [Serializable]
    public class ModDataEntry
    {
        public string key;
        public string json;
    }
}

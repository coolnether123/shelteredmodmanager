using System;
using System.Collections.Generic;

namespace Manager.Shared
{
    [Serializable]
    public class ModStatusEntry
    {
        public bool enabled = true;
        public bool locked = false;
        public string notes;
    }

    [Serializable]
    public class ModStatusEntryKV
    {
        public string id;
        public bool enabled = true;
        public bool locked = false;
        public string notes;
    }

    [Serializable]
    public class LoadOrderFile
    {
        public string[] order;
        public ModStatusEntryKV[] mods;
    }

    public class ProcessedLoadOrderData
    {
        public string[] Order;
        public Dictionary<string, ModStatusEntry> Mods;

        public ProcessedLoadOrderData()
        {
            Order = new string[0];
            Mods = new Dictionary<string, ModStatusEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

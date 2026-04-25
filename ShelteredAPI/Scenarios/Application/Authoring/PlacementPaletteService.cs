using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class PlacementPaletteService
    {
        internal sealed class PaletteEntry
        {
            public string ActionId;
            public string Label;
            public string Hint;
            public string Section;
        }

        public PaletteEntry CreateEntry(string section, string actionId, string label, string hint)
        {
            return new PaletteEntry
            {
                Section = section,
                ActionId = actionId,
                Label = label,
                Hint = hint
            };
        }

        public Dictionary<string, List<PaletteEntry>> GroupBySection(IEnumerable<PaletteEntry> entries)
        {
            Dictionary<string, List<PaletteEntry>> grouped = new Dictionary<string, List<PaletteEntry>>();
            foreach (PaletteEntry entry in entries)
            {
                if (entry == null)
                    continue;

                string key = string.IsNullOrEmpty(entry.Section) ? "General" : entry.Section;
                List<PaletteEntry> bucket;
                if (!grouped.TryGetValue(key, out bucket))
                {
                    bucket = new List<PaletteEntry>();
                    grouped[key] = bucket;
                }

                bucket.Add(entry);
            }

            return grouped;
        }
    }
}

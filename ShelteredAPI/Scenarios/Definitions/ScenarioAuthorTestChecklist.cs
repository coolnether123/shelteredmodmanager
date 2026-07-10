using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios.Definitions
{
    internal enum ScenarioAuthorTestVerificationSource
    {
        None = 0,
        Manual = 1,
        Editor = 2
    }

    internal sealed class ScenarioAuthorTestChecklistItem
    {
        public string Id { get; set; }
        public bool Checked { get; set; }
        public string Note { get; set; }
        public DateTime? CheckedUtc { get; set; }
        public ScenarioAuthorTestVerificationSource Source { get; set; }
    }

    internal sealed class ScenarioAuthorTestChecklist
    {
        public ScenarioAuthorTestChecklist()
        {
            Items = new List<ScenarioAuthorTestChecklistItem>();
        }

        internal List<ScenarioAuthorTestChecklistItem> Items { get; private set; }

        internal ScenarioAuthorTestChecklistItem Find(string id)
        {
            for (int i = 0; Items != null && i < Items.Count; i++)
            {
                ScenarioAuthorTestChecklistItem item = Items[i];
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        internal ScenarioAuthorTestChecklistItem GetOrCreate(string id)
        {
            ScenarioAuthorTestChecklistItem item = Find(id);
            if (item != null)
                return item;

            item = new ScenarioAuthorTestChecklistItem { Id = id };
            Items.Add(item);
            return item;
        }

        internal void RemoveIfEmpty(ScenarioAuthorTestChecklistItem item)
        {
            if (item != null && !item.Checked && string.IsNullOrEmpty(item.Note))
                Items.Remove(item);
        }
    }
}

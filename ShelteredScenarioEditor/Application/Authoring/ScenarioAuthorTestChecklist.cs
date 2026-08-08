using System;
using System.Collections.Generic;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal enum ScenarioAuthorTestVerificationSource
    {
        None = 0,
        Manual = 1,
        Editor = 2
    }

    internal sealed class ScenarioAuthorTestChecklistItem
    {
        internal string Id { get; set; }
        internal bool Checked { get; set; }
        internal string Note { get; set; }
        internal DateTime? CheckedUtc { get; set; }
        internal ScenarioAuthorTestVerificationSource Source { get; set; }

        internal ScenarioAuthorTestChecklistItem Copy()
        {
            return new ScenarioAuthorTestChecklistItem
            {
                Id = Id,
                Checked = Checked,
                Note = Note,
                CheckedUtc = CheckedUtc,
                Source = Source
            };
        }
    }

    /// <summary>
    /// Editor-only author verification state. It is intentionally not part of
    /// ScenarioDefinition because published scenarios and the modder API do not
    /// consume this workflow metadata.
    /// </summary>
    internal sealed class ScenarioAuthorTestChecklist
    {
        private readonly List<ScenarioAuthorTestChecklistItem> _items =
            new List<ScenarioAuthorTestChecklistItem>();

        internal IList<ScenarioAuthorTestChecklistItem> Items
        {
            get { return _items.AsReadOnly(); }
        }

        internal bool HasAuthoredContent
        {
            get
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    ScenarioAuthorTestChecklistItem item = _items[i];
                    if (item != null && (item.Checked || !string.IsNullOrEmpty(item.Note)))
                        return true;
                }

                return false;
            }
        }

        internal ScenarioAuthorTestChecklistItem Find(string id)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                ScenarioAuthorTestChecklistItem item = _items[i];
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
            _items.Add(item);
            return item;
        }

        internal bool Remove(string id)
        {
            ScenarioAuthorTestChecklistItem item = Find(id);
            return item != null && _items.Remove(item);
        }

        internal void Add(ScenarioAuthorTestChecklistItem item)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id))
                _items.Add(item);
        }

        internal ScenarioAuthorTestChecklist Copy()
        {
            ScenarioAuthorTestChecklist copy = new ScenarioAuthorTestChecklist();
            for (int i = 0; i < _items.Count; i++)
                copy.Add(_items[i] != null ? _items[i].Copy() : null);
            return copy;
        }
    }
}

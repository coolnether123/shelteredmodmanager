using System;
using System.Collections.Generic;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioAuthorTestChecklistStep
    {
        public ScenarioAuthorTestChecklistStep(string id, string displayName, string readmeName)
        {
            Id = id;
            DisplayName = displayName;
            ReadmeName = readmeName;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string ReadmeName { get; private set; }
    }

    internal sealed class ScenarioAuthorTestChecklistService
    {
        internal const string StartedPlaytestId = "started_playtest";
        internal const string SavedReloadedId = "saved_reloaded";
        internal const string ReachedOutcomesId = "reached_outcomes";
        internal const string VerifiedRequiredModsId = "verified_required_mods";
        internal const string InstalledExportId = "installed_export";
        internal const string ToggleActionPrefix = "testchecklist.toggle.";
        internal const string NoteActionPrefix = "testchecklist.note.";
        internal const int MaximumNoteLength = 160;

        private static readonly ScenarioAuthorTestChecklistStep[] Steps =
        {
            new ScenarioAuthorTestChecklistStep(StartedPlaytestId, "Started a playtest", "playtest"),
            new ScenarioAuthorTestChecklistStep(SavedReloadedId, "Saved and reloaded during play", "save/load during play"),
            new ScenarioAuthorTestChecklistStep(ReachedOutcomesId, "Reached each ending/outcome", "each outcome"),
            new ScenarioAuthorTestChecklistStep(VerifiedRequiredModsId, "Verified required mods list", "required mods"),
            new ScenarioAuthorTestChecklistStep(InstalledExportId, "Installed the exported package and played it", "export reinstall")
        };

        private readonly Func<DateTime> _utcNow;

        public ScenarioAuthorTestChecklistService()
            : this(delegate { return DateTime.UtcNow; })
        {
        }

        internal ScenarioAuthorTestChecklistService(Func<DateTime> utcNow)
        {
            _utcNow = utcNow ?? delegate { return DateTime.UtcNow; };
        }

        internal ScenarioAuthorTestChecklistStep[] GetSteps()
        {
            return (ScenarioAuthorTestChecklistStep[])Steps.Clone();
        }

        internal ScenarioAuthorTestChecklistItem[] GetItems(ScenarioEditorSession session)
        {
            IList<ScenarioAuthorTestChecklistItem> items = session != null && session.AuthorTestChecklist != null
                ? session.AuthorTestChecklist.Items
                : null;
            ScenarioAuthorTestChecklistItem[] snapshots =
                new ScenarioAuthorTestChecklistItem[items != null ? items.Count : 0];
            for (int i = 0; items != null && i < items.Count; i++)
                snapshots[i] = items[i] != null ? items[i].Copy() : null;
            return snapshots;
        }

        internal ScenarioAuthorTestChecklistItem FindItem(ScenarioEditorSession session, string itemId)
        {
            ScenarioAuthorTestChecklistItem item = session != null && session.AuthorTestChecklist != null
                ? session.AuthorTestChecklist.Find(itemId)
                : null;
            return item != null ? item.Copy() : null;
        }

        internal bool ToggleManual(ScenarioEditorSession session, string itemId)
        {
            if (!IsKnownItem(itemId) || session == null || session.WorkingDefinition == null)
                return false;

            ScenarioAuthorTestChecklist checklist = EnsureChecklist(session);
            ScenarioAuthorTestChecklistItem item = checklist.Find(itemId);
            bool isChecked = item == null || !item.Checked;
            if (isChecked)
            {
                item = checklist.GetOrCreate(itemId);
                item.Checked = true;
                item.CheckedUtc = _utcNow().ToUniversalTime();
                item.Source = ScenarioAuthorTestVerificationSource.Manual;
            }
            else
            {
                if (item != null && !string.IsNullOrEmpty(item.Note))
                {
                    item.Checked = false;
                    item.CheckedUtc = null;
                    item.Source = ScenarioAuthorTestVerificationSource.None;
                }
                else
                    checklist.Remove(itemId);
            }

            session.MarkChecklistChanged();
            return true;
        }

        internal bool SetNote(ScenarioEditorSession session, string itemId, string note)
        {
            if (!IsKnownItem(itemId) || session == null || session.WorkingDefinition == null)
                return false;

            string normalized = (note ?? string.Empty).Trim();
            if (normalized.Length > MaximumNoteLength)
                normalized = normalized.Substring(0, MaximumNoteLength);
            ScenarioAuthorTestChecklist checklist = EnsureChecklist(session);
            ScenarioAuthorTestChecklistItem item = checklist.Find(itemId);
            if (string.Equals(item != null ? item.Note ?? string.Empty : string.Empty, normalized, StringComparison.Ordinal))
                return false;
            if (item == null && normalized.Length == 0)
                return false;
            if ((item == null || !item.Checked) && normalized.Length == 0)
                checklist.Remove(itemId);
            else
            {
                item = checklist.GetOrCreate(itemId);
                item.Note = normalized;
            }
            session.MarkChecklistChanged();
            return true;
        }

        internal bool MarkPlaytestStarted(ScenarioEditorSession session)
        {
            return MarkEditorVerified(session, StartedPlaytestId);
        }

        internal bool MarkExportReinstalled(ScenarioEditorSession session)
        {
            return MarkEditorVerified(session, InstalledExportId);
        }

        internal int CountChecked(ScenarioEditorSession session)
        {
            int count = 0;
            for (int i = 0; i < Steps.Length; i++)
            {
                ScenarioAuthorTestChecklistItem item = FindItem(session, Steps[i].Id);
                if (item != null && item.Checked)
                    count++;
            }

            return count;
        }

        internal string BuildReadmeHonestyLine(ScenarioEditorSession session)
        {
            List<string> verified = new List<string>();
            for (int i = 0; i < Steps.Length; i++)
            {
                ScenarioAuthorTestChecklistItem item = FindItem(session, Steps[i].Id);
                if (item != null && item.Checked)
                    verified.Add(Steps[i].ReadmeName);
            }

            if (verified.Count == 0)
                return null;
            return "Author verified: " + string.Join(", ", verified.ToArray());
        }

        private bool MarkEditorVerified(ScenarioEditorSession session, string itemId)
        {
            if (session == null || session.WorkingDefinition == null)
                return false;
            ScenarioAuthorTestChecklist checklist = EnsureChecklist(session);
            ScenarioAuthorTestChecklistItem item = checklist.Find(itemId);
            if (item != null && item.Checked && item.Source == ScenarioAuthorTestVerificationSource.Editor)
                return false;
            item = checklist.GetOrCreate(itemId);
            item.Checked = true;
            item.CheckedUtc = _utcNow().ToUniversalTime();
            item.Source = ScenarioAuthorTestVerificationSource.Editor;
            session.MarkChecklistChanged();
            return true;
        }

        private static ScenarioAuthorTestChecklist EnsureChecklist(ScenarioEditorSession session)
        {
            if (session.AuthorTestChecklist == null)
                session.AuthorTestChecklist = new ScenarioAuthorTestChecklist();
            return session.AuthorTestChecklist;
        }

        private static bool IsKnownItem(string itemId)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                if (string.Equals(Steps[i].Id, itemId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

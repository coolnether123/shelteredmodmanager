using System;
using System.Collections.Generic;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring
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

        internal ScenarioAuthorTestChecklist GetChecklist(ScenarioDefinition definition)
        {
            if (definition == null)
                return new ScenarioAuthorTestChecklist();
            if (definition.AuthorTestChecklist == null)
                definition.AuthorTestChecklist = new ScenarioAuthorTestChecklist();
            return definition.AuthorTestChecklist;
        }

        internal bool ToggleManual(ScenarioEditorSession session, string itemId)
        {
            if (!IsKnownItem(itemId) || session == null || session.WorkingDefinition == null)
                return false;

            ScenarioAuthorTestChecklist checklist = GetChecklist(session.WorkingDefinition);
            ScenarioAuthorTestChecklistItem item = checklist.GetOrCreate(itemId);
            item.Checked = !item.Checked;
            if (item.Checked)
            {
                item.CheckedUtc = _utcNow().ToUniversalTime();
                item.Source = ScenarioAuthorTestVerificationSource.Manual;
            }
            else
            {
                item.CheckedUtc = null;
                item.Source = ScenarioAuthorTestVerificationSource.None;
                checklist.RemoveIfEmpty(item);
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
            ScenarioAuthorTestChecklist checklist = GetChecklist(session.WorkingDefinition);
            ScenarioAuthorTestChecklistItem item = checklist.GetOrCreate(itemId);
            if (string.Equals(item.Note ?? string.Empty, normalized, StringComparison.Ordinal))
                return false;
            item.Note = normalized;
            checklist.RemoveIfEmpty(item);
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

        internal int CountChecked(ScenarioDefinition definition)
        {
            int count = 0;
            ScenarioAuthorTestChecklist checklist = GetChecklist(definition);
            for (int i = 0; i < Steps.Length; i++)
            {
                ScenarioAuthorTestChecklistItem item = checklist.Find(Steps[i].Id);
                if (item != null && item.Checked)
                    count++;
            }

            return count;
        }

        internal string BuildReadmeHonestyLine(ScenarioDefinition definition)
        {
            ScenarioAuthorTestChecklist checklist = GetChecklist(definition);
            List<string> verified = new List<string>();
            for (int i = 0; i < Steps.Length; i++)
            {
                ScenarioAuthorTestChecklistItem item = checklist.Find(Steps[i].Id);
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
            ScenarioAuthorTestChecklistItem item = GetChecklist(session.WorkingDefinition).GetOrCreate(itemId);
            if (item.Checked && item.Source == ScenarioAuthorTestVerificationSource.Editor)
                return false;
            item.Checked = true;
            item.CheckedUtc = _utcNow().ToUniversalTime();
            item.Source = ScenarioAuthorTestVerificationSource.Editor;
            session.MarkChecklistChanged();
            return true;
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

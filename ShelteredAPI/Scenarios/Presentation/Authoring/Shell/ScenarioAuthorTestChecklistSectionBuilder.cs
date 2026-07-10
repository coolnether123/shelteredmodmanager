using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed class ScenarioAuthorTestChecklistSectionBuilder
    {
        private readonly ScenarioAuthorTestChecklistService _service;

        public ScenarioAuthorTestChecklistSectionBuilder(ScenarioAuthorTestChecklistService service)
        {
            _service = service;
        }

        internal ScenarioAuthoringInspectorSection BuildTestSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Text("This is an honest author record, not an export gate. Check each step after you test it and add a short note when useful."));
            ScenarioAuthorTestChecklist checklist = _service.GetChecklist(definition);
            ScenarioAuthorTestChecklistStep[] steps = _service.GetSteps();
            for (int i = 0; i < steps.Length; i++)
            {
                ScenarioAuthorTestChecklistStep step = steps[i];
                ScenarioAuthorTestChecklistItem entry = checklist.Find(step.Id);
                bool complete = entry != null && entry.Checked;
                bool editorVerified = complete && entry.Source == ScenarioAuthorTestVerificationSource.Editor;
                string detail = complete && entry.CheckedUtc.HasValue
                    ? "Checked " + entry.CheckedUtc.Value.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)
                    : "Not yet checked";
                items.Add(Item.ActionItem(Item.Action(
                    ScenarioAuthorTestChecklistService.ToggleActionPrefix + step.Id,
                    (complete ? "[x] " : "[ ] ") + step.DisplayName,
                    complete ? "Clear this test record." : "Record that you completed this test step.",
                    true,
                    false,
                    complete ? "OK" : "--",
                    detail,
                    editorVerified ? "VERIFIED BY THE EDITOR" : complete ? "SELF-ATTESTED" : null)));

                ScenarioAuthoringInspectorItem note = Item.Property("Note - " + step.DisplayName, entry != null ? entry.Note ?? string.Empty : string.Empty);
                note.Editable = true;
                note.HoverHint = "Optional short test note (up to " + ScenarioAuthorTestChecklistService.MaximumNoteLength.ToString(CultureInfo.InvariantCulture) + " characters).";
                note.Action = Item.Action(
                    ScenarioAuthorTestChecklistService.NoteActionPrefix + step.Id + ".",
                    "Save note",
                    note.HoverHint,
                    true,
                    false,
                    "NT");
                items.Add(note);
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "test_author_checklist",
                Title = "Did you test it?",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        internal ScenarioAuthoringInspectorItem BuildExportSummary(ScenarioDefinition definition)
        {
            int complete = _service.CountChecked(definition);
            string value = complete.ToString(CultureInfo.InvariantCulture) + " of 5 test steps done";
            string detail = complete == 5
                ? "All author test steps are recorded."
                : "Export is still allowed; completing the author test checklist is encouraged.";
            return Item.Property("TEST CHECKLIST", value, detail);
        }
    }
}

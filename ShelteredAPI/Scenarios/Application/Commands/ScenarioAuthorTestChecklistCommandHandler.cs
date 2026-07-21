using System;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;

namespace ShelteredAPI.Scenarios.Application.Commands
{
    internal sealed class ScenarioAuthorTestChecklistCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthorTestChecklistService _checklistService;

        public ScenarioAuthorTestChecklistCommandHandler(
            IScenarioEditorService editorService,
            ScenarioAuthorTestChecklistService checklistService)
        {
            _editorService = editorService;
            _checklistService = checklistService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = !string.IsNullOrEmpty(actionId)
                && (actionId.StartsWith(ScenarioAuthorTestChecklistService.ToggleActionPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthorTestChecklistService.NoteActionPrefix, StringComparison.Ordinal));
            message = null;
            if (!handled || _checklistService == null)
                return false;

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (actionId.StartsWith(ScenarioAuthorTestChecklistService.ToggleActionPrefix, StringComparison.Ordinal))
            {
                string id = actionId.Substring(ScenarioAuthorTestChecklistService.ToggleActionPrefix.Length);
                bool changed = _checklistService.ToggleManual(session, id);
                message = changed ? "Author test checklist updated." : "Checklist item could not be updated.";
                return changed;
            }

            string payload = actionId.Substring(ScenarioAuthorTestChecklistService.NoteActionPrefix.Length);
            int separator = payload.IndexOf('.');
            if (separator <= 0)
            {
                message = "Checklist note action is invalid.";
                return false;
            }
            string itemId = payload.Substring(0, separator);
            string note = ScenarioAuthoringActionCodec.DecodeToken(payload.Substring(separator + 1));
            bool noteChanged = _checklistService.SetNote(session, itemId, note);
            message = noteChanged ? "Checklist note updated." : "Checklist note was unchanged.";
            return noteChanged;
        }
    }
}

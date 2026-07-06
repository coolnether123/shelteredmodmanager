using System;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringBaseModeReloadService
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioLaunchCoordinator _launchCoordinator;

        public ScenarioAuthoringBaseModeReloadService(
            IScenarioEditorService editorService,
            ScenarioAuthoringDraftRepository draftRepository,
            ScenarioLaunchCoordinator launchCoordinator)
        {
            if (editorService == null) throw new ArgumentNullException("editorService");
            if (draftRepository == null) throw new ArgumentNullException("draftRepository");
            if (launchCoordinator == null) throw new ArgumentNullException("launchCoordinator");

            _editorService = editorService;
            _draftRepository = draftRepository;
            _launchCoordinator = launchCoordinator;
        }

        public bool SaveAndReload(ScenarioEditorSession editorSession, ScenarioBaseGameMode newBaseMode, out string message)
        {
            message = null;
            if (editorSession == null || editorSession.WorkingDefinition == null)
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioDefinition definition = editorSession.WorkingDefinition;
            string draftId = definition.Id;
            if (string.IsNullOrEmpty(draftId))
            {
                message = "The active draft does not have an id.";
                return true;
            }

            SaveEntry draftStartupSave;
            if (!_draftRepository.TryGetDraftSaveEntry(draftId, out draftStartupSave) || draftStartupSave == null)
            {
                message = "Could not resolve the draft authoring save.";
                return true;
            }

            BaseModeSnapshot snapshot = BaseModeSnapshot.Capture(definition);
            ApplyBaseMode(definition, newBaseMode);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);
            if (validation == null || !validation.IsValid)
            {
                snapshot.Restore(definition);
                message = "Base switch save failed validation: " + FormatValidationSummary(validation);
                return true;
            }

            return QueueSavedDraftReload(draftId, draftStartupSave, ScenarioSelectionIds.GetDefaultSaveType(newBaseMode), newBaseMode, "as " + FormatBaseMode(newBaseMode), false, out message);
        }

        public bool SaveAndReloadCurrentWorld(ScenarioEditorSession editorSession, out string message)
        {
            message = null;
            if (editorSession == null || editorSession.WorkingDefinition == null)
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioDefinition definition = editorSession.WorkingDefinition;
            string draftId = definition.Id;
            if (string.IsNullOrEmpty(draftId))
            {
                message = "The active draft does not have an id.";
                return true;
            }

            SaveEntry draftStartupSave;
            if (!_draftRepository.TryGetDraftSaveEntry(draftId, out draftStartupSave) || draftStartupSave == null)
            {
                message = "Could not resolve the draft authoring save.";
                return true;
            }

            ScenarioValidationResult validation = _editorService.CommitChanges(null);
            if (validation == null || !validation.IsValid)
            {
                message = "Restart save failed validation: " + FormatValidationSummary(validation);
                return true;
            }

            SaveManager.SaveType launchSaveType = ScenarioSelectionIds.GetDefaultSaveType(definition.BaseGameMode);
            return QueueSavedDraftReload(draftId, draftStartupSave, launchSaveType, definition.BaseGameMode, "for playtest restart", true, out message);
        }

        public bool SaveBaseModeOnly(ScenarioEditorSession editorSession, ScenarioBaseGameMode newBaseMode, out string message)
        {
            message = null;
            if (editorSession == null || editorSession.WorkingDefinition == null)
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioDefinition definition = editorSession.WorkingDefinition;
            BaseModeSnapshot snapshot = BaseModeSnapshot.Capture(definition);
            ApplyBaseMode(definition, newBaseMode);
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);
            if (validation == null || !validation.IsValid)
            {
                snapshot.Restore(definition);
                message = "Base switch save failed validation: " + FormatValidationSummary(validation);
                return true;
            }

            message = "Base mode saved as " + FormatBaseMode(newBaseMode)
                + ". The current world stays loaded; this draft reopens in that base next time.";
            return true;
        }

        public static void ApplyBaseMode(ScenarioDefinition definition, ScenarioBaseGameMode baseMode)
        {
            if (definition == null)
                return;

            definition.BaseGameMode = baseMode;
            if (definition.SelectionRules == null)
                definition.SelectionRules = new ScenarioSelectionRulesDefinition();
            if (definition.SelectionRules.Availability == null)
                definition.SelectionRules.Availability = new ScenarioModeAvailabilityDefinition();

            definition.SelectionRules.Availability.UseOnly(baseMode);
        }

        public static string FormatBaseMode(ScenarioBaseGameMode mode)
        {
            if (mode == ScenarioBaseGameMode.Survival)
                return "Standard";
            return mode.ToString();
        }

        private static string FormatValidationSummary(ScenarioValidationResult validation)
        {
            if (validation == null || validation.Issues == null || validation.Issues.Length == 0)
                return "Unknown validation error.";

            for (int i = 0; i < validation.Issues.Length; i++)
            {
                ScenarioValidationIssue issue = validation.Issues[i];
                if (issue != null && !string.IsNullOrEmpty(issue.Message))
                    return issue.Message;
            }

            return "Unknown validation error.";
        }

        private bool QueueSavedDraftReload(
            string draftId,
            SaveEntry draftStartupSave,
            SaveManager.SaveType launchSaveType,
            ScenarioBaseGameMode baseMode,
            string label,
            bool reenterPlaytest,
            out string message)
        {
            ScenarioAuthoringBootstrapService bootstrap = ScenarioAuthoringBootstrapService.Instance;
            ScenarioAuthoringSession pending = bootstrap.QueueExistingDraft(draftId, launchSaveType);
            if (pending == null)
            {
                message = "Draft saved, but the authoring reload could not be queued.";
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] QueueExistingDraft failed for draftId=" + draftId + ".");
                return true;
            }

            if (reenterPlaytest)
                pending.RequestPlaytestAfterBootstrap();

            string error;
            if (!_launchCoordinator.QueueAuthoringDraftSceneReload(
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    draftStartupSave,
                    launchSaveType,
                    "authoring draft '" + draftId + "'",
                    baseMode,
                    out error))
            {
                bootstrap.CancelPendingDraft("Authoring reload launch failed.", false);
                message = "Draft saved, but world reload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] Launch failed for draftId=" + draftId
                    + " baseMode=" + baseMode + " error=" + (error ?? "<null>") + ".");
                return true;
            }

            bootstrap.RequestReloadActiveSession(pending, "Restarting playtest. Reloading authoring world " + label + ".");
            message = "Scenario draft saved. Reloading world " + label + ".";
            return true;
        }

        private sealed class BaseModeSnapshot
        {
            private readonly ScenarioBaseGameMode _baseMode;
            private readonly bool _hadSelectionRules;
            private readonly bool _hadAvailability;
            private readonly bool _survival;
            private readonly bool _surrounded;
            private readonly bool _stasis;

            private BaseModeSnapshot(
                ScenarioBaseGameMode baseMode,
                bool hadSelectionRules,
                bool hadAvailability,
                bool survival,
                bool surrounded,
                bool stasis)
            {
                _baseMode = baseMode;
                _hadSelectionRules = hadSelectionRules;
                _hadAvailability = hadAvailability;
                _survival = survival;
                _surrounded = surrounded;
                _stasis = stasis;
            }

            public static BaseModeSnapshot Capture(ScenarioDefinition definition)
            {
                ScenarioSelectionRulesDefinition rules = definition != null ? definition.SelectionRules : null;
                ScenarioModeAvailabilityDefinition availability = rules != null ? rules.Availability : null;
                return new BaseModeSnapshot(
                    definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival,
                    rules != null,
                    availability != null,
                    availability != null && availability.Survival,
                    availability != null && availability.Surrounded,
                    availability != null && availability.Stasis);
            }

            public void Restore(ScenarioDefinition definition)
            {
                if (definition == null)
                    return;

                definition.BaseGameMode = _baseMode;
                if (!_hadSelectionRules)
                {
                    definition.SelectionRules = null;
                    return;
                }

                if (definition.SelectionRules == null)
                    definition.SelectionRules = new ScenarioSelectionRulesDefinition();

                if (!_hadAvailability)
                {
                    definition.SelectionRules.Availability = null;
                    return;
                }

                if (definition.SelectionRules.Availability == null)
                    definition.SelectionRules.Availability = new ScenarioModeAvailabilityDefinition();

                definition.SelectionRules.Availability.Survival = _survival;
                definition.SelectionRules.Availability.Surrounded = _surrounded;
                definition.SelectionRules.Availability.Stasis = _stasis;
            }
        }
    }
}

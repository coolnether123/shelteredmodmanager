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

            SaveManager.SaveType launchSaveType = ScenarioSelectionIds.GetDefaultSaveType(newBaseMode);
            ScenarioAuthoringBootstrapService bootstrap = ScenarioAuthoringBootstrapService.Instance;
            ScenarioAuthoringSession pending = bootstrap.QueueExistingDraft(draftId, launchSaveType);
            if (pending == null)
            {
                message = "Draft saved, but the authoring reload could not be queued.";
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] QueueExistingDraft failed for draftId=" + draftId + ".");
                return true;
            }

            string error;
            if (!_launchCoordinator.QueueAuthoringDraftSceneReload(
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    draftStartupSave,
                    launchSaveType,
                    "authoring draft '" + draftId + "'",
                    newBaseMode,
                    out error))
            {
                bootstrap.CancelPendingDraft("Authoring base-mode reload launch failed.", false);
                message = "Draft saved, but world reload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] Launch failed for draftId=" + draftId
                    + " baseMode=" + newBaseMode + " error=" + (error ?? "<null>") + ".");
                return true;
            }

            bootstrap.RequestCloseActiveSession("Reloading authoring world for " + FormatBaseMode(newBaseMode) + " base mode.", false);

            message = "Scenario draft saved. Reloading world as " + FormatBaseMode(newBaseMode) + ".";
            return true;
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

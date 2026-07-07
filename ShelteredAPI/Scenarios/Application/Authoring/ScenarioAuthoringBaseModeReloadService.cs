using System;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringBaseModeReloadService
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioLaunchCoordinator _launchCoordinator;
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioPlayStartReadiness _playStartReadiness = new ScenarioPlayStartReadiness();

        public ScenarioAuthoringBaseModeReloadService(
            IScenarioEditorService editorService,
            ScenarioAuthoringDraftRepository draftRepository,
            ScenarioLaunchCoordinator launchCoordinator,
            ScenarioAuthoringCaptureService captureService)
        {
            if (editorService == null) throw new ArgumentNullException("editorService");
            if (draftRepository == null) throw new ArgumentNullException("draftRepository");
            if (launchCoordinator == null) throw new ArgumentNullException("launchCoordinator");

            _editorService = editorService;
            _draftRepository = draftRepository;
            _launchCoordinator = launchCoordinator;
            _captureService = captureService;
        }

        public bool SaveAndReload(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode newBaseMode,
            string familyChoice,
            out string message)
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
            ScenarioBackendWorldMaterializer.StoreCurrentWorld(definition);
            ApplyBaseMode(definition, newBaseMode);
            ScenarioBackendWorldMaterializer.MaterializeCurrentWorld(definition, newBaseMode);
            if (!ApplyFamilyChoice(editorSession, definition, newBaseMode, familyChoice, out message))
            {
                snapshot.Restore(definition);
                return true;
            }
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);
            if (validation == null || !validation.IsValid)
            {
                snapshot.Restore(definition);
                message = "Base switch save failed validation: " + FormatValidationSummary(validation);
                return true;
            }

            return QueueSavedDraftReload(
                draftId,
                draftStartupSave,
                ScenarioSelectionIds.GetDefaultSaveType(newBaseMode),
                newBaseMode,
                "as " + FormatBaseMode(newBaseMode) + " backend (" + FormatFamilyChoice(familyChoice) + ")",
                false,
                string.Equals(NormalizeFamilyChoice(familyChoice), ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal),
                true,
                out message);
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
            string playStartReason;
            if (!_playStartReadiness.CanStartPlay(definition, out playStartReason))
            {
                message = "Playtest restart blocked: " + playStartReason;
                return true;
            }

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
            ScenarioBackendWorldMaterializer.StoreCurrentWorld(definition);
            return QueueSavedDraftReload(draftId, draftStartupSave, launchSaveType, definition.BaseGameMode, "for playtest restart", true, false, false, out message);
        }

        public bool SaveBaseModeOnly(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode newBaseMode,
            string familyChoice,
            out string message)
        {
            message = null;
            if (editorSession == null || editorSession.WorkingDefinition == null)
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioDefinition definition = editorSession.WorkingDefinition;
            BaseModeSnapshot snapshot = BaseModeSnapshot.Capture(definition);
            ScenarioBackendWorldMaterializer.StoreCurrentWorld(definition);
            ApplyBaseMode(definition, newBaseMode);
            ScenarioBackendWorldMaterializer.MaterializeCurrentWorld(definition, newBaseMode);
            if (!ApplyFamilyChoice(editorSession, definition, newBaseMode, familyChoice, out message))
            {
                snapshot.Restore(definition);
                return true;
            }
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);
            if (validation == null || !validation.IsValid)
            {
                snapshot.Restore(definition);
                message = "Base switch save failed validation: " + FormatValidationSummary(validation);
                return true;
            }

            message = "Base mode saved as " + FormatBaseMode(newBaseMode)
                + " with " + FormatFamilyChoice(familyChoice)
                + ". The target backend world is saved and will load next time this draft opens.";
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

        private bool ApplyFamilyChoice(
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioBaseGameMode baseMode,
            string familyChoice,
            out string message)
        {
            message = null;
            if (definition == null)
                return true;

            string normalizedChoice = NormalizeFamilyChoice(familyChoice);
            definition.BaseFamilyChoice = normalizedChoice;

            if (string.Equals(normalizedChoice, ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal))
            {
                FamilySetupDefinition family = EnsureFamily(definition);
                family.OverrideVanillaFamily = false;
                family.Members.Clear();
                if (editorSession != null)
                    editorSession.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                return true;
            }

            FamilySetupDefinition setup = EnsureFamily(definition);
            if (setup.Members.Count > 0)
            {
                setup.OverrideVanillaFamily = true;
                if (editorSession != null)
                    editorSession.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
                return true;
            }

            if (_captureService == null)
            {
                message = "Could not keep current cast while switching to " + FormatBaseMode(baseMode)
                    + ": family capture service is unavailable. Choose the default family option or add authored cast first.";
                return false;
            }

            string captureMessage;
            if (!_captureService.CaptureCurrentFamily(editorSession, out captureMessage))
            {
                message = "Could not keep current cast while switching to " + FormatBaseMode(baseMode)
                    + ": " + (string.IsNullOrEmpty(captureMessage) ? "live family capture failed." : captureMessage);
                return false;
            }

            return true;
        }

        private static FamilySetupDefinition EnsureFamily(ScenarioDefinition definition)
        {
            if (definition.FamilySetup == null)
                definition.FamilySetup = new FamilySetupDefinition();
            return definition.FamilySetup;
        }

        private static string NormalizeFamilyChoice(string familyChoice)
        {
            return string.Equals(familyChoice, ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ScenarioBaseFamilyChoices.UseBaseDefaultFamily
                : ScenarioBaseFamilyChoices.KeepCurrentCast;
        }

        private static string FormatFamilyChoice(string familyChoice)
        {
            return string.Equals(NormalizeFamilyChoice(familyChoice), ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? "the base default family"
                : "the current cast";
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
            bool captureBaseDefaultFamily,
            bool suppressIntroCutscene,
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
            if (captureBaseDefaultFamily)
                pending.RequestBaseDefaultFamilyCaptureAfterBootstrap();
            if (suppressIntroCutscene)
                pending.RequestSuppressIntroCutsceneAfterSceneLoad();

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

            string reloadReason = reenterPlaytest
                ? "Restarting playtest. Reloading authoring world " + label + "."
                : "Reloading authoring world " + label + ".";
            bootstrap.RequestReloadActiveSession(pending, reloadReason);
            message = "Scenario draft saved. Reloading world " + label + ".";
            return true;
        }

        private sealed class BaseModeSnapshot
        {
            private readonly ScenarioDefinition _definition;

            private BaseModeSnapshot(ScenarioDefinition definition)
            {
                _definition = definition;
            }

            public static BaseModeSnapshot Capture(ScenarioDefinition definition)
            {
                return new BaseModeSnapshot(ScenarioDefinitionCloner.Clone(definition));
            }

            public void Restore(ScenarioDefinition definition)
            {
                if (definition == null || _definition == null)
                    return;

                definition.BaseGameMode = _definition.BaseGameMode;
                definition.BaseFamilyChoice = _definition.BaseFamilyChoice;
                definition.SelectionRules = _definition.SelectionRules;
                definition.FamilySetup = _definition.FamilySetup;
                definition.BunkerEdits = _definition.BunkerEdits;
                definition.BunkerGrid = _definition.BunkerGrid;
                definition.AssetReferences = _definition.AssetReferences;
                definition.BackendWorlds = _definition.BackendWorlds;
            }
        }
    }
}

using System;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringBaseModeReloadService
    {
        private readonly object _queuedReloadSync = new object();
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioAuthoringSessionLifecycleService _sessionLifecycle;
        private readonly ScenarioPreviewSessionHost _previewHost;
        private QueuedBaseModeReload _queuedReload;

        public ScenarioAuthoringBaseModeReloadService(
            IScenarioEditorService editorService,
            ScenarioAuthoringDraftRepository draftRepository,
            ScenarioAuthoringCaptureService captureService,
            ScenarioAuthoringSessionLifecycleService sessionLifecycle,
            ScenarioPreviewSessionHost previewHost)
        {
            if (editorService == null) throw new ArgumentNullException("editorService");
            if (draftRepository == null) throw new ArgumentNullException("draftRepository");

            _editorService = editorService;
            _draftRepository = draftRepository;
            _captureService = captureService;
            _sessionLifecycle = sessionLifecycle;
            _previewHost = previewHost;
        }

        public bool SaveAndReload(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode newBaseMode,
            string familyChoice,
            out string message)
        {
            return SaveAndReload(editorSession, newBaseMode, familyChoice, false, out message);
        }

        public bool SaveAndReload(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode newBaseMode,
            string familyChoice,
            bool autoPopulateStartingCastAfterReload,
            out string message)
        {
            message = null;
            if (_sessionLifecycle != null && _sessionLifecycle.HasPendingDraftLaunch())
            {
                return QueueAfterCurrentReload(
                    editorSession,
                    newBaseMode,
                    familyChoice,
                    autoPopulateStartingCastAfterReload,
                    out message);
            }

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
            ScenarioEditorBackendWorldMaterializer.StoreCurrentWorld(definition);
            ApplyBaseMode(definition, newBaseMode);
            ScenarioEditorBackendWorldMaterializer.MaterializeCurrentWorld(definition, newBaseMode);
            if (!ApplyFamilyChoice(editorSession, definition, newBaseMode, familyChoice, out message))
            {
                snapshot.Restore(definition);
                return true;
            }
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);

            return QueueSavedDraftReload(
                draftId,
                definition,
                draftStartupSave,
                ResolveTransportSaveType(newBaseMode),
                newBaseMode,
                "as " + FormatBaseMode(newBaseMode) + " backend (" + FormatFamilyChoice(familyChoice) + ")",
                false,
                string.Equals(NormalizeFamilyChoice(familyChoice), ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal),
                autoPopulateStartingCastAfterReload,
                true,
                validation,
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
            if (!ShelteredScenarioAuthoring.CanStartPlay(definition, out playStartReason))
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
                message = "Draft saved, but playtest restart is blocked by validation: " + FormatValidationSummary(validation);
                return true;
            }

            SaveManager.SaveType launchSaveType = ResolveTransportSaveType(definition.BaseGameMode);
            ScenarioEditorBackendWorldMaterializer.StoreCurrentWorld(definition);
            return QueueSavedDraftReload(draftId, definition, draftStartupSave, launchSaveType, definition.BaseGameMode, "for playtest restart", true, false, false, false, validation, out message);
        }

        public bool SaveAndReloadBaseline(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode baseMode,
            string label,
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

            ScenarioValidationResult validation = _editorService.CommitChanges(null);

            ScenarioBaseGameMode reloadMode = Enum.IsDefined(typeof(ScenarioBaseGameMode), baseMode)
                ? baseMode
                : definition.BaseGameMode;
            string reloadLabel = string.IsNullOrEmpty(label) ? "from the selected baseline" : label;
            ScenarioAuthoringSession loadingSession = _sessionLifecycle != null
                ? _sessionLifecycle.CurrentOrPending
                : null;
            if (loadingSession != null
                && _sessionLifecycle.Phase != ScenarioAuthoringSessionPhase.Active)
            {
                if (loadingSession.BaseMode == reloadMode)
                {
                    message = "Scenario draft saved. Continuing the current authoring world load "
                        + reloadLabel + "." + FormatValidationSuffix(validation);
                    return true;
                }

                return QueueBaselineAfterCurrentLoad(
                    draftId,
                    reloadMode,
                    reloadLabel,
                    validation,
                    out message);
            }

            SaveManager.SaveType launchSaveType = ResolveTransportSaveType(reloadMode);
            return QueueSavedDraftReload(
                draftId,
                definition,
                draftStartupSave,
                launchSaveType,
                reloadMode,
                reloadLabel,
                false,
                false,
                false,
                true,
                validation,
                out message);
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
            ScenarioEditorBackendWorldMaterializer.StoreCurrentWorld(definition);
            ApplyBaseMode(definition, newBaseMode);
            ScenarioEditorBackendWorldMaterializer.MaterializeCurrentWorld(definition, newBaseMode);
            if (!ApplyFamilyChoice(editorSession, definition, newBaseMode, familyChoice, out message))
            {
                snapshot.Restore(definition);
                return true;
            }
            editorSession.MarkDraftChanged(ScenarioDirtySection.Meta);

            ScenarioValidationResult validation = _editorService.CommitChanges(null);

            message = "Base mode saved as " + FormatBaseMode(newBaseMode)
                + " with " + FormatFamilyChoice(familyChoice)
                + ". The target backend world is saved and will load next time this draft opens."
                + FormatValidationSuffix(validation);
            return true;
        }

        internal ScenarioBaseGameMode ResolveModeSelectionBase(string draftId, ScenarioBaseGameMode fallbackMode)
        {
            lock (_queuedReloadSync)
            {
                return _queuedReload != null
                    && string.Equals(_queuedReload.DraftId, draftId, StringComparison.Ordinal)
                    ? _queuedReload.BaseMode
                    : fallbackMode;
            }
        }

        internal bool TryStartQueuedReload(
            ScenarioEditorSession editorSession,
            string completedDraftId,
            out string message)
        {
            message = null;
            QueuedBaseModeReload queued;
            lock (_queuedReloadSync)
            {
                if (_queuedReload == null
                    || !string.Equals(_queuedReload.DraftId, completedDraftId, StringComparison.Ordinal))
                {
                    return false;
                }

                queued = _queuedReload;
                _queuedReload = null;
            }

            _sessionLifecycle.SetQueuedReloadStatus(null);

            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            if (definition == null)
            {
                message = "The queued " + FormatBaseMode(queued.BaseMode) + " load could not start because the draft did not reopen.";
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] " + message + " draftId=" + (completedDraftId ?? "<none>") + ".");
                return true;
            }

            MMLog.WriteInfo("[ScenarioAuthoringBaseModeReload] Current load completed. Starting queued "
                + FormatBaseMode(queued.BaseMode) + " load for draftId=" + completedDraftId + ".");
            if (queued.UseSavedBaseline)
            {
                SaveAndReloadBaseline(
                    editorSession,
                    queued.BaseMode,
                    queued.Label,
                    out message);
            }
            else
            {
                SaveAndReload(
                    editorSession,
                    queued.BaseMode,
                    queued.FamilyChoice,
                    queued.AutoPopulateStartingCastAfterReload,
                    out message);
            }
            return true;
        }

        internal void CancelQueuedReload(string draftId, string reason)
        {
            bool cleared = false;
            lock (_queuedReloadSync)
            {
                if (_queuedReload != null
                    && (string.IsNullOrEmpty(draftId)
                        || string.Equals(_queuedReload.DraftId, draftId, StringComparison.Ordinal)))
                {
                    _queuedReload = null;
                    cleared = true;
                }
            }

            if (!cleared)
                return;

            _sessionLifecycle.SetQueuedReloadStatus(null);
            MMLog.WriteInfo("[ScenarioAuthoringBaseModeReload] Cleared queued mode load for draftId="
                + (draftId ?? "<any>") + ". Reason=" + (reason ?? "unspecified") + ".");
        }

        private bool QueueAfterCurrentReload(
            ScenarioEditorSession editorSession,
            ScenarioBaseGameMode newBaseMode,
            string familyChoice,
            bool autoPopulateStartingCastAfterReload,
            out string message)
        {
            ScenarioAuthoringSession loadingSession = _sessionLifecycle.CurrentOrPending;
            string draftId = editorSession != null && editorSession.WorkingDefinition != null
                ? editorSession.WorkingDefinition.Id
                : (loadingSession != null ? loadingSession.DraftId : null);
            if (string.IsNullOrEmpty(draftId) || loadingSession == null)
            {
                message = "A mode load is already running, but its draft could not be identified.";
                return true;
            }

            string loadingModeLabel = FormatBaseMode(loadingSession.BaseMode);
            string queuedModeLabel = FormatBaseMode(newBaseMode);
            if (newBaseMode == loadingSession.BaseMode)
            {
                lock (_queuedReloadSync)
                    _queuedReload = null;
                _sessionLifecycle.SetQueuedReloadStatus(null);
                message = "Queued mode change cleared. Continuing to load " + loadingModeLabel + ".";
                MMLog.WriteInfo("[ScenarioAuthoringBaseModeReload] Queued mode load cleared because the latest selection matches the active load. draftId="
                    + draftId + " mode=" + newBaseMode + ".");
                return true;
            }

            lock (_queuedReloadSync)
            {
                _queuedReload = new QueuedBaseModeReload(
                    draftId,
                    newBaseMode,
                    NormalizeFamilyChoice(familyChoice),
                    autoPopulateStartingCastAfterReload,
                    false,
                    null);
            }

            message = "Waiting for " + loadingModeLabel + " to finish loading; then loading " + queuedModeLabel + ".";
            _sessionLifecycle.SetQueuedReloadStatus(message);
            MMLog.WriteInfo("[ScenarioAuthoringBaseModeReload] Queued " + queuedModeLabel
                + " after the active " + loadingModeLabel + " load. draftId=" + draftId + ".");
            return true;
        }

        private bool QueueBaselineAfterCurrentLoad(
            string draftId,
            ScenarioBaseGameMode baseMode,
            string label,
            ScenarioValidationResult validation,
            out string message)
        {
            lock (_queuedReloadSync)
            {
                _queuedReload = new QueuedBaseModeReload(
                    draftId,
                    baseMode,
                    ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.KeepCurrentCast,
                    false,
                    true,
                    label);
            }

            message = "Scenario draft saved. Waiting for the current authoring world to finish loading; then loading "
                + FormatBaseMode(baseMode) + " " + label + "." + FormatValidationSuffix(validation);
            _sessionLifecycle.SetQueuedReloadStatus(message);
            MMLog.WriteInfo("[ScenarioAuthoringBaseModeReload] Queued saved baseline after the current load. draftId="
                + draftId + " mode=" + baseMode + ".");
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

            if (string.Equals(normalizedChoice, ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal))
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

            setup.OverrideVanillaFamily = false;
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
            return string.Equals(familyChoice, ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
                ? ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily
                : ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.KeepCurrentCast;
        }

        private static string FormatFamilyChoice(string familyChoice)
        {
            return string.Equals(NormalizeFamilyChoice(familyChoice), ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily, StringComparison.Ordinal)
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

        private static string FormatValidationSuffix(ScenarioValidationResult validation)
        {
            if (validation == null || validation.IsValid)
                return string.Empty;

            return " Validation has errors: " + FormatValidationSummary(validation);
        }

        private bool QueueSavedDraftReload(
            string draftId,
            ScenarioDefinition definition,
            SaveEntry draftStartupSave,
            SaveManager.SaveType launchSaveType,
            ScenarioBaseGameMode baseMode,
            string label,
            bool reenterPlaytest,
            bool captureBaseDefaultFamily,
            bool autoPopulateStartingCast,
            bool suppressIntroCutscene,
            ScenarioValidationResult validation,
            out string message)
        {
            ScenarioAuthoringSession pending = _sessionLifecycle.QueueCurrentDraftReload(
                draftId,
                baseMode,
                launchSaveType);
            if (pending == null)
            {
                message = "Draft saved, but the authoring reload could not be queued.";
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] Current lifecycle identity could not queue reload for draftId=" + draftId + ".");
                return false;
            }

            if (reenterPlaytest)
                pending.RequestPlaytestAfterBootstrap();
            if (captureBaseDefaultFamily)
                pending.RequestBaseDefaultFamilyCaptureAfterBootstrap();
            if (autoPopulateStartingCast)
                pending.RequestStartingCastAutoPopulateAfterBootstrap();
            if (suppressIntroCutscene)
                pending.RequestSuppressIntroCutsceneAfterSceneLoad();

            string error;
            if (!_previewHost.RestartWorld(
                    new ScenarioWorldLaunchRequest
                    {
                        StorageScenarioId = ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                        StartupSave = draftStartupSave,
                        SaveType = launchSaveType,
                        TargetLabel = "authoring draft '" + draftId + "'",
                        BaseGameMode = baseMode,
                        Definition = definition
                    },
                    out error))
            {
                _sessionLifecycle.CancelPending("Authoring reload launch failed.", false);
                message = "Draft saved, but world reload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                MMLog.WriteWarning("[ScenarioAuthoringBaseModeReload] Launch failed for draftId=" + draftId
                    + " baseMode=" + baseMode + " error=" + (error ?? "<null>") + ".");
                return true;
            }

            string reloadReason = reenterPlaytest
                ? "Restarting playtest. Reloading authoring world " + label + "."
                : "Reloading authoring world " + label + ".";
            _sessionLifecycle.BeginReload(pending, reloadReason);
            message = "Scenario draft saved. Reloading world " + label + "." + FormatValidationSuffix(validation);
            return true;
        }

        private static SaveManager.SaveType ResolveTransportSaveType(ScenarioBaseGameMode baseMode)
        {
            if (baseMode == ScenarioBaseGameMode.Surrounded) return SaveManager.SaveType.SlotSurrounded;
            if (baseMode == ScenarioBaseGameMode.Stasis) return SaveManager.SaveType.SlotStasis;
            return SaveManager.SaveType.Slot1;
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
                return new BaseModeSnapshot(ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition));
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

        private sealed class QueuedBaseModeReload
        {
            public QueuedBaseModeReload(
                string draftId,
                ScenarioBaseGameMode baseMode,
                string familyChoice,
                bool autoPopulateStartingCastAfterReload,
                bool useSavedBaseline,
                string label)
            {
                DraftId = draftId;
                BaseMode = baseMode;
                FamilyChoice = familyChoice;
                AutoPopulateStartingCastAfterReload = autoPopulateStartingCastAfterReload;
                UseSavedBaseline = useSavedBaseline;
                Label = label;
            }

            public string DraftId { get; private set; }
            public ScenarioBaseGameMode BaseMode { get; private set; }
            public string FamilyChoice { get; private set; }
            public bool AutoPopulateStartingCastAfterReload { get; private set; }
            public bool UseSavedBaseline { get; private set; }
            public string Label { get; private set; }
        }
    }
}

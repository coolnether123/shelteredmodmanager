using System;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredScenarioEditor.Application.Objects;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Persistence;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioEditorController : IScenarioEditorService
    {
        private const string RecoveryStatusMessage = "This draft was recovered from a backup after the main file was unreadable; review and save.";
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly IScenarioPlaytestOrchestrator _playtestOrchestrator;
        private readonly IScenarioPauseService _pauseService;
        private readonly ScenarioObjectIdentityAssignmentService _identityAssignmentService;
        private readonly ScenarioEditorActorReferenceService _actorResolver;
        private readonly ScenarioDraftSnapshotService _snapshotService;
        private readonly ScenarioAuthoringSidecarStore _sidecarStore;
        private readonly ScenarioPreviewSessionHost _previewHost;

        public static ScenarioEditorController Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioEditorController>(); }
        }

        public ScenarioEditorSession CurrentSession
        {
            get { return _sessionStore.Current; }
        }

        internal ScenarioEditorController(
            IScenarioEditorSessionStore sessionStore,
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            IScenarioPlaytestOrchestrator playtestOrchestrator,
            IScenarioPauseService pauseService,
            ScenarioObjectIdentityAssignmentService identityAssignmentService,
            ScenarioEditorActorReferenceService actorResolver,
            ScenarioDraftSnapshotService snapshotService,
            ScenarioAuthoringSidecarStore sidecarStore,
            ScenarioPreviewSessionHost previewHost)
        {
            _sessionStore = sessionStore;
            _serializer = serializer;
            _validator = validator;
            _playtestOrchestrator = playtestOrchestrator;
            _pauseService = pauseService;
            _identityAssignmentService = identityAssignmentService;
            _actorResolver = actorResolver;
            _snapshotService = snapshotService;
            _sidecarStore = sidecarStore;
            _previewHost = previewHost;
        }

        public ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode)
        {
            ScenarioDefinition definition = CreateBlankDefinition(baseMode);
            ScenarioEditorSession session = CreateSession(definition, ScenarioEditorState.CreateForNewDraft());
            _sessionStore.Set(session, null);

            PauseForEditor();
            MMLog.WriteInfo("[ScenarioEditorController] Entered scenario edit mode for base mode " + baseMode + ".");
            return session;
        }

        public ScenarioEditorSession LoadEditMode(string scenarioFilePath)
        {
            ScenarioDefinition definition;
            string recoveryMessage;
            bool recovered;
            if (!_serializer.TryLoadWithRecovery(scenarioFilePath, out definition, out recoveryMessage, out recovered))
                throw new InvalidOperationException(string.IsNullOrEmpty(recoveryMessage) ? "Scenario XML could not be loaded." : recoveryMessage);

            string sidecarWarning = null;
            ScenarioEditorState editorState = _sidecarStore != null
                ? _sidecarStore.Load(scenarioFilePath, out sidecarWarning)
                : new ScenarioEditorState();
            ScenarioEditorSession session = CreateSession(definition, editorState);
            ScenarioObjectIdentityAssignmentSummary migration = _identityAssignmentService.AssignMissingIds(session);
            if (recovered)
            {
                session.LoadWarning = RecoveryStatusMessage;
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
            }
            if (!string.IsNullOrEmpty(sidecarWarning))
            {
                session.LoadWarning = string.IsNullOrEmpty(session.LoadWarning)
                    ? sidecarWarning
                    : session.LoadWarning + " " + sidecarWarning;
                session.MarkChecklistChanged();
            }
            _sessionStore.Set(session, scenarioFilePath);

            ScenarioDraftSnapshotInfo newerAutosave;
            if (_snapshotService != null && _snapshotService.TryGetNewerAutosave(scenarioFilePath, out newerAutosave))
                session.LoadWarning = "A newer autosave is available from " + newerAutosave.AgeText + ". Open History to review and restore it; your manual draft was not changed.";

            PauseForEditor();
            if (!string.IsNullOrEmpty(recoveryMessage))
                MMLog.WriteWarning("[ScenarioEditorController] " + recoveryMessage);
            if (migration.AssignedCount > 0)
                MMLog.WriteInfo("[ScenarioEditorController] Assigned " + migration.AssignedCount + " missing scenario object id(s) while loading old scenario XML.");
            MMLog.WriteInfo("[ScenarioEditorController] Loaded scenario edit session from " + scenarioFilePath + ".");
            return session;
        }

        public ScenarioValidationResult CommitChanges(string scenarioFilePath)
        {
            ScenarioEditorSession session = RequireSession();
            string path = !string.IsNullOrEmpty(scenarioFilePath) ? scenarioFilePath : _sessionStore.CurrentFilePath;
            if (string.IsNullOrEmpty(path))
            {
                ScenarioValidationResult missingPath = new ScenarioValidationResult();
                missingPath.AddError("Scenario save path is required.");
                MMLog.WriteWarning("[ScenarioEditorController] Save blocked because the active scenario session has no file path.");
                return missingPath;
            }

            ScenarioObjectIdentityAssignmentSummary identityMigration = _identityAssignmentService.AssignMissingIds(session);
            if (identityMigration.AssignedCount > 0)
                MMLog.WriteInfo("[ScenarioEditorController] Assigned " + identityMigration.AssignedCount + " missing scenario object id(s) before validation.");
            int assignedActors = _actorResolver != null ? _actorResolver.AssignMissingCastActorRefs(session.WorkingDefinition) : 0;
            if (assignedActors > 0)
                MMLog.WriteInfo("[ScenarioEditorController] Assigned " + assignedActors + " missing scenario actor ref(s) before validation.");

            ScenarioValidationResult validation = ValidateForSave(session.WorkingDefinition, path);

            _serializer.Save(session.WorkingDefinition, path);
            try
            {
                if (_sidecarStore != null)
                    _sidecarStore.Save(path, session.EditorState);
            }
            catch (Exception ex)
            {
                validation.AddError("Scenario definition was saved, but editor checklist state was not saved: " + ex.Message);
                MMLog.WriteWarning("[ScenarioEditorController] Checklist sidecar save failed for " + path + ": " + ex.Message);
                return validation;
            }
            // Saving atomically replaces scenario.xml, which changes the exact
            // file stamp used by the draft metadata cache. Republish the entry
            // now so main-thread draft views can resolve and reopen this draft
            // without performing XML I/O on the Unity thread.
            _serializer.LoadInfo(path, ScenarioAuthoringDraftRepository.DraftOwnerId);
            session.OriginalDefinition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(session.WorkingDefinition);
            session.DirtyFlags.Clear();
            _sessionStore.Set(session, path);
            if (validation != null && !validation.IsValid)
                MMLog.WriteWarning("[ScenarioEditorController] Saved scenario definition with validation errors to " + path + ".");
            else
                MMLog.WriteInfo("[ScenarioEditorController] Saved scenario definition to " + path + ".");
            return validation;
        }

        public ScenarioEditorPlaytestResult BeginPlaytest()
        {
            ScenarioEditorSession session = RequireSession();
            string playStartReason;
            if (!ShelteredScenarioAuthoring.CanStartPlay(session.WorkingDefinition, out playStartReason))
            {
                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked by start-state readiness: " + playStartReason);
                return ScenarioEditorPlaytestResult.Failed("Playtest blocked: " + playStartReason);
            }

            ScenarioValidationResult validation;
            try
            {
                ScenarioObjectIdentityAssignmentSummary identityMigration = _identityAssignmentService.AssignMissingIds(session);
                if (identityMigration.AssignedCount > 0)
                    MMLog.WriteInfo("[ScenarioEditorController] Assigned " + identityMigration.AssignedCount + " missing scenario object id(s) before playtest validation.");
                int assignedActors = _actorResolver != null ? _actorResolver.AssignMissingCastActorRefs(session.WorkingDefinition) : 0;
                if (assignedActors > 0)
                    MMLog.WriteInfo("[ScenarioEditorController] Assigned " + assignedActors + " missing scenario actor ref(s) before playtest validation.");

                validation = _validator.Validate(session.WorkingDefinition, _sessionStore.CurrentFilePath);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioEditorController] Playtest validation failed: " + ex.Message);
                return ScenarioEditorPlaytestResult.Failed("Playtest validation failed: " + ex.Message);
            }

            if (validation == null)
            {
                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked because validation returned no result.");
                return ScenarioEditorPlaytestResult.Failed("Playtest blocked because scenario validation did not return a result.");
            }

            if (!validation.IsValid)
            {
                System.Collections.Generic.List<string> blockingMessages = new System.Collections.Generic.List<string>();
                ScenarioValidationIssue[] issues = validation.Issues;
                for (int i = 0; issues != null && i < issues.Length && i < 3; i++)
                {
                    if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                        blockingMessages.Add("Playtest blocked: " + issues[i].Message);
                }

                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked by validation.");
                return ScenarioEditorPlaytestResult.Failed(
                    blockingMessages.Count > 0
                        ? string.Join(" ", blockingMessages.ToArray())
                        : "Playtest blocked by scenario validation.");
            }

            return _playtestOrchestrator.BeginPlaytest(session, _sessionStore.CurrentFilePath);
        }

        public void EndPlaytest()
        {
            ScenarioEditorSession session = RequireSession();
            _playtestOrchestrator.EndPlaytest(session);
        }

        public void ConvertToNormalSave()
        {
            _previewHost.Close();
            ScenarioEditorSession session = _sessionStore.Current;
            if (session != null)
                session.IsConvertedToNormalSave = true;
            MMLog.WriteInfo("[ScenarioEditorController] Scenario binding converted to a normal save.");
        }

        public void RequestRestart()
        {
            ScenarioEditorSession session = RequireSession();
            session.RequestedRestart = true;
        }

        public void CloseEditor(bool resumeGame)
        {
            ScenarioEditorSession previous = _sessionStore.Current;
            if (previous != null && previous.PlaytestState == ScenarioPlaytestState.Playtesting)
            {
                try
                {
                    _playtestOrchestrator.EndPlaytest(previous);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioEditorController] Failed to end active playtest during close: " + ex.Message);
                }
            }

            _sessionStore.Clear();

            _previewHost.Close();
            ResumeFromEditor();
            MMLog.WriteInfo("[ScenarioEditorController] Editor session closed. resumeGame=" + resumeGame
                + ", hadPreviousSession=" + (previous != null) + ".");
        }

        public void MaintainAuthoringPause()
        {
            ScenarioEditorSession session = CurrentSession;
            if (session == null)
                return;

            if (session.PlaytestState == ScenarioPlaytestState.Playtesting)
            {
                ResumeFromEditor();
                return;
            }

            PauseForEditor();
        }

        private static ScenarioEditorSession CreateSession(
            ScenarioDefinition definition,
            ScenarioEditorState editorState)
        {
            return new ScenarioEditorSession
            {
                WorkingDefinition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition),
                OriginalDefinition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition),
                EditorState = editorState != null ? editorState.Copy() : new ScenarioEditorState(),
                PlaytestState = ScenarioPlaytestState.Idle,
                CurrentEditCategory = ScenarioEditCategory.Family,
                HasAppliedToCurrentWorld = false
            };
        }

        private static ScenarioDefinition CreateBlankDefinition(ScenarioBaseGameMode baseMode)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Id = "com.author.scenario.new";
            definition.DisplayName = "New Custom Scenario";
            definition.Description = string.Empty;
            definition.Author = ShelteredScenarioAuthoring.DefaultAuthor;
            definition.Version = ShelteredScenarioAuthoring.DefaultVersion;
            definition.BaseGameMode = baseMode;
            definition.SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(baseMode);
            return definition;
        }

        private ScenarioEditorSession RequireSession()
        {
            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null)
                throw new InvalidOperationException("No scenario editor session is active.");
            return session;
        }

        private ScenarioValidationResult ValidateForSave(ScenarioDefinition definition, string path)
        {
            try
            {
                ScenarioValidationResult validation = _validator.Validate(definition, path);
                if (validation != null)
                    return validation;

                ScenarioValidationResult missing = new ScenarioValidationResult();
                missing.AddError("Scenario validation did not return a result.");
                MMLog.WriteWarning("[ScenarioEditorController] Validation returned no result while saving " + path + "; saving draft anyway.");
                return missing;
            }
            catch (Exception ex)
            {
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError("Scenario validation failed before save: " + ex.Message);
                MMLog.WriteWarning("[ScenarioEditorController] Validation failed while saving " + path + "; saving draft anyway. " + ex.Message);
                return failed;
            }
        }

        private void PauseForEditor()
        {
            _pauseService.EnsurePaused("Scenario authoring active.");
        }

        private void ResumeFromEditor()
        {
            _pauseService.ReleasePause("Scenario authoring released simulation.");
        }
    }
}

using System;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Objects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioEditorController : IScenarioEditorService
    {
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly IScenarioPlaytestOrchestrator _playtestOrchestrator;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioPauseService _pauseService;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly ScenarioObjectIdentityAssignmentService _identityAssignmentService;
        private readonly ScenarioPlayStartReadiness _playStartReadiness = new ScenarioPlayStartReadiness();

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
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioPauseService pauseService,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            ScenarioObjectIdentityAssignmentService identityAssignmentService)
        {
            _sessionStore = sessionStore;
            _serializer = serializer;
            _validator = validator;
            _playtestOrchestrator = playtestOrchestrator;
            _runtimeBindingService = runtimeBindingService;
            _pauseService = pauseService;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _identityAssignmentService = identityAssignmentService;
        }

        public ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode)
        {
            ScenarioDefinition definition = CreateBlankDefinition(baseMode);
            ScenarioEditorSession session = CreateSession(definition);
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

            ScenarioEditorSession session = CreateSession(definition);
            ScenarioObjectIdentityAssignmentSummary migration = _identityAssignmentService.AssignMissingIds(session);
            if (recovered)
            {
                session.LoadWarning = recoveryMessage;
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
            }
            _sessionStore.Set(session, scenarioFilePath);

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

            ScenarioValidationResult validation = _validator.Validate(session.WorkingDefinition, path);
            if (validation == null)
            {
                validation = new ScenarioValidationResult();
                validation.AddError("Scenario validation did not return a result.");
                MMLog.WriteWarning("[ScenarioEditorController] Save blocked because validation returned no result.");
                return validation;
            }

            if (!validation.IsValid)
            {
                MMLog.WriteWarning("[ScenarioEditorController] Save blocked by scenario validation for " + path + ".");
                return validation;
            }

            _serializer.Save(session.WorkingDefinition, path);
            session.OriginalDefinition = ScenarioDefinitionCloner.Clone(session.WorkingDefinition);
            session.DirtyFlags.Clear();
            _sessionStore.Set(session, path);
            MMLog.WriteInfo("[ScenarioEditorController] Saved scenario definition to " + path + ".");
            return validation;
        }

        public ScenarioApplyResult BeginPlaytest()
        {
            ScenarioEditorSession session = RequireSession();
            string playStartReason;
            if (!_playStartReadiness.CanStartPlay(session.WorkingDefinition, out playStartReason))
            {
                ScenarioApplyResult blockedByStartState = new ScenarioApplyResult();
                blockedByStartState.AddMessage("Playtest blocked: " + playStartReason);
                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked by start-state readiness: " + playStartReason);
                return blockedByStartState;
            }

            ScenarioValidationResult validation;
            try
            {
                ScenarioObjectIdentityAssignmentSummary identityMigration = _identityAssignmentService.AssignMissingIds(session);
                if (identityMigration.AssignedCount > 0)
                    MMLog.WriteInfo("[ScenarioEditorController] Assigned " + identityMigration.AssignedCount + " missing scenario object id(s) before playtest validation.");

                validation = _validator.Validate(session.WorkingDefinition, _sessionStore.CurrentFilePath);
            }
            catch (Exception ex)
            {
                ScenarioApplyResult failedValidation = new ScenarioApplyResult();
                failedValidation.AddMessage("Playtest validation failed: " + ex.Message);
                MMLog.WriteWarning("[ScenarioEditorController] Playtest validation failed: " + ex.Message);
                return failedValidation;
            }

            if (validation == null)
            {
                ScenarioApplyResult failedClosed = new ScenarioApplyResult();
                failedClosed.AddMessage("Playtest blocked because scenario validation did not return a result.");
                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked because validation returned no result.");
                return failedClosed;
            }

            if (!validation.IsValid)
            {
                ScenarioApplyResult blocked = new ScenarioApplyResult();
                ScenarioValidationIssue[] issues = validation.Issues;
                for (int i = 0; issues != null && i < issues.Length && i < 3; i++)
                {
                    if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                        blocked.AddMessage("Playtest blocked: " + issues[i].Message);
                }

                if (blocked.Messages.Length == 0)
                    blocked.AddMessage("Playtest blocked by scenario validation.");
                MMLog.WriteWarning("[ScenarioEditorController] Playtest blocked by validation.");
                return blocked;
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
            _runtimeBindingService.ConvertToNormalSave();
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

            try { _spriteSwapEngine.Clear("Scenario editor closed."); }
            catch (Exception ex) { MMLog.WriteWarning("[ScenarioEditorController] Sprite swap cleanup failed during close: " + ex.Message); }
            try { _sceneSpritePlacementEngine.Clear("Scenario editor closed."); }
            catch (Exception ex) { MMLog.WriteWarning("[ScenarioEditorController] Scene sprite cleanup failed during close: " + ex.Message); }
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

        private static ScenarioEditorSession CreateSession(ScenarioDefinition definition)
        {
            return new ScenarioEditorSession
            {
                WorkingDefinition = ScenarioDefinitionCloner.Clone(definition),
                OriginalDefinition = ScenarioDefinitionCloner.Clone(definition),
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
            definition.Author = "unknown";
            definition.Version = "0.1.0";
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

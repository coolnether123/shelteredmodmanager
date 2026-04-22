using System;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioEditorController : IScenarioEditorService
    {
        private readonly object _sync = new object();
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly IScenarioDefinitionValidator _validator;
        private readonly IScenarioPlaytestOrchestrator _playtestOrchestrator;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioPauseService _pauseService;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private ScenarioEditorSession _session;
        private string _lastScenarioFilePath;

        public static ScenarioEditorController Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioEditorController>(); }
        }

        public ScenarioEditorSession CurrentSession
        {
            get
            {
                lock (_sync)
                {
                    return _session;
                }
            }
        }

        internal ScenarioEditorController(
            IScenarioDefinitionSerializer serializer,
            IScenarioDefinitionValidator validator,
            IScenarioPlaytestOrchestrator playtestOrchestrator,
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioPauseService pauseService,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine)
        {
            _serializer = serializer;
            _validator = validator;
            _playtestOrchestrator = playtestOrchestrator;
            _runtimeBindingService = runtimeBindingService;
            _pauseService = pauseService;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
        }

        public ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode)
        {
            ScenarioDefinition definition = CreateBlankDefinition(baseMode);
            ScenarioEditorSession session = CreateSession(definition);
            lock (_sync)
            {
                _session = session;
            }

            PauseForEditor();
            MMLog.WriteInfo("[ScenarioEditorController] Entered scenario edit mode for base mode " + baseMode + ".");
            return session;
        }

        public ScenarioEditorSession LoadEditMode(string scenarioFilePath)
        {
            ScenarioDefinition definition = _serializer.Load(scenarioFilePath);
            ScenarioEditorSession session = CreateSession(definition);
            lock (_sync)
            {
                _session = session;
                _lastScenarioFilePath = scenarioFilePath;
            }

            PauseForEditor();
            MMLog.WriteInfo("[ScenarioEditorController] Loaded scenario edit session from " + scenarioFilePath + ".");
            return session;
        }

        public ScenarioValidationResult CommitChanges(string scenarioFilePath)
        {
            ScenarioEditorSession session = RequireSession();
            string path = !string.IsNullOrEmpty(scenarioFilePath) ? scenarioFilePath : _lastScenarioFilePath;
            if (string.IsNullOrEmpty(path))
            {
                ScenarioValidationResult missingPath = new ScenarioValidationResult();
                missingPath.AddError("Scenario save path is required.");
                return missingPath;
            }

            ScenarioValidationResult validation = _validator.Validate(session.WorkingDefinition, path);
            if (!validation.IsValid)
                return validation;

            _serializer.Save(session.WorkingDefinition, path);
            session.OriginalDefinition = ScenarioDefinitionCloner.Clone(session.WorkingDefinition);
            session.DirtyFlags.Clear();
            _lastScenarioFilePath = path;
            MMLog.WriteInfo("[ScenarioEditorController] Saved scenario definition to " + path + ".");
            return validation;
        }

        public ScenarioApplyResult BeginPlaytest()
        {
            ScenarioEditorSession session = RequireSession();
            return _playtestOrchestrator.BeginPlaytest(session, _lastScenarioFilePath);
        }

        public bool TryGetActiveWorkingDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath)
        {
            definition = null;
            scenarioFilePath = null;

            lock (_sync)
            {
                if (_session == null || _session.WorkingDefinition == null)
                    return false;

                if (!string.IsNullOrEmpty(scenarioId)
                    && !string.Equals(_session.WorkingDefinition.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                definition = ScenarioDefinitionCloner.Clone(_session.WorkingDefinition);
                scenarioFilePath = _lastScenarioFilePath;
                return definition != null;
            }
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
            ScenarioEditorSession previous;
            lock (_sync)
            {
                previous = _session;
                _session = null;
                _lastScenarioFilePath = null;
            }

            _spriteSwapEngine.Clear("Scenario editor closed.");
            _sceneSpritePlacementEngine.Clear("Scenario editor closed.");
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
            return definition;
        }

        private ScenarioEditorSession RequireSession()
        {
            lock (_sync)
            {
                if (_session == null)
                    throw new InvalidOperationException("No scenario editor session is active.");
                return _session;
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

using System;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioEditorController
    {
        private static readonly ScenarioEditorController _instance = new ScenarioEditorController();
        private readonly object _sync = new object();
        private readonly ScenarioDefinitionSerializer _serializer = new ScenarioDefinitionSerializer();
        private readonly ScenarioValidatorImpl _validator = new ScenarioValidatorImpl();
        private readonly ScenarioApplier _applier = new ScenarioApplier();
        private ScenarioEditorSession _session;
        private string _lastScenarioFilePath;
        private bool _pausedByEditor;

        public static ScenarioEditorController Instance
        {
            get { return _instance; }
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

        private ScenarioEditorController()
        {
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
            if (!ScenarioWorldReady.IsReady())
            {
                ScenarioApplyResult notReady = new ScenarioApplyResult();
                notReady.AddMessage("World is not ready for scenario apply; playtest did not start.");
                return notReady;
            }

            if (!PauseManager.isPaused && Time.timeScale != 0f)
                MMLog.WriteWarning("[ScenarioEditorController] BeginPlaytest called while game was not paused.");

            ScenarioApplyResult result = _applier.ApplyAll(session.WorkingDefinition, _lastScenarioFilePath);
            ShelteredScenarioRuntimeBindingManager.Instance.SetBinding(new ScenarioRuntimeBinding
            {
                ScenarioId = session.WorkingDefinition.Id,
                VersionApplied = session.WorkingDefinition.Version,
                IsActive = true,
                IsConvertedToNormalSave = false,
                DayCreated = GameTime.Day
            });
            session.PlaytestState = ScenarioPlaytestState.Playtesting;
            ResumeFromEditor();
            return result;
        }

        public void EndPlaytest()
        {
            ScenarioEditorSession session = RequireSession();
            PauseForEditor();
            session.PlaytestState = ScenarioPlaytestState.Paused;
            MMLog.WriteInfo("[ScenarioEditorController] Playtest ended; editor pause restored.");
        }

        public void ConvertToNormalSave()
        {
            ShelteredScenarioRuntimeBindingManager.Instance.ConvertToNormalSave();
            MMLog.WriteInfo("[ScenarioEditorController] Scenario binding converted to a normal save.");
        }

        public void RequestRestart()
        {
            ScenarioEditorSession session = RequireSession();
            session.RequestedRestart = true;
        }

        public void CloseEditor(bool resumeGame)
        {
            lock (_sync)
            {
                _session = null;
            }

            if (resumeGame)
                ResumeFromEditor();
        }

        private static ScenarioEditorSession CreateSession(ScenarioDefinition definition)
        {
            return new ScenarioEditorSession
            {
                WorkingDefinition = ScenarioDefinitionCloner.Clone(definition),
                OriginalDefinition = ScenarioDefinitionCloner.Clone(definition),
                PlaytestState = ScenarioPlaytestState.Idle,
                CurrentEditCategory = ScenarioEditCategory.Family
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
            if (PauseManager.isPaused || Time.timeScale == 0f)
                return;

            PauseManager.Pause();
            if (!PauseManager.isPaused)
                Time.timeScale = 0f;
            _pausedByEditor = true;
        }

        private void ResumeFromEditor()
        {
            if (!_pausedByEditor)
                return;

            if (PauseManager.isPaused)
                PauseManager.Resume();
            else if (Time.timeScale == 0f)
                Time.timeScale = 1f;
            _pausedByEditor = false;
        }
    }
}

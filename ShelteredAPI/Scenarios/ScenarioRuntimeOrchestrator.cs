using System;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioRuntimeOrchestrator : IScenarioRuntimeOrchestrator
    {
        private readonly IShelteredCustomScenarioService _customScenarioService;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioApplier _applier;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private string _lastAppliedKey;

        public ScenarioRuntimeOrchestrator(
            IShelteredCustomScenarioService customScenarioService,
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioEditorService editorService,
            IScenarioApplier applier,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine)
        {
            _customScenarioService = customScenarioService;
            _runtimeBindingService = runtimeBindingService;
            _editorService = editorService;
            _applier = applier;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
        }

        public void UpdatePendingScenarioSpawn()
        {
            CustomScenarioState state = _customScenarioService.CurrentState;
            if (state == null || state.LifecycleState != CustomScenarioLifecycleState.Pending || string.IsNullOrEmpty(state.ScenarioId))
                return;

            if (!ScenarioWorldReady.IsReady())
                return;

            CustomScenarioInfo scenarioInfo;
            if (!_customScenarioService.TryGet(state.ScenarioId, out scenarioInfo)
                || _customScenarioService.VerifyDependencies(scenarioInfo) != ScenarioDependencyVerificationState.Match)
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] Dependencies are not satisfied; custom scenario will not spawn: " + state.ScenarioId);
                _customScenarioService.ClearState();
                return;
            }

            ScenarioDef definition;
            string error;
            if (!_customScenarioService.TryCreateScenarioDef(state.ScenarioId, null, out definition, out error))
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] " + error);
                _customScenarioService.ClearState();
                return;
            }

            QuestInstance instance = QuestManager.instance.SpawnQuestOrScenario(definition);
            if (instance == null)
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] QuestManager failed to spawn custom scenario: " + state.ScenarioId);
                _customScenarioService.ClearState();
                return;
            }

            _customScenarioService.MarkSpawned(state.ScenarioId);
            MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Spawned custom scenario: " + state.ScenarioId);
        }

        public void UpdateActiveScenarioApply()
        {
            ScenarioRuntimeBinding binding = _runtimeBindingService.GetActiveBindingForStartup();
            if (binding == null || string.IsNullOrEmpty(binding.ScenarioId) || !binding.IsActive)
            {
                _lastAppliedKey = null;
                _spriteSwapEngine.Clear("No active scenario binding was available for startup.");
                _sceneSpritePlacementEngine.Clear("No active scenario binding was available for startup.");
                return;
            }

            string applyKey = _runtimeBindingService.CurrentRevision
                + "|" + binding.ScenarioId + "|" + (binding.VersionApplied ?? string.Empty);
            if (string.Equals(_lastAppliedKey, applyKey, StringComparison.OrdinalIgnoreCase))
                return;

            if (!ScenarioWorldReady.IsReady())
                return;

            ScenarioDefinition definition;
            string scenarioFilePath;
            ScenarioValidationResult validation;
            if (!TryResolveDefinition(binding, out definition, out scenarioFilePath, out validation))
            {
                LogValidationFailure(binding.ScenarioId, validation);
                _lastAppliedKey = applyKey;
                return;
            }

            ScenarioApplyResult apply = _applier.ApplyAll(definition, scenarioFilePath);
            _lastAppliedKey = applyKey;
            MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Applied active scenario binding: " + binding.ScenarioId
                + " messages=" + apply.Messages.Length + ".");
        }

        private bool TryResolveDefinition(ScenarioRuntimeBinding binding, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            definition = null;
            scenarioFilePath = null;
            validation = null;
            if (binding == null || string.IsNullOrEmpty(binding.ScenarioId))
                return false;

            if (_editorService.TryGetActiveWorkingDefinition(binding.ScenarioId, out definition, out scenarioFilePath))
            {
                MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Using active authoring definition for scenario '" + binding.ScenarioId + "'.");
                return true;
            }

            return _customScenarioService.TryLoadDefinition(binding.ScenarioId, out definition, out scenarioFilePath, out validation);
        }

        private static void LogValidationFailure(string scenarioId, ScenarioValidationResult validation)
        {
            if (validation == null)
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] Scenario failed to load: " + scenarioId);
                return;
            }

            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] " + scenarioId + " " + issues[i].Severity + ": " + issues[i].Message);
            }
        }
    }
}

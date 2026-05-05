using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioRuntimeOrchestrator : IScenarioRuntimeOrchestrator
    {
        private readonly ICustomScenarioLifecycleService _customScenarioLifecycle;
        private readonly ICustomScenarioRegistry _customScenarioRegistry;
        private readonly IScenarioDependencyVerifier _dependencyVerifier;
        private readonly IScenarioDefinitionFactory _definitionFactory;
        private readonly IScenarioDefinitionCatalogService _definitionCatalog;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioRuntimeDefinitionOverrideProvider _definitionOverrideProvider;
        private readonly IScenarioApplier _applier;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly IVanillaScenarioRuntime _vanillaRuntime;
        private string _lastAppliedKey;
        private string _blockedApplyKey;
        private int _blockedCatalogRevision = -1;
        private ScenarioRuntimeApplyBlockReason _blockedReason = ScenarioRuntimeApplyBlockReason.None;
        private string _blockedDetails;

        public ScenarioRuntimeOrchestrator(
            ICustomScenarioLifecycleService customScenarioLifecycle,
            ICustomScenarioRegistry customScenarioRegistry,
            IScenarioDependencyVerifier dependencyVerifier,
            IScenarioDefinitionFactory definitionFactory,
            IScenarioDefinitionCatalogService definitionCatalog,
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioRuntimeDefinitionOverrideProvider definitionOverrideProvider,
            IScenarioApplier applier,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            IVanillaScenarioRuntime vanillaRuntime)
        {
            _customScenarioLifecycle = customScenarioLifecycle;
            _customScenarioRegistry = customScenarioRegistry;
            _dependencyVerifier = dependencyVerifier;
            _definitionFactory = definitionFactory;
            _definitionCatalog = definitionCatalog;
            _runtimeBindingService = runtimeBindingService;
            _definitionOverrideProvider = definitionOverrideProvider;
            _applier = applier;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _vanillaRuntime = vanillaRuntime;
        }

        public void UpdatePendingScenarioSpawn()
        {
            CustomScenarioState state = _customScenarioLifecycle.CurrentState;
            if (state == null || state.LifecycleState != CustomScenarioLifecycleState.Pending || string.IsNullOrEmpty(state.ScenarioId))
                return;

            string reason;
            if (!_vanillaRuntime.IsWorldReady(out reason))
                return;

            CustomScenarioInfo scenarioInfo;
            if (!_customScenarioRegistry.TryGet(state.ScenarioId, out scenarioInfo)
                || _dependencyVerifier.VerifyDependencies(scenarioInfo) != ScenarioDependencyVerificationState.Match)
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] Dependencies are not satisfied; custom scenario will not spawn: " + state.ScenarioId);
                _customScenarioLifecycle.ClearState();
                return;
            }

            ScenarioDef definition;
            string error;
            if (!_definitionFactory.TryCreateScenarioDef(state.ScenarioId, null, out definition, out error))
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] " + error);
                _customScenarioLifecycle.ClearState();
                return;
            }

            QuestInstance instance;
            if (!_vanillaRuntime.TrySpawnScenario(definition, out instance, out reason))
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] " + reason);
                _customScenarioLifecycle.ClearState();
                return;
            }

            if (!_customScenarioLifecycle.MarkSpawned(state.ScenarioId))
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] Failed to mark custom scenario as spawned: " + state.ScenarioId);
                _customScenarioLifecycle.ClearState();
                return;
            }

            BindSpawnedQuestInstance(instance);
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
            if (IsApplyStillBlocked(applyKey))
                return;

            string reason;
            if (!_vanillaRuntime.IsWorldReady(out reason))
                return;

            ScenarioDefinition definition;
            string scenarioFilePath;
            ScenarioValidationResult validation;
            if (!TryResolveDefinition(binding, out definition, out scenarioFilePath, out validation))
            {
                MarkApplyBlocked(applyKey, binding.ScenarioId, ClassifyDefinitionFailure(validation), FormatValidationFailure(validation));
                return;
            }

            try
            {
                ScenarioApplyResult apply = _applier.ApplyAll(definition, scenarioFilePath);
                _lastAppliedKey = applyKey;
                ClearApplyBlocked();
                MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Applied active scenario binding: " + binding.ScenarioId
                    + " messages=" + apply.Messages.Length + ".");
            }
            catch (Exception ex)
            {
                MarkApplyBlocked(applyKey, binding.ScenarioId, ScenarioRuntimeApplyBlockReason.RuntimeApplyException, ex.Message);
            }
        }

        private bool TryResolveDefinition(ScenarioRuntimeBinding binding, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            definition = null;
            scenarioFilePath = null;
            validation = null;
            if (binding == null || string.IsNullOrEmpty(binding.ScenarioId))
                return false;

            if (_definitionOverrideProvider.TryGetDefinitionOverride(binding.ScenarioId, out definition, out scenarioFilePath))
            {
                MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Using active authoring definition for scenario '" + binding.ScenarioId + "'.");
                return true;
            }

            return _definitionCatalog.TryLoadDefinition(binding.ScenarioId, out definition, out scenarioFilePath, out validation);
        }

        private bool IsApplyStillBlocked(string applyKey)
        {
            return !string.IsNullOrEmpty(_blockedApplyKey)
                && string.Equals(_blockedApplyKey, applyKey, StringComparison.OrdinalIgnoreCase)
                && _blockedCatalogRevision == _definitionCatalog.CatalogRevision
                && _blockedReason != ScenarioRuntimeApplyBlockReason.None;
        }

        private void MarkApplyBlocked(string applyKey, string scenarioId, ScenarioRuntimeApplyBlockReason reason, string details)
        {
            string normalizedDetails = details ?? string.Empty;
            bool changed = !string.Equals(_blockedApplyKey, applyKey, StringComparison.OrdinalIgnoreCase)
                || _blockedCatalogRevision != _definitionCatalog.CatalogRevision
                || _blockedReason != reason
                || !string.Equals(_blockedDetails ?? string.Empty, normalizedDetails, StringComparison.Ordinal);

            _blockedApplyKey = applyKey;
            _blockedCatalogRevision = _definitionCatalog.CatalogRevision;
            _blockedReason = reason;
            _blockedDetails = normalizedDetails;

            if (changed)
            {
                MMLog.WriteWarning("[ScenarioRuntimeOrchestrator] Active scenario binding is blocked for '" + scenarioId
                    + "' (" + reason + "). It will retry after the scenario catalog changes or the binding is updated. "
                    + normalizedDetails);
            }
        }

        private void ClearApplyBlocked()
        {
            _blockedApplyKey = null;
            _blockedCatalogRevision = -1;
            _blockedReason = ScenarioRuntimeApplyBlockReason.None;
            _blockedDetails = null;
        }

        private static ScenarioRuntimeApplyBlockReason ClassifyDefinitionFailure(ScenarioValidationResult validation)
        {
            if (validation == null || validation.Issues.Length == 0)
                return ScenarioRuntimeApplyBlockReason.MissingDefinition;

            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null
                    && !string.IsNullOrEmpty(issues[i].Message)
                    && issues[i].Message.IndexOf("not indexed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return ScenarioRuntimeApplyBlockReason.MissingDefinition;
                }
            }

            return ScenarioRuntimeApplyBlockReason.InvalidDefinition;
        }

        private static string FormatValidationFailure(ScenarioValidationResult validation)
        {
            if (validation == null || validation.Issues.Length == 0)
                return "Scenario definition could not be resolved.";

            List<string> parts = new List<string>();
            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    parts.Add(issues[i].Severity + ": " + issues[i].Message);
            }

            return parts.Count == 0 ? "Scenario definition could not be resolved." : string.Join("; ", parts.ToArray());
        }

        private void BindSpawnedQuestInstance(QuestInstance instance)
        {
            if (instance == null)
                return;

            ScenarioRuntimeBinding binding = _runtimeBindingService.CurrentBinding;
            if (binding == null)
                return;

            binding.ScenarioQuestInstanceId = instance.id;
            _runtimeBindingService.SetBinding(binding);
            MMLog.WriteInfo("[ScenarioRuntimeOrchestrator] Bound scenario QuestInstance id "
                + instance.id.ToString() + " to scenario '" + (binding.ScenarioId ?? string.Empty) + "'.");
        }
    }

    internal enum ScenarioRuntimeApplyBlockReason
    {
        None = 0,
        MissingDefinition = 1,
        InvalidDefinition = 2,
        MissingDependency = 3,
        RuntimeApplyException = 4
    }
}

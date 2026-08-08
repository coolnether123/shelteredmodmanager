using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Shared;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioRuntimeOutcomeVerification
    {
        internal static void Verify(ScenarioValidationResult result)
        {
            VerifyInstalledPresenterModeBranching(result);
            VerifyVictoryPresentationText(result);
            VerifyPerRunResetContract(result);
            VerifyRetryableEffectContract(result);
            VerifyAuthoredVisitorPriorityContract(result);
            VerifyMissingDefinitionRefreshRetry(result);
        }

        private static void VerifyPerRunResetContract(ScenarioValidationResult result)
        {
            ScenarioRuntimeBinding first = new ScenarioRuntimeBinding
            {
                ScenarioId = "repeatable.scenario",
                VersionApplied = "1.0.0",
                DayCreated = 1,
                RunId = "run-a"
            };
            ScenarioRuntimeBinding second = new ScenarioRuntimeBinding
            {
                ScenarioId = first.ScenarioId,
                VersionApplied = first.VersionApplied,
                DayCreated = first.DayCreated,
                RunId = "run-b"
            };
            string firstKey = ScenarioRuntimeStateService.BuildRuntimeBindingId(first, first.ScenarioId, first.VersionApplied);
            string secondKey = ScenarioRuntimeStateService.BuildRuntimeBindingId(second, second.ScenarioId, second.VersionApplied);
            Assert(!string.Equals(firstKey, secondKey, System.StringComparison.OrdinalIgnoreCase),
                "Separate starts of the same installed scenario must receive separate runtime journal scopes.", result);
        }

        private static void VerifyInstalledPresenterModeBranching(ScenarioValidationResult result)
        {
            Assert(ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Survival),
                "Survival authored wins must use the ShelteredAPI scenario victory panel.", result);
            Assert(!ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(false, ScenarioBaseGameMode.Survival),
                "Survival authored losses must retain the vanilla loss flow.", result);
            Assert(!ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Surrounded)
                && !ScenarioInstalledEndGamePresenter.UsesScenarioVictoryPanel(true, ScenarioBaseGameMode.Stasis),
                "Surrounded and Stasis authored wins must retain their native success score panels.", result);
        }

        private static void VerifyVictoryPresentationText(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition { DisplayName = "Ten Day Trial" };
            ScenarioConditionRef condition = new ScenarioConditionRef { Id = "win_1", Kind = ScenarioConditionKind.SurviveDays, Quantity = 10 };
            Assert(ScenarioWinLossOutcomeService.BuildFulfilledConditionText(definition, condition) == "Survived for 10 days.",
                "Victory presentation must describe the fulfilled authored condition.", result);
            ScenarioEndGamePresentation presentation = ScenarioWinLossOutcomeService.BuildPresentation(definition, condition, true);
            Assert(presentation.Success && presentation.ScenarioDisplayName == "Ten Day Trial"
                && presentation.BaseGameMode == ScenarioBaseGameMode.Survival,
                "Victory presentation must carry authored identity and base-mode routing facts.", result);
        }

        private static void VerifyRetryableEffectContract(ScenarioValidationResult result)
        {
            ScenarioEffectDispatcher dispatcher = new ScenarioEffectDispatcher();
            dispatcher.Register(new StubRetryableHandler());
            string message;
            bool retryable;
            bool handled = dispatcher.Dispatch(
                new ScenarioDefinition(),
                new ScenarioEffectDefinition { Kind = ScenarioEffectKind.StartConversation },
                new ScenarioRuntimeState(),
                out message,
                out retryable);

            Assert(!handled && retryable && !ScenarioScheduleRuntimeCoordinator.ShouldJournalEffectFailure(retryable),
                "Participant-resolution failures must remain unjournaled so a once-only conversation retries.", result);
            Assert(ScenarioScheduleRuntimeCoordinator.ShouldJournalEffectFailure(false),
                "Non-retryable effect failures must retain the normal failure journal contract.", result);
        }

        private static void VerifyAuthoredVisitorPriorityContract(ScenarioValidationResult result)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition { Id = "visitor_due" };
            ScenarioEffectDefinition effect = new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent };
            ScenarioPropertyBag.Set(effect.Properties, "eventType", "NpcVisit");
            action.Effects.Add(effect);
            Assert(ScenarioScheduleRuntimeCoordinator.ContainsAuthoredVisitorEffect(action),
                "NpcVisit world-event actions must be classified for authored visitor priority.", result);

            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioWorldEventRuntimeState.Bind(definition);
            ScenarioWorldEventRuntimeState.SetAuthoredVisitorPriority(true);
            Assert(ScenarioWorldEventRuntimeState.SuppressRandomVisitors,
                "Due-and-pending authored visitors must suppress new vanilla random visitors.", result);
            ScenarioWorldEventRuntimeState.Bind(null);
        }

        private static void VerifyMissingDefinitionRefreshRetry(ScenarioValidationResult result)
        {
            const string scenarioId = "verification.catalog-retry";
            RefreshableDefinitionCatalog innerCatalog = new RefreshableDefinitionCatalog(scenarioId);
            ScenarioRuntimeOrchestrator orchestrator = null;
            ScenarioDefinitionCatalogRefreshCoordinator catalog = new ScenarioDefinitionCatalogRefreshCoordinator(
                innerCatalog,
                delegate { return orchestrator; });
            ScenarioRuntimeDefinitionResolver definitionResolver = new ScenarioRuntimeDefinitionResolver(catalog);
            CountingScenarioApplier applier = new CountingScenarioApplier();
            VerificationRuntimeBindingService bindings = new VerificationRuntimeBindingService(
                new ScenarioRuntimeBinding
                {
                    ScenarioId = scenarioId,
                    VersionApplied = "1.0.0",
                    IsActive = true,
                    RunId = "catalog-retry-run"
                });

            orchestrator = new ScenarioRuntimeOrchestrator(
                new EmptyScenarioLifecycle(),
                new EmptyScenarioRegistry(),
                new MatchingDependencyVerifier(),
                new UnusedDefinitionFactory(),
                definitionResolver,
                bindings,
                applier,
                new EmptySpriteSwapEngine(),
                new EmptySceneSpritePlacementEngine(),
                new ReadyVanillaScenarioRuntime());

            orchestrator.UpdateActiveScenarioApply();
            int attemptsWhileMissing = innerCatalog.LoadAttempts;
            orchestrator.UpdateActiveScenarioApply();
            Assert(applier.ApplyCount == 0 && attemptsWhileMissing == 1 && innerCatalog.LoadAttempts == attemptsWhileMissing,
                "Missing definition was not retained as a blocked runtime apply pending catalog refresh.", result);

            catalog.RefreshDefinitionCatalog();
            Assert(innerCatalog.CatalogRevision == 1 && innerCatalog.LoadAttempts == attemptsWhileMissing + 1
                && applier.ApplyCount == 1,
                "Catalog refresh did not cause the blocked active binding to retry and apply.", result);
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition)
                result.AddError(message);
        }

        private sealed class StubRetryableHandler : IScenarioRetryableEffectHandler
        {
            public bool CanHandle(ScenarioEffectKind kind)
            {
                return kind == ScenarioEffectKind.StartConversation;
            }

            public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
            {
                bool retryable;
                return Handle(definition, effect, state, out message, out retryable);
            }

            public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message, out bool retryable)
            {
                message = "Starting family is not materialized yet.";
                retryable = true;
                return false;
            }
        }

        private sealed class RefreshableDefinitionCatalog : IScenarioDefinitionCatalogService
        {
            private readonly string _scenarioId;
            private bool _available;

            public RefreshableDefinitionCatalog(string scenarioId)
            {
                _scenarioId = scenarioId;
            }

            public int CatalogRevision { get; private set; }
            public int LoadAttempts { get; private set; }

            public void RefreshDefinitionCatalog()
            {
                _available = true;
                CatalogRevision++;
            }

            public ScenarioInfo[] ListDefinitions()
            {
                return new ScenarioInfo[0];
            }

            public ScenarioValidationResult ValidateDefinition(string scenarioId)
            {
                ScenarioDefinition ignoredDefinition;
                string ignoredPath;
                ScenarioValidationResult validation;
                TryLoadDefinition(scenarioId, out ignoredDefinition, out ignoredPath, out validation);
                return validation;
            }

            public bool TryLoadDefinition(
                string scenarioId,
                out ScenarioDefinition definition,
                out string scenarioFilePath,
                out ScenarioValidationResult validation)
            {
                LoadAttempts++;
                definition = null;
                scenarioFilePath = null;
                validation = new ScenarioValidationResult();
                if (!_available || !string.Equals(_scenarioId, scenarioId, System.StringComparison.OrdinalIgnoreCase))
                {
                    validation.AddError("Scenario definition is not indexed.");
                    return false;
                }

                definition = new ScenarioDefinition { Id = _scenarioId, Version = "1.0.0" };
                scenarioFilePath = "verification-scenario.xml";
                return true;
            }
        }

        private sealed class VerificationRuntimeBindingService : IScenarioRuntimeBindingService
        {
            private ScenarioRuntimeBinding _binding;

            public VerificationRuntimeBindingService(ScenarioRuntimeBinding binding)
            {
                _binding = binding;
            }

            public ScenarioRuntimeBinding CurrentBinding { get { return _binding; } }
            public int CurrentRevision { get { return 1; } }
            public void EnsureHooked() { }
            public void SetBinding(ScenarioRuntimeBinding binding) { _binding = binding; }
            public void ConvertToNormalSave() { if (_binding != null) _binding.IsConvertedToNormalSave = true; }
            public ScenarioRuntimeBinding GetActiveBindingForStartup() { return _binding; }
        }

        private sealed class CountingScenarioApplier : IScenarioApplier
        {
            public int ApplyCount { get; private set; }
            public ScenarioApplyResult ApplyAll(ScenarioDefinition definition) { return ApplyAll(definition, null); }
            public ScenarioApplyResult ApplyAll(ScenarioDefinition definition, string scenarioFilePath)
            {
                ApplyCount++;
                return new ScenarioApplyResult();
            }
        }

        private sealed class EmptySpriteSwapEngine : IScenarioSpriteSwapEngine
        {
            public void Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result) { }
            public void Update() { }
            public void Clear(string reason) { }
        }

        private sealed class EmptySceneSpritePlacementEngine : IScenarioSceneSpritePlacementEngine
        {
            public int Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result) { return 0; }
            public void Clear(string reason) { }
        }

        private sealed class EmptyScenarioLifecycle : ICustomScenarioLifecycleService
        {
            public CustomScenarioState CurrentState { get { return CustomScenarioState.None(); } }
            public bool MarkSelected(string scenarioId) { return false; }
            public bool MarkSpawned(string scenarioId) { return false; }
            public void ClearState() { }
        }

        private sealed class EmptyScenarioRegistry : ICustomScenarioRegistry
        {
            public bool TryGet(string scenarioId, out CustomScenarioInfo scenario) { scenario = null; return false; }
            public CustomScenarioInfo[] List() { return new CustomScenarioInfo[0]; }
        }

        private sealed class MatchingDependencyVerifier : IScenarioDependencyVerifier
        {
            public SlotManifest CreateDependencyManifest(CustomScenarioInfo info) { return null; }
            public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
            {
                return ScenarioDependencyVerificationState.Match;
            }
        }

        private sealed class UnusedDefinitionFactory : IScenarioDefinitionFactory
        {
            public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
            {
                definition = null;
                errorMessage = "Not used by runtime-apply verification.";
                return false;
            }

            public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
            {
                definition = null;
                errorMessage = "Not used by runtime-apply verification.";
                return false;
            }

            public ScenarioDef BuildScenarioDefFromDefinition(string scenarioId) { return null; }
        }

        private sealed class ReadyVanillaScenarioRuntime : IVanillaScenarioRuntime
        {
            public bool IsWorldReady(out string blockingReason) { blockingReason = null; return true; }
            public bool TrySpawnScenario(ScenarioDef definition, out QuestInstance instance, out string reason) { instance = null; reason = null; return false; }
            public bool TryStartQuest(string questId, out string reason) { reason = null; return false; }
            public bool TryGetQuestInstance(int instanceId, out QuestInstance instance, out string reason) { instance = null; reason = null; return false; }
            public System.Collections.Generic.List<QuestInstance> GetCurrentQuests() { return new System.Collections.Generic.List<QuestInstance>(); }
            public bool TryFinishQuest(QuestInstance instance, bool success, out string reason) { reason = null; return false; }
            public bool TryAbandonQuest(QuestInstance instance, out string reason) { reason = null; return false; }
        }
    }
}

using System;
using System.Collections.Generic;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal enum ScenarioDependencyVerificationState
    {
        Match = 0,
        VersionMismatch = 1,
        Warning = 2,
        Missing = 3,
        Unknown = 4
    }

    internal interface IScenarioDefinitionSerializer
    {
        ScenarioDefinition Load(string filePath);
        bool TryLoadWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered);
        ScenarioDefinition FromXml(string xml);
        void Save(ScenarioDefinition definition, string filePath);
        string ToXml(ScenarioDefinition definition);
        ScenarioInfo LoadInfo(string filePath, string ownerModId);
    }

    internal interface IScenarioDefinitionCatalog
    {
        void Refresh();
        ScenarioInfo[] ListAll();
        bool TryGet(string scenarioId, out ScenarioInfo info);
    }

    internal interface IScenarioDefinitionValidator
    {
        ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath);
    }

    internal interface IScenarioDefinitionReader
    {
        ScenarioInfo[] ListAll();
        bool TryGetInfo(string scenarioId, out ScenarioInfo info);
        ScenarioValidationResult Validate(string scenarioId);
        bool TryLoad(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
        bool TryLoadUnchecked(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out string errorMessage);
    }

    internal interface IScenarioStateManager
    {
        event Action<ScenarioStateSnapshot> StateChanged;

        CustomScenarioState GetCustomScenarioState();
        void SetCustomScenarioState(CustomScenarioState state, string source, string reason);
        ScenarioRuntimeBinding GetRuntimeBinding();
        void SetRuntimeBinding(ScenarioRuntimeBinding binding, string source, string reason);
        void ConvertRuntimeBindingToNormalSave(string source, string reason);
        int RuntimeBindingRevision { get; }
    }

    internal sealed class ScenarioStateSnapshot
    {
        public CustomScenarioState CustomScenarioState { get; set; }
        public ScenarioRuntimeBinding RuntimeBinding { get; set; }
        public int RuntimeBindingRevision { get; set; }
        public string Source { get; set; }
        public string Reason { get; set; }
    }

    internal interface ICustomScenarioRegistry
    {
        bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
        CustomScenarioInfo[] List();
    }

    internal interface IScenarioDefinitionCatalogService
    {
        int CatalogRevision { get; }
        void RefreshDefinitionCatalog();
        ScenarioInfo[] ListDefinitions();
        ScenarioValidationResult ValidateDefinition(string scenarioId);
        bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
    }

    internal interface IScenarioDefinitionFactory
    {
        bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
        bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage);
        ScenarioDef BuildScenarioDefFromDefinition(string scenarioId);
    }

    internal interface ICustomScenarioLifecycleService
    {
        CustomScenarioState CurrentState { get; }
        bool MarkSelected(string scenarioId);
        bool MarkSpawned(string scenarioId);
        void ClearState();
    }

    internal interface IScenarioDependencyVerifier
    {
        SlotManifest CreateDependencyManifest(CustomScenarioInfo info);
        ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info);
    }

    internal interface IScenarioDefinitionDependencyReader
    {
        ScenarioModDependency[] LoadDefinitionDependencies(string scenarioId);
    }

    internal interface IShelteredCustomScenarioService : ICustomScenarioService
    {
    }

    internal interface IScenarioRuntimeBindingService
    {
        ScenarioRuntimeBinding CurrentBinding { get; }
        int CurrentRevision { get; }
        void EnsureHooked();
        void SetBinding(ScenarioRuntimeBinding binding);
        void ConvertToNormalSave();
        ScenarioRuntimeBinding GetActiveBindingForStartup();
    }

    internal interface IScenarioRuntimeBindingPersistence
    {
        ScenarioRuntimeBinding Load(SaveData data);
        void Save(SaveData data, ScenarioRuntimeBinding binding);
    }

    internal interface IVanillaScenarioRuntime
    {
        bool IsWorldReady(out string blockingReason);
        bool TrySpawnScenario(ScenarioDef definition, out QuestInstance instance, out string reason);
        bool TryStartQuest(string questId, out string reason);
        bool TryGetQuestInstance(int instanceId, out QuestInstance instance, out string reason);
        List<QuestInstance> GetCurrentQuests();
        bool TryFinishQuest(QuestInstance instance, bool success, out string reason);
    }

    internal interface IScenarioQuestInstanceResolver
    {
        bool TryResolve(ScenarioRuntimeBinding binding, out QuestInstance instance, out string reason);
    }

    internal interface IScenarioWinLossConditionAdapter
    {
        bool TryCreateConditionRef(ScenarioDefinition definition, ScenarioRuntimeBinding binding, ConditionDef condition, out ScenarioConditionRef conditionRef, out string reason);
    }

    internal interface IScenarioWinLossOutcomeService
    {
        bool IsOutcomeArmed { get; }
        bool IsPresentationPending { get; }
        void ResetForNewRun();
        void Initialize(ScenarioDefinition definition, ScenarioRuntimeBinding binding);
        void Tick(ScenarioRuntimeState state);
    }

    internal interface IScenarioEndGamePresenter
    {
        void ResetForNewRun();
        bool TryPresent(ScenarioEndGamePresentation presentation, out string reason);
    }

    internal sealed class ScenarioEndGamePresentation
    {
        public bool Success { get; set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        public string ScenarioDisplayName { get; set; }
        public int DaysSurvived { get; set; }
        public string FulfilledConditionText { get; set; }
    }

    internal interface IScenarioAuthoringSessionContext
    {
        bool HasActiveSession { get; }
    }

    internal interface IScenarioScoreSnapshotService
    {
        ScenarioScoreSnapshot GetSnapshot();
        void SetSnapshot(ScenarioScoreSnapshot snapshot);
        void ClearSnapshot();
    }

    internal interface IScenarioTriggerRuntimeService
    {
        bool Fire(string triggerId, string source, out string message);
        bool Fire(ScenarioRuntimeState state, string triggerId, string source, out string message);
        bool HasFired(ScenarioRuntimeState state, string triggerId);
    }

    internal interface IScenarioSpriteAssetResolver
    {
        Sprite ResolveSprite(ScenarioDefinition definition, string packRoot, string spriteId, string relativePath, string runtimeSpriteKey, string contextLabel);
        string ResolveRelativePath(ScenarioDefinition definition, string spriteId, string relativePath);
        void Invalidate();
    }

    internal interface IScenarioSpriteSwapEngine
    {
        void Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result);
        void Update();
        void Clear(string reason);
    }

    internal interface IScenarioSceneSpritePlacementEngine
    {
        int Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result);
        void Clear(string reason);
    }

    internal interface IScenarioApplier
    {
        ScenarioApplyResult ApplyAll(ScenarioDefinition definition);
        ScenarioApplyResult ApplyAll(ScenarioDefinition definition, string scenarioFilePath);
    }

    internal interface IScenarioRuntimeDefinitionOverrideProvider
    {
        bool TryGetDefinitionOverride(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath);
    }

    internal interface IScenarioPlaytestOrchestrator
    {
        ScenarioApplyResult BeginPlaytest(ScenarioEditorSession session, string scenarioFilePath);
        void EndPlaytest(ScenarioEditorSession session);
    }

    internal interface IScenarioEditorService
    {
        ScenarioEditorSession CurrentSession { get; }
        ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode);
        ScenarioEditorSession LoadEditMode(string scenarioFilePath);
        ScenarioValidationResult CommitChanges(string scenarioFilePath);
        ScenarioApplyResult BeginPlaytest();
        void EndPlaytest();
        void ConvertToNormalSave();
        void RequestRestart();
        void CloseEditor(bool resumeGame);
        void MaintainAuthoringPause();
    }

    internal interface IScenarioPauseService
    {
        bool OwnsPause { get; }
        bool EnsurePaused(string reason);
        void ReleasePause(string reason);
        bool ShouldSuppressPauseMenu();
        bool IsPauseMenuPanel(BasePanel panel);
    }

    internal interface IScenarioRuntimeOrchestrator
    {
        void UpdatePendingScenarioSpawn();
        void UpdateActiveScenarioApply();
    }
}

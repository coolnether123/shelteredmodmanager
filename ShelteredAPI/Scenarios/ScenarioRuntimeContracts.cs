using System;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public enum ScenarioDependencyVerificationState
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

    internal interface IShelteredCustomScenarioService : ICustomScenarioService
    {
        void RefreshDefinitionCatalog();
        ScenarioInfo[] ListDefinitions();
        ScenarioValidationResult ValidateDefinition(string scenarioId);
        bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
        bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage);
        bool MarkSelected(string scenarioId);
        bool MarkSpawned(string scenarioId);
        void ClearState();
        SlotManifest CreateDependencyManifest(CustomScenarioInfo info);
        ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info);
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
        bool TryGetActiveWorkingDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath);
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

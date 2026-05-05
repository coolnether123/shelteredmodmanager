using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Selection{
    internal enum ScenarioCatalogSource
    {
        Vanilla = 0,
        Modded = 1,
        Draft = 2
    }

    internal enum ScenarioLaunchMode
    {
        Survival = 0,
        Surrounded = 1,
        Stasis = 2,
        CustomDefinition = 3,
        AuthoringDraft = 4
    }

    internal sealed class ScenarioCatalogEntry
    {
        public string ScenarioId { get; set; }
        public string StorageScenarioId { get; set; }
        public ScenarioCatalogSource Source { get; set; }
        public ScenarioLaunchMode LaunchMode { get; set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        public SaveManager.SaveType DefaultSaveType { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string OwnerModId { get; set; }
        public int Order { get; set; }
        public int SaveCount { get; set; }
        public bool CanStart { get; set; }
        public ScenarioDependencyVerificationState DependencyState { get; set; }
        public SlotManifest DependencyManifest { get; set; }
        public CustomScenarioInfo CustomScenario { get; set; }

        public bool IsVanilla
        {
            get { return Source == ScenarioCatalogSource.Vanilla; }
        }

        public bool IsModded
        {
            get { return Source == ScenarioCatalogSource.Modded; }
        }
    }

    internal interface IScenarioSelectionCatalogService
    {
        void Refresh();
        ScenarioCatalogEntry[] ListAll();
        ScenarioCatalogEntry[] ListBySource(ScenarioCatalogSource source);
        bool TryGet(string scenarioId, out ScenarioCatalogEntry entry);
    }

    internal interface IScenarioSaveLibrary
    {
        string ToStorageScenarioId(string scenarioId);
        SaveEntry[] ListSaves(string scenarioId);
        int CountSaves(string scenarioId);
        int GetNextAvailableSlot(string scenarioId);
        SaveEntry Get(string scenarioId, string saveId);
        SaveEntry GetBySlot(string scenarioId, int absoluteSlot);
        SaveEntry CreateNext(string scenarioId, SaveCreateOptions options);
        bool Delete(string scenarioId, string saveId);
        bool DeleteBySlot(string scenarioId, int absoluteSlot);
        void QueueNewGameSaveTarget(string scenarioId, SaveEntry startupSave, SaveManager.SaveType saveType);
        void QueueLoadTarget(string scenarioId, SaveEntry save, SaveManager.SaveType saveType);
        bool ClearQueuedNewGameSave(SaveManager.SaveType saveType);
        bool ClearQueuedLoad(SaveManager.SaveType saveType);
    }
}

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal enum ScenarioBookBrowserViewKind
    {
        Types,
        Scenarios,
        Saves,
        DraftDetails
    }

    internal enum ScenarioBookType
    {
        Published,
        Surrounded,
        Stasis,
        Draft
    }

    internal enum ScenarioBookRowKind
    {
        Empty,
        Type,
        Scenario,
        StartScenario,
        OpenDraft,
        CreateDraft,
        LoadSave
    }

    internal sealed class ScenarioBookRowModel
    {
        public ScenarioBookRowKind Kind;
        public string Title;
        public string Detail;
        public string Badge;
        public bool IsLocked;
        public bool CanDelete;
        public ScenarioBookType Type;
        public ScenarioCatalogEntry Scenario;
        public SaveEntry Save;
        public ScenarioBookSaveDetailModel SaveDetail;
    }

    internal sealed class ScenarioBookDraftEditorModel
    {
        public ScenarioCatalogEntry Scenario;
        public string DisplayName;
        public string Description;
    }

    internal sealed class ScenarioBookSaveDetailModel
    {
        public SaveEntry Save;
        public bool IsVanilla;
        public int DaysSurvived;
        public string SaveTime;
        public bool HasBinding;
        public string BindingScenarioId;
        public string VersionApplied;
        public bool IsActive;
        public bool IsConvertedToNormalSave;
        public int DayCreated;
        public int? ScenarioQuestInstanceId;
        public bool HasRuntimeState;
        public string ScenarioOutcome;
        public string ScenarioOutcomeConditionId;
        public int LastProcessedDay;
        public bool HasScoreSnapshot;
        public bool ScoreHasTotal;
        public int ScoreTotal;
        public string ScoreCompletionState;
        public int ScoreDay;
        public string MetadataError;
    }

    internal sealed class ScenarioBookPlayStatsModel
    {
        public int SaveCount;
        public int ActiveSaveCount;
        public int ConvertedSaveCount;
        public int CompletedSaveCount;
        public int WinCount;
        public int LossCount;
        public int BestDaySurvived;
        public bool HasBindingData;
        public bool HasOutcomeData;
        public bool HasScoreData;
        public string ScoreSummary;
    }
}

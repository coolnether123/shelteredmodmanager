using System.Collections.Generic;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal enum ScenarioLibrarySortMode
    {
        PinnedFirst,
        RecentlyPlayed,
        RecentlyDownloaded,
        CreationDate,
        Name
    }

    internal enum ScenarioBookBrowserViewKind
    {
        Types,
        Scenarios,
        Saves
    }

    internal enum ScenarioBookType
    {
        Published,
        Surrounded,
        Stasis
    }

    internal enum ScenarioBookRowKind
    {
        Empty,
        Type,
        Scenario,
        StartScenario,
        LoadSave,
        OpenScenarioSaves
    }

    internal sealed class ScenarioBookRowModel
    {
        public ScenarioBookRowKind Kind;
        public string Title;
        public string Detail;
        public string Badge;
        public string SectionLabel;
        public bool IsLocked;
        public bool CanDelete;
        public ScenarioBookType Type;
        public ScenarioCatalogEntry Scenario;
        public SaveEntry Save;
        public ScenarioBookSaveDetailModel SaveDetail;
        public bool IsPinned;
        public ScenarioLibrarySortMode LibrarySortMode;
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
        public ScenarioBookPlayStatsModel()
        {
            ScoreLines = new List<ScenarioBookStatLine>();
        }

        public int SaveCount;
        public int ActiveSaveCount;
        public int ConvertedSaveCount;
        public int CompletedSaveCount;
        public int WinCount;
        public int LossCount;
        public int BestDaySurvived;
        public int ScoredSaveCount;
        public int BestScoreTotal;
        public bool HasBestScoreTotal;
        public bool HasBindingData;
        public bool HasOutcomeData;
        public bool HasScoreData;
        public string ScoreSummary;
        public List<ScenarioBookStatLine> ScoreLines;
    }

    internal sealed class ScenarioBookStatLine
    {
        public string Label;
        public string Value;
    }
}

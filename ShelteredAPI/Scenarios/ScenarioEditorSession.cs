using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public enum ScenarioPlaytestState
    {
        Idle = 0,
        Playtesting = 1,
        Paused = 2
    }

    public enum ScenarioEditCategory
    {
        Family = 0,
        Inventory = 1,
        Bunker = 2,
        Triggers = 3,
        Assets = 4,
        WinLoss = 5
    }

    public enum ScenarioDirtySection
    {
        None = 0,
        Meta = 1,
        Family = 2,
        Inventory = 4,
        Bunker = 8,
        Triggers = 16,
        WinLoss = 32,
        Assets = 64
    }

    /// <summary>
    /// In-memory editor state only. Persist the WorkingDefinition through the XML
    /// serializer; never serialize this session object directly, because dirty and
    /// playtest state are editor concerns and should not leak into scenario packs.
    /// </summary>
    public sealed class ScenarioEditorSession
    {
        public ScenarioEditorSession()
        {
            DirtyFlags = new List<ScenarioDirtySection>();
            PlaytestState = ScenarioPlaytestState.Idle;
            CurrentEditCategory = ScenarioEditCategory.Family;
        }

        public ScenarioDefinition WorkingDefinition { get; set; }
        public ScenarioDefinition OriginalDefinition { get; set; }
        public List<ScenarioDirtySection> DirtyFlags { get; private set; }
        public ScenarioPlaytestState PlaytestState { get; set; }
        public bool RequestedRestart { get; set; }
        public ScenarioEditCategory CurrentEditCategory { get; set; }
        public bool HasAppliedToCurrentWorld { get; set; }
    }
}

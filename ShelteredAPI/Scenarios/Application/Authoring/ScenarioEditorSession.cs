using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal enum ScenarioPlaytestState
    {
        Idle = 0,
        Playtesting = 1,
        Paused = 2
    }

    internal enum ScenarioEditCategory
    {
        Family = 0,
        Inventory = 1,
        Bunker = 2,
        Triggers = 3,
        Assets = 4,
        WinLoss = 5,
        Map = 6
    }

    internal enum ScenarioDirtySection
    {
        None = 0,
        Meta = 1,
        Family = 2,
        Inventory = 4,
        Bunker = 8,
        Triggers = 16,
        WinLoss = 32,
        Assets = 64,
        Map = 128,
        AuthorTestChecklist = 256
    }

    /// <summary>
    /// In-memory editor state only. Persist the WorkingDefinition through the XML
    /// serializer; never serialize this session object directly, because dirty and
    /// playtest state are editor concerns and should not leak into scenario packs.
    /// </summary>
    internal sealed class ScenarioEditorSession
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
        public string LoadWarning { get; set; }
        public int DraftRevision { get; private set; }
        public int AppliedDraftRevision { get; private set; }

        public bool HasUnappliedDraftChanges
        {
            get { return HasAppliedToCurrentWorld && AppliedDraftRevision != DraftRevision; }
        }

        public void MarkDraftChanged(ScenarioDirtySection section, ScenarioEditCategory category)
        {
            if (DirtyFlags == null)
                DirtyFlags = new List<ScenarioDirtySection>();
            if (section != ScenarioDirtySection.None && !DirtyFlags.Contains(section))
                DirtyFlags.Add(section);
            CurrentEditCategory = category;
            DraftRevision++;
        }

        public void MarkDraftChanged(ScenarioDirtySection section)
        {
            MarkDraftChanged(section, CurrentEditCategory);
        }

        public void MarkAppliedToCurrentWorld()
        {
            HasAppliedToCurrentWorld = true;
            AppliedDraftRevision = DraftRevision;
        }

        // A stopped playtest must not keep claiming ownership of the live world.
        // The next start reapplies the saved draft and creates a fresh completion
        // carrier instead of being rejected as a stale continuation.
        public void ResetStoppedPlaytestWorld()
        {
            HasAppliedToCurrentWorld = false;
            AppliedDraftRevision = DraftRevision;
            RequestedRestart = false;
        }

        public void MarkChecklistChanged()
        {
            if (DirtyFlags == null)
                DirtyFlags = new List<ScenarioDirtySection>();
            if (!DirtyFlags.Contains(ScenarioDirtySection.AuthorTestChecklist))
                DirtyFlags.Add(ScenarioDirtySection.AuthorTestChecklist);
        }
    }
}

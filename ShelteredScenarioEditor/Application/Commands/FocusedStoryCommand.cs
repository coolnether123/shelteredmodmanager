using System.Globalization;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum FocusedStoryCommandKind
    {
        OpenStage
    }

    /// <summary>Navigation command for opening a Story stage document.</summary>
    internal sealed class FocusedStoryCommand : ScenarioAuthoringCommand
    {
        private const string ActionPrefix = "scenario.story.focused_editor.stage.open.";
        private FocusedStoryCommand(int stageIndex)
            : base(ActionPrefix + stageIndex.ToString(CultureInfo.InvariantCulture), ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = FocusedStoryCommandKind.OpenStage;
            StageIndex = stageIndex;
        }

        internal FocusedStoryCommandKind Kind { get; private set; }
        internal int StageIndex { get; private set; }

        internal static FocusedStoryCommand OpenStage(int stageIndex)
        {
            return new FocusedStoryCommand(stageIndex);
        }

        internal bool ValidateStructure(out string reason)
        {
            reason = StageIndex >= 0 ? null : "Story stage index is invalid.";
            return reason == null;
        }
    }
}

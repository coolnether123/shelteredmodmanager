using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Domain.Timeline;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
namespace ShelteredScenarioEditor.Application.Timeline{
    internal sealed class ScenarioTimelineNavigationService
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;

        public ScenarioTimelineNavigationService(
            ScenarioAuthoringLayoutService layoutService,
            IScenarioEditorService editorService,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            _layoutService = layoutService;
            _editorService = editorService;
            _rendererInteraction = rendererInteraction;
        }

        public bool Navigate(ScenarioAuthoringState state, ScenarioTimelineEntry entry, out string message)
        {
            message = null;
            if (state == null || entry == null)
                return false;

            ScenarioStageKind stage = ResolveStage(entry);
            if (_layoutService != null)
            {
                _layoutService.SelectStage(state, stage);
                if (!string.IsNullOrEmpty(entry.OwnerWindowId))
                    _layoutService.SetWindowOpen(state, entry.OwnerWindowId, true);
            }
            state.TimelineSelectionId = entry.Id;
            state.TimelineSelectedEntryId = entry.Id;
            ApplyFocusedEditorLink(state, entry);
            ScenarioAuthoringTarget target = BuildTimelineTarget(entry, stage);
            if (target != null)
            {
                state.SelectedTarget = target;
                state.HoveredTarget = null;
                state.MultiSelection.Clear();
                state.MultiSelection.Add(target.Copy());
            }
            state.ShellVisible = true;
            string focusedTitle = entry.Title ?? entry.Id;
            if (entry.Kind == ScenarioTimelineEntryKind.Quest)
            {
                const string questPrefix = "Quest ";
                string authoredTitle = focusedTitle != null && focusedTitle.StartsWith(questPrefix, System.StringComparison.OrdinalIgnoreCase)
                    ? focusedTitle.Substring(questPrefix.Length)
                    : focusedTitle;
                string displayTitle = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                    authoredTitle,
                    authoredTitle,
                    entry.SourceId ?? entry.TargetId ?? entry.Id,
                    "Quest").Text;
                focusedTitle = questPrefix + displayTitle;
            }
            message = target != null
                ? "Focused " + focusedTitle + " in " + stage + "."
                : "Timeline target is missing: " + (entry.TargetId ?? entry.Id) + ".";
            return true;
        }

        private static ScenarioStageKind ResolveStage(ScenarioTimelineEntry entry)
        {
            switch (entry.Kind)
            {
                case ScenarioTimelineEntryKind.Inventory:
                    return ScenarioStageKind.InventoryStorage;
                case ScenarioTimelineEntryKind.Survivor:
                    return ScenarioStageKind.People;
                case ScenarioTimelineEntryKind.Quest:
                case ScenarioTimelineEntryKind.Story:
                    return ScenarioStageKind.Quests;
                case ScenarioTimelineEntryKind.Map:
                    return ScenarioStageKind.Map;
                case ScenarioTimelineEntryKind.Weather:
                case ScenarioTimelineEntryKind.WorldEvent:
                case ScenarioTimelineEntryKind.Journal:
                case ScenarioTimelineEntryKind.CustomModded:
                    return ScenarioStageKind.Events;
                case ScenarioTimelineEntryKind.Bunker:
                case ScenarioTimelineEntryKind.Object:
                default:
                    return ResolveBunkerStage(entry);
            }
        }

        private void ApplyFocusedEditorLink(ScenarioAuthoringState state, ScenarioTimelineEntry entry)
        {
            if (state == null || entry == null)
                return;

            FocusedStoryCommand story = entry.FocusCommand as FocusedStoryCommand;
            if (story != null && story.Kind == FocusedStoryCommandKind.OpenStage)
            {
                ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
                ScenarioStoryFocusedEditorActions.SelectStageDocument(
                    session != null ? session.WorkingDefinition : null,
                    story.StageIndex,
                    _rendererInteraction);
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
                return;
            }

            GameplayScheduleCommand gameplay = entry.FocusCommand as GameplayScheduleCommand;
            if (gameplay != null)
            {
                ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
                if (gameplay.Kind == GameplayScheduleCommandKind.OpenFutureSurvivor)
                    ScenarioCastWorkspaceActions.SelectFutureDocument(session != null ? session.WorkingDefinition : null, gameplay.Index, _rendererInteraction);
                else if (gameplay.Kind == GameplayScheduleCommandKind.OpenQuestDocument)
                    ScenarioStoryFocusedEditorActions.SelectQuestDocument(session != null ? session.WorkingDefinition : null, gameplay.Index, _rendererInteraction);
                else if (gameplay.Kind == GameplayScheduleCommandKind.OpenTimedPicker)
                    SetFocusedEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, gameplay.Index);
                else if (gameplay.Kind == GameplayScheduleCommandKind.OpenWeatherEditor)
                    SetFocusedEditor(state, "weather", gameplay.Index);
                return;
            }

            TimelineNavigationCommand timeline = entry.FocusCommand as TimelineNavigationCommand;
            if (timeline == null || timeline.Index < 0)
                return;
            if (timeline.Kind == TimelineNavigationCommandKind.FocusTrigger)
                SetFocusedEditor(state, "trigger", timeline.Index);
            else if (timeline.Kind == TimelineNavigationCommandKind.FocusScheduledAction)
                SetFocusedEditor(state, entry.Kind == ScenarioTimelineEntryKind.WorldEvent ? ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent : "scheduled_action", timeline.Index);
            else if (timeline.Kind == TimelineNavigationCommandKind.FocusJournalEntry)
                SetFocusedEditor(state, "journal_entry", timeline.Index);
        }

        private static void SetFocusedEditor(ScenarioAuthoringState state, string kind, int index)
        {
            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = false;
        }

        private static ScenarioStageKind ResolveBunkerStage(ScenarioTimelineEntry entry)
        {
            string text = ((entry != null ? entry.Title : null) ?? string.Empty) + " "
                + ((entry != null ? entry.Type : null) ?? string.Empty) + " "
                + ((entry != null ? entry.TargetId : null) ?? string.Empty);
            text = text.ToLowerInvariant();
            ScenarioTargetScope scope = ScenarioTargetScopeTextMatcher.MatchBunkerScope(text);
            if (scope == ScenarioTargetScope.BunkerBackground)
                return ScenarioStageKind.BunkerBackground;
            if (scope == ScenarioTargetScope.BunkerSurface)
                return ScenarioStageKind.BunkerSurface;
            return ScenarioStageKind.BunkerInside;
        }

        private static ScenarioAuthoringTarget BuildTimelineTarget(ScenarioTimelineEntry entry, ScenarioStageKind stage)
        {
            if (entry == null)
                return null;

            ScenarioAuthoringTargetKind kind = ScenarioAuthoringTargetKind.Unknown;
            if (entry.Kind == ScenarioTimelineEntryKind.Survivor)
                kind = ScenarioAuthoringTargetKind.Character;
            else if (entry.Kind == ScenarioTimelineEntryKind.Bunker)
                kind = ScenarioAuthoringTargetKind.Room;
            else if (entry.Kind == ScenarioTimelineEntryKind.Object)
                kind = ScenarioAuthoringTargetKind.PlaceableObject;

            string sourceId = !string.IsNullOrEmpty(entry.SourceId) ? entry.SourceId : entry.TargetId;
            string targetId = !string.IsNullOrEmpty(entry.TargetId) ? entry.TargetId : sourceId;

            return new ScenarioAuthoringTarget
            {
                Id = BuildSourceTargetId(entry),
                Kind = kind,
                DisplayName = entry.Title ?? targetId,
                Description = "Timeline source " + Safe(entry.SourceCollection) + " #" + entry.SourceIndex + " source " + Safe(sourceId) + " target " + Safe(targetId) + " focus " + Safe(entry.FocusAutomationId) + ".",
                AdapterId = "ShelteredAPI.Timeline",
                GameObjectName = sourceId,
                TransformPath = stage + "/" + Safe(entry.OwnerWindowId) + "/" + Safe(entry.SourceCollection) + "/" + entry.SourceIndex + "/" + Safe(entry.FocusAutomationId),
                ScenarioReferenceId = sourceId,
                SupportsInspect = true,
                SupportsReplace = entry.Kind == ScenarioTimelineEntryKind.Object || entry.Kind == ScenarioTimelineEntryKind.Bunker
            };
        }

        private static string BuildSourceTargetId(ScenarioTimelineEntry entry)
        {
            return "timeline:" + Safe(entry.SourceCollection) + ":" + entry.SourceIndex + ":" + Safe(entry.SourceId ?? entry.TargetId ?? entry.Id);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

    }
}

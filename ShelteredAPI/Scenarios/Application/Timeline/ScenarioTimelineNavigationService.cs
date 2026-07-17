using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Application.Timeline{
    internal sealed class ScenarioTimelineNavigationService
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly IScenarioEditorService _editorService;

        public ScenarioTimelineNavigationService(ScenarioAuthoringLayoutService layoutService, IScenarioEditorService editorService)
        {
            _layoutService = layoutService;
            _editorService = editorService;
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
            message = target != null
                ? "Focused " + (entry.Title ?? entry.Id) + " in " + stage + "."
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

            if (entry.Kind == ScenarioTimelineEntryKind.Story)
            {
                int storyStageIndex = ResolveStoryStageIndex(entry);
                if (storyStageIndex >= 0)
                {
                    ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
                    ScenarioStoryFocusedEditorActions.SelectStageDocument(
                        session != null ? session.WorkingDefinition : null,
                        storyStageIndex);
                    state.FocusedEditorIndex = -1;
                    state.FocusedEditorIsNew = false;
                    return;
                }
            }

            if (string.IsNullOrEmpty(entry.FocusActionId))
                return;

            string prefix = ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix;
            if (entry.FocusActionId.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                int survivorIndex;
                if (!int.TryParse(entry.FocusActionId.Substring(prefix.Length), out survivorIndex))
                    return;

                ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
                ScenarioCastWorkspaceActions.SelectFutureDocument(
                    session != null ? session.WorkingDefinition : null,
                    survivorIndex);
                return;
            }

            if (!string.Equals(entry.SourceKind, "scheduled_action", System.StringComparison.OrdinalIgnoreCase)
                || entry.SourceIndex < 0)
                return;

            state.FocusedEditorKind = entry.Kind == ScenarioTimelineEntryKind.WorldEvent
                ? ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent
                : "scheduled_action";
            state.FocusedEditorIndex = entry.SourceIndex;
            state.FocusedEditorIsNew = false;
        }

        private static int ResolveStoryStageIndex(ScenarioTimelineEntry entry)
        {
            if (entry == null)
                return -1;
            string focusPrefix = ScenarioStoryFocusedEditorActions.ActionStageOpenPrefix;
            int focusedIndex;
            if (!string.IsNullOrEmpty(entry.FocusActionId)
                && entry.FocusActionId.StartsWith(focusPrefix, System.StringComparison.Ordinal)
                && int.TryParse(entry.FocusActionId.Substring(focusPrefix.Length), out focusedIndex))
            {
                return focusedIndex;
            }
            if (string.Equals(entry.SourceCollection, "ScenarioFlow.Stages", System.StringComparison.Ordinal))
                return entry.SourceIndex;

            const string prefix = "ScenarioFlow.Stages[";
            string collection = entry.SourceCollection;
            if (string.IsNullOrEmpty(collection) || !collection.StartsWith(prefix, System.StringComparison.Ordinal))
                return -1;
            int close = collection.IndexOf(']', prefix.Length);
            int parsed;
            return close > prefix.Length
                && int.TryParse(collection.Substring(prefix.Length, close - prefix.Length), out parsed)
                    ? parsed
                    : -1;
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
                Description = "Timeline source " + Safe(entry.SourceCollection) + " #" + entry.SourceIndex + " source " + Safe(sourceId) + " target " + Safe(targetId) + " focus " + Safe(entry.FocusActionId) + ".",
                AdapterId = "ShelteredAPI.Timeline",
                GameObjectName = sourceId,
                TransformPath = stage + "/" + Safe(entry.OwnerWindowId) + "/" + Safe(entry.SourceCollection) + "/" + entry.SourceIndex + "/" + Safe(entry.FocusActionId),
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

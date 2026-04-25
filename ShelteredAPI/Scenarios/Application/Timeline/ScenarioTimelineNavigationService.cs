using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTimelineNavigationService
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public ScenarioTimelineNavigationService(ScenarioAuthoringLayoutService layoutService)
        {
            _layoutService = layoutService;
        }

        public bool Navigate(ScenarioAuthoringState state, ScenarioTimelineEntry entry, out string message)
        {
            message = null;
            if (state == null || entry == null)
                return false;

            ScenarioStageKind stage = ResolveStage(entry);
            if (_layoutService != null)
                _layoutService.SelectStage(state, stage);
            state.TimelineSelectionId = entry.Id;
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
                    return ScenarioStageKind.Quests;
                case ScenarioTimelineEntryKind.Map:
                    return ScenarioStageKind.Map;
                case ScenarioTimelineEntryKind.Weather:
                case ScenarioTimelineEntryKind.CustomModded:
                    return ScenarioStageKind.Events;
                case ScenarioTimelineEntryKind.Bunker:
                case ScenarioTimelineEntryKind.Object:
                default:
                    return ResolveBunkerStage(entry);
            }
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
            if (entry == null || string.IsNullOrEmpty(entry.TargetId))
                return null;

            ScenarioAuthoringTargetKind kind = ScenarioAuthoringTargetKind.Unknown;
            if (entry.Kind == ScenarioTimelineEntryKind.Survivor)
                kind = ScenarioAuthoringTargetKind.Character;
            else if (entry.Kind == ScenarioTimelineEntryKind.Bunker)
                kind = ScenarioAuthoringTargetKind.Room;
            else if (entry.Kind == ScenarioTimelineEntryKind.Object)
                kind = ScenarioAuthoringTargetKind.PlaceableObject;

            return new ScenarioAuthoringTarget
            {
                Id = "timeline:" + entry.Id,
                Kind = kind,
                DisplayName = entry.Title ?? entry.TargetId,
                Description = "Timeline target " + entry.TargetId + ".",
                AdapterId = "ShelteredAPI.Timeline",
                GameObjectName = entry.TargetId,
                TransformPath = stage + "/" + entry.TargetId,
                ScenarioReferenceId = entry.TargetId,
                SupportsInspect = true,
                SupportsReplace = entry.Kind == ScenarioTimelineEntryKind.Object || entry.Kind == ScenarioTimelineEntryKind.Bunker
            };
        }

    }
}

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
            message = "Focused " + (entry.Title ?? entry.Id) + " in " + stage + ".";
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
                    return ScenarioStageKind.BunkerInside;
            }
        }
    }
}

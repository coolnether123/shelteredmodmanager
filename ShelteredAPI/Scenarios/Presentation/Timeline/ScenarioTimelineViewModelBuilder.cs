using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTimelineViewModelBuilder
    {
        public ScenarioTimelineViewModel Build(List<ScenarioTimelineDay> days)
        {
            ScenarioTimelineViewModel viewModel = new ScenarioTimelineViewModel();
            List<ScenarioTimelineDayViewModel> result = new List<ScenarioTimelineDayViewModel>();
            for (int i = 0; days != null && i < days.Count; i++)
            {
                ScenarioTimelineDay day = days[i];
                result.Add(BuildDay(day));
            }
            viewModel.Days = result.ToArray();
            return viewModel;
        }

        private static ScenarioTimelineDayViewModel BuildDay(ScenarioTimelineDay day)
        {
            List<ScenarioTimelineEntryViewModel> entries = new List<ScenarioTimelineEntryViewModel>();
            for (int i = 0; day != null && day.Entries != null && i < day.Entries.Count; i++)
                entries.Add(BuildEntry(day.Entries[i]));

            ScenarioTimelineDayViewModel result = new ScenarioTimelineDayViewModel();
            result.Day = day != null ? day.Day : 1;
            result.Count = entries.Count;
            result.Badge = entries.Count.ToString();
            result.Categories = BuildCategories(day);
            result.Entries = entries.ToArray();
            return result;
        }

        private static ScenarioTimelineEntryViewModel BuildEntry(ScenarioTimelineEntry entry)
        {
            ScenarioTimelineEntryViewModel model = new ScenarioTimelineEntryViewModel();
            model.Id = entry != null ? entry.Id : null;
            model.Time = FormatTime(entry != null ? entry.When : null);
            model.Title = entry != null ? entry.Title : string.Empty;
            model.Type = entry != null ? entry.Type : string.Empty;
            model.OwnerStage = entry != null ? entry.OwnerStage : string.Empty;
            model.Status = entry != null ? entry.Status.ToString() : string.Empty;
            model.Warning = entry != null ? entry.Warning : null;
            model.ActionId = ScenarioAuthoringActionIds.ActionTimelineEntryPrefix + (entry != null ? entry.Id : string.Empty);
            return model;
        }

        private static string BuildCategories(ScenarioTimelineDay day)
        {
            string value = string.Empty;
            for (int i = 0; day != null && day.Entries != null && i < day.Entries.Count; i++)
            {
                string token = day.Entries[i] != null ? day.Entries[i].Kind.ToString() : null;
                if (string.IsNullOrEmpty(token) || value.IndexOf(token) >= 0)
                    continue;
                value = value.Length == 0 ? token : value + ", " + token;
            }
            return value;
        }

        private static string FormatTime(ScenarioScheduleTime time)
        {
            if (time == null)
                return "--:--";
            return time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Timeline;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>
    /// Revision-keyed projection of the authored unified timeline. The shell asks for
    /// it every frame, but BuildEntries and marker allocation only run after a draft
    /// mutation/undo changes ScenarioEditorSession.DraftRevision.
    /// </summary>
    internal sealed class ScenarioDayTimelineRibbonViewModelBuilder
    {
        internal const int MaxVisibleMarkersPerDay = 4;
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private ScenarioDefinition _cachedDefinition;
        private int _cachedRevision = -1;
        private ScenarioDayTimelineRibbonViewModel _cachedViewModel;

        public ScenarioDayTimelineRibbonViewModelBuilder(ScenarioTimelineBuilder timelineBuilder)
        {
            _timelineBuilder = timelineBuilder;
        }

        public ScenarioDayTimelineRibbonViewModel Build(ScenarioEditorSession session)
        {
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            int revision = session != null ? session.DraftRevision : -1;
            if (_cachedViewModel != null
                && object.ReferenceEquals(_cachedDefinition, definition)
                && _cachedRevision == revision)
            {
                return _cachedViewModel;
            }

            _cachedDefinition = definition;
            _cachedRevision = revision;
            _cachedViewModel = BuildFresh(definition);
            return _cachedViewModel;
        }

        private ScenarioDayTimelineRibbonViewModel BuildFresh(ScenarioDefinition definition)
        {
            List<ScenarioTimelineEntry> entries = _timelineBuilder != null
                ? _timelineBuilder.BuildEntries(definition, null)
                : new List<ScenarioTimelineEntry>();
            entries.Sort(CompareEntries);

            List<ScenarioDayTimelineRibbonMarkerViewModel> markers = new List<ScenarioDayTimelineRibbonMarkerViewModel>();
            int lastDay = 7;
            int chapters = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry == null)
                    continue;

                int day = entry.When != null ? Math.Max(1, entry.When.Day) : 1;
                bool chapter = entry.Kind == ScenarioTimelineEntryKind.Story;
                string summary = BuildSummary(entry);
                markers.Add(new ScenarioDayTimelineRibbonMarkerViewModel
                {
                    Day = day,
                    Domain = ResolveDomain(entry),
                    Title = entry.Title ?? entry.Id ?? "Timeline entry",
                    Summary = summary,
                    IsChapter = chapter,
                    Action = BuildAction(entry, chapter, summary)
                });
                lastDay = Math.Max(lastDay, day);
                if (chapter)
                    chapters++;
            }

            ScenarioDayTimelineRibbonDayViewModel[] days = BuildDays(markers, lastDay);

            return new ScenarioDayTimelineRibbonViewModel
            {
                FirstDay = 1,
                LastDay = lastDay,
                EntryCount = markers.Count,
                ChapterCount = chapters,
                Zoom = 0f,
                ZoomState = "overview",
                FirstVisibleDay = 1f,
                LastVisibleDay = lastDay,
                EmptyMessage = markers.Count == 0 ? "No scheduled events yet - add some in Timeline or Story" : null,
                Days = days,
                Markers = markers.ToArray()
            };
        }

        private static ScenarioDayTimelineRibbonDayViewModel[] BuildDays(
            List<ScenarioDayTimelineRibbonMarkerViewModel> markers,
            int lastDay)
        {
            ScenarioDayTimelineRibbonDayViewModel[] days = new ScenarioDayTimelineRibbonDayViewModel[Math.Max(1, lastDay)];
            for (int day = 1; day <= days.Length; day++)
            {
                int markerCount = 0;
                int chapterCount = 0;
                for (int markerIndex = 0; markers != null && markerIndex < markers.Count; markerIndex++)
                {
                    ScenarioDayTimelineRibbonMarkerViewModel marker = markers[markerIndex];
                    if (marker == null || marker.Day != day)
                        continue;
                    markerCount++;
                    if (marker.IsChapter)
                        chapterCount++;
                }

                int overflow = Math.Max(0, markerCount - MaxVisibleMarkersPerDay);
                days[day - 1] = new ScenarioDayTimelineRibbonDayViewModel
                {
                    Day = day,
                    MarkerCount = markerCount,
                    ChapterCount = chapterCount,
                    Label = day.ToString(CultureInfo.InvariantCulture),
                    OverflowLabel = overflow > 0 ? "+" + overflow.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    HoverAction = BuildDayHoverAction(day, markerCount, chapterCount)
                };
            }
            return days;
        }

        private static ScenarioAuthoringInspectorAction BuildDayHoverAction(int day, int markerCount, int chapterCount)
        {
            string scheduled = markerCount == 1 ? "1 scheduled event" : markerCount.ToString(CultureInfo.InvariantCulture) + " scheduled events";
            string chapters = chapterCount == 1 ? "1 story chapter" : chapterCount.ToString(CultureInfo.InvariantCulture) + " story chapters";
            return new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionTimelineDayPrefix + day.ToString(CultureInfo.InvariantCulture),
                Label = "Day " + day.ToString(CultureInfo.InvariantCulture),
                Hint = scheduled + ". " + chapters + ".",
                Detail = markerCount > 0 ? "Timeline density" : "No authored events",
                IconText = "D" + day.ToString(CultureInfo.InvariantCulture),
                Enabled = true
            };
        }

        private static ScenarioAuthoringInspectorAction BuildAction(
            ScenarioTimelineEntry entry,
            bool chapter,
            string summary)
        {
            string actionId = ScenarioAuthoringActionIds.ActionTimelineEntryPrefix + (entry.Id ?? string.Empty);

            return new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = entry.Title ?? entry.Id ?? "Timeline entry",
                Hint = summary,
                Detail = entry.OwnerStage,
                IconText = chapter ? "CH" : ResolveGlyph(entry),
                Enabled = true,
                Emphasized = chapter
            };
        }

        private static string BuildSummary(ScenarioTimelineEntry entry)
        {
            int day = entry != null && entry.When != null ? Math.Max(1, entry.When.Day) : 1;
            int hour = entry != null && entry.When != null ? Math.Max(0, entry.When.Hour) : 0;
            int minute = entry != null && entry.When != null ? Math.Max(0, entry.When.Minute) : 0;
            string time = "Day " + day.ToString(CultureInfo.InvariantCulture)
                + " at " + hour.ToString("00", CultureInfo.InvariantCulture)
                + ":" + minute.ToString("00", CultureInfo.InvariantCulture);
            string owner = entry != null && !string.IsNullOrEmpty(entry.OwnerStage) ? " - " + entry.OwnerStage : string.Empty;
            string warning = entry != null && !string.IsNullOrEmpty(entry.Warning) ? " " + entry.Warning : string.Empty;
            return (entry != null ? entry.Title : "Timeline entry") + "\n" + time + owner + warning;
        }

        private static string ResolveDomain(ScenarioTimelineEntry entry)
        {
            if (entry == null)
                return "change";
            switch (entry.Kind)
            {
                case ScenarioTimelineEntryKind.Story: return "story";
                case ScenarioTimelineEntryKind.Weather: return "weather";
                case ScenarioTimelineEntryKind.Inventory: return "inventory";
                case ScenarioTimelineEntryKind.Survivor: return "arrival";
                case ScenarioTimelineEntryKind.Journal: return "journal";
                case ScenarioTimelineEntryKind.WorldEvent: return "world_event";
                case ScenarioTimelineEntryKind.Quest: return "story";
                default:
                    return string.Equals(entry.SourceKind, "trigger", StringComparison.OrdinalIgnoreCase) ? "trigger" : "change";
            }
        }

        private static string ResolveGlyph(ScenarioTimelineEntry entry)
        {
            string domain = ResolveDomain(entry);
            if (domain == "weather") return "WE";
            if (domain == "inventory") return "IV";
            if (domain == "arrival") return "AR";
            if (domain == "journal") return "JR";
            if (domain == "world_event") return "EV";
            if (domain == "trigger") return "TR";
            return "TM";
        }

        private static int CompareEntries(ScenarioTimelineEntry left, ScenarioTimelineEntry right)
        {
            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;
            int result = (left.When != null ? left.When.Day : 1).CompareTo(right.When != null ? right.When.Day : 1);
            if (result != 0) return result;
            result = (left.When != null ? left.When.Hour : 0).CompareTo(right.When != null ? right.When.Hour : 0);
            if (result != 0) return result;
            return (left.When != null ? left.When.Minute : 0).CompareTo(right.When != null ? right.When.Minute : 0);
        }
    }
}

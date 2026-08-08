using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class TimelineNavigationAutomationIds
    {
        public const string SelectDayPrefix = "scenario.timeline.day.";
        public const string OpenEntryPrefix = "scenario.timeline.entry.";
    }

    internal enum TimelineNavigationCommandKind
    {
        SelectDay,
        OpenEntry,
        FocusTrigger,
        FocusScheduledAction,
        FocusJournalEntry
    }

    internal sealed class TimelineNavigationCommand : ScenarioAuthoringCommand
    {
        private TimelineNavigationCommand(TimelineNavigationCommandKind kind, string value, int index, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            Value = value ?? string.Empty;
            Index = index;
        }

        public TimelineNavigationCommandKind Kind { get; private set; }
        public string Value { get; private set; }
        public int Index { get; private set; }

        public static TimelineNavigationCommand SelectDay(string dayId)
        {
            return new TimelineNavigationCommand(
                TimelineNavigationCommandKind.SelectDay,
                dayId,
                -1,
                TimelineNavigationAutomationIds.SelectDayPrefix + (dayId ?? string.Empty));
        }

        public static TimelineNavigationCommand OpenEntry(string entryId)
        {
            return new TimelineNavigationCommand(
                TimelineNavigationCommandKind.OpenEntry,
                entryId,
                -1,
                TimelineNavigationAutomationIds.OpenEntryPrefix + (entryId ?? string.Empty));
        }

        public static TimelineNavigationCommand FocusScheduledAction(int index)
        {
            return new TimelineNavigationCommand(
                TimelineNavigationCommandKind.FocusScheduledAction,
                null,
                index,
                "scenario.timeline.focus.scheduled_action." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static TimelineNavigationCommand FocusTrigger(int index)
        {
            return new TimelineNavigationCommand(
                TimelineNavigationCommandKind.FocusTrigger,
                null,
                index,
                "scenario.timeline.focus.trigger." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static TimelineNavigationCommand FocusJournalEntry(int index)
        {
            return new TimelineNavigationCommand(
                TimelineNavigationCommandKind.FocusJournalEntry,
                null,
                index,
                "scenario.timeline.focus.journal_entry." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

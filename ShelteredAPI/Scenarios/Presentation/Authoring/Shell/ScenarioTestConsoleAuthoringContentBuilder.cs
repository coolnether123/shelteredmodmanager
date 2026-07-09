using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Presentation.Authoring.Timeline;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Test-stage-only view model builder; rendering stays in the existing parchment inspector renderer.</summary>
    internal static class ScenarioTestConsoleAuthoringContentBuilder
    {
        public static ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioEditorSession session = context != null ? context.EditorSession : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioTestConsoleService console = Resolve();
            bool active = session != null && session.PlaytestState == ScenarioPlaytestState.Playtesting;
            if (console != null)
                console.SetConsoleVisible(active);

            return new[]
            {
                Section("test_console_live", "Test Console", BuildLiveItems(console, active), ScenarioAuthoringInspectorSectionLayout.Summary),
                Section("test_console_controls", "Safe time controls", BuildControlItems(active), ScenarioAuthoringInspectorSectionLayout.ActionStrip),
                Section("test_console_upcoming", "Next authored events", BuildUpcomingItems(definition, active), ScenarioAuthoringInspectorSectionLayout.PropertyList),
                Section("test_console_fire_now", "Fire now in playtest", BuildFireNowItems(definition, active), ScenarioAuthoringInspectorSectionLayout.ActionStrip),
                Section("test_console_log", "Execution log (newest first)", BuildLogItems(console, active), ScenarioAuthoringInspectorSectionLayout.PropertyList),
                Section("test_console_advanced", "Advanced diagnostics", BuildAdvancedItems(console, active), ScenarioAuthoringInspectorSectionLayout.NoteList)
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveItems(ScenarioTestConsoleService console, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (!active)
            {
                items.Add(Item.Text("Start a playtest to open the live scenario console."));
                return items;
            }
            ScenarioRuntimeState state = GetState();
            items.Add(Item.Property("Scenario time", "Day " + GameTime.Day + " " + GameTime.Hour.ToString("D2") + ":" + GameTime.Minute.ToString("D2")));
            items.Add(Item.Property("Active story stage", console != null && !string.IsNullOrEmpty(console.ActiveStoryStageId) ? console.ActiveStoryStageId : "Awaiting vanilla encounter state"));
            items.Add(Item.Property("Flags / milestones", FormatFlags(state)));
            items.Add(Item.Property("Quest states", "Vanilla quest state is retained by QuestManager; live inspection is shown in Advanced until its read seam is verified."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildControlItems(bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleHour, "+1 hour", "Advance through one bounded vanilla-clock hour; never changes Unity time scale.", active, true, "H+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleDay, "+1 day", "Advance through 24 bounded vanilla-clock hour steps.", active, false, "D+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleNextEvent, "Run until next authored event", "Advance no more than 72 hours to the next scheduled authored event.", active, true, "NX")));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildUpcomingItems(ScenarioDefinition definition, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioScheduledActionDefinition> actions = new List<ScenarioScheduledActionDefinition>();
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
                if (definition.ScheduledActions[i] != null) actions.Add(definition.ScheduledActions[i]);
            actions.Sort(delegate(ScenarioScheduledActionDefinition left, ScenarioScheduledActionDefinition right)
            {
                long l = ToMinutes(left != null ? left.DueTime : null);
                long r = ToMinutes(right != null ? right.DueTime : null);
                return l.CompareTo(r);
            });
            for (int i = 0; i < actions.Count && i < 5; i++)
                items.Add(Item.Property(Display(actions[i]), FormatWhen(actions[i].DueTime) + " / " + (actions[i].ActionType ?? "Scheduled action")));
            if (items.Count == 0)
                items.Add(Item.Text(active ? "No direct scheduled actions are authored. Trigger and conversation schedules still appear in the execution log when evaluated." : "Start a playtest to see upcoming runtime events."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildFireNowItems(ScenarioDefinition definition, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (trigger == null || string.IsNullOrEmpty(trigger.Id)) continue;
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleFirePrefix + ScenarioAuthoringActionCodec.EncodeToken(trigger.Id), "Fire trigger: " + trigger.Id, "Manually fires the selected scenario trigger and logs the authoring-only action.", active, false, "TR")));
            }
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null || string.IsNullOrEmpty(action.Id)) continue;
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleFirePrefix + ScenarioAuthoringActionCodec.EncodeToken(action.Id), "Fire now: " + Display(action), "Applies this safe scheduled action without changing Unity time scale.", active, false, "FX")));
            }
            for (int i = 0; definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null && i < definition.ScenarioFlow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                if (stage == null || string.IsNullOrEmpty(stage.Id)) continue;
                ScenarioAuthoringInspectorAction jump = Item.Action(ScenarioAuthoringActionIds.ActionTestConsoleStoryStagePrefix + ScenarioAuthoringActionCodec.EncodeToken(stage.Id), "Jump to story stage: " + stage.Id, "Direct jumping is disabled until the vanilla encounter stage seam is live-verified.", false, false, "ST");
                jump.DisabledReason = "Vanilla encounter progression has no verified safe jump seam.";
                items.Add(Item.ActionItem(jump));
            }
            if (items.Count == 0)
                items.Add(Item.Text("No authored trigger, scheduled action, world event, or story stage is available to invoke."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLogItems(ScenarioTestConsoleService console, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeExecutionLogEntry[] entries = console != null && console.ExecutionLog != null ? console.ExecutionLog.GetMostRecentFirst(16) : new ScenarioRuntimeExecutionLogEntry[0];
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null) items.Add(Item.Text(entries[i].ToPlainLanguage()));
            if (items.Count == 0)
                items.Add(Item.Text(active ? "No runtime activity has been recorded since the console opened." : "Execution logging is paused while playtest is not active."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildAdvancedItems(ScenarioTestConsoleService console, bool active)
        {
            ScenarioRuntimeState state = GetState();
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Execution log capacity", ScenarioRuntimeExecutionLog.Capacity.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Recorded entries", console != null && console.ExecutionLog != null ? console.ExecutionLog.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Runtime binding", Item.Safe(state != null ? state.RuntimeBindingId : null)));
            items.Add(Item.Property("Last scheduler pass", state != null ? "Day " + state.LastProcessedDay + " " + state.LastProcessedHour.ToString("D2") + ":" + state.LastProcessedMinute.ToString("D2") : "None"));
            items.Add(Item.Text("Time controls use at most 72 one-hour GameTime field increments and invoke vanilla new-day listeners at the normal 06:00 rollover. They never set a high Time.timeScale."));
            return items;
        }

        private static ScenarioAuthoringInspectorSection Section(string id, string title, List<ScenarioAuthoringInspectorItem> items, ScenarioAuthoringInspectorSectionLayout layout)
        {
            return new ScenarioAuthoringInspectorSection { Id = id, Title = title, Expanded = true, Layout = layout, Items = items.ToArray() };
        }

        private static ScenarioTestConsoleService Resolve()
        {
            try { return ScenarioCompositionRoot.Resolve<ScenarioTestConsoleService>(); }
            catch { return null; }
        }

        private static ScenarioRuntimeState GetState()
        {
            try { return ScenarioCompositionRoot.Resolve<ScenarioRuntimeStateService>().State; }
            catch { return null; }
        }

        private static string FormatFlags(ScenarioRuntimeState state)
        {
            if (state == null || state.Flags == null || state.Flags.Count == 0) return "None";
            List<string> values = new List<string>();
            for (int i = 0; i < state.Flags.Count && i < 6; i++) if (state.Flags[i] != null) values.Add(state.Flags[i].FlagId + "=" + state.Flags[i].Value);
            return string.Join(", ", values.ToArray());
        }

        private static string Display(ScenarioScheduledActionDefinition action) { return ScenarioTimelineCreatorText.ScheduledActionName(null, action); }
        private static string FormatWhen(ScenarioScheduleTime time) { return time == null ? "Unscheduled" : "Day " + time.Day + " " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2"); }
        private static long ToMinutes(ScenarioScheduleTime time) { return time == null ? long.MaxValue : (((long)Math.Max(1, time.Day) * 24L + Math.Max(0, time.Hour)) * 60L + Math.Max(0, time.Minute)); }
    }
}

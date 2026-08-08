using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Content;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    /// <summary>Test-stage-only view model builder; rendering stays in the existing parchment inspector renderer.</summary>
    internal static class ScenarioTestConsoleAuthoringContentBuilder
    {
        public static ScenarioAuthoringInspectorSection[] Build(
            ScenarioAuthoringWindowContentContext context,
            ScenarioTestConsoleService console,
            ScenarioPreviewSessionHost previewSession)
        {
            ScenarioEditorSession session = context != null ? context.EditorSession : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioRuntimeSnapshot runtimeState = GetState(previewSession);
            bool active = session != null && session.PlaytestState == ScenarioPlaytestState.Playtesting;
            if (console != null)
                console.SetConsoleVisible(active);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(Section("test_console_status", "Status", BuildLiveItems(console, active), ScenarioAuthoringInspectorSectionLayout.Summary, ScenarioAuthoringInspectorSectionRendererKind.TestStatus));
            // "Next authored events" remains the creator-language contract for this Upcoming instrument panel.
            sections.Add(Section("test_console_upcoming", "Upcoming", BuildUpcomingItems(definition, runtimeState, active), ScenarioAuthoringInspectorSectionLayout.PropertyList, ScenarioAuthoringInspectorSectionRendererKind.TestUpcoming));
            sections.Add(Section("test_console_log", "Execution log (newest first)", BuildLogItems(console, previewSession, active), ScenarioAuthoringInspectorSectionLayout.PropertyList, ScenarioAuthoringInspectorSectionRendererKind.TestLog));
            sections.Add(Section("test_console_controls", "Controls", BuildControlItems(definition, runtimeState, active), ScenarioAuthoringInspectorSectionLayout.ActionStrip, ScenarioAuthoringInspectorSectionRendererKind.TestControls));
            bool showAdvanced = context != null
                && context.State != null
                && context.State.Settings != null
                && context.State.Settings.ShowAdvancedDetails;
            List<ScenarioAuthoringInspectorItem> advancedItems = BuildAdvancedToggleItems(showAdvanced);
            if (showAdvanced)
                advancedItems.AddRange(BuildAdvancedItems(console, previewSession, active));
            ScenarioAuthoringInspectorSection advanced = Section(
                "test_console_advanced",
                "Advanced diagnostics",
                advancedItems,
                ScenarioAuthoringInspectorSectionLayout.NoteList,
                ScenarioAuthoringInspectorSectionRendererKind.Default);
            advanced.IsAdvanced = true;
            sections.Add(advanced);
            return sections.ToArray();
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveItems(ScenarioTestConsoleService console, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (!active)
            {
                items.Add(Item.Text("Start a playtest to open the live scenario console."));
                return items;
            }
            items.Add(Item.Property("Day", GameTime.Day.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Time", GameTime.Hour.ToString("D2") + ":" + GameTime.Minute.ToString("D2")));
            items.Add(Item.Property("Stage", FormatStage(console)));
            items.Add(Item.Property("State", "Playtest running"));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildControlItems(ScenarioDefinition definition, ScenarioRuntimeSnapshot runtimeState, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.ActionItem(Item.Action(ScenarioTestConsoleCommand.AdvanceOneHour(), "+1 hour", "Advance through one bounded vanilla-clock hour; never changes Unity time scale.", active, false, "H+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioTestConsoleCommand.AdvanceOneDay(), "+1 day", "Advance through 24 bounded vanilla-clock hour steps.", active, false, "D+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioTestConsoleCommand.RunUntilNextEvent(), "Run until next authored event", "Advance no more than 72 hours to the next scheduled authored event.", active, true, "NX")));
            items.AddRange(BuildFireNowItems(definition, runtimeState, active));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildUpcomingItems(ScenarioDefinition definition, ScenarioRuntimeSnapshot runtimeState, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioScheduledActionDefinition> actions = new List<ScenarioScheduledActionDefinition>();
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action != null && !IsOnceConsumed(runtimeState, action))
                    actions.Add(action);
            }
            actions.Sort(delegate(ScenarioScheduledActionDefinition left, ScenarioScheduledActionDefinition right)
            {
                long l = ToMinutes(left != null ? left.DueTime : null);
                long r = ToMinutes(right != null ? right.DueTime : null);
                return l.CompareTo(r);
            });
            for (int i = 0; i < actions.Count && i < 5; i++)
                items.Add(Item.Property(FormatWhen(actions[i].DueTime), Display(actions[i]), actions[i].ActionType ?? "Scheduled action"));
            if (items.Count == 0)
                items.Add(Item.Text(active ? "No pending direct scheduled actions remain. Trigger and conversation schedules still appear in the execution log when evaluated." : "Start a playtest to see upcoming runtime events."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildFireNowItems(ScenarioDefinition definition, ScenarioRuntimeSnapshot runtimeState, bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (trigger == null || string.IsNullOrEmpty(trigger.Id)) continue;
                items.Add(Item.ActionItem(Item.Action(ScenarioTestConsoleCommand.FireNow(trigger.Id), "Fire now: " + ResolvePrimaryName(null, trigger.Id, "Trigger"), "Manually fires the selected scenario trigger and logs the authoring-only action.", active, false, "TR")));
            }
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null || string.IsNullOrEmpty(action.Id)) continue;
                bool consumed = IsOnceConsumed(runtimeState, action);
                ScenarioAuthoringInspectorAction fire = Item.Action(
                    ScenarioTestConsoleCommand.FireNow(action.Id),
                    consumed ? "Fired: " + Display(action) : "Fire now: " + Display(action),
                    consumed ? "This once-only action already succeeded in the current run." : "Applies this safe scheduled action without changing Unity time scale.",
                    active && !consumed,
                    false,
                    consumed ? "OK" : "FX");
                if (consumed)
                    fire.DisabledReason = "Once-only action already consumed.";
                items.Add(Item.ActionItem(fire));
            }
            for (int i = 0; definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null && i < definition.ScenarioFlow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                if (stage == null || string.IsNullOrEmpty(stage.Id)) continue;
                ScenarioAuthoringInspectorAction jump = Item.Action(ScenarioTestConsoleCommand.JumpToStoryStage(stage.Id), "Fire stage: " + ResolvePrimaryName(null, stage.Id, "Story stage"), "Direct jumping is disabled until the vanilla encounter stage seam is live-verified.", false, false, "ST");
                jump.DisabledReason = "Vanilla encounter progression has no verified safe jump seam.";
                items.Add(Item.ActionItem(jump));
            }
            if (items.Count == 0)
                items.Add(Item.Text("No authored trigger, scheduled action, world event, or story stage is available to invoke."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLogItems(
            ScenarioTestConsoleService console,
            ScenarioPreviewSessionHost previewSession,
            bool active)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeExecutionEntrySnapshot[] entries = GetExecutionLog(previewSession, 16);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;
                ScenarioAuthoringInspectorItem item = Item.Text(entries[i].PlainLanguage);
                item.IconText = OutcomeIcon(entries[i].Outcome);
                item.Badge = OutcomeLabel(entries[i].Outcome);
                item.Emphasized = string.Equals(entries[i].Outcome, "FailedWithError", StringComparison.Ordinal)
                    || string.Equals(entries[i].Outcome, "SkippedConditionFalse", StringComparison.Ordinal);
                items.Add(item);
            }
            if (items.Count == 0)
                items.Add(Item.Text(active ? "No runtime activity has been recorded since the console opened." : "Execution logging is paused while playtest is not active."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildAdvancedItems(
            ScenarioTestConsoleService console,
            ScenarioPreviewSessionHost previewSession,
            bool active)
        {
            ScenarioRuntimeSnapshot state = GetState(previewSession);
            ScenarioRuntimeExecutionEntrySnapshot[] entries = GetExecutionLog(previewSession, 64);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Execution log view", "64 newest entries"));
            items.Add(Item.Property("Recorded entries", (entries != null ? entries.Length : 0).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Runtime binding", Item.Safe(state != null ? state.RuntimeBindingId : null)));
            items.Add(Item.Property("Active story stage ID", Item.Safe(console != null ? console.ActiveStoryStageId : null)));
            items.Add(Item.Property("Flags / milestones", FormatFlags(state)));
            items.Add(Item.Property("Quest states", "QuestManager retains vanilla quest state; a safe live read seam is not verified."));
            items.Add(Item.Property("Last scheduler pass", state != null ? "Day " + state.LastProcessedDay + " " + state.LastProcessedHour.ToString("D2") + ":" + state.LastProcessedMinute.ToString("D2") : "None"));
            items.Add(Item.Text("Time controls use at most 72 one-hour GameTime field increments and invoke vanilla new-day listeners at the normal 06:00 rollover. They never set a high Time.timeScale."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildAdvancedToggleItems(bool showAdvanced)
        {
            return new List<ScenarioAuthoringInspectorItem>
            {
                Item.ActionItem(Item.Action(
                    ShellUxCommand.SettingToggle("debug.show_advanced_details"),
                    showAdvanced ? "Hide advanced diagnostics" : "Advanced diagnostics",
                    "Show raw runtime IDs, counters, and scheduler details.",
                    true,
                    false,
                    showAdvanced ? "-" : "+"))
            };
        }

        private static string FormatStage(ScenarioTestConsoleService console)
        {
            string id = console != null ? console.ActiveStoryStageId : null;
            if (string.IsNullOrEmpty(id)) return "Waiting for encounter";
            return ResolvePrimaryName(null, id, "Story stage");
        }

        private static string OutcomeIcon(string outcome)
        {
            if (string.Equals(outcome, "FailedWithError", StringComparison.Ordinal)) return "!";
            if (string.Equals(outcome, "SkippedConditionFalse", StringComparison.Ordinal) || string.Equals(outcome, "OnceAlreadyConsumed", StringComparison.Ordinal)) return "-";
            if (string.Equals(outcome, "RetryPending", StringComparison.Ordinal)) return "~";
            if (string.Equals(outcome, "Scheduled", StringComparison.Ordinal)) return ">";
            return "+";
        }

        private static string OutcomeLabel(string outcome)
        {
            if (string.Equals(outcome, "FailedWithError", StringComparison.Ordinal)) return "FAILED";
            if (string.Equals(outcome, "SkippedConditionFalse", StringComparison.Ordinal)) return "SKIPPED";
            if (string.Equals(outcome, "OnceAlreadyConsumed", StringComparison.Ordinal)) return "CONSUMED";
            if (string.Equals(outcome, "RetryPending", StringComparison.Ordinal)) return "RETRYING";
            if (string.Equals(outcome, "Scheduled", StringComparison.Ordinal)) return "QUEUED";
            if (string.Equals(outcome, "ManuallyFired", StringComparison.Ordinal)) return "MANUAL";
            return "FIRED";
        }

        private static string ResolvePrimaryName(string literalText, string storageId, string fallbackText)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                literalText,
                null,
                storageId,
                fallbackText).Text;
        }

        private static bool IsOnceConsumed(ScenarioRuntimeSnapshot state, ScenarioScheduledActionDefinition action)
        {
            if (state == null || action == null || string.IsNullOrEmpty(action.Id)
                || (action.Policy != null && action.Policy.Repeatable)
                || state.Actions == null)
            {
                return false;
            }

            ScenarioRuntimeActionSnapshot[] actions = state.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioRuntimeActionSnapshot record = actions[i];
                if (record != null
                    && string.Equals(record.Status, "Succeeded", StringComparison.Ordinal)
                    && string.Equals(record.ActionKey, action.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static ScenarioAuthoringInspectorSection Section(
            string id,
            string title,
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioAuthoringInspectorSectionLayout layout,
            ScenarioAuthoringInspectorSectionRendererKind rendererKind)
        {
            return new ScenarioAuthoringInspectorSection { Id = id, Title = title, Expanded = true, Layout = layout, RendererKind = rendererKind, Items = items.ToArray() };
        }

        private static ScenarioRuntimeSnapshot GetState(ScenarioPreviewSessionHost previewSession)
        {
            try
            {
                return previewSession != null ? previewSession.CaptureRuntimeState() : null;
            }
            catch { return null; }
        }

        private static ScenarioRuntimeExecutionEntrySnapshot[] GetExecutionLog(
            ScenarioPreviewSessionHost previewSession,
            int maximum)
        {
            try
            {
                return previewSession != null
                    ? previewSession.CaptureExecutionLog(maximum)
                    : new ScenarioRuntimeExecutionEntrySnapshot[0];
            }
            catch
            {
                return new ScenarioRuntimeExecutionEntrySnapshot[0];
            }
        }

        private static string FormatFlags(ScenarioRuntimeSnapshot state)
        {
            if (state == null || state.Flags == null || state.Flags.Length == 0) return "None";
            List<string> values = new List<string>();
            for (int i = 0; i < state.Flags.Length && i < 6; i++) if (state.Flags[i] != null) values.Add(state.Flags[i].Id + "=" + state.Flags[i].Value);
            return string.Join(", ", values.ToArray());
        }

        private static string Display(ScenarioScheduledActionDefinition action)
        {
            string literal = ScenarioTimelineCreatorText.ScheduledActionName(null, action);
            return ResolvePrimaryName(literal, action != null ? action.Id : null, "Scheduled event");
        }
        private static string FormatWhen(ScenarioScheduleTime time) { return time == null ? "Unscheduled" : "Day " + time.Day + " " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2"); }
        private static long ToMinutes(ScenarioScheduleTime time) { return time == null ? long.MaxValue : (((long)Math.Max(1, time.Day) * 24L + Math.Max(0, time.Hour)) * 60L + Math.Max(0, time.Minute)); }
    }
}

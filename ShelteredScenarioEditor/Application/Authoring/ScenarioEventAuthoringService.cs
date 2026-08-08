using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredScenarioEditor.Application.Commands;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioEventAuthoringService
    {
        private readonly ScenarioTriggerAuthoringService _triggers;
        private readonly ScenarioGateAuthoringService _gates;
        private readonly ScenarioScheduledActionAuthoringService _scheduledActions;
        private readonly ScenarioJournalAuthoringService _journal;

        public ScenarioEventAuthoringService()
        {
            ScenarioEventTemplateFactory templates = new ScenarioEventTemplateFactory();
            _triggers = new ScenarioTriggerAuthoringService();
            _gates = new ScenarioGateAuthoringService(templates);
            _scheduledActions = new ScenarioScheduledActionAuthoringService(templates);
            _journal = new ScenarioJournalAuthoringService();
        }

        public bool Execute(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (command == null)
                return false;
            if (command.Operation == EventAuthoringOperation.ToggleVanillaWorldEventSuppression)
                return ToggleVanillaSuppression(session, command.Category, out message);
            if (command.Operation == EventAuthoringOperation.ApplyTimelinePreset)
                return ScenarioTimelinePresetService.TryCreate(session, command.Value, out message);
            if (_triggers.Handle(session, command, out message))
                return true;
            if (_gates.Handle(session, command, out message))
                return true;
            if (_scheduledActions.Handle(session, command, out message))
                return true;
            if (_journal.Handle(session, command, out message))
                return true;

            return false;
        }

        private static bool ToggleVanillaSuppression(ScenarioEditorSession session, string category, out string message)
        {
            message = null;
            ScenarioVanillaSuppressionDefinition suppression = EnsureVanillaSuppression(session.WorkingDefinition);
            if (string.Equals(category, "randomVisitors", StringComparison.OrdinalIgnoreCase))
                suppression.RandomVisitors = !suppression.RandomVisitors;
            else if (string.Equals(category, "binman", StringComparison.OrdinalIgnoreCase))
                suppression.Binman = !suppression.Binman;
            else if (string.Equals(category, "raids", StringComparison.OrdinalIgnoreCase))
                suppression.Raids = !suppression.Raids;
            else if (string.Equals(category, "stasisVisitors", StringComparison.OrdinalIgnoreCase))
                suppression.StasisVisitors = !suppression.StasisVisitors;
            else if (string.Equals(category, "radioBroadcastOdds", StringComparison.OrdinalIgnoreCase))
                suppression.RadioBroadcastOdds = !suppression.RadioBroadcastOdds;
            else
                return false;

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated vanilla world-event suppression.";
            return true;
        }

        private static ScenarioVanillaSuppressionDefinition EnsureVanillaSuppression(ScenarioDefinition definition)
        {
            if (definition.VanillaSuppression == null)
                definition.VanillaSuppression = new ScenarioVanillaSuppressionDefinition();
            return definition.VanillaSuppression;
        }
    }

    internal sealed class ScenarioTriggerAuthoringService
    {
        public bool Handle(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            if (command.Operation == EventAuthoringOperation.AddManualTrigger)
                return AddTrigger(session, "Manual", out message);
            if (command.Operation == EventAuthoringOperation.AddScheduledTrigger)
                return AddTrigger(session, "Scheduled", out message);

            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            int index = command.Index;
            if (index < 0 || index >= events.Triggers.Count)
                return false;
            if (command.Operation == EventAuthoringOperation.DeleteTrigger)
            {
                string id = events.Triggers[index] != null ? events.Triggers[index].Id : null;
                events.Triggers.RemoveAt(index);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed trigger '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleTriggerType)
            {
                TriggerDef trigger = events.Triggers[index];
                trigger.Type = NextTriggerType(trigger != null ? trigger.Type : null);
                ApplyDefaults(session.WorkingDefinition, trigger);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger type to " + trigger.Type + ".";
                return true;
            }

            int delta = command.Delta;
            if (command.Operation == EventAuthoringOperation.AdjustTriggerDay)
            {
                TriggerDef trigger = events.Triggers[index];
                int day = Math.Max(1, ScenarioPropertyBag.GetInt(trigger.Properties, "day", 1) + delta);
                ScenarioPropertyBag.Set(trigger.Properties, "day", day.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger day to " + day + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustTriggerHour)
            {
                TriggerDef trigger = events.Triggers[index];
                int hour = ScenarioAuthoringSchedule.Clamp(ScenarioPropertyBag.GetInt(trigger.Properties, "hour", 8) + delta, 0, 23);
                ScenarioPropertyBag.Set(trigger.Properties, "hour", hour.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger hour to " + hour + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustTriggerMinute)
            {
                TriggerDef trigger = events.Triggers[index];
                int minute = ScenarioAuthoringSchedule.Clamp(ScenarioPropertyBag.GetInt(trigger.Properties, "minute", 0) + delta, 0, 59);
                ScenarioPropertyBag.Set(trigger.Properties, "minute", minute.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger minute to " + minute + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.SetTriggerTarget)
            {
                TriggerDef trigger = events.Triggers[index];
                ApplyTriggerTarget(session.WorkingDefinition, trigger, command.Value ?? string.Empty);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger target.";
                return true;
            }

            return false;
        }

        private static bool AddTrigger(ScenarioEditorSession session, string type, out string message)
        {
            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            TriggerDef trigger = new TriggerDef();
            trigger.Id = ScenarioEventIdFactory.NextTriggerId(events);
            trigger.Type = type;
            ApplyDefaults(session.WorkingDefinition, trigger);
            events.Triggers.Add(trigger);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added " + type.ToLowerInvariant() + " trigger '" + trigger.Id + "'.";
            return true;
        }

        private static TriggersAndEventsDefinition EnsureEvents(ScenarioDefinition definition)
        {
            if (definition.TriggersAndEvents == null)
                definition.TriggersAndEvents = new TriggersAndEventsDefinition();
            return definition.TriggersAndEvents;
        }

        private static void ApplyDefaults(ScenarioDefinition definition, TriggerDef trigger)
        {
            if (trigger == null)
                return;

            string type = trigger.Type ?? "Manual";
            if (string.Equals(type, "Scheduled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "TimeReached", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioScheduleTime time = ScenarioAuthoringSchedule.NextTime();
                ScenarioPropertyBag.Set(trigger.Properties, "day", time.Day.ToString());
                ScenarioPropertyBag.Set(trigger.Properties, "hour", time.Hour.ToString());
                ScenarioPropertyBag.Set(trigger.Properties, "minute", time.Minute.ToString());
            }
            else if (string.Equals(type, "ScenarioFlagSet", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "flagId", ScenarioEventReferenceFinder.FirstFlagId(definition) ?? string.Empty);
                ScenarioPropertyBag.Set(trigger.Properties, "flagValue", "true");
            }
            else if (string.Equals(type, "QuestCompleted", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "questId", ScenarioEventReferenceFinder.FirstQuestId(definition) ?? string.Empty);
            }
            else if (string.Equals(type, "ItemQuantityAvailable", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "itemId", ScenarioEventReferenceFinder.FirstItemId() ?? string.Empty);
                ScenarioPropertyBag.Set(trigger.Properties, "quantity", "1");
            }
        }

        private static string NextTriggerType(string current)
        {
            if (string.Equals(current, "Manual", StringComparison.OrdinalIgnoreCase))
                return "Scheduled";
            if (string.Equals(current, "Scheduled", StringComparison.OrdinalIgnoreCase))
                return "ScenarioFlagSet";
            if (string.Equals(current, "ScenarioFlagSet", StringComparison.OrdinalIgnoreCase))
                return "QuestCompleted";
            if (string.Equals(current, "QuestCompleted", StringComparison.OrdinalIgnoreCase))
                return "ItemQuantityAvailable";
            return "Manual";
        }

        private static void ApplyTriggerTarget(ScenarioDefinition definition, TriggerDef trigger, string target)
        {
            if (trigger == null)
                return;
            string type = trigger.Type ?? string.Empty;
            if (string.Equals(type, "ScenarioFlagSet", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "flagId", target);
                ScenarioPropertyBag.Set(trigger.Properties, "flagValue", "true");
            }
            else if (string.Equals(type, "QuestCompleted", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "questId", target);
            }
            else if (string.Equals(type, "ItemQuantityAvailable", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "itemId", target);
                if (ScenarioPropertyBag.GetInt(trigger.Properties, "quantity", 0) <= 0)
                    ScenarioPropertyBag.Set(trigger.Properties, "quantity", "1");
            }
        }
    }

    internal sealed class ScenarioGateAuthoringService
    {
        private readonly ScenarioEventTemplateFactory _templates;

        public ScenarioGateAuthoringService(ScenarioEventTemplateFactory templates)
        {
            _templates = templates;
        }

        public bool Handle(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;

            if (command.Operation == EventAuthoringOperation.AddGate)
                return AddGate(session, out message);

            int index = command.Index;
            if (index < 0 || index >= definition.Gates.Count)
                return false;
            if (command.Operation == EventAuthoringOperation.DeleteGate)
            {
                string id = definition.Gates[index] != null ? definition.Gates[index].Id : null;
                definition.Gates.RemoveAt(index);
                ClearGateReferences(definition, id);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed gate '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.ToggleGateMode)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                group.Mode = group.Mode == ScenarioConditionGroupMode.All ? ScenarioConditionGroupMode.Any : ScenarioConditionGroupMode.All;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition mode to " + group.Mode + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AddGateCondition)
            {
                EnsureGroup(definition.Gates[index]).Conditions.Add(_templates.CreateCondition(definition, ScenarioConditionKind.ScenarioFlagSet));
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Added gate condition.";
                return true;
            }

            int conditionIndex = command.ChildIndex;
            if (command.Operation == EventAuthoringOperation.DeleteGateCondition)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                group.Conditions.RemoveAt(conditionIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed gate condition.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleGateConditionKind)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                ScenarioConditionKind kind = NextConditionKind(group.Conditions[conditionIndex] != null ? group.Conditions[conditionIndex].Kind : ScenarioConditionKind.TimeReached);
                group.Conditions[conditionIndex] = _templates.CreateCondition(definition, kind);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition kind to " + kind + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetGateConditionActor)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                ScenarioCastMemberReferenceCandidate candidate;
                if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, command.Value ?? string.Empty, out candidate))
                    return false;
                ApplyConditionActorTarget(group.Conditions[conditionIndex], candidate);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition cast member to " + candidate.DisplayName + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetGateConditionTarget)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                ApplyConditionTarget(group.Conditions[conditionIndex], command.Value ?? string.Empty);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition target.";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.AdjustGateConditionQuantity)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                ScenarioConditionRef condition = group.Conditions[conditionIndex];
                condition.Quantity = Math.Max(1, condition.Quantity + command.Delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition quantity to " + condition.Quantity + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetGateConditionFlagValue)
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                ScenarioConditionRef condition = group.Conditions[conditionIndex];
                condition.FlagValue = command.Value ?? string.Empty;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate flag value.";
                return true;
            }

            return false;
        }

        private bool AddGate(ScenarioEditorSession session, out string message)
        {
            ScenarioDefinition definition = session.WorkingDefinition;
            ScenarioGateDefinition gate = new ScenarioGateDefinition();
            gate.Id = ScenarioEventIdFactory.NextGateId(definition);
            gate.DisplayName = "Gate " + (definition.Gates.Count + 1).ToString();
            gate.Conditions.Mode = ScenarioConditionGroupMode.All;
            gate.Conditions.Conditions.Add(_templates.CreateCondition(definition, ScenarioConditionKind.ScenarioFlagSet));
            definition.Gates.Add(gate);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added scenario gate '" + gate.Id + "'.";
            return true;
        }

        private static ScenarioConditionGroup EnsureGroup(ScenarioGateDefinition gate)
        {
            if (gate.Conditions == null)
                gate.Conditions = new ScenarioConditionGroup();
            return gate.Conditions;
        }

        private static void ClearGateReferences(ScenarioDefinition definition, string gateId)
        {
            if (string.IsNullOrEmpty(gateId))
                return;
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action != null && string.Equals(action.GateId, gateId, StringComparison.OrdinalIgnoreCase))
                    action.GateId = null;
            }
            for (int i = 0; definition != null && definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                if (entry != null && string.Equals(entry.GateId, gateId, StringComparison.OrdinalIgnoreCase))
                    entry.GateId = null;
            }
        }

        private static ScenarioConditionKind NextConditionKind(ScenarioConditionKind current)
        {
            switch (current)
            {
                case ScenarioConditionKind.TimeReached: return ScenarioConditionKind.ScenarioFlagSet;
                case ScenarioConditionKind.ScenarioFlagSet: return ScenarioConditionKind.QuestCompleted;
                case ScenarioConditionKind.QuestCompleted: return ScenarioConditionKind.ItemQuantityAvailable;
                case ScenarioConditionKind.ItemQuantityAvailable: return ScenarioConditionKind.SurvivorPresent;
                case ScenarioConditionKind.SurvivorPresent: return ScenarioConditionKind.BunkerExpansionUnlocked;
                case ScenarioConditionKind.BunkerExpansionUnlocked: return ScenarioConditionKind.CustomTrigger;
                default: return ScenarioConditionKind.TimeReached;
            }
        }

        private static void ApplyConditionTarget(ScenarioConditionRef condition, string target)
        {
            if (condition == null)
                return;
            condition.ActorRef = null;
            condition.TargetId = target;
            if (condition.Kind == ScenarioConditionKind.ScenarioFlagSet)
            {
                condition.FlagId = target;
                if (string.IsNullOrEmpty(condition.FlagValue))
                    condition.FlagValue = "true";
            }
        }

        private static void ApplyConditionActorTarget(ScenarioConditionRef condition, ScenarioCastMemberReferenceCandidate candidate)
        {
            if (condition == null || candidate == null)
                return;

            condition.ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
            condition.TargetId = candidate.TargetId;
        }
    }

    internal sealed class ScenarioScheduledActionAuthoringService
    {
        private readonly ScenarioEventTemplateFactory _templates;

        public ScenarioScheduledActionAuthoringService(ScenarioEventTemplateFactory templates)
        {
            _templates = templates;
        }

        public bool Handle(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;

            if (command.Operation == EventAuthoringOperation.AddScheduledAction)
                return AddScheduledAction(session, out message);
            if (command.Operation == EventAuthoringOperation.AddWorldEvent)
                return AddWorldEventAction(session, out message);

            int index = command.Index;
            if (IsScheduledActionOperation(command.Operation) && (index < 0 || index >= definition.ScheduledActions.Count))
                return false;
            if (command.Operation == EventAuthoringOperation.DeleteScheduledAction)
            {
                string id = definition.ScheduledActions[index] != null ? definition.ScheduledActions[index].Id : null;
                definition.ScheduledActions.RemoveAt(index);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed scheduled action '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleScheduledActionType)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[index];
                ScenarioEffectKind kind = NextEffectKind(PrimaryEffectKind(action));
                action.ActionType = kind.ToString();
                action.Effects.Clear();
                action.Effects.Add(_templates.CreateEffect(definition, kind));
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled action type to " + action.ActionType + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleScheduledActionGate)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[index];
                action.GateId = NextGateReference(definition, action.GateId);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = string.IsNullOrEmpty(action.GateId) ? "Scheduled action gate cleared." : "Scheduled action now requires gate '" + action.GateId + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.ToggleScheduledActionRepeat)
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.Repeatable = !policy.Repeatable;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = policy.Repeatable ? "Scheduled action is repeatable." : "Scheduled action runs once.";
                return true;
            }

            int delta = command.Delta;
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionCooldown)
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.CooldownMinutes = Math.Max(0, policy.CooldownMinutes + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated cooldown to " + policy.CooldownMinutes + " minute(s).";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionWindowEndDay)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[index];
                ScenarioSchedulePolicy policy = EnsurePolicy(action);
                int minDay = action != null && action.DueTime != null ? action.DueTime.Day : 1;
                policy.WindowEndDay = policy.WindowEndDay <= 0
                    ? (delta > 0 ? minDay + delta : 0)
                    : policy.WindowEndDay + delta;
                if (policy.WindowEndDay > 0)
                    policy.WindowEndDay = Math.Max(minDay, policy.WindowEndDay);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = policy.WindowEndDay > 0 ? "Updated window end day to " + policy.WindowEndDay + "." : "Cleared schedule window.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionChance)
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                int percent = (int)Math.Round(policy.Chance * 100f) + delta;
                percent = ScenarioAuthoringSchedule.Clamp(percent, 0, 100);
                policy.Chance = percent / 100f;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated chance to " + percent + "%.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionJitter)
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.JitterMinutes = Math.Max(0, policy.JitterMinutes + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated jitter to " + policy.JitterMinutes + " minute(s).";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionMaxRuns)
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.MaxRuns = Math.Max(0, policy.MaxRuns + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = policy.MaxRuns > 0 ? "Updated max runs to " + policy.MaxRuns + "." : "Cleared max runs.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionDay)
            {
                ScenarioScheduleTime time = definition.ScheduledActions[index].DueTime;
                time.Day = Math.Max(1, time.Day + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled day to " + time.Day + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionHour)
            {
                ScenarioScheduleTime time = definition.ScheduledActions[index].DueTime;
                time.Hour = ScenarioAuthoringSchedule.Clamp(time.Hour + delta, 0, 23);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled hour to " + time.Hour + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustScheduledActionMinute)
            {
                ScenarioScheduleTime time = definition.ScheduledActions[index].DueTime;
                time.Minute = ScenarioAuthoringSchedule.Clamp(time.Minute + delta, 0, 59);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled minute to " + time.Minute + ".";
                return true;
            }

            if (HandleWorldEventAction(session, command, out message))
                return true;

            return HandleEffect(session, command, out message);
        }

        private static bool IsScheduledActionOperation(EventAuthoringOperation operation)
        {
            return operation >= EventAuthoringOperation.DeleteScheduledAction
                && operation <= EventAuthoringOperation.SetScheduledEffectFlagValue;
        }

        private bool AddScheduledAction(ScenarioEditorSession session, out string message)
        {
            ScenarioDefinition definition = session.WorkingDefinition;
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = ScenarioEventIdFactory.NextScheduledActionId(definition);
            action.ActionType = ScenarioEffectKind.SetScenarioFlag.ToString();
            action.DueTime = ScenarioAuthoringSchedule.NextTime();
            action.Effects.Add(_templates.CreateEffect(definition, ScenarioEffectKind.SetScenarioFlag));
            definition.ScheduledActions.Add(action);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added scheduled action '" + action.Id + "' for " + ScenarioAuthoringSchedule.Format(action.DueTime) + ".";
            return true;
        }

        private bool AddWorldEventAction(ScenarioEditorSession session, out string message)
        {
            ScenarioDefinition definition = session.WorkingDefinition;
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = ScenarioEventIdFactory.NextScheduledActionId(definition);
            action.ActionType = ScenarioEffectKind.WorldEvent.ToString();
            action.DueTime = ScenarioAuthoringSchedule.NextTime();
            action.Effects.Add(_templates.CreateEffect(definition, ScenarioEffectKind.WorldEvent));
            definition.ScheduledActions.Add(action);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added world event '" + action.Id + "' for " + ScenarioAuthoringSchedule.Format(action.DueTime) + ".";
            return true;
        }

        private bool HandleWorldEventAction(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;
            int actionIndex = command.Index;
            if (actionIndex < 0 || actionIndex >= definition.ScheduledActions.Count)
                return false;
            if (command.Operation == EventAuthoringOperation.SetWorldEventType)
                return SetWorldEventProperty(session, actionIndex, "eventType", command.Value ?? string.Empty, out message);
            if (command.Operation == EventAuthoringOperation.SetWorldEventNpcType)
                return SetWorldEventProperty(session, actionIndex, "npcType", command.Value ?? string.Empty, out message);
            if (command.Operation == EventAuthoringOperation.SetWorldEventOutcome)
                return SetWorldEventProperty(session, actionIndex, "outcome", command.Value ?? string.Empty, out message);

            if (command.Operation == EventAuthoringOperation.AddWorldEventTradeItem)
                return AddWorldEventItemSpec(session, actionIndex, "tradeItems", out message);
            if (command.Operation == EventAuthoringOperation.AddWorldEventWeapon)
                return AddWorldEventItemSpec(session, actionIndex, "weapons", out message);
            if (command.Operation == EventAuthoringOperation.AddWorldEventArmor)
                return AddWorldEventItemSpec(session, actionIndex, "armor", out message);

            int itemIndex = command.ChildIndex;
            if (command.Operation == EventAuthoringOperation.DeleteWorldEventTradeItem)
                return DeleteWorldEventItemSpec(session, actionIndex, "tradeItems", itemIndex, out message);
            if (command.Operation == EventAuthoringOperation.DeleteWorldEventWeapon)
                return DeleteWorldEventItemSpec(session, actionIndex, "weapons", itemIndex, out message);
            if (command.Operation == EventAuthoringOperation.DeleteWorldEventArmor)
                return DeleteWorldEventItemSpec(session, actionIndex, "armor", itemIndex, out message);

            if (command.Operation == EventAuthoringOperation.SetWorldEventTradeItem)
                return SetWorldEventItemSpecItem(session, actionIndex, "tradeItems", itemIndex, command.Value, out message);
            if (command.Operation == EventAuthoringOperation.SetWorldEventWeapon)
                return SetWorldEventItemSpecItem(session, actionIndex, "weapons", itemIndex, command.Value, out message);
            if (command.Operation == EventAuthoringOperation.SetWorldEventArmor)
                return SetWorldEventItemSpecItem(session, actionIndex, "armor", itemIndex, command.Value, out message);

            if (command.Operation == EventAuthoringOperation.AdjustWorldEventTradeQuantity)
                return StepWorldEventItemSpecQuantity(session, actionIndex, "tradeItems", itemIndex, command.Delta, out message);
            if (command.Operation == EventAuthoringOperation.AdjustWorldEventWeaponQuantity)
                return StepWorldEventItemSpecQuantity(session, actionIndex, "weapons", itemIndex, command.Delta, out message);
            if (command.Operation == EventAuthoringOperation.AdjustWorldEventArmorQuantity)
                return StepWorldEventItemSpecQuantity(session, actionIndex, "armor", itemIndex, command.Delta, out message);

            if (command.Operation == EventAuthoringOperation.AdjustWorldEventRaidMinimum)
                return StepWorldEventIntProperty(session, actionIndex, "minNpcs", command.Delta, 1, out message);
            if (command.Operation == EventAuthoringOperation.AdjustWorldEventRaidMaximum)
                return StepWorldEventIntProperty(session, actionIndex, "maxNpcs", command.Delta, 1, out message);

            return false;
        }

        private static bool SetWorldEventProperty(ScenarioEditorSession session, int actionIndex, string key, string value, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            ScenarioPropertyBag.Set(effect.Properties, key, value);
            if (string.Equals(key, "eventType", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(ScenarioPropertyBag.GetString(effect.Properties, "npcType", null)))
                        ScenarioPropertyBag.Set(effect.Properties, "npcType", "Trader");
                }
                else if (string.Equals(value, "Broadcast", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(ScenarioPropertyBag.GetString(effect.Properties, "outcome", null)))
                {
                    ScenarioPropertyBag.Set(effect.Properties, "outcome", "None");
                }
                else if (string.Equals(value, "Raid", StringComparison.OrdinalIgnoreCase))
                {
                    int count = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "count", effect.Quantity > 0 ? effect.Quantity : 1));
                    ScenarioPropertyBag.Set(effect.Properties, "minNpcs", Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", count)).ToString());
                    ScenarioPropertyBag.Set(effect.Properties, "maxNpcs", Math.Max(count, ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", count)).ToString());
                }
            }
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated world event " + key + ".";
            return true;
        }

        private static bool StepWorldEventIntProperty(ScenarioEditorSession session, int actionIndex, string key, int delta, int minimum, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            int value = Math.Max(minimum, ScenarioPropertyBag.GetInt(effect.Properties, key, minimum) + delta);
            if (string.Equals(key, "maxNpcs", StringComparison.OrdinalIgnoreCase))
                value = Math.Max(value, ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", minimum));
            if (string.Equals(key, "minNpcs", StringComparison.OrdinalIgnoreCase))
            {
                int max = ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", value);
                if (max < value)
                    ScenarioPropertyBag.Set(effect.Properties, "maxNpcs", value.ToString());
            }
            ScenarioPropertyBag.Set(effect.Properties, key, value.ToString());
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated " + key + " to " + value + ".";
            return true;
        }

        private static bool AddWorldEventItemSpec(ScenarioEditorSession session, int actionIndex, string key, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            List<ItemSpecEntry> entries = ParseItemSpec(ScenarioPropertyBag.GetString(effect.Properties, key, null));
            entries.Add(new ItemSpecEntry { ItemId = ScenarioInventoryItemCatalog.DefaultItemId(), Quantity = 1 });
            ScenarioPropertyBag.Set(effect.Properties, key, FormatItemSpec(entries));
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added world event item row.";
            return true;
        }

        private static bool DeleteWorldEventItemSpec(ScenarioEditorSession session, int actionIndex, string key, int itemIndex, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            List<ItemSpecEntry> entries = ParseItemSpec(ScenarioPropertyBag.GetString(effect.Properties, key, null));
            if (itemIndex < 0 || itemIndex >= entries.Count)
            {
                message = "World event item row is missing.";
                return false;
            }
            entries.RemoveAt(itemIndex);
            ScenarioPropertyBag.Set(effect.Properties, key, FormatItemSpec(entries));
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Removed world event item row.";
            return true;
        }

        private static bool SetWorldEventItemSpecItem(ScenarioEditorSession session, int actionIndex, string key, int itemIndex, string itemId, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            List<ItemSpecEntry> entries = ParseItemSpec(ScenarioPropertyBag.GetString(effect.Properties, key, null));
            if (itemIndex < 0 || itemIndex >= entries.Count)
            {
                message = "World event item row is missing.";
                return false;
            }
            entries[itemIndex].ItemId = string.IsNullOrEmpty(itemId) ? ScenarioInventoryItemCatalog.DefaultItemId() : itemId;
            entries[itemIndex].Quantity = Math.Max(1, entries[itemIndex].Quantity);
            ScenarioPropertyBag.Set(effect.Properties, key, FormatItemSpec(entries));
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated world event item to '" + entries[itemIndex].ItemId + "'.";
            return true;
        }

        private static bool StepWorldEventItemSpecQuantity(ScenarioEditorSession session, int actionIndex, string key, int itemIndex, int delta, out string message)
        {
            ScenarioEffectDefinition effect;
            if (!TryGetWorldEventEffect(session.WorkingDefinition, actionIndex, out effect))
            {
                message = "World event target is missing.";
                return false;
            }

            List<ItemSpecEntry> entries = ParseItemSpec(ScenarioPropertyBag.GetString(effect.Properties, key, null));
            if (itemIndex < 0 || itemIndex >= entries.Count)
            {
                message = "World event item row is missing.";
                return false;
            }
            entries[itemIndex].Quantity = Math.Max(1, entries[itemIndex].Quantity + delta);
            ScenarioPropertyBag.Set(effect.Properties, key, FormatItemSpec(entries));
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated world event item quantity to " + entries[itemIndex].Quantity + ".";
            return true;
        }

        private static bool TryGetWorldEventEffect(ScenarioDefinition definition, int actionIndex, out ScenarioEffectDefinition effect)
        {
            effect = null;
            if (definition == null || definition.ScheduledActions == null || actionIndex < 0 || actionIndex >= definition.ScheduledActions.Count)
                return false;
            ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                if (action.Effects[i] != null && action.Effects[i].Kind == ScenarioEffectKind.WorldEvent)
                {
                    effect = action.Effects[i];
                    return true;
                }
            }
            return false;
        }

        private static List<ItemSpecEntry> ParseItemSpec(string spec)
        {
            List<ItemSpecEntry> entries = new List<ItemSpecEntry>();
            if (string.IsNullOrEmpty(spec))
                return entries;

            string[] parts = spec.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] pair = parts[i].Split(':');
                string itemId = pair.Length > 0 ? pair[0].Trim() : string.Empty;
                int quantity = 1;
                if (pair.Length > 1)
                    int.TryParse(pair[1], out quantity);
                if (!string.IsNullOrEmpty(itemId))
                    entries.Add(new ItemSpecEntry { ItemId = itemId, Quantity = Math.Max(1, quantity) });
            }
            return entries;
        }

        private static string FormatItemSpec(List<ItemSpecEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;
            List<string> parts = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                ItemSpecEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId))
                    continue;
                parts.Add(entry.ItemId + ":" + Math.Max(1, entry.Quantity).ToString());
            }
            return string.Join(",", parts.ToArray());
        }

        private sealed class ItemSpecEntry
        {
            public string ItemId;
            public int Quantity;
        }

        private bool HandleEffect(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;
            int actionIndex = command.Index;
            if (actionIndex < 0 || actionIndex >= definition.ScheduledActions.Count)
                return false;
            if (command.Operation == EventAuthoringOperation.AddScheduledEffect)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                action.Effects.Add(_templates.CreateEffect(definition, PrimaryEffectKind(action)));
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Added scheduled action effect.";
                return true;
            }

            int effectIndex = command.ChildIndex;
            if (command.Operation == EventAuthoringOperation.DeleteScheduledEffect)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                action.Effects.RemoveAt(effectIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed scheduled action effect.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleScheduledEffectKind)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                ScenarioEffectKind kind = NextEffectKind(action.Effects[effectIndex] != null ? action.Effects[effectIndex].Kind : ScenarioEffectKind.SetScenarioFlag);
                action.Effects[effectIndex] = _templates.CreateEffect(definition, kind);
                if (effectIndex == 0)
                    action.ActionType = kind.ToString();
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated effect kind to " + kind + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetScheduledEffectActor)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                ScenarioCastMemberReferenceCandidate candidate;
                if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, false, true, command.Value ?? string.Empty, out candidate))
                    return false;
                ApplyEffectActorTarget(action.Effects[effectIndex], candidate);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled effect cast member to " + candidate.DisplayName + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetScheduledEffectTarget)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                ApplyEffectTarget(action.Effects[effectIndex], command.Value ?? string.Empty);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled effect target.";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.AdjustScheduledEffectQuantity)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                ScenarioEffectDefinition effect = action.Effects[effectIndex];
                effect.Quantity = Math.Max(1, effect.Quantity + command.Delta);
                if (effect.Kind == ScenarioEffectKind.WorldEvent)
                    ScenarioPropertyBag.Set(effect.Properties, "count", effect.Quantity.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled effect quantity to " + effect.Quantity + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.AdjustScheduledEffectWeatherDuration)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                action.Effects[effectIndex].DurationHours = Math.Max(0, action.Effects[effectIndex].DurationHours + command.Delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated weather duration to " + action.Effects[effectIndex].DurationHours + " hour(s).";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetScheduledEffectFlagValue)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                action.Effects[effectIndex].FlagValue = command.Value ?? string.Empty;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled flag value.";
                return true;
            }

            return false;
        }

        private static void ApplyEffectTarget(ScenarioEffectDefinition effect, string target)
        {
            if (effect == null)
                return;
            effect.ActorRef = null;
            effect.TargetId = target;
            switch (effect.Kind)
            {
                case ScenarioEffectKind.AddInventory:
                case ScenarioEffectKind.RemoveInventory:
                    effect.ItemId = target;
                    break;
                case ScenarioEffectKind.SetWeather:
                case ScenarioEffectKind.RestoreWeather:
                    effect.WeatherState = target;
                    break;
                case ScenarioEffectKind.StartQuest:
                    effect.QuestId = target;
                    break;
                case ScenarioEffectKind.ActivateObject:
                case ScenarioEffectKind.DeactivateObject:
                    effect.ObjectId = target;
                    break;
                case ScenarioEffectKind.SpawnFutureSurvivor:
                    effect.SurvivorId = target;
                    break;
                case ScenarioEffectKind.UnlockBunkerExpansion:
                    effect.BunkerExpansionId = target;
                    break;
                case ScenarioEffectKind.SetScenarioFlag:
                    effect.FlagId = target;
                    if (string.IsNullOrEmpty(effect.FlagValue))
                        effect.FlagValue = "true";
                    break;
                case ScenarioEffectKind.FireTrigger:
                    effect.TriggerId = target;
                    break;
                case ScenarioEffectKind.StartConversation:
                    effect.ConversationId = target;
                    break;
                case ScenarioEffectKind.WriteJournalEntry:
                    ScenarioPropertyBag.Set(effect.Properties, "text", target);
                    break;
                case ScenarioEffectKind.WorldEvent:
                    ApplyWorldEventTarget(effect, target);
                    break;
            }
        }

        private static void ApplyWorldEventTarget(ScenarioEffectDefinition effect, string target)
        {
            if (effect == null || string.IsNullOrEmpty(target))
                return;
            int separator = target.IndexOf(':');
            if (separator <= 0)
                return;
            string key = target.Substring(0, separator);
            string value = target.Substring(separator + 1);
            ScenarioPropertyBag.Set(effect.Properties, key, value);
            if (string.Equals(key, "eventType", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, "NpcVisit", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(ScenarioPropertyBag.GetString(effect.Properties, "npcType", null)))
            {
                ScenarioPropertyBag.Set(effect.Properties, "npcType", "Trader");
            }
        }

        private static void ApplyEffectActorTarget(ScenarioEffectDefinition effect, ScenarioCastMemberReferenceCandidate candidate)
        {
            if (effect == null || candidate == null)
                return;

            effect.ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
            effect.TargetId = candidate.TargetId;
            if (effect.Kind == ScenarioEffectKind.SpawnFutureSurvivor)
                effect.SurvivorId = candidate.TargetId;
        }

        private static ScenarioSchedulePolicy EnsurePolicy(ScenarioScheduledActionDefinition action)
        {
            if (action.Policy == null)
                action.Policy = new ScenarioSchedulePolicy();
            return action.Policy;
        }

        private static string NextGateReference(ScenarioDefinition definition, string current)
        {
            if (definition == null || definition.Gates == null || definition.Gates.Count == 0)
                return null;
            if (string.IsNullOrEmpty(current))
                return definition.Gates[0] != null ? definition.Gates[0].Id : null;
            for (int i = 0; i < definition.Gates.Count; i++)
            {
                if (definition.Gates[i] != null && string.Equals(definition.Gates[i].Id, current, StringComparison.OrdinalIgnoreCase))
                {
                    int next = i + 1;
                    return next < definition.Gates.Count && definition.Gates[next] != null ? definition.Gates[next].Id : null;
                }
            }
            return null;
        }

        private static ScenarioEffectKind PrimaryEffectKind(ScenarioScheduledActionDefinition action)
        {
            if (action != null && action.Effects != null && action.Effects.Count > 0 && action.Effects[0] != null)
                return action.Effects[0].Kind;
            return ScenarioEffectKind.SetScenarioFlag;
        }

        private static ScenarioEffectKind NextEffectKind(ScenarioEffectKind current)
        {
            switch (current)
            {
                case ScenarioEffectKind.SetScenarioFlag: return ScenarioEffectKind.FireTrigger;
                case ScenarioEffectKind.FireTrigger: return ScenarioEffectKind.AddInventory;
                case ScenarioEffectKind.AddInventory: return ScenarioEffectKind.RemoveInventory;
                case ScenarioEffectKind.RemoveInventory: return ScenarioEffectKind.SetWeather;
                case ScenarioEffectKind.SetWeather: return ScenarioEffectKind.StartQuest;
                case ScenarioEffectKind.StartQuest: return ScenarioEffectKind.SpawnFutureSurvivor;
                case ScenarioEffectKind.SpawnFutureSurvivor: return ScenarioEffectKind.ActivateObject;
                case ScenarioEffectKind.ActivateObject: return ScenarioEffectKind.DeactivateObject;
                case ScenarioEffectKind.DeactivateObject: return ScenarioEffectKind.UnlockBunkerExpansion;
                case ScenarioEffectKind.UnlockBunkerExpansion: return ScenarioEffectKind.RestoreWeather;
                case ScenarioEffectKind.RestoreWeather: return ScenarioEffectKind.WriteJournalEntry;
                case ScenarioEffectKind.WriteJournalEntry: return ScenarioEffectKind.StartConversation;
                case ScenarioEffectKind.StartConversation: return ScenarioEffectKind.WorldEvent;
                default: return ScenarioEffectKind.SetScenarioFlag;
            }
        }
    }

    internal sealed class ScenarioJournalAuthoringService
    {
        public bool Handle(ScenarioEditorSession session, EventAuthoringCommand command, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;
            JournalDefinition journal = EnsureJournal(definition);

            if (command.Operation == EventAuthoringOperation.AddJournalEntry)
                return AddEntry(session, out message);
            if (command.Operation == EventAuthoringOperation.ToggleFirstVanillaJournalEntry)
            {
                journal.VanillaPolicy.SuppressFirstEntry = !journal.VanillaPolicy.SuppressFirstEntry;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = journal.VanillaPolicy.SuppressFirstEntry ? "Vanilla first journal entry suppressed." : "Vanilla first journal entry allowed.";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.ToggleVanillaJournalCategory)
            {
                ScenarioJournalVanillaCategory category;
                if (!TryParseCategory(command.Category, out category))
                    return false;
                ToggleCategory(journal.VanillaPolicy, category);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated vanilla journal category suppression for " + category + ".";
                return true;
            }

            int index = command.Index;
            if (index < 0 || index >= journal.Entries.Count)
                return false;
            if (command.Operation == EventAuthoringOperation.DeleteJournalEntry)
            {
                string id = journal.Entries[index] != null ? journal.Entries[index].Id : null;
                journal.Entries.RemoveAt(index);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed journal entry '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.CycleJournalEntryGate)
            {
                JournalEntryDefinition entry = journal.Entries[index];
                entry.GateId = NextGateReference(definition, entry != null ? entry.GateId : null);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = string.IsNullOrEmpty(entry.GateId) ? "Journal entry condition gate cleared." : "Journal entry now requires gate '" + entry.GateId + "'.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.ToggleJournalEntryRepeat)
            {
                JournalEntryDefinition entry = journal.Entries[index];
                entry.Mode = entry.Mode == ScenarioJournalEntryMode.Repeat ? ScenarioJournalEntryMode.Once : ScenarioJournalEntryMode.Repeat;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = entry.Mode == ScenarioJournalEntryMode.Repeat ? "Journal entry is repeatable." : "Journal entry runs once.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.ClearJournalEntryWriter)
            {
                journal.Entries[index].Writer = null;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Journal writer set to any present member.";
                return true;
            }

            int delta = command.Delta;
            if (command.Operation == EventAuthoringOperation.AdjustJournalEntryDay)
            {
                ScenarioScheduleTime time = EnsureDueTime(journal.Entries[index]);
                time.Day = Math.Max(1, time.Day + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal day to " + time.Day + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustJournalEntryHour)
            {
                ScenarioScheduleTime time = EnsureDueTime(journal.Entries[index]);
                time.Hour = ScenarioAuthoringSchedule.Clamp(time.Hour + delta, 0, 23);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal hour to " + time.Hour + ".";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.AdjustJournalEntryMinute)
            {
                ScenarioScheduleTime time = EnsureDueTime(journal.Entries[index]);
                time.Minute = ScenarioAuthoringSchedule.Clamp(time.Minute + delta, 0, 59);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal minute to " + time.Minute + ".";
                return true;
            }

            if (command.Operation == EventAuthoringOperation.SetJournalEntryId)
            {
                journal.Entries[index].Id = command.Value ?? string.Empty;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal entry id.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.SetJournalEntryText)
            {
                journal.Entries[index].Text = command.Value ?? string.Empty;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal entry text.";
                return true;
            }
            if (command.Operation == EventAuthoringOperation.SetJournalEntryWriter)
            {
                ScenarioCastMemberReferenceCandidate candidate;
                if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, command.Value ?? string.Empty, out candidate))
                    return false;
                journal.Entries[index].Writer = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated journal writer to " + candidate.DisplayName + ".";
                return true;
            }

            return false;
        }

        private static bool AddEntry(ScenarioEditorSession session, out string message)
        {
            JournalDefinition journal = EnsureJournal(session.WorkingDefinition);
            JournalEntryDefinition entry = new JournalEntryDefinition();
            entry.Id = NextJournalEntryId(journal);
            entry.Text = "We should write this down. {writer} will remember day {day}.";
            entry.DueTime = ScenarioAuthoringSchedule.NextTime();
            entry.Mode = ScenarioJournalEntryMode.Once;
            journal.Entries.Add(entry);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added journal entry '" + entry.Id + "' for " + ScenarioAuthoringSchedule.Format(entry.DueTime) + ".";
            return true;
        }

        private static JournalDefinition EnsureJournal(ScenarioDefinition definition)
        {
            if (definition.Journal == null)
                definition.Journal = new JournalDefinition();
            if (definition.Journal.VanillaPolicy == null)
                definition.Journal.VanillaPolicy = new JournalVanillaPolicyDefinition();
            return definition.Journal;
        }

        private static ScenarioScheduleTime EnsureDueTime(JournalEntryDefinition entry)
        {
            if (entry.DueTime == null)
                entry.DueTime = ScenarioAuthoringSchedule.NextTime();
            return entry.DueTime;
        }

        private static string NextJournalEntryId(JournalDefinition journal)
        {
            int index = journal != null && journal.Entries != null ? journal.Entries.Count + 1 : 1;
            string id;
            do
            {
                id = "journal_" + index.ToString();
                index++;
            }
            while (HasJournalEntry(journal, id));
            return id;
        }

        private static bool HasJournalEntry(JournalDefinition journal, string id)
        {
            for (int i = 0; journal != null && journal.Entries != null && i < journal.Entries.Count; i++)
                if (journal.Entries[i] != null && string.Equals(journal.Entries[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string NextGateReference(ScenarioDefinition definition, string current)
        {
            if (definition == null || definition.Gates == null || definition.Gates.Count == 0)
                return null;
            if (string.IsNullOrEmpty(current))
                return definition.Gates[0] != null ? definition.Gates[0].Id : null;
            for (int i = 0; i < definition.Gates.Count; i++)
            {
                if (definition.Gates[i] != null && string.Equals(definition.Gates[i].Id, current, StringComparison.OrdinalIgnoreCase))
                {
                    int next = i + 1;
                    return next < definition.Gates.Count && definition.Gates[next] != null ? definition.Gates[next].Id : null;
                }
            }
            return null;
        }

        private static void ToggleCategory(JournalVanillaPolicyDefinition policy, ScenarioJournalVanillaCategory category)
        {
            if (policy == null)
                return;
            for (int i = 0; policy.SuppressedCategories != null && i < policy.SuppressedCategories.Count; i++)
            {
                if (policy.SuppressedCategories[i] == category)
                {
                    policy.SuppressedCategories.RemoveAt(i);
                    return;
                }
            }
            policy.SuppressedCategories.Add(category);
        }

        private static bool TryParseCategory(string token, out ScenarioJournalVanillaCategory category)
        {
            category = ScenarioJournalVanillaCategory.Death;
            if (string.IsNullOrEmpty(token))
                return false;
            try
            {
                category = (ScenarioJournalVanillaCategory)Enum.Parse(typeof(ScenarioJournalVanillaCategory), token, true);
                return Enum.IsDefined(typeof(ScenarioJournalVanillaCategory), category);
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class ScenarioEventTemplateFactory
    {
        public ScenarioConditionRef CreateCondition(ScenarioDefinition definition, ScenarioConditionKind kind)
        {
            ScenarioConditionRef condition = new ScenarioConditionRef();
            condition.Id = "condition_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            condition.Kind = kind;
            switch (kind)
            {
                case ScenarioConditionKind.TimeReached:
                    condition.Time = ScenarioAuthoringSchedule.NextTime();
                    break;
                case ScenarioConditionKind.ItemQuantityAvailable:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstItemId();
                    condition.Quantity = 1;
                    break;
                case ScenarioConditionKind.QuestActive:
                case ScenarioConditionKind.QuestCompleted:
                case ScenarioConditionKind.QuestFailed:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstQuestId(definition);
                    break;
                case ScenarioConditionKind.SurvivorPresent:
                case ScenarioConditionKind.SurvivorStatCheck:
                case ScenarioConditionKind.SurvivorTraitCheck:
                    ScenarioCastMemberReferenceCandidate survivor = ScenarioCastMemberReferenceCatalog.FindFirst(definition, true, true);
                    condition.ActorRef = survivor != null ? ScenarioCastMemberReferenceCatalog.CopyActorRef(survivor.ActorRef) : null;
                    condition.TargetId = survivor != null ? survivor.TargetId : ScenarioEventReferenceFinder.FirstSurvivorName(definition);
                    condition.StatId = "Strength";
                    condition.StatValue = 5;
                    condition.TraitId = "Strength:Optimistic";
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstExpansionId(definition);
                    break;
                case ScenarioConditionKind.TechnologyUnlocked:
                    condition.TargetId = null;
                    break;
                case ScenarioConditionKind.CustomTrigger:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstTriggerId(definition);
                    break;
                case ScenarioConditionKind.ScenarioFlagSet:
                    condition.FlagId = ScenarioEventReferenceFinder.FirstFlagId(definition);
                    condition.TargetId = condition.FlagId;
                    condition.FlagValue = "true";
                    break;
            }
            return condition;
        }

        public ScenarioEffectDefinition CreateEffect(ScenarioDefinition definition, ScenarioEffectKind kind)
        {
            ScenarioEffectDefinition effect = new ScenarioEffectDefinition();
            effect.Id = "effect_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            effect.Kind = kind;
            effect.Quantity = 1;
            effect.DurationHours = 1;
            switch (kind)
            {
                case ScenarioEffectKind.UnlockBunkerExpansion:
                    effect.BunkerExpansionId = ScenarioEventReferenceFinder.FirstExpansionId(definition);
                    effect.TargetId = effect.BunkerExpansionId;
                    break;
                case ScenarioEffectKind.ActivateObject:
                case ScenarioEffectKind.DeactivateObject:
                    effect.ObjectId = ScenarioEventReferenceFinder.FirstObjectId(definition);
                    effect.TargetId = effect.ObjectId;
                    break;
                case ScenarioEffectKind.AddInventory:
                case ScenarioEffectKind.RemoveInventory:
                    effect.ItemId = ScenarioEventReferenceFinder.FirstItemId();
                    break;
                case ScenarioEffectKind.SpawnFutureSurvivor:
                    ScenarioCastMemberReferenceCandidate survivor = ScenarioCastMemberReferenceCatalog.FindFirst(definition, false, true);
                    effect.ActorRef = survivor != null ? ScenarioCastMemberReferenceCatalog.CopyActorRef(survivor.ActorRef) : null;
                    effect.SurvivorId = survivor != null ? survivor.TargetId : ScenarioEventReferenceFinder.FirstFutureSurvivorId(definition);
                    effect.TargetId = effect.SurvivorId;
                    break;
                case ScenarioEffectKind.StartQuest:
                    effect.QuestId = ScenarioEventReferenceFinder.FirstQuestId(definition);
                    effect.TargetId = effect.QuestId;
                    break;
                case ScenarioEffectKind.SetWeather:
                    effect.WeatherState = "Rain";
                    break;
                case ScenarioEffectKind.RestoreWeather:
                    effect.WeatherState = "None";
                    break;
                case ScenarioEffectKind.SetScenarioFlag:
                    effect.FlagId = ScenarioEventReferenceFinder.FirstFlagId(definition);
                    effect.TargetId = effect.FlagId;
                    effect.FlagValue = "true";
                    break;
                case ScenarioEffectKind.FireTrigger:
                    effect.TriggerId = ScenarioEventReferenceFinder.FirstTriggerId(definition);
                    effect.TargetId = effect.TriggerId;
                    break;
                case ScenarioEffectKind.WriteJournalEntry:
                    ScenarioPropertyBag.Set(effect.Properties, "text", "Authored journal entry for day {day}.");
                    ScenarioPropertyBag.Set(effect.Properties, "format", "WriterPrefix");
                    ScenarioPropertyBag.Set(effect.Properties, "writerMode", "AnyPresent");
                    break;
                case ScenarioEffectKind.StartConversation:
                    effect.ConversationId = FirstConversationId(definition);
                    effect.TargetId = effect.ConversationId;
                    break;
                case ScenarioEffectKind.WorldEvent:
                    ScenarioPropertyBag.Set(effect.Properties, "eventType", "NpcVisit");
                    ScenarioPropertyBag.Set(effect.Properties, "npcType", "Trader");
                    ScenarioPropertyBag.Set(effect.Properties, "count", "1");
                    effect.Quantity = 1;
                    break;
            }
            return effect;
        }

        private static string FirstConversationId(ScenarioDefinition definition)
        {
            if (definition != null
                && definition.Conversations != null
                && definition.Conversations.Conversations != null
                && definition.Conversations.Conversations.Count > 0
                && definition.Conversations.Conversations[0] != null)
            {
                return definition.Conversations.Conversations[0].Id;
            }
            return "conversation_1";
        }
    }

    internal static class ScenarioEventIdFactory
    {
        public static string NextTriggerId(TriggersAndEventsDefinition events)
        {
            int index = events != null && events.Triggers != null ? events.Triggers.Count + 1 : 1;
            string id;
            do
            {
                id = "trigger_" + index.ToString();
                index++;
            }
            while (ShelteredScenarioAuthoring.IndexDefinition(events).HasTrigger(id));
            return id;
        }

        public static string NextGateId(ScenarioDefinition definition)
        {
            int index = definition != null && definition.Gates != null ? definition.Gates.Count + 1 : 1;
            string id;
            do
            {
                id = "gate_" + index.ToString();
                index++;
            }
            while (ShelteredScenarioAuthoring.IndexDefinition(definition).HasGate(id));
            return id;
        }

        public static string NextScheduledActionId(ScenarioDefinition definition)
        {
            int index = definition != null && definition.ScheduledActions != null ? definition.ScheduledActions.Count + 1 : 1;
            string id;
            do
            {
                id = "action_" + index.ToString();
                index++;
            }
            while (HasScheduledAction(definition, id));
            return id;
        }

        private static bool HasScheduledAction(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
                if (definition.ScheduledActions[i] != null && string.Equals(definition.ScheduledActions[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    internal static class ScenarioEventReferenceFinder
    {
        public static string FirstTriggerId(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
                if (definition.TriggersAndEvents.Triggers[i] != null && !string.IsNullOrEmpty(definition.TriggersAndEvents.Triggers[i].Id))
                    return definition.TriggersAndEvents.Triggers[i].Id;
            return null;
        }

        public static string FirstQuestId(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
                if (definition.Quests.Quests[i] != null && !string.IsNullOrEmpty(definition.Quests.Quests[i].Id))
                    return definition.Quests.Quests[i].Id;
            return null;
        }

        public static string FirstExpansionId(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
                if (definition.BunkerGrid.Expansions[i] != null && !string.IsNullOrEmpty(definition.BunkerGrid.Expansions[i].Id))
                    return definition.BunkerGrid.Expansions[i].Id;
            return null;
        }

        public static string FirstObjectId(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
                if (definition.BunkerEdits.ObjectPlacements[i] != null && !string.IsNullOrEmpty(definition.BunkerEdits.ObjectPlacements[i].ScenarioObjectId))
                    return definition.BunkerEdits.ObjectPlacements[i].ScenarioObjectId;
            return null;
        }

        public static string FirstFutureSurvivorId(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
                if (definition.FamilySetup.FutureSurvivors[i] != null && !string.IsNullOrEmpty(definition.FamilySetup.FutureSurvivors[i].Id))
                    return definition.FamilySetup.FutureSurvivors[i].Id;
            return null;
        }

        public static string FirstSurvivorName(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
                if (definition.FamilySetup.Members[i] != null && !string.IsNullOrEmpty(definition.FamilySetup.Members[i].Name))
                    return definition.FamilySetup.Members[i].Name;
            return null;
        }

        public static string FirstFlagId(ScenarioDefinition definition)
        {
            string id = FirstFlagIdFromTriggers(definition);
            if (!string.IsNullOrEmpty(id))
                return id;
            id = FirstFlagIdFromGates(definition);
            if (!string.IsNullOrEmpty(id))
                return id;
            return FirstFlagIdFromActions(definition);
        }

        public static string FirstItemId()
        {
            return ScenarioInventoryItemCatalog.DefaultItemId();
        }

        private static string FirstFlagIdFromTriggers(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                string id = ScenarioPropertyBag.GetString(trigger != null ? trigger.Properties : null, "flagId", null);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            return null;
        }

        private static string FirstFlagIdFromGates(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                string id = FirstFlagId(definition.Gates[i] != null ? definition.Gates[i].Conditions : null);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            return null;
        }

        private static string FirstFlagId(ScenarioConditionGroup group)
        {
            for (int i = 0; group != null && group.Conditions != null && i < group.Conditions.Count; i++)
            {
                ScenarioConditionRef condition = group.Conditions[i];
                string id = condition != null ? condition.FlagId ?? condition.TargetId : null;
                if (condition != null && condition.Kind == ScenarioConditionKind.ScenarioFlagSet && !string.IsNullOrEmpty(id))
                    return id;
            }
            for (int i = 0; group != null && group.Groups != null && i < group.Groups.Count; i++)
            {
                string id = FirstFlagId(group.Groups[i]);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            return null;
        }

        private static string FirstFlagIdFromActions(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                {
                    ScenarioEffectDefinition effect = action.Effects[e];
                    string id = effect != null ? effect.FlagId ?? effect.TargetId : null;
                    if (effect != null && effect.Kind == ScenarioEffectKind.SetScenarioFlag && !string.IsNullOrEmpty(id))
                        return id;
                }
            }
            return null;
        }
    }

}

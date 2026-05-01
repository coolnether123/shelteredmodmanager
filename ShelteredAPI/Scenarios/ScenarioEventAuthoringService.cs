using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioEventAuthoringService
    {
        private readonly ScenarioTriggerAuthoringService _triggers;
        private readonly ScenarioGateAuthoringService _gates;
        private readonly ScenarioScheduledActionAuthoringService _scheduledActions;

        public ScenarioEventAuthoringService()
        {
            ScenarioEventTemplateFactory templates = new ScenarioEventTemplateFactory();
            _triggers = new ScenarioTriggerAuthoringService();
            _gates = new ScenarioGateAuthoringService(templates);
            _scheduledActions = new ScenarioScheduledActionAuthoringService(templates);
        }

        public bool CanHandle(string actionId)
        {
            return !string.IsNullOrEmpty(actionId)
                && (actionId.StartsWith("scenario.trigger.", StringComparison.Ordinal)
                    || actionId.StartsWith("scenario.gate.", StringComparison.Ordinal)
                    || actionId.StartsWith("scenario.action.", StringComparison.Ordinal));
        }

        public bool TryHandleAction(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (_triggers.TryHandle(session, actionId, out message))
                return true;
            if (_gates.TryHandle(session, actionId, out message))
                return true;
            if (_scheduledActions.TryHandle(session, actionId, out message))
                return true;

            return false;
        }
    }

    internal sealed class ScenarioTriggerAuthoringService
    {
        public bool TryHandle(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTriggerAddManual, StringComparison.Ordinal))
                return AddTrigger(session, "Manual", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTriggerAddScheduled, StringComparison.Ordinal))
                return AddTrigger(session, "Scheduled", out message);

            TriggersAndEventsDefinition events = EnsureEvents(session.WorkingDefinition);
            int index;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionTriggerDeletePrefix, events.Triggers.Count, out index))
            {
                string id = events.Triggers[index] != null ? events.Triggers[index].Id : null;
                events.Triggers.RemoveAt(index);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed trigger '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionTriggerTypePrefix, events.Triggers.Count, out index))
            {
                TriggerDef trigger = events.Triggers[index];
                trigger.Type = NextTriggerType(trigger != null ? trigger.Type : null);
                ApplyDefaults(trigger);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger type to " + trigger.Type + ".";
                return true;
            }

            int delta;
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionTriggerDayPrefix, events.Triggers.Count, out index, out delta))
            {
                TriggerDef trigger = events.Triggers[index];
                int day = Math.Max(1, ScenarioPropertyBag.GetInt(trigger.Properties, "day", 1) + delta);
                ScenarioPropertyBag.Set(trigger.Properties, "day", day.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger day to " + day + ".";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionTriggerHourPrefix, events.Triggers.Count, out index, out delta))
            {
                TriggerDef trigger = events.Triggers[index];
                int hour = ScenarioAuthoringSchedule.Clamp(ScenarioPropertyBag.GetInt(trigger.Properties, "hour", 8) + delta, 0, 23);
                ScenarioPropertyBag.Set(trigger.Properties, "hour", hour.ToString());
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated trigger hour to " + hour + ".";
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
            ApplyDefaults(trigger);
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

        private static void ApplyDefaults(TriggerDef trigger)
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
                ScenarioPropertyBag.Set(trigger.Properties, "flagId", "flag_1");
                ScenarioPropertyBag.Set(trigger.Properties, "flagValue", "true");
            }
            else if (string.Equals(type, "QuestCompleted", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "questId", "quest_1");
            }
            else if (string.Equals(type, "ItemQuantityAvailable", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioPropertyBag.Set(trigger.Properties, "itemId", "Food");
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
    }

    internal sealed class ScenarioGateAuthoringService
    {
        private readonly ScenarioEventTemplateFactory _templates;

        public ScenarioGateAuthoringService(ScenarioEventTemplateFactory templates)
        {
            _templates = templates;
        }

        public bool TryHandle(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionGateAdd, StringComparison.Ordinal))
                return AddGate(session, out message);

            int index;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionGateDeletePrefix, definition.Gates.Count, out index))
            {
                string id = definition.Gates[index] != null ? definition.Gates[index].Id : null;
                definition.Gates.RemoveAt(index);
                ClearGateReferences(definition, id);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed gate '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionGateModePrefix, definition.Gates.Count, out index))
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                group.Mode = group.Mode == ScenarioConditionGroupMode.All ? ScenarioConditionGroupMode.Any : ScenarioConditionGroupMode.All;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated gate condition mode to " + group.Mode + ".";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionGateConditionAddPrefix, definition.Gates.Count, out index))
            {
                EnsureGroup(definition.Gates[index]).Conditions.Add(_templates.CreateCondition(definition, ScenarioConditionKind.ScenarioFlagSet));
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Added gate condition.";
                return true;
            }

            int conditionIndex;
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioAuthoringActionIds.ActionGateConditionDeletePrefix, definition.Gates.Count, out index, out conditionIndex))
            {
                ScenarioConditionGroup group = EnsureGroup(definition.Gates[index]);
                if (conditionIndex >= group.Conditions.Count)
                    return false;
                group.Conditions.RemoveAt(conditionIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed gate condition.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioAuthoringActionIds.ActionGateConditionKindPrefix, definition.Gates.Count, out index, out conditionIndex))
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
    }

    internal sealed class ScenarioScheduledActionAuthoringService
    {
        private readonly ScenarioEventTemplateFactory _templates;

        public ScenarioScheduledActionAuthoringService(ScenarioEventTemplateFactory templates)
        {
            _templates = templates;
        }

        public bool TryHandle(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionScheduledActionAdd, StringComparison.Ordinal))
                return AddScheduledAction(session, out message);

            int index;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix, definition.ScheduledActions.Count, out index))
            {
                string id = definition.ScheduledActions[index] != null ? definition.ScheduledActions[index].Id : null;
                definition.ScheduledActions.RemoveAt(index);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed scheduled action '" + (id ?? ("#" + index.ToString())) + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionTypePrefix, definition.ScheduledActions.Count, out index))
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
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionGatePrefix, definition.ScheduledActions.Count, out index))
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[index];
                action.GateId = NextGateReference(definition, action.GateId);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = string.IsNullOrEmpty(action.GateId) ? "Scheduled action gate cleared." : "Scheduled action now requires gate '" + action.GateId + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionRepeatPrefix, definition.ScheduledActions.Count, out index))
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.Repeatable = !policy.Repeatable;
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = policy.Repeatable ? "Scheduled action is repeatable." : "Scheduled action runs once.";
                return true;
            }

            int delta;
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix, definition.ScheduledActions.Count, out index, out delta))
            {
                ScenarioSchedulePolicy policy = EnsurePolicy(definition.ScheduledActions[index]);
                policy.CooldownMinutes = Math.Max(0, policy.CooldownMinutes + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated cooldown to " + policy.CooldownMinutes + " minute(s).";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionDayPrefix, definition.ScheduledActions.Count, out index, out delta))
            {
                ScenarioScheduleTime time = definition.ScheduledActions[index].DueTime;
                time.Day = Math.Max(1, time.Day + delta);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled day to " + time.Day + ".";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionHourPrefix, definition.ScheduledActions.Count, out index, out delta))
            {
                ScenarioScheduleTime time = definition.ScheduledActions[index].DueTime;
                time.Hour = ScenarioAuthoringSchedule.Clamp(time.Hour + delta, 0, 23);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Updated scheduled hour to " + time.Hour + ".";
                return true;
            }

            return TryHandleEffect(session, actionId, out message);
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

        private bool TryHandleEffect(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            ScenarioDefinition definition = session.WorkingDefinition;
            int actionIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionEffectAddPrefix, definition.ScheduledActions.Count, out actionIndex))
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                action.Effects.Add(_templates.CreateEffect(definition, PrimaryEffectKind(action)));
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Added scheduled action effect.";
                return true;
            }

            int effectIndex;
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionEffectDeletePrefix, definition.ScheduledActions.Count, out actionIndex, out effectIndex))
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[actionIndex];
                if (effectIndex >= action.Effects.Count)
                    return false;
                action.Effects.RemoveAt(effectIndex);
                ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                message = "Removed scheduled action effect.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioAuthoringActionIds.ActionScheduledActionEffectKindPrefix, definition.ScheduledActions.Count, out actionIndex, out effectIndex))
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

            return false;
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
                default: return ScenarioEffectKind.SetScenarioFlag;
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
                    condition.TargetId = "Food";
                    condition.Quantity = 1;
                    break;
                case ScenarioConditionKind.QuestActive:
                case ScenarioConditionKind.QuestCompleted:
                case ScenarioConditionKind.QuestFailed:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstQuestId(definition) ?? "quest_1";
                    break;
                case ScenarioConditionKind.SurvivorPresent:
                case ScenarioConditionKind.SurvivorStatCheck:
                case ScenarioConditionKind.SurvivorTraitCheck:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstSurvivorName(definition) ?? "Survivor";
                    condition.StatId = "Strength";
                    condition.StatValue = 5;
                    condition.TraitId = "Strength:Optimistic";
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstExpansionId(definition) ?? "expansion_1";
                    break;
                case ScenarioConditionKind.TechnologyUnlocked:
                    condition.TargetId = "technology_1";
                    break;
                case ScenarioConditionKind.CustomTrigger:
                    condition.TargetId = ScenarioEventReferenceFinder.FirstTriggerId(definition) ?? "trigger_1";
                    break;
                case ScenarioConditionKind.ScenarioFlagSet:
                    condition.FlagId = "flag_1";
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
                    effect.BunkerExpansionId = ScenarioEventReferenceFinder.FirstExpansionId(definition) ?? "expansion_1";
                    effect.TargetId = effect.BunkerExpansionId;
                    break;
                case ScenarioEffectKind.ActivateObject:
                case ScenarioEffectKind.DeactivateObject:
                    effect.ObjectId = ScenarioEventReferenceFinder.FirstObjectId(definition) ?? "object_1";
                    effect.TargetId = effect.ObjectId;
                    break;
                case ScenarioEffectKind.AddInventory:
                case ScenarioEffectKind.RemoveInventory:
                    effect.ItemId = "Food";
                    break;
                case ScenarioEffectKind.SpawnFutureSurvivor:
                    effect.SurvivorId = ScenarioEventReferenceFinder.FirstFutureSurvivorId(definition) ?? "future_survivor_1";
                    effect.TargetId = effect.SurvivorId;
                    break;
                case ScenarioEffectKind.StartQuest:
                    effect.QuestId = ScenarioEventReferenceFinder.FirstQuestId(definition) ?? "quest_1";
                    effect.TargetId = effect.QuestId;
                    break;
                case ScenarioEffectKind.SetWeather:
                    effect.WeatherState = "Rain";
                    break;
                case ScenarioEffectKind.RestoreWeather:
                    effect.WeatherState = "None";
                    break;
                case ScenarioEffectKind.SetScenarioFlag:
                    effect.FlagId = "flag_1";
                    effect.TargetId = effect.FlagId;
                    effect.FlagValue = "true";
                    break;
                case ScenarioEffectKind.FireTrigger:
                    effect.TriggerId = ScenarioEventReferenceFinder.FirstTriggerId(definition) ?? "trigger_1";
                    effect.TargetId = effect.TriggerId;
                    break;
            }
            return effect;
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
            while (ScenarioDefinitionLookup.HasTrigger(events, id));
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
            while (ScenarioDefinitionLookup.HasGate(definition, id));
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
            return definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && definition.TriggersAndEvents.Triggers.Count > 0 && definition.TriggersAndEvents.Triggers[0] != null
                ? definition.TriggersAndEvents.Triggers[0].Id
                : null;
        }

        public static string FirstQuestId(ScenarioDefinition definition)
        {
            return definition != null && definition.Quests != null && definition.Quests.Quests != null && definition.Quests.Quests.Count > 0 && definition.Quests.Quests[0] != null
                ? definition.Quests.Quests[0].Id
                : null;
        }

        public static string FirstExpansionId(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && definition.BunkerGrid.Expansions.Count > 0 && definition.BunkerGrid.Expansions[0] != null
                ? definition.BunkerGrid.Expansions[0].Id
                : null;
        }

        public static string FirstObjectId(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && definition.BunkerEdits.ObjectPlacements.Count > 0 && definition.BunkerEdits.ObjectPlacements[0] != null
                ? definition.BunkerEdits.ObjectPlacements[0].ScenarioObjectId
                : null;
        }

        public static string FirstFutureSurvivorId(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && definition.FamilySetup.FutureSurvivors.Count > 0 && definition.FamilySetup.FutureSurvivors[0] != null
                ? definition.FamilySetup.FutureSurvivors[0].Id
                : null;
        }

        public static string FirstSurvivorName(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && definition.FamilySetup.Members.Count > 0 && definition.FamilySetup.Members[0] != null
                ? definition.FamilySetup.Members[0].Name
                : null;
        }
    }

}

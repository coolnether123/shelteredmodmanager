using System;
using System.Collections.Generic;
using ShelteredAPI.Content;

using ModAPI.Scenarios;

using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal sealed class SchedulingValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            HashSet<string> actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ValidateAuthoredSchedules(definition, summary, actionIds);
            ValidateJournal(definition, summary, actionIds);
            ValidateSharedSchedules(definition, summary, actionIds);
        }

        private static void ValidateAuthoredSchedules(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> actionIds)
        {
            ScenarioDefinitionIndex index = new ScenarioDefinitionIndex(definition);
            for (int i = 0; definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                string id = TrimToNull(survivor != null ? survivor.Id : null);
                if (id == null)
                    summary.AddError("people.future_survivor.id_required", "[People] Future survivor #" + i + " is missing id.");
                else
                    AddActionId(summary, actionIds, "authored:survivor:" + id, "[People] Duplicate future survivor schedule id: " + id);
                ValidateTime(summary, survivor != null ? survivor.Arrival : null, "[People] Future survivor '" + (id ?? ("#" + i)) + "'");
                if (survivor == null || survivor.Survivor == null || TrimToNull(survivor.Survivor.Name) == null)
                    summary.AddError("people.future_survivor.name_required", "[People] Future survivor '" + (id ?? ("#" + i)) + "' needs a usable name.");
            }

            for (int i = 0; definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = definition.StartingInventory.ScheduledChanges[i];
                string id = TrimToNull(change != null ? change.Id : null);
                if (id == null)
                    summary.AddError("inventory.schedule.id_required", "[Inventory / Storage] Timed inventory change #" + i + " is missing id.");
                else
                    AddActionId(summary, actionIds, "authored:inventory:" + id, "[Inventory / Storage] Duplicate timed inventory schedule id: " + id);
                ValidateTime(summary, change != null ? change.When : null, "[Inventory / Storage] Timed inventory '" + (id ?? ("#" + i)) + "'");
                ValidateItem(summary, change != null ? change.ItemId : null, "[Inventory / Storage] Timed inventory '" + (id ?? ("#" + i)) + "'");
                if (change == null || change.Quantity <= 0)
                    summary.AddError("inventory.schedule.quantity", "[Inventory / Storage] Timed inventory '" + (id ?? ("#" + i)) + "' quantity must be greater than zero.");
            }

            ValidateTriggers(index, definition, summary, actionIds);

            for (int i = 0; definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null && i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
            {
                WeatherEventDefinition weather = definition.TriggersAndEvents.WeatherEvents[i];
                string id = TrimToNull(weather != null ? weather.Id : null);
                if (id == null)
                    summary.AddError("events.weather.id_required", "[Events] Weather event #" + i + " is missing id.");
                else
                    AddActionId(summary, actionIds, "authored:weather:" + id, "[Events] Duplicate weather event id: " + id);
                ValidateTime(summary, weather != null ? weather.When : null, "[Events] Weather event '" + (id ?? ("#" + i)) + "'");
                if (!IsValidWeather(weather != null ? weather.WeatherState : null))
                    summary.AddError("events.weather.invalid_state", "[Events] Weather event '" + (id ?? ("#" + i)) + "' has invalid weather state.");
                if (weather != null && weather.DurationHours < 0)
                    summary.AddError("events.weather.duration", "[Events] Weather event '" + (id ?? ("#" + i)) + "' durationHours cannot be negative.");
            }

            for (int i = 0; definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                string id = TrimToNull(quest != null ? quest.Id : null);
                bool hasTrigger = TrimToNull(quest != null ? quest.StartTriggerId : null) != null;
                bool hasSchedule = quest != null && quest.ScheduledStart != null;
                if (id == null)
                    summary.AddError("quests.id_required", "[Quests] Quest #" + i + " is missing id.");
                if (hasTrigger && hasSchedule)
                    summary.AddError("quests.start_ambiguous", "[Quests] Quest '" + (id ?? ("#" + i)) + "' has both trigger start and scheduled start.");
                if (hasSchedule)
                    ValidateTime(summary, quest.ScheduledStart, "[Quests] Scheduled quest '" + (id ?? ("#" + i)) + "'");
            }
        }

        private static void ValidateTriggers(ScenarioDefinitionIndex index, ScenarioDefinition definition, ValidationSummary summary, HashSet<string> actionIds)
        {
            HashSet<string> triggerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                string id = TrimToNull(trigger != null ? trigger.Id : null);
                if (id == null)
                {
                    summary.AddError("events.trigger.id_required", "[Events] Trigger #" + i + " is missing id.");
                    continue;
                }

                if (triggerIds.Contains(id))
                    summary.AddError("events.trigger.id_duplicate", "[Events] Duplicate trigger id: " + id);
                else
                    triggerIds.Add(id);

                ScenarioScheduledActionDefinition action;
                string reason;
                if (ScenarioTriggerDefinitionCompiler.TryCreateAction(trigger, i, out action, out reason))
                {
                    AddActionId(summary, actionIds, "trigger:" + id, "[Events] Duplicate trigger schedule id: " + id);
                    ValidateTime(summary, action.DueTime, "[Events] Trigger '" + id + "'");
                    for (int c = 0; action.ConditionRefs != null && c < action.ConditionRefs.Count; c++)
                        ValidateTriggerCondition(definition, index, summary, action.ConditionRefs[c], "[Events] Trigger '" + id + "'");
                    continue;
                }

                if (!ScenarioTriggerDefinitionCompiler.IsManual(trigger) && !string.IsNullOrEmpty(reason))
                    summary.AddError("events.trigger.invalid_target", "[Events] " + reason);
            }
        }

        private static void ValidateTriggerCondition(ScenarioDefinition definition, ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioConditionRef condition, string scope)
        {
            if (condition == null)
                return;

            string target = TrimToNull(condition.TargetId);
            switch (condition.Kind)
            {
                case ScenarioConditionKind.QuestActive:
                case ScenarioConditionKind.QuestCompleted:
                case ScenarioConditionKind.QuestFailed:
                    if (target == null || !index.HasQuest(target))
                        summary.AddError("events.trigger.unknown_quest", scope + " references unknown quest '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.SurvivorPresent:
                    if (condition.ActorRef != null)
                    {
                        if (!ScenarioActorReferenceIndex.Contains(definition, condition.ActorRef, true, true))
                            summary.AddError("events.trigger.deleted_actor", scope + " references deleted cast member actor '" + ScenarioActorReferenceIndex.Format(condition.ActorRef) + "'. Fix: open Events > Conditions and pick an existing cast member, or clear the actor link.");
                    }
                    else if (target == null || (!index.HasFamilySurvivor(target) && !index.HasFutureSurvivor(target)))
                        summary.AddError("events.trigger.unknown_survivor", scope + " references unknown survivor '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    if (target == null || !index.HasExpansion(target))
                        summary.AddError("events.trigger.unknown_expansion", scope + " references unknown bunker expansion '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.ItemQuantityAvailable:
                    ValidateItem(summary, target, scope);
                    break;
                case ScenarioConditionKind.CustomTrigger:
                    if (target == null || !index.HasTrigger(target))
                        summary.AddError("events.trigger.unknown_trigger", scope + " references unknown trigger '" + (target ?? string.Empty) + "'.");
                    break;
            }
        }

        private static void ValidateJournal(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> actionIds)
        {
            ScenarioDefinitionIndex index = new ScenarioDefinitionIndex(definition);
            HashSet<string> entryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                string id = TrimToNull(entry != null ? entry.Id : null);
                string scope = "[Events] Journal entry '" + (id ?? ("#" + i)) + "'";
                if (id == null)
                    summary.AddError("journal.entry.id_required", "[Events] Journal entry #" + i + " is missing id.");
                else if (!entryIds.Add(id))
                    summary.AddError("journal.entry.id_duplicate", "[Events] Duplicate journal entry id: " + id);
                else
                    AddActionId(summary, actionIds, "action:journal." + id, "[Events] Journal entry schedule id conflicts with another scheduled action: journal." + id);

                if (TrimToNull(entry != null ? entry.Text : null) == null)
                    summary.AddError("journal.entry.text_required", scope + " is missing text.");

                ValidateTime(summary, entry != null ? entry.DueTime : null, scope);

                string triggerId = TrimToNull(entry != null ? entry.TriggerId : null);
                if (triggerId != null && !index.HasTrigger(triggerId))
                    summary.AddError("journal.entry.unknown_trigger", scope + " references unknown trigger '" + triggerId + "'.");

                if (entry != null && entry.Writer != null && !ScenarioActorReferenceIndex.Contains(definition, entry.Writer, true, true))
                    summary.AddError("journal.entry.deleted_writer", scope + " references deleted cast member actor '" + ScenarioActorReferenceIndex.Format(entry.Writer) + "'.");

                if (entry != null && entry.Mode == ScenarioJournalEntryMode.Repeat && entry.CooldownMinutes < 0)
                    summary.AddError("journal.entry.cooldown", scope + " repeat cooldown cannot be negative.");

                for (int c = 0; entry != null && entry.Conditions != null && c < entry.Conditions.Count; c++)
                    ValidateTriggerCondition(definition, index, summary, entry.Conditions[c], scope);
            }
        }

        private static void ValidateSharedSchedules(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> actionIds)
        {
            ScenarioDefinitionIndex index = new ScenarioDefinitionIndex(definition);
            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                string id = TrimToNull(action != null ? action.Id : null);
                if (id == null)
                    summary.AddError("events.action.id_required", "[Events] Scheduled action #" + i + " is missing id.");
                else
                    AddActionId(summary, actionIds, "action:" + id, "[Events] Duplicate scheduled action id: " + id);

                ValidateTime(summary, action != null ? action.DueTime : null, "[Events] Scheduled action '" + (id ?? ("#" + i)) + "'");
                ValidatePolicy(summary, action != null ? action.Policy : null, action != null ? action.DueTime : null, "[Events] Scheduled action '" + (id ?? ("#" + i)) + "'");
                if (action == null || action.Effects == null || action.Effects.Count == 0)
                    summary.AddError("events.action.effects_required", "[Events] Scheduled action '" + (id ?? ("#" + i)) + "' must contain at least one effect.");

                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                    ValidateEffect(definition, index, summary, action.Effects[e], "[Events] Scheduled action '" + (id ?? ("#" + i)) + "'");
            }
        }

        private static void ValidateEffect(ScenarioDefinition definition, ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioEffectDefinition effect, string scope)
        {
            if (effect == null)
            {
                summary.AddError("events.effect.null", scope + " has a null effect.");
                return;
            }

            switch (effect.Kind)
            {
                case ScenarioEffectKind.AddInventory:
                case ScenarioEffectKind.RemoveInventory:
                    ValidateItem(summary, effect.ItemId, scope);
                    if (effect.Quantity <= 0)
                        summary.AddError("inventory.effect.quantity", scope + " inventory effect quantity must be greater than zero.");
                    break;
                case ScenarioEffectKind.SetWeather:
                case ScenarioEffectKind.RestoreWeather:
                    if (!IsValidWeather(effect.WeatherState))
                        summary.AddError("events.effect.weather", scope + " weather effect has invalid state.");
                    break;
                case ScenarioEffectKind.SpawnFutureSurvivor:
                    string survivorId = TrimToNull(effect.SurvivorId) ?? TrimToNull(effect.TargetId);
                    if (effect.ActorRef != null)
                    {
                        if (!ScenarioActorReferenceIndex.Contains(definition, effect.ActorRef, false, true))
                            summary.AddError("people.effect.deleted_actor", scope + " references deleted future survivor actor '" + ScenarioActorReferenceIndex.Format(effect.ActorRef) + "'. Fix: open Events > Scheduled Changes and pick an existing future survivor, or clear the actor link.");
                    }
                    else if (survivorId == null)
                        summary.AddError("people.effect.survivor_required", scope + " survivor effect is missing survivorId/targetId.");
                    else if (!index.HasFutureSurvivor(survivorId))
                        summary.AddError("people.effect.unknown_survivor", scope + " references unknown future survivor '" + survivorId + "'.");
                    break;
                case ScenarioEffectKind.StartQuest:
                    string questId = TrimToNull(effect.QuestId) ?? TrimToNull(effect.TargetId);
                    if (questId == null)
                        summary.AddError("quests.effect.quest_required", scope + " quest effect is missing questId/targetId.");
                    else if (!index.HasQuest(questId))
                        summary.AddError("quests.effect.unknown_quest", scope + " references unknown quest '" + questId + "'.");
                    break;
                case ScenarioEffectKind.ActivateObject:
                case ScenarioEffectKind.DeactivateObject:
                    string objectId = TrimToNull(effect.ObjectId) ?? TrimToNull(effect.TargetId);
                    if (objectId == null)
                        summary.AddError("bunker.effect.object_required", scope + " object effect is missing objectId/targetId.");
                    else if (!index.HasObject(objectId))
                        summary.AddError("bunker.effect.unknown_object", scope + " references unknown object '" + objectId + "'.");
                    break;
                case ScenarioEffectKind.UnlockBunkerExpansion:
                    string expansionId = TrimToNull(effect.BunkerExpansionId) ?? TrimToNull(effect.TargetId);
                    if (expansionId == null)
                        summary.AddError("bunker.effect.expansion_required", scope + " bunker unlock effect is missing expansion id.");
                    else if (!index.HasExpansion(expansionId))
                        summary.AddError("bunker.effect.unknown_expansion", scope + " references unknown bunker expansion '" + expansionId + "'.");
                    break;
                case ScenarioEffectKind.SetScenarioFlag:
                    if (TrimToNull(effect.FlagId) == null && TrimToNull(effect.TargetId) == null)
                        summary.AddError("events.effect.flag_required", scope + " flag effect is missing flag id.");
                    break;
                case ScenarioEffectKind.FireTrigger:
                    string triggerId = TrimToNull(effect.TriggerId) ?? TrimToNull(effect.TargetId);
                    if (triggerId == null)
                        summary.AddError("events.effect.trigger_required", scope + " trigger effect is missing triggerId/targetId.");
                    else if (!index.HasTrigger(triggerId))
                        summary.AddError("events.effect.unknown_trigger", scope + " references unknown trigger '" + triggerId + "'.");
                    break;
                case ScenarioEffectKind.WriteJournalEntry:
                    if (TrimToNull(ScenarioPropertyBag.GetString(effect.Properties, "text", null)) == null)
                        summary.AddError("journal.effect.text_required", scope + " journal effect is missing text.");
                    break;
                case ScenarioEffectKind.WorldEvent:
                    ValidateWorldEvent(summary, effect, scope);
                    break;
            }
        }

        private static void ValidatePolicy(ValidationSummary summary, ScenarioSchedulePolicy policy, ScenarioScheduleTime dueTime, string scope)
        {
            if (policy == null)
                return;
            if (policy.CooldownMinutes < 0)
                summary.AddError("schedule.policy.cooldown", scope + " cooldownMinutes cannot be negative.");
            if (policy.WindowEndDay < 0)
                summary.AddError("schedule.policy.window", scope + " windowEndDay cannot be negative.");
            if (policy.WindowEndDay > 0 && dueTime != null && policy.WindowEndDay < dueTime.Day)
                summary.AddError("schedule.policy.window", scope + " windowEndDay must be on or after DueTime day.");
            if (policy.Chance < 0f || policy.Chance > 1f)
                summary.AddError("schedule.policy.chance", scope + " chance must be between 0 and 1.");
            if (policy.JitterMinutes < 0)
                summary.AddError("schedule.policy.jitter", scope + " jitterMinutes cannot be negative.");
            if (policy.MaxRuns < 0)
                summary.AddError("schedule.policy.max_runs", scope + " maxRuns cannot be negative.");
        }

        private static void ValidateWorldEvent(ValidationSummary summary, ScenarioEffectDefinition effect, string scope)
        {
            string eventType = TrimToNull(ScenarioPropertyBag.GetString(effect.Properties, "eventType", null));
            if (eventType == null)
            {
                summary.AddError("events.world.event_type", scope + " WorldEvent effect is missing eventType.");
                return;
            }

            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
            {
                string npcType = TrimToNull(ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Passerby"));
                if (!IsNpcVisitType(npcType))
                    summary.AddError("events.world.npc_type", scope + " WorldEvent NpcVisit has unknown npcType '" + (npcType ?? string.Empty) + "'.");
                if (ScenarioPropertyBag.GetInt(effect.Properties, "count", 1) < 1)
                    summary.AddError("events.world.count", scope + " WorldEvent NpcVisit count must be at least 1.");
                ValidateItemSpec(summary, ScenarioPropertyBag.GetString(effect.Properties, "tradeItems", null), scope + " tradeItems");
                ValidateItemSpec(summary, ScenarioPropertyBag.GetString(effect.Properties, "lootItems", null), scope + " lootItems");
                return;
            }

            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
            {
                int count = ScenarioPropertyBag.GetInt(effect.Properties, "count", 0);
                int min = ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", count > 0 ? count : 1);
                int max = ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", count > 0 ? count : min);
                if (count < 0 || min < 1 || max < min)
                    summary.AddError("events.world.raid_count", scope + " WorldEvent Raid has invalid count/minNpcs/maxNpcs.");
                ValidateItemSpec(summary, ScenarioPropertyBag.GetString(effect.Properties, "weapons", null), scope + " weapons");
                ValidateItemSpec(summary, ScenarioPropertyBag.GetString(effect.Properties, "armor", null), scope + " armor");
                return;
            }

            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
            {
                string outcome = TrimToNull(ScenarioPropertyBag.GetString(effect.Properties, "outcome", ScenarioPropertyBag.GetString(effect.Properties, "broadcastOutcome", "None")));
                if (!string.Equals(outcome, "None", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Trader", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Recruit", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Joiner", StringComparison.OrdinalIgnoreCase))
                    summary.AddError("events.world.broadcast_outcome", scope + " WorldEvent Broadcast has unknown outcome '" + (outcome ?? string.Empty) + "'.");
                return;
            }

            summary.AddError("events.world.event_type", scope + " WorldEvent has unknown eventType '" + eventType + "'.");
        }

        private static bool IsNpcVisitType(string value)
        {
            return string.Equals(value, "Trader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Joiner", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Recruit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Passerby", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateItemSpec(ValidationSummary summary, string spec, string scope)
        {
            if (TrimToNull(spec) == null)
                return;

            string[] entries = spec.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] parts = entries[i].Split(':');
                string itemId = parts.Length > 0 ? TrimToNull(parts[0]) : null;
                int quantity = 1;
                if (parts.Length > 1 && !int.TryParse(parts[1], out quantity))
                    quantity = 0;
                if (itemId == null || quantity <= 0)
                {
                    summary.AddError("events.world.item_spec", scope + " contains invalid item entry '" + entries[i] + "'.");
                    continue;
                }
                ValidateItem(summary, itemId, scope);
            }
        }

        private static void ValidateTime(ValidationSummary summary, ScenarioScheduleTime time, string scope)
        {
            if (time == null)
            {
                summary.AddError("schedule.time.required", scope + " is missing schedule time.");
                return;
            }

            if (time.Day < 1 || time.Hour < 0 || time.Hour > 23 || time.Minute < 0 || time.Minute > 59)
                summary.AddError("schedule.time.invalid", scope + " has invalid day/hour/minute.");
        }

        private static void ValidateItem(ValidationSummary summary, string itemId, string scope)
        {
            if (TrimToNull(itemId) == null)
            {
                summary.AddError("inventory.item.required", scope + " is missing item id.");
                return;
            }

            ItemManager.ItemType type;
            if (!InventoryHelper.ResolveItemType(itemId, out type))
                summary.AddError("inventory.item.invalid", scope + " references unknown item id '" + itemId + "'.");
        }

        private static bool IsValidWeather(string state)
        {
            string value = TrimToNull(state);
            return value != null
                && (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "Rain", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "BlackRain", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "LightSand", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "MediumSand", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "HeavySand", StringComparison.OrdinalIgnoreCase));
        }

        private static void AddActionId(ValidationSummary summary, HashSet<string> ids, string id, string message)
        {
            if (ids.Contains(id))
                summary.AddError("schedule.id.duplicate", message);
            else
                ids.Add(id);
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}

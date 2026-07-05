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
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal sealed class SchedulingValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            HashSet<string> actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ValidateLegacySchedules(definition, summary, actionIds);
            ValidateSharedSchedules(definition, summary, actionIds);
        }

        private static void ValidateLegacySchedules(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> actionIds)
        {
            ScenarioDefinitionIndex index = new ScenarioDefinitionIndex(definition);
            for (int i = 0; definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                string id = TrimToNull(survivor != null ? survivor.Id : null);
                if (id == null)
                    summary.AddError("people.future_survivor.id_required", "[People] Future survivor #" + i + " is missing id.");
                else
                    AddActionId(summary, actionIds, "legacy:survivor:" + id, "[People] Duplicate future survivor schedule id: " + id);
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
                    AddActionId(summary, actionIds, "legacy:inventory:" + id, "[Inventory / Storage] Duplicate timed inventory schedule id: " + id);
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
                    AddActionId(summary, actionIds, "legacy:weather:" + id, "[Events] Duplicate weather event id: " + id);
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
                        ValidateTriggerCondition(index, summary, action.ConditionRefs[c], "[Events] Trigger '" + id + "'");
                    continue;
                }

                if (!ScenarioTriggerDefinitionCompiler.IsManual(trigger) && !string.IsNullOrEmpty(reason))
                    summary.AddError("events.trigger.invalid_target", "[Events] " + reason);
            }
        }

        private static void ValidateTriggerCondition(ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioConditionRef condition, string scope)
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
                    if (target == null || (!index.HasFamilySurvivor(target) && !index.HasFutureSurvivor(target)))
                        summary.AddError("events.trigger.unknown_survivor", scope + " references unknown survivor '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    if (target == null || !index.HasExpansion(target))
                        summary.AddError("events.trigger.unknown_expansion", scope + " references unknown bunker expansion '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.ItemQuantityAvailable:
                    ValidateItem(summary, target, scope);
                    break;
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
                if (action == null || action.Effects == null || action.Effects.Count == 0)
                    summary.AddError("events.action.effects_required", "[Events] Scheduled action '" + (id ?? ("#" + i)) + "' must contain at least one effect.");

                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                    ValidateEffect(index, summary, action.Effects[e], "[Events] Scheduled action '" + (id ?? ("#" + i)) + "'");
            }
        }

        private static void ValidateEffect(ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioEffectDefinition effect, string scope)
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
                    if (survivorId == null)
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

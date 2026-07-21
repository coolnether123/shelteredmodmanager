using System;
using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal sealed class GateConditionValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            ScenarioDefinitionIndex index = new ScenarioDefinitionIndex(definition);
            for (int i = 0; definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                string id = TrimToNull(gate != null ? gate.Id : null);
                if (id == null)
                    summary.AddError("events.gate.id_required", "[Events] Gate #" + i + " is missing id.");
                ValidateGroup(definition, index, summary, gate != null ? gate.Conditions : null, "[Events] Gate '" + (id ?? ("#" + i)) + "'");
            }

            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                string gateId = TrimToNull(action != null ? action.GateId : null);
                if (gateId != null && !index.HasGate(gateId))
                    summary.AddError("events.action.unknown_gate", "[Events] Scheduled action '" + (action.Id ?? ("#" + i)) + "' references unknown gate '" + gateId + "'.");
            }

            for (int i = 0; definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                string entryId = definition.Journal.Entries[i] != null ? definition.Journal.Entries[i].Id : null;
                string gateId = TrimToNull(definition.Journal.Entries[i] != null ? definition.Journal.Entries[i].GateId : null);
                if (gateId != null && !index.HasGate(gateId))
                    summary.AddError("journal.entry.unknown_gate", "[Events] Journal entry '" + (entryId ?? ("#" + i)) + "' references unknown gate '" + gateId + "'.");
            }

            ValidateCircularGateRefs(definition, summary);
        }

        private static void ValidateGroup(ScenarioDefinition definition, ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioConditionGroup group, string scope)
        {
            if (group == null)
                return;

            for (int i = 0; group.Conditions != null && i < group.Conditions.Count; i++)
                ValidateCondition(definition, index, summary, group.Conditions[i], scope);

            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
                ValidateGroup(definition, index, summary, group.Groups[i], scope);
        }

        private static void ValidateCondition(ScenarioDefinition definition, ScenarioDefinitionIndex index, ValidationSummary summary, ScenarioConditionRef condition, string scope)
        {
            if (condition == null)
            {
                summary.AddError("events.condition.null", scope + " has a null condition.");
                return;
            }

            string target = TrimToNull(condition.TargetId);
            switch (condition.Kind)
            {
                case ScenarioConditionKind.QuestActive:
                case ScenarioConditionKind.QuestCompleted:
                case ScenarioConditionKind.QuestFailed:
                    if (target == null || !index.HasQuest(target))
                        summary.AddError("quests.condition.unknown", scope + " references unknown quest '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    if (target == null || !index.HasExpansion(target))
                        summary.AddError("bunker.condition.unknown_expansion", scope + " references unknown bunker expansion '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.ScenarioFlagSet:
                    if (TrimToNull(condition.FlagId) == null && target == null)
                        summary.AddError("events.condition.flag_required", scope + " scenario flag condition is missing flag id.");
                    break;
                case ScenarioConditionKind.TimeReached:
                    if (condition.Time != null && (condition.Time.Day < 1 || condition.Time.Hour < 0 || condition.Time.Hour > 23 || condition.Time.Minute < 0 || condition.Time.Minute > 59))
                        summary.AddError("events.condition.time_invalid", scope + " time condition has invalid day/hour/minute.");
                    break;
                case ScenarioConditionKind.SurvivorPresent:
                case ScenarioConditionKind.SurvivorStatCheck:
                case ScenarioConditionKind.SurvivorTraitCheck:
                    if (condition.ActorRef != null)
                    {
                        if (!ScenarioCastMemberReferenceCatalog.HasActorRef(definition, condition.ActorRef, true, true))
                            summary.AddError("people.condition.deleted_actor", scope + " references deleted cast member actor '" + ScenarioCastMemberReferenceCatalog.FormatActorRef(condition.ActorRef) + "'. Fix: open Events > Conditions and pick an existing cast member, or clear the actor link.");
                    }
                    else if (target == null)
                        summary.AddError("people.condition.survivor_required", scope + " survivor condition is missing target id.");
                    else if (!index.HasFamilySurvivor(target) && !index.HasFutureSurvivor(target))
                        summary.AddError("people.condition.unknown_survivor", scope + " references unknown survivor '" + target + "'.");
                    break;
                case ScenarioConditionKind.ItemQuantityAvailable:
                    if (target == null)
                        summary.AddError("inventory.condition.item_required", scope + " item quantity condition is missing item id.");
                    else
                    {
                        ItemManager.ItemType type;
                        if (!ContentInjector.ResolveItemType(target, out type))
                            summary.AddError("inventory.condition.item_unknown", scope + " references unknown item id '" + target + "'.");
                    }
                    if (condition.Quantity <= 0)
                        summary.AddError("inventory.condition.quantity", scope + " item quantity condition must be greater than zero.");
                    break;
                case ScenarioConditionKind.TechnologyUnlocked:
                    if (target == null)
                        summary.AddError("events.condition.target_required", scope + " condition is missing target id.");
                    break;
                case ScenarioConditionKind.CustomTrigger:
                    if (target == null)
                        summary.AddError("events.condition.target_required", scope + " condition is missing target id.");
                    else if (!index.HasTrigger(target))
                        summary.AddError("events.condition.unknown_trigger", scope + " references unknown trigger '" + target + "'.");
                    break;
            }
        }

        private static void ValidateCircularGateRefs(ScenarioDefinition definition, ValidationSummary summary)
        {
            Dictionary<string, string> edges = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                string id = TrimToNull(gate != null ? gate.Id : null);
                if (id == null)
                    continue;
                string target = FirstGateReference(gate.Conditions);
                if (target != null)
                    edges[id] = target;
            }

            foreach (KeyValuePair<string, string> edge in edges)
            {
                string slow = edge.Key;
                string fast = edge.Value;
                while (fast != null && edges.ContainsKey(fast))
                {
                    if (string.Equals(slow, fast, StringComparison.OrdinalIgnoreCase))
                    {
                        summary.AddError("events.gate.circular", "[Events] Gate dependency chain contains a cycle at '" + slow + "'.");
                        break;
                    }
                    fast = edges[fast];
                }
            }
        }

        private static string FirstGateReference(ScenarioConditionGroup group)
        {
            for (int i = 0; group != null && group.Conditions != null && i < group.Conditions.Count; i++)
            {
                ScenarioConditionRef condition = group.Conditions[i];
                if (condition != null && condition.Kind == ScenarioConditionKind.ScenarioFlagSet && TrimToNull(condition.TargetId) != null)
                    return condition.TargetId;
            }
            return null;
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

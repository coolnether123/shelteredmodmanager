using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class GateConditionValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            HashSet<string> gates = CollectGateIds(definition);
            for (int i = 0; definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                string id = TrimToNull(gate != null ? gate.Id : null);
                if (id == null)
                    summary.AddError("events.gate.id_required", "[Events] Gate #" + i + " is missing id.");
                ValidateGroup(definition, summary, gate != null ? gate.Conditions : null, "[Events] Gate '" + (id ?? ("#" + i)) + "'");
            }

            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                string gateId = TrimToNull(action != null ? action.GateId : null);
                if (gateId != null && !gates.Contains(gateId))
                    summary.AddError("events.action.unknown_gate", "[Events] Scheduled action '" + (action.Id ?? ("#" + i)) + "' references unknown gate '" + gateId + "'.");
            }

            ValidateCircularGateRefs(definition, summary);
        }

        private static void ValidateGroup(ScenarioDefinition definition, ValidationSummary summary, ScenarioConditionGroup group, string scope)
        {
            if (group == null)
                return;

            for (int i = 0; group.Conditions != null && i < group.Conditions.Count; i++)
                ValidateCondition(definition, summary, group.Conditions[i], scope);

            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
                ValidateGroup(definition, summary, group.Groups[i], scope);
        }

        private static void ValidateCondition(ScenarioDefinition definition, ValidationSummary summary, ScenarioConditionRef condition, string scope)
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
                    if (target == null || !HasQuest(definition, target))
                        summary.AddError("quests.condition.unknown", scope + " references unknown quest '" + (target ?? string.Empty) + "'.");
                    break;
                case ScenarioConditionKind.BunkerExpansionUnlocked:
                    if (target == null || !HasExpansion(definition, target))
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
                    if (target == null)
                        summary.AddError("people.condition.survivor_required", scope + " survivor condition is missing target id.");
                    break;
                case ScenarioConditionKind.ItemQuantityAvailable:
                    if (target == null)
                        summary.AddError("inventory.condition.item_required", scope + " item quantity condition is missing item id.");
                    if (condition.Quantity <= 0)
                        summary.AddError("inventory.condition.quantity", scope + " item quantity condition must be greater than zero.");
                    break;
                case ScenarioConditionKind.TechnologyUnlocked:
                case ScenarioConditionKind.CustomTrigger:
                    if (target == null)
                        summary.AddError("events.condition.target_required", scope + " condition is missing target id.");
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

        private static HashSet<string> CollectGateIds(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.Gates != null && i < definition.Gates.Count; i++)
            {
                string id = TrimToNull(definition.Gates[i] != null ? definition.Gates[i].Id : null);
                if (id != null)
                    ids.Add(id);
            }
            return ids;
        }

        private static bool HasQuest(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
                if (definition.Quests.Quests[i] != null && string.Equals(definition.Quests.Quests[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool HasExpansion(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
                if (definition.BunkerGrid.Expansions[i] != null && string.Equals(definition.BunkerGrid.Expansions[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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

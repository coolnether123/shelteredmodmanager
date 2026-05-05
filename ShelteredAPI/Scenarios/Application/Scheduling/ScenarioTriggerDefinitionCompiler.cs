using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
    internal static class ScenarioTriggerDefinitionCompiler
    {
        public static bool TryCreateAction(TriggerDef trigger, int index, out ScenarioScheduledActionDefinition action, out string reason)
        {
            action = null;
            reason = null;

            string triggerId = TrimToNull(trigger != null ? trigger.Id : null);
            if (triggerId == null)
            {
                reason = "Trigger #" + index.ToString(CultureInfo.InvariantCulture) + " is missing id.";
                return false;
            }

            string type = Normalize(trigger.Type);
            if (type == null || type == "manual" || type == "custom" || type == "code")
                return false;

            action = NewFireAction(triggerId, index, ReadSchedule(trigger.Properties));
            if (type == "immediate" || type == "startup" || type == "start")
                return true;

            if (type == "timereached" || type == "dayreached" || type == "schedule" || type == "scheduled")
                return true;

            ScenarioConditionRef condition;
            if (TryCreateCondition(trigger, type, out condition, out reason))
            {
                action.ConditionRefs.Add(condition);
                return true;
            }

            action = null;
            if (string.IsNullOrEmpty(reason))
                reason = "Unsupported trigger type '" + (trigger.Type ?? string.Empty) + "'. Use manual/custom for code-fired triggers or a supported automatic type.";
            return false;
        }

        public static bool IsManual(TriggerDef trigger)
        {
            string type = Normalize(trigger != null ? trigger.Type : null);
            return type == null || type == "manual" || type == "custom" || type == "code";
        }

        public static string BuildActionId(string triggerId, int index)
        {
            string id = TrimToNull(triggerId);
            return "trigger." + (id ?? index.ToString(CultureInfo.InvariantCulture));
        }

        private static ScenarioScheduledActionDefinition NewFireAction(string triggerId, int index, ScenarioScheduleTime dueTime)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = BuildActionId(triggerId, index);
            action.ActionType = "FireTrigger";
            action.DueTime = dueTime ?? new ScenarioScheduleTime();
            action.Effects.Add(new ScenarioEffectDefinition
            {
                Kind = ScenarioEffectKind.FireTrigger,
                TriggerId = triggerId,
                TargetId = triggerId
            });
            return action;
        }

        private static bool TryCreateCondition(TriggerDef trigger, string type, out ScenarioConditionRef condition, out string reason)
        {
            condition = new ScenarioConditionRef();
            condition.Id = trigger.Id + ".condition";
            reason = null;

            if (type == "scenarioflagset" || type == "flagset")
            {
                condition.Kind = ScenarioConditionKind.ScenarioFlagSet;
                condition.FlagId = ScenarioPropertyBag.FirstString(trigger.Properties, "flagId", "targetId");
                condition.TargetId = condition.FlagId;
                condition.FlagValue = ScenarioPropertyBag.FirstString(trigger.Properties, "flagValue", "value");
                return Require(condition.FlagId, "Trigger '" + trigger.Id + "' requires flagId for type '" + trigger.Type + "'.", out reason);
            }

            if (type == "questactive" || type == "questcompleted" || type == "questfailed")
            {
                condition.Kind = type == "questactive"
                    ? ScenarioConditionKind.QuestActive
                    : (type == "questcompleted" ? ScenarioConditionKind.QuestCompleted : ScenarioConditionKind.QuestFailed);
                condition.TargetId = ScenarioPropertyBag.FirstString(trigger.Properties, "questId", "targetId");
                return Require(condition.TargetId, "Trigger '" + trigger.Id + "' requires questId for type '" + trigger.Type + "'.", out reason);
            }

            if (type == "survivorpresent")
            {
                condition.Kind = ScenarioConditionKind.SurvivorPresent;
                condition.TargetId = ScenarioPropertyBag.FirstString(trigger.Properties, "survivorId", "name", "targetId");
                return Require(condition.TargetId, "Trigger '" + trigger.Id + "' requires survivorId/name.", out reason);
            }

            if (type == "itemquantityavailable" || type == "itemquantity" || type == "hasitem")
            {
                condition.Kind = ScenarioConditionKind.ItemQuantityAvailable;
                condition.TargetId = ScenarioPropertyBag.FirstString(trigger.Properties, "itemId", "targetId");
                condition.Quantity = ScenarioPropertyBag.GetInt(trigger.Properties, "quantity", 1);
                if (!Require(condition.TargetId, "Trigger '" + trigger.Id + "' requires itemId.", out reason))
                    return false;
                if (condition.Quantity <= 0)
                {
                    reason = "Trigger '" + trigger.Id + "' item quantity must be greater than zero.";
                    return false;
                }
                return true;
            }

            if (type == "bunkerexpansionunlocked" || type == "technologyunlocked")
            {
                condition.Kind = type == "technologyunlocked"
                    ? ScenarioConditionKind.TechnologyUnlocked
                    : ScenarioConditionKind.BunkerExpansionUnlocked;
                condition.TargetId = ScenarioPropertyBag.FirstString(trigger.Properties, "bunkerExpansionId", "technologyId", "targetId");
                return Require(condition.TargetId, "Trigger '" + trigger.Id + "' requires target id for type '" + trigger.Type + "'.", out reason);
            }

            condition = null;
            return false;
        }

        private static ScenarioScheduleTime ReadSchedule(List<ScenarioProperty> properties)
        {
            ScenarioScheduleTime time = new ScenarioScheduleTime();
            time.Day = Math.Max(1, ScenarioPropertyBag.GetInt(properties, "day", ScenarioPropertyBag.GetInt(properties, "days", time.Day)));
            time.Hour = Clamp(ScenarioPropertyBag.GetInt(properties, "hour", time.Hour), 0, 23);
            time.Minute = Clamp(ScenarioPropertyBag.GetInt(properties, "minute", time.Minute), 0, 59);
            return time;
        }

        private static bool Require(string value, string message, out string reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(value))
                return true;

            reason = message;
            return false;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static string Normalize(string value)
        {
            string trimmed = TrimToNull(value);
            return trimmed != null ? trimmed.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant() : null;
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

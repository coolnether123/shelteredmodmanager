using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioWinLossConditionAdapter : IScenarioWinLossConditionAdapter
    {
        public bool TryCreateConditionRef(
            ScenarioDefinition definition,
            ScenarioRuntimeBinding binding,
            ConditionDef condition,
            out ScenarioConditionRef conditionRef,
            out string reason)
        {
            conditionRef = null;
            reason = null;

            if (condition == null)
                return false;

            string type = Normalize(condition.Type);
            if (type == null)
            {
                reason = "Win/loss condition type is missing.";
                return false;
            }

            conditionRef = new ScenarioConditionRef();
            conditionRef.Id = condition.Id;

            if (type == "survivedays")
            {
                int days = GetInt(condition.Properties, "days", 0);
                if (days <= 0)
                {
                    reason = "surviveDays condition requires a positive 'days' property.";
                    return false;
                }

                conditionRef.Kind = ScenarioConditionKind.TimeReached;
                conditionRef.Time = new ScenarioScheduleTime();
                conditionRef.Time.Day = Math.Max(1, (binding != null ? binding.DayCreated : 1) + days - 1);
                conditionRef.Time.Hour = 0;
                conditionRef.Time.Minute = 0;
                return true;
            }

            if (type == "timereached" || type == "dayreached")
            {
                int day = GetInt(condition.Properties, "day", GetInt(condition.Properties, "days", 0));
                if (day <= 0)
                {
                    reason = "timeReached/dayReached condition requires a positive 'day' property.";
                    return false;
                }

                conditionRef.Kind = ScenarioConditionKind.TimeReached;
                conditionRef.Time = new ScenarioScheduleTime();
                conditionRef.Time.Day = day;
                conditionRef.Time.Hour = GetInt(condition.Properties, "hour", 0);
                conditionRef.Time.Minute = GetInt(condition.Properties, "minute", 0);
                return true;
            }

            if (type == "itemquantityavailable" || type == "itemquantity" || type == "hasitem")
            {
                conditionRef.Kind = ScenarioConditionKind.ItemQuantityAvailable;
                conditionRef.TargetId = FirstProperty(condition.Properties, "itemId", "targetId");
                conditionRef.Quantity = GetInt(condition.Properties, "quantity", 1);
                return Require(conditionRef.TargetId, "Item quantity condition requires an itemId property.", out reason);
            }

            if (type == "questactive" || type == "questcompleted" || type == "questfailed")
            {
                conditionRef.Kind = type == "questactive"
                    ? ScenarioConditionKind.QuestActive
                    : (type == "questcompleted" ? ScenarioConditionKind.QuestCompleted : ScenarioConditionKind.QuestFailed);
                conditionRef.TargetId = FirstProperty(condition.Properties, "questId", "targetId");
                return Require(conditionRef.TargetId, "Quest condition requires a questId property.", out reason);
            }

            if (type == "survivorpresent")
            {
                conditionRef.Kind = ScenarioConditionKind.SurvivorPresent;
                conditionRef.TargetId = FirstProperty(condition.Properties, "survivorId", "name", "targetId");
                return Require(conditionRef.TargetId, "Survivor condition requires a survivorId/name property.", out reason);
            }

            if (type == "bunkerexpansionunlocked" || type == "technologyunlocked")
            {
                conditionRef.Kind = type == "technologyunlocked"
                    ? ScenarioConditionKind.TechnologyUnlocked
                    : ScenarioConditionKind.BunkerExpansionUnlocked;
                conditionRef.TargetId = FirstProperty(condition.Properties, "bunkerExpansionId", "technologyId", "targetId");
                return Require(conditionRef.TargetId, "Bunker/technology condition requires a target id property.", out reason);
            }

            if (type == "scenarioflagset" || type == "flagset")
            {
                conditionRef.Kind = ScenarioConditionKind.ScenarioFlagSet;
                conditionRef.FlagId = FirstProperty(condition.Properties, "flagId", "targetId");
                conditionRef.TargetId = conditionRef.FlagId;
                conditionRef.FlagValue = FirstProperty(condition.Properties, "flagValue", "value");
                return Require(conditionRef.FlagId, "Scenario flag condition requires a flagId property.", out reason);
            }

            if (type == "customtrigger" || type == "trigger")
            {
                conditionRef.Kind = ScenarioConditionKind.CustomTrigger;
                conditionRef.TargetId = FirstProperty(condition.Properties, "triggerId", "targetId");
                return Require(conditionRef.TargetId, "Custom trigger condition requires a triggerId property.", out reason);
            }

            reason = "Unsupported win/loss condition type: " + condition.Type + ".";
            conditionRef = null;
            return false;
        }

        private static bool Require(string value, string message, out string reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(value))
                return true;

            reason = message;
            return false;
        }

        private static string FirstProperty(List<ScenarioProperty> properties, params string[] keys)
        {
            for (int i = 0; keys != null && i < keys.Length; i++)
            {
                string value = GetProperty(properties, keys[i]);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }

        private static int GetInt(List<ScenarioProperty> properties, string key, int fallback)
        {
            string value = GetProperty(properties, key);
            int parsed;
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return null;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            return value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}

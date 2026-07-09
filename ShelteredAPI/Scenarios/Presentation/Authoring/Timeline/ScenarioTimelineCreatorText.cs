using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Timeline
{
    /// <summary>Single creator-language mapping for Timeline condition/effect names and summaries.</summary>
    internal static class ScenarioTimelineCreatorText
    {
        public static string EffectName(ScenarioEffectKind kind)
        {
            switch (kind)
            {
                case ScenarioEffectKind.UnlockBunkerExpansion: return "Unlock bunker area";
                case ScenarioEffectKind.ActivateObject: return "Enable object";
                case ScenarioEffectKind.DeactivateObject: return "Disable object";
                case ScenarioEffectKind.AddInventory: return "Deliver supplies";
                case ScenarioEffectKind.RemoveInventory: return "Remove supplies";
                case ScenarioEffectKind.SpawnFutureSurvivor: return "Bring in survivor";
                case ScenarioEffectKind.StartQuest: return "Start quest";
                case ScenarioEffectKind.SetWeather: return "Change weather";
                case ScenarioEffectKind.SetScenarioFlag: return "Set flag";
                case ScenarioEffectKind.RestoreWeather: return "Restore weather";
                case ScenarioEffectKind.FireTrigger: return "Fire trigger";
                case ScenarioEffectKind.WriteJournalEntry: return "Write journal message";
                case ScenarioEffectKind.StartConversation: return "Start conversation";
                case ScenarioEffectKind.WorldEvent: return "World event";
                default: return kind.ToString();
            }
        }

        public static string ConditionName(ScenarioConditionKind kind)
        {
            switch (kind)
            {
                case ScenarioConditionKind.TimeReached: return "Time reached";
                case ScenarioConditionKind.ItemQuantityAvailable: return "Supplies available";
                case ScenarioConditionKind.TechnologyUnlocked: return "Technology unlocked";
                case ScenarioConditionKind.QuestActive: return "Quest is active";
                case ScenarioConditionKind.QuestCompleted: return "Quest is complete";
                case ScenarioConditionKind.QuestFailed: return "Quest failed";
                case ScenarioConditionKind.SurvivorPresent: return "Survivor is present";
                case ScenarioConditionKind.SurvivorStatCheck: return "Survivor stat check";
                case ScenarioConditionKind.SurvivorTraitCheck: return "Survivor trait check";
                case ScenarioConditionKind.BunkerExpansionUnlocked: return "Bunker area unlocked";
                case ScenarioConditionKind.CustomTrigger: return "Trigger fired";
                case ScenarioConditionKind.ScenarioFlagSet: return "Flag is set";
                default: return kind.ToString();
            }
        }

        public static string EffectAdvancedDetail(ScenarioEffectKind kind) { return "Runtime effect: " + kind; }
        public static string ConditionAdvancedDetail(ScenarioConditionKind kind) { return "Runtime condition: " + kind; }

        public static string EffectNameFromRaw(string value)
        {
            ScenarioEffectKind[] kinds = (ScenarioEffectKind[])Enum.GetValues(typeof(ScenarioEffectKind));
            for (int i = 0; i < kinds.Length; i++)
                if (string.Equals(kinds[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return EffectName(kinds[i]);
            return value;
        }

        public static string ScheduledActionName(ScenarioDefinition definition, ScenarioScheduledActionDefinition action)
        {
            string delivery = InventoryDeliverySummary(action);
            if (!string.IsNullOrEmpty(delivery))
                return delivery;
            List<string> pieces = new List<string>();
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect != null)
                    pieces.Add(EffectSummary(definition, effect));
            }
            return pieces.Count > 0 ? string.Join("; ", pieces.ToArray()) : "Scheduled change";
        }

        private static string InventoryDeliverySummary(ScenarioScheduledActionDefinition action)
        {
            List<string> supplies = new List<string>();
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null || effect.Kind != ScenarioEffectKind.AddInventory)
                    return null;
                supplies.Add(effect.Quantity.ToString(CultureInfo.InvariantCulture) + " " + ItemName(effect.ItemId));
            }
            if (supplies.Count == 0)
                return null;
            return "Deliver " + string.Join(" and ", supplies.ToArray());
        }

        public static string TimelineSummary(ScenarioDefinition definition, ScenarioTimelineEntry entry)
        {
            string when = FormatWhen(entry != null ? entry.When : null);
            string name = entry != null && string.Equals(entry.SourceKind, "scheduled_action", StringComparison.OrdinalIgnoreCase)
                ? ScheduledActionName(definition, GetScheduledAction(definition, entry.SourceIndex))
                : (entry != null ? entry.Title : null);
            return when + " - " + Safe(name);
        }

        private static string EffectSummary(ScenarioDefinition definition, ScenarioEffectDefinition effect)
        {
            switch (effect.Kind)
            {
                case ScenarioEffectKind.AddInventory: return "Deliver " + effect.Quantity.ToString(CultureInfo.InvariantCulture) + " " + ItemName(effect.ItemId);
                case ScenarioEffectKind.RemoveInventory: return "Remove " + effect.Quantity.ToString(CultureInfo.InvariantCulture) + " " + ItemName(effect.ItemId);
                case ScenarioEffectKind.SetWeather: return "Change weather to " + Safe(effect.WeatherState).ToLowerInvariant();
                case ScenarioEffectKind.RestoreWeather: return "Restore weather";
                case ScenarioEffectKind.WorldEvent: return WorldEventSummary(effect);
                case ScenarioEffectKind.WriteJournalEntry: return "Write journal message";
                case ScenarioEffectKind.StartQuest: return "Start quest " + Safe(effect.QuestId ?? effect.TargetId);
                case ScenarioEffectKind.SetScenarioFlag: return "Set flag " + Safe(effect.FlagId ?? effect.TargetId) + " to " + Safe(effect.FlagValue);
                case ScenarioEffectKind.FireTrigger: return "Fire trigger " + Safe(effect.TriggerId ?? effect.TargetId);
                default: return EffectName(effect.Kind);
            }
        }

        private static string WorldEventSummary(ScenarioEffectDefinition effect)
        {
            string type = ScenarioPropertyBag.GetString(effect.Properties, "eventType", "World event");
            if (string.Equals(type, "NpcVisit", StringComparison.OrdinalIgnoreCase)) return "Visitor arrives";
            if (string.Equals(type, "Raid", StringComparison.OrdinalIgnoreCase)) return "Raid";
            if (string.Equals(type, "Broadcast", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "RadioScan", StringComparison.OrdinalIgnoreCase)) return "Radio message";
            return type;
        }

        private static string ItemName(string itemId)
        {
            if (string.Equals(itemId, "Water", StringComparison.OrdinalIgnoreCase)) return "water";
            if (string.Equals(itemId, "Ration", StringComparison.OrdinalIgnoreCase)) return "canned food";
            return Safe(itemId).Replace("_", " ").ToLowerInvariant();
        }

        private static string FormatWhen(ScenarioScheduleTime time)
        {
            if (time == null) return "Unscheduled";
            return "Day " + Math.Max(1, time.Day).ToString(CultureInfo.InvariantCulture) + ", " + time.Hour.ToString("D2", CultureInfo.InvariantCulture) + ":" + time.Minute.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static ScenarioScheduledActionDefinition GetScheduledAction(ScenarioDefinition definition, int index)
        {
            return definition != null && definition.ScheduledActions != null && index >= 0 && index < definition.ScheduledActions.Count ? definition.ScheduledActions[index] : null;
        }

        private static string Safe(string value) { return string.IsNullOrEmpty(value) ? "<missing>" : value; }
    }
}

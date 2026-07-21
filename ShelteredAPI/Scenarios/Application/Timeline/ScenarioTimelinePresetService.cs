using System;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Application.Timeline
{
    /// <summary>Creates small, runtime-native scheduled actions from Timeline's creator presets.</summary>
    internal static class ScenarioTimelinePresetService
    {
        public const string ActionPrefix = "scenario.timeline.preset.";

        public static bool CanCreate(ScenarioDefinition definition, string presetId)
        {
            string reason;
            return TryCreateAction(definition, presetId, out reason) != null;
        }

        public static bool TryCreate(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith(ActionPrefix, StringComparison.Ordinal))
                return false;

            string presetId = actionId.Substring(ActionPrefix.Length);
            string reason;
            ScenarioScheduledActionDefinition action = TryCreateAction(session.WorkingDefinition, presetId, out reason);
            if (action == null)
            {
                message = reason ?? "That Timeline preset is not available for this draft.";
                return true;
            }

            session.WorkingDefinition.ScheduledActions.Add(action);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Added " + DescribePreset(presetId) + " for " + ScenarioAuthoringSchedule.Format(action.DueTime) + ".";
            return true;
        }

        public static ScenarioScheduledActionDefinition TryCreateAction(ScenarioDefinition definition, string presetId, out string reason)
        {
            reason = null;
            if (definition == null)
            {
                reason = "No active scenario draft is available.";
                return null;
            }

            ScenarioScheduledActionDefinition action = NewAction(definition);
            if (string.Equals(presetId, "deliver_supplies", StringComparison.OrdinalIgnoreCase))
            {
                action.ActionType = ScenarioEffectKind.AddInventory.ToString();
                action.Effects.Add(InventoryEffect("Water", 5));
                action.Effects.Add(InventoryEffect("Ration", 3));
                return action;
            }
            if (string.Equals(presetId, "change_weather", StringComparison.OrdinalIgnoreCase))
            {
                action.ActionType = ScenarioEffectKind.SetWeather.ToString();
                action.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.SetWeather, WeatherState = "Rain", DurationHours = 0 });
                return action;
            }
            if (string.Equals(presetId, "visitor_arrives", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioEffectDefinition effect = new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent, Quantity = 1 };
                ScenarioPropertyBag.Set(effect.Properties, "eventType", "NpcVisit");
                ScenarioPropertyBag.Set(effect.Properties, "npcType", "Passerby");
                ScenarioPropertyBag.Set(effect.Properties, "count", "1");
                action.ActionType = ScenarioEffectKind.WorldEvent.ToString();
                action.Effects.Add(effect);
                return action;
            }
            if (string.Equals(presetId, "journal_message", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioEffectDefinition effect = new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WriteJournalEntry };
                ScenarioPropertyBag.Set(effect.Properties, "text", "A new note was added on day {day}.");
                ScenarioPropertyBag.Set(effect.Properties, "entryId", action.Id + ".journal");
                action.ActionType = ScenarioEffectKind.WriteJournalEntry.ToString();
                action.Effects.Add(effect);
                return action;
            }
            if (string.Equals(presetId, "start_quest", StringComparison.OrdinalIgnoreCase))
            {
                string questId = ScenarioEventReferenceFinder.FirstQuestId(definition);
                if (string.IsNullOrEmpty(questId))
                {
                    reason = "Add a quest in Story before scheduling its start.";
                    return null;
                }
                action.ActionType = ScenarioEffectKind.StartQuest.ToString();
                action.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.StartQuest, QuestId = questId, TargetId = questId });
                return action;
            }
            if (string.Equals(presetId, "set_flag", StringComparison.OrdinalIgnoreCase))
            {
                string flagId = ScenarioEventReferenceFinder.FirstFlagId(definition);
                if (string.IsNullOrEmpty(flagId))
                    flagId = "milestone_1";
                action.ActionType = ScenarioEffectKind.SetScenarioFlag.ToString();
                action.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.SetScenarioFlag, FlagId = flagId, TargetId = flagId, FlagValue = "true" });
                return action;
            }

            reason = "Unknown Timeline preset '" + presetId + "'.";
            return null;
        }

        private static ScenarioScheduledActionDefinition NewAction(ScenarioDefinition definition)
        {
            return new ScenarioScheduledActionDefinition
            {
                Id = ScenarioEventIdFactory.NextScheduledActionId(definition),
                DueTime = ScenarioAuthoringSchedule.NextTime()
            };
        }

        private static ScenarioEffectDefinition InventoryEffect(string itemId, int quantity)
        {
            return new ScenarioEffectDefinition { Kind = ScenarioEffectKind.AddInventory, ItemId = itemId, TargetId = itemId, Quantity = quantity };
        }

        private static string DescribePreset(string presetId)
        {
            if (string.Equals(presetId, "deliver_supplies", StringComparison.OrdinalIgnoreCase)) return "Deliver supplies";
            if (string.Equals(presetId, "change_weather", StringComparison.OrdinalIgnoreCase)) return "Change weather";
            if (string.Equals(presetId, "visitor_arrives", StringComparison.OrdinalIgnoreCase)) return "Visitor arrives";
            if (string.Equals(presetId, "journal_message", StringComparison.OrdinalIgnoreCase)) return "Journal message";
            if (string.Equals(presetId, "start_quest", StringComparison.OrdinalIgnoreCase)) return "Start quest";
            if (string.Equals(presetId, "set_flag", StringComparison.OrdinalIgnoreCase)) return "Set flag";
            return "Timeline preset";
        }
    }
}

using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Application.Timeline;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    /// <summary>Contract fixture for Timeline presets, creator summaries, and collision analysis.</summary>
    internal static class ScenarioTimelineUxVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Quests.Quests.Add(new QuestDefinition { Id = "first_quest", Title = "First Quest" });
            string[] presets = { "deliver_supplies", "change_weather", "visitor_arrives", "journal_message", "start_quest", "set_flag" };
            for (int i = 0; i < presets.Length; i++)
            {
                string reason;
                ScenarioScheduledActionDefinition action = ScenarioTimelinePresetService.TryCreateAction(definition, presets[i], out reason);
                Assert(action != null && action.DueTime != null && action.Effects != null && action.Effects.Count > 0,
                    "Timeline preset did not create a runtime-compilable scheduled action: " + presets[i], result);
                if (action != null)
                    definition.ScheduledActions.Add(action);
            }

            ScenarioScheduledActionDefinition delivery = definition.ScheduledActions[0];
            delivery.DueTime.Day = 3;
            delivery.DueTime.Hour = 8;
            delivery.DueTime.Minute = 0;
            Assert(string.Equals(ScenarioTimelineCreatorText.ScheduledActionName(definition, delivery), "Deliver 5 water and 3 canned food", StringComparison.Ordinal),
                "Timeline delivery summary was not creator-friendly.", result);

            ScenarioScheduledActionDefinition removal = new ScenarioScheduledActionDefinition { Id = "remove_water", ActionType = ScenarioEffectKind.RemoveInventory.ToString() };
            removal.DueTime.Day = 3;
            removal.DueTime.Hour = 8;
            removal.DueTime.Minute = 0;
            removal.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RemoveInventory, ItemId = "Water", TargetId = "Water", Quantity = 1 });
            definition.ScheduledActions.Add(removal);

            List<ScenarioTimelineEntry> entries = new ScenarioTimelineBuilder().BuildEntries(definition, null);
            bool collisionFound = false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && !string.IsNullOrEmpty(entries[i].Warning) && entries[i].Warning.IndexOf("Adds and removes Water", StringComparison.Ordinal) >= 0)
                    collisionFound = true;
            Assert(collisionFound, "Timeline collision analysis did not warn about same-time add/remove ordering.", result);
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }
    }
}

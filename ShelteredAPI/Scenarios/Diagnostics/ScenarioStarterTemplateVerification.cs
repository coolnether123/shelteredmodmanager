using System;
using System.Globalization;

using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring.Templates;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    /// <summary>Contract checks for serializer-backed starter template content.</summary>
    internal static class ScenarioStarterTemplateVerification
    {
        internal static void Verify(ScenarioValidationResult result)
        {
            if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.ContentResolution))
            {
                ModAPIRegistry.RegisterAPI<IContentResolutionService>(
                    GameRuntimeApiIds.ContentResolution,
                    new ShelteredContentResolutionService(),
                    "ShelteredAPI.TemplateVerification");
            }
            ScenarioStarterTemplate[] templates = ScenarioStarterTemplateCatalog.All();
            Assert(templates.Length == 3, "Starter template catalog must contain exactly three curated templates.", result);
            for (int i = 0; i < templates.Length; i++)
                VerifyTemplate(templates[i], result);
        }

        private static void VerifyTemplate(ScenarioStarterTemplate template, ScenarioValidationResult result)
        {
            Assert(template != null && !string.IsNullOrEmpty(template.BundledXml), "Starter template is missing bundled XML.", result);
            if (template == null || string.IsNullOrEmpty(template.BundledXml))
                return;

            ScenarioDefinition definition;
            try
            {
                definition = new ScenarioDefinitionSerializer().FromXml(template.BundledXml);
            }
            catch (Exception ex)
            {
                result.AddError("Starter template '" + template.Key + "' did not deserialize: " + ex.Message);
                return;
            }

            ScenarioValidationResult validation = new ScenarioValidator().Validate(definition, null);
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                    result.AddError("Starter template '" + template.Key + "' validation error: " + issues[i].Message);
            }

            string playtestReason;
            Assert(new ScenarioPlayStartReadiness().CanStartPlay(definition, out playtestReason),
                "Starter template '" + template.Key + "' failed playtest preflight: " + (playtestReason ?? "unknown reason"), result);

            string summary = template.BuildSummary();
            VerifySummaryFacts(definition, summary, template.Key, result);
        }

        private static void VerifySummaryFacts(ScenarioDefinition definition, string summary, string templateKey, ScenarioValidationResult result)
        {
            if (string.Equals(templateKey, ScenarioStarterTemplateBuilder.SmallSurvivalKey, StringComparison.OrdinalIgnoreCase))
            {
                int survivors = definition.FamilySetup != null ? definition.FamilySetup.Members.Count : 0;
                int timeline = (definition.ScheduledActions != null ? definition.ScheduledActions.Count : 0)
                    + (definition.StartingInventory != null ? definition.StartingInventory.ScheduledChanges.Count : 0);
                int victoryDay = 0;
                for (int i = 0; definition.WinLossConditions != null && i < definition.WinLossConditions.WinConditions.Count; i++)
                {
                    ConditionDef condition = definition.WinLossConditions.WinConditions[i];
                    for (int p = 0; condition != null && string.Equals(condition.Type, "surviveDays", StringComparison.OrdinalIgnoreCase) && p < condition.Properties.Count; p++)
                    {
                        ScenarioProperty property = condition.Properties[p];
                        if (property != null && string.Equals(property.Key, "day", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(property.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out victoryDay);
                    }
                }
                Assert(Contains(summary, survivors.ToString(CultureInfo.InvariantCulture) + " survivors"), "Small Survival card survivor count drifted from its definition.", result);
                Assert(Contains(summary, timeline.ToString(CultureInfo.InvariantCulture) + " timeline events"), "Small Survival card timeline count drifted from its definition.", result);
                Assert(Contains(summary, "victory: survive " + victoryDay.ToString(CultureInfo.InvariantCulture) + " days"), "Small Survival card victory summary drifted from its authored condition.", result);
                return;
            }

            if (string.Equals(templateKey, ScenarioStarterTemplateBuilder.DialogueStoryKey, StringComparison.OrdinalIgnoreCase))
            {
                int actors = 0;
                for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                {
                    if (definition.ScenarioCharacters[i] != null && definition.ScenarioCharacters[i].ActorRef != null)
                        actors++;
                }
                int stages = definition.ScenarioFlow != null ? definition.ScenarioFlow.Stages.Count : 0;
                int responses = 0;
                for (int s = 0; definition.ScenarioFlow != null && s < definition.ScenarioFlow.Stages.Count; s++)
                {
                    ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[s];
                    for (int i = 0; stage != null && i < stage.IntercomStages.Count; i++)
                        responses += stage.IntercomStages[i] != null ? stage.IntercomStages[i].Options.Count : 0;
                }
                Assert(Contains(summary, actors.ToString(CultureInfo.InvariantCulture) + " actor-bound story character"), "Dialogue Story card actor count drifted from its definition.", result);
                Assert(Contains(summary, stages.ToString(CultureInfo.InvariantCulture) + " story stages"), "Dialogue Story card stage count drifted from its definition.", result);
                Assert(Contains(summary, responses.ToString(CultureInfo.InvariantCulture) + " response branches"), "Dialogue Story card branch count drifted from its definition.", result);
                return;
            }

            MapAuthoringDefinition map = definition.Map;
            int locations = map != null ? map.Locations.Count : 0;
            int hidden = 0;
            int searchable = 0;
            int deterministicTables = 0;
            int minDanger = int.MaxValue;
            int maxDanger = int.MinValue;
            for (int i = 0; map != null && i < map.Locations.Count; i++)
            {
                if (map.Locations[i] != null && map.Locations[i].HiddenUntilDiscovered)
                    hidden++;
                if (map.Locations[i] != null && map.Locations[i].Searchable)
                    searchable++;
                if (map.Locations[i] != null)
                {
                    minDanger = Math.Min(minDanger, map.Locations[i].Danger);
                    maxDanger = Math.Max(maxDanger, map.Locations[i].Danger);
                }
            }
            for (int i = 0; map != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                bool deterministic = table != null && table.Entries.Count > 0;
                for (int e = 0; deterministic && e < table.Entries.Count; e++)
                {
                    MapLootEntryDefinition entry = table.Entries[e];
                    deterministic = entry != null && entry.MinQuantity == entry.MaxQuantity && Math.Abs(entry.Chance - 1f) <= 0.0001f;
                }
                if (deterministic)
                    deterministicTables++;
            }
            Assert(Contains(summary, locations.ToString(CultureInfo.InvariantCulture) + " map locations"), "Expedition Map card location count drifted from its definition.", result);
            Assert(Contains(summary, hidden.ToString(CultureInfo.InvariantCulture) + " hidden"), "Expedition Map card hidden-location count drifted from its definition.", result);
            Assert(Contains(summary, deterministicTables.ToString(CultureInfo.InvariantCulture) + " deterministic loot tables"), "Expedition Map card deterministic-loot count drifted from its definition.", result);
            Assert(Contains(summary, searchable.ToString(CultureInfo.InvariantCulture) + " searchable"), "Expedition Map card searchable-location count drifted from its definition.", result);
            Assert(Contains(summary, "danger " + minDanger.ToString(CultureInfo.InvariantCulture) + "-" + maxDanger.ToString(CultureInfo.InvariantCulture)), "Expedition Map card danger range drifted from its definition.", result);
        }

        private static bool Contains(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }
    }
}

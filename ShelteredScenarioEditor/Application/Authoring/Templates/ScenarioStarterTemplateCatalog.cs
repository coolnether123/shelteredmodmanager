using ShelteredScenarioEditor.Application.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Application.Authoring.Templates
{
    internal sealed class ScenarioStarterTemplate
    {
        private readonly ScenarioEditorDefinitionSerializer _serializer;

        internal ScenarioStarterTemplate(string key, string title, string teaches, string bundledXml, ScenarioEditorDefinitionSerializer serializer)
        {
            Key = key;
            Title = title;
            Teaches = teaches;
            BundledXml = bundledXml;
            _serializer = serializer;
        }

        internal string Key { get; private set; }
        internal string Title { get; private set; }
        internal string Teaches { get; private set; }
        internal string BundledXml { get; private set; }

        internal ScenarioDefinition CreateDefinition()
        {
            return _serializer.FromXml(BundledXml);
        }

        internal string BuildSummary()
        {
            return ScenarioStarterTemplateSummary.Build(CreateDefinition());
        }
    }

    /// <summary>
    /// Owns the serializer-produced XML bundled with the authoring runtime.
    /// Definitions are always instantiated by deserializing that XML, matching a
    /// saved scenario pack rather than sharing mutable fixture objects.
    /// </summary>
    internal static class ScenarioStarterTemplateCatalog
    {
        private static readonly ScenarioEditorDefinitionSerializer Serializer = new ScenarioEditorDefinitionSerializer();
        private static readonly ScenarioStarterTemplate[] Templates = BuildTemplates();

        internal static ScenarioStarterTemplate[] All()
        {
            ScenarioStarterTemplate[] copy = new ScenarioStarterTemplate[Templates.Length];
            Array.Copy(Templates, copy, Templates.Length);
            return copy;
        }

        internal static bool TryGet(string key, out ScenarioStarterTemplate template)
        {
            for (int i = 0; i < Templates.Length; i++)
            {
                if (string.Equals(Templates[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    template = Templates[i];
                    return true;
                }
            }

            template = null;
            return false;
        }

        private static ScenarioStarterTemplate[] BuildTemplates()
        {
            return new[]
            {
                Bundle(ScenarioStarterTemplateBuilder.SmallSurvivalKey, "Small Survival Challenge", "Supplies + timeline + victory", ScenarioStarterTemplateBuilder.BuildSmallSurvivalChallenge()),
                Bundle(ScenarioStarterTemplateBuilder.DialogueStoryKey, "Dialogue Story", "Story + branching dialogue", ScenarioStarterTemplateBuilder.BuildDialogueStory()),
                Bundle(ScenarioStarterTemplateBuilder.ExpeditionMapKey, "Expedition Map Scenario", "Map locations + deterministic loot", ScenarioStarterTemplateBuilder.BuildExpeditionMapScenario())
            };
        }

        private static ScenarioStarterTemplate Bundle(string key, string title, string teaches, ScenarioDefinition definition)
        {
            return new ScenarioStarterTemplate(key, title, teaches, Serializer.ToXml(definition), Serializer);
        }
    }

    internal static class ScenarioStarterTemplateSummary
    {
        internal static string Build(ScenarioDefinition definition)
        {
            if (definition == null)
                return "Template content unavailable";
            if (string.Equals(definition.Id, "Template.SmallSurvivalChallenge", StringComparison.OrdinalIgnoreCase))
                return BuildSurvival(definition);
            if (string.Equals(definition.Id, "Template.DialogueStory", StringComparison.OrdinalIgnoreCase))
                return BuildDialogue(definition);
            return BuildMap(definition);
        }

        private static string BuildSurvival(ScenarioDefinition definition)
        {
            int survivors = definition.FamilySetup != null ? definition.FamilySetup.Members.Count : 0;
            int timelineEntries = (definition.ScheduledActions != null ? definition.ScheduledActions.Count : 0)
                + (definition.StartingInventory != null ? definition.StartingInventory.ScheduledChanges.Count : 0);
            string supplies = MatchesPreset(definition.StartingInventory, ScenarioSuppliesPresetCatalog.PresetScarce) ? "scarce supplies" : "authored supplies";
            int victoryDay = FindSurvivalVictoryDay(definition);
            return survivors.ToString(CultureInfo.InvariantCulture) + " survivors - " + supplies + " - "
                + timelineEntries.ToString(CultureInfo.InvariantCulture) + " timeline events - victory: survive "
                + victoryDay.ToString(CultureInfo.InvariantCulture) + " days";
        }

        private static string BuildDialogue(ScenarioDefinition definition)
        {
            int actorBoundCharacters = 0;
            for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                if (definition.ScenarioCharacters[i] != null && definition.ScenarioCharacters[i].ActorRef != null)
                    actorBoundCharacters++;
            }

            int stageCount = definition.ScenarioFlow != null ? definition.ScenarioFlow.Stages.Count : 0;
            int responses = 0;
            bool recruit = false;
            for (int s = 0; definition.ScenarioFlow != null && s < definition.ScenarioFlow.Stages.Count; s++)
            {
                ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[s];
                for (int i = 0; stage != null && i < stage.IntercomStages.Count; i++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                    responses += intercom != null ? intercom.Options.Count : 0;
                    recruit = recruit || intercom != null && intercom.RecruitAsFamily && intercom.CharacterIdsToRecruit.Count > 0;
                }
            }

            return actorBoundCharacters.ToString(CultureInfo.InvariantCulture) + " actor-bound story character - "
                + stageCount.ToString(CultureInfo.InvariantCulture) + " story stages - "
                + responses.ToString(CultureInfo.InvariantCulture) + " response branches - payoff: "
                + (recruit ? "recruit" : "reward");
        }

        private static string BuildMap(ScenarioDefinition definition)
        {
            MapAuthoringDefinition map = definition.Map;
            int locations = map != null ? map.Locations.Count : 0;
            int hidden = 0;
            int searchable = 0;
            int deterministicTables = 0;
            int minDanger = int.MaxValue;
            int maxDanger = int.MinValue;
            for (int i = 0; map != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && location.HiddenUntilDiscovered)
                    hidden++;
                if (location != null && location.Searchable)
                    searchable++;
                if (location != null)
                {
                    minDanger = Math.Min(minDanger, location.Danger);
                    maxDanger = Math.Max(maxDanger, location.Danger);
                }
            }
            for (int i = 0; map != null && i < map.LootTables.Count; i++)
            {
                if (IsDeterministic(map.LootTables[i]))
                    deterministicTables++;
            }

            return locations.ToString(CultureInfo.InvariantCulture) + " map locations - "
                + deterministicTables.ToString(CultureInfo.InvariantCulture) + " deterministic loot tables - "
                + hidden.ToString(CultureInfo.InvariantCulture) + " hidden - "
                + searchable.ToString(CultureInfo.InvariantCulture) + " searchable - danger "
                + (minDanger == int.MaxValue ? "n/a" : minDanger.ToString(CultureInfo.InvariantCulture) + "-" + maxDanger.ToString(CultureInfo.InvariantCulture));
        }

        private static bool MatchesPreset(StartingInventoryDefinition inventory, string presetId)
        {
            List<ItemEntry> expected = ScenarioSuppliesPresetCatalog.BuildStacks(presetId);
            if (inventory == null || inventory.Items.Count != expected.Count)
                return false;
            for (int i = 0; i < expected.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < inventory.Items.Count; j++)
                {
                    ItemEntry actual = inventory.Items[j];
                    if (actual != null && string.Equals(actual.ItemId, expected[i].ItemId, StringComparison.OrdinalIgnoreCase) && actual.Quantity == expected[i].Quantity)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static int FindSurvivalVictoryDay(ScenarioDefinition definition)
        {
            for (int i = 0; definition.WinLossConditions != null && i < definition.WinLossConditions.WinConditions.Count; i++)
            {
                ScenarioConditionRef condition = definition.WinLossConditions.WinConditions[i];
                if (condition == null || condition.Kind != ScenarioConditionKind.SurviveDays)
                    continue;
                return condition.Quantity;
            }
            return 0;
        }

        private static bool IsDeterministic(MapLootTableDefinition table)
        {
            if (table == null || table.Entries.Count == 0)
                return false;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry == null || entry.MinQuantity != entry.MaxQuantity || Math.Abs(entry.Chance - 1f) > 0.0001f)
                    return false;
            }
            return true;
        }
    }
}

using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring.Supplies;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Application.Authoring.Templates
{
    /// <summary>
    /// Creates the canonical starter fixtures as neutral definitions. The bundled
    /// catalog serializes these through ScenarioDefinitionSerializer before use.
    /// </summary>
    internal static class ScenarioStarterTemplateBuilder
    {
        internal const string SmallSurvivalKey = "small-survival-challenge";
        internal const string DialogueStoryKey = "dialogue-story";
        internal const string ExpeditionMapKey = "expedition-map-scenario";

        internal static ScenarioDefinition BuildSmallSurvivalChallenge()
        {
            ScenarioDefinition definition = CreateDefinition(
                "Template.SmallSurvivalChallenge",
                "Small Survival Challenge",
                "Learn starting supplies, timeline events, and a clear victory condition in a compact ten-day survival run.");

            AddStartingSurvivor(definition, "Morgan", 101);
            AddStartingSurvivor(definition, "Riley", 102);
            AddInventory(definition.StartingInventory.Items, ScenarioSuppliesPresetCatalog.BuildStacks(ScenarioSuppliesPresetCatalog.PresetScarce));
            definition.StartingInventory.OverrideRandomStart = true;

            definition.StartingInventory.ScheduledChanges.Add(new TimedInventoryChangeDefinition
            {
                Id = "day-2-supply-delivery",
                ItemId = StableItemId(ItemManager.ItemType.Water),
                Quantity = 3,
                Kind = ScenarioInventoryChangeKind.Add,
                When = new ScenarioScheduleTime { Day = 2, Hour = 10, Minute = 0 }
            });
            definition.ScheduledActions.Add(CreateRaid("day-5-raid", 5, 18, 2));
            definition.ScheduledActions.Add(CreateWeather("day-7-rain", 7, 8, "Rain", 24));

            ConditionDef victory = new ConditionDef { Id = "survive-ten-days", Type = "surviveDays" };
            victory.Properties.Add(new ScenarioProperty { Key = "day", Value = "10" });
            victory.Properties.Add(new ScenarioProperty { Key = "hour", Value = "8" });
            victory.Properties.Add(new ScenarioProperty { Key = "minute", Value = "0" });
            definition.WinLossConditions.WinConditions.Add(victory);
            return definition;
        }

        internal static ScenarioDefinition BuildDialogueStory()
        {
            ScenarioDefinition definition = CreateDefinition(
                "Template.DialogueStory",
                "Dialogue Story",
                "Learn actor-bound story characters, branching intercom choices, stage routing, and a recruit payoff.");
            AddStartingSurvivor(definition, "Alex", 201);

            ScenarioNpcDefinition guide = new ScenarioNpcDefinition
            {
                CharacterId = "radio-guide",
                DisplayName = "Casey",
                ActorRef = Actor("ScenarioCharacter", "radio-guide", 301),
                PresetId = "Adult",
                Personality = "Friendly"
            };
            definition.ScenarioCharacters.Add(guide);

            ScenarioFlowStageDefinition opening = new ScenarioFlowStageDefinition { Id = "first-contact", UnansweredNextStage = "walk-away", UnansweredNextDays = 1 };
            opening.CharacterIds.Add(guide.CharacterId);
            ScenarioIntercomStageDefinition choice = new ScenarioIntercomStageDefinition { Id = "offer", Type = "Choice" };
            choice.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = guide.CharacterId, TextKey = "I found your signal. Do you have room for one more survivor?" });
            choice.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "Come to the shelter.", NextId = "accept" });
            choice.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "Leave the supplies and move on.", NextId = "decline" });
            opening.IntercomStages.Add(choice);
            opening.IntercomStages.Add(RouteToStage("accept", guide.CharacterId, "I will be there by morning.", "welcome"));
            opening.IntercomStages.Add(RouteToStage("decline", guide.CharacterId, "Understood. Stay safe out there.", "walk-away"));
            definition.ScenarioFlow.Stages.Add(opening);

            ScenarioFlowStageDefinition welcome = new ScenarioFlowStageDefinition { Id = "welcome", UnansweredNextStage = "welcome", UnansweredNextDays = 1 };
            welcome.CharacterIds.Add(guide.CharacterId);
            ScenarioIntercomStageDefinition recruit = new ScenarioIntercomStageDefinition { Id = "recruit", Type = "EndEncounter", RecruitAsFamily = true };
            recruit.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = guide.CharacterId, TextKey = "Thanks for taking a chance on me." });
            recruit.CharacterIdsToRecruit.Add(guide.CharacterId);
            recruit.EndOptions = new ScenarioEncounterEndOptionsDefinition { Type = "EnterRecruit", CompleteParentScenario = true };
            welcome.IntercomStages.Add(recruit);
            definition.ScenarioFlow.Stages.Add(welcome);

            ScenarioFlowStageDefinition walkAway = new ScenarioFlowStageDefinition { Id = "walk-away", UnansweredNextStage = "walk-away", UnansweredNextDays = 1 };
            walkAway.CharacterIds.Add(guide.CharacterId);
            ScenarioIntercomStageDefinition farewell = new ScenarioIntercomStageDefinition { Id = "farewell", Type = "EndEncounter" };
            farewell.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = guide.CharacterId, TextKey = "The channel goes quiet." });
            farewell.EndOptions = new ScenarioEncounterEndOptionsDefinition { Type = "NothingHappens", CompleteParentScenario = true };
            walkAway.IntercomStages.Add(farewell);
            definition.ScenarioFlow.Stages.Add(walkAway);
            return definition;
        }

        internal static ScenarioDefinition BuildExpeditionMapScenario()
        {
            ScenarioDefinition definition = CreateDefinition(
                "Template.ExpeditionMapScenario",
                "Expedition Map Scenario",
                "Learn map locations, discovery visibility, search danger, and deterministic authored loot.");
            AddStartingSurvivor(definition, "Sam", 401);

            definition.Map.Width = 20f;
            definition.Map.Height = 20f;
            definition.Map.StartLocationId = "roadside-store";
            definition.Map.Locations.Add(new MapLocationDefinition
            {
                Id = "roadside-store",
                DisplayName = "Roadside Store",
                Kind = "Shop",
                X = 5f,
                Y = 6f,
                GridX = 5,
                GridY = 6,
                Radius = 1f,
                Searchable = true,
                DiscoveredAtStart = true,
                VisibleAtStart = true,
                HiddenUntilDiscovered = false,
                LootTableId = "store-loot",
                ReplaceGeneratedLoot = true,
                Danger = 1
            });
            definition.Map.Locations.Add(new MapLocationDefinition
            {
                Id = "sealed-cache",
                DisplayName = "Sealed Cache",
                Kind = "Cache",
                X = 14f,
                Y = 12f,
                GridX = 14,
                GridY = 12,
                Radius = 1f,
                Searchable = true,
                DiscoveredAtStart = false,
                VisibleAtStart = false,
                HiddenUntilDiscovered = true,
                LootTableId = "cache-loot",
                ReplaceGeneratedLoot = true,
                Danger = 3
            });
            definition.Map.LootTables.Add(CreateDeterministicLootTable("store-loot", "Roadside Store Loot", StableItemId(ItemManager.ItemType.Water), 4));
            definition.Map.LootTables.Add(CreateDeterministicLootTable("cache-loot", "Sealed Cache Loot", StableItemId(ItemManager.ItemType.FirstAid), 2));
            return definition;
        }

        private static ScenarioDefinition CreateDefinition(string id, string title, string description)
        {
            ScenarioDefinition definition = new ScenarioDefinition
            {
                Id = id,
                DisplayName = title,
                Description = description,
                Author = "ShelteredAPI",
                Version = "1.0.0",
                BaseGameMode = ScenarioBaseGameMode.Survival,
                BaseFamilyChoice = ScenarioBaseFamilyChoices.KeepCurrentCast,
                SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(ScenarioBaseGameMode.Survival)
            };
            definition.FamilySetup.OverrideVanillaFamily = true;
            return definition;
        }

        private static void AddStartingSurvivor(ScenarioDefinition definition, string name, int actorId)
        {
            definition.FamilySetup.Members.Add(new FamilyMemberConfig
            {
                Name = name,
                Gender = ScenarioGender.Any,
                ExactAge = 28,
                ActorRef = Actor("FamilyMember", name, actorId)
            });
        }

        private static ScenarioActorRef Actor(string bindingType, string bindingKey, int localId)
        {
            return new ScenarioActorRef
            {
                Kind = "Scenario",
                LocalId = localId,
                Domain = "starter-template",
                BindingType = bindingType,
                BindingKey = bindingKey,
                DisplayNameFallback = bindingKey
            };
        }

        private static void AddInventory(List<ItemEntry> target, List<ItemEntry> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                target.Add(new ItemEntry { ItemId = source[i].ItemId, Quantity = source[i].Quantity });
        }

        private static ScenarioScheduledActionDefinition CreateRaid(string id, int day, int hour, int count)
        {
            ScenarioScheduledActionDefinition action = CreateAction(id, day, hour, ScenarioEffectKind.WorldEvent);
            ScenarioEffectDefinition effect = new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent, Quantity = 1 };
            effect.Properties.Add(new ScenarioProperty { Key = "eventType", Value = "Raid" });
            effect.Properties.Add(new ScenarioProperty { Key = "count", Value = count.ToString() });
            action.Effects.Add(effect);
            return action;
        }

        private static ScenarioScheduledActionDefinition CreateWeather(string id, int day, int hour, string weather, int durationHours)
        {
            ScenarioScheduledActionDefinition action = CreateAction(id, day, hour, ScenarioEffectKind.SetWeather);
            action.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.SetWeather, WeatherState = weather, DurationHours = durationHours });
            return action;
        }

        private static ScenarioScheduledActionDefinition CreateAction(string id, int day, int hour, ScenarioEffectKind kind)
        {
            return new ScenarioScheduledActionDefinition
            {
                Id = id,
                ActionType = kind.ToString(),
                DueTime = new ScenarioScheduleTime { Day = day, Hour = hour, Minute = 0 }
            };
        }

        private static ScenarioIntercomStageDefinition RouteToStage(string id, string characterId, string text, string stageId)
        {
            ScenarioIntercomStageDefinition route = new ScenarioIntercomStageDefinition
            {
                Id = id,
                Type = "EndEncounter",
                StageChange = new ScenarioStageChangeDefinition { Id = stageId, DelayDays = 1 }
            };
            route.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = characterId, TextKey = text });
            return route;
        }

        private static MapLootTableDefinition CreateDeterministicLootTable(string id, string title, string itemId, int quantity)
        {
            MapLootTableDefinition table = new MapLootTableDefinition { Id = id, DisplayName = title };
            table.Entries.Add(new MapLootEntryDefinition
            {
                ItemId = itemId,
                MinQuantity = quantity,
                MaxQuantity = quantity,
                Weight = 1,
                Chance = 1f
            });
            return table;
        }

        private static string StableItemId(ItemManager.ItemType itemType)
        {
            return ScenarioInventoryItemCatalog.GetStableItemId(itemType);
        }
    }
}

using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed class ScenarioHomeProgressFacts
    {
        public string WorldBadge { get; private set; }
        public string PeopleBadge { get; private set; }
        public string InventoryBadge { get; private set; }
        public string EventsBadge { get; private set; }
        public string ArtBadge { get; private set; }
        public string PlaytestBadge { get; private set; }
        public string PublishBadge { get; private set; }

        public static ScenarioHomeProgressFacts Build(ScenarioDefinition definition, ScenarioEditorSession editorSession)
        {
            int placedChanges = CountObjectPlacements(definition)
                + CountSceneSpritePlacements(definition)
                + CountStructuralChanges(definition);
            int survivors = CountStartingSurvivors(definition) + CountFutureSurvivors(definition);
            int items = CountStartingInventoryItems(definition) + CountScheduledInventoryItems(definition);
            int eventsAndStory = CountTimelineEvents(definition) + CountStoryStages(definition);
            int artChanges = CountSpriteSwaps(definition) + CountCustomSprites(definition);
            int dirtyFlags = Item.CountDirtyFlags(editorSession);

            return new ScenarioHomeProgressFacts
            {
                WorldBadge = FormatCount(placedChanges, "placed change", "placed changes"),
                PeopleBadge = FormatCount(survivors, "survivor", "survivors"),
                InventoryBadge = FormatCount(items, "item", "items"),
                EventsBadge = FormatCount(eventsAndStory, "event + story stage", "events + story stages"),
                ArtBadge = FormatCount(artChanges, "art change", "art changes"),
                PlaytestBadge = dirtyFlags == 0 ? "Saved" : "Unsaved changes",
                PublishBadge = "Review"
            };
        }

        private static int CountObjectPlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null
                ? definition.BunkerEdits.ObjectPlacements.Count
                : 0;
        }

        private static int CountSceneSpritePlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null
                ? definition.AssetReferences.SceneSpritePlacements.Count
                : 0;
        }

        private static int CountStructuralChanges(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerEdits != null && definition.BunkerEdits.RoomChanges != null
                ? definition.BunkerEdits.RoomChanges.Count
                : 0;
        }

        private static int CountStartingSurvivors(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null
                ? definition.FamilySetup.Members.Count
                : 0;
        }

        private static int CountFutureSurvivors(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null
                ? definition.FamilySetup.FutureSurvivors.Count
                : 0;
        }

        private static int CountStartingInventoryItems(ScenarioDefinition definition)
        {
            int total = 0;
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.Items != null && i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry item = definition.StartingInventory.Items[i];
                if (item != null)
                    total += item.Quantity;
            }

            return total;
        }

        private static int CountScheduledInventoryItems(ScenarioDefinition definition)
        {
            int total = 0;
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = definition.StartingInventory.ScheduledChanges[i];
                if (change != null)
                    total += change.Quantity;
            }

            return total;
        }

        private static int CountTimelineEvents(ScenarioDefinition definition)
        {
            int total = 0;
            if (definition != null && definition.TriggersAndEvents != null)
            {
                total += definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count : 0;
                total += definition.TriggersAndEvents.WeatherEvents != null ? definition.TriggersAndEvents.WeatherEvents.Count : 0;
            }

            total += definition != null && definition.ScheduledActions != null ? definition.ScheduledActions.Count : 0;
            total += definition != null && definition.Gates != null ? definition.Gates.Count : 0;
            return total;
        }

        private static int CountStoryStages(ScenarioDefinition definition)
        {
            return definition != null && definition.Quests != null && definition.Quests.Quests != null
                ? definition.Quests.Quests.Count
                : 0;
        }

        private static int CountSpriteSwaps(ScenarioDefinition definition)
        {
            return definition != null && definition.AssetReferences != null && definition.AssetReferences.SpriteSwaps != null
                ? definition.AssetReferences.SpriteSwaps.Count
                : 0;
        }

        private static int CountCustomSprites(ScenarioDefinition definition)
        {
            return definition != null && definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null
                ? definition.AssetReferences.CustomSprites.Count
                : 0;
        }

        private static string FormatCount(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}

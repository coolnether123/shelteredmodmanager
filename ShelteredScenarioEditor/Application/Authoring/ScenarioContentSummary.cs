using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Application.Authoring
{
    /// <summary>
    /// Neutral counts describing what an existing scenario definition contains.
    /// Used by the setup wizard to show what an installed-scenario copy includes
    /// before the author commits to copying it. Kept content-derived (no template
    /// specialization) so any definition - authored, installed, or template -
    /// produces the same shape.
    /// </summary>
    internal sealed class ScenarioContentSummary
    {
        public int WorldChanges { get; private set; }
        public int Cast { get; private set; }
        public int StoryStages { get; private set; }
        public int TimelineEntries { get; private set; }
        public int MapLocations { get; private set; }
        public int AssetFiles { get; private set; }
        public int RequiredMods { get; private set; }

        public static ScenarioContentSummary Build(ScenarioDefinition definition)
        {
            ScenarioContentSummary summary = new ScenarioContentSummary();
            if (definition == null)
                return summary;

            summary.WorldChanges = CountWorldChanges(definition);
            summary.Cast = CountCast(definition);
            summary.StoryStages = definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null
                ? definition.ScenarioFlow.Stages.Count
                : 0;
            summary.TimelineEntries = CountTimelineEntries(definition);
            summary.MapLocations = definition.Map != null && definition.Map.Locations != null
                ? definition.Map.Locations.Count
                : 0;
            summary.AssetFiles = CollectAssetFileCount(definition);
            summary.RequiredMods = CountRequiredMods(definition);
            return summary;
        }

        /// <summary>Compact single-line summary for the wizard base card.</summary>
        public string ToCardLine()
        {
            List<string> parts = new List<string>();
            parts.Add(Plural(WorldChanges, "world change", "world changes"));
            parts.Add(Plural(Cast, "cast member", "cast members"));
            parts.Add(Plural(StoryStages, "story stage", "story stages"));
            parts.Add(Plural(TimelineEntries, "timeline entry", "timeline entries"));
            parts.Add(Plural(MapLocations, "map location", "map locations"));
            parts.Add(Plural(AssetFiles, "asset file", "asset files"));
            parts.Add(Plural(RequiredMods, "required mod", "required mods"));
            return string.Join(" - ", parts.ToArray());
        }

        private static int CountWorldChanges(ScenarioDefinition definition)
        {
            int total = 0;
            if (definition.BunkerEdits != null)
            {
                total += definition.BunkerEdits.ObjectPlacements != null ? definition.BunkerEdits.ObjectPlacements.Count : 0;
                total += definition.BunkerEdits.RoomChanges != null ? definition.BunkerEdits.RoomChanges.Count : 0;
            }
            if (definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null)
                total += definition.AssetReferences.SceneSpritePlacements.Count;
            return total;
        }

        private static int CountCast(ScenarioDefinition definition)
        {
            int total = 0;
            if (definition.FamilySetup != null)
            {
                total += definition.FamilySetup.Members != null ? definition.FamilySetup.Members.Count : 0;
                total += definition.FamilySetup.FutureSurvivors != null ? definition.FamilySetup.FutureSurvivors.Count : 0;
            }
            total += definition.ScenarioCharacters != null ? definition.ScenarioCharacters.Count : 0;
            return total;
        }

        private static int CountTimelineEntries(ScenarioDefinition definition)
        {
            int total = 0;
            total += definition.ScheduledActions != null ? definition.ScheduledActions.Count : 0;
            total += definition.Gates != null ? definition.Gates.Count : 0;
            if (definition.TriggersAndEvents != null)
            {
                total += definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count : 0;
                total += definition.TriggersAndEvents.WeatherEvents != null ? definition.TriggersAndEvents.WeatherEvents.Count : 0;
            }
            if (definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null)
                total += definition.StartingInventory.ScheduledChanges.Count;
            return total;
        }

        private static int CollectAssetFileCount(ScenarioDefinition definition)
        {
            List<string> paths = ScenarioPackagePlanner.CollectAssetPaths(definition);
            return paths != null ? paths.Count : 0;
        }

        private static int CountRequiredMods(ScenarioDefinition definition)
        {
            int total = 0;
            for (int i = 0; definition.ModDependencies != null && i < definition.ModDependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                if (dependency != null && dependency.Kind == ScenarioModDependencyKind.Required)
                    total++;
            }
            return total;
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}

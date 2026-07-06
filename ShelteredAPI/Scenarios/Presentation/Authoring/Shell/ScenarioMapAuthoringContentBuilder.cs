using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Inspector;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioMapAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind
        {
            get { return ScenarioAuthoringWindowContentKind.Map; }
        }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            if (map == null)
                map = new MapAuthoringDefinition();

            return new[]
            {
                BuildRuntimeNoticeSection(),
                BuildSupportedActionsSection(),
                BuildOverviewSection(definition, map),
                BuildMarkerSection(map),
                BuildBoundaryTerrainSection(map),
                BuildLootSection(map),
                BuildEncounterSection(map),
                BuildRouteSection(map)
            };
        }

        private static ScenarioAuthoringInspectorSection BuildRuntimeNoticeSection()
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_runtime_notice",
                Title = "Map Authoring Status",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("Map authoring is not yet supported.", "This page is a read-only review of map-facing scenario data already present in the draft.", "Read-only", "MAP", null, true),
                    Property("Supported Here", "Review authored map locations, markers, boundaries, terrain, loot tables, encounters, and routes when those records exist."),
                    Property("Authoring Today", "World-map story events are authored in Story. Runtime shelter changes are authored in World, Timeline, Cast, Supplies, and Art."),
                    Property("Not Supported Yet", "Creating, moving, painting, or wiring map nodes from this page.")
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildSupportedActionsSection()
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_supported_actions",
                Title = "Where To Author Map-Facing Work",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests,
                        "Open Story",
                        "Author world-map encounters, stage routing, dialogue, rewards, and outcomes in Story.",
                        true,
                        true,
                        "ST")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicStory,
                        "Story Help",
                        "Open help for story and scenario-flow authoring.",
                        true,
                        false,
                        "HP")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicWorldCamera,
                        "World Help",
                        "Open help for the supported world authoring surfaces.",
                        true,
                        false,
                        "WH"))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildOverviewSection(ScenarioDefinition definition, MapAuthoringDefinition map)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_overview",
                Title = "Map Overview",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    Property("Scenario", Safe(definition != null ? definition.DisplayName : null)),
                    Property("Start Location", Safe(map.StartLocationId)),
                    Property("Map Size", FormatSize(map.Width, map.Height)),
                    Property("Default Terrain", Safe(map.DefaultTerrainId)),
                    Property("Locations", Count(map.Locations)),
                    Property("Markers", Count(map.Markers)),
                    Property("Boundaries", Count(map.Boundaries)),
                    Property("Terrain Patches", Count(map.TerrainPatches)),
                    Property("Loot Tables", Count(map.LootTables)),
                    Property("Encounter Tables", Count(map.EncounterTables)),
                    Property("Routes", Count(map.Routes))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildMarkerSection(MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; map.Locations != null && i < map.Locations.Count && items.Count < 8; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location == null)
                    continue;

                items.Add(Property(
                    Safe(location.Id),
                    FormatPoint(location.X, location.Y) + " / " + Safe(location.Kind) + " / " + FormatLocationTables(location)));
            }

            for (int i = 0; map.Markers != null && i < map.Markers.Count && items.Count < 14; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                if (marker == null)
                    continue;

                items.Add(Property(
                    Safe(marker.Id),
                    marker.Kind + " / " + FormatPoint(marker.X, marker.Y) + " / " + (marker.VisibleAtStart ? "Visible" : "Hidden")));
            }

            if (items.Count == 0)
                items.Add(Text("No map locations or key markers have been authored yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_markers",
                Title = "Locations And Markers",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildBoundaryTerrainSection(MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; map.Boundaries != null && i < map.Boundaries.Count && items.Count < 8; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                if (boundary == null)
                    continue;

                items.Add(Property(
                    Safe(boundary.Id),
                    boundary.Kind + " / " + FormatBoundaryShape(boundary) + " / " + FormatZoneTables(boundary)));
            }

            for (int i = 0; map.TerrainPatches != null && i < map.TerrainPatches.Count && items.Count < 14; i++)
            {
                MapTerrainPatchDefinition patch = map.TerrainPatches[i];
                if (patch == null)
                    continue;

                items.Add(Property(
                    Safe(patch.Id),
                    Safe(patch.TerrainId) + " / " + patch.Shape + " / " + FormatTerrainGeometry(patch)));
            }

            if (items.Count == 0)
                items.Add(Text("No map boundaries or terrain paint patches have been authored yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_boundaries",
                Title = "Boundaries And Terrain",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildLootSection(MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                if (table == null)
                    continue;

                items.Add(Property(Safe(table.Id), Count(table.Entries) + " loot entries"));
            }

            if (items.Count == 0)
                items.Add(Text("No expedition loot tables have been authored yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_loot",
                Title = "Loot Tables",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildEncounterSection(MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                if (table == null)
                    continue;

                items.Add(Property(Safe(table.Id), Count(table.Entries) + " encounter entries"));
            }

            if (items.Count == 0)
                items.Add(Text("No encounter tables have been authored yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_encounters",
                Title = "Encounter Tables",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildRouteSection(MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; map.Routes != null && i < map.Routes.Count; i++)
            {
                ExpeditionRouteDefinition route = map.Routes[i];
                if (route == null)
                    continue;

                string direction = Safe(route.FromLocationId) + (route.OneWay ? " -> " : " <-> ") + Safe(route.ToLocationId);
                items.Add(Property(Safe(route.Id), direction + " / risk " + route.Risk.ToString(CultureInfo.InvariantCulture)));
            }

            if (items.Count == 0)
                items.Add(Text("No expedition routes have been authored yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_routes",
                Title = "Expedition Routes",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static string FormatLocationTables(MapLocationDefinition location)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(location.TerrainId))
                parts.Add("terrain " + location.TerrainId);
            if (!string.IsNullOrEmpty(location.LootTableId))
                parts.Add("loot " + location.LootTableId);
            if (!string.IsNullOrEmpty(location.EncounterTableId))
                parts.Add("encounters " + location.EncounterTableId);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no tables";
        }

        private static string FormatZoneTables(MapBoundaryDefinition boundary)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(boundary.TerrainId))
                parts.Add("terrain " + boundary.TerrainId);
            if (!string.IsNullOrEmpty(boundary.LootTableId))
                parts.Add("loot " + boundary.LootTableId);
            if (!string.IsNullOrEmpty(boundary.EncounterTableId))
                parts.Add("encounters " + boundary.EncounterTableId);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no tables";
        }

        private static string FormatBoundaryShape(MapBoundaryDefinition boundary)
        {
            if (boundary.MinX.HasValue && boundary.MinY.HasValue && boundary.MaxX.HasValue && boundary.MaxY.HasValue)
                return FormatPoint(boundary.MinX.Value, boundary.MinY.Value) + " to " + FormatPoint(boundary.MaxX.Value, boundary.MaxY.Value);
            return Count(boundary.Points) + " polygon points";
        }

        private static string FormatTerrainGeometry(MapTerrainPatchDefinition patch)
        {
            if (patch.Shape == MapTerrainBrushShape.Circle)
                return FormatPoint(patch.X, patch.Y) + " radius " + patch.Radius.ToString("0.##", CultureInfo.InvariantCulture);
            if (patch.Shape == MapTerrainBrushShape.Polygon)
                return Count(patch.Points) + " polygon points";
            return FormatPoint(patch.X, patch.Y) + " size " + FormatSize(patch.Width, patch.Height);
        }

        private static ScenarioAuthoringInspectorItem Text(string value)
        {
            return ScenarioInspectorItemFactory.Text(value);
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return ScenarioInspectorItemFactory.Property(label, value);
        }

        private static string Safe(string value)
        {
            return ScenarioInspectorItemFactory.Safe(value);
        }

        private static string Count<T>(List<T> values)
        {
            return (values != null ? values.Count : 0).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatPoint(float x, float y)
        {
            return x.ToString("0.##", CultureInfo.InvariantCulture) + "," + y.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatSize(float width, float height)
        {
            return width.ToString("0.##", CultureInfo.InvariantCulture) + "x" + height.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

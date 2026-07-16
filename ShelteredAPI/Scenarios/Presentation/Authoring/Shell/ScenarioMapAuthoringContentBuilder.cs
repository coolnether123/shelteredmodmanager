using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Map;
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
                BuildRuntimeNoticeSection(context != null ? context.State : null),
                BuildGenerationSection(definition),
                BuildSelectionSection(context != null ? context.State : null),
                BuildSupportedActionsSection(context != null ? context.State : null),
                BuildBrushOptionsSection(context != null ? context.State : null),
                ScenarioMapLocationEditorSectionBuilder.Build(context != null ? context.State : null, definition, map),
                BuildOverviewSection(definition, map),
                BuildMarkerSection(map),
                BuildBoundaryTerrainSection(map),
                BuildLootSection(map),
                BuildEncounterSection(map),
                BuildRouteSection(map)
            };
        }

        private static ScenarioAuthoringInspectorSection BuildRuntimeNoticeSection(ScenarioAuthoringState state)
        {
            bool active = state != null && state.MapAuthoringActive;
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_runtime_notice",
                Title = "Map Authoring Status",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text(
                        active ? "Real map authoring is active." : "Open the real Sheltered map to select vanilla regions.",
                        active ? "The editor chrome is hidden while the vanilla map panel owns input. Escape or the map close button returns here." : "Use Open Map, or right-click the shelter radio, to edit the real expedition map.",
                        active ? "Active" : "Ready",
                        "MAP",
                        null,
                        true),
                    Property("Supported Here", "Open the vanilla map, select towns/regions, place POI assets, and paint tree, mountain, or clear terrain cells."),
                    Property("Selection Mode", active ? "MapAuthoringActive" : "Map workshop"),
                    Property("Map Click Mode", state != null && !string.IsNullOrEmpty(state.MapAuthoringMode) ? state.MapAuthoringMode : "select"),
                    Property("Movement", "Click-to-move is used because vanilla mouse drag already pans the map and drags waypoints.")
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildGenerationSection(ScenarioDefinition definition)
        {
            bool fixedSeed = definition != null && definition.SeedOverride.HasValue;
            string seed = fixedSeed
                ? definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture)
                : "Vanilla / any generated map";
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_generation",
                Title = "Map Generation",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Property("Generation", seed),
                    Text("Generation choices take effect the next time the scenario world is loaded. Manual terrain and POI edits are layered over that generated base map."),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionScenarioSeedRandom,
                        "Render Normally",
                        "Do not pin a seed; let Sheltered render its normal generated map for each new scenario.",
                        definition != null,
                        !fixedSeed,
                        "VN")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionScenarioSeedFixed,
                        "Keep Seed",
                        "Pin a generated seed so this map layout can be reproduced.",
                        definition != null,
                        fixedSeed,
                        "FX")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionScenarioSeedReroll,
                        "Randomize Map",
                        "Choose a new fixed scenario seed. Reload or start Playtest to render the new map.",
                        definition != null,
                        false,
                        "RR"))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (selection == null)
            {
                items.Add(Text("No vanilla map region is selected yet."));
            }
            else
            {
                items.Add(Property(selection.Authored ? "Authored Location" : "Vanilla Region", Safe(selection.DisplayName)));
                items.Add(Property("Grid", selection.GridX.ToString(CultureInfo.InvariantCulture) + "," + selection.GridY.ToString(CultureInfo.InvariantCulture)));
                items.Add(Property("Kind", Safe(selection.Topography) + " / " + Safe(selection.Category)));
                items.Add(Property("Icon", Safe(selection.IconId)));
                items.Add(Property("Flags", FormatSelectionFlags(selection)));
                items.Add(Property("Loot", FormatSelectionLoot(selection)));
                items.Add(Property("Encounter", "open " + selection.OpenGroundEncounterChance.ToString(CultureInfo.InvariantCulture) + "% / faction " + selection.OpenGroundFactionEncounterChance.ToString(CultureInfo.InvariantCulture) + "% / animal " + selection.AnimalEncounterChance.ToString(CultureInfo.InvariantCulture) + "%"));
                items.Add(Property("Selection Kind", selection.Authored ? "Authored draft location" : "Vanilla map region"));
                items.Add(Property("State", selection.Authored ? "Authored" : "Vanilla - edits will be saved to your scenario"));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_selection",
                Title = "Selected Vanilla Region",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildSupportedActionsSection(ScenarioAuthoringState state)
        {
            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            bool hasSelection = selection != null;
            bool mapActive = state != null && state.MapAuthoringActive;
            bool authoredSelected = hasSelection && selection.Authored;
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_supported_actions",
                Title = "Map Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        mapActive ? ScenarioAuthoringActionIds.ActionMapAuthoringClose : ScenarioAuthoringActionIds.ActionMapAuthoringOpen,
                        mapActive ? "Close Map" : "Open Map",
                        mapActive ? "Close the vanilla map and return to this workshop page." : "Open the real Sheltered map for region selection.",
                        true,
                        true,
                        mapActive ? "CL" : "MP")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeSelect,
                        "Select",
                        "Map clicks select authored locations first, then vanilla regions.",
                        true,
                        state == null || string.IsNullOrEmpty(state.MapAuthoringMode) || state.MapAuthoringMode == "select",
                        "SL")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModePlace,
                        "Place POI Asset",
                        "The next map click creates an authored point-of-interest asset on an available generated region.",
                        true,
                        state != null && state.MapAuthoringMode == "place",
                        "PL")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeMove,
                        "Move",
                        "The next map click relocates the selected authored location.",
                        authoredSelected,
                        state != null && state.MapAuthoringMode == "move",
                        "MV",
                        null,
                        null,
                        null,
                        authoredSelected ? null : "Select an authored location first.")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainTrees,
                        "Paint Trees",
                        "Paint Woodland across the active brush footprint.",
                        true,
                        state != null && state.MapAuthoringMode == "terrain:Woodland",
                        "TR")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainMountains,
                        "Paint Mountains",
                        "Paint Mountains across the active brush footprint.",
                        true,
                        state != null && state.MapAuthoringMode == "terrain:Mountains",
                        "MT")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainClear,
                        "Clear Terrain",
                        "Clear terrain across the active brush footprint.",
                        true,
                        state != null && state.MapAuthoringMode == "terrain:NowhereSpecial",
                        "ER")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainGeneratedBlend,
                        "Generated Blend",
                        "Mark an area for deterministic procedural terrain that favors the generated base and adjoining manual terrain while preserving existing POIs.",
                        true,
                        state != null && state.MapAuthoringMode == "terrain:" + ScenarioMapTerrainModes.GeneratedBlend,
                        "GB")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicMap,
                        "Map Help",
                        "Open a concrete guide for map-facing data and required source pages.",
                        true,
                        false,
                        "HP")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests,
                        "Open Story",
                        "Author quest and encounter flow connected to map-facing scenario work.",
                        true,
                        false,
                        "ST"))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildBrushOptionsSection(ScenarioAuthoringState state)
        {
            int size = state != null && state.MapTerrainBrushSize > 0 ? state.MapTerrainBrushSize : 3;
            string shape = state != null && !string.IsNullOrEmpty(state.MapTerrainBrushShape) ? state.MapTerrainBrushShape : "circle";
            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_terrain_brush",
                Title = "Terrain Paintbrush",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Property("Brush", shape + " / " + size.ToString(CultureInfo.InvariantCulture) + " cells"),
                    Text("Choose Trees, Mountains, Clear, or Generated Blend, then click the map to paint the marked area. Later brush strokes win where areas overlap."),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeCircle, "Round", "Use a round terrain brush.", true, shape == "circle", "O")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeSquare, "Square", "Use a square terrain brush.", true, shape == "square", "[]")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize1, "1 x 1", "Paint one map cell.", true, size == 1, "1")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize3, "3 x 3", "Paint a small area.", true, size == 3, "3")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize5, "5 x 5", "Paint a medium area.", true, size == 5, "5")),
                    ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize7, "7 x 7", "Paint a large area.", true, size == 7, "7"))
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
                parts.Add((location.ReplaceGeneratedLoot ? "replace loot " : "loot ") + location.LootTableId);
            if (!string.IsNullOrEmpty(location.EncounterTableId))
                parts.Add("encounters " + location.EncounterTableId);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no tables";
        }

        private static string FormatSelectionFlags(ScenarioMapRegionSelection selection)
        {
            List<string> parts = new List<string>();
            parts.Add(selection.Searchable ? "searchable" : "not searchable");
            parts.Add(selection.VisibleOnMap ? "visible" : "hidden marker");
            parts.Add(selection.Discovered ? "discovered" : "undiscovered");
            if (selection.HiddenUntilDiscovered)
                parts.Add("hidden until discovered");
            return string.Join(", ", parts.ToArray());
        }

        private static string FormatSelectionLoot(ScenarioMapRegionSelection selection)
        {
            List<string> parts = new List<string>();
            parts.Add(selection.HasItems ? "has items" : "no generated items");
            if (selection.HasHiddenItems)
                parts.Add("hidden items");
            parts.Add(selection.LocationSpecificLootTypeCount.ToString(CultureInfo.InvariantCulture) + " location loot types");
            parts.Add("max " + selection.MaxItems.ToString(CultureInfo.InvariantCulture));
            return string.Join(", ", parts.ToArray());
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

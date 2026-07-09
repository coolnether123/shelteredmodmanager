using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal static class ScenarioMapLocationEditorSectionBuilder
    {
        public static ScenarioAuthoringInspectorSection Build(ScenarioAuthoringState state, ScenarioDefinition definition, MapAuthoringDefinition map)
        {
            MapLocationDefinition location = ResolveSelectedLocation(state, map);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (location == null)
            {
                items.Add(ScenarioInspectorItemFactory.Text("Select a map region or authored location to edit map stats, loot, icon, and visibility."));
            }
            else
            {
                bool authored = state != null && state.MapSelection != null && state.MapSelection.Authored;
                items.Add(ScenarioInspectorItemFactory.Property("State", authored ? "Authored" : "Vanilla - edits will be saved to your scenario"));
                items.Add(ScenarioInspectorItemFactory.Property("Core map fields", "Applies in game", "Name, kind, icon, searchable, visibility, discovery, and danger are projected onto MapRegion."));
                items.Add(Editable("Name", location.DisplayName, "displayName", location.Id, "Shown on the expedition map."));
                items.Add(Editable("Kind / Category", location.Kind, "kind", location.Id, "Maps to the vanilla map category and a known topography when possible."));
                items.Add(Editable("Icon Id", location.IconId, "iconId", location.Id, "Must match a known map icon sprite id."));
                items.Add(Action(ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix, location.Id),
                    "Next Icon",
                    "Cycle through the known map icon sprite ids.",
                    true,
                    false,
                    "IC")));
                items.Add(ScenarioInspectorItemFactory.Property("Known Icons", FormatKnownIconIds()));
                items.Add(Action(Toggle(location, "searchable", "Searchable", location.Searchable)));
                items.Add(Editable("Danger", location.Danger.ToString(CultureInfo.InvariantCulture), "danger", location.Id, "Fallback open-ground encounter chance."));
                items.Add(Action(Toggle(location, "visibleAtStart", "Visible At Start", location.VisibleAtStart)));
                items.Add(Action(Toggle(location, "discoveredAtStart", "Discovered At Start", location.DiscoveredAtStart)));
                items.Add(Action(Toggle(location, "hiddenUntilDiscovered", "Hidden Until Discovery", location.HiddenUntilDiscovered)));

                items.Add(ScenarioInspectorItemFactory.Property("Loot settings", "Applies in game", "The preview below uses the same deterministic planner as runtime projection."));
                items.Add(Editable("Loot Table", location.LootTableId, "lootTableId", location.Id, "References an authored loot table id."));
                items.Add(Action(Toggle(location, "replaceGeneratedLoot", "Replace Generated Loot", location.ReplaceGeneratedLoot)));
                AddLootPreview(items, definition, location);

                items.Add(Editable("Encounter Table", location.EncounterTableId, "encounterTableId", location.Id, "References an authored encounter table id."));
                AddProjectionStatus(items);

                if (authored)
                {
                    items.Add(Action(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix + ScenarioAuthoringActionCodec.EncodeToken(location.Id),
                        "Duplicate to New Cell",
                        "Copies this location, then waits for a different target cell. No copy is created in place.",
                        true,
                        false,
                        "CP")));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "map_location_editor",
                Title = "Authored Location Editor",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        internal static void AddLootPreview(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, MapLocationDefinition location)
        {
            ScenarioMapLootPreview preview = ScenarioMapLootPreviewService.Build(
                definition,
                location,
                ModRandom.CurrentSeed,
                ScenarioMapLootPreviewService.DefaultSimulationRolls);
            if (!string.IsNullOrEmpty(preview.Error))
            {
                items.Add(ScenarioInspectorItemFactory.Property("Loot roll preview", preview.Error));
                return;
            }

            items.Add(ScenarioInspectorItemFactory.Property(
                "Exact roll (seed " + preview.FixedSeed.ToString(CultureInfo.InvariantCulture) + ")",
                FormatExact(preview.ExactRoll)));
            if (preview.Distribution.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Property("1,000-roll distribution", "No items rolled"));
            for (int i = 0; i < preview.Distribution.Count; i++)
            {
                ScenarioMapLootDistributionEntry entry = preview.Distribution[i];
                items.Add(ScenarioInspectorItemFactory.Property(
                    (entry.Hidden ? "Hidden " : string.Empty) + entry.ItemId,
                    entry.PercentOfRolls.ToString("0.0", CultureInfo.InvariantCulture) + "% of rolls; avg "
                        + entry.AverageQuantityPerRoll.ToString("0.00", CultureInfo.InvariantCulture) + " per roll"));
            }
        }

        private static void AddProjectionStatus(List<ScenarioAuthoringInspectorItem> items)
        {
            ScenarioMapProjectionField[] fields = ScenarioMapProjectionFieldCatalog.GetEncounterFields();
            Dictionary<string, List<string>> groupedFields = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Dictionary<string, string> statuses = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i++)
            {
                ScenarioMapProjectionField field = fields[i];
                List<string> names;
                if (!groupedFields.TryGetValue(field.Group, out names))
                {
                    names = new List<string>();
                    groupedFields[field.Group] = names;
                    statuses[field.Group] = field.StatusText;
                }
                names.Add(field.Field);
            }
            foreach (KeyValuePair<string, List<string>> group in groupedFields)
                items.Add(ScenarioInspectorItemFactory.Property(group.Key, statuses[group.Key], string.Join(", ", group.Value.ToArray())));
        }

        private static string FormatExact(List<MapLootProjectionEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return "Nothing";
            List<string> parts = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                MapLootProjectionEntry entry = entries[i];
                if (entry != null)
                    parts.Add((entry.Hidden ? "hidden " : string.Empty) + entry.ItemId + " x" + entry.Quantity.ToString(CultureInfo.InvariantCulture));
            }
            return parts.Count == 0 ? "Nothing" : string.Join(", ", parts.ToArray());
        }

        private static MapLocationDefinition ResolveSelectedLocation(ScenarioAuthoringState state, MapAuthoringDefinition map)
        {
            string id = state != null ? state.MapSelectedLocationId : null;
            if (string.IsNullOrEmpty(id) && state != null && state.MapSelection != null)
                id = !string.IsNullOrEmpty(state.MapSelection.LocationId) ? state.MapSelection.LocationId : state.MapSelection.CapturedLocationId;
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.Id, id, StringComparison.OrdinalIgnoreCase))
                    return location;
            }
            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            if (selection == null || selection.Authored || string.IsNullOrEmpty(id))
                return null;
            return new MapLocationDefinition
            {
                Id = id,
                DisplayName = selection.DisplayName,
                Kind = !string.IsNullOrEmpty(selection.Topography) ? selection.Topography : selection.Category,
                IconId = selection.IconId,
                X = selection.GridX,
                Y = selection.GridY,
                GridX = selection.GridX,
                GridY = selection.GridY,
                Searchable = selection.Searchable,
                VisibleAtStart = selection.VisibleOnMap,
                DiscoveredAtStart = selection.Discovered,
                HiddenUntilDiscovered = selection.HiddenUntilDiscovered,
                Danger = selection.OpenGroundEncounterChance,
                ReplaceGeneratedLoot = selection.ReplaceGeneratedLoot
            };
        }

        private static ScenarioAuthoringInspectorItem Editable(string label, string value, string field, string id, string hint)
        {
            ScenarioAuthoringInspectorItem item = ScenarioInspectorItemFactory.Property(label, value ?? string.Empty, hint);
            item.Editable = true;
            item.Action = ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionMapLocationEditPrefix + field + "." + ScenarioAuthoringActionCodec.EncodeToken(id) + ".",
                label,
                hint,
                true,
                false,
                "ED");
            return item;
        }

        private static string FormatKnownIconIds()
        {
            string[] icons = ScenarioMapIconCatalog.GetKnownIconIds();
            int count = icons != null ? icons.Length : 0;
            int limit = count > 10 ? 10 : count;
            List<string> labels = new List<string>();
            for (int i = 0; i < limit; i++) labels.Add(icons[i]);
            if (count > limit) labels.Add("+" + (count - limit).ToString(CultureInfo.InvariantCulture) + " more");
            return labels.Count > 0 ? string.Join(", ", labels.ToArray()) : "<none>";
        }

        private static ScenarioAuthoringInspectorAction Toggle(MapLocationDefinition location, string field, string label, bool value)
        {
            return ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix + field + "." + ScenarioAuthoringActionCodec.EncodeToken(location.Id),
                label,
                "Toggle " + label + " for this map location.",
                true,
                value,
                value ? "ON" : "OFF");
        }

        private static ScenarioAuthoringInspectorItem Action(ScenarioAuthoringInspectorAction action)
        {
            return ScenarioInspectorItemFactory.ActionItem(action);
        }
    }
}

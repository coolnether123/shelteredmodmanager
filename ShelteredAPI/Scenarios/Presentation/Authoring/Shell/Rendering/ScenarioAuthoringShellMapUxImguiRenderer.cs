using System;
using System.Globalization;
using System.Text;

using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private string _mapLootPreviewKey;
        private ScenarioMapLootPreview _mapLootPreview;

        private float DrawMapAuthoringLegend(Rect inner, float y)
        {
            GUI.Label(new Rect(inner.x, y, inner.width, 20f), "Legend + filters", _smallTitleStyle);
            y += 23f;
            y = DrawMapFilterChip(inner, y, ScenarioMapAuthoringFilter.VanillaRegions, "Vanilla regions", "VN");
            y = DrawMapFilterChip(inner, y, ScenarioMapAuthoringFilter.AuthoredLocations, "Authored locations", "AU");
            y = DrawMapFilterChip(inner, y, ScenarioMapAuthoringFilter.HiddenUntilDiscovered, "Hidden until found", "HD");
            y = DrawMapFilterChip(inner, y, ScenarioMapAuthoringFilter.InvalidOrBlocked, "Invalid / blocked", "!!");
            y = DrawMapFilterChip(inner, y, ScenarioMapAuthoringFilter.DependencyLocked, "Dependency locked", "LK");
            GUI.Label(new Rect(inner.x, y + 3f, inner.width, 34f), "Off filters dim matching markers.", _mutedTextStyle);
            return y + 37f;
        }

        private float DrawMapFilterChip(Rect inner, float y, ScenarioMapAuthoringFilter filter, string label, string glyph)
        {
            bool visible = ScenarioMapAuthoringFilterState.IsVisible(filter);
            Rect rect = new Rect(inner.x, y, inner.width, 27f);
            GUIStyle style = visible ? _activeButtonStyle : _buttonStyle;
            if (GUI.Button(rect, new GUIContent(glyph + "  " + label, "Toggle " + label + " markers."), style))
                ScenarioMapAuthoringFilterState.Toggle(filter);
            return y + 31f;
        }

        private void DrawMapUxSelectionDetails(ScenarioMapRegionSelection selection, ScenarioAuthoringState state)
        {
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            MapLocationDefinition location = FindSelectedMapLocation(definition, selection);
            GUILayout.Space(8f);
            GUILayout.Label("Playtest effect", _smallTitleStyle);
            GUILayout.Label("Core map fields and loot: Applies in game", _mutedTextStyle);
            DrawEncounterProjectionHonesty();

            if (location == null)
                return;

            DrawOverlayLootPreview(definition, location);
            if (selection.Authored)
            {
                GUILayout.Space(6f);
                DrawButton(GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true), GUILayout.Height(30f)), new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix + ScenarioAuthoringActionCodec.EncodeToken(location.Id),
                    Label = "Duplicate to New Cell",
                    Hint = "Copy loot, encounter, and properties, then choose a different valid cell.",
                    Enabled = true,
                    IconText = "CP"
                }, false);
            }
        }

        private void DrawEncounterProjectionHonesty()
        {
            ScenarioMapProjectionField[] fields = ScenarioMapProjectionFieldCatalog.GetEncounterFields();
            string previousGroup = null;
            for (int i = 0; i < fields.Length; i++)
            {
                ScenarioMapProjectionField field = fields[i];
                if (field == null || string.Equals(previousGroup, field.Group, StringComparison.Ordinal))
                    continue;
                previousGroup = field.Group;
                GUILayout.Label(field.Group + ": " + field.StatusText, _mutedTextStyle);
            }
        }

        private void DrawOverlayLootPreview(ScenarioDefinition definition, MapLocationDefinition location)
        {
            string key = BuildLootPreviewKey(definition, location);
            if (!string.Equals(key, _mapLootPreviewKey, StringComparison.Ordinal))
            {
                _mapLootPreviewKey = key;
                _mapLootPreview = ScenarioMapLootPreviewService.Build(definition, location, ModRandom.CurrentSeed, ScenarioMapLootPreviewService.DefaultSimulationRolls);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Loot roll preview", _smallTitleStyle);
            if (_mapLootPreview == null || !string.IsNullOrEmpty(_mapLootPreview.Error))
            {
                GUILayout.Label(_mapLootPreview != null ? _mapLootPreview.Error : "Preview unavailable.", _mutedTextStyle);
                return;
            }
            GUILayout.Label("Exact seed " + _mapLootPreview.FixedSeed.ToString(CultureInfo.InvariantCulture) + ": " + FormatOverlayExactRoll(_mapLootPreview), _textStyle);
            GUILayout.Label("Across 1,000 deterministic samples", _mutedTextStyle);
            for (int i = 0; i < _mapLootPreview.Distribution.Count; i++)
            {
                ScenarioMapLootDistributionEntry entry = _mapLootPreview.Distribution[i];
                GUILayout.Label(
                    (entry.Hidden ? "Hidden " : string.Empty) + entry.ItemId + " — "
                    + entry.PercentOfRolls.ToString("0.0", CultureInfo.InvariantCulture) + "% • avg "
                    + entry.AverageQuantityPerRoll.ToString("0.00", CultureInfo.InvariantCulture),
                    _mutedTextStyle);
            }
        }

        private static MapLocationDefinition FindSelectedMapLocation(ScenarioDefinition definition, ScenarioMapRegionSelection selection)
        {
            string id = selection != null ? (!string.IsNullOrEmpty(selection.LocationId) ? selection.LocationId : selection.CapturedLocationId) : null;
            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.Id, id, StringComparison.OrdinalIgnoreCase))
                    return location;
            }
            return null;
        }

        private static string FormatOverlayExactRoll(ScenarioMapLootPreview preview)
        {
            if (preview.ExactRoll.Count == 0)
                return "Nothing";
            StringBuilder value = new StringBuilder();
            for (int i = 0; i < preview.ExactRoll.Count; i++)
            {
                MapLootProjectionEntry item = preview.ExactRoll[i];
                if (item == null) continue;
                if (value.Length > 0) value.Append(", ");
                if (item.Hidden) value.Append("hidden ");
                value.Append(item.ItemId).Append(" x").Append(item.Quantity.ToString(CultureInfo.InvariantCulture));
            }
            return value.Length == 0 ? "Nothing" : value.ToString();
        }

        private static string BuildLootPreviewKey(ScenarioDefinition definition, MapLocationDefinition location)
        {
            StringBuilder key = new StringBuilder();
            key.Append(ModRandom.CurrentSeed).Append('|').Append(location != null ? location.Id : null).Append('|').Append(location != null ? location.LootTableId : null);
            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            for (int i = 0; map != null && map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                if (table == null || location == null || !string.Equals(table.Id, location.LootTableId, StringComparison.OrdinalIgnoreCase)) continue;
                for (int entryIndex = 0; table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapLootEntryDefinition entry = table.Entries[entryIndex];
                    if (entry != null)
                        key.Append('|').Append(entry.ItemId).Append(':').Append(entry.MinQuantity).Append(':').Append(entry.MaxQuantity).Append(':').Append(entry.Weight).Append(':').Append(entry.Chance).Append(':').Append(entry.Hidden);
                }
            }
            return key.ToString();
        }
    }
}

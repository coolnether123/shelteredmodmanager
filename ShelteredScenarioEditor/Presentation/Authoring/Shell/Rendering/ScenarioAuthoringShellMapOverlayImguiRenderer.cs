using System;
using System.Globalization;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Map;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private string _mapOverlayDropdownId;

        private void DrawMapAuthoringOverlayCore(
            float scaledWidth,
            float scaledHeight,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            Rect leftDock = BuildMapAuthoringLeftDockRect(scaledWidth, scaledHeight);
            Rect rightDock = BuildMapAuthoringRightDockRect(scaledWidth, scaledHeight);
            RegisterVisualSurface("map.authoring.left_dock", leftDock);
            RegisterVisualSurface("map.authoring.right_dock", rightDock);
            using (EnterVisualSurface("map.authoring.left_dock"))
                DrawMapAuthoringModeDock(leftDock, state, selection);
            using (EnterVisualSurface("map.authoring.right_dock"))
                DrawMapAuthoringSelectionDock(rightDock, state, selection);
            if (inputCapture != null)
            {
                inputCapture.RegisterInteractiveRect(leftDock);
                inputCapture.RegisterInteractiveRect(rightDock);
            }
        }

        private Rect BuildMapAuthoringLeftDockRect(float scaledWidth, float scaledHeight)
        {
            float width = scaledWidth < 1100f ? 160f : 184f;
            float height = Mathf.Max(280f, scaledHeight - (Margin * 2f));
            return new Rect(Margin, Margin, width, height);
        }

        private Rect BuildMapAuthoringRightDockRect(float scaledWidth, float scaledHeight)
        {
            // Keep tools in the narrow strip beside Sheltered's map frame. The
            // previous inspector-width dock intruded into the vanilla map canvas.
            float width = scaledWidth < 1400f ? 224f : 248f;
            float height = Mathf.Max(280f, scaledHeight - (Margin * 2f));
            return new Rect(scaledWidth - width - Margin, Margin, width, height);
        }

        private void DrawMapAuthoringModeDock(Rect rect, ScenarioAuthoringState state, ScenarioMapRegionSelection selection)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, rect.height - 20f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Map", _smallTitleStyle);

            float y = inner.y + 34f;
            y = DrawModeTab(inner.x, y, inner.width, "Select", "SL", MapAuthoringCommand.SetMode(MapAuthoringModeKind.Select, null), state == null || string.IsNullOrEmpty(state.MapAuthoringMode) || state.MapAuthoringMode == "select", true);
            y = DrawModeTab(inner.x, y + 4f, inner.width, "Add Location", "PL", MapAuthoringCommand.SetMode(MapAuthoringModeKind.Place, null), state != null && state.MapAuthoringMode == "place", true);
            bool canMove = selection != null && selection.Authored;
            y = DrawModeTab(inner.x, y + 4f, inner.width, "Move", "MV", MapAuthoringCommand.SetMode(MapAuthoringModeKind.Move, null), state != null && state.MapAuthoringMode == "move", canMove);
            DrawMapAuthoringLegend(inner, y + 16f);

            Rect closeRect = new Rect(inner.x, inner.yMax - 30f, inner.width, 28f);
            MapAuthoringCommand closeCommand = MapAuthoringCommand.CloseMap();
            DrawButton(closeRect, new ScenarioAuthoringInspectorAction
            {
                Id = closeCommand.AutomationId,
                Command = closeCommand,
                Label = "Close",
                Hint = "Close the map and return to the scenario editor.",
                Enabled = true,
                IconText = "CL"
            }, false);
        }

        private float DrawModeTab(float x, float y, float width, string label, string icon, MapAuthoringCommand command, bool active, bool enabled)
        {
            DrawButton(new Rect(x, y, width, 32f), new ScenarioAuthoringInspectorAction
            {
                Id = command.AutomationId,
                Command = command,
                Label = label,
                Hint = "Set map authoring mode to " + label + ".",
                Enabled = enabled,
                Emphasized = active,
                IconText = icon,
                DisabledReason = enabled ? null : "Select one of your custom locations before moving it."
            }, false);
            return y + 32f;
        }

        private void DrawMapAuthoringSelectionDock(Rect rect, ScenarioAuthoringState state, ScenarioMapRegionSelection selection)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Map Tools", _smallTitleStyle);

            Rect body = new Rect(inner.x, inner.y + 30f, inner.width, inner.height - 30f);
            GUILayout.BeginArea(body);
            Vector2 scroll = GetWindowScrollPosition("map.authoring.right_dock");
            RegisterScrollRegion("map.authoring.right_dock", body);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, body.width - 18f);
            DrawMapBrushControls(state);
            if (selection == null)
                DrawMapAuthoringEmptySelection(state);
            else
                DrawMapAuthoringSelectionEditor(selection, state);
            DrawMapGenerationControls();
            _activeContentWidth = previousContentWidth;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition("map.authoring.right_dock", scroll);
        }

        private void DrawMapBrushControls(ScenarioAuthoringState state)
        {
            int size = state != null && state.MapTerrainBrushSize > 0 ? state.MapTerrainBrushSize : 3;
            string shape = state != null && !string.IsNullOrEmpty(state.MapTerrainBrushShape) ? state.MapTerrainBrushShape : "circle";
            GUILayout.Label("Terrain Brush", _textStyle);
            DrawMapOptionDropdown(
                "map.terrain",
                "Paint With",
                FormatTerrainDropdownValue(state),
                new[]
                {
                    MapDropdownAction(MapAuthoringCommand.SetMode(MapAuthoringModeKind.PaintTerrain, "Woodland"), "Trees", "TR", state != null && state.MapAuthoringMode == "terrain:Woodland"),
                    MapDropdownAction(MapAuthoringCommand.SetMode(MapAuthoringModeKind.PaintTerrain, "Mountains"), "Mountains", "MT", state != null && state.MapAuthoringMode == "terrain:Mountains"),
                    MapDropdownAction(MapAuthoringCommand.SetMode(MapAuthoringModeKind.PaintTerrain, "NowhereSpecial"), "Clear", "ER", state != null && state.MapAuthoringMode == "terrain:NowhereSpecial"),
                    MapDropdownAction(MapAuthoringCommand.SetMode(MapAuthoringModeKind.PaintTerrain, ShelteredScenarioAuthoring.GeneratedBlendTerrainId), "Blend Area", "GB", state != null && state.MapAuthoringMode == "terrain:" + ShelteredScenarioAuthoring.GeneratedBlendTerrainId)
                });
            DrawMapOptionDropdown(
                "map.shape",
                "Brush Shape",
                string.Equals(shape, "square", StringComparison.OrdinalIgnoreCase) ? "Square" : "Round",
                new[]
                {
                    MapDropdownAction(MapAuthoringCommand.SetBrushShape(MapTerrainBrushShape.Circle), "Round", "O", shape == "circle"),
                    MapDropdownAction(MapAuthoringCommand.SetBrushShape(MapTerrainBrushShape.Rectangle), "Square", "[]", shape == "square")
                });
            DrawMapOptionDropdown(
                "map.size",
                "Brush Size",
                size.ToString(CultureInfo.InvariantCulture) + " x " + size.ToString(CultureInfo.InvariantCulture),
                new[]
                {
                    MapDropdownAction(MapAuthoringCommand.SetBrushSize(1), "1 x 1", "1", size == 1),
                    MapDropdownAction(MapAuthoringCommand.SetBrushSize(3), "3 x 3", "3", size == 3),
                    MapDropdownAction(MapAuthoringCommand.SetBrushSize(5), "5 x 5", "5", size == 5),
                    MapDropdownAction(MapAuthoringCommand.SetBrushSize(7), "7 x 7", "7", size == 7)
                });
            GUILayout.Label("Blend fills an area to match the terrain around it.", _mutedTextStyle);
            GUILayout.Space(8f);
        }

        private void DrawMapOptionDropdown(
            string dropdownId,
            string label,
            string value,
            ScenarioAuthoringInspectorAction[] actions)
        {
            GUILayout.Label(label, _mutedTextStyle);
            Rect selectorRect = GUILayoutUtility.GetRect(0f, 27f, GUILayout.ExpandWidth(true), GUILayout.Height(27f));
            bool expanded = string.Equals(_mapOverlayDropdownId, dropdownId, StringComparison.Ordinal);
            if (GUI.Button(selectorRect, new GUIContent((value ?? "Choose") + "  [v]", "Choose " + label.ToLowerInvariant() + "."), expanded ? _activeButtonStyle : _buttonStyle))
                _mapOverlayDropdownId = expanded ? null : dropdownId;

            if (!expanded || actions == null)
                return;

            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                Rect optionRect = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
                bool previousEnabled = GUI.enabled;
                GUI.enabled = action.Enabled;
                if (GUI.Button(optionRect, new GUIContent(action.Label, action.Hint), action.Emphasized ? _activeButtonStyle : _buttonStyle))
                {
                    ExecuteInspectorAction(action);
                    _mapOverlayDropdownId = null;
                }
                GUI.enabled = previousEnabled;
            }
        }

        private static ScenarioAuthoringInspectorAction MapDropdownAction(string id, string label, string icon, bool active)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Hint = "Use " + label + ".",
                Enabled = true,
                Emphasized = active,
                IconText = icon
            };
        }

        private static ScenarioAuthoringInspectorAction MapDropdownAction(MapAuthoringCommand command, string label, string icon, bool active)
        {
            ScenarioAuthoringInspectorAction action = MapDropdownAction(command.AutomationId, label, icon, active);
            action.Command = command;
            return action;
        }

        private static ScenarioAuthoringInspectorAction MapDropdownAction(EditorLifecycleCommand command, string label, string icon, bool active)
        {
            ScenarioAuthoringInspectorAction action = MapDropdownAction(command.AutomationId, label, icon, active);
            action.Command = command;
            return action;
        }

        private static string FormatTerrainDropdownValue(ScenarioAuthoringState state)
        {
            string mode = state != null ? state.MapAuthoringMode : null;
            if (string.Equals(mode, "terrain:Woodland", StringComparison.OrdinalIgnoreCase))
                return "Trees";
            if (string.Equals(mode, "terrain:Mountains", StringComparison.OrdinalIgnoreCase))
                return "Mountains";
            if (string.Equals(mode, "terrain:NowhereSpecial", StringComparison.OrdinalIgnoreCase))
                return "Clear";
            if (string.Equals(mode, "terrain:" + ShelteredScenarioAuthoring.GeneratedBlendTerrainId, StringComparison.OrdinalIgnoreCase))
                return "Blend Area";
            return "Choose terrain";
        }

        private void DrawMapAuthoringEmptySelection(ScenarioAuthoringState state)
        {
            GUILayout.Label("Choose a location or paint directly on the map.", _textStyle);
            GUILayout.Label("Current tool: " + FormatOverlayMode(state), _mutedTextStyle);
        }

        private void DrawMapAuthoringSelectionEditor(ScenarioMapRegionSelection selection, ScenarioAuthoringState state)
        {
            string id = GetEditableLocationId(selection);
            GUILayout.Label(selection.DisplayName ?? "<unnamed>", _textStyle);
            GUILayout.Label(FormatSelectionStateLine(selection), _mutedTextStyle);
            GUILayout.Label("Position: " + selection.GridX.ToString(CultureInfo.InvariantCulture) + ", " + selection.GridY.ToString(CultureInfo.InvariantCulture) + " | Terrain: " + SafeOverlay(selection.Topography), _mutedTextStyle);
            GUILayout.Label(FormatOverlayFlags(selection), _mutedTextStyle);
            GUILayout.Space(8f);
            DrawOverlayEditableProperty("Name", selection.DisplayName, MapLocationFieldKind.DisplayName, id, "The name players see on the map.");
            DrawOverlayEditableProperty("Location Type", !string.IsNullOrEmpty(selection.Topography) ? selection.Topography : selection.Category, MapLocationFieldKind.Kind, id, "The kind of place players will find here.");
            DrawOverlayEditableProperty("Map Icon", selection.IconId, MapLocationFieldKind.IconId, id, "The icon players see for this location.");
            MapAuthoringCommand cycleIconCommand = MapAuthoringCommand.CycleLocationIcon(id);
            DrawButton(GUILayoutUtility.GetRect(116f, 28f, GUILayout.Width(116f), GUILayout.Height(28f)), new ScenarioAuthoringInspectorAction
            {
                Id = cycleIconCommand.AutomationId,
                Command = cycleIconCommand,
                Label = "Next Icon",
                Hint = "Show the next available map icon.",
                Enabled = true,
                IconText = "IC"
            }, false);
            GUILayout.Label("Available icons: " + FormatKnownOverlayIconIds(), _mutedTextStyle);
            DrawOverlayEditableProperty("Encounter Risk", selection.OpenGroundEncounterChance.ToString(CultureInfo.InvariantCulture), MapLocationFieldKind.Danger, id, "Higher values make encounters more likely here.");
            DrawOverlayEditableProperty("Loot List", selection.LootTableId, MapLocationFieldKind.LootTableId, id, "The loot list used when players search this location.");
            DrawOverlayToggle(selection, id, MapLocationFieldKind.ReplaceGeneratedLoot, "Replace Existing Loot", selection.ReplaceGeneratedLoot);
            DrawOverlayEditableProperty("Encounter List", selection.EncounterTableId, MapLocationFieldKind.EncounterTableId, id, "The encounter list used at this location.");
            DrawOverlayToggle(selection, id, MapLocationFieldKind.Searchable, "Players Can Search Here", selection.Searchable);
            DrawOverlayToggle(selection, id, MapLocationFieldKind.VisibleAtStart, "Show From Start", selection.VisibleOnMap);
            DrawOverlayToggle(selection, id, MapLocationFieldKind.DiscoveredAtStart, "Start Discovered", selection.Discovered);
            DrawOverlayToggle(selection, id, MapLocationFieldKind.HiddenUntilDiscovered, "Hide Until Found", selection.HiddenUntilDiscovered);
            DrawMapUxSelectionDetails(selection, state);
            GUILayout.Space(8f);
            GUILayout.Label("Current tool: " + FormatOverlayMode(state), _mutedTextStyle);
        }

        private void DrawMapGenerationControls()
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            bool fixedSeed = definition != null && definition.SeedOverride.HasValue;
            GUILayout.Space(10f);
            GUILayout.Label("Starting Map", _textStyle);
            string value = fixedSeed
                ? "Random layout chosen"
                : "Default random map";
            DrawMapOptionDropdown(
                "map.generation",
                "Map Layout",
                value,
                new[]
                {
                    MapDropdownAction(EditorLifecycleCommand.UseRandomSeed, "Use Default Random Map", "VN", !fixedSeed),
                    MapDropdownAction(EditorLifecycleCommand.RerollSeed, "Choose New Random Layout", "RR", false)
                });
            GUILayout.Label("Reload or start a playtest to see the new layout.", _mutedTextStyle);
        }

        private void DrawOverlayEditableProperty(string label, string value, MapLocationFieldKind field, string locationId, string hint)
        {
            MapAuthoringCommand editCommand = MapAuthoringCommand.EditLocationField(field, locationId, null);
            DrawEditableProperty(new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value ?? string.Empty,
                Editable = true,
                Action = new ScenarioAuthoringInspectorAction
                {
                    Id = editCommand.AutomationId,
                    Command = editCommand,
                    Label = label,
                    Hint = hint,
                    Enabled = true,
                    IconText = "ED"
                }
            }, true);
        }

        private void DrawOverlayToggle(ScenarioMapRegionSelection selection, string locationId, MapLocationFieldKind field, string label, bool value)
        {
            MapAuthoringCommand toggleCommand = MapAuthoringCommand.ToggleLocationField(field, locationId);
            DrawButton(GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f)), new ScenarioAuthoringInspectorAction
            {
                Id = toggleCommand.AutomationId,
                Command = toggleCommand,
                Label = label,
                Hint = "Turn " + label + " on or off for this location.",
                Enabled = selection != null,
                Emphasized = value,
                IconText = value ? "ON" : "OFF",
                Badge = value ? "On" : "Off"
            }, false);
        }

        private static string GetEditableLocationId(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return null;
            if (!string.IsNullOrEmpty(selection.LocationId))
                return selection.LocationId;
            if (!string.IsNullOrEmpty(selection.CapturedLocationId))
                return selection.CapturedLocationId;
            return "vanilla-" + selection.GridX.ToString(CultureInfo.InvariantCulture) + "-" + selection.GridY.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatSelectionStateLine(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return string.Empty;
            return selection.Authored
                ? "Custom location"
                : "Existing map location - changes are saved to this scenario";
        }

        private static string FormatKnownOverlayIconIds()
        {
            string[] icons = ShelteredScenarioAuthoring.GetKnownMapIconIds();
            if (icons == null || icons.Length == 0)
                return "<none>";

            int limit = icons.Length > 5 ? 5 : icons.Length;
            string value = string.Empty;
            for (int i = 0; i < limit; i++)
                value = value.Length == 0 ? icons[i] : value + ", " + icons[i];
            if (icons.Length > limit)
                value += ", +" + (icons.Length - limit).ToString(CultureInfo.InvariantCulture);
            return value;
        }

        private static string FormatOverlayMode(ScenarioAuthoringState state)
        {
            string mode = state != null && !string.IsNullOrEmpty(state.MapAuthoringMode) ? state.MapAuthoringMode : "select";
            string sourceId;
            if (ScenarioMapLocationDuplicateService.TryReadSourceId(mode, out sourceId))
                return "Copy Location - choose a new spot";
            if (string.Equals(mode, "place", StringComparison.OrdinalIgnoreCase))
                return "Add Location";
            if (string.Equals(mode, "move", StringComparison.OrdinalIgnoreCase))
                return "Move Location";
            if (string.Equals(mode, "terrain:Woodland", StringComparison.OrdinalIgnoreCase))
                return "Paint Trees";
            if (string.Equals(mode, "terrain:Mountains", StringComparison.OrdinalIgnoreCase))
                return "Paint Mountains";
            if (string.Equals(mode, "terrain:NowhereSpecial", StringComparison.OrdinalIgnoreCase))
                return "Clear Terrain";
            if (string.Equals(mode, "terrain:" + ShelteredScenarioAuthoring.GeneratedBlendTerrainId, StringComparison.OrdinalIgnoreCase))
                return "Blend Terrain";
            return "Select";
        }

        private static string FormatOverlayFlags(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return string.Empty;

            string visible = selection.VisibleOnMap ? "Shown on map" : "Hidden from map";
            string discovered = selection.Discovered ? "Discovered" : "Undiscovered";
            string searchable = selection.Searchable ? "Searchable" : "Cannot be searched";
            return visible + " | " + discovered + " | " + searchable;
        }

        private static string SafeOverlay(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }
    }
}

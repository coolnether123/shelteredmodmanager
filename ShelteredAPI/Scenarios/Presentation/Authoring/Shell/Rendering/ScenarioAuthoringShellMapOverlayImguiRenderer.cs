using System;
using System.Globalization;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
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
            float width = Mathf.Min(360f, Mathf.Max(306f, scaledWidth * 0.27f));
            float leftDockWidth = BuildMapAuthoringLeftDockRect(scaledWidth, scaledHeight).width;
            float maxWidth = Mathf.Max(260f, scaledWidth - leftDockWidth - (Margin * 3f) - 420f);
            width = Mathf.Min(width, maxWidth);
            float height = Mathf.Max(280f, scaledHeight - (Margin * 2f));
            return new Rect(Mathf.Max(Margin + leftDockWidth + Margin, scaledWidth - width - Margin), Margin, width, height);
        }

        private void DrawMapAuthoringModeDock(Rect rect, ScenarioAuthoringState state, ScenarioMapRegionSelection selection)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, rect.height - 20f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Map", _smallTitleStyle);

            float y = inner.y + 34f;
            y = DrawModeTab(inner.x, y, inner.width, "Select", "SL", ScenarioAuthoringActionIds.ActionMapAuthoringModeSelect, state == null || string.IsNullOrEmpty(state.MapAuthoringMode) || state.MapAuthoringMode == "select", true);
            y = DrawModeTab(inner.x, y + 6f, inner.width, "Place", "PL", ScenarioAuthoringActionIds.ActionMapAuthoringModePlace, state != null && state.MapAuthoringMode == "place", true);
            bool canMove = selection != null && selection.Authored;
            y = DrawModeTab(inner.x, y + 6f, inner.width, "Move", "MV", ScenarioAuthoringActionIds.ActionMapAuthoringModeMove, state != null && state.MapAuthoringMode == "move", canMove);
            DrawMapAuthoringLegend(inner, y + 14f);

            Rect closeRect = new Rect(inner.x, inner.yMax - 30f, inner.width, 28f);
            DrawButton(closeRect, new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionMapAuthoringClose,
                Label = "Close",
                Hint = "Close the vanilla map and return to the Map workshop page.",
                Enabled = true,
                IconText = "CL"
            }, false);
        }

        private float DrawModeTab(float x, float y, float width, string label, string icon, string actionId, bool active, bool enabled)
        {
            DrawButton(new Rect(x, y, width, 38f), new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = label,
                Hint = "Set map authoring mode to " + label + ".",
                Enabled = enabled,
                Emphasized = active,
                IconText = icon,
                DisabledReason = enabled ? null : "Select an authored location before using Move mode."
            }, false);
            return y + 38f;
        }

        private void DrawMapAuthoringSelectionDock(Rect rect, ScenarioAuthoringState state, ScenarioMapRegionSelection selection)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Location", _smallTitleStyle);

            Rect body = new Rect(inner.x, inner.y + 30f, inner.width, inner.height - 30f);
            GUILayout.BeginArea(body);
            Vector2 scroll = GetWindowScrollPosition("map.authoring.right_dock");
            RegisterScrollRegion("map.authoring.right_dock", body);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, body.width - 18f);
            if (selection == null)
                DrawMapAuthoringEmptySelection(state);
            else
                DrawMapAuthoringSelectionEditor(selection, state);
            _activeContentWidth = previousContentWidth;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition("map.authoring.right_dock", scroll);
        }

        private void DrawMapAuthoringEmptySelection(ScenarioAuthoringState state)
        {
            GUILayout.Label("Click the map to select or place locations.", _textStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Mode: " + FormatOverlayMode(state) + ". Escape or Close returns to the Map workshop page.", _mutedTextStyle);
            GUILayout.Label("Place and Move require an existing vanilla region; empty NowhereSpecial cells are blocked.", _mutedTextStyle);
        }

        private void DrawMapAuthoringSelectionEditor(ScenarioMapRegionSelection selection, ScenarioAuthoringState state)
        {
            string id = GetEditableLocationId(selection);
            GUILayout.Label(selection.DisplayName ?? "<unnamed>", _textStyle);
            GUILayout.Label(FormatSelectionStateLine(selection), _mutedTextStyle);
            GUILayout.Label("Grid " + selection.GridX.ToString(CultureInfo.InvariantCulture) + "," + selection.GridY.ToString(CultureInfo.InvariantCulture) + "  " + SafeOverlay(selection.Topography), _mutedTextStyle);
            GUILayout.Label(FormatOverlayFlags(selection), _mutedTextStyle);
            GUILayout.Space(8f);
            DrawOverlayEditableProperty("Name", selection.DisplayName, "displayName", id, "Shown in the scenario map draft and later runtime projection.");
            DrawOverlayEditableProperty("Kind", !string.IsNullOrEmpty(selection.Topography) ? selection.Topography : selection.Category, "kind", id, "Semantic category used by E5/E6 projection.");
            DrawOverlayEditableProperty("Icon Id", selection.IconId, "iconId", id, "Must match a known map icon sprite id.");
            DrawButton(GUILayoutUtility.GetRect(116f, 28f, GUILayout.Width(116f), GUILayout.Height(28f)), new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix, id),
                Label = "Next Icon",
                Hint = "Cycle through the known map icon sprite ids.",
                Enabled = true,
                IconText = "IC"
            }, false);
            GUILayout.Label("Known Icons: " + FormatKnownOverlayIconIds(), _mutedTextStyle);
            DrawOverlayEditableProperty("Danger", selection.OpenGroundEncounterChance.ToString(CultureInfo.InvariantCulture), "danger", id, "Basic encounter danger/risk value for later projection.");
            DrawOverlayEditableProperty("Loot Table", selection.LootTableId, "lootTableId", id, "References an existing MapLootTableDefinition id.");
            DrawOverlayToggle(selection, id, "replaceGeneratedLoot", "Replace Generated Loot", selection.ReplaceGeneratedLoot);
            DrawOverlayEditableProperty("Encounter Table", selection.EncounterTableId, "encounterTableId", id, "References an existing MapEncounterTableDefinition id.");
            DrawOverlayToggle(selection, id, "searchable", "Searchable", selection.Searchable);
            DrawOverlayToggle(selection, id, "visibleAtStart", "Visible", selection.VisibleOnMap);
            DrawOverlayToggle(selection, id, "discoveredAtStart", "Discovered", selection.Discovered);
            DrawOverlayToggle(selection, id, "hiddenUntilDiscovered", "Hidden Until Discovery", selection.HiddenUntilDiscovered);
            DrawMapUxSelectionDetails(selection, state);
            GUILayout.Space(8f);
            GUILayout.Label("Mode: " + FormatOverlayMode(state), _mutedTextStyle);
        }

        private void DrawOverlayEditableProperty(string label, string value, string field, string locationId, string hint)
        {
            DrawEditableProperty(new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value ?? string.Empty,
                Editable = true,
                Action = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionMapLocationEditPrefix + field + "." + ScenarioAuthoringActionCodec.EncodeToken(locationId) + ".",
                    Label = label,
                    Hint = hint,
                    Enabled = true,
                    IconText = "ED"
                }
            }, true);
        }

        private void DrawOverlayToggle(ScenarioMapRegionSelection selection, string locationId, string field, string label, bool value)
        {
            DrawButton(GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f)), new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix + field + "." + ScenarioAuthoringActionCodec.EncodeToken(locationId),
                Label = label,
                Hint = "Toggle " + label + " for this map location.",
                Enabled = selection != null,
                Emphasized = value,
                IconText = value ? "ON" : "OFF",
                Badge = value ? "Enabled" : "Disabled"
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
                ? "Authored"
                : "Vanilla - edits will be saved to your scenario";
        }

        private static string FormatKnownOverlayIconIds()
        {
            string[] icons = ScenarioMapIconCatalog.GetKnownIconIds();
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
            return ScenarioMapLocationDuplicateService.TryReadSourceId(mode, out sourceId) ? "duplicate — choose a new cell" : mode;
        }

        private static string FormatOverlayFlags(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return string.Empty;

            string visible = selection.VisibleOnMap ? "visible" : "hidden";
            string discovered = selection.Discovered ? "discovered" : "undiscovered";
            string searchable = selection.Searchable ? "searchable" : "not searchable";
            return visible + ", " + discovered + ", " + searchable;
        }

        private static string SafeOverlay(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Purpose-built layouts for the editor's compact utility surfaces.</summary>
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const string AssetFilterAll = "all";
        private string _newWindowsAssetFilter = AssetFilterAll;
        private Vector2 _testConsoleLogScroll = Vector2.zero;
        private GUIStyle _metadataMultilineFieldStyle;

        private bool TryDrawNewWindowsSection(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            if (section == null)
                return false;

            bool supported = section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.MetadataForm
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.TestStatus
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.TestUpcoming
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.TestLog
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.TestControls
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.PackagePreview
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.PackageActions
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryFilters
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryRow;
            if (!supported)
                return false;

            bool wideAssetSurface = section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryFilters
                || section.RendererKind == ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryRow;
            float boundedWidth = Math.Min(ResolveLogicalPixelCap(wideAssetSurface ? 1080f : 760f), GetSectionContentWidth());
            GUILayout.BeginVertical(GUILayout.Width(boundedWidth));
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = boundedWidth;
            switch (section.RendererKind)
            {
                case ScenarioAuthoringInspectorSectionRendererKind.MetadataForm:
                    DrawMetadataFormSection(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.TestStatus:
                case ScenarioAuthoringInspectorSectionRendererKind.TestUpcoming:
                case ScenarioAuthoringInspectorSectionRendererKind.TestLog:
                case ScenarioAuthoringInspectorSectionRendererKind.TestControls:
                    DrawTestConsoleSection(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.PackagePreview:
                    DrawPackagePreviewSection(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.PackageActions:
                    DrawPackageActionsSection(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryFilters:
                    DrawAssetInventoryFilters(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.AssetInventoryRow:
                    if (AssetRowMatchesFilter(section))
                        DrawAssetInventoryRow(section);
                    break;
            }
            _activeContentWidth = previousContentWidth;
            GUILayout.EndVertical();
            return true;
        }

        private void DrawMetadataFormSection(ScenarioAuthoringInspectorSection section)
        {
            DrawMetadataFormSection(section, true);
        }

        private void DrawMetadataFormSection(ScenarioAuthoringInspectorSection section, bool drawTitle)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            if (drawTitle)
                GUILayout.Label(section.Title ?? "Scenario details", _sectionTitleStyle);
            ScenarioAuthoringInspectorItem version = FindProperty(section, "Version");
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || !item.Editable)
                    continue;
                if (string.Equals(item.Label, "Version", StringComparison.OrdinalIgnoreCase))
                {
                    DrawMetadataVersionRow(section, version);
                    continue;
                }

                bool multiline = string.Equals(item.Label, "Description", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Label, "Credits", StringComparison.OrdinalIgnoreCase);
                DrawMetadataField(section.Id, item, multiline);
                GUILayout.Space(4f);
            }
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && !item.Editable && item.Action == null) DrawItem(item);
            }
            GUILayout.EndVertical();
        }

        private void DrawMetadataVersionRow(ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorItem version)
        {
            if (version == null) return;
            float width = GetSectionContentWidth();
            bool stacked = width < 520f;
            string warning = MetadataWarning(version);
            float rowHeight = (stacked ? 64f : 32f) + (string.IsNullOrEmpty(warning) ? 0f : 18f);
            Rect row = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            float labelWidth = stacked ? row.width : 108f;
            Rect labelRect = new Rect(row.x, row.y + 5f, labelWidth, 22f);
            GUI.Label(labelRect, "Version", _mutedTextStyle);
            float fieldY = stacked ? row.y + 28f : row.y;
            float fieldX = stacked ? row.x : labelRect.xMax + 8f;
            float buttonsWidth = 144f;
            float fieldWidth = Math.Min(ResolveLogicalPixelCap(520f), Math.Max(76f, row.xMax - fieldX - buttonsWidth - 8f));
            DrawNewWindowsEditableControl(new Rect(fieldX, fieldY, fieldWidth, 32f), section.Id, version, false);

            float buttonX = fieldX + fieldWidth + 8f;
            int buttonIndex = 0;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Kind != ScenarioAuthoringInspectorItemKind.Action || item.Action == null)
                    continue;
                Rect buttonRect = new Rect(buttonX + (buttonIndex * 72f), fieldY, 68f, 32f);
                DrawButton(buttonRect, item.Action, false);
                buttonIndex++;
            }
            if (!string.IsNullOrEmpty(warning))
                DrawMetadataWarning(new Rect(fieldX, fieldY + 30f, row.xMax - fieldX, 18f), warning);
        }

        private void DrawMetadataField(string sectionId, ScenarioAuthoringInspectorItem item, bool multiline)
        {
            float width = GetSectionContentWidth();
            bool stacked = width < 520f;
            string warning = MetadataWarning(item);
            float labelAndGapWidth = stacked ? 0f : 116f;
            float maximumWidth = ResolveLogicalPixelCap(multiline ? 680f : 520f);
            float estimatedFieldWidth = Math.Min(maximumWidth, Math.Max(80f, width - labelAndGapWidth));
            float controlHeight = multiline
                ? Mathf.Clamp(
                    GetMetadataMultilineFieldStyle().CalcHeight(
                        new GUIContent(item.Value ?? string.Empty),
                        estimatedFieldWidth),
                    70f,
                    140f)
                : 32f;
            float rowHeight = stacked ? controlHeight + 28f + (string.IsNullOrEmpty(warning) ? 0f : 18f) : controlHeight + (string.IsNullOrEmpty(warning) ? 0f : 18f);
            Rect row = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            float labelWidth = stacked ? row.width : 108f;
            GUI.Label(new Rect(row.x, row.y + 5f, labelWidth, 22f), item.Label ?? string.Empty, _mutedTextStyle);
            float fieldX = stacked ? row.x : row.x + labelWidth + 8f;
            float fieldY = stacked ? row.y + 26f : row.y;
            Rect fieldRect = new Rect(fieldX, fieldY, Math.Min(maximumWidth, Math.Max(80f, row.xMax - fieldX)), controlHeight);
            DrawNewWindowsEditableControl(fieldRect, sectionId, item, multiline);
            if (!string.IsNullOrEmpty(warning))
                DrawMetadataWarning(new Rect(fieldRect.x, fieldRect.yMax, fieldRect.width, 18f), warning);
        }

        private void DrawNewWindowsEditableControl(Rect rect, string sectionId, ScenarioAuthoringInspectorItem item, bool multiline)
        {
            string controlName = "newwindows.metadata." + (sectionId ?? string.Empty) + "." + (item.Label ?? string.Empty);
            string committed = item.Value ?? string.Empty;
            string focusedName = GUI.GetNameOfFocusedControl();
            bool wasFocused = string.Equals(focusedName, controlName, StringComparison.Ordinal);
            bool previouslyFocused = _editableFieldsFocusedLastFrame.Contains(controlName);
            string draft;
            if (!_editableFieldDrafts.TryGetValue(controlName, out draft) || (!wasFocused && !previouslyFocused))
                draft = committed;

            GUI.SetNextControlName(controlName);
            string next = multiline
                ? GUI.TextArea(rect, draft, GetMetadataMultilineFieldStyle())
                : GUI.TextField(rect, draft, _uiContext.Styles.Field);
            _editableFieldDrafts[controlName] = next;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _editableFieldFocused = _editableFieldFocused || focused;
            if (focused) DrawFieldFocusBorder(rect);

            bool commitKey = focused
                && Event.current != null
                && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && (!multiline || Event.current.control || Event.current.command);
            bool lostFocus = previouslyFocused && !focused;
            if ((commitKey || lostFocus) && item.Action != null && !string.Equals(next, committed, StringComparison.Ordinal))
                ScenarioAuthoringBackendService.Instance.ExecuteAction(item.Action.Id + ScenarioAuthoringActionCodec.EncodeToken(next));
            if (commitKey)
            {
                GUI.FocusControl(null);
                Event.current.Use();
            }
            TrackEditableFieldFocus(controlName, focused);
        }

        private GUIStyle GetMetadataMultilineFieldStyle()
        {
            if (_metadataMultilineFieldStyle != null)
                return _metadataMultilineFieldStyle;

            _metadataMultilineFieldStyle = new GUIStyle(_uiContext.Styles.Field);
            _metadataMultilineFieldStyle.fixedHeight = 0f;
            _metadataMultilineFieldStyle.wordWrap = true;
            _metadataMultilineFieldStyle.alignment = TextAnchor.UpperLeft;
            return _metadataMultilineFieldStyle;
        }

        private void DrawMetadataWarning(Rect rect, string warning)
        {
            if (string.IsNullOrEmpty(warning)) return;
            GUIStyle style = new GUIStyle(_mutedTextStyle);
            style.normal.textColor = _uiContext.Styles.Theme.Palette.AccentWarning;
            style.alignment = TextAnchor.MiddleLeft;
            GUI.Label(rect, warning, style);
        }

        private static string MetadataWarning(ScenarioAuthoringInspectorItem item)
        {
            string label = item != null ? item.Label ?? string.Empty : string.Empty;
            string value = item != null ? (item.Value ?? string.Empty).Trim() : string.Empty;
            if (string.Equals(label, "Description", StringComparison.OrdinalIgnoreCase) && value.Length == 0)
                return "Recommended before export";
            if (string.Equals(label, "Author", StringComparison.OrdinalIgnoreCase)
                && (value.Length == 0 || string.Equals(value, ScenarioMetadataDefaults.DefaultAuthor, StringComparison.OrdinalIgnoreCase)))
                return "Replace the placeholder author";
            if (string.Equals(label, "Version", StringComparison.OrdinalIgnoreCase)
                && (value.Length == 0 || string.Equals(value, ScenarioMetadataDefaults.DefaultVersion, StringComparison.OrdinalIgnoreCase)))
                return "Review the default version";
            return string.Empty;
        }

        private void DrawTestConsoleSection(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            if (!string.IsNullOrEmpty(section.Title)) GUILayout.Label(section.Title, _sectionTitleStyle);
            switch (section.RendererKind)
            {
                case ScenarioAuthoringInspectorSectionRendererKind.TestStatus:
                    DrawTestStatus(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.TestUpcoming:
                    DrawTestUpcoming(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.TestLog:
                    DrawTestLog(section);
                    break;
                case ScenarioAuthoringInspectorSectionRendererKind.TestControls:
                    DrawUniformActionGrid(section, 126f, 30f);
                    break;
            }
            GUILayout.EndVertical();
        }

        private void DrawTestStatus(ScenarioAuthoringInspectorSection section)
        {
            float width = GetSectionContentWidth();
            int count = section.Items != null ? section.Items.Length : 0;
            int columns = width >= 620f ? Math.Min(4, count) : Math.Min(2, count);
            if (columns <= 0) return;
            float gap = 8f;
            float cardWidth = (width - (gap * (columns - 1))) / columns;
            for (int start = 0; start < count; start += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns && start + column < count; column++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[start + column];
                    Rect card = GUILayoutUtility.GetRect(cardWidth, 58f, GUILayout.Width(cardWidth), GUILayout.Height(58f));
                    Rect inner = ScenarioUiWidgets.DrawCard(card, _uiContext.Styles, item != null ? item.Label : string.Empty);
                    GUI.Label(inner, item != null ? item.Value ?? string.Empty : string.Empty, _textStyle);
                    if (column < columns - 1) GUILayout.Space(gap);
                }
                GUILayout.EndHorizontal();
                if (start + columns < count) GUILayout.Space(gap);
            }
        }

        private void DrawTestUpcoming(ScenarioAuthoringInspectorSection section)
        {
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null) continue;
                if (item.Kind != ScenarioAuthoringInspectorItemKind.Property)
                {
                    DrawItem(item);
                    continue;
                }
                Rect row = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true), GUILayout.Height(38f));
                GUI.Label(new Rect(row.x, row.y + 4f, Math.Min(112f, row.width * 0.28f), 20f), item.Label ?? string.Empty, _mutedTextStyle);
                float bodyX = row.x + Math.Min(120f, row.width * 0.30f);
                GUI.Label(new Rect(bodyX, row.y + 2f, row.xMax - bodyX, 20f), item.Value ?? string.Empty, _textStyle);
                if (!string.IsNullOrEmpty(item.Detail)) GUI.Label(new Rect(bodyX, row.y + 20f, row.xMax - bodyX, 17f), item.Detail, _mutedTextStyle);
                ScenarioUiWidgets.DrawHorizontalDivider(new Rect(row.x, row.yMax - 1f, row.width, 1f), _uiContext.Styles);
            }
        }

        private void DrawTestLog(ScenarioAuthoringInspectorSection section)
        {
            Rect viewport = GUILayoutUtility.GetRect(0f, 190f, GUILayout.ExpandWidth(true), GUILayout.Height(190f));
            GUI.Box(viewport, GUIContent.none, _uiContext.Styles.Card);
            GUILayout.BeginArea(new Rect(viewport.x + 5f, viewport.y + 5f, viewport.width - 10f, viewport.height - 10f));
            _testConsoleLogScroll = GUILayout.BeginScrollView(_testConsoleLogScroll, false, true);
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null) continue;
                Rect row = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true), GUILayout.Height(38f));
                GUI.Label(new Rect(row.x + 3f, row.y + 6f, 22f, 22f), item.IconText ?? "-", _sectionTitleStyle);
                float badgeWidth = string.IsNullOrEmpty(item.Badge) ? 0f : 74f;
                GUI.Label(new Rect(row.x + 28f, row.y + 3f, Math.Max(40f, row.width - badgeWidth - 36f), 32f), item.Value ?? string.Empty, _textStyle);
                if (badgeWidth > 0f)
                    ScenarioUiWidgets.DrawPill(new Rect(row.xMax - badgeWidth, row.y + 8f, badgeWidth - 4f, 20f), item.Badge, _uiContext.Styles, ResolveOutcomeEmphasis(item.Badge));
                ScenarioUiWidgets.DrawHorizontalDivider(new Rect(row.x, row.yMax - 1f, row.width, 1f), _uiContext.Styles);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static ScenarioUiPillEmphasis ResolveOutcomeEmphasis(string badge)
        {
            if (string.Equals(badge, "FAILED", StringComparison.OrdinalIgnoreCase)) return ScenarioUiPillEmphasis.Danger;
            if (string.Equals(badge, "SKIPPED", StringComparison.OrdinalIgnoreCase)) return ScenarioUiPillEmphasis.Warning;
            if (string.Equals(badge, "FIRED", StringComparison.OrdinalIgnoreCase) || string.Equals(badge, "MANUAL", StringComparison.OrdinalIgnoreCase)) return ScenarioUiPillEmphasis.Success;
            return ScenarioUiPillEmphasis.Default;
        }

        private void DrawUniformActionGrid(ScenarioAuthoringInspectorSection section, float minimumWidth, float height)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                if (section.Items[i] != null && section.Items[i].Action != null) actions.Add(section.Items[i].Action);
            float available = GetSectionContentWidth();
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + 4f) / (minimumWidth + 4f)), 1, Math.Max(1, actions.Count));
            float width = (available - ((columns - 1) * 4f)) / columns;
            for (int start = 0; start < actions.Count; start += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns && start + column < actions.Count; column++)
                {
                    Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
                    DrawButton(rect, actions[start + column], false);
                    if (column < columns - 1) GUILayout.Space(4f);
                }
                GUILayout.EndHorizontal();
                if (start + columns < actions.Count) GUILayout.Space(4f);
            }
        }

        private void DrawPackagePreviewSection(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            GUILayout.Label(section.Title ?? "Package preview", _sectionTitleStyle);
            GUILayout.Label("FILES", _mutedTextStyle);
            DrawPackageFileHeader();
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || !string.Equals(item.Label, "FILE", StringComparison.Ordinal)) continue;
                DrawPackageFileRow(item);
            }
            ScenarioAuthoringInspectorItem total = FindProperty(section, "TOTAL");
            Rect totalRect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            GUI.Label(new Rect(totalRect.x, totalRect.y + 4f, totalRect.width - 120f, 20f), "Total", _sectionTitleStyle);
            GUI.Label(new Rect(totalRect.xMax - 116f, totalRect.y + 4f, 116f, 20f), total != null ? total.Value : "0 B", _sectionTitleStyle);
            GUILayout.Space(8f);
            DrawPackageSubsection(section, "DEPENDENCY", "DEPENDENCIES", ScenarioUiPillEmphasis.Default);
            GUILayout.Space(8f);
            DrawPackageProblems(section);
            GUILayout.Space(8f);
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                if (section.Items[i] != null && section.Items[i].Action != null)
                    DrawButton(GUILayoutUtility.GetRect(210f, 30f, GUILayout.Width(210f), GUILayout.Height(30f)), section.Items[i].Action, false);
            GUILayout.EndVertical();
        }

        private void DrawPackageFileHeader()
        {
            Rect row = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            GUI.Label(new Rect(row.x + 4f, row.y, row.width - 124f, row.height), "Name", _mutedTextStyle);
            GUI.Label(new Rect(row.xMax - 116f, row.y, 112f, row.height), "Size", _mutedTextStyle);
            ScenarioUiWidgets.DrawHorizontalDivider(new Rect(row.x, row.yMax - 1f, row.width, 1f), _uiContext.Styles);
        }

        private void DrawPackageFileRow(ScenarioAuthoringInspectorItem item)
        {
            Rect row = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            bool advanced = _snapshot != null && _snapshot.State != null && _snapshot.State.Settings != null
                && _snapshot.State.Settings.GetBool("debug.show_advanced_details", false);
            string name = advanced ? item.Value ?? string.Empty : Path.GetFileName(item.Value ?? string.Empty);
            GUI.Label(new Rect(row.x + 4f, row.y + 3f, row.width - 124f, 22f), ShortenToFit(name, row.width - 128f, _textStyle), _textStyle);
            GUI.Label(new Rect(row.xMax - 116f, row.y + 3f, 112f, 22f), item.Detail ?? string.Empty, _textStyle);
            ScenarioUiWidgets.DrawHorizontalDivider(new Rect(row.x, row.yMax - 1f, row.width, 1f), _uiContext.Styles);
        }

        private void DrawPackageSubsection(ScenarioAuthoringInspectorSection section, string label, string title, ScenarioUiPillEmphasis emphasis)
        {
            GUILayout.Label(title, _mutedTextStyle);
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || !string.Equals(item.Label, label, StringComparison.Ordinal)) continue;
                Rect row = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
                ScenarioUiWidgets.DrawPill(new Rect(row.x + 2f, row.y + 3f, 88f, 20f), label == "DEPENDENCY" ? "DECLARED" : label, _uiContext.Styles, emphasis);
                GUI.Label(new Rect(row.x + 98f, row.y + 3f, row.width - 100f, 20f), item.Value ?? string.Empty, _textStyle);
            }
        }

        private void DrawPackageProblems(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.Label("PROBLEMS", _mutedTextStyle);
            bool found = false;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null
                    || (!string.Equals(item.Label, "PROBLEM", StringComparison.Ordinal)
                        && !string.Equals(item.Label, "PREFLIGHT", StringComparison.Ordinal)
                        && !string.Equals(item.Label, "ACCEPTED", StringComparison.Ordinal))) continue;
                found = true;
                bool ready = string.Equals(item.Label, "PREFLIGHT", StringComparison.Ordinal);
                bool accepted = string.Equals(item.Label, "ACCEPTED", StringComparison.Ordinal);
                Rect row = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true), GUILayout.Height(34f));
                ScenarioUiWidgets.DrawPill(new Rect(row.x + 2f, row.y + 6f, 78f, 20f), ready ? "READY" : accepted ? "ACCEPTED" : "BLOCKER", _uiContext.Styles, ready ? ScenarioUiPillEmphasis.Success : accepted ? ScenarioUiPillEmphasis.Warning : ScenarioUiPillEmphasis.Danger);
                GUI.Label(new Rect(row.x + 88f, row.y + 3f, row.width - 90f, 28f), ready && !string.IsNullOrEmpty(item.Detail) ? item.Detail : item.Value ?? string.Empty, _textStyle);
            }
            if (!found) GUILayout.Label("No package problems found.", _textStyle);
        }

        private void DrawPackageActionsSection(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            GUILayout.Label(section.Title ?? "Export package", _sectionTitleStyle);
            DrawUniformActionGrid(section, 170f, 34f);
            GUILayout.Space(8f);
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Action == null) DrawItem(item);
            }
            GUILayout.EndVertical();
        }

        private void DrawAssetInventoryFilters(ScenarioAuthoringInspectorSection section)
        {
            _newWindowsAssetFilter = ScenarioAuthoringRendererInteractionState.Instance.AssetInventoryFilter;
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            GUILayout.Label(section.Title ?? "Filter", _sectionTitleStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                string filter = section.Items[i] != null ? section.Items[i].Value ?? AssetFilterAll : AssetFilterAll;
                string label = char.ToUpperInvariant(filter[0]) + filter.Substring(1);
                Rect rect = GUILayoutUtility.GetRect(82f, 26f, GUILayout.Width(82f), GUILayout.Height(26f));
                bool active = string.Equals(_newWindowsAssetFilter, filter, StringComparison.OrdinalIgnoreCase);
                RegisterInteractiveRegion(rect);
                if (DrawPlainButton(rect, new GUIContent(label), active ? _activeButtonStyle : _buttonStyle, true))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(
                        ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetInventoryFilterPrefix, filter));
                    _newWindowsAssetFilter = ScenarioAuthoringRendererInteractionState.Instance.AssetInventoryFilter;
                    Event.current.Use();
                }
                if (i < section.Items.Length - 1) GUILayout.Space(4f);
            }
            GUILayout.EndHorizontal();
            if (GetSectionContentWidth() >= 720f) DrawAssetTableHeader();
            GUILayout.EndVertical();
        }

        private void DrawAssetTableHeader()
        {
            Rect row = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            float x = row.x + 62f;
            float actionWidth = 170f;
            float nameWidth = Math.Max(120f, row.width - 574f);
            GUI.Label(new Rect(x, row.y, nameWidth, row.height), "Name", _mutedTextStyle); x += nameWidth + 6f;
            GUI.Label(new Rect(x, row.y, 82f, row.height), "Dimensions", _mutedTextStyle); x += 88f;
            GUI.Label(new Rect(x, row.y, 74f, row.height), "Size", _mutedTextStyle); x += 80f;
            GUI.Label(new Rect(x, row.y, 92f, row.height), "Source", _mutedTextStyle); x += 98f;
            GUI.Label(new Rect(x, row.y, 58f, row.height), "Uses", _mutedTextStyle);
            GUI.Label(new Rect(row.xMax - actionWidth, row.y, actionWidth, row.height), "Actions", _mutedTextStyle);
        }

        private bool AssetRowMatchesFilter(ScenarioAuthoringInspectorSection section)
        {
            if (string.Equals(_newWindowsAssetFilter, AssetFilterAll, StringComparison.OrdinalIgnoreCase)) return true;
            if (section != null && string.Equals(section.RendererFilter, _newWindowsAssetFilter, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(_newWindowsAssetFilter, "large", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
                    if (section.Items[i] != null && (section.Items[i].Value ?? string.Empty).StartsWith("Large texture", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void DrawAssetInventoryRow(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            ScenarioAuthoringInspectorItem file = FindProperty(section, "File name");
            if (GetSectionContentWidth() < 720f)
                DrawAssetInventoryRowStacked(section, file);
            else
                DrawAssetInventoryRowWide(section, file);
            ScenarioAuthoringInspectorItem credit = FindProperty(section, "Author / credit note");
            if (credit != null)
            {
                GUILayout.Space(4f);
                DrawEditableProperty(credit, false);
            }
            GUILayout.EndVertical();
        }

        private void DrawAssetInventoryRowWide(ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorItem file)
        {
            List<ScenarioAuthoringInspectorAction> actions = FindActions(section);
            float rowHeight = Math.Max(76f, actions.Count * 28f);
            Rect row = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            float x = row.x;
            if (file != null && file.PreviewSprite != null) DrawSpritePreview(new Rect(x, row.y + 6f, 54f, 54f), file.PreviewSprite, file.Emphasized);
            else GUI.Box(new Rect(x, row.y + 6f, 54f, 54f), "--", _uiContext.Styles.Card);
            x += 62f;
            float actionWidth = 170f;
            float nameWidth = Math.Max(120f, row.width - 574f);
            GUI.Label(new Rect(x, row.y + 8f, nameWidth, 22f), file != null ? file.Value ?? string.Empty : string.Empty, _sectionTitleStyle);
            if (file != null && !string.IsNullOrEmpty(file.Detail)) GUI.Label(new Rect(x, row.y + 32f, nameWidth, 32f), file.Detail, _mutedTextStyle);
            if (file != null && !string.IsNullOrEmpty(file.Badge)) ScenarioUiWidgets.DrawPill(new Rect(x, row.yMax - 22f, Math.Min(84f, nameWidth), 20f), file.Badge, _uiContext.Styles, AssetBadgeEmphasis(file.Badge));
            x += nameWidth + 6f;
            DrawAssetCell(row, ref x, 82f, PropertyValue(section, "Dimensions"));
            DrawAssetCell(row, ref x, 74f, PropertyValue(section, "File size"));
            DrawAssetCell(row, ref x, 92f, PropertyValue(section, "Source"));
            DrawAssetCell(row, ref x, 58f, PropertyValue(section, "References"));
            float actionX = row.xMax - actionWidth;
            for (int i = 0; i < actions.Count; i++) DrawButton(new Rect(actionX, row.y + (i * 28f), actionWidth, 24f), actions[i], false);
        }

        private void DrawAssetCell(Rect row, ref float x, float width, string value)
        {
            GUI.Label(new Rect(x, row.y + 8f, width, row.height - 16f), value ?? string.Empty, _textStyle);
            x += width + 6f;
        }

        private void DrawAssetInventoryRowStacked(ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorItem file)
        {
            Rect heading = GUILayoutUtility.GetRect(0f, 66f, GUILayout.ExpandWidth(true), GUILayout.Height(66f));
            if (file != null && file.PreviewSprite != null) DrawSpritePreview(new Rect(heading.x, heading.y + 5f, 56f, 56f), file.PreviewSprite, file.Emphasized);
            GUI.Label(new Rect(heading.x + 64f, heading.y + 6f, heading.width - 64f, 24f), file != null ? file.Value ?? string.Empty : string.Empty, _sectionTitleStyle);
            if (file != null && !string.IsNullOrEmpty(file.Badge)) ScenarioUiWidgets.DrawPill(new Rect(heading.x + 64f, heading.y + 34f, 84f, 20f), file.Badge, _uiContext.Styles, AssetBadgeEmphasis(file.Badge));
            string facts = PropertyValue(section, "Dimensions") + "  |  " + PropertyValue(section, "File size") + "  |  " + PropertyValue(section, "Source") + "  |  " + PropertyValue(section, "References") + " uses";
            GUILayout.Label(facts, _mutedTextStyle);
            List<ScenarioAuthoringInspectorAction> actions = FindActions(section);
            ScenarioAuthoringInspectorSection actionSection = new ScenarioAuthoringInspectorSection { Items = ToActionItems(actions) };
            DrawUniformActionGrid(actionSection, 120f, 28f);
        }

        private static ScenarioAuthoringInspectorItem[] ToActionItems(List<ScenarioAuthoringInspectorAction> actions)
        {
            ScenarioAuthoringInspectorItem[] items = new ScenarioAuthoringInspectorItem[actions != null ? actions.Count : 0];
            for (int i = 0; i < items.Length; i++) items[i] = new ScenarioAuthoringInspectorItem { Kind = ScenarioAuthoringInspectorItemKind.Action, Action = actions[i] };
            return items;
        }

        private static ScenarioUiPillEmphasis AssetBadgeEmphasis(string badge)
        {
            if (string.Equals(badge, "MISSING", StringComparison.OrdinalIgnoreCase)) return ScenarioUiPillEmphasis.Danger;
            if (string.Equals(badge, "ORPHAN", StringComparison.OrdinalIgnoreCase) || string.Equals(badge, "LARGE", StringComparison.OrdinalIgnoreCase)) return ScenarioUiPillEmphasis.Warning;
            return ScenarioUiPillEmphasis.Success;
        }

        private static ScenarioAuthoringInspectorItem FindProperty(ScenarioAuthoringInspectorSection section, string label)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Kind == ScenarioAuthoringInspectorItemKind.Property && string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        private static string PropertyValue(ScenarioAuthoringInspectorSection section, string label)
        {
            ScenarioAuthoringInspectorItem item = FindProperty(section, label);
            return item != null ? item.Value ?? string.Empty : string.Empty;
        }

        private static List<ScenarioAuthoringInspectorAction> FindActions(ScenarioAuthoringInspectorSection section)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Kind == ScenarioAuthoringInspectorItemKind.Action && item.Action != null) actions.Add(item.Action);
            }
            return actions;
        }
    }
}

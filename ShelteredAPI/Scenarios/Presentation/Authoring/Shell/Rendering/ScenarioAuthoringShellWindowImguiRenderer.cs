using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private Rect DrawWindowCore(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringShellAnimationService.WindowVisualState visual =
                window != null ? _animations.GetWindowVisual(window.Id) : null;
            float alpha = visual != null ? visual.Alpha : 1f;
            float scale = visual != null ? visual.Scale : 1f;
            using (ScenarioUiGuiScope.Apply(alpha, rect, scale))
            {
                bool scaled = Mathf.Abs(scale - 1f) > 0.0001f;
                if (scaled)
                    _scaledWindowDrawDepth++;

                try
                {
                    return DrawWindowCoreUnscoped(rect, window);
                }
                finally
                {
                    if (scaled)
                        _scaledWindowDrawDepth--;
                }
            }
        }

        private Rect DrawWindowCoreUnscoped(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            if (window != null)
            {
                if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase)
                    && _snapshot != null
                    && _snapshot.ShellViewModel != null
                    && _snapshot.ShellViewModel.Settings != null)
                {
                    return DrawSettingsWindow(rect, _snapshot.ShellViewModel.Settings, window);
                }

                if (window.RendererKind == ScenarioAuthoringShellRendererKind.Inspector)
                    return DrawInspectorWindow(rect, window);

                if (window.RendererKind == ScenarioAuthoringShellRendererKind.BottomTray
                    && string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                    return DrawBottomTrayWindow(rect, window);

                if (string.Equals(window.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase))
                    return DrawPixelEditorWindow(rect, window);
            }

            return DrawStandardWindow(rect, window);
        }

        private Rect DrawStandardWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioAuthoringInspectorAction[] secondaryActions = GetHeaderActions(window.HeaderActions, false);
            bool hasSecondaryActions = secondaryActions.Length > 0;
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                window.Title ?? string.Empty,
                null,
                false,
                hasSecondaryActions ? 58f : 30f,
                12f + (chromeActions.Length * 24f));
            Rect headerRect = regions.Header;
            Rect titleRowRect = new Rect(headerRect.x, headerRect.y, headerRect.width, 30f);

            float actionX = titleRowRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                ScenarioAuthoringInspectorAction action = chromeActions[i];
                Rect actionRect = new Rect(actionX, headerRect.y + 3f, 22f, 22f);
                DrawButton(actionRect, action, false);
                actionX -= 24f;
            }

            if (hasSecondaryActions)
            {
                Rect tabsRect = new Rect(headerRect.x + 6f, titleRowRect.yMax + 2f, headerRect.width - 12f, 22f);
                float tabX = tabsRect.x;
                for (int i = 0; i < secondaryActions.Length; i++)
                {
                    ScenarioAuthoringInspectorAction action = secondaryActions[i];
                    float width = Mathf.Clamp(MeasureButtonWidth(action, true, 18f), 42f, 74f);
                    Rect actionRect = new Rect(tabX, tabsRect.y, width, tabsRect.height);
                    DrawButton(actionRect, action, true);
                    tabX = actionRect.xMax + 4f;
                    if (tabX >= tabsRect.xMax)
                        break;
                }
            }

            if (window.Collapsed)
                return RuntimeCompat.ZeroRect();

            Rect bodyRect = regions.Body;
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                DrawSection(window.Sections[i]);
                if (i < window.Sections.Length - 1)
                    GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(window.Id, scrollPosition);
            DrawFloatingResizeGrip(rect, window);
            return bodyRect;
        }

        private void DrawFloatingResizeGrip(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            if (window == null || window.Dock != ScenarioAuthoringShellDock.Floating)
                return;

            Rect gripRect = BuildFloatingResizeRect(rect);
            GUI.Label(gripRect, "///", _mutedTextStyle);
        }

        private Rect DrawInspectorWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                !string.IsNullOrEmpty(window.Title) ? window.Title : "Selection",
                null,
                false,
                34f,
                12f + (chromeActions.Length * 24f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 24f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                DrawButton(new Rect(actionX, headerRect.y + 4f, 22f, 22f), chromeActions[i], false);
                actionX -= 24f;
            }

            Rect bodyRect = regions.Body;
            if (IsEmptyInspector(window))
            {
                DrawEmptyInspectorState(bodyRect);
                DrawFloatingResizeGrip(rect, window);
                return bodyRect;
            }

            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition.x = 0f;
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(120f, bodyRect.width - 18f)));
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                DrawSection(window.Sections[i], true);
                if (i < window.Sections.Length - 1)
                    GUILayout.Space(6f);
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            scrollPosition.x = 0f;
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(window.Id, scrollPosition);
            DrawFloatingResizeGrip(rect, window);
            return bodyRect;
        }

        private static bool IsEmptyInspector(ScenarioAuthoringShellWindowViewModel window)
        {
            return window != null
                && window.Sections != null
                && window.Sections.Length == 1
                && window.Sections[0] != null
                && string.Equals(window.Sections[0].Id, "empty", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawEmptyInspectorState(Rect bodyRect)
        {
            Rect cardRect = new Rect(bodyRect.x + 12f, bodyRect.y + 12f, bodyRect.width - 24f, 112f);
            GUI.Box(cardRect, GUIContent.none, _uiContext.Styles.Section);
            GUI.Label(new Rect(cardRect.x + 14f, cardRect.y + 12f, cardRect.width - 28f, 24f), "Nothing selected", _sectionTitleStyle);
            GUI.Label(
                new Rect(cardRect.x + 14f, cardRect.y + 42f, cardRect.width - 28f, 48f),
                "Pick an object, room, or placed asset in the shelter to inspect its scenario rules.",
                _mutedTextStyle);
        }

        private Rect DrawBottomTrayWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                window.Title ?? "Asset Placement",
                null,
                false,
                34f,
                12f + (chromeActions.Length * 24f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                ScenarioAuthoringInspectorAction action = chromeActions[i];
                Rect actionRect = new Rect(actionX, headerRect.y + 6f, 22f, 22f);
                DrawButton(actionRect, action, false);
                actionX -= 24f;
            }
            Rect bodyRect = regions.Body;
            bool showDetailsPane = bodyRect.width >= 720f;
            float pickerWidth = showDetailsPane
                ? Mathf.Clamp(bodyRect.width * 0.62f, 420f, bodyRect.width - 256f)
                : bodyRect.width;
            Rect pickerRect = new Rect(bodyRect.x, bodyRect.y, pickerWidth, bodyRect.height);
            Rect detailsRect = showDetailsPane
                ? new Rect(pickerRect.xMax + 16f, bodyRect.y, bodyRect.xMax - pickerRect.xMax - 16f, bodyRect.height)
                : RuntimeCompat.ZeroRect();

            Rect filterRect = new Rect(pickerRect.x, pickerRect.y, pickerRect.width, 30f);
            DrawCandidateSearchControl(
                filterRect,
                "build_palette_search",
                ref _buildPaletteSearchText,
                ref _buildPaletteSearchFocused);

            float pickerScrollHeight = ResolveRowBoundedScrollHeight(pickerRect.height - 40f);
            GUILayout.BeginArea(new Rect(pickerRect.x, pickerRect.y + 40f, pickerRect.width, pickerScrollHeight));
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, pickerRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            bool drewCandidateGrid = false;
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (section == null
                    || string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                    || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                    continue;

                DrawSection(section, !showDetailsPane, _buildPaletteSearchText, CandidateFilterAll);
                GUILayout.Space(6f);
                drewCandidateGrid = true;
            }
            if (!drewCandidateGrid)
            {
                string emptyGuidance = "Pick a build tool to see its palette here.";
                for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
                {
                    ScenarioAuthoringInspectorSection section = window.Sections[i];
                    if (section == null
                        || !string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                        || section.Items == null)
                        continue;

                    for (int j = 0; j < section.Items.Length; j++)
                    {
                        ScenarioAuthoringInspectorItem item = section.Items[j];
                        if (item != null
                            && item.Kind == ScenarioAuthoringInspectorItemKind.Action
                            && item.Action != null
                            && item.Action.Emphasized
                            && !string.IsNullOrEmpty(item.Action.Hint))
                        {
                            emptyGuidance = item.Action.Hint;
                            break;
                        }
                    }
                }
                GUILayout.Label(emptyGuidance, _mutedTextStyle);
            }
            if (!showDetailsPane)
            {
                for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
                {
                    ScenarioAuthoringInspectorSection section = window.Sections[i];
                    if (section == null
                        || string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                        || section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                        continue;

                    DrawSection(section);
                    GUILayout.Space(6f);
                }
            }
            GUILayout.Space(24f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(window.Id, scrollPosition);

            if (showDetailsPane)
            {
                float detailsScrollHeight = ResolveRowBoundedScrollHeight(detailsRect.height);
                GUILayout.BeginArea(new Rect(detailsRect.x, detailsRect.y, detailsRect.width, detailsScrollHeight));
                previousContentWidth = _activeContentWidth;
                _activeContentWidth = Math.Max(120f, detailsRect.width - 18f);
                Vector2 detailsScroll = GetWindowScrollPosition(window.Id + ".details");
                detailsScroll = GUILayout.BeginScrollView(detailsScroll, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
                {
                    ScenarioAuthoringInspectorSection section = window.Sections[i];
                    if (section == null
                        || string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                        || section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                        continue;

                    DrawSection(section);
                    GUILayout.Space(6f);
                }
                GUILayout.Space(24f);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                _activeContentWidth = previousContentWidth;
                SetWindowScrollPosition(window.Id + ".details", detailsScroll);
            }
            DrawFloatingResizeGrip(rect, window);
            return bodyRect;
        }

        private static float ResolveRowBoundedScrollHeight(float height)
        {
            const float rowQuantum = 30f;
            if (height <= rowQuantum * 3f)
                return Math.Max(rowQuantum, height);

            return Math.Max(rowQuantum * 3f, Mathf.Floor(height / rowQuantum) * rowQuantum);
        }

        private Rect DrawDocumentModalCore(Rect rect, ScenarioAuthoringInspectorDocument document, string scrollId)
        {
            string title = document != null && !string.IsNullOrEmpty(document.Title)
                ? document.Title.ToUpperInvariant()
                : "DOCUMENT";
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                title,
                document != null ? document.Subtitle : null,
                false,
                46f,
                0f);
            Rect bodyRect = regions.Body;
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(scrollId);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (string.Equals(scrollId, "sprite_picker", StringComparison.Ordinal))
            {
                DrawCandidateFilterControls(
                    GUILayoutUtility.GetRect(120f, 64f, GUILayout.ExpandWidth(true), GUILayout.Height(64f)),
                    "sprite_picker_search",
                    ref _spritePickerSearchText,
                    ref _spritePickerCandidateFilter,
                    ref _spritePickerSearchFocused);
                GUILayout.Space(6f);
            }

            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
            {
                if (string.Equals(scrollId, "sprite_picker", StringComparison.Ordinal))
                    DrawSection(document.Sections[i], false, _spritePickerSearchText, _spritePickerCandidateFilter);
                else
                    DrawSection(document.Sections[i]);
                if (i < document.Sections.Length - 1)
                    GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(scrollId, scrollPosition);
            return bodyRect;
        }

        private Rect DrawPixelEditorWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor =
                _snapshot != null && _snapshot.ShellViewModel != null
                    ? _snapshot.ShellViewModel.CustomSpriteEditor
                    : null;

            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                editor != null && editor.Dirty ? "Pixel Editor *" : "Pixel Editor",
                editor != null ? editor.SourceLabel : null,
                false,
                34f,
                12f + (chromeActions.Length * 24f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                Rect actionRect = new Rect(actionX, headerRect.y + 6f, 22f, 22f);
                DrawButton(actionRect, chromeActions[i], false);
                actionX -= 24f;
            }

            Rect bodyRect = regions.Body;
            if (editor == null || !editor.Visible)
            {
                GUI.Label(bodyRect, "Open Edit Pixels from the sprite browser to start a pixel editing session.", _mutedTextStyle);
                DrawFloatingResizeGrip(rect, window);
                return bodyRect;
            }

            DrawCustomSpriteEditorDedicated(bodyRect, editor);
            DrawFloatingResizeGrip(rect, window);
            return bodyRect;
        }

        private void DrawCustomSpriteEditorDedicated(Rect bodyRect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            float controlsWidth = Mathf.Clamp(bodyRect.width * 0.28f, 180f, 270f);
            float footerHeight = 42f;
            Rect toolsRect = new Rect(bodyRect.x, bodyRect.y, controlsWidth, Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect canvasPane = new Rect(toolsRect.xMax + 10f, bodyRect.y, Math.Max(160f, bodyRect.xMax - toolsRect.xMax - 10f), Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect footerRect = new Rect(bodyRect.x, bodyRect.yMax - footerHeight, bodyRect.width, footerHeight);

            GUILayout.BeginArea(toolsRect);
            GUILayout.BeginVertical(_uiContext.Styles.Section, GUILayout.Width(toolsRect.width), GUILayout.Height(toolsRect.height));
            GUILayout.Label(editor.IsCharacterEditor ? "Character Pixels" : "Sprite Pixels", _sectionTitleStyle);
            GUILayout.Label(editor.Dirty ? "Unsaved changes" : "No unsaved changes", _mutedTextStyle);
            if (editor.IsCharacterEditor)
            {
                DrawCharacterPartToolbar(editor);
                GUILayout.Space(6f);
            }
            DrawCustomEditorToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomClipboardToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomZoomToolbar(editor);
            GUILayout.Space(6f);
            GUILayout.Label("Zoom " + Mathf.RoundToInt(Mathf.Max(1f, editor.Zoom) * 100f) + "%", _smallTitleStyle);
            GUILayout.Label("Color", _smallTitleStyle);
            Rect colorRect = GUILayoutUtility.GetRect(112f, 40f, GUILayout.Width(112f), GUILayout.Height(40f));
            DrawColorPreview(colorRect, editor.ActiveColor);
            GUILayout.Label("#" + (editor.ActiveColorHex ?? "000000FF"), _textStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f), GUILayout.Height(22f));
                DrawBrushSwatch(swatchRect, editor.BrushPalette[i], i == editor.ActiveBrushIndex, i);
                GUILayout.Space(3f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            DrawColorSlider("R", editor, 0);
            DrawColorSlider("G", editor, 1);
            DrawColorSlider("B", editor, 2);
            DrawColorSlider("A", editor, 3);
            GUILayout.Space(6f);
            GUILayout.Label(BuildSelectionSummary(editor), _mutedTextStyle);
            GUILayout.Label(BuildClipboardSummary(editor), _mutedTextStyle);
            GUILayout.EndVertical();
            GUILayout.EndArea();

            DrawPixelCanvasViewport(canvasPane, editor);

            GUILayout.BeginArea(footerRect);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, "Save", editor.Dirty, 96f, "Save the current pixel edit.");
            GUILayout.Space(8f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, "Discard", false, 96f, "Discard the current pixel edit.", editor.Dirty);
            GUILayout.Space(8f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, "Close", false, 96f, editor.Dirty ? "Save or discard before closing." : "Close the sprite editor.", !editor.Dirty);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCustomSpriteEditor()
        {
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor =
                _snapshot != null && _snapshot.ShellViewModel != null
                    ? _snapshot.ShellViewModel.CustomSpriteEditor
                    : null;
            if (editor == null || !editor.Visible)
                return;

            GUILayout.Space(6f);
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            GUILayout.Label(editor.IsCharacterEditor ? "Character Pixel Editor" : "Pixel Editor", _sectionTitleStyle);
            GUILayout.Label(
                "Source: " + (editor.SourceLabel ?? "<sprite>") + (editor.Dirty ? " | Modified" : " | Unchanged"),
                _mutedTextStyle);
            float contentWidth = GetSectionContentWidth();
            bool stackedLayout = contentWidth < 680f;
            float controlsWidth = Mathf.Clamp(contentWidth, 120f, 308f);
            if (stackedLayout)
                GUILayout.BeginVertical();
            else
                GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(controlsWidth));
            if (editor.IsCharacterEditor)
            {
                DrawCharacterPartToolbar(editor);
                GUILayout.Space(6f);
            }
            DrawCustomEditorToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomClipboardToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomZoomToolbar(editor);
            GUILayout.Space(6f);
            GUILayout.Label("Active Color", _smallTitleStyle);
            Rect colorRect = GUILayoutUtility.GetRect(112f, 44f, GUILayout.Width(112f), GUILayout.Height(44f));
            DrawColorPreview(colorRect, editor.ActiveColor);
            GUILayout.Label("#" + (editor.ActiveColorHex ?? "000000FF"), _textStyle);
            GUILayout.Label(BuildSelectionSummary(editor), _mutedTextStyle);
            GUILayout.Label(BuildClipboardSummary(editor), _mutedTextStyle);
            if (editor.IsCharacterEditor)
                GUILayout.Label("Editing: " + (editor.CharacterPartLabel ?? "Part"), _mutedTextStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f), GUILayout.Height(24f));
                DrawBrushSwatch(swatchRect, editor.BrushPalette[i], i == editor.ActiveBrushIndex, i);
                GUILayout.Space(4f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            DrawColorSlider("R", editor, 0);
            DrawColorSlider("G", editor, 1);
            DrawColorSlider("B", editor, 2);
            DrawColorSlider("A", editor, 3);
            GUILayout.Space(6f);
            GUILayout.Label(BuildToolHint(editor.ActiveTool), _mutedTextStyle);
            GUILayout.EndVertical();

            GUILayout.Space(stackedLayout ? 8f : 10f);
            GUILayout.BeginVertical();
            float zoom = Mathf.Max(1f, editor.Zoom);
            float width = Mathf.Max(1f, editor.Width * zoom);
            float height = Mathf.Max(1f, editor.Height * zoom);
            float viewportWidth = stackedLayout ? contentWidth : Math.Max(260f, contentWidth - controlsWidth - 18f);
            float viewportHeight = Mathf.Clamp(height + 18f, 180f, stackedLayout ? 360f : 420f);
            GUILayout.Label("Canvas " + editor.Width + "x" + editor.Height + " @ " + editor.Zoom + "x", _smallTitleStyle);
            GUILayout.Label("Mouse wheel zooms. Right click always samples color.", _mutedTextStyle);
            Vector2 canvasScroll = GetWindowScrollPosition("custom_sprite_canvas");
            canvasScroll = GUILayout.BeginScrollView(canvasScroll, true, true, GUILayout.Width(viewportWidth), GUILayout.Height(viewportHeight));
            Rect canvasRect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            DrawPixelCanvas(canvasRect, editor);
            GUILayout.EndScrollView();
            SetWindowScrollPosition("custom_sprite_canvas", canvasScroll);
            GUILayout.EndVertical();
            if (stackedLayout)
                GUILayout.EndVertical();
            else
                GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCustomEditorToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint,
                "Paint",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Paint,
                92f,
                "Paint pixels using the active color.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick,
                "Pick",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick,
                92f,
                "Sample a pixel color from the canvas.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect,
                "Select",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select,
                92f,
                "Drag a rectangular pixel selection.");
            GUILayout.EndHorizontal();
        }

        private void DrawCustomClipboardToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy,
                "Copy",
                false,
                92f,
                "Copy the current selection. If nothing is selected, copy the whole sprite.",
                true);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste,
                "Paste",
                editor.HasClipboard,
                92f,
                editor.HasClipboard ? "Paste the pixel clipboard into the canvas." : "Pixel clipboard is empty.",
                editor.HasClipboard);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear,
                "Clear Sel",
                editor.HasSelection,
                92f,
                editor.HasSelection ? "Clear the current pixel selection." : "There is no active selection.",
                editor.HasSelection);
            GUILayout.EndHorizontal();
        }

        private void DrawCustomZoomToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut,
                "Zoom -",
                false,
                92f,
                "Zoom out of the canvas.",
                editor.Zoom > 1);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset,
                editor.Zoom + "x",
                false,
                68f,
                "Reset canvas zoom to 8x.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn,
                "Zoom +",
                false,
                92f,
                "Zoom into the canvas.",
                editor.Zoom < 48);
            GUILayout.EndHorizontal();
        }

        private void DrawCharacterPartToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead,
                "Head",
                editor.CharacterPart == ScenarioCharacterTexturePart.Head,
                92f,
                "Edit the head texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso,
                "Torso",
                editor.CharacterPart == ScenarioCharacterTexturePart.Torso,
                92f,
                "Edit the torso texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs,
                "Legs",
                editor.CharacterPart == ScenarioCharacterTexturePart.Legs,
                92f,
                "Edit the legs texture for this family member.");
            GUILayout.EndHorizontal();
        }

        private void DrawBrushSwatch(Rect rect, Color color, bool active, int brushIndex)
        {
            Color previous = GUI.color;
            GUI.color = color.a <= 0.001f ? new Color(0f, 0f, 0f, 0.2f) : color;
            GUI.Box(rect, GUIContent.none, active ? _activeButtonStyle : _uiContext.Styles.Field);
            GUI.color = previous;

            if (color.a <= 0.001f)
                GUI.Label(rect, "X", _mutedTextStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioSpriteSwapAuthoringService.BuildCustomPresetActionId(brushIndex));
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private void DrawInlineAction(string actionId, string label, bool emphasized, float width, string hint, bool enabled = true)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.Height(28f));
            DrawButton(rect, new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = label,
                Hint = hint,
                Enabled = enabled,
                Emphasized = emphasized
            }, false);
        }

        private void DrawColorSlider(string label, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, int channel)
        {
            Color activeColor = editor.ActiveColor;
            float currentValue = channel == 0
                ? activeColor.r
                : (channel == 1 ? activeColor.g : (channel == 2 ? activeColor.b : activeColor.a));
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _textStyle, GUILayout.Width(18f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0f, 1f, GUILayout.Width(184f));
            GUILayout.Label(Mathf.RoundToInt(nextValue * 255f).ToString(), _mutedTextStyle, GUILayout.Width(34f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(nextValue - currentValue) <= 0.0001f)
                return;

            Color updatedColor = activeColor;
            if (channel == 0) updatedColor.r = nextValue;
            else if (channel == 1) updatedColor.g = nextValue;
            else if (channel == 2) updatedColor.b = nextValue;
            else updatedColor.a = nextValue;

            ScenarioAuthoringBackendService.Instance.ExecuteAction(
                ScenarioSpriteSwapAuthoringService.BuildCustomColorActionId(updatedColor));
            if (Event.current != null)
                Event.current.Use();
        }

        private void DrawColorPreview(Rect rect, Color color)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            Rect fillRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            DrawCheckerboard(fillRect, 6);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawPixelCanvas(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            DrawPixelCanvas(rect, editor, Mathf.Max(1f, editor.Zoom));
        }

        private void DrawPixelCanvasViewport(Rect viewportRect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUI.Box(viewportRect, GUIContent.none, _uiContext.Styles.Section);
            Rect inner = new Rect(viewportRect.x + 10f, viewportRect.y + 10f, viewportRect.width - 20f, viewportRect.height - 20f);
            if (editor.Width <= 0 || editor.Height <= 0)
            {
                GUI.Label(inner, "No sprite pixels available.", _mutedTextStyle);
                return;
            }

            float fitZoom = Mathf.Min(inner.width / editor.Width, inner.height / editor.Height);
            float displayZoom = Mathf.Clamp(Mathf.Min(Mathf.Max(1f, editor.Zoom), fitZoom), 1f, 64f);
            float canvasWidth = editor.Width * displayZoom;
            float canvasHeight = editor.Height * displayZoom;
            Vector2 overflow = new Vector2(Math.Max(0f, canvasWidth - inner.width), Math.Max(0f, canvasHeight - inner.height));
            _pixelEditorPan = new Vector2(
                Mathf.Clamp(_pixelEditorPan.x, -overflow.x * 0.5f, overflow.x * 0.5f),
                Mathf.Clamp(_pixelEditorPan.y, -overflow.y * 0.5f, overflow.y * 0.5f));

            Rect canvasRect = new Rect(
                inner.x + ((inner.width - canvasWidth) * 0.5f) + _pixelEditorPan.x,
                inner.y + ((inner.height - canvasHeight) * 0.5f) + _pixelEditorPan.y,
                canvasWidth,
                canvasHeight);

            Event current = Event.current;
            if (current != null && inner.Contains(current.mousePosition))
            {
                bool panButton = current.button == 2 || (current.button == 0 && current.modifiers == EventModifiers.Alt);
                if (current.type == EventType.MouseDown && panButton && (overflow.x > 0f || overflow.y > 0f))
                {
                    _pixelEditorPanning = true;
                    _pixelEditorPanStartMouse = current.mousePosition;
                    _pixelEditorPanStart = _pixelEditorPan;
                    current.Use();
                }
                else if (_pixelEditorPanning && current.type == EventType.MouseDrag)
                {
                    _pixelEditorPan = _pixelEditorPanStart + (current.mousePosition - _pixelEditorPanStartMouse);
                    current.Use();
                }
                else if (_pixelEditorPanning && current.type == EventType.MouseUp)
                {
                    _pixelEditorPanning = false;
                    current.Use();
                }
            }

            GUI.BeginGroup(inner);
            Rect localCanvasRect = new Rect(canvasRect.x - inner.x, canvasRect.y - inner.y, canvasRect.width, canvasRect.height);
            DrawPixelCanvas(localCanvasRect, editor, displayZoom);
            GUI.EndGroup();

            GUI.Label(new Rect(inner.x + 8f, inner.yMax - 24f, inner.width - 16f, 20f),
                editor.Width + "x" + editor.Height + " | " + Mathf.RoundToInt(Mathf.Max(1f, editor.Zoom) * 100f) + "% | wheel zoom, middle-drag pan",
                _mutedTextStyle);
        }

        private void DrawPixelCanvas(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float displayZoom)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            if (editor.PreviewSprite == null || editor.PreviewSprite.texture == null)
            {
                GUI.Label(rect, "No Sprite", _mutedTextStyle);
                return;
            }

            if (editor.Checkerboard)
                DrawCheckerboard(rect, Mathf.RoundToInt(displayZoom));

            GUI.DrawTextureWithTexCoords(rect, editor.PreviewSprite.texture, new Rect(0f, 0f, 1f, 1f), true);
            DrawPixelGrid(rect, editor, displayZoom);
            DrawSelectionOverlay(rect, editor, displayZoom);

            Event current = Event.current;
            if (current != null && rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.ScrollWheel)
                {
                    string zoomActionId = current.delta.y < 0f
                        ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn
                        : ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut;
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(zoomActionId);
                    current.Use();
                    return;
                }

                int pixelX;
                int pixelY;
                if (!TryGetCanvasPixel(rect, editor, current.mousePosition, displayZoom, out pixelX, out pixelY))
                    return;

                string actionId = null;
                if (current.button == 1)
                {
                    if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomPickActionId(pixelX, pixelY);
                }
                else if (editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select)
                {
                    if (current.type == EventType.MouseDown)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectStartActionId(pixelX, pixelY);
                    else if (current.type == EventType.MouseDrag)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectDragActionId(pixelX, pixelY);
                    else if (current.type == EventType.MouseUp)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectEndActionId(pixelX, pixelY);
                }
                else if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                {
                    actionId = editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick
                        ? ScenarioSpriteSwapAuthoringService.BuildCustomPickActionId(pixelX, pixelY)
                        : ScenarioSpriteSwapAuthoringService.BuildCustomPaintActionId(pixelX, pixelY);
                }

                if (!string.IsNullOrEmpty(actionId))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
                    current.Use();
                }
            }
        }

        private static bool TryGetCanvasPixel(
            Rect rect,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor,
            Vector2 pointer,
            float displayZoom,
            out int pixelX,
            out int pixelY)
        {
            pixelX = Mathf.Clamp(
                Mathf.FloorToInt((pointer.x - rect.x) / Mathf.Max(1f, displayZoom)),
                0,
                Mathf.Max(0, editor.Width - 1));
            pixelY = Mathf.Clamp(
                editor.Height - 1 - Mathf.FloorToInt((pointer.y - rect.y) / Mathf.Max(1f, displayZoom)),
                0,
                Mathf.Max(0, editor.Height - 1));
            return editor.Width > 0 && editor.Height > 0;
        }

        private void DrawPixelGrid(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            DrawPixelGrid(rect, editor, Mathf.Max(1f, editor.Zoom));
        }

        private void DrawPixelGrid(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float displayZoom)
        {
            if (displayZoom < 8 || editor.Width <= 0 || editor.Height <= 0)
                return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            for (int x = 1; x < editor.Width; x++)
            {
                float lineX = rect.x + (x * displayZoom);
                GUI.DrawTexture(new Rect(lineX, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            }

            for (int y = 1; y < editor.Height; y++)
            {
                float lineY = rect.y + (y * displayZoom);
                GUI.DrawTexture(new Rect(rect.x, lineY, rect.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void DrawSelectionOverlay(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            DrawSelectionOverlay(rect, editor, Mathf.Max(1f, editor.Zoom));
        }

        private void DrawSelectionOverlay(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float displayZoom)
        {
            if (!editor.HasSelection || editor.SelectionWidth <= 0 || editor.SelectionHeight <= 0)
                return;

            float zoom = Mathf.Max(1f, displayZoom);
            Rect selectionRect = new Rect(
                rect.x + (editor.SelectionX * zoom),
                rect.y + ((editor.Height - (editor.SelectionY + editor.SelectionHeight)) * zoom),
                editor.SelectionWidth * zoom,
                editor.SelectionHeight * zoom);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.83f, 0.23f, 0.18f);
            GUI.DrawTexture(selectionRect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.87f, 0.30f, 1f);
            DrawRectBorder(selectionRect, 2f);
            GUI.color = previous;
        }

        private static void DrawRectBorder(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private static string BuildSelectionSummary(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.HasSelection)
                return "Selection: none";

            return "Selection: " + editor.SelectionWidth + "x" + editor.SelectionHeight
                + " at (" + editor.SelectionX + ", " + editor.SelectionY + ")";
        }

        private static string BuildClipboardSummary(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.HasClipboard)
                return "Clipboard: empty";

            return "Clipboard: " + editor.ClipboardWidth + "x" + editor.ClipboardHeight;
        }

        private static string BuildToolHint(ScenarioSpriteSwapAuthoringService.CustomEditorTool tool)
        {
            if (tool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick)
                return "Pick tool: click pixels to sample their exact RGBA color. Right click always samples.";
            if (tool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select)
                return "Select tool: drag a rectangle, then use Copy and Paste to move pixel regions.";

            return "Paint tool: drag to paint individual pixels. If a selection exists, painting is limited to it.";
        }

        private void DrawCheckerboard(Rect rect, int zoom)
        {
            int tile = Mathf.Max(4, zoom);
            Color previous = GUI.color;
            for (int y = 0; y < rect.height; y += tile)
            {
                for (int x = 0; x < rect.width; x += tile)
                {
                    bool dark = (((x / tile) + (y / tile)) % 2) == 0;
                    GUI.color = dark
                        ? new Color(0.22f, 0.20f, 0.18f, 1f)
                        : new Color(0.33f, 0.30f, 0.27f, 1f);
                    GUI.DrawTexture(new Rect(rect.x + x, rect.y + y, tile, tile), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
        }

        private void DrawCandidateFilterControls(
            Rect rect,
            string controlName,
            ref string searchText,
            ref string candidateFilter,
            ref bool searchFocused)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(26f));
            GUI.SetNextControlName(controlName);
            string nextSearchText = GUILayout.TextField(searchText ?? string.Empty, _uiContext.Styles.Field, GUILayout.Height(26f));
            if (!string.Equals(nextSearchText, searchText ?? string.Empty, StringComparison.Ordinal))
                searchText = nextSearchText;

            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(64f), GUILayout.Height(26f)))
                searchText = string.Empty;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawCandidateFilterButton("All", CandidateFilterAll, ref candidateFilter);
            DrawCandidateFilterButton("Active", CandidateFilterActive, ref candidateFilter);
            DrawCandidateFilterButton("Vanilla", CandidateFilterVanilla, ref candidateFilter);
            DrawCandidateFilterButton("Scenario", CandidateFilterScenario, ref candidateFilter);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            searchFocused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
        }

        private void DrawCandidateSearchControl(
            Rect rect,
            string controlName,
            ref string searchText,
            ref bool searchFocused)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(26f));
            GUI.SetNextControlName(controlName);
            string nextSearchText = GUILayout.TextField(searchText ?? string.Empty, _uiContext.Styles.Field, GUILayout.Height(26f));
            if (!string.Equals(nextSearchText, searchText ?? string.Empty, StringComparison.Ordinal))
                searchText = nextSearchText;

            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(64f), GUILayout.Height(26f)))
                searchText = string.Empty;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            searchFocused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
        }

        private void DrawCandidateFilterButton(string label, string value, ref string candidateFilter)
        {
            bool active = string.Equals(candidateFilter, value, StringComparison.OrdinalIgnoreCase);
            if (GUILayout.Button(label, active ? _activeButtonStyle : _buttonStyle, GUILayout.Width(78f), GUILayout.Height(26f)))
                candidateFilter = value;
        }

        private void DrawSection(ScenarioAuthoringInspectorSection section)
        {
            DrawSection(section, false);
        }

        private void DrawSection(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            DrawSection(section, compactInspector, null, CandidateFilterAll);
        }

        private void DrawSection(
            ScenarioAuthoringInspectorSection section,
            bool compactInspector,
            string candidateSearchText,
            string candidateFilter)
        {
            if (section == null)
                return;

            GUILayout.BeginVertical(_uiContext.Styles.Section);
            if (!string.IsNullOrEmpty(section.Title))
                GUILayout.Label(section.Title, _sectionTitleStyle);

            if (IsHomeQuestionSection(section))
            {
                DrawHomeQuestionCard(section);
            }
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.ActionStrip || section.Layout == ScenarioAuthoringInspectorSectionLayout.TabStrip)
            {
                bool renderAsTabs = section.Layout == ScenarioAuthoringInspectorSectionLayout.TabStrip;
                float rowLimit = GetSectionContentWidth();
                float rowWidth = 0f;
                GUILayout.BeginHorizontal();
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action == null)
                        continue;

                    float width = Mathf.Clamp(
                        MeasureButtonWidth(item.Action, renderAsTabs, 20f),
                        renderAsTabs ? 72f : 94f,
                        renderAsTabs ? 148f : 184f);
                    if (rowWidth > 0f && rowWidth + width > rowLimit)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.Space(4f);
                        GUILayout.BeginHorizontal();
                        rowWidth = 0f;
                    }

                    Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                    DrawButton(rect, item.Action, renderAsTabs);
                    GUILayout.Space(4f);
                    rowWidth += width + 4f;
                }
                GUILayout.EndHorizontal();

                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action != null)
                        continue;

                    DrawItem(item, compactInspector);
                }
            }
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
            {
                int totalCandidates = CountCandidateActions(section);
                int visibleCandidates = CountFilteredCandidateActions(section, candidateSearchText, candidateFilter);
                GUILayout.Label("Results " + visibleCandidates + " / " + totalCandidates, _mutedTextStyle);

                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action != null)
                        continue;
                    if (item.Kind == ScenarioAuthoringInspectorItemKind.Property
                        && string.Equals(item.Label, "Count", StringComparison.OrdinalIgnoreCase))
                        continue;

                    DrawItem(item, compactInspector);
                }

                float availableWidth = GetSectionContentWidth();
                float cardGap = 4f;
                float preferredCardWidth = compactInspector ? 160f : 176f;
                float minCardWidth = compactInspector ? 148f : 160f;
                int maxColumns = compactInspector ? 2 : 4;
                int columns = Mathf.Clamp(
                    Mathf.FloorToInt((availableWidth + cardGap) / (minCardWidth + cardGap)),
                    1,
                    maxColumns);
                float cardWidth = Math.Min(preferredCardWidth, (availableWidth - (cardGap * (columns - 1))) / columns);
                cardWidth = Mathf.Clamp(cardWidth, 128f, preferredCardWidth);
                int count = 0;
                if (visibleCandidates == 0)
                    GUILayout.Label("No candidates match the current search.", _mutedTextStyle);

                GUILayout.BeginHorizontal();
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action == null)
                        continue;
                    if (!CandidateActionMatches(section, item.Action, candidateSearchText, candidateFilter))
                        continue;

                    Rect rect = GUILayoutUtility.GetRect(cardWidth, 84f, GUILayout.Width(cardWidth), GUILayout.Height(84f));
                    DrawCandidateCard(rect, item.Action);
                    count++;
                    if (count % columns == 0 && HasMoreVisibleCandidate(section, i + 1, candidateSearchText, candidateFilter))
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    else
                    {
                        GUILayout.Space(cardGap);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                    DrawItem(section.Items[i], compactInspector);
            }
            GUILayout.EndVertical();
        }

        private static bool IsHomeQuestionSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && !string.IsNullOrEmpty(section.Id)
                && section.Id.StartsWith("home_", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(section.Id, "home_identity", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(section.Id, "home_quick_actions", StringComparison.OrdinalIgnoreCase)
                && section.Layout == ScenarioAuthoringInspectorSectionLayout.ActionStrip;
        }

        private void DrawHomeQuestionCard(ScenarioAuthoringInspectorSection section)
        {
            ScenarioAuthoringInspectorAction action = null;
            string detail = string.Empty;
            string badge = string.Empty;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (item.Action != null)
                    action = item.Action;
                else if (string.IsNullOrEmpty(detail))
                    detail = item.Value ?? item.Label ?? string.Empty;
                else if (string.IsNullOrEmpty(badge))
                    badge = item.Value ?? item.Label ?? string.Empty;
            }

            if (action == null)
                return;

            Rect rect = GUILayoutUtility.GetRect(120f, 82f, GUILayout.ExpandWidth(true), GUILayout.Height(82f));
            DrawButton(rect, new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Label = string.Empty,
                Hint = action.Hint ?? action.Detail,
                Detail = action.Detail,
                Enabled = action.Enabled,
                Emphasized = action.Emphasized
            }, false);

            float sideWidth = 92f;
            if (!string.IsNullOrEmpty(badge))
            {
                Vector2 measuredBadge = _mutedTextStyle.CalcSize(new GUIContent(badge));
                sideWidth = Mathf.Clamp(measuredBadge.x + 24f, 92f, Math.Min(220f, rect.width * 0.36f));
            }

            Rect textRect = new Rect(rect.x + 14f, rect.y + 9f, rect.width - sideWidth - 32f, rect.height - 18f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), ShortenToFit(section.Title ?? string.Empty, textRect.width, _sectionTitleStyle), _sectionTitleStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 27f, textRect.width, 34f), detail ?? string.Empty, _mutedTextStyle);
            if (!string.IsNullOrEmpty(badge))
            {
                Rect badgeRect = new Rect(rect.xMax - sideWidth - 14f, rect.y + 16f, sideWidth, 22f);
                ScenarioUiWidgets.DrawPill(badgeRect, badge, _uiContext.Styles, ScenarioUiPillEmphasis.Default);
            }
            GUI.Label(new Rect(rect.xMax - sideWidth - 14f, rect.yMax - 34f, sideWidth, 20f), action.Label ?? string.Empty, _mutedTextStyle);
        }

        private void DrawItem(ScenarioAuthoringInspectorItem item)
        {
            DrawItem(item, false);
        }

        private void DrawItem(ScenarioAuthoringInspectorItem item, bool compactInspector)
        {
            if (item == null)
                return;

            if (item.PreviewSprite != null || !string.IsNullOrEmpty(item.Detail) || !string.IsNullOrEmpty(item.Badge))
            {
                DrawRichItem(item);
                return;
            }

            switch (item.Kind)
            {
                case ScenarioAuthoringInspectorItemKind.Property:
                    string value = compactInspector ? Shorten(item.Value, 34) : item.Value;
                    float rowHeight = CalculateKeyValueRowHeight(item.Label, value);
                    Rect rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
                    ScenarioUiWidgets.DrawKeyValueRow(rowRect, item.Label, value, _uiContext.Styles);
                    break;
                case ScenarioAuthoringInspectorItemKind.Action:
                    if (item.Action != null)
                    {
                        float width = Mathf.Clamp(MeasureButtonWidth(item.Action, false, 24f), 96f, GetSectionContentWidth());
                        Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                        DrawButton(rect, item.Action, false);
                    }
                    break;
                default:
                    GUILayout.Label(item.Value ?? string.Empty, _textStyle);
                    break;
            }
        }

        private float CalculateKeyValueRowHeight(string label, string value)
        {
            float rowWidth = GetSectionContentWidth();
            float gap = _uiContext != null ? _uiContext.Styles.Theme.Metrics.PaddingSm : 6f;
            float labelWidth = Math.Max(42f, (rowWidth - gap) / 2.4f);
            float valueWidth = Math.Max(60f, rowWidth - labelWidth - gap);
            float labelHeight = _mutedTextStyle != null ? _mutedTextStyle.CalcHeight(new GUIContent(label ?? string.Empty), labelWidth) : 20f;
            float valueHeight = _textStyle != null ? _textStyle.CalcHeight(new GUIContent(value ?? string.Empty), valueWidth) : 20f;
            return Mathf.Clamp(Math.Max(24f, Math.Max(labelHeight, valueHeight)), 24f, 72f);
        }

        private void DrawRichItem(ScenarioAuthoringInspectorItem item)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            bool hasPreview = item.PreviewSprite != null;
            float rowHeight = hasPreview ? 92f : 78f;
            Rect rowRect = GUILayoutUtility.GetRect(120f, rowHeight, GUILayout.ExpandWidth(true));
            Rect textRect;
            if (hasPreview && rowRect.width >= 220f)
            {
                Rect previewRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 84f, rowRect.height - 12f);
                DrawSpritePreview(previewRect, item.PreviewSprite, item.Emphasized);
                textRect = new Rect(previewRect.xMax + 12f, rowRect.y + 6f, rowRect.width - previewRect.width - 18f, rowRect.height - 12f);
            }
            else
            {
                textRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, rowRect.width - 12f, rowRect.height - 12f);
            }

            string title = item.Value ?? string.Empty;
            string detail = item.Kind == ScenarioAuthoringInspectorItemKind.Property
                ? CombineDetail(item.Label, item.Detail)
                : item.Detail;
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), ShortenToFit(title, textRect.width, _sectionTitleStyle), _sectionTitleStyle);
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + 26f, textRect.width, 34f), detail, _mutedTextStyle);

            if (!string.IsNullOrEmpty(item.Badge))
            {
                Vector2 badgeSize = _mutedTextStyle.CalcSize(new GUIContent(item.Badge));
                Rect badgeRect = new Rect(textRect.x, rowRect.yMax - 26f, Mathf.Max(56f, badgeSize.x + 18f), 20f);
                ScenarioUiWidgets.DrawPill(badgeRect, item.Badge, _uiContext.Styles, item.Emphasized ? ScenarioUiPillEmphasis.Active : ScenarioUiPillEmphasis.Default);
            }
            GUILayout.EndVertical();
        }

        private void DrawCandidateCard(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return;

            GUIStyle style = !action.Enabled ? _uiContext.Styles.ButtonDisabled : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
            bool manualHighlightEnabled = _scaledWindowDrawDepth == 0;
            bool hovered = manualHighlightEnabled && rect.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero);
            bool pressed = hovered && Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (GUI.Button(rect, GUIContent.none, style) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            DrawButtonAnimationOverlay(rect, action.Id, action.Enabled, hovered, pressed);

            Rect textRect;
            if (action.PreviewSprite != null && rect.width >= 150f)
            {
                Rect previewRect = new Rect(rect.x + 6f, rect.y + 6f, 70f, rect.height - 12f);
                DrawSpritePreview(previewRect, action.PreviewSprite, action.Emphasized);
                textRect = new Rect(previewRect.xMax + 10f, rect.y + 8f, rect.width - previewRect.width - 22f, rect.height - 16f);
            }
            else
            {
                textRect = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);
            }

            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f), ShortenToFit(action.Label ?? string.Empty, textRect.width, _textStyle), _textStyle);
            string detail = !string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + 22f, textRect.width, 30f), detail, _mutedTextStyle);

            if (!string.IsNullOrEmpty(action.Badge))
            {
                Vector2 badgeSize = _mutedTextStyle.CalcSize(new GUIContent(action.Badge));
                Rect badgeRect = new Rect(textRect.x, rect.yMax - 22f, Mathf.Max(52f, badgeSize.x + 16f), 18f);
                ScenarioUiWidgets.DrawPill(badgeRect, action.Badge, _uiContext.Styles, action.Emphasized ? ScenarioUiPillEmphasis.Active : ScenarioUiPillEmphasis.Default);
            }
        }

        private static int CountCandidateActions(ScenarioAuthoringInspectorSection section)
        {
            int count = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                if (section.Items[i] != null && section.Items[i].Action != null)
                    count++;
            }

            return count;
        }

        private float GetSectionContentWidth()
        {
            return Math.Max(120f, _activeContentWidth - 24f);
        }

        private static bool HasMoreVisibleCandidate(
            ScenarioAuthoringInspectorSection section,
            int startIndex,
            string searchText,
            string candidateFilter)
        {
            for (int i = Math.Max(0, startIndex); section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null
                    && item.Action != null
                    && CandidateActionMatches(section, item.Action, searchText, candidateFilter))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountFilteredCandidateActions(
            ScenarioAuthoringInspectorSection section,
            string searchText,
            string candidateFilter)
        {
            int count = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Action != null && CandidateActionMatches(section, item.Action, searchText, candidateFilter))
                    count++;
            }

            return count;
        }

        private static bool CandidateActionMatches(
            ScenarioAuthoringInspectorSection section,
            ScenarioAuthoringInspectorAction action,
            string searchText,
            string candidateFilter)
        {
            if (action == null)
                return false;

            if (!CandidateMatchesFilter(section, action, candidateFilter))
                return false;

            return CandidateMatchesSearch(action, searchText);
        }

        private static bool CandidateMatchesFilter(
            ScenarioAuthoringInspectorSection section,
            ScenarioAuthoringInspectorAction action,
            string candidateFilter)
        {
            string filter = string.IsNullOrEmpty(candidateFilter) ? CandidateFilterAll : candidateFilter;
            if (string.Equals(filter, CandidateFilterAll, StringComparison.OrdinalIgnoreCase))
                return true;

            string sectionId = section != null ? section.Id ?? string.Empty : string.Empty;
            string badge = action != null ? action.Badge ?? string.Empty : string.Empty;
            if (string.Equals(filter, CandidateFilterActive, StringComparison.OrdinalIgnoreCase))
            {
                return action.Emphasized
                    || StringContains(badge, "active")
                    || StringContains(badge, "saved")
                    || StringContains(badge, "preview");
            }

            if (string.Equals(filter, CandidateFilterVanilla, StringComparison.OrdinalIgnoreCase))
            {
                return StringContains(sectionId, "vanilla")
                    || StringContains(badge, "live");
            }

            if (string.Equals(filter, CandidateFilterScenario, StringComparison.OrdinalIgnoreCase))
            {
                return StringContains(sectionId, "modded")
                    || StringContains(sectionId, "scenario")
                    || StringContains(badge, "mod")
                    || StringContains(badge, "user");
            }

            return true;
        }

        private static bool CandidateMatchesSearch(ScenarioAuthoringInspectorAction action, string searchText)
        {
            string normalized = (searchText ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return true;

            string haystack = ((action.Label ?? string.Empty) + " "
                + (action.Detail ?? string.Empty) + " "
                + (action.Hint ?? string.Empty) + " "
                + (action.Badge ?? string.Empty) + " "
                + (action.IconText ?? string.Empty)).ToLowerInvariant();
            string[] tokens = normalized.ToLowerInvariant().Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static bool StringContains(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(token)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawSpritePreview(Rect rect, Sprite sprite, bool emphasized)
        {
            ScenarioUiWidgets.DrawSpritePreviewFrame(rect, sprite, _uiContext.Styles, emphasized);
        }

        private static string CombineDetail(string primary, string secondary)
        {
            if (string.IsNullOrEmpty(primary))
                return secondary ?? string.Empty;
            if (string.IsNullOrEmpty(secondary))
                return primary;
            return primary + " | " + secondary;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static string ShortenToFit(string value, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(value) || style == null)
                return value ?? string.Empty;

            if (style.CalcSize(new GUIContent(value)).x <= maxWidth)
                return value;

            const string ellipsis = "...";
            float ellipsisWidth = style.CalcSize(new GUIContent(ellipsis)).x;
            if (ellipsisWidth >= maxWidth)
                return string.Empty;

            int low = 0;
            int high = value.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = value.Substring(0, mid) + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
                    low = mid;
                else
                    high = mid - 1;
            }

            return value.Substring(0, low) + ellipsis;
        }

        private Rect DrawSettingsWindow(
            Rect rect,
            ScenarioAuthoringSettingsViewModel settings,
            ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window != null ? window.HeaderActions : null, true);
            int settingsActionCount = settings.HeaderActions != null ? settings.HeaderActions.Length : 0;
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                settings.Title ?? "Editor Settings",
                null,
                false,
                30f,
                12f + (chromeActions.Length * 24f) + (settingsActionCount * 86f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                Rect actionRect = new Rect(actionX, headerRect.y + 3f, 22f, 22f);
                DrawButton(actionRect, chromeActions[i], false);
                actionX -= 24f;
            }

            for (int i = settings.HeaderActions != null ? settings.HeaderActions.Length - 1 : -1; i >= 0; i--)
            {
                Rect actionRect = new Rect(actionX - 82f, headerRect.y + 3f, 82f, 22f);
                DrawButton(actionRect, settings.HeaderActions[i], false);
                actionX -= 86f;
            }

            Rect bodyRect = regions.Body;
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            _settingsScrollPosition = GUILayout.BeginScrollView(_settingsScrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label(settings.Subtitle ?? string.Empty, _mutedTextStyle);
            GUILayout.Space(8f);
            for (int i = 0; settings.Sections != null && i < settings.Sections.Length; i++)
            {
                ScenarioAuthoringSettingsSectionViewModel section = settings.Sections[i];
                if (section == null)
                    continue;

                GUILayout.BeginVertical(_uiContext.Styles.Section);
                GUILayout.Label(section.Title ?? string.Empty, _sectionTitleStyle);
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    DrawSettingItem(section.Items[j]);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            DrawFloatingResizeGrip(rect, window);
            return bodyRect;
        }

        private Vector2 GetWindowScrollPosition(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return Vector2.zero;

            Vector2 scrollPosition;
            if (_windowScrollPositions.TryGetValue(windowId, out scrollPosition))
                return scrollPosition;

            return Vector2.zero;
        }

        private void SetWindowScrollPosition(string windowId, Vector2 scrollPosition)
        {
            if (string.IsNullOrEmpty(windowId))
                return;

            _windowScrollPositions[windowId] = scrollPosition;
        }

        private void DrawSettingItem(ScenarioAuthoringSettingsItemViewModel item)
        {
            if (item == null)
                return;

            float availableWidth = GetSectionContentWidth();
            bool stacked = availableWidth < 500f;
            if (!stacked)
                GUILayout.BeginHorizontal();

            float labelWidth = stacked ? availableWidth : Mathf.Clamp(availableWidth - 190f, 220f, 340f);
            GUILayout.BeginVertical(GUILayout.Width(labelWidth));
            GUILayout.Label(item.Label ?? string.Empty, _textStyle);
            GUILayout.Label(item.Description ?? string.Empty, _mutedTextStyle);
            GUILayout.EndVertical();
            if (stacked)
                GUILayout.Space(4f);

            if (item.Kind == ScenarioAuthoringSettingKind.Toggle)
            {
                DrawButton(GUILayoutUtility.GetRect(84f, 24f, GUILayout.Width(84f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingTogglePrefix + item.Id,
                        Label = item.BoolValue ? "On" : "Off",
                        Enabled = item.Enabled,
                        Emphasized = item.BoolValue
                    },
                    false);
            }
            else if (item.Kind == ScenarioAuthoringSettingKind.Float || item.Kind == ScenarioAuthoringSettingKind.Integer)
            {
                DrawButton(GUILayoutUtility.GetRect(26f, 24f, GUILayout.Width(26f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingDecreasePrefix + item.Id,
                        Label = "-",
                        Enabled = item.Enabled && item.CanDecrease
                    },
                    false);
                GUILayout.Label(item.ValueText ?? string.Empty, _uiContext.Styles.Field, GUILayout.Width(84f), GUILayout.Height(24f));
                DrawButton(GUILayoutUtility.GetRect(26f, 24f, GUILayout.Width(26f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingIncreasePrefix + item.Id,
                        Label = "+",
                        Enabled = item.Enabled && item.CanIncrease
                    },
                    false);
            }
            else if (item.Kind == ScenarioAuthoringSettingKind.Choice)
            {
                DrawSettingChoiceOptions(item, stacked ? availableWidth : 180f);
            }
            else
            {
                GUILayout.Label(item.ValueText ?? string.Empty, _uiContext.Styles.Field, GUILayout.Width(160f), GUILayout.Height(24f));
            }

            if (!stacked)
                GUILayout.EndHorizontal();
        }

        private void DrawSettingChoiceOptions(ScenarioAuthoringSettingsItemViewModel item, float availableWidth)
        {
            if (item == null)
                return;

            float rowLimit = Mathf.Max(120f, availableWidth);
            float rowWidth = 0f;
            GUILayout.BeginHorizontal();
            for (int i = 0; item.ChoiceValues != null && i < item.ChoiceValues.Length; i++)
            {
                string value = item.ChoiceValues[i] ?? string.Empty;
                string label = item.ChoiceLabels != null && i < item.ChoiceLabels.Length && !string.IsNullOrEmpty(item.ChoiceLabels[i])
                    ? item.ChoiceLabels[i]
                    : value;
                bool selected = i == item.SelectedChoiceIndex;
                ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionSettingSelectPrefix + item.Id + "." + value,
                    Label = label,
                    Hint = "Set " + item.Label + " to " + label + ".",
                    Enabled = item.Enabled,
                    Emphasized = selected
                };
                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 18f), 58f, rowLimit);
                if (rowWidth > 0f && rowWidth + width > rowLimit)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                    GUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }

                Rect rect = GUILayoutUtility.GetRect(width, 24f, GUILayout.Width(width), GUILayout.Height(24f));
                DrawButton(rect, action, false);
                GUILayout.Space(4f);
                rowWidth += width + 4f;
            }

            GUILayout.EndHorizontal();
        }
    }
}

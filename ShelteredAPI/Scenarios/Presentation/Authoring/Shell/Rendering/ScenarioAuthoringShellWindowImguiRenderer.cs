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
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
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
            RegisterInteractiveRegion(rect);
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
            // TODO(centralize): Standard windows are the remaining generic dock/floating surface.
            // Move callers into central workspace regions when their final placement is known.
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
                    float width = Math.Max(42f, MeasureButtonWidth(action, true, 18f));
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
            RegisterScrollRegion(window.Id, bodyRect);
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

        private Rect DrawWorkshopSurfaceCore(Rect contentRect, ScenarioAuthoringShellWindowViewModel[] windows, string activeWorkspaceId)
        {
            ScenarioAuthoringShellWindowViewModel window = FindWindow(windows, activeWorkspaceId);
            if (window == null)
                return RuntimeCompat.ZeroRect();

            Rect backdropRect = new Rect(
                0f,
                0f,
                contentRect.xMax + ScenarioAuthoringShellLayout.Margin,
                contentRect.yMax);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.17f, 0.13f, 0.09f, 1f);
            GUI.DrawTexture(backdropRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            Rect pageRect = ScenarioAuthoringShellLayout.BuildWorkshopPageRect(contentRect);
            GUI.Label(new Rect(pageRect.x, pageRect.y, pageRect.width, 34f), window.Title ?? string.Empty, _smallTitleStyle);
            if (!string.IsNullOrEmpty(window.Subtitle))
                GUI.Label(new Rect(pageRect.x, pageRect.y + 30f, pageRect.width, 20f), window.Subtitle, _mutedTextStyle);

            Rect bodyRect = new Rect(pageRect.x, pageRect.y + 58f, pageRect.width, Math.Max(120f, pageRect.height - 58f));
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            if (!string.Equals(_lastWorkshopWorkspaceId, activeWorkspaceId ?? string.Empty, StringComparison.Ordinal))
            {
                _lastWorkshopWorkspaceId = activeWorkspaceId ?? string.Empty;
                if (IsHomeWorkshopPage(window))
                    scrollPosition = Vector2.zero;
            }
            RegisterScrollRegion(window.Id, bodyRect);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (IsHomeWorkshopPage(window))
            {
                DrawHomeWorkshopPage(window);
            }
            else
            {
                for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
                {
                    DrawSection(window.Sections[i]);
                    if (i < window.Sections.Length - 1)
                        GUILayout.Space(8f);
                }
            }
            GUILayout.Space(18f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(window.Id, scrollPosition);
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
            // TODO(centralize): Selection details still render in a right-side inspector.
            // Merge this into the central workspace once selection/edit panels are assigned.
            if (IsEmptyInspector(window))
            {
                return RuntimeCompat.ZeroRect();
            }

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
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition.x = 0f;
            RegisterScrollRegion(window.Id, bodyRect);
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

        private void DrawEmptyInspectorChip(Rect rect)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 7f, rect.width - 24f, 20f), "Nothing selected", _mutedTextStyle);
        }

        private Rect DrawBottomTrayWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            // TODO(centralize): Asset placement still uses a standalone bottom tray.
            // Merge palette/details content into the central workspace when the tool layout lands.
            if (IsPlacementActive())
                return DrawCollapsedPlacementTray(rect, window);

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

            Rect filterRect = new Rect(pickerRect.x, pickerRect.y, pickerRect.width, 24f);
            DrawCandidateSearchControl(
                filterRect,
                "build_palette_search",
                ref _buildPaletteSearchText,
                ref _buildPaletteSearchFocused);

            float pickerScrollHeight = Math.Max(98f, pickerRect.height - 30f);
            Rect pickerScrollRect = new Rect(pickerRect.x, pickerRect.y + 30f, pickerRect.width, pickerScrollHeight);
            GUILayout.BeginArea(pickerScrollRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, pickerRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            RegisterScrollRegion(window.Id + ".picker", pickerScrollRect);
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
                float detailsScrollHeight = Math.Max(30f, detailsRect.height);
                Rect detailsScrollRect = new Rect(detailsRect.x, detailsRect.y, detailsRect.width, detailsScrollHeight);
                GUILayout.BeginArea(detailsScrollRect);
                previousContentWidth = _activeContentWidth;
                _activeContentWidth = Math.Max(120f, detailsRect.width - 18f);
                Vector2 detailsScroll = GetWindowScrollPosition(window.Id + ".details");
                RegisterScrollRegion(window.Id + ".details", detailsScrollRect);
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

        private Rect DrawCollapsedPlacementTray(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            // TODO(centralize): Active placement feedback is still a collapsed tray strip.
            // Move this state into the central placement workspace/status area.
            DrawChromePanel(rect, _rootPanelStyle);
            string label = ResolveActivePlacementLabel(window);
            string validity = ResolvePlacementValidityLabel(window);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, Math.Max(80f, rect.width * 0.45f), 22f), label, _textStyle);
            GUI.Label(new Rect(rect.x + (rect.width * 0.48f), rect.y + 10f, 120f, 22f), validity, _mutedTextStyle);
            GUI.Label(new Rect(rect.xMax - 190f, rect.y + 10f, 178f, 22f), "Esc or right click cancels", _mutedTextStyle);
            return RuntimeCompat.ZeroRect();
        }

        private static string ResolveActivePlacementLabel(ScenarioAuthoringShellWindowViewModel window)
        {
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null && item.Action != null && item.Action.Emphasized && !string.IsNullOrEmpty(item.Action.Label))
                        return item.Action.Label;
                }
            }

            return "Placing asset";
        }

        private static string ResolvePlacementValidityLabel(ScenarioAuthoringShellWindowViewModel window)
        {
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null
                        && string.Equals(item.Label, "Placement", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(item.Value))
                    {
                        return item.Value;
                    }
                }
            }

            return "Previewing";
        }

        private Rect DrawDocumentModalCore(Rect rect, ScenarioAuthoringInspectorDocument document, string scrollId)
        {
            // TODO(centralize): Document-style editors still render as modals over the shell.
            // Convert to central workspace panels when the editor workflow is consolidated.
            string title = document != null && !string.IsNullOrEmpty(document.Title)
                ? document.Title.ToUpperInvariant()
                : "DOCUMENT";
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                title,
                document != null ? document.Subtitle : null,
                false,
                46f,
                12f + ((document != null && document.HeaderActions != null ? document.HeaderActions.Length : 0) * 24f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 28f;
            for (int i = document != null && document.HeaderActions != null ? document.HeaderActions.Length - 1 : -1; i >= 0; i--)
            {
                Rect actionRect = new Rect(actionX, headerRect.y + 6f, 22f, 22f);
                DrawButton(actionRect, document.HeaderActions[i], false);
                actionX -= 24f;
            }

            Rect bodyRect = regions.Body;
            GUILayout.BeginArea(bodyRect);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, bodyRect.width - 18f);
            Vector2 scrollPosition = GetWindowScrollPosition(scrollId);
            RegisterScrollRegion(scrollId, bodyRect);
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
            // TODO(centralize): Pixel editor is still a dedicated floating-style window.
            // Merge it into the central art/edit workspace when the art workflow is finalized.
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
            float controlsWidth = Mathf.Clamp(bodyRect.width * 0.34f, 304f, 344f);
            controlsWidth = Math.Min(controlsWidth, Math.Max(224f, bodyRect.width - 170f));
            float controlsContentWidth = Math.Max(180f, controlsWidth - 24f);
            float footerHeight = 42f;
            Rect toolsRect = new Rect(bodyRect.x, bodyRect.y, controlsWidth, Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect canvasPane = new Rect(toolsRect.xMax + 10f, bodyRect.y, Math.Max(160f, bodyRect.xMax - toolsRect.xMax - 10f), Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect footerRect = new Rect(bodyRect.x, bodyRect.yMax - footerHeight, bodyRect.width, footerHeight);

            GUILayout.BeginArea(toolsRect);
            RegisterScrollRegion("pixel_editor.tools", toolsRect);
            Vector2 toolsScroll = GetWindowScrollPosition("pixel_editor.tools");
            toolsScroll = GUILayout.BeginScrollView(toolsScroll, false, true, GUILayout.Width(toolsRect.width), GUILayout.Height(toolsRect.height));
            GUILayout.BeginVertical(_uiContext.Styles.Section, GUILayout.Width(Math.Max(180f, toolsRect.width - 18f)));
            GUILayout.Label(editor.IsCharacterEditor ? "Character Pixels" : "Sprite Pixels", _sectionTitleStyle);
            GUILayout.Label(editor.Dirty ? "Unsaved changes" : "No unsaved changes", _mutedTextStyle);
            if (editor.IsCharacterEditor)
            {
                DrawCharacterPartToolbar(editor, controlsContentWidth);
                GUILayout.Space(6f);
            }
            DrawCustomEditorToolbar(editor, controlsContentWidth);
            GUILayout.Space(6f);
            DrawCustomClipboardToolbar(editor, controlsContentWidth);
            GUILayout.Space(6f);
            DrawCustomZoomToolbar(editor, controlsContentWidth);
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
            GUILayout.EndScrollView();
            SetWindowScrollPosition("pixel_editor.tools", toolsScroll);
            GUILayout.EndArea();

            DrawPixelCanvasViewport(canvasPane, editor);

            GUILayout.BeginArea(footerRect);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, "Save", editor.Dirty, 96f, "Save the current pixel edit. Ctrl+S");
            GUILayout.Space(8f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, "Discard", false, 96f, "Discard the current pixel edit.", editor.Dirty);
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
                DrawCharacterPartToolbar(editor, controlsWidth);
                GUILayout.Space(6f);
            }
            DrawCustomEditorToolbar(editor, controlsWidth);
            GUILayout.Space(6f);
            DrawCustomClipboardToolbar(editor, controlsWidth);
            GUILayout.Space(6f);
            DrawCustomZoomToolbar(editor, controlsWidth);
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
            Rect canvasScrollRect = GUILayoutUtility.GetRect(viewportWidth, viewportHeight, GUILayout.Width(viewportWidth), GUILayout.Height(viewportHeight));
            RegisterScrollRegion("custom_sprite_canvas", canvasScrollRect);
            GUILayout.BeginArea(canvasScrollRect);
            canvasScroll = GUILayout.BeginScrollView(canvasScroll, true, true, GUILayout.Width(viewportWidth), GUILayout.Height(viewportHeight));
            Rect canvasRect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            DrawPixelCanvas(canvasRect, editor);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition("custom_sprite_canvas", canvasScroll);
            GUILayout.EndVertical();
            if (stackedLayout)
                GUILayout.EndVertical();
            else
                GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCustomEditorToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 88f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint,
                "Paint",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Paint,
                buttonWidth,
                "Paint pixels using the active color.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick,
                "Pick",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick,
                buttonWidth,
                "Sample a pixel color from the canvas.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect,
                "Select",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select,
                buttonWidth,
                "Drag a rectangular pixel selection.");
            GUILayout.EndHorizontal();
        }

        private void DrawCustomClipboardToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 88f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy,
                "Copy",
                false,
                buttonWidth,
                "Copy the current selection. If nothing is selected, copy the whole sprite. Ctrl+C",
                true);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste,
                "Paste",
                editor.HasClipboard,
                buttonWidth,
                editor.HasClipboard ? "Paste the pixel clipboard into the canvas. Ctrl+V" : "Pixel clipboard is empty.",
                editor.HasClipboard);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear,
                "Clear Sel",
                editor.HasSelection,
                buttonWidth,
                editor.HasSelection ? "Clear the current pixel selection." : "There is no active selection.",
                editor.HasSelection);
            GUILayout.EndHorizontal();
        }

        private void DrawCustomZoomToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 88f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut,
                "Zoom -",
                false,
                buttonWidth,
                "Zoom out of the canvas.",
                editor.Zoom > 1);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset,
                editor.Zoom + "x",
                false,
                Math.Max(68f, buttonWidth - 20f),
                "Reset canvas zoom to 8x.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn,
                "Zoom +",
                false,
                buttonWidth,
                "Zoom into the canvas.",
                editor.Zoom < 48);
            GUILayout.EndHorizontal();
        }

        private void DrawCharacterPartToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 88f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead,
                "Head",
                editor.CharacterPart == ScenarioCharacterTexturePart.Head,
                buttonWidth,
                "Edit the head texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso,
                "Torso",
                editor.CharacterPart == ScenarioCharacterTexturePart.Torso,
                buttonWidth,
                "Edit the torso texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs,
                "Legs",
                editor.CharacterPart == ScenarioCharacterTexturePart.Legs,
                buttonWidth,
                "Edit the legs texture for this family member.");
            GUILayout.EndHorizontal();
        }

        private static float ResolveToolbarButtonWidth(float contentWidth, int columns, float gap, float minimum)
        {
            int safeColumns = Math.Max(1, columns);
            float available = Math.Max(minimum, contentWidth - (gap * (safeColumns - 1)));
            return Math.Max(minimum, available / safeColumns);
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
            ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = label,
                Hint = hint,
                Enabled = enabled,
                Emphasized = emphasized
            };
            float resolvedWidth = Math.Max(width, MeasureButtonWidth(action, false, 16f));
            Rect rect = GUILayoutUtility.GetRect(resolvedWidth, 28f, GUILayout.Width(resolvedWidth), GUILayout.Height(28f));
            DrawButton(rect, action, false);
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
            ScenarioUiAtlasSkin.DrawCornerCutTexture(fillRect, Texture2D.whiteTexture);
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
            RegisterInteractiveRegion(inner);
            if (editor.Width <= 0 || editor.Height <= 0)
            {
                GUI.Label(inner, "No sprite pixels available.", _mutedTextStyle);
                return;
            }

            float displayZoom = Mathf.Clamp(Mathf.Max(1f, editor.Zoom), 1f, 64f);
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
            if (current != null && IsPointerInsideGuiRect(inner, current))
            {
                if (TryHandlePixelCanvasWheel(inner, current))
                    return;

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
                if (TryHandlePixelCanvasWheel(rect, current))
                    return;

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
                    if (current.type == EventType.MouseDown
                        && editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Paint)
                    {
                        ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomStrokeBegin);
                    }

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

        private bool TryHandlePixelCanvasWheel(Rect rect, Event current)
        {
            bool contains = current != null && IsPointerInsideGuiRect(rect, current);

            if (current == null || !contains)
            {
                ResetPixelCanvasWheelAxisIfIdle();
                return false;
            }

            int deltaSign;
            if (!TryAcceptPixelCanvasWheel(current, out deltaSign))
                return false;

            string zoomActionId = deltaSign < 0
                ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn
                : ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut;
            ScenarioAuthoringBackendService.Instance.ExecuteAction(zoomActionId);
            if (current.type != EventType.Layout && current.type != EventType.Repaint)
                current.Use();
            return true;
        }

        private bool TryAcceptPixelCanvasWheel(Event current, out int deltaSign)
        {
            deltaSign = 0;
            if (current == null)
                return false;

            if (current.type == EventType.ScrollWheel && Mathf.Abs(current.delta.y) > 0.0001f)
            {
                if (_pixelEditorWheelHandledFrame == Time.frameCount)
                    return false;

                deltaSign = current.delta.y < 0f ? -1 : 1;
                _pixelEditorWheelAxisActive = false;
                _pixelEditorWheelHandledFrame = Time.frameCount;
                return true;
            }

            if (current.type != EventType.Repaint)
                return false;

            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) <= 0.0001f)
                wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) <= 0.0001f)
            {
                _pixelEditorWheelAxisActive = false;
                return false;
            }

            if (_pixelEditorWheelHandledFrame == Time.frameCount || _pixelEditorWheelAxisActive)
                return false;

            deltaSign = wheel > 0f ? -1 : 1;
            _pixelEditorWheelAxisActive = true;
            _pixelEditorWheelHandledFrame = Time.frameCount;
            return true;
        }

        private void ResetPixelCanvasWheelAxisIfIdle()
        {
            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) <= 0.0001f)
                wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) <= 0.0001f)
                _pixelEditorWheelAxisActive = false;
        }

        private static bool IsPointerInsideGuiRect(Rect rect, Event current)
        {
            if (current != null && rect.Contains(current.mousePosition))
                return true;

            Vector3 mousePosition = UnityEngine.Input.mousePosition;
            Vector2 guiMousePosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            return rect.Contains(guiMousePosition);
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

        private static bool IsHomeWorkshopPage(ScenarioAuthoringShellWindowViewModel window)
        {
            return window != null
                && string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(window.Title, "Test", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawHomeWorkshopPage(ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorSection identity = FindSection(window, "home_identity");
            ScenarioAuthoringInspectorSection setup = FindSection(window, "home_setup_checklist");
            ScenarioAuthoringInspectorSection baseMode = FindSection(window, "home_base_mode");
            ScenarioAuthoringInspectorSection quickActions = FindSection(window, "home_quick_actions");
            ScenarioAuthoringInspectorSection advanced = FindSection(window, "home_advanced");

            if (identity != null)
            {
                DrawHomeIdentityHeader(identity);
                GUILayout.Space(8f);
            }

            if (setup != null || baseMode != null)
            {
                float contentWidth = GetSectionContentWidth();
                bool twoColumns = contentWidth >= 760f && setup != null && baseMode != null;
                float columnGap = 10f;
                float columnWidth = twoColumns ? (contentWidth - columnGap) * 0.5f : contentWidth;
                if (twoColumns)
                    GUILayout.BeginHorizontal();

                if (setup != null)
                {
                    if (twoColumns)
                        GUILayout.BeginVertical(GUILayout.Width(columnWidth));
                    float previousContentWidth = _activeContentWidth;
                    if (twoColumns)
                        _activeContentWidth = Math.Max(120f, columnWidth);
                    DrawHomeSetupChecklist(setup);
                    if (twoColumns)
                        _activeContentWidth = previousContentWidth;
                    if (twoColumns)
                        GUILayout.EndVertical();
                }

                if (twoColumns)
                    GUILayout.Space(columnGap);

                if (baseMode != null)
                {
                    if (twoColumns)
                        GUILayout.BeginVertical(GUILayout.Width(columnWidth));
                    float previousContentWidth = _activeContentWidth;
                    if (twoColumns)
                        _activeContentWidth = Math.Max(120f, columnWidth);
                    DrawHomeBaseSelector(baseMode);
                    if (twoColumns)
                        _activeContentWidth = previousContentWidth;
                    if (twoColumns)
                        GUILayout.EndVertical();
                }

                if (twoColumns)
                    GUILayout.EndHorizontal();

                GUILayout.Space(10f);
            }

            DrawHomeQuestionGrid(window);

            if (quickActions != null)
            {
                GUILayout.Space(8f);
                DrawSection(quickActions);
            }

            if (advanced != null)
            {
                GUILayout.Space(8f);
                DrawSection(advanced);
            }
        }

        private static ScenarioAuthoringInspectorSection FindSection(ScenarioAuthoringShellWindowViewModel window, string id)
        {
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (section != null && string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase))
                    return section;
            }

            return null;
        }

        private void DrawHomeIdentityHeader(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Card);
            ScenarioAuthoringInspectorItem titleItem = null;
            List<ScenarioAuthoringInspectorAction> chips = new List<ScenarioAuthoringInspectorAction>();
            ScenarioAuthoringInspectorItem pathItem = null;
            ScenarioAuthoringInspectorAction copyPath = null;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (item.Editable)
                    titleItem = item;
                else if (item.Action != null && string.Equals(item.Action.Id, ScenarioAuthoringActionIds.ActionDraftCopyPath, StringComparison.Ordinal))
                    copyPath = item.Action;
                else if (item.Action != null)
                    chips.Add(item.Action);
                else if (string.Equals(item.Label, "Draft Path", StringComparison.OrdinalIgnoreCase))
                    pathItem = item;
            }

            DrawHomeHeadlineField(titleItem);
            GUILayout.Space(6f);
            DrawHomeStatusChips(chips);
            if (pathItem != null)
            {
                GUILayout.Space(5f);
                DrawHomeDraftPath(pathItem, copyPath);
            }
            GUILayout.EndVertical();
        }

        private void DrawHomeHeadlineField(ScenarioAuthoringInspectorItem item)
        {
            string label = item != null ? item.Label ?? "Title" : "Title";
            string value = item != null ? item.Value ?? string.Empty : string.Empty;
            Rect rect = GUILayoutUtility.GetRect(240f, 38f, GUILayout.ExpandWidth(true), GUILayout.Height(38f));
            GUIStyle style = new GUIStyle(_uiContext.Styles.Field);
            style.fontSize = Math.Max(style.fontSize, 22);
            style.fontStyle = FontStyle.Bold;
            string controlName = "editable." + label;
            string focusedName = GUI.GetNameOfFocusedControl();
            bool wasFocused = string.Equals(focusedName, controlName, StringComparison.Ordinal);
            bool previouslyFocused = _editableFieldsFocusedLastFrame.Contains(controlName);
            string draft;
            if (!_editableFieldDrafts.TryGetValue(controlName, out draft) || (!wasFocused && !previouslyFocused))
                draft = value;
            GUI.SetNextControlName(controlName);
            string next = GUI.TextField(rect, draft, style);
            _editableFieldDrafts[controlName] = next;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _editableFieldFocused = _editableFieldFocused || focused;
            if (focused || (Event.current != null && rect.Contains(Event.current.mousePosition)))
                DrawFieldFocusBorder(rect);
            TryCommitEditableField(item, controlName, value, next, previouslyFocused, focused);
            TrackEditableFieldFocus(controlName, focused);
        }

        private void DrawHomeStatusChips(List<ScenarioAuthoringInspectorAction> chips)
        {
            float rowLimit = GetSectionContentWidth();
            float rowWidth = 0f;
            GUILayout.BeginHorizontal();
            for (int i = 0; chips != null && i < chips.Count; i++)
            {
                ScenarioAuthoringInspectorAction action = chips[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 26f), 84f, Math.Min(240f, rowLimit));
                if (rowWidth > 0f && rowWidth + width > rowLimit)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }

                Rect rect = GUILayoutUtility.GetRect(width, 26f, GUILayout.Width(width), GUILayout.Height(26f));
                DrawHomeStatusChip(rect, action);
                GUILayout.Space(6f);
                rowWidth += width + 6f;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawHomeStatusChip(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null || _uiContext == null || _uiContext.Styles == null)
                return;

            GUIStyle style = ResolveHomeChipStyle(action);
            string tooltip = action.Enabled
                ? (action.Hint ?? action.Detail ?? string.Empty)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (action.Hint ?? action.Detail ?? string.Empty));
            GUIContent content = new GUIContent(ShortenToFit(action.Label ?? string.Empty, Math.Max(0f, rect.width - 14f), style), tooltip);
            RegisterInteractiveRegion(rect);
            if (!string.IsNullOrEmpty(action.Id))
                RegisterTourTarget("action:" + action.Id, rect);
            if (action.Enabled)
            {
                if (GUI.Button(rect, content, style))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                    if (Event.current != null)
                        Event.current.Use();
                }
            }
            else
            {
                GUI.Box(rect, content, style);
            }
        }

        private GUIStyle ResolveHomeChipStyle(ScenarioAuthoringInspectorAction action)
        {
            ScenarioUiPillEmphasis emphasis = ResolveHomeChipEmphasis(action);
            switch (emphasis)
            {
                case ScenarioUiPillEmphasis.Success:
                    return _uiContext.Styles.PillSuccess;
                case ScenarioUiPillEmphasis.Warning:
                    return _uiContext.Styles.PillWarning;
                case ScenarioUiPillEmphasis.Danger:
                    return _uiContext.Styles.PillDanger;
                case ScenarioUiPillEmphasis.Active:
                    return _uiContext.Styles.PillEmphasized;
                default:
                    return _uiContext.Styles.Pill;
            }
        }

        private static ScenarioUiPillEmphasis ResolveHomeChipEmphasis(ScenarioAuthoringInspectorAction action)
        {
            string label = action != null ? action.Label ?? string.Empty : string.Empty;
            if (StringContains(label, "error") || StringContains(label, "fix validation"))
                return ScenarioUiPillEmphasis.Danger;
            if (StringContains(label, "warning") || StringContains(label, "unsaved") || StringContains(label, "unavailable") || StringContains(label, "save draft"))
                return ScenarioUiPillEmphasis.Warning;
            if (StringContains(label, "saved") || string.Equals(label, "OK", StringComparison.OrdinalIgnoreCase) || StringContains(label, "running"))
                return ScenarioUiPillEmphasis.Success;
            if (StringContains(label, "ready to test"))
                return ScenarioUiPillEmphasis.Active;
            return ScenarioUiPillEmphasis.Default;
        }

        private void DrawHomeDraftPath(ScenarioAuthoringInspectorItem pathItem, ScenarioAuthoringInspectorAction copyPath)
        {
            float rowLimit = GetSectionContentWidth();
            float copyWidth = copyPath != null ? Mathf.Clamp(MeasureButtonWidth(copyPath, false, 22f), 88f, 132f) : 0f;
            bool inlineCopy = copyPath == null || rowLimit - copyWidth - 12f >= 180f;
            float rowHeight = inlineCopy ? 26f : 54f;
            Rect rowRect = GUILayoutUtility.GetRect(240f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            Rect labelRect = new Rect(rowRect.x, rowRect.y + 4f, inlineCopy ? Math.Max(90f, rowRect.width - copyWidth - 12f) : rowRect.width, 18f);
            string path = pathItem != null ? pathItem.Value ?? string.Empty : string.Empty;
            const string prefix = "Draft: ";
            GUIStyle pathStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            pathStyle.wordWrap = false;
            pathStyle.clipping = TextClipping.Clip;
            float pathWidth = Math.Max(12f, labelRect.width - pathStyle.CalcSize(new GUIContent(prefix)).x);
            GUI.Label(labelRect, new GUIContent(prefix + MiddleTruncate(path, pathWidth, pathStyle), pathItem != null ? pathItem.HoverHint ?? path : path), pathStyle);
            if (copyPath != null)
            {
                float copyX = inlineCopy ? rowRect.xMax - copyWidth : rowRect.x;
                float copyY = inlineCopy ? rowRect.y : rowRect.y + 28f;
                DrawButton(new Rect(copyX, copyY, copyWidth, 24f), copyPath, false);
            }
        }

        private void DrawHomeBaseSelector(ScenarioAuthoringInspectorSection section)
        {
            DrawHomeSectionLabel(section.Title ?? "Scenario Base");
            GUILayout.BeginVertical(_uiContext.Styles.Card);
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            string hint = null;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (item.Action != null)
                    actions.Add(item.Action);
                else if (string.IsNullOrEmpty(hint))
                    hint = item.Value ?? item.Label;
            }

            float width = GetSectionContentWidth();
            float gap = 4f;
            bool stacked;
            float[] segmentWidths = CalculateHomeSegmentWidths(actions, width, gap, out stacked);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < actions.Count; i++)
            {
                if (stacked && i > 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                }

                float segmentWidth = i < segmentWidths.Length ? segmentWidths[i] : width;
                Rect rect = GUILayoutUtility.GetRect(segmentWidth, 30f, GUILayout.Width(segmentWidth), GUILayout.Height(30f));
                DrawButton(rect, actions[i], true);
                if (!stacked && i < actions.Count - 1)
                    GUILayout.Space(gap);
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Space(6f);
                GUILayout.Label(hint, _uiContext.Styles.PaperMutedText);
            }
            GUILayout.EndVertical();
        }

        private float[] CalculateHomeSegmentWidths(List<ScenarioAuthoringInspectorAction> actions, float availableWidth, float gap, out bool stacked)
        {
            stacked = false;
            int count = actions != null ? actions.Count : 0;
            if (count == 0)
                return new float[0];

            float[] widths = new float[count];
            float totalMinimum = 0f;
            for (int i = 0; i < count; i++)
            {
                widths[i] = Math.Max(74f, MeasureButtonWidth(actions[i], true, 18f));
                totalMinimum += widths[i];
            }

            float totalGap = gap * Math.Max(0, count - 1);
            if (totalMinimum + totalGap > availableWidth)
            {
                stacked = true;
                for (int i = 0; i < count; i++)
                    widths[i] = availableWidth;
                return widths;
            }

            float surplus = (availableWidth - totalGap - totalMinimum) / count;
            for (int i = 0; i < count; i++)
                widths[i] += surplus;
            return widths;
        }

        private void DrawHomeSetupChecklist(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(section.Title ?? "Set Up Your Scenario", _sectionTitleStyle);
            GUILayout.FlexibleSpace();
            ScenarioAuthoringInspectorAction dismiss = FindAction(section, ScenarioAuthoringActionIds.ActionSetupDismiss);
            if (dismiss != null)
            {
                float width = Mathf.Clamp(MeasureButtonWidth(dismiss, false, 18f), 70f, 100f);
                DrawButton(GUILayoutUtility.GetRect(width, 24f, GUILayout.Width(width), GUILayout.Height(24f)), dismiss, false);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            GUILayout.BeginVertical(_uiContext.Styles.Card);

            string recommendedActionId = null;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null || string.Equals(item.Action.Id, ScenarioAuthoringActionIds.ActionSetupDismiss, StringComparison.Ordinal))
                    continue;
                if (recommendedActionId == null && item.Action.Enabled)
                    recommendedActionId = item.Action.Id;
            }

            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null || string.Equals(item.Action.Id, ScenarioAuthoringActionIds.ActionSetupDismiss, StringComparison.Ordinal))
                    continue;
                Rect rect = GUILayoutUtility.GetRect(180f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
                DrawChecklistItem(rect, item.Action, string.Equals(item.Action.Id, recommendedActionId, StringComparison.Ordinal));
                GUILayout.Space(3f);
            }
            GUILayout.EndVertical();
        }

        private void DrawHomeSectionLabel(string title)
        {
            GUILayout.Label(title ?? string.Empty, _sectionTitleStyle);
            GUILayout.Space(3f);
        }

        private void DrawChecklistItem(Rect rect, ScenarioAuthoringInspectorAction action, bool recommended)
        {
            if (action == null)
                return;

            bool complete = !action.Enabled && action.Label != null && action.Label.StartsWith("Done:", StringComparison.OrdinalIgnoreCase);
            if (!complete)
            {
                DrawButton(rect, action, false);
                if (recommended && _uiContext != null && _uiContext.Styles != null)
                {
                    float pulse = 0.45f + (Mathf.Sin(Time.realtimeSinceStartup * 2.1f) * 0.20f);
                    Color oldColor = GUI.color;
                    GUI.color = new Color(0.94f, 0.80f, 0.52f, pulse);
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
                    GUI.color = oldColor;
                }
                return;
            }

            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            Rect markRect = new Rect(rect.x + 8f, rect.y + 4f, 32f, rect.height - 8f);
            Rect textRect = new Rect(markRect.xMax + 8f, rect.y + 3f, rect.width - 48f, rect.height - 6f);
            ScenarioUiWidgets.DrawPill(markRect, "OK", _uiContext.Styles, ScenarioUiPillEmphasis.Success);
            string label = action.Label.Substring("Done:".Length).Trim();
            GUI.Label(textRect, label, _uiContext.Styles.PaperBodyText);
        }

        private static ScenarioAuthoringInspectorAction FindAction(ScenarioAuthoringInspectorSection section, string actionId)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Action != null && string.Equals(item.Action.Id, actionId, StringComparison.Ordinal))
                    return item.Action;
            }

            return null;
        }

        private void DrawHomeQuestionGrid(ScenarioAuthoringShellWindowViewModel window)
        {
            List<ScenarioAuthoringInspectorSection> questions = new List<ScenarioAuthoringInspectorSection>();
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (IsHomeQuestionSection(section))
                    questions.Add(section);
            }

            if (questions.Count == 0)
                return;

            float availableWidth = GetSectionContentWidth();
            float gap = 10f;
            int columns = availableWidth >= 760f ? 2 : 1;
            float cardWidth = columns == 2 ? (availableWidth - gap) * 0.5f : availableWidth;
            for (int i = 0; i < questions.Count; i += columns)
            {
                int rowCount = Math.Min(columns, questions.Count - i);
                float rowCardWidth = rowCount == columns
                    ? cardWidth
                    : (availableWidth - (gap * (rowCount - 1))) / rowCount;
                GUILayout.BeginHorizontal();
                for (int column = 0; column < rowCount; column++)
                {
                    int index = i + column;
                    Rect rect = GUILayoutUtility.GetRect(rowCardWidth, 78f, GUILayout.Width(rowCardWidth), GUILayout.Height(78f));
                    DrawHomeQuestionCard(rect, questions[index]);

                    if (column < rowCount - 1)
                        GUILayout.Space(gap);
                }
                GUILayout.EndHorizontal();
                if (i + columns < questions.Count)
                    GUILayout.Space(8f);
            }
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
            else if (IsTargetStripSection(section))
            {
                DrawTargetStripSection(section, compactInspector);
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

                    float width = Math.Max(
                        renderAsTabs ? 72f : 94f,
                        MeasureButtonWidth(item.Action, renderAsTabs, 20f));
                    width = Math.Min(width, rowLimit);
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
                bool buildPaletteSection = IsBuildPaletteSection(section);
                float cardGap = buildPaletteSection ? 3f : 4f;
                float preferredCardWidth = buildPaletteSection ? (compactInspector ? 152f : 176f) : (compactInspector ? 190f : 224f);
                float minCardWidth = buildPaletteSection ? (compactInspector ? 136f : 156f) : (compactInspector ? 176f : 196f);
                float cardHeight = buildPaletteSection ? 72f : 94f;
                int maxColumns = buildPaletteSection ? (compactInspector ? 4 : 5) : (compactInspector ? 2 : 4);
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

                    Rect rect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
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
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.FactGrid)
            {
                DrawFactGrid(section, compactInspector);
            }
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.CastCardGrid)
            {
                DrawCastCardGrid(section, compactInspector);
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
                && !string.Equals(section.Id, "home_setup_checklist", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(section.Id, "home_base_mode", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(section.Id, "home_advanced", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(section.Id, "home_quick_actions", StringComparison.OrdinalIgnoreCase)
                && section.Layout == ScenarioAuthoringInspectorSectionLayout.ActionStrip;
        }

        private static bool IsTargetStripSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && string.Equals(section.Id, "target_strip", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawTargetStripSection(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;

                if (item.Action != null && IsSelectionStackAction(item.Action))
                {
                    DrawCompactActionRow(item.Action);
                    GUILayout.Space(2f);
                    continue;
                }

                DrawItem(item, compactInspector);
                if (i == 0)
                    GUILayout.Space(4f);
            }
        }

        private void DrawHomeQuestionCard(ScenarioAuthoringInspectorSection section)
        {
            Rect rect = GUILayoutUtility.GetRect(120f, 78f, GUILayout.ExpandWidth(true), GUILayout.Height(78f));
            DrawHomeQuestionCard(rect, section);
        }

        private void DrawHomeQuestionCard(Rect rect, ScenarioAuthoringInspectorSection section)
        {
            ScenarioAuthoringInspectorAction action = null;
            string detail = string.Empty;
            string badge = string.Empty;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (item.Action != null && action == null)
                    action = item.Action;
                else if (string.IsNullOrEmpty(detail))
                    detail = item.Value ?? item.Label ?? string.Empty;
                else if (string.IsNullOrEmpty(badge))
                    badge = item.Value ?? item.Label ?? string.Empty;
            }

            if (action == null)
                return;

            string tooltip = action.Enabled
                ? (action.Hint ?? action.Detail ?? string.Empty)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (action.Hint ?? action.Detail ?? string.Empty));
            RegisterInteractiveRegion(rect);
            if (!string.IsNullOrEmpty(action.Id))
                RegisterTourTarget("action:" + action.Id, rect);
            if (action.Enabled)
            {
                if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), _uiContext.Styles.Card))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                    if (Event.current != null)
                        Event.current.Use();
                }
            }
            else
            {
                GUI.Box(rect, new GUIContent(string.Empty, tooltip), _uiContext.Styles.Card);
            }
            DrawButtonAnimationOverlay(rect, action.Id, action.Enabled, rect.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero), false);
            if (action.Emphasized && _uiContext != null && _uiContext.Styles != null)
                ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderStrongTexture);

            GUIStyle actionStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            actionStyle.alignment = TextAnchor.MiddleRight;
            actionStyle.wordWrap = false;
            actionStyle.clipping = TextClipping.Clip;
            float actionLabelWidth = actionStyle.CalcSize(new GUIContent(action.Label ?? string.Empty)).x + 12f;
            float sideWidth = Mathf.Max(108f, actionLabelWidth);
            if (!string.IsNullOrEmpty(badge))
            {
                Vector2 measuredBadge = _mutedTextStyle.CalcSize(new GUIContent(badge));
                sideWidth = Mathf.Max(sideWidth, measuredBadge.x + 24f);
            }

            sideWidth = Mathf.Clamp(sideWidth, 108f, Math.Min(240f, rect.width * 0.46f));
            Rect glyphRect = new Rect(rect.x + 12f, rect.y + 16f, 38f, 38f);
            bool drewGlyph = DrawHomeQuestionGlyph(glyphRect, section, action);
            float textX = drewGlyph ? glyphRect.xMax + 10f : rect.x + 14f;
            float textReservedWidth = drewGlyph ? glyphRect.width + 42f : 32f;
            Rect textRect = new Rect(textX, rect.y + 8f, Math.Max(24f, rect.width - sideWidth - textReservedWidth), rect.height - 16f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), ShortenToFit(section.Title ?? string.Empty, textRect.width, _uiContext.Styles.PaperTitleText), _uiContext.Styles.PaperTitleText);
            GUI.Label(new Rect(textRect.x, textRect.y + 26f, textRect.width, 32f), detail ?? string.Empty, _uiContext.Styles.PaperMutedText);
            if (!string.IsNullOrEmpty(badge))
            {
                Rect badgeRect = new Rect(rect.xMax - sideWidth - 14f, rect.y + 14f, sideWidth, 22f);
                ScenarioUiWidgets.DrawPill(badgeRect, badge, _uiContext.Styles, ResolveHomeBadgeEmphasis(badge));
            }
            Rect actionRect = new Rect(rect.xMax - sideWidth - 14f, rect.yMax - 32f, sideWidth, 20f);
            GUI.Label(actionRect, ShortenToFit(action.Label ?? string.Empty, actionRect.width, actionStyle), actionStyle);
        }

        private bool DrawHomeQuestionGlyph(Rect rect, ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorAction action)
        {
            if (_uiContext == null || _uiContext.Styles == null)
                return false;

            Rect iconRect = new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, rect.height - 10f);
            string role = ResolveHomeIconRole(section);
            if (string.IsNullOrEmpty(role) || !ScenarioUiAtlasSkin.HasIcon(role))
                return false;

            GUI.Box(rect, GUIContent.none, action != null && action.Emphasized ? _uiContext.Styles.ButtonActive : _uiContext.Styles.Field);
            return ScenarioUiAtlasSkin.DrawIcon(iconRect, role);
        }

        private static string ResolveHomeIconRole(ScenarioAuthoringInspectorSection section)
        {
            if (section == null || string.IsNullOrEmpty(section.Id))
                return null;
            return section.Id;
        }

        private static ScenarioUiPillEmphasis ResolveHomeBadgeEmphasis(string badge)
        {
            if (StringContains(badge, "unsaved") || StringContains(badge, "warning"))
                return ScenarioUiPillEmphasis.Warning;
            if (StringContains(badge, "saved") || StringContains(badge, "ready"))
                return ScenarioUiPillEmphasis.Success;
            if (StringContains(badge, "review"))
                return ScenarioUiPillEmphasis.Active;
            if (StringContains(badge, "error"))
                return ScenarioUiPillEmphasis.Danger;
            return ScenarioUiPillEmphasis.Default;
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
                    if (item.Editable)
                    {
                        DrawEditableProperty(item, compactInspector);
                        break;
                    }

                    string value = compactInspector ? Shorten(item.Value, 34) : item.Value;
                    float rowHeight = CalculateKeyValueRowHeight(item.Label, value);
                    Rect rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
                    ScenarioUiWidgets.DrawKeyValueRow(rowRect, item.Label, value, _uiContext.Styles);
                    break;
                case ScenarioAuthoringInspectorItemKind.Action:
                    if (item.Action != null)
                    {
                        if (IsSelectionStackAction(item.Action))
                        {
                            DrawCompactActionRow(item.Action);
                            break;
                        }

                        float width = Math.Max(96f, MeasureButtonWidth(item.Action, false, 24f));
                        width = Math.Min(width, GetSectionContentWidth());
                        Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                        DrawButton(rect, item.Action, false);
                    }
                    break;
                default:
                    GUILayout.Label(item.Value ?? string.Empty, _textStyle);
                    break;
            }
        }

        private static bool IsSelectionStackAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && !string.IsNullOrEmpty(action.Id)
                && (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSelectionStackCycle, StringComparison.Ordinal)
                    || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded, StringComparison.Ordinal)
                    || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix, StringComparison.Ordinal));
        }

        private void DrawCompactActionRow(ScenarioAuthoringInspectorAction action)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            Event evt = Event.current;
            if (action != null
                && action.Enabled
                && string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded, StringComparison.Ordinal)
                && evt != null
                && evt.type == EventType.MouseDown
                && evt.button == 0
                && rowRect.Contains(evt.mousePosition))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                evt.Use();
                return;
            }

            DrawButton(rowRect, action, false);
        }

        private void DrawEditableProperty(ScenarioAuthoringInspectorItem item, bool compactInspector)
        {
            string label = item != null ? item.Label ?? string.Empty : string.Empty;
            string value = compactInspector ? Shorten(item != null ? item.Value : null, 34) : (item != null ? item.Value : null);
            float rowHeight = Math.Max(30f, CalculateKeyValueRowHeight(label, value) + 6f);
            Rect rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            float gap = _uiContext != null ? _uiContext.Styles.Theme.Metrics.PaddingSm : 6f;
            float labelWidth = Math.Max(42f, (rowRect.width - gap) / 2.4f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y + 3f, labelWidth, rowRect.height - 6f);
            Rect fieldRect = new Rect(labelRect.xMax + gap, rowRect.y + 2f, Math.Max(60f, rowRect.width - labelWidth - gap), rowRect.height - 4f);
            GUI.Label(labelRect, label, _mutedTextStyle);

            string controlName = "editable." + label;
            string focusedName = GUI.GetNameOfFocusedControl();
            bool wasFocused = string.Equals(focusedName, controlName, StringComparison.Ordinal);
            bool previouslyFocused = _editableFieldsFocusedLastFrame.Contains(controlName);
            string draft;
            if (!_editableFieldDrafts.TryGetValue(controlName, out draft) || (!wasFocused && !previouslyFocused))
                draft = value ?? string.Empty;

            Event evt = Event.current;
            bool hovered = evt != null && fieldRect.Contains(evt.mousePosition);
            if (hovered && evt.type == EventType.MouseDown && evt.button == 0)
                GUI.FocusControl(controlName);

            GUI.SetNextControlName(controlName);
            string next = GUI.TextField(fieldRect, draft, _uiContext.Styles.Field);
            _editableFieldDrafts[controlName] = next;

            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _editableFieldFocused = _editableFieldFocused || focused;
            if (focused || hovered)
            {
                DrawFieldFocusBorder(fieldRect);
                GUIStyle fieldStyle = _uiContext.Styles.Field;
                Vector2 textSize = fieldStyle.CalcSize(new GUIContent(next ?? string.Empty));
                float caretX = Mathf.Min(fieldRect.xMax - fieldStyle.padding.right - 2f, fieldRect.x + fieldStyle.padding.left + textSize.x + 1f);
                float caretY = fieldRect.y + Math.Max(4f, (fieldRect.height - fieldStyle.lineHeight) / 2f);
                Color oldColor = GUI.color;
                GUI.color = GUI.skin.settings.cursorColor;
                GUI.DrawTexture(new Rect(caretX, caretY, 1f, Math.Max(14f, fieldStyle.lineHeight)), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
            TryCommitEditableField(item, controlName, value ?? string.Empty, next, previouslyFocused, focused);
            TrackEditableFieldFocus(controlName, focused);
        }

        private void TrackEditableFieldFocus(string controlName, bool focused)
        {
            if (string.IsNullOrEmpty(controlName))
                return;

            if (focused)
                _editableFieldsFocusedLastFrame.Add(controlName);
            else
                _editableFieldsFocusedLastFrame.Remove(controlName);
        }

        private void TryCommitEditableField(
            ScenarioAuthoringInspectorItem item,
            string controlName,
            string committedValue,
            string draftValue,
            bool previouslyFocused,
            bool focused)
        {
            if (item == null || item.Action == null || string.IsNullOrEmpty(item.Action.Id))
                return;

            bool enterPressed = focused
                && Event.current != null
                && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool lostFocus = previouslyFocused && !focused;
            if (!enterPressed && !lostFocus)
                return;

            string next = draftValue ?? string.Empty;
            string current = committedValue ?? string.Empty;
            if (!string.Equals(next, current, StringComparison.Ordinal))
                ScenarioAuthoringBackendService.Instance.ExecuteAction(item.Action.Id + ScenarioAuthoringActionCodec.EncodeToken(next));

            _editableFieldDrafts.Remove(controlName);
            if (enterPressed && Event.current != null)
            {
                GUI.FocusControl(null);
                Event.current.Use();
            }
        }

        private void DrawFieldFocusBorder(Rect rect)
        {
            if (_uiContext == null || _uiContext.Styles == null)
                return;

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderStrongTexture);
            GUI.color = oldColor;
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
                float previewSize = Mathf.Clamp(rect.height - 12f, 44f, 70f);
                Rect previewRect = new Rect(rect.x + 6f, rect.y + 6f, previewSize, previewSize);
                DrawSpritePreview(previewRect, action.PreviewSprite, action.Emphasized);
                textRect = new Rect(previewRect.xMax + 10f, rect.y + 8f, rect.width - previewRect.width - 22f, rect.height - 16f);
            }
            else
            {
                textRect = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);
            }

            GUIStyle labelStyle = new GUIStyle(_textStyle);
            labelStyle.wordWrap = false;
            labelStyle.clipping = TextClipping.Clip;
            float labelHeight = 20f;
            string fittedLabel = ShortenToFit(action.Label ?? string.Empty, textRect.width, labelStyle);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, labelHeight), new GUIContent(fittedLabel, BuildFullLabelTooltip(action)), labelStyle);
            string detail = !string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + labelHeight + 2f, textRect.width, Math.Max(16f, rect.height - labelHeight - 30f)), detail, _mutedTextStyle);

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

        private static string BuildFullLabelTooltip(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return string.Empty;

            string label = action.Label ?? string.Empty;
            string hint = action.Hint ?? action.Detail ?? string.Empty;
            if (string.IsNullOrEmpty(label))
                return hint;
            if (string.IsNullOrEmpty(hint) || hint.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
                return label;
            return label + ": " + hint;
        }

        private static bool IsBuildPaletteSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && !string.IsNullOrEmpty(section.Id)
                && (section.Id.IndexOf("palette", StringComparison.OrdinalIgnoreCase) >= 0
                    || section.Id.IndexOf("structure_tools", StringComparison.OrdinalIgnoreCase) >= 0
                    || section.Id.IndexOf("objects_", StringComparison.OrdinalIgnoreCase) >= 0
                    || section.Id.IndexOf("walls_", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DrawFactGrid(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            float availableWidth = GetSectionContentWidth();
            float gap = 6f;
            bool twoColumns = !compactInspector && availableWidth >= 420f;
            float cellWidth = twoColumns ? (availableWidth - gap) * 0.5f : availableWidth;
            float cellHeight = 64f;
            int column = 0;
            int actionColumn = 0;
            bool actionRow = false;
            int actionColumns = availableWidth >= 720f ? 4 : (availableWidth >= 460f ? 2 : 1);
            float actionWidth = (availableWidth - (gap * (actionColumns - 1))) / actionColumns;
            GUILayout.BeginHorizontal();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;

                if (item.Action != null)
                {
                    if (!actionRow)
                    {
                        if (column != 0)
                        {
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                        }
                        column = 0;
                        actionColumn = 0;
                        actionRow = true;
                    }

                    if (actionColumn >= actionColumns)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        actionColumn = 0;
                    }

                    float width = Math.Max(86f, MeasureButtonWidth(item.Action, false, 24f));
                    width = Math.Min(width, actionWidth);
                    Rect actionRect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.Height(28f));
                    DrawButton(actionRect, item.Action, false);
                    actionColumn++;
                    if (actionColumn < actionColumns)
                        GUILayout.Space(gap);
                    continue;
                }

                if (actionRow)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    actionRow = false;
                    actionColumn = 0;
                    column = 0;
                }

                Rect cellRect = GUILayoutUtility.GetRect(cellWidth, cellHeight, GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));
                DrawFactCell(cellRect, item);
                column++;
                if (twoColumns && column < 2)
                {
                    GUILayout.Space(gap);
                }
                else if (i + 1 < section.Items.Length)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    column = 0;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCastCardGrid(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            float availableWidth = GetSectionContentWidth();
            float gap = 8f;
            DrawCastSectionActions(section, availableWidth, gap);

            int cardCount = CountCastCards(section);
            if (cardCount == 0)
            {
                DrawCastSectionText(section, compactInspector);
                return;
            }

            float minCardWidth = compactInspector ? 250f : 286f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + gap) / (minCardWidth + gap)), 1, compactInspector ? 2 : 4);
            float cardWidth = (availableWidth - (gap * (columns - 1))) / columns;
            float cardHeight = compactInspector ? 174f : 198f;
            int column = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.CastCard == null)
                    continue;

                Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                DrawCastCard(cardRect, item.CastCard);
                column++;
                if (column >= columns && HasMoreCastCards(section, i + 1))
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(gap);
                    GUILayout.BeginHorizontal();
                    column = 0;
                }
                else if (column < columns)
                {
                    GUILayout.Space(gap);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCastSectionActions(ScenarioAuthoringInspectorSection section, float availableWidth, float gap)
        {
            float rowWidth = 0f;
            bool hasActions = false;
            GUILayout.BeginHorizontal();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null)
                    continue;

                float width = Math.Max(94f, MeasureButtonWidth(item.Action, false, 22f));
                width = Math.Min(width, availableWidth);
                if (rowWidth > 0f && rowWidth + width > availableWidth)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }

                Rect rect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.Height(28f));
                DrawButton(rect, item.Action, false);
                GUILayout.Space(gap);
                rowWidth += width + gap;
                hasActions = true;
            }
            GUILayout.EndHorizontal();
            if (hasActions)
                GUILayout.Space(8f);
        }

        private void DrawCastSectionText(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action != null || item.CastCard != null)
                    continue;

                DrawItem(item, compactInspector);
            }
        }

        private void DrawCastCard(Rect rect, ScenarioCastCardViewModel card)
        {
            if (card == null)
                return;

            ScenarioAuthoringInspectorAction primary = card.PrimaryAction;
            bool clickable = primary != null && primary.Enabled && !string.IsNullOrEmpty(primary.Id);
            GUIContent content = new GUIContent(string.Empty, primary != null ? primary.Hint ?? primary.Detail ?? string.Empty : string.Empty);
            RegisterInteractiveRegion(rect);
            GUI.Box(rect, content, _uiContext.Styles.Card);

            bool hovered = rect.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero);
            DrawButtonAnimationOverlay(rect, primary != null ? primary.Id : null, clickable, hovered, false);

            Rect portraitRect = new Rect(rect.x + 10f, rect.y + 10f, 82f, 96f);
            DrawCastPortrait(portraitRect, card);

            Rect statusRect = new Rect(rect.xMax - 92f, rect.y + 10f, 76f, 20f);
            if (!string.IsNullOrEmpty(card.Status))
                ScenarioUiWidgets.DrawPill(statusRect, card.Status, _uiContext.Styles, ResolveCastStatusEmphasis(card.Status));

            float textX = portraitRect.xMax + 10f;
            float textWidth = Math.Max(60f, rect.xMax - textX - 12f);
            GUIStyle nameStyle = _uiContext.Styles.PaperTitleText;
            GUIStyle detailStyle = _uiContext.Styles.PaperMutedText;
            GUI.Label(new Rect(textX, rect.y + 34f, textWidth, 24f), ShortenToFit(card.Name ?? "Survivor", textWidth, nameStyle), nameStyle);
            GUI.Label(new Rect(textX, rect.y + 58f, textWidth, 20f), ShortenToFit(card.RoleLine ?? string.Empty, textWidth, detailStyle), detailStyle);
            if (!string.IsNullOrEmpty(card.ArrivalSummary))
                GUI.Label(new Rect(textX, rect.y + 80f, textWidth, 20f), ShortenToFit(card.ArrivalSummary, textWidth, detailStyle), detailStyle);

            DrawCastStats(new Rect(rect.x + 10f, portraitRect.yMax + 8f, rect.width - 20f, 42f), card.Stats);
            DrawCastTraitChips(new Rect(rect.x + 10f, rect.yMax - 52f, rect.width - 20f, 18f), card.Traits);
            Rect actionsRect = new Rect(rect.x + 10f, rect.yMax - 29f, rect.width - 20f, 24f);
            DrawCastCardActions(actionsRect, card);

            Event evt = Event.current;
            if (clickable
                && evt != null
                && evt.type == EventType.MouseDown
                && evt.button == 0
                && rect.Contains(evt.mousePosition)
                && !actionsRect.Contains(evt.mousePosition))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(primary.Id);
                evt.Use();
            }
        }

        private void DrawCastPortrait(Rect rect, ScenarioCastCardViewModel card)
        {
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, _uiContext.Styles.PanelInsetTexture);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
            Rect imageRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 22f);
            if (card == null || (card.PortraitTexture == null && (card.PortraitSprite == null || card.PortraitSprite.texture == null)))
            {
                GUI.Label(imageRect, "No Portrait", _uiContext.Styles.EmptyStateText);
            }
            else if (card.PortraitTexture != null)
            {
                DrawTextureFitted(imageRect, card.PortraitTexture, 0f);
            }
            else
            {
                DrawSpritePreview(imageRect, card.PortraitSprite, false);
            }

            float swatch = 9f;
            float y = rect.yMax - swatch - 5f;
            float x = rect.x + 8f;
            DrawColorSwatch(new Rect(x, y, swatch, swatch), card.HairColor);
            DrawColorSwatch(new Rect(x + 14f, y, swatch, swatch), card.SkinColor);
            DrawColorSwatch(new Rect(x + 28f, y, swatch, swatch), card.ShirtColor);
            DrawColorSwatch(new Rect(x + 42f, y, swatch, swatch), card.PantsColor);
        }

        private void DrawCastStats(Rect rect, ScenarioCastStatViewModel[] stats)
        {
            if (stats == null || stats.Length == 0)
            {
                GUI.Label(rect, "Stats unavailable", _mutedTextStyle);
                return;
            }

            int count = Math.Min(stats.Length, 5);
            float gap = 5f;
            float columnWidth = (rect.width - (gap * (count - 1))) / count;
            for (int i = 0; i < count; i++)
            {
                ScenarioCastStatViewModel stat = stats[i];
                Rect statRect = new Rect(rect.x + (i * (columnWidth + gap)), rect.y, columnWidth, rect.height);
                DrawCastStat(statRect, stat);
            }
        }

        private void DrawCastStat(Rect rect, ScenarioCastStatViewModel stat)
        {
            if (stat == null)
                return;

            GUIStyle labelStyle = new GUIStyle(_mutedTextStyle);
            labelStyle.normal.textColor = _uiContext.Styles.PaperMutedText.normal.textColor;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 14f), new GUIContent(stat.Label ?? string.Empty, stat.Id + " " + stat.Value.ToString() + "/" + stat.Max.ToString()), labelStyle);

            Rect bar = new Rect(rect.x + 2f, rect.y + 18f, rect.width - 4f, 8f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(bar, _uiContext.Styles.PanelInsetTexture);
            float max = Math.Max(1, stat.Max);
            float fillWidth = Mathf.Clamp01(stat.Value / max) * Math.Max(0f, bar.width - 4f);
            Rect fill = new Rect(bar.x + 2f, bar.y + 2f, fillWidth, bar.height - 4f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.45f, 0.61f, 0.32f, 1f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawCastTraitChips(Rect rect, string[] traits)
        {
            if (traits == null || traits.Length == 0)
                return;

            float x = rect.x;
            int max = Math.Min(traits.Length, 3);
            for (int i = 0; i < max; i++)
            {
                string trait = traits[i] ?? string.Empty;
                GUIStyle chipText = _uiContext.Styles.PaperMutedText;
                float width = Mathf.Clamp(chipText.CalcSize(new GUIContent(trait)).x + 18f, 52f, Math.Max(52f, rect.xMax - x));
                if (x + width > rect.xMax)
                    break;

                ScenarioUiWidgets.DrawPill(new Rect(x, rect.y, width, rect.height), ShortenToFit(trait, width - 10f, chipText), _uiContext.Styles, ScenarioUiPillEmphasis.Default);
                x += width + 5f;
            }
        }

        private void DrawCastCardActions(Rect rect, ScenarioCastCardViewModel card)
        {
            float x = rect.x;
            if (card.PrimaryAction != null)
            {
                float width = Mathf.Clamp(MeasureButtonWidth(card.PrimaryAction, false, 18f), 58f, 112f);
                DrawButton(new Rect(x, rect.y, width, rect.height), card.PrimaryAction, false);
                x += width + 4f;
            }

            ScenarioAuthoringInspectorAction[] actions = card.SecondaryActions;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 18f), 46f, 86f);
                if (x + width > rect.xMax)
                    break;

                DrawButton(new Rect(x, rect.y, width, rect.height), action, false);
                x += width + 4f;
            }
        }

        private static ScenarioUiPillEmphasis ResolveCastStatusEmphasis(string status)
        {
            if (StringContains(status, "active") || StringContains(status, "starting"))
                return ScenarioUiPillEmphasis.Success;
            if (StringContains(status, "future"))
                return ScenarioUiPillEmphasis.Active;
            if (StringContains(status, "dead") || StringContains(status, "unconscious") || StringContains(status, "catatonic"))
                return ScenarioUiPillEmphasis.Danger;
            if (StringContains(status, "away"))
                return ScenarioUiPillEmphasis.Warning;
            return ScenarioUiPillEmphasis.Default;
        }

        private static int CountCastCards(ScenarioAuthoringInspectorSection section)
        {
            int count = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                if (section.Items[i] != null && section.Items[i].CastCard != null)
                    count++;
            }

            return count;
        }

        private static bool HasMoreCastCards(ScenarioAuthoringInspectorSection section, int startIndex)
        {
            for (int i = Math.Max(0, startIndex); section != null && section.Items != null && i < section.Items.Length; i++)
            {
                if (section.Items[i] != null && section.Items[i].CastCard != null)
                    return true;
            }

            return false;
        }

        private static void DrawColorSwatch(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawFactCell(Rect rect, ScenarioAuthoringInspectorItem item)
        {
            if (item == null)
                return;

            if (!string.IsNullOrEmpty(item.PulseKey))
                DrawItemPulseOverlay(rect, item);

            GUIContent label = new GUIContent(item.Label ?? string.Empty, item.HoverHint ?? item.Detail ?? item.Value ?? string.Empty);
            GUIContent value = new GUIContent(item.Value ?? string.Empty, item.HoverHint ?? item.Detail ?? string.Empty);
            Rect labelRect = new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 16f);
            Rect valueRect = new Rect(rect.x + 8f, rect.y + 20f, rect.width - 16f, rect.height - 24f);
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
            GUI.Label(labelRect, label, _mutedTextStyle);
            GUIStyle valueStyle = new GUIStyle(_textStyle);
            valueStyle.wordWrap = true;
            GUI.Label(valueRect, value, valueStyle);
        }

        private void DrawItemPulseOverlay(Rect rect, ScenarioAuthoringInspectorItem item)
        {
            string signature = item.PulseSignature ?? item.Value ?? item.Label ?? string.Empty;
            float pulse = _animations.GetPulseProgress(item.PulseKey, signature, 0.70f, ScenarioUiEasing.EaseOut);
            if (pulse <= 0.001f)
                return;

            Color oldColor = GUI.color;
            GUI.color = new Color(0.94f, 0.80f, 0.52f, 0.32f * pulse);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
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

        private static void DrawTextureFitted(Rect rect, Texture texture, float padding)
        {
            if (texture == null)
                return;

            Rect inner = new Rect(rect.x + padding, rect.y + padding, rect.width - (padding * 2f), rect.height - (padding * 2f));
            if (inner.width <= 0f || inner.height <= 0f || texture.width <= 0 || texture.height <= 0)
                return;

            float scale = Mathf.Min(inner.width / texture.width, inner.height / texture.height);
            Rect fitted = new Rect(
                inner.x + ((inner.width - (texture.width * scale)) * 0.5f),
                inner.y + ((inner.height - (texture.height * scale)) * 0.5f),
                texture.width * scale,
                texture.height * scale);
            GUI.DrawTexture(fitted, texture, ScaleMode.StretchToFill, true);
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
            return ScenarioUiMeasuredLabel.FitLabelWithEllipsis(value, maxWidth, style);
        }

        private static string MiddleTruncate(string value, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(value) || style == null)
                return value ?? string.Empty;
            if (style.CalcSize(new GUIContent(value)).x <= maxWidth)
                return value;

            const string ellipsis = "...";
            float ellipsisWidth = style.CalcSize(new GUIContent(ellipsis)).x;
            if (ellipsisWidth >= maxWidth)
                return string.Empty;

            int left = Math.Min(12, value.Length);
            int right = Math.Min(18, Math.Max(0, value.Length - left));
            while (left + right > 0)
            {
                string candidate = value.Substring(0, left) + ellipsis + value.Substring(value.Length - right);
                if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
                    return candidate;

                if (left > right && left > 4)
                    left--;
                else if (right > 6)
                    right--;
                else if (left > 4)
                    left--;
                else
                    break;
            }

            return ScenarioUiMeasuredLabel.FitLabelWithEllipsis(value, maxWidth, style);
        }

        private Rect DrawSettingsWindow(
            Rect rect,
            ScenarioAuthoringSettingsViewModel settings,
            ScenarioAuthoringShellWindowViewModel window)
        {
            // TODO(centralize): Settings still render as a standalone editor window.
            // Move them into the central workspace/settings surface after navigation is settled.
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
            RegisterScrollRegion((window != null ? window.Id : null) ?? "settings", bodyRect);
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
                        Enabled = item.Enabled && item.CanDecrease,
                        DisabledReason = item.Enabled ? "This setting is already at its minimum value." : "This setting cannot be changed."
                    },
                    false);
                GUILayout.Label(item.ValueText ?? string.Empty, _uiContext.Styles.Field, GUILayout.Width(84f), GUILayout.Height(24f));
                DrawButton(GUILayoutUtility.GetRect(26f, 24f, GUILayout.Width(26f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingIncreasePrefix + item.Id,
                        Label = "+",
                        Enabled = item.Enabled && item.CanIncrease,
                        DisabledReason = item.Enabled ? "This setting is already at its maximum value." : "This setting cannot be changed."
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
                float width = Math.Max(58f, MeasureButtonWidth(action, false, 18f));
                width = Math.Min(width, rowLimit);
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

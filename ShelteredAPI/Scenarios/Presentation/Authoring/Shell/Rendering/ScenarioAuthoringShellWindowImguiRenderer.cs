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
            float slideProgress = visual != null ? (1f - visual.Slide) : 1f;
            Rect slidingRect = ResolveWindowSlidingRect(rect, slideProgress);
            using (ScenarioUiGuiScope.Apply(alpha, slidingRect, scale))
            {
                bool scaled = Mathf.Abs(scale - 1f) > 0.0001f;
                if (scaled)
                    _scaledWindowDrawDepth++;

                try
                {
                    return DrawWindowCoreUnscoped(slidingRect, window);
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
            scrollPosition = BeginMeasuredScrollView(scrollPosition, bodyRect);
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

            bool homeWorkshopPage = IsHomeWorkshopPage(window);
            Rect pageRect = homeWorkshopPage
                ? ScenarioAuthoringShellLayout.BuildHomeWorkshopPageRect(contentRect)
                : ScenarioAuthoringShellLayout.BuildWorkshopPageRect(contentRect);
            Rect bodyRect;
            if (homeWorkshopPage)
            {
                bodyRect = new Rect(pageRect.x, pageRect.y, pageRect.width, pageRect.height);
            }
            else
            {
                Rect ribbonRect = new Rect(pageRect.x, pageRect.y, pageRect.width, ScenarioAuthoringShellLayout.WorkshopTimelineRibbonHeight);
                DrawWorkshopTimelineRibbon(ribbonRect, window);
                bodyRect = new Rect(
                    pageRect.x,
                    ribbonRect.yMax,
                    pageRect.width,
                    Math.Max(120f, pageRect.yMax - ribbonRect.yMax));
            }
            if (IsAssetBrowserWorkshopPage(window))
                return DrawAssetBrowserWorkshopPage(bodyRect, window);

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
            scrollPosition = BeginMeasuredScrollView(scrollPosition, bodyRect);
            if (homeWorkshopPage)
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
            scrollPosition = BeginMeasuredScrollView(scrollPosition, bodyRect);
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
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                return DrawPlaceflowBrowserWindow(rect, window);

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
            scrollPosition = BeginMeasuredScrollView(scrollPosition, pickerScrollRect);
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

        private Rect DrawPlaceflowBrowserWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            Rect headerRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 34f);
            GUI.Label(new Rect(headerRect.x, headerRect.y + 3f, Math.Max(120f, headerRect.width - 80f), 26f), window.Title ?? "Tool Workspace", _smallTitleStyle);

            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            float actionX = headerRect.xMax - 24f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                DrawButton(new Rect(actionX, headerRect.y + 5f, 22f, 22f), chromeActions[i], false);
                actionX -= 24f;
            }

            Rect bodyRect = new Rect(rect.x + 12f, headerRect.yMax + 6f, rect.width - 24f, Math.Max(120f, rect.yMax - headerRect.yMax - 18f));
            return DrawAssetBrowserWorkshopPage(bodyRect, window, true);
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
            scrollPosition = BeginMeasuredScrollView(scrollPosition, bodyRect);
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

        private Vector2 BeginMeasuredScrollView(Vector2 scrollPosition, Rect viewportRect)
        {
            return GUILayout.BeginScrollView(
                scrollPosition,
                false,
                true,
                GUILayout.Width(Math.Max(1f, viewportRect.width)),
                GUILayout.Height(Math.Max(1f, viewportRect.height)));
        }

        private Rect DrawPixelEditorWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            // TODO(centralize): Pixel editor is still a dedicated floating-style window.
            // Merge it into the central art/edit workspace when the art workflow is finalized.
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor =
                _snapshot != null && _snapshot.ShellViewModel != null
                    ? _snapshot.ShellViewModel.CustomSpriteEditor
                    : null;
            if (editor != null && editor.AnimationPlaying && _snapshot != null)
                ScenarioSpriteSwapAuthoringService.Instance.TickAnimationPreview(_snapshot.State);

            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioUiWindowRegions regions = _uiContext.Frame.Build(
                rect,
                string.Empty,
                null,
                false,
                40f,
                12f + (chromeActions.Length * 24f));
            Rect headerRect = regions.Header;
            float actionX = headerRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                Rect actionRect = new Rect(actionX, headerRect.y + 6f, 22f, 22f);
                DrawButton(actionRect, chromeActions[i], false);
                actionX -= 24f;
            }

            DrawPixelEditorHeader(headerRect, editor, actionX - headerRect.x);

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

        private void DrawPixelEditorHeader(Rect headerRect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float titleWidth)
        {
            string title = editor != null && !string.IsNullOrEmpty(editor.SourceLabel)
                ? "Pixel Editor - " + editor.SourceLabel
                : "Pixel Editor";
            if (editor != null && editor.Dirty)
                title += " *";

            string secondary = string.Empty;
            if (editor != null && editor.IsAnimationEditor)
            {
                secondary = "Frame "
                    + (editor.AnimationFrameIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "/"
                    + editor.AnimationFrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " - "
                    + FormatSeconds(editor.AnimationFrameDurationSeconds);
            }
            else if (editor != null)
            {
                secondary = editor.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "x"
                    + editor.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            Rect inner = new Rect(headerRect.x + 12f, headerRect.y + 4f, Math.Max(80f, titleWidth - 18f), headerRect.height - 8f);
            float secondaryWidth = !string.IsNullOrEmpty(secondary)
                ? Mathf.Clamp(ScenarioUiMeasuredLabel.Width(secondary, _mutedTextStyle, 12f), 96f, Math.Min(220f, inner.width * 0.42f))
                : 0f;
            Rect titleRect = new Rect(inner.x, inner.y + 3f, Math.Max(40f, inner.width - secondaryWidth - 10f), 24f);
            Rect secondaryRect = new Rect(titleRect.xMax + 10f, inner.y + 7f, secondaryWidth, 18f);
            GUI.Label(titleRect, ShortenToFit(title, titleRect.width, _smallTitleStyle), _smallTitleStyle);
            if (secondaryWidth > 0f)
                GUI.Label(secondaryRect, secondary, _mutedTextStyle);
        }

        private void DrawCustomSpriteEditorDedicated(Rect bodyRect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            float controlsWidth = Mathf.Clamp(bodyRect.width * 0.26f, 248f, 288f);
            controlsWidth = Math.Min(controlsWidth, Math.Max(224f, bodyRect.width - 360f));
            float controlsContentWidth = Math.Max(196f, controlsWidth - 20f);
            float footerHeight = editor.IsAnimationEditor ? 76f : 0f;
            Rect toolsRect = new Rect(bodyRect.x, bodyRect.y, controlsWidth, Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect canvasPane = new Rect(toolsRect.xMax + 10f, bodyRect.y, Math.Max(160f, bodyRect.xMax - toolsRect.xMax - 10f), Math.Max(120f, bodyRect.height - footerHeight - 8f));
            Rect footerRect = new Rect(bodyRect.x, bodyRect.yMax - footerHeight, bodyRect.width, footerHeight);

            GUILayout.BeginArea(toolsRect);
            RegisterInteractiveRegion(toolsRect);
            GUILayout.BeginVertical(_uiContext.Styles.Section, GUILayout.Width(Math.Max(180f, toolsRect.width)));
            DrawPixelEditorPrimaryControls(editor, controlsContentWidth);
            GUILayout.Space(4f);
            if (editor.IsCharacterEditor)
            {
                DrawCharacterPartToolbar(editor, controlsContentWidth);
                GUILayout.Space(4f);
            }
            if (editor.IsAnimationEditor)
            {
                DrawPixelEditorAnimationGroup(editor, controlsContentWidth);
                GUILayout.Space(4f);
            }
            DrawPixelEditorClipboardGroup(editor, controlsContentWidth);
            GUILayout.Space(4f);
            DrawPixelEditorColorGroup(editor, controlsContentWidth);
            GUILayout.EndVertical();
            GUILayout.EndArea();

            DrawPixelCanvasViewport(canvasPane, editor);

            if (editor.IsAnimationEditor && footerRect.height > 0f)
            {
                GUILayout.BeginArea(footerRect);
                DrawAnimationTimeline(editor, footerRect.width);
                GUILayout.EndArea();
            }
        }

        private void DrawPixelEditorPrimaryControls(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            DrawCustomEditorToolbar(editor, contentWidth);
            GUILayout.Space(4f);
            DrawCustomZoomToolbar(editor, contentWidth);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            float saveWidth = (contentWidth - 6f) * 0.5f;
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, "Save", editor.Dirty, saveWidth, editor.IsAnimationEditor ? "Save edited animation frames. Ctrl+S" : "Save the current pixel edit. Ctrl+S");
            GUILayout.Space(6f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, "Discard", false, saveWidth, "Discard the current pixel edit.", editor.Dirty);
            GUILayout.EndHorizontal();
        }

        private void DrawPixelEditorAnimationGroup(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            if (!DrawPixelEditorGroupHeader("pixel_editor.group.animation", "Animation", true))
                return;

            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepPrevious, "<", false, 34f, "Previous frame.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayPause, editor.AnimationPlaying ? "Pause" : "Play", editor.AnimationPlaying, 62f, "Preview inside the editor.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepNext, ">", false, 34f, "Next frame.");
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayInWorld, editor.AnimationPlayingInWorld ? "Stop World" : "Play In World", editor.AnimationPlayingInWorld, Math.Min(contentWidth, 118f), "Play the edited animation on the selected world asset.");
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationOnionToggle, "Onion", editor.OnionSkin, 68f, "Ghost neighboring frames under the current frame.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationCompareToggle, "Original", editor.CompareOriginal, 78f, "Toggle the original frame overlay.");
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            DrawAnimationSpeedSlider(editor, contentWidth);
            if (DrawPixelEditorGroupHeader("pixel_editor.group.animation.more", "More", false))
            {
                GUILayout.BeginHorizontal();
                DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertFrame, "Revert Frame", false, Math.Max(104f, (contentWidth - 6f) * 0.5f), "Revert the current frame.");
                GUILayout.Space(6f);
                DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertAll, "Revert Animation", false, Math.Max(126f, (contentWidth - 6f) * 0.5f), "Revert all edited frames.");
                GUILayout.EndHorizontal();
            }
        }

        private void DrawPixelEditorClipboardGroup(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            if (!DrawPixelEditorGroupHeader("pixel_editor.group.clipboard", "Clipboard", false))
                return;

            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy, "Copy", false, 58f, "Copy the current selection. If nothing is selected, copy the whole sprite. Ctrl+C", true);
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste, "Paste", editor.HasClipboard, 62f, editor.HasClipboard ? "Paste the pixel clipboard into the canvas. Ctrl+V" : "Pixel clipboard is empty.", editor.HasClipboard);
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear, "Clear Selection", editor.HasSelection, Math.Min(contentWidth, 132f), editor.HasSelection ? "Clear the current pixel selection." : "There is no active selection.", editor.HasSelection);
            GUILayout.Label(BuildSelectionSummary(editor), _mutedTextStyle);
            GUILayout.Label(BuildClipboardSummary(editor), _mutedTextStyle);
        }

        private void DrawPixelEditorColorGroup(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            if (!DrawPixelEditorGroupHeader("pixel_editor.group.color", "Color", true))
                return;

            GUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(48f, 28f, GUILayout.Width(48f), GUILayout.Height(28f));
            DrawColorPreview(colorRect, editor.ActiveColor);
            GUILayout.Label("#" + (editor.ActiveColorHex ?? "000000FF"), _textStyle, GUILayout.Width(Math.Max(92f, contentWidth - 112f)));
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f), GUILayout.Height(20f));
                DrawBrushSwatch(swatchRect, editor.BrushPalette[i], i == editor.ActiveBrushIndex, i);
                GUILayout.Space(2f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick, "Picker", editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick, 70f, "Use the picker tool to sample a color from the sprite.");
            GUILayout.Label("Sample on canvas", _mutedTextStyle, GUILayout.Width(Math.Max(88f, contentWidth - 78f)));
            GUILayout.EndHorizontal();
        }

        private bool DrawPixelEditorGroupHeader(string key, string label, bool defaultExpanded)
        {
            bool expanded = GetPixelEditorGroupExpanded(key, defaultExpanded);
            Rect rect = GUILayoutUtility.GetRect(120f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
            {
                Id = key,
                Label = (expanded ? "v " : "> ") + label,
                Hint = expanded ? "Collapse " + label + "." : "Expand " + label + ".",
                Enabled = true,
                Emphasized = expanded
            };
            if (DrawPlainButton(rect, new GUIContent(action.Label, action.Hint), expanded ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererPixelGroupTogglePrefix, key));
                if (Event.current != null)
                    Event.current.Use();
                expanded = GetPixelEditorGroupExpanded(key, defaultExpanded);
            }

            DrawButtonAnimationOverlay(rect, key, true, IsInteractiveHoverAllowed(rect), IsInteractiveMouseDownAllowed(rect));
            return expanded;
        }

        private bool GetPixelEditorGroupExpanded(string key, bool defaultExpanded)
        {
            return ScenarioAuthoringRendererInteractionState.Instance.GetDisclosureExpanded(key, defaultExpanded);
        }

        private void DrawAnimationToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            GUILayout.Label("Animation", _smallTitleStyle);
            GUILayout.Label(
                "Frame " + (editor.AnimationFrameIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + editor.AnimationFrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " | " + FormatSeconds(editor.AnimationFrameDurationSeconds)
                + " fixed",
                _mutedTextStyle);
            GUILayout.Label("Source timing and frame count are fixed by the vanilla clip.", _mutedTextStyle);
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 84f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepPrevious, "<", false, 42f, "Previous frame.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayPause, editor.AnimationPlaying ? "Pause" : "Play", editor.AnimationPlaying, buttonWidth, "Preview at the source game timing.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepNext, ">", false, 42f, "Next frame.");
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationOnionToggle, "Onion", editor.OnionSkin, buttonWidth, "Ghost neighboring frames under the current frame.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationCompareToggle, "Original", editor.CompareOriginal, buttonWidth, "Toggle the original frame overlay.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertFrame, "Revert", false, buttonWidth, "Revert the current frame.");
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationRevertAll, "Revert Animation", false, Math.Min(contentWidth, 180f), "Revert all edited frames.");
        }

        private void DrawAnimationTimeline(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepPrevious, "<", false, 34f, "Previous frame.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationPlayPause, editor.AnimationPlaying ? "Pause" : "Play", editor.AnimationPlaying, 62f, "Preview inside the editor.");
            GUILayout.Space(4f);
            DrawInlineAction(ScenarioAuthoringActionIds.ActionSpriteSwapAnimationStepNext, ">", false, 34f, "Next frame.");
            GUILayout.Space(8f);
            GUILayout.Label(
                (editor.AnimationClipName ?? "Animation")
                + " | " + editor.AnimationFrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " frames | "
                + editor.AnimationSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + "x",
                _mutedTextStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; editor.AnimationFrames != null && i < editor.AnimationFrames.Count; i++)
            {
                ScenarioSpriteSwapAuthoringService.AnimationFrameModel frame = editor.AnimationFrames[i];
                Rect frameRect = GUILayoutUtility.GetRect(46f, 44f, GUILayout.Width(46f), GUILayout.Height(44f));
                DrawAnimationFrameThumb(frameRect, frame, i == editor.AnimationFrameIndex);
                if (GUI.Button(frameRect, GUIContent.none, GUIStyle.none))
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioSpriteSwapAuthoringService.BuildAnimationFrameActionId(i));
                GUILayout.Space(4f);
            }
            GUILayout.FlexibleSpace();
            int previous = editor.AnimationFrameIndex > 0 ? editor.AnimationFrameIndex - 1 : Math.Max(0, editor.AnimationFrameCount - 1);
            DrawInlineAction(ScenarioSpriteSwapAuthoringService.BuildAnimationCopyActionId(previous), "Copy Prev", false, 92f, "Copy the previous frame into the current frame.");
            GUILayout.EndHorizontal();
        }

        private void DrawAnimationFrameThumb(Rect rect, ScenarioSpriteSwapAuthoringService.AnimationFrameModel frame, bool selected)
        {
            GUI.Box(rect, GUIContent.none, selected ? _uiContext.Styles.ButtonActive : _uiContext.Styles.Field);
            Rect imageRect = new Rect(rect.x + 5f, rect.y + 4f, rect.width - 10f, rect.height - 16f);
            Sprite sprite = frame != null ? frame.EditedSprite : null;
            if (sprite != null && sprite.texture != null)
                GUI.DrawTextureWithTexCoords(imageRect, sprite.texture, new Rect(0f, 0f, 1f, 1f), true);
            string label = frame != null
                ? ((frame.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + (frame.Dirty ? "*" : string.Empty))
                : "?";
            GUI.Label(new Rect(rect.x, rect.yMax - 14f, rect.width, 12f), label, _mutedTextStyle);
        }

        private static string FormatSeconds(float seconds)
        {
            return Math.Max(0f, seconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s";
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
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 62f);
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
            float buttonWidth = ResolveToolbarButtonWidth(contentWidth, 3, 4f, 42f);
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut,
                "-",
                false,
                42f,
                "Zoom out of the canvas.",
                editor.Zoom > 1);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset,
                Mathf.RoundToInt(Mathf.Max(1f, editor.Zoom) * 100f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%",
                false,
                Math.Max(76f, contentWidth - 92f),
                "Reset canvas zoom to 8x.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn,
                "+",
                false,
                42f,
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

            if (DrawPlainButton(rect, GUIContent.none, GUIStyle.none, true))
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

        private void DrawAnimationSpeedSlider(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth)
        {
            float currentValue = Mathf.Clamp(editor != null ? editor.AnimationSpeed : 1f, 0.25f, 2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed", _textStyle, GUILayout.Width(44f));
            float sliderWidth = Math.Max(72f, contentWidth - 94f);
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0.25f, 2f, GUILayout.Width(sliderWidth));
            GUILayout.Label(currentValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x", _mutedTextStyle, GUILayout.Width(42f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(nextValue - currentValue) <= 0.01f)
                return;

            float rounded = Mathf.Round(nextValue * 100f) / 100f;
            ScenarioAuthoringBackendService.Instance.ExecuteAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapAnimationSpeedPrefix
                + rounded.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            if (Event.current != null)
                Event.current.Use();
        }

        private void DrawColorSlider(string label, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, int channel)
        {
            DrawColorSlider(label, editor, 236f, channel);
        }

        private void DrawColorSlider(string label, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, float contentWidth, int channel)
        {
            Color activeColor = editor.ActiveColor;
            float currentValue = channel == 0
                ? activeColor.r
                : (channel == 1 ? activeColor.g : (channel == 2 ? activeColor.b : activeColor.a));
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _textStyle, GUILayout.Width(18f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0f, 1f, GUILayout.Width(Math.Max(70f, contentWidth - 60f)));
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
            if (current != null && IsPointerInsideGuiRect(inner, current) && IsInteractiveVisualTopmost(inner))
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

            DrawAnimationReferenceOverlays(rect, editor);
            GUI.DrawTextureWithTexCoords(rect, editor.PreviewSprite.texture, new Rect(0f, 0f, 1f, 1f), true);
            DrawPixelGrid(rect, editor, displayZoom);
            DrawSelectionOverlay(rect, editor, displayZoom);

            Event current = Event.current;
            if (current != null && rect.Contains(current.mousePosition) && IsInteractiveVisualTopmost(rect))
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

        private void DrawAnimationReferenceOverlays(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.IsAnimationEditor || editor.AnimationFrames == null || editor.AnimationFrames.Count == 0)
                return;

            Color previousColor = GUI.color;
            if (editor.OnionSkin && editor.AnimationFrames.Count > 1)
            {
                int previous = editor.AnimationFrameIndex > 0 ? editor.AnimationFrameIndex - 1 : editor.AnimationFrames.Count - 1;
                int next = (editor.AnimationFrameIndex + 1) % editor.AnimationFrames.Count;
                DrawAnimationFrameOverlay(rect, editor.AnimationFrames[previous], 0.28f, new Color(0.55f, 0.85f, 1f, 0.28f));
                if (next != previous)
                    DrawAnimationFrameOverlay(rect, editor.AnimationFrames[next], 0.18f, new Color(1f, 0.75f, 0.35f, 0.18f));
            }

            if (editor.CompareOriginal && editor.AnimationFrameIndex >= 0 && editor.AnimationFrameIndex < editor.AnimationFrames.Count)
                DrawAnimationFrameOverlay(rect, editor.AnimationFrames[editor.AnimationFrameIndex], 0.45f, new Color(1f, 1f, 1f, 0.45f), true);
            GUI.color = previousColor;
        }

        private void DrawAnimationFrameOverlay(Rect rect, ScenarioSpriteSwapAuthoringService.AnimationFrameModel frame, float alpha, Color tint)
        {
            DrawAnimationFrameOverlay(rect, frame, alpha, tint, false);
        }

        private void DrawAnimationFrameOverlay(Rect rect, ScenarioSpriteSwapAuthoringService.AnimationFrameModel frame, float alpha, Color tint, bool original)
        {
            if (frame == null)
                return;

            Sprite sprite = original ? frame.OriginalSprite : frame.EditedSprite;
            if (sprite == null || sprite.texture == null)
                return;

            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, new Rect(0f, 0f, 1f, 1f), true);
            GUI.color = new Color(1f, 1f, 1f, alpha);
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
            searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            candidateFilter = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateFilter(controlName, CandidateFilterAll);
            GUILayout.BeginArea(rect);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(26f));
            Rect searchRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
            bool searchTopmost = IsInteractiveVisualTopmost(searchRect);
            string nextSearchText;
            if (searchTopmost)
            {
                GUI.SetNextControlName(controlName);
                nextSearchText = GUI.TextField(searchRect, searchText ?? string.Empty, _uiContext.Styles.SearchField);
            }
            else
            {
                nextSearchText = searchText ?? string.Empty;
                GUI.Box(searchRect, nextSearchText, _uiContext.Styles.SearchField);
            }
            if (!string.Equals(nextSearchText, searchText ?? string.Empty, StringComparison.Ordinal))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, controlName + "\n" + nextSearchText));
                searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            }
            DrawSearchPlaceholder(searchRect, searchText, "Filter choices");
            if (searchTopmost && (string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal) || (Event.current != null && searchRect.Contains(Event.current.mousePosition))))
                DrawFieldFocusBorder(searchRect);

            Rect clearRect = GUILayoutUtility.GetRect(64f, 26f, GUILayout.Width(64f), GUILayout.Height(26f));
            if (DrawPlainButton(clearRect, new GUIContent("Clear"), _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, controlName + "\n"));
                searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawCandidateFilterButton(controlName, "All", CandidateFilterAll, ref candidateFilter);
            DrawCandidateFilterButton(controlName, "Active", CandidateFilterActive, ref candidateFilter);
            DrawCandidateFilterButton(controlName, "Vanilla", CandidateFilterVanilla, ref candidateFilter);
            DrawCandidateFilterButton(controlName, "Scenario", CandidateFilterScenario, ref candidateFilter);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            searchFocused = searchTopmost && string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
        }

        private void DrawCandidateSearchControl(
            Rect rect,
            string controlName,
            ref string searchText,
            ref bool searchFocused)
        {
            searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(26f));
            Rect searchRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
            bool searchTopmost = IsInteractiveVisualTopmost(searchRect);
            string nextSearchText;
            if (searchTopmost)
            {
                GUI.SetNextControlName(controlName);
                nextSearchText = GUI.TextField(searchRect, searchText ?? string.Empty, _uiContext.Styles.SearchField);
            }
            else
            {
                nextSearchText = searchText ?? string.Empty;
                GUI.Box(searchRect, nextSearchText, _uiContext.Styles.SearchField);
            }
            if (!string.Equals(nextSearchText, searchText ?? string.Empty, StringComparison.Ordinal))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, controlName + "\n" + nextSearchText));
                searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            }
            DrawSearchPlaceholder(searchRect, searchText, "Filter choices");
            if (searchTopmost && (string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal) || (Event.current != null && searchRect.Contains(Event.current.mousePosition))))
                DrawFieldFocusBorder(searchRect);

            Rect clearRect = GUILayoutUtility.GetRect(64f, 26f, GUILayout.Width(64f), GUILayout.Height(26f));
            if (DrawPlainButton(clearRect, new GUIContent("Clear"), _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, controlName + "\n"));
                searchText = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateSearch(controlName);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            searchFocused = searchTopmost && string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
        }

        private void DrawCandidateFilterButton(string controlName, string label, string value, ref string candidateFilter)
        {
            bool active = string.Equals(candidateFilter, value, StringComparison.OrdinalIgnoreCase);
            Rect rect = GUILayoutUtility.GetRect(78f, 26f, GUILayout.Width(78f), GUILayout.Height(26f));
            if (DrawPlainButton(rect, new GUIContent(label), active ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateFilterPrefix, controlName + "\n" + value));
                candidateFilter = ScenarioAuthoringRendererInteractionState.Instance.GetCandidateFilter(controlName, CandidateFilterAll);
            }
        }

        private static bool IsHomeWorkshopPage(ScenarioAuthoringShellWindowViewModel window)
        {
            return window != null
                && string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(window.Title, "Test", StringComparison.OrdinalIgnoreCase);
        }

        // Softened Home landing: one calm reading order of orient -> create ->
        // refine -> share. Orientation (title, save state, one "what next"
        // element) leads; the question cards read as a lighter table of
        // contents grouped into bands; every secondary panel (base, details,
        // status) collapses to a one-line summary that expands on click so the
        // first screenful never slams the reader with everything at once.
        private void DrawHomeWorkshopPage(ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorSection identity = FindSection(window, "home_identity");
            ScenarioAuthoringInspectorSection next = FindSection(window, "home_next");
            ScenarioAuthoringInspectorSection setup = FindSection(window, "home_setup_checklist");
            ScenarioAuthoringInspectorSection baseMode = FindSection(window, "home_base_mode");
            ScenarioAuthoringInspectorSection details = FindSection(window, "home_metadata");
            ScenarioAuthoringInspectorSection status = FindSection(window, "home_save_status");
            ScenarioAuthoringInspectorSection quickActions = FindSection(window, "home_quick_actions");
            ScenarioAuthoringInspectorSection advanced = FindSection(window, "home_advanced");

            // ORIENT.
            if (identity != null)
            {
                DrawHomeIdentityHeader(identity);
                GUILayout.Space(10f);
            }

            // ORIENT: exactly one primary "what next" element. While the setup
            // checklist is present (draft incomplete) it is the guidance; once
            // it auto-hides on completion the "what next" callout takes over.
            if (setup != null)
            {
                DrawHomeSetupChecklist(setup);
                GUILayout.Space(14f);
            }
            else if (next != null)
            {
                DrawHomeNextCallout(next);
                GUILayout.Space(14f);
            }

            // CREATE -> REFINE -> SHARE bands of question cards.
            DrawHomeQuestionBands(window);

            // Progressive disclosure of secondary panels, collapsed at rest.
            GUILayout.Space(14f);
            if (baseMode != null)
                DrawHomeCollapsibleGroup("home.group.base", "Scenario base", BuildBaseModeSummary(baseMode), false, baseMode, DrawHomeBaseSelectorBody);
            if (details != null)
                DrawHomeCollapsibleGroup("home.group.details", "Scenario details", BuildDetailsSummary(details), false, details, DrawHomeDetailsBody);
            if (status != null)
                DrawHomeCollapsibleGroup("home.group.status", "Save & export status", BuildStatusSummary(status), false, status, DrawHomeStatusBody);

            if (quickActions != null)
            {
                GUILayout.Space(10f);
                DrawSection(quickActions);
            }

            if (advanced != null)
            {
                GUILayout.Space(8f);
                DrawSection(advanced);
            }
        }

        // Renders a collapsible Home panel: a full-width toggle bar showing the
        // group title and, while collapsed, a one-line summary. Modeled on the
        // pixel editor's group-header pattern so the collapse behaviour stays
        // consistent with the rest of the editor.
        private void DrawHomeCollapsibleGroup(
            string key,
            string title,
            string summary,
            bool defaultExpanded,
            ScenarioAuthoringInspectorSection section,
            Action<ScenarioAuthoringInspectorSection> drawBody)
        {
            bool expanded = GetHomeGroupExpanded(key, defaultExpanded);
            Rect rect = GUILayoutUtility.GetRect(120f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            string glyph = expanded ? "v " : "> ";
            string header = string.IsNullOrEmpty(summary) || expanded
                ? glyph + title
                : glyph + title + "  -  " + summary;
            string hint = expanded ? "Collapse " + title + "." : "Expand " + title + ".";
            if (DrawPlainButton(rect, new GUIContent(header, hint), expanded ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererHomeGroupTogglePrefix, key));
                if (Event.current != null)
                    Event.current.Use();
                expanded = GetHomeGroupExpanded(key, defaultExpanded);
            }
            DrawButtonAnimationOverlay(rect, key, true, IsInteractiveHoverAllowed(rect), IsInteractiveMouseDownAllowed(rect));
            if (expanded && drawBody != null)
            {
                GUILayout.Space(4f);
                drawBody(section);
            }
            GUILayout.Space(8f);
        }

        private bool GetHomeGroupExpanded(string key, bool defaultExpanded)
        {
            return ScenarioAuthoringRendererInteractionState.Instance.GetDisclosureExpanded(key, defaultExpanded);
        }

        // The "what next" callout: leads with the guidance line, then a quiet
        // row of the fix / test / help actions beneath it.
        private void DrawHomeNextCallout(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Card);
            GUIStyle guidanceStyle = new GUIStyle(_sectionTitleStyle);
            guidanceStyle.wordWrap = true;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action != null)
                    continue;
                string text = item.Value ?? item.Label ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                    GUILayout.Label(text, guidanceStyle);
            }

            float rowLimit = GetSectionContentWidth();
            float rowWidth = 0f;
            bool rowOpen = false;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null)
                    continue;
                if (!rowOpen)
                {
                    GUILayout.Space(6f);
                    GUILayout.BeginHorizontal();
                    rowOpen = true;
                }
                float width = Mathf.Clamp(MeasureButtonWidth(item.Action, false, 20f), 94f, Math.Min(300f, rowLimit));
                if (rowWidth > 0f && rowWidth + width > rowLimit)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }
                Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                DrawButton(rect, item.Action, false);
                GUILayout.Space(6f);
                rowWidth += width + 6f;
            }
            if (rowOpen)
                GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static string BuildBaseModeSummary(ScenarioAuthoringInspectorSection section)
        {
            // The first enabled/selected base option label doubles as the
            // current base name; the "OK" icon marks the selected option.
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Action != null && string.Equals(item.Action.IconText, "OK", StringComparison.OrdinalIgnoreCase))
                    return item.Action.Label ?? string.Empty;
            }
            return string.Empty;
        }

        private static string BuildDetailsSummary(ScenarioAuthoringInspectorSection section)
        {
            string author = FindHomePropertyValue(section, "Author");
            string version = FindHomePropertyValue(section, "Version");
            if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(version))
                return "by " + author + "  -  v" + version;
            if (!string.IsNullOrEmpty(author))
                return "by " + author;
            if (!string.IsNullOrEmpty(version))
                return "v" + version;
            return string.Empty;
        }

        private static string BuildStatusSummary(ScenarioAuthoringInspectorSection section)
        {
            string saved = FindHomePropertyValue(section, "Last Saved");
            return string.IsNullOrEmpty(saved) ? string.Empty : "saved " + saved;
        }

        private static string FindHomePropertyValue(ScenarioAuthoringInspectorSection section, string label)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase))
                    return item.Value ?? string.Empty;
            }
            return string.Empty;
        }

        private void DrawHomeBaseSelectorBody(ScenarioAuthoringInspectorSection section)
        {
            DrawHomeBaseSelectorControls(section);
        }

        private void DrawHomeDetailsBody(ScenarioAuthoringInspectorSection section)
        {
            DrawMetadataFormSection(section, false);
        }

        private void DrawHomeStatusBody(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Card);
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
                DrawItem(section.Items[i], false);
            GUILayout.EndVertical();
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

        private static Rect Inset(Rect rect, float inset)
        {
            float doubleInset = inset * 2f;
            return new Rect(
                rect.x + inset,
                rect.y + inset,
                Math.Max(0f, rect.width - doubleInset),
                Math.Max(0f, rect.height - doubleInset));
        }

        private void DrawSurvivorEditor(Rect rect, ScenarioSurvivorEditorViewModel editor)
        {
            if (editor == null)
            {
                GUI.Label(rect, "Survivor editor data is unavailable.", _mutedTextStyle);
                return;
            }

            RegisterInteractiveRegion(rect);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 42f);
            Rect footerRect = new Rect(rect.x, rect.yMax - 132f, rect.width, 132f);
            Rect contentRect = new Rect(rect.x, headerRect.yMax + 8f, rect.width, Math.Max(120f, footerRect.y - headerRect.yMax - 14f));
            DrawSurvivorEditorHeader(headerRect, editor);

            float gap = 12f;
            float leftWidth = Mathf.Clamp(contentRect.width * 0.44f, 360f, 458f);
            leftWidth = Math.Min(leftWidth, Math.Max(260f, contentRect.width - 360f - gap));
            Rect leftRect = new Rect(contentRect.x, contentRect.y, leftWidth, contentRect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, contentRect.y, Math.Max(260f, contentRect.xMax - leftRect.xMax - gap), contentRect.height);
            DrawSurvivorAppearancePanel(leftRect, editor);
            DrawSurvivorStatsPanel(rightRect, editor);
            DrawSurvivorEditorFooter(footerRect, editor);
        }

        private void DrawSurvivorEditorHeader(Rect rect, ScenarioSurvivorEditorViewModel editor)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            float x = rect.x + 10f;
            float y = rect.y + 7f;
            float height = 28f;
            float nameWidth = Mathf.Clamp(rect.width * 0.34f, 180f, 320f);
            DrawButton(new Rect(x, y, nameWidth, height), editor.NameAction, false);
            x += nameWidth + 8f;

            float genderWidth = editor.GenderAction != null ? Mathf.Clamp(MeasureButtonWidth(editor.GenderAction, false, 22f), 118f, 190f) : 150f;
            DrawButton(new Rect(x, y, genderWidth, height), editor.GenderAction, false);
            x += genderWidth + 8f;

            float bodyWidth = editor.BodyAction != null ? Mathf.Clamp(MeasureButtonWidth(editor.BodyAction, false, 22f), 104f, 170f) : 130f;
            DrawButton(new Rect(x, y, bodyWidth, height), editor.BodyAction, false);
        }

        private void DrawSurvivorAppearancePanel(Rect rect, ScenarioSurvivorEditorViewModel editor)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            Rect inner = Inset(rect, 10f);
            RegisterScrollRegion("survivor.appearance", inner);
            Vector2 scroll = GetWindowScrollPosition("survivor.appearance");
            float contentHeight = MeasureSurvivorAppearanceContentHeight(editor);
            Rect viewRect = new Rect(0f, 0f, Math.Max(1f, inner.width - 18f), Math.Max(inner.height, contentHeight));
            scroll = GUI.BeginScrollView(inner, scroll, viewRect, false, true);

            GUI.Label(new Rect(0f, 0f, viewRect.width, 24f), "Appearance", _sectionTitleStyle);

            float portraitHeight = 176f;
            Rect portraitRect = new Rect((viewRect.width - 176f) * 0.5f, 30f, 176f, portraitHeight);
            DrawCastPortrait(portraitRect, editor.Portrait, false);

            float y = portraitRect.yMax + 10f;
            for (int i = 0; editor.TextureRows != null && i < editor.TextureRows.Length; i++)
            {
                DrawSurvivorTextureRow(new Rect(0f, y, viewRect.width, 30f), editor.TextureRows[i]);
                y += 34f;
            }

            y += 4f;
            GUI.Label(new Rect(0f, y, viewRect.width, 22f), "Colors", _smallTitleStyle);
            y += 26f;
            for (int i = 0; editor.ColorRows != null && i < editor.ColorRows.Length; i++)
            {
                DrawSurvivorColorRow(new Rect(0f, y, viewRect.width, 30f), editor.ColorRows[i]);
                y += 34f;
            }

            GUI.EndScrollView();
            SetWindowScrollPosition("survivor.appearance", scroll);
        }

        private static float MeasureSurvivorAppearanceContentHeight(ScenarioSurvivorEditorViewModel editor)
        {
            int textureRows = editor != null && editor.TextureRows != null ? editor.TextureRows.Length : 0;
            int colorRows = editor != null && editor.ColorRows != null ? editor.ColorRows.Length : 0;
            return 30f + 176f + 10f + (textureRows * 34f) + 4f + 22f + 26f + (colorRows * 34f) + 10f;
        }

        private void DrawSurvivorTextureRow(Rect rect, ScenarioSurvivorTextureRowViewModel row)
        {
            if (row == null)
                return;

            float stepWidth = 30f;
            float labelWidth = 82f;
            DrawButton(new Rect(rect.x, rect.y, stepWidth, 28f), row.PreviousAction, false);
            GUI.Label(new Rect(rect.x + stepWidth + 8f, rect.y + 3f, labelWidth, 22f), row.Label ?? string.Empty, _textStyle);
            GUI.Label(new Rect(rect.x + stepWidth + labelWidth + 12f, rect.y + 4f, Math.Max(30f, rect.width - (stepWidth * 2f) - labelWidth - 28f), 20f), ShortenToFit(row.Detail ?? "default", Math.Max(30f, rect.width - (stepWidth * 2f) - labelWidth - 28f), _mutedTextStyle), _mutedTextStyle);
            DrawButton(new Rect(rect.xMax - stepWidth, rect.y, stepWidth, 28f), row.NextAction, false);
        }

        private void DrawSurvivorColorRow(Rect rect, ScenarioSurvivorColorRowViewModel row)
        {
            if (row == null)
                return;

            float stepWidth = 30f;
            float labelWidth = 62f;
            float swatchWidth = 58f;
            DrawButton(new Rect(rect.x, rect.y, stepWidth, 28f), row.PreviousAction, false);
            GUI.Label(new Rect(rect.x + stepWidth + 8f, rect.y + 3f, labelWidth, 22f), row.Label ?? string.Empty, _textStyle);

            Rect swatchRect = new Rect(rect.x + stepWidth + labelWidth + 12f, rect.y + 1f, swatchWidth, 26f);
            DrawColorPreview(swatchRect, row.Color);
            RegisterTourTarget("action:" + row.OpenColorPickerActionId, swatchRect);
            if (DrawPlainButton(swatchRect, GUIContent.none, GUIStyle.none, true))
                ScenarioAuthoringBackendService.Instance.ExecuteAction(row.OpenColorPickerActionId);

            float hexX = swatchRect.xMax + 8f;
            float hexWidth = Math.Max(42f, rect.xMax - hexX - stepWidth - 8f);
            GUI.Label(new Rect(hexX, rect.y + 4f, hexWidth, 20f), ShortenToFit(row.Hex ?? string.Empty, hexWidth, _mutedTextStyle), _mutedTextStyle);
            DrawButton(new Rect(rect.xMax - stepWidth, rect.y, stepWidth, 28f), row.NextAction, false);
        }

        private void DrawSurvivorStatsPanel(Rect rect, ScenarioSurvivorEditorViewModel editor)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            Rect inner = Inset(rect, 10f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Stats", _sectionTitleStyle);
            float rowStep = 28f;
            float rowHeight = 28f;
            float y = inner.y + 28f;
            if (!string.IsNullOrEmpty(editor.SkillsLimitationText))
            {
                Rect limitationRect = new Rect(inner.x, y, inner.width, 46f);
                GUI.Box(limitationRect, GUIContent.none, _uiContext.Styles.Field);
                GUIStyle limitationStyle = new GUIStyle(_mutedTextStyle);
                limitationStyle.wordWrap = true;
                GUI.Label(new Rect(limitationRect.x + 8f, limitationRect.y + 5f, limitationRect.width - 16f, limitationRect.height - 10f), editor.SkillsLimitationText, limitationStyle);
                y += 52f;
            }
            for (int i = 0; editor.StatRows != null && i < editor.StatRows.Length; i++)
            {
                DrawSurvivorStatRow(new Rect(inner.x, y, inner.width, rowHeight), editor.StatRows[i]);
                y += rowStep;
            }

            y += 6f;
            GUI.Label(new Rect(inner.x, y, inner.width, 22f), "Traits", _sectionTitleStyle);
            y += 26f;
            for (int i = 0; editor.TraitRows != null && i < editor.TraitRows.Length; i++)
            {
                DrawSurvivorTraitRow(new Rect(inner.x, y, inner.width, rowHeight), editor.TraitRows[i]);
                y += rowStep;
            }

            y += 6f;
            GUI.Label(new Rect(inner.x, y, inner.width, 22f), "Condition", _sectionTitleStyle);
            y += 26f;
            float conditionGap = 8f;
            float conditionWidth = Math.Max(170f, (inner.width - conditionGap) * 0.5f);
            for (int i = 0; editor.ConditionRows != null && i < editor.ConditionRows.Length; i++)
            {
                int column = i % 2;
                int rowIndex = i / 2;
                Rect conditionRect = new Rect(inner.x + column * (conditionWidth + conditionGap), y + rowIndex * rowStep, conditionWidth, rowHeight);
                DrawSurvivorConditionRow(conditionRect, editor.ConditionRows[i]);
            }

            DrawSurvivorTraitPickerPopup(editor);
        }

        private void DrawSurvivorStatRow(Rect rect, ScenarioSurvivorStatRowViewModel row)
        {
            if (row == null)
                return;

            float labelWidth = Mathf.Clamp(rect.width * 0.24f, 78f, 126f);
            float buttonWidth = 30f;
            float valueWidth = 48f;
            float rangeWidth = 44f;
            GUI.Label(new Rect(rect.x, rect.y + 7f, labelWidth, 20f), row.Label ?? string.Empty, _textStyle);
            DrawButton(new Rect(rect.x + labelWidth + 6f, rect.y + 3f, buttonWidth, 28f), row.DecreaseAction, false);

            Rect barRect = new Rect(rect.x + labelWidth + buttonWidth + 14f, rect.y + 8f, Math.Max(48f, rect.width - labelWidth - (buttonWidth * 2f) - valueWidth - rangeWidth - 42f), 16f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(barRect, _uiContext.Styles.PanelInsetTexture);
            float fillWidth = Mathf.Clamp01(row.Value / (float)Math.Max(1, row.Max)) * Math.Max(0f, barRect.width - 4f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.45f, 0.61f, 0.32f, 1f);
            GUI.DrawTexture(new Rect(barRect.x + 2f, barRect.y + 2f, fillWidth, barRect.height - 4f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            Rect valueRect = new Rect(barRect.xMax + 8f, rect.y + 3f, valueWidth, 28f);
            DrawSurvivorInlineTextField(valueRect, "survivor.stat." + row.Id, row.Value.ToString(), row.TextAction);
            GUI.Label(new Rect(valueRect.xMax + 4f, rect.y + 7f, rangeWidth, 20f), row.RangeText ?? string.Empty, _mutedTextStyle);
            DrawButton(new Rect(rect.xMax - buttonWidth, rect.y + 3f, buttonWidth, 28f), row.IncreaseAction, false);
        }

        private void DrawSurvivorTraitRow(Rect rect, ScenarioSurvivorTraitRowViewModel row)
        {
            if (row == null)
                return;

            float labelWidth = Mathf.Clamp(rect.width * 0.34f, 118f, 170f);
            GUI.Label(new Rect(rect.x, rect.y + 7f, labelWidth, 20f), row.Label ?? string.Empty, _textStyle);
            float buttonWidth = 30f;
            Rect previousRect = new Rect(rect.x + labelWidth + 8f, rect.y + 3f, buttonWidth, 28f);
            Rect nextRect = new Rect(rect.xMax - buttonWidth, rect.y + 3f, buttonWidth, 28f);
            Rect pickerRect = new Rect(previousRect.xMax + 6f, rect.y + 3f, Math.Max(80f, nextRect.x - previousRect.xMax - 12f), 28f);
            DrawButton(previousRect, row.PreviousAction, false);
            bool pickerEnabled = row.PickerAction != null && row.PickerAction.Enabled;
            bool pickerPressed = pickerEnabled && IsInteractiveMouseDownAllowed(pickerRect);
            ScenarioUiAtlasSkin.DrawButton(pickerRect, false, pickerEnabled, pickerPressed, false);
            GUIContent pickerContent = new GUIContent(ShortenToFit(row.PickerAction != null ? row.PickerAction.Label ?? string.Empty : string.Empty, Math.Max(0f, pickerRect.width - 14f), pickerEnabled ? _buttonContentStyle : _disabledButtonContentStyle), row.PickerAction != null ? row.PickerAction.Hint ?? row.PickerAction.Detail ?? string.Empty : string.Empty);
            if (DrawPlainButton(pickerRect, pickerContent, pickerEnabled ? _buttonContentStyle : _disabledButtonContentStyle, pickerEnabled))
            {
                _survivorTraitPickerKey = string.Equals(_survivorTraitPickerKey, row.PickerKey, StringComparison.OrdinalIgnoreCase) ? null : row.PickerKey;
                _survivorTraitPickerSearchText = string.Empty;
                _survivorTraitPickerButtonRect = pickerRect;
                Event.current.Use();
            }
            else if (string.Equals(_survivorTraitPickerKey, row.PickerKey, StringComparison.OrdinalIgnoreCase))
            {
                _survivorTraitPickerButtonRect = pickerRect;
            }
            DrawButton(nextRect, row.NextAction, false);
        }

        private void DrawSurvivorConditionRow(Rect rect, ScenarioSurvivorConditionRowViewModel row)
        {
            if (row == null)
                return;

            float labelWidth = Mathf.Clamp(rect.width * 0.34f, 58f, 84f);
            float buttonWidth = 24f;
            float valueWidth = 38f;
            string rangeText = row.RangeText ?? string.Empty;
            GUIStyle rangeStyle = new GUIStyle(_mutedTextStyle);
            rangeStyle.wordWrap = false;
            rangeStyle.clipping = TextClipping.Clip;
            float rangeWidth = Math.Max(46f, rangeStyle.CalcSize(new GUIContent(rangeText)).x + 2f);
            bool showRange = rect.width >= 48f + 6f + buttonWidth + 6f + valueWidth + 4f + rangeWidth + 8f + buttonWidth;
            if (!showRange)
                rangeWidth = 0f;
            float fixedWidthAfterLabel = showRange
                ? 6f + buttonWidth + 6f + valueWidth + 4f + rangeWidth + 8f + buttonWidth
                : 6f + buttonWidth + 6f + valueWidth + 8f + buttonWidth;
            labelWidth = Math.Min(labelWidth, Math.Max(48f, rect.width - fixedWidthAfterLabel));
            GUI.Label(new Rect(rect.x, rect.y + 6f, labelWidth, 20f), row.Label ?? string.Empty, _textStyle);
            Rect decreaseRect = new Rect(rect.x + labelWidth + 6f, rect.y + 2f, buttonWidth, 24f);
            DrawButton(decreaseRect, row.DecreaseAction, false);
            Rect valueRect = new Rect(decreaseRect.xMax + 6f, rect.y + 2f, valueWidth, 24f);
            DrawSurvivorInlineTextField(valueRect, "survivor.condition." + row.Id, row.Value.ToString(), row.TextAction);
            Rect rangeRect = showRange
                ? new Rect(valueRect.xMax + 4f, rect.y + 6f, rangeWidth, 20f)
                : new Rect(valueRect.xMax, rect.y + 6f, 0f, 20f);
            if (showRange)
                GUI.Label(rangeRect, rangeText, rangeStyle);
            Rect increaseRect = new Rect(rect.xMax - buttonWidth, rect.y + 2f, buttonWidth, 24f);
            float helpX = showRange ? rangeRect.xMax + 8f : valueRect.xMax + 8f;
            float helpWidth = Math.Max(0f, increaseRect.x - helpX - 8f);
            if (helpWidth >= 90f)
                GUI.Label(new Rect(helpX, rect.y + 6f, helpWidth, 20f), ShortenToFit(row.HelpText ?? string.Empty, helpWidth, _mutedTextStyle), _mutedTextStyle);
            DrawButton(increaseRect, row.IncreaseAction, false);
        }

        private void DrawSurvivorInlineTextField(Rect rect, string controlName, string value, ScenarioAuthoringInspectorAction action)
        {
            ScenarioAuthoringInspectorItem item = new ScenarioAuthoringInspectorItem
            {
                Action = action
            };
            DrawInlineEditableField(rect, item, controlName, value ?? string.Empty, _uiContext.Styles.Field);
        }

        private void DrawSurvivorTraitPickerPopup(ScenarioSurvivorEditorViewModel editor)
        {
            if (string.IsNullOrEmpty(_survivorTraitPickerKey) || editor == null)
                return;

            ScenarioSurvivorTraitRowViewModel row = FindSurvivorTraitRow(editor, _survivorTraitPickerKey);
            if (row == null || row.Options == null)
            {
                _survivorTraitPickerKey = null;
                return;
            }

            Rect popupRect = new Rect(_survivorTraitPickerButtonRect.x, _survivorTraitPickerButtonRect.yMax + 4f, Math.Max(300f, _survivorTraitPickerButtonRect.width + 120f), 246f);
            popupRect.x = Mathf.Min(popupRect.x, Screen.width - popupRect.width - 12f);
            popupRect.y = Mathf.Min(popupRect.y, Screen.height - popupRect.height - 12f);
            RegisterInteractiveRegion(popupRect);
            GUI.Box(popupRect, GUIContent.none, _uiContext.Styles.Section);

            Rect inner = Inset(popupRect, 8f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 20f), row.Label ?? "Trait", _smallTitleStyle);
            Rect searchRect = new Rect(inner.x, inner.y + 24f, inner.width, 26f);
            bool searchTopmost = IsInteractiveVisualTopmost(searchRect);
            if (searchTopmost)
            {
                GUI.SetNextControlName("survivor_trait_picker_search");
                _survivorTraitPickerSearchText = GUI.TextField(searchRect, _survivorTraitPickerSearchText ?? string.Empty, _uiContext.Styles.SearchField);
            }
            else
            {
                GUI.Box(searchRect, _survivorTraitPickerSearchText ?? string.Empty, _uiContext.Styles.SearchField);
            }
            DrawSearchPlaceholder(searchRect, _survivorTraitPickerSearchText, "Filter traits");
            if (searchTopmost && (string.Equals(GUI.GetNameOfFocusedControl(), "survivor_trait_picker_search", StringComparison.Ordinal) || (Event.current != null && searchRect.Contains(Event.current.mousePosition))))
                DrawFieldFocusBorder(searchRect);

            float y = searchRect.yMax + 8f;
            int drawn = 0;
            for (int i = 0; i < row.Options.Length && drawn < 6; i++)
            {
                ScenarioSurvivorTraitOptionViewModel option = row.Options[i];
                if (!TraitOptionMatchesSearch(option, _survivorTraitPickerSearchText))
                    continue;

                Rect optionRect = new Rect(inner.x, y, inner.width, 26f);
                DrawButton(optionRect, option.SelectAction, false);
                Rect descriptionRect = new Rect(inner.x + 8f, optionRect.yMax - 2f, inner.width - 16f, 18f);
                GUI.Label(descriptionRect, ShortenToFit(option.Description ?? string.Empty, descriptionRect.width, _mutedTextStyle), _mutedTextStyle);
                y += 46f;
                drawn++;
            }

            if (drawn == 0)
                GUI.Label(new Rect(inner.x, y, inner.width, 22f), "No matching traits.", _mutedTextStyle);

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0 && !popupRect.Contains(evt.mousePosition) && !_survivorTraitPickerButtonRect.Contains(evt.mousePosition))
                _survivorTraitPickerKey = null;
        }

        private static ScenarioSurvivorTraitRowViewModel FindSurvivorTraitRow(ScenarioSurvivorEditorViewModel editor, string key)
        {
            for (int i = 0; editor != null && editor.TraitRows != null && i < editor.TraitRows.Length; i++)
            {
                ScenarioSurvivorTraitRowViewModel row = editor.TraitRows[i];
                if (row != null && string.Equals(row.PickerKey, key, StringComparison.OrdinalIgnoreCase))
                    return row;
            }

            return null;
        }

        private static bool TraitOptionMatchesSearch(ScenarioSurvivorTraitOptionViewModel option, string search)
        {
            if (option == null)
                return false;

            string trimmed = (search ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return true;

            string haystack = (option.Label ?? string.Empty) + " " + (option.Description ?? string.Empty) + " " + (option.Id ?? string.Empty);
            return haystack.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawModFieldSection(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.BeginVertical(_uiContext.Styles.Section);
            if (!string.IsNullOrEmpty(section.Title))
                GUILayout.Label(section.Title, _sectionTitleStyle);

            for (int i = 0; section.ModFieldRows != null && i < section.ModFieldRows.Length; i++)
                DrawModFieldRow(section.ModFieldRows[i]);

            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                DrawItem(section.Items[i]);

            GUILayout.EndVertical();
        }

        private void DrawModFieldRow(ScenarioSurvivorModFieldRowViewModel row)
        {
            if (row == null)
                return;

            if (row.Kind == ScenarioSurvivorModFieldControlKind.Text)
            {
                DrawEditableProperty(new ScenarioAuthoringInspectorItem
                {
                    Kind = ScenarioAuthoringInspectorItemKind.Property,
                    Label = row.Label,
                    Value = row.ValueText,
                    Editable = true,
                    HoverHint = row.HelpText,
                    Action = row.TextAction
                }, false);
                return;
            }

            float height = row.Kind == ScenarioSurvivorModFieldControlKind.Notice ? 46f : 34f;
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            if (row.Kind == ScenarioSurvivorModFieldControlKind.Notice)
            {
                GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);
                Rect labelRect = new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 18f);
                Rect helpRect = new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, 18f);
                GUI.Label(labelRect, new GUIContent(row.Label ?? string.Empty, row.HelpText ?? string.Empty), row.Emphasized ? _textStyle : _mutedTextStyle);
                GUI.Label(helpRect, ShortenToFit(row.HelpText ?? string.Empty, helpRect.width, _mutedTextStyle), _mutedTextStyle);
                return;
            }

            float labelWidth = Mathf.Clamp(rect.width * 0.34f, 120f, 210f);
            GUI.Label(new Rect(rect.x, rect.y + 7f, labelWidth, 20f), new GUIContent(row.Label ?? string.Empty, row.HelpText ?? string.Empty), _textStyle);

            if (row.Kind == ScenarioSurvivorModFieldControlKind.Toggle)
            {
                float width = Mathf.Clamp(MeasureButtonWidth(row.ToggleAction, false, 20f), 84f, 126f);
                DrawButton(new Rect(rect.xMax - width, rect.y + 3f, width, 28f), row.ToggleAction, false);
                return;
            }

            if (row.Kind == ScenarioSurvivorModFieldControlKind.Stepper)
            {
                float buttonWidth = 30f;
                float valueWidth = Mathf.Clamp(rect.width - labelWidth - (buttonWidth * 2f) - 24f, 70f, 160f);
                DrawButton(new Rect(rect.x + labelWidth + 8f, rect.y + 3f, buttonWidth, 28f), row.DecreaseAction, false);
                GUI.Label(new Rect(rect.x + labelWidth + buttonWidth + 14f, rect.y + 4f, valueWidth, 26f), row.ValueText ?? string.Empty, _uiContext.Styles.Field);
                DrawButton(new Rect(rect.x + labelWidth + buttonWidth + valueWidth + 20f, rect.y + 3f, buttonWidth, 28f), row.IncreaseAction, false);
                return;
            }

            if (row.Kind == ScenarioSurvivorModFieldControlKind.Enum)
            {
                Rect buttonRect = new Rect(rect.x + labelWidth + 8f, rect.y + 3f, Math.Max(120f, rect.width - labelWidth - 8f), 28f);
                DrawButton(buttonRect, row.CycleAction, false);
                return;
            }

            if (row.Kind == ScenarioSurvivorModFieldControlKind.Color && row.ColorRow != null)
            {
                Rect swatchRect = new Rect(rect.x + labelWidth + 8f, rect.y + 4f, 62f, 26f);
                DrawColorPreview(swatchRect, row.ColorRow.Color);
                RegisterTourTarget("action:" + row.ColorRow.OpenColorPickerActionId, swatchRect);
                if (DrawPlainButton(swatchRect, GUIContent.none, GUIStyle.none, true))
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(row.ColorRow.OpenColorPickerActionId);
                Rect valueRect = new Rect(swatchRect.xMax + 8f, rect.y + 7f, Math.Max(40f, rect.xMax - swatchRect.xMax - 8f), 20f);
                GUI.Label(valueRect, ShortenToFit(row.ValueText ?? string.Empty, valueRect.width, _mutedTextStyle), _mutedTextStyle);
            }
        }

        private void DrawSurvivorEditorFooter(Rect rect, ScenarioSurvivorEditorViewModel editor)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            Rect inner = Inset(rect, 8f);
            GUIStyle disclosureStyle = new GUIStyle(_mutedTextStyle);
            disclosureStyle.wordWrap = true;
            float disclosureY = inner.y;
            for (int i = 0; editor.UtilityDisclosureLines != null && i < editor.UtilityDisclosureLines.Length; i++)
            {
                string line = editor.UtilityDisclosureLines[i];
                if (string.IsNullOrEmpty(line))
                    continue;
                GUI.Label(new Rect(inner.x, disclosureY, inner.width, 34f), line, disclosureStyle);
                disclosureY += 34f;
            }

            float closeWidth = 0f;
            for (int i = 0; editor.CloseActions != null && i < editor.CloseActions.Length; i++)
                closeWidth += Math.Max(76f, MeasureButtonWidth(editor.CloseActions[i], false, 20f)) + 6f;
            closeWidth = Math.Max(0f, closeWidth - 6f);

            Rect utilityRect = new Rect(inner.x, inner.y + 72f, Math.Max(120f, inner.width - closeWidth - 18f), Math.Max(28f, inner.height - 72f));
            DrawWrappedActionButtons(utilityRect, editor.UtilityActions, 28f, 6f, false);

            float x = inner.xMax - closeWidth;
            for (int i = 0; editor.CloseActions != null && i < editor.CloseActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = editor.CloseActions[i];
                float width = Math.Max(76f, MeasureButtonWidth(action, false, 20f));
                DrawButton(new Rect(x, inner.y + inner.height - 30f, width, 28f), action, false);
                x += width + 6f;
            }
        }

        private void DrawWrappedActionButtons(Rect rect, ScenarioAuthoringInspectorAction[] actions, float height, float gap, bool tabs)
        {
            float x = rect.x;
            float y = rect.y;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                float width = Math.Max(90f, MeasureButtonWidth(action, tabs, 20f));
                width = Math.Min(width, rect.width);
                if (x > rect.x && x + width > rect.xMax)
                {
                    x = rect.x;
                    y += height + gap;
                }
                if (y + height > rect.yMax)
                    break;

                DrawButton(new Rect(x, y, width, height), action, tabs);
                x += width + gap;
            }
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
            if (pathItem != null || copyPath != null)
            {
                GUILayout.Space(5f);
                bool showPath = pathItem != null && IsHomeAdvancedDetailsEnabled();
                DrawHomeDraftPath(showPath ? pathItem : null, copyPath);
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
            bool fieldTopmost = IsInteractiveVisualTopmost(rect);
            string next;
            if (fieldTopmost)
            {
                GUI.SetNextControlName(controlName);
                next = GUI.TextField(rect, draft, style);
            }
            else
            {
                GUI.Box(rect, draft, style);
                next = draft;
            }
            _editableFieldDrafts[controlName] = next;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _editableFieldFocused = _editableFieldFocused || focused;
            if (focused || (fieldTopmost && Event.current != null && rect.Contains(Event.current.mousePosition)))
                DrawFieldFocusBorder(rect);
            TryCommitEditableField(item, controlName, value, next, previouslyFocused, focused);
            TrackEditableFieldFocus(controlName, focused);
        }

        private void DrawInlineEditableField(Rect rect, ScenarioAuthoringInspectorItem item, string controlName, string value, GUIStyle style)
        {
            if (string.IsNullOrEmpty(controlName))
                controlName = "editable.inline";

            string focusedName = GUI.GetNameOfFocusedControl();
            bool wasFocused = string.Equals(focusedName, controlName, StringComparison.Ordinal);
            bool previouslyFocused = _editableFieldsFocusedLastFrame.Contains(controlName);
            string draft;
            if (!_editableFieldDrafts.TryGetValue(controlName, out draft) || (!wasFocused && !previouslyFocused))
                draft = value ?? string.Empty;

            Event evt = Event.current;
            bool fieldTopmost = IsInteractiveVisualTopmost(rect);
            bool hovered = evt != null && rect.Contains(evt.mousePosition) && fieldTopmost;
            if (hovered && evt.type == EventType.MouseDown && evt.button == 0)
                GUI.FocusControl(controlName);

            string next;
            if (fieldTopmost)
            {
                GUI.SetNextControlName(controlName);
                next = GUI.TextField(rect, draft, style);
            }
            else
            {
                GUI.Box(rect, draft, style);
                next = draft;
            }

            _editableFieldDrafts[controlName] = next;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _editableFieldFocused = _editableFieldFocused || focused;
            if (focused || hovered)
                DrawFieldFocusBorder(rect);
            TryCommitEditableField(item, controlName, value ?? string.Empty, next, previouslyFocused, focused);
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

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 26f), 84f, Math.Min(300f, rowLimit));
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
            if (RegisterRichHoverHelpSource(rect, action))
                tooltip = string.Empty;
            string fitted;
            string fitTooltip;
            ScenarioUiMeasuredLabel.PreserveLabelWithOverflowTooltip(action.Label ?? string.Empty, Math.Max(0f, rect.width - 14f), style, out fitted, out fitTooltip);
            if (!string.IsNullOrEmpty(fitTooltip))
                tooltip = string.IsNullOrEmpty(tooltip) ? fitTooltip : tooltip + "\n" + fitTooltip;
            GUIContent content = new GUIContent(fitted, tooltip);
            RegisterInteractiveRegion(rect);
            if (!string.IsNullOrEmpty(action.Id))
                RegisterTourTarget("action:" + action.Id, rect);
            if (action.Enabled)
            {
                if (DrawPlainButton(rect, content, style, true))
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

        private bool IsHomeAdvancedDetailsEnabled()
        {
            return _snapshot != null
                && _snapshot.State != null
                && _snapshot.State.Settings != null
                && _snapshot.State.Settings.GetBool("debug.show_advanced_details", false);
        }

        private void DrawHomeDraftPath(ScenarioAuthoringInspectorItem pathItem, ScenarioAuthoringInspectorAction copyPath)
        {
            // Default header keeps the raw filesystem path out of sight; the Copy
            // Path action stays available, and the full path is revealed only when
            // the caller passes a non-null pathItem (Advanced details enabled).
            if (pathItem == null)
            {
                if (copyPath == null)
                    return;
                float compactWidth = Mathf.Clamp(MeasureButtonWidth(copyPath, false, 22f), 88f, 160f);
                Rect compactRow = GUILayoutUtility.GetRect(compactWidth, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
                DrawButton(new Rect(compactRow.x, compactRow.y, compactWidth, 24f), copyPath, false);
                return;
            }

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

        // Controls-only base selector body (no leading label); the collapsible
        // group header supplies the title on Home.
        private void DrawHomeBaseSelectorControls(ScenarioAuthoringInspectorSection section)
        {
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

        private void DrawChecklistItem(Rect rect, ScenarioAuthoringInspectorAction action, bool recommended)
        {
            if (action == null || _uiContext == null || _uiContext.Styles == null)
                return;

            // Every row renders as a single layer: a status chip on the left, then
            // the item label. Done rows are inert; todo rows are click targets for
            // the whole row. This avoids the earlier state split where todo rows
            // were centered full-width buttons that collided with the chip layout.
            string rawLabel = action.Label ?? string.Empty;
            bool complete = !action.Enabled && rawLabel.StartsWith("Done:", StringComparison.OrdinalIgnoreCase);
            string label = StripChecklistLabelPrefix(rawLabel);

            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Field);

            Rect chipRect = new Rect(rect.x + 8f, rect.y + 4f, 34f, rect.height - 8f);
            ScenarioUiWidgets.DrawPill(
                chipRect,
                complete ? "OK" : "GO",
                _uiContext.Styles,
                complete ? ScenarioUiPillEmphasis.Success : ScenarioUiPillEmphasis.Active);

            float labelX = chipRect.xMax + 10f;
            Rect labelRect = new Rect(labelX, rect.y + 3f, Math.Max(20f, rect.xMax - labelX - 10f), rect.height - 6f);
            GUI.Label(labelRect, label, complete ? _uiContext.Styles.PaperMutedText : _uiContext.Styles.PaperBodyText);

            if (!complete && action.Enabled)
            {
                RegisterInteractiveRegion(rect);
                if (!string.IsNullOrEmpty(action.Id))
                    RegisterTourTarget("action:" + action.Id, rect);
                if (DrawPlainButton(rect, GUIContent.none, _buttonContentStyle, true))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                    if (Event.current != null)
                        Event.current.Use();
                }
                if (recommended)
                {
                    float pulse = 0.45f + (Mathf.Sin(Time.realtimeSinceStartup * 2.1f) * 0.20f);
                    Color oldColor = GUI.color;
                    GUI.color = new Color(0.94f, 0.80f, 0.52f, pulse);
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
                    GUI.color = oldColor;
                }
            }
        }

        private static string StripChecklistLabelPrefix(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            int colon = label.IndexOf(':');
            return colon >= 0 && colon + 1 < label.Length ? label.Substring(colon + 1).Trim() : label.Trim();
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

        // Question cards read as a light table of contents. They are grouped
        // into three quietly labelled bands so the reading order (create ->
        // refine -> share) is legible without a wall of undifferentiated tiles.
        private static readonly string[] HomeCreateBandIds = { "home_world", "home_people", "home_inventory" };
        private static readonly string[] HomeRefineBandIds = { "home_events", "home_art" };
        private static readonly string[] HomeShareBandIds = { "home_test", "home_publish" };

        private void DrawHomeQuestionBands(ScenarioAuthoringShellWindowViewModel window)
        {
            bool drewCreate = DrawHomeQuestionBand(window, "Create", HomeCreateBandIds, false);
            bool drewRefine = DrawHomeQuestionBand(window, "Refine", HomeRefineBandIds, drewCreate);
            DrawHomeQuestionBand(window, "Share", HomeShareBandIds, drewCreate || drewRefine);
        }

        private bool DrawHomeQuestionBand(
            ScenarioAuthoringShellWindowViewModel window,
            string bandLabel,
            string[] ids,
            bool precededByBand)
        {
            List<ScenarioAuthoringInspectorSection> questions = new List<ScenarioAuthoringInspectorSection>();
            for (int i = 0; ids != null && i < ids.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = FindSection(window, ids[i]);
                if (IsHomeQuestionSection(section))
                    questions.Add(section);
            }

            if (questions.Count == 0)
                return false;

            if (precededByBand)
                GUILayout.Space(12f);
            if (!string.IsNullOrEmpty(bandLabel))
            {
                GUILayout.Label(bandLabel, _mutedTextStyle);
                GUILayout.Space(4f);
            }
            DrawHomeQuestionCards(questions);
            return true;
        }

        private void DrawHomeQuestionCards(List<ScenarioAuthoringInspectorSection> questions)
        {
            if (questions == null || questions.Count == 0)
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
                    Rect rect = GUILayoutUtility.GetRect(rowCardWidth, 124f, GUILayout.Width(rowCardWidth), GUILayout.Height(124f));
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

            if (section.Layout == ScenarioAuthoringInspectorSectionLayout.SurvivorEditor)
            {
                Rect rect = GUILayoutUtility.GetRect(0f, 520f, GUILayout.ExpandWidth(true), GUILayout.Height(520f));
                DrawSurvivorEditor(rect, section.SurvivorEditor);
                return;
            }

            if (section.Layout == ScenarioAuthoringInspectorSectionLayout.ModFieldList)
            {
                DrawModFieldSection(section);
                return;
            }

            if (TryDrawNewWindowsSection(section, compactInspector))
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
            else if (IsTimelineTrackSection(section))
            {
                DrawTimelineTrackSection(section);
            }
            else if (IsPacingSection(section))
            {
                DrawPacingSection(section);
            }
            else if (IsStoryMapSection(section))
            {
                DrawStoryMapSection(section);
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
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.InventorySlotGrid)
            {
                DrawInventorySlotGridSection(section, compactInspector);
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
                && !string.Equals(section.Id, "home_next", StringComparison.OrdinalIgnoreCase)
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
            Rect rect = GUILayoutUtility.GetRect(120f, 124f, GUILayout.ExpandWidth(true), GUILayout.Height(124f));
            DrawHomeQuestionCard(rect, section);
        }

        private void DrawHomeQuestionCard(Rect rect, ScenarioAuthoringInspectorSection section)
        {
            ScenarioAuthoringInspectorAction action = null;
            ScenarioAuthoringInspectorAction fixAction = null;
            string detail = string.Empty;
            string badge = string.Empty;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (item.Action != null && action == null)
                    action = item.Action;
                else if (item.Action != null && fixAction == null && !IsHomeQuestionUtilityAction(item.Action))
                    fixAction = item.Action;
                else if (string.IsNullOrEmpty(detail))
                    detail = item.Value ?? item.Label ?? string.Empty;
                else if (string.IsNullOrEmpty(badge))
                    badge = item.Value ?? item.Label ?? string.Empty;
            }

            if (action == null)
                return;

            RegisterRichHoverSiblingAvoidRect(rect);
            string tooltip = action.Enabled
                ? (action.Hint ?? action.Detail ?? string.Empty)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (action.Hint ?? action.Detail ?? string.Empty));
            if (RegisterRichHoverHelpSource(rect, action))
                tooltip = string.Empty;
            RegisterInteractiveRegion(rect);
            if (section != null && !string.IsNullOrEmpty(section.Id))
                RegisterTourTarget("section:" + section.Id, rect);
            if (!string.IsNullOrEmpty(action.Id))
                RegisterTourTarget("action:" + action.Id, rect);
            bool clicked = DrawPlainButton(rect, new GUIContent(string.Empty, tooltip), _uiContext.Styles.Card, action.Enabled);
            if (_uiContext != null && _uiContext.Styles != null)
            {
                Color domainTint;
                int domainSeed;
                ResolveHomeDomainFace(section != null ? section.Id : null, out domainTint, out domainSeed);
                ScenarioUiParchment.PaintFace(
                    rect,
                    _uiContext.Styles.Textures,
                    domainTint,
                    domainSeed,
                    0.05f,
                    1f,
                    _uiContext.Styles.BevelLightTexture,
                    _uiContext.Styles.BevelDarkTexture);
            }
            bool hovered = action.Enabled && IsInteractiveHoverAllowed(rect);
            bool pressed = action.Enabled && IsInteractiveMouseDownAllowed(rect);
            DrawButtonAnimationOverlay(rect, action.Id, action.Enabled, hovered, pressed);
            if (clicked)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
            if (action.Emphasized && _uiContext != null && _uiContext.Styles != null)
                ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderStrongTexture);

            GUIStyle detailStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            detailStyle.wordWrap = true;
            detailStyle.clipping = TextClipping.Clip;
            float fixWidth = fixAction != null ? Mathf.Clamp(MeasureButtonWidth(fixAction, false, 20f), 104f, Math.Min(168f, rect.width * 0.34f)) : 0f;
            float sideWidth = fixWidth > 0f ? fixWidth : (!string.IsNullOrEmpty(badge) ? 116f : 0f);
            if (!string.IsNullOrEmpty(badge))
            {
                Vector2 measuredBadge = _mutedTextStyle.CalcSize(new GUIContent(badge));
                sideWidth = Mathf.Max(sideWidth, measuredBadge.x + 24f);
            }

            if (sideWidth > 0f)
                sideWidth = Mathf.Clamp(sideWidth, 116f, Math.Min(190f, rect.width * 0.38f));
            Rect glyphRect = new Rect(rect.x + 12f, rect.y + 14f, 42f, 42f);
            bool drewGlyph = DrawHomeQuestionGlyph(glyphRect, section, action);
            float textX = drewGlyph ? glyphRect.xMax + 10f : rect.x + 14f;
            float textReservedWidth = drewGlyph ? glyphRect.width + 44f : 28f;
            Rect textRect = new Rect(textX, rect.y + 9f, Math.Max(24f, rect.width - sideWidth - textReservedWidth), rect.height - 18f);
            GUIStyle titleStyle = new GUIStyle(_uiContext.Styles.PaperTitleText);
            titleStyle.wordWrap = true;
            titleStyle.clipping = TextClipping.Clip;
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 38f), section.Title ?? string.Empty, titleStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 42f, textRect.width, rect.height - 58f), detail ?? string.Empty, detailStyle);
            if (!string.IsNullOrEmpty(badge))
            {
                Rect badgeRect = new Rect(rect.xMax - sideWidth - 14f, rect.y + 14f, sideWidth, 22f);
                ScenarioUiWidgets.DrawPill(badgeRect, badge, _uiContext.Styles, ResolveHomeBadgeEmphasis(badge));
            }
            if (fixAction != null)
            {
                Rect actionRect = new Rect(rect.xMax - sideWidth - 14f, rect.yMax - 38f, sideWidth, 28f);
                DrawButton(actionRect, fixAction, false);
            }
        }

        private static bool IsHomeQuestionUtilityAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && !string.IsNullOrEmpty(action.Id)
                && (action.Id.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal)
                    || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionTourStartPrefix, StringComparison.Ordinal));
        }

        private bool DrawHomeQuestionGlyph(Rect rect, ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorAction action)
        {
            if (_uiContext == null || _uiContext.Styles == null)
                return false;

            Rect iconRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            string role = ResolveHomeIconRole(section);
            GUI.Box(rect, GUIContent.none, action != null && action.Emphasized ? _uiContext.Styles.ButtonActive : _uiContext.Styles.Field);
            // Give each domain's glyph plate its own grain instance and faint
            // tint so the tiles read as distinct stamped frames, not clones.
            Color glyphTint;
            int glyphSeed;
            ResolveHomeDomainFace(section != null ? section.Id : null, out glyphTint, out glyphSeed);
            ScenarioUiParchment.PaintFace(
                rect,
                _uiContext.Styles.Textures,
                new Color(glyphTint.r, glyphTint.g, glyphTint.b, glyphTint.a * 0.6f),
                glyphSeed + 101,
                0.05f,
                0.5f,
                null,
                null);
            if (!string.IsNullOrEmpty(role) && ScenarioUiAtlasSkin.HasIcon(role) && ScenarioUiAtlasSkin.DrawIcon(iconRect, role))
                return true;

            string iconText = action != null ? action.IconText ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(iconText))
                return false;

            GUIStyle iconStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            iconStyle.alignment = TextAnchor.MiddleCenter;
            iconStyle.fontStyle = FontStyle.Bold;
            iconStyle.wordWrap = false;
            iconStyle.clipping = TextClipping.Clip;
            GUI.Label(iconRect, ShortenToFit(iconText, iconRect.width, iconStyle), iconStyle);
            return true;
        }

        private static string ResolveHomeIconRole(ScenarioAuthoringInspectorSection section)
        {
            if (section == null || string.IsNullOrEmpty(section.Id))
                return null;
            return section.Id;
        }

        /// <summary>
        /// Maps a home question section to a subtle domain identity: a faint
        /// colour wash and a distinct grain seed. Variations stay quiet (wash
        /// alpha ~0.05) so the parchment set reads as one cohesive book, with
        /// each page family only softly tinted (Cast warm, Supplies olive,
        /// Timeline amber, Story burgundy, World sepia, Home/Ready neutral).
        /// </summary>
        private static void ResolveHomeDomainFace(string sectionId, out Color tint, out int grainSeed)
        {
            string id = sectionId ?? string.Empty;
            if (string.Equals(id, "home_people", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.60f, 0.32f, 0.20f, 0.055f); // Cast: warm terracotta
                grainSeed = 34;
                return;
            }
            if (string.Equals(id, "home_inventory", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.42f, 0.44f, 0.22f, 0.055f); // Supplies: olive
                grainSeed = 47;
                return;
            }
            if (string.Equals(id, "home_events", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.72f, 0.50f, 0.14f, 0.055f); // Timeline: amber
                grainSeed = 58;
                return;
            }
            if (string.Equals(id, "home_art", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.52f, 0.18f, 0.22f, 0.05f); // Story/Art: burgundy
                grainSeed = 63;
                return;
            }
            if (string.Equals(id, "home_world", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.46f, 0.34f, 0.20f, 0.055f); // World/Map: sepia
                grainSeed = 21;
                return;
            }

            // Home, Ready-to-test, Publish and anything else stay neutral.
            tint = new Color(0f, 0f, 0f, 0f);
            grainSeed = 12;
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
                && rowRect.Contains(evt.mousePosition)
                && IsInteractiveVisualTopmost(rowRect))
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
            bool fieldTopmost = IsInteractiveVisualTopmost(fieldRect);
            bool hovered = evt != null && fieldRect.Contains(evt.mousePosition) && fieldTopmost;
            if (hovered && evt.type == EventType.MouseDown && evt.button == 0)
                GUI.FocusControl(controlName);

            string next;
            if (fieldTopmost)
            {
                GUI.SetNextControlName(controlName);
                next = GUI.TextField(fieldRect, draft, _uiContext.Styles.Field);
            }
            else
            {
                GUI.Box(fieldRect, draft, _uiContext.Styles.Field);
                next = draft;
            }
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
            DrawCandidateCard(rect, action, false);
        }

        private void DrawCandidateCard(Rect rect, ScenarioAuthoringInspectorAction action, bool armPlacementOnAssetBrowserClick)
        {
            DrawCandidateCard(rect, action, armPlacementOnAssetBrowserClick, false);
        }

        private void DrawCandidateCard(Rect rect, ScenarioAuthoringInspectorAction action, bool armPlacementOnAssetBrowserClick, bool showFavoriteToggle)
        {
            if (action == null)
                return;

            if (showFavoriteToggle && HandleAssetFavoriteStarInput(rect, action))
                return;

            GUIStyle style = !action.Enabled ? _uiContext.Styles.ButtonDisabled : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
            bool hovered = action.Enabled && IsInteractiveHoverAllowed(rect);
            bool pressed = action.Enabled && IsInteractiveMouseDownAllowed(rect);
            if (DrawPlainButton(rect, GUIContent.none, style, action.Enabled))
            {
                ExecuteCandidateCardAction(action, armPlacementOnAssetBrowserClick);
                if (Event.current != null)
                    Event.current.Use();
            }
            RegisterRichHoverHelpSource(rect, action);

            DrawButtonAnimationOverlay(rect, action.Id, action.Enabled, hovered, pressed);

            Rect textRect;
            if (action.PreviewSprite != null && rect.width >= 150f)
            {
                float previewSize = Mathf.Clamp(rect.height - 12f, 44f, 70f);
                Rect previewRect = new Rect(rect.x + 6f, rect.y + 6f, previewSize, previewSize);
                DrawSpritePreview(previewRect, action.PreviewSprite, action.Emphasized, action.HasPreviewTint ? action.PreviewTint : Color.white);
                textRect = new Rect(previewRect.xMax + 10f, rect.y + 8f, rect.width - previewRect.width - 22f, rect.height - 16f);
            }
            else
            {
                textRect = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);
            }

            GUIStyle labelStyle = new GUIStyle(_textStyle);
            labelStyle.wordWrap = true;
            labelStyle.clipping = TextClipping.Clip;
            float labelHeight = Math.Min(40f, Math.Max(20f, labelStyle.CalcHeight(new GUIContent(action.Label ?? string.Empty), textRect.width)));
            string fittedLabel = action.Label ?? string.Empty;
            string labelTooltip = RegisterRichHoverHelpSource(rect, action) ? string.Empty : BuildFullLabelTooltip(action);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, labelHeight), new GUIContent(fittedLabel, labelTooltip), labelStyle);
            string detail = !string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + labelHeight + 2f, textRect.width, Math.Max(16f, rect.height - labelHeight - 30f)), detail, _mutedTextStyle);

            if (!string.IsNullOrEmpty(action.Badge))
            {
                Vector2 badgeSize = _mutedTextStyle.CalcSize(new GUIContent(action.Badge));
                Rect badgeRect = new Rect(textRect.x, rect.yMax - 22f, Mathf.Max(52f, badgeSize.x + 16f), 18f);
                ScenarioUiWidgets.DrawPill(badgeRect, action.Badge, _uiContext.Styles, action.Emphasized ? ScenarioUiPillEmphasis.Active : ScenarioUiPillEmphasis.Default);
            }
            if (showFavoriteToggle)
                DrawAssetFavoriteStar(rect, action);
        }

        private bool HandleAssetFavoriteStarInput(Rect cardRect, ScenarioAuthoringInspectorAction action)
        {
            string sourceActionId = ScenarioAssetBrowserUx.DecodeSourceActionId(action != null ? action.Id : null);
            Event current = Event.current;
            Rect starRect = BuildAssetFavoriteStarRect(cardRect);
            if (string.IsNullOrEmpty(sourceActionId)
                || current == null
                || current.button != 0
                || current.type != EventType.MouseDown
                || !starRect.Contains(current.mousePosition)
                || !IsInteractiveVisualTopmost(starRect))
                return false;

            ScenarioAuthoringBackendService.Instance.ExecuteAction(
                ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetFavoriteTogglePrefix, sourceActionId));
            current.Use();
            return true;
        }

        private void DrawAssetFavoriteStar(Rect cardRect, ScenarioAuthoringInspectorAction action)
        {
            string sourceActionId = ScenarioAssetBrowserUx.DecodeSourceActionId(action != null ? action.Id : null);
            if (string.IsNullOrEmpty(sourceActionId))
                return;

            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            bool favorite = ScenarioAssetBrowserUx.IsFavorite(state, sourceActionId);
            Rect starRect = BuildAssetFavoriteStarRect(cardRect);
            GUIStyle style = new GUIStyle(_buttonContentStyle);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 19;
            Color previous = GUI.color;
            Color starColor = favorite
                ? _uiContext.Styles.Theme.Palette.TextTitle
                : new Color(0.77f, 0.72f, 0.63f, 0.72f);
            GUI.color = new Color(previous.r * starColor.r, previous.g * starColor.g, previous.b * starColor.b, previous.a * starColor.a);
            GUI.Label(starRect, new GUIContent(favorite ? "\u2605" : "\u2606", favorite ? "Remove from Favorites" : "Add to Favorites"), style);
            GUI.color = previous;
        }

        private static Rect BuildAssetFavoriteStarRect(Rect cardRect)
        {
            return new Rect(cardRect.x + 5f, cardRect.yMax - 29f, 26f, 24f);
        }

        private static void ExecuteCandidateCardAction(ScenarioAuthoringInspectorAction action, bool armPlacementOnAssetBrowserClick)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return;

            ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
            if (armPlacementOnAssetBrowserClick
                && action.Id.StartsWith(ScenarioAuthoringActionIds.ActionAssetBrowserSelectPrefix, StringComparison.Ordinal))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionAssetBrowserPlaceSelected);
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

            bool compactReference = AllCastCardsAreCompactReferences(section);
            float minCardWidth = compactReference ? 288f : (compactInspector ? 300f : 360f);
            int maxColumns = compactReference ? 5 : (compactInspector ? 2 : 4);
            int columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + gap) / (minCardWidth + gap)), 1, maxColumns);
            float cardWidth = (availableWidth - (gap * (columns - 1))) / columns;
            float cardHeight = compactReference ? 72f : (compactInspector ? 174f : 198f);
            int column = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.CastCard == null)
                    continue;

                Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                if (item.CastCard.CompactReference)
                    DrawCastReferenceCard(cardRect, item.CastCard);
                else
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
            bool multiAction = HasSecondaryCastActions(card);
            bool clickable = !multiAction && primary != null && primary.Enabled && !string.IsNullOrEmpty(primary.Id);
            GUIContent content = new GUIContent(string.Empty, primary != null ? primary.Hint ?? primary.Detail ?? string.Empty : string.Empty);
            RegisterInteractiveRegion(rect);
            GUI.Box(rect, content, _uiContext.Styles.Card);

            bool hovered = clickable && IsInteractiveHoverAllowed(rect);
            DrawButtonAnimationOverlay(rect, primary != null ? primary.Id : null, clickable, hovered, false);

            Rect portraitRect = new Rect(rect.x + 10f, rect.y + 10f, 82f, 96f);
            DrawCastPortrait(portraitRect, card);

            DrawCastStatusPill(new Rect(rect.x + 100f, rect.y + 10f, rect.width - 112f, 20f), card.Status, TextAnchor.UpperRight);

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
                && !actionsRect.Contains(evt.mousePosition)
                && IsInteractiveVisualTopmost(rect))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(primary.Id);
                evt.Use();
            }
        }

        private void DrawCastReferenceCard(Rect rect, ScenarioCastCardViewModel card)
        {
            if (card == null)
                return;

            ScenarioAuthoringInspectorAction action = card.PrimaryAction;
            GUIContent content = new GUIContent(string.Empty, action != null ? action.Hint ?? action.Detail ?? string.Empty : string.Empty);
            RegisterInteractiveRegion(rect);
            bool clickable = action != null && action.Enabled && !string.IsNullOrEmpty(action.Id);
            bool clicked = DrawPlainButton(rect, content, _uiContext.Styles.Card, clickable);
            bool hovered = clickable && IsInteractiveHoverAllowed(rect);
            bool pressed = clickable && IsInteractiveMouseDownAllowed(rect);
            DrawButtonAnimationOverlay(rect, action != null ? action.Id : null, clickable, hovered, pressed);
            if (clicked)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            Rect portraitRect = new Rect(rect.x + 8f, rect.y + 8f, 46f, 56f);
            DrawCastPortrait(portraitRect, card);

            float actionWidth = action != null ? Math.Max(94f, MeasureButtonWidth(action, false, 18f)) : 0f;
            actionWidth = Math.Min(actionWidth, Math.Max(0f, rect.width - 172f));
            float statusReserve = action != null ? actionWidth + 10f : 136f;
            float textX = portraitRect.xMax + 9f;
            float textWidth = Math.Max(48f, rect.width - (textX - rect.x) - statusReserve - 10f);
            GUIStyle nameStyle = _uiContext.Styles.PaperTitleText;
            GUIStyle detailStyle = _uiContext.Styles.PaperMutedText;
            GUI.Label(new Rect(textX, rect.y + 13f, textWidth, 22f), ShortenToFit(card.Name ?? "Survivor", textWidth, nameStyle), nameStyle);
            GUI.Label(new Rect(textX, rect.y + 37f, textWidth, 18f), ShortenToFit(card.RoleLine ?? string.Empty, textWidth, detailStyle), detailStyle);

            if (action != null)
            {
                Rect actionPillRect = new Rect(rect.xMax - actionWidth - 8f, rect.y + 24f, actionWidth, 20f);
                ScenarioUiWidgets.DrawPill(actionPillRect, ShortenToFit(action.Label ?? string.Empty, actionPillRect.width - 12f, _uiContext.Styles.PaperMutedText), _uiContext.Styles, action.Enabled ? ScenarioUiPillEmphasis.Active : ScenarioUiPillEmphasis.Default);
            }
            else
            {
                DrawCastStatusPill(new Rect(rect.xMax - 146f, rect.y + 24f, 136f, 20f), card.Status, TextAnchor.UpperRight);
            }
        }

        private void DrawCastPortrait(Rect rect, ScenarioCastCardViewModel card)
        {
            DrawCastPortrait(rect, card, true);
        }

        private void DrawCastPortrait(Rect rect, ScenarioCastCardViewModel card, bool showColorSwatches)
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

            if (!showColorSwatches || card == null)
                return;

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
            if (card == null)
                return;

            ScenarioAuthoringInspectorAction[] secondary = card.SecondaryActions;
            int secondaryCount = 0;
            for (int i = 0; secondary != null && i < secondary.Length; i++)
            {
                if (secondary[i] != null)
                    secondaryCount++;
            }

            float gap = 4f;
            float primaryWidth = card.PrimaryAction != null ? Math.Max(82f, MeasureButtonWidth(card.PrimaryAction, false, 18f)) : 0f;
            float fullSecondaryWidth = 0f;
            for (int i = 0; secondary != null && i < secondary.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = secondary[i];
                if (action == null)
                    continue;

                if (fullSecondaryWidth > 0f)
                    fullSecondaryWidth += gap;
                fullSecondaryWidth += Math.Max(60f, MeasureButtonWidth(action, false, 18f));
            }

            float fullWidth = primaryWidth + (card.PrimaryAction != null && secondaryCount > 0 ? gap : 0f) + fullSecondaryWidth;
            bool compactSecondary = fullWidth > rect.width;
            float compactWidth = 30f;
            float x = rect.x;
            if (card.PrimaryAction != null)
            {
                float width = compactSecondary && secondaryCount > 0
                    ? Math.Min(primaryWidth, Math.Max(82f, rect.width - ((compactWidth + gap) * secondaryCount)))
                    : primaryWidth;
                DrawButton(new Rect(x, rect.y, width, rect.height), card.PrimaryAction, false);
                x += width + gap;
            }

            for (int i = 0; secondary != null && i < secondary.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = secondary[i];
                if (action == null)
                    continue;

                ScenarioAuthoringInspectorAction renderAction = compactSecondary ? CloneCastCompactAction(action) : action;
                float width = compactSecondary ? compactWidth : Math.Max(60f, MeasureButtonWidth(action, false, 18f));
                if (x + width > rect.xMax)
                    break;

                DrawButton(new Rect(x, rect.y, width, rect.height), renderAction, false);
                x += width + gap;
            }
        }

        private static bool HasSecondaryCastActions(ScenarioCastCardViewModel card)
        {
            for (int i = 0; card != null && card.SecondaryActions != null && i < card.SecondaryActions.Length; i++)
            {
                if (card.SecondaryActions[i] != null)
                    return true;
            }

            return false;
        }

        private ScenarioAuthoringInspectorAction CloneCastCompactAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return null;

            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Label = CompactCastActionLabel(action),
                Hint = string.IsNullOrEmpty(action.Hint) ? action.Label : action.Label + " - " + action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                Emphasized = action.Emphasized,
                DisabledReason = action.DisabledReason
            };
        }

        private static string CompactCastActionLabel(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(action.IconText))
                return action.IconText;
            if (StringContains(action.Label, "up"))
                return "^";
            if (StringContains(action.Label, "down"))
                return "v";
            if (StringContains(action.Label, "remove"))
                return "X";
            return Shorten(action.Label, 2);
        }

        private void DrawCastStatusPill(Rect bounds, string status, TextAnchor anchor)
        {
            if (string.IsNullOrEmpty(status))
                return;

            GUIStyle style = _uiContext.Styles.PaperMutedText;
            float width = Mathf.Clamp(style.CalcSize(new GUIContent(status)).x + 20f, 58f, Math.Max(58f, bounds.width));
            float x = anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight
                ? bounds.xMax - width
                : bounds.x;
            ScenarioUiWidgets.DrawPill(new Rect(x, bounds.y, width, bounds.height), status, _uiContext.Styles, ResolveCastStatusEmphasis(status));
        }

        private static ScenarioUiPillEmphasis ResolveCastStatusEmphasis(string status)
        {
            if (StringContains(status, "active") || StringContains(status, "starting"))
                return ScenarioUiPillEmphasis.Success;
            if (StringContains(status, "world only"))
                return ScenarioUiPillEmphasis.Default;
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

        private static bool AllCastCardsAreCompactReferences(ScenarioAuthoringInspectorSection section)
        {
            int count = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioCastCardViewModel card = section.Items[i] != null ? section.Items[i].CastCard : null;
                if (card == null)
                    continue;

                count++;
                if (!card.CompactReference)
                    return false;
            }

            return count > 0;
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

        private void DrawSpritePreview(Rect rect, Sprite sprite, bool emphasized, Color tint)
        {
            ScenarioUiWidgets.DrawSpritePreviewFrame(rect, sprite, _uiContext.Styles, emphasized, tint);
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

            return value;
        }

        private static string ShortenToFit(string value, float maxWidth, GUIStyle style)
        {
            return value ?? string.Empty;
        }

        private static string MiddleTruncate(string value, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(value) || style == null)
                return value ?? string.Empty;
            if (style.CalcSize(new GUIContent(value)).x <= maxWidth)
                return value;

            const string separator = " / ";
            float separatorWidth = style.CalcSize(new GUIContent(separator)).x;
            if (separatorWidth >= maxWidth)
                return string.Empty;

            int left = Math.Min(12, value.Length);
            int right = Math.Min(18, Math.Max(0, value.Length - left));
            while (left + right > 0)
            {
                string candidate = value.Substring(0, left) + separator + value.Substring(value.Length - right);
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

            return value;
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

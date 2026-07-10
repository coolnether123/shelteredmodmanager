using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private static bool IsAssetBrowserWorkshopPage(ScenarioAuthoringShellWindowViewModel window)
        {
            return window != null
                && string.Equals(window.Id, ScenarioAuthoringWindowIds.AssetBrowser, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateAssetBrowserOpenState(string activeWorkspaceId, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            bool open = string.Equals(activeWorkspaceId, ScenarioAuthoringWindowIds.AssetBrowser, StringComparison.OrdinalIgnoreCase);
            ScenarioAuthoringShellWindowViewModel buildTools = FindWindow(windows, ScenarioAuthoringWindowIds.BuildTools);
            open = open || (buildTools != null && buildTools.Visible && !buildTools.Collapsed && !IsAnyPlacementActive());
            if (!open)
                _assetBrowserDefaultResolved = false;
        }

        private Rect DrawAssetBrowserWorkshopPage(Rect bodyRect, ScenarioAuthoringShellWindowViewModel window)
        {
            return DrawAssetBrowserWorkshopPage(bodyRect, window, false);
        }

        private Rect DrawAssetBrowserWorkshopPage(Rect bodyRect, ScenarioAuthoringShellWindowViewModel window, bool armPlacementOnCardClick)
        {
            _assetBrowserSearchText = ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserSearch;
            _assetBrowserCategoryFilter = ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserCategory;
            if (!_assetBrowserDefaultResolved)
            {
                _assetBrowserCategoryFilter = ScenarioAssetBrowserUx.ResolveDefaultFilter(
                    _snapshot != null ? _snapshot.State : null,
                    window != null ? window.Sections : null);
                ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserCategory = _assetBrowserCategoryFilter;
                _assetBrowserDefaultResolved = true;
            }

            Rect searchRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, 32f);
            DrawAssetBrowserSearch(searchRect);

            float contentY = searchRect.yMax + 8f;
            float contentHeight = Math.Max(100f, bodyRect.yMax - contentY);
            bool compact = bodyRect.width < 980f;
            float detailWidth = compact ? 252f : 312f;
            detailWidth = Math.Min(detailWidth, Math.Max(220f, bodyRect.width * 0.30f));
            float railWidth = MeasureAssetBrowserRailWidth(window, compact);
            railWidth = Math.Min(railWidth, Math.Max(compact ? 138f : 172f, bodyRect.width - detailWidth - 250f));
            Rect railRect = new Rect(bodyRect.x, contentY, railWidth, contentHeight);
            Rect detailRect = new Rect(bodyRect.xMax - detailWidth, contentY, detailWidth, contentHeight);
            Rect gridRect = new Rect(railRect.xMax + 10f, contentY, Math.Max(220f, detailRect.x - railRect.xMax - 20f), contentHeight);

            DrawAssetBrowserCategoryRail(railRect, window);
            DrawAssetBrowserGrid(gridRect, window, armPlacementOnCardClick);
            DrawAssetBrowserDetailPane(detailRect, window);
            return bodyRect;
        }

        private void DrawAssetBrowserSearch(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(28f));
            Rect searchRect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
            bool searchTopmost = IsInteractiveVisualTopmost(searchRect);
            string nextSearchText;
            if (searchTopmost)
            {
                GUI.SetNextControlName("asset_browser_search");
                nextSearchText = GUI.TextField(searchRect, _assetBrowserSearchText ?? string.Empty, _uiContext.Styles.SearchField);
            }
            else
            {
                nextSearchText = _assetBrowserSearchText ?? string.Empty;
                GUI.Box(searchRect, nextSearchText, _uiContext.Styles.SearchField);
            }
            if (!string.Equals(nextSearchText, _assetBrowserSearchText ?? string.Empty, StringComparison.Ordinal))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetSearchPrefix, nextSearchText));
                _assetBrowserSearchText = ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserSearch;
            }
            DrawSearchPlaceholder(searchRect, _assetBrowserSearchText, "Filter assets");
            Event current = Event.current;
            if (searchTopmost && ((current != null && searchRect.Contains(current.mousePosition)) || string.Equals(GUI.GetNameOfFocusedControl(), "asset_browser_search", StringComparison.Ordinal)))
                DrawFieldFocusBorder(searchRect);

            float clearWidth = ScenarioUiMeasuredLabel.Width("Clear", _buttonStyle, 18f);
            Rect clearRect = GUILayoutUtility.GetRect(clearWidth, 30f, GUILayout.Width(clearWidth), GUILayout.Height(30f));
            if (DrawPlainButton(clearRect, new GUIContent("Clear"), _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionRendererAssetSearchClear);
                _assetBrowserSearchText = ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserSearch;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            _assetBrowserSearchFocused = searchTopmost && string.Equals(GUI.GetNameOfFocusedControl(), "asset_browser_search", StringComparison.Ordinal);
        }

        private void DrawAssetBrowserCategoryRail(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.Label("Browse", _sectionTitleStyle);
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            DrawAssetBrowserCategoryButton("Favorites", "Starred assets", ScenarioAssetBrowserUx.FavoritesFilter, ScenarioAssetBrowserUx.CountMatches(window, state, ScenarioAssetBrowserUx.FavoritesFilter));
            DrawAssetBrowserCategoryButton("Recent", "Last 20 placed / used", ScenarioAssetBrowserUx.RecentFilter, ScenarioAssetBrowserUx.CountMatches(window, state, ScenarioAssetBrowserUx.RecentFilter));
            DrawAssetBrowserCategoryButton("All", "Entire catalog", CandidateFilterAll, CountAssetBrowserCandidates(window));
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (!IsAssetBrowserCandidateSection(section))
                    continue;

                ScenarioAssetBrowserUx.CategoryLabel label = ScenarioAssetBrowserUx.GetCategoryLabel(section);
                DrawAssetBrowserCategoryButton(label.Primary, label.Secondary, section.Id, CountCandidateActions(section));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawAssetBrowserCategoryButton(string label, string secondary, string filter, int count)
        {
            bool active = string.Equals(_assetBrowserCategoryFilter, filter, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(_assetBrowserCategoryFilter) && string.Equals(filter, CandidateFilterAll, StringComparison.OrdinalIgnoreCase));
            string safeLabel = string.IsNullOrEmpty(label) ? "Assets" : label;
            string display = safeLabel + "  " + count;
            Rect rect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true), GUILayout.Height(32f));
            string tooltip = safeLabel + (string.IsNullOrEmpty(secondary) ? string.Empty : " — " + secondary);
            if (DrawPlainButton(rect, new GUIContent(display, tooltip), active ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetCategorySelectPrefix, filter));
                _assetBrowserCategoryFilter = ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserCategory;
            }
            GUILayout.Space(4f);
        }

        private void DrawAssetBrowserGrid(Rect rect, ScenarioAuthoringShellWindowViewModel window, bool armPlacementOnCardClick)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            GUILayout.BeginArea(inner);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, inner.width - 18f);
            Vector2 scroll = GetWindowScrollPosition("asset_browser.grid");
            RegisterScrollRegion("asset_browser.grid", inner);
            scroll = BeginMeasuredScrollView(scroll, inner);

            int visibleCount = 0;
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (!IsAssetBrowserCandidateSection(section) || !AssetBrowserCategoryMatches(section))
                    continue;

                int sectionVisibleCount = CountVisibleAssetBrowserActions(section);
                if (sectionVisibleCount == 0)
                    continue;

                GUILayout.Label(section.Title ?? "Assets", _sectionTitleStyle);
                DrawAssetBrowserGridSection(section, armPlacementOnCardClick);
                GUILayout.Space(10f);
                visibleCount += sectionVisibleCount;
            }

            if (visibleCount == 0)
                GUILayout.Label("No assets match the current search and category.", _mutedTextStyle);

            GUILayout.Space(18f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition("asset_browser.grid", scroll);
        }

        private void DrawAssetBrowserGridSection(ScenarioAuthoringInspectorSection section, bool armPlacementOnCardClick)
        {
            float availableWidth = GetSectionContentWidth();
            float gap = 8f;
            float minCardWidth = 178f;
            float preferredCardWidth = 218f;
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((availableWidth + gap) / (minCardWidth + gap)),
                1,
                5);
            float cardWidth = Math.Min(preferredCardWidth, (availableWidth - (gap * (columns - 1))) / columns);
            cardWidth = Mathf.Clamp(cardWidth, 154f, preferredCardWidth);
            float cardHeight = 122f;
            int count = 0;

            GUILayout.BeginHorizontal();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null)
                    continue;
                if (!IsVisibleAssetBrowserAction(section, item.Action))
                    continue;

                Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                DrawCandidateCard(cardRect, item.Action, armPlacementOnCardClick, true);
                count++;
                if (count % columns == 0 && HasMoreVisibleAssetBrowserAction(section, i + 1))
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(gap);
                    GUILayout.BeginHorizontal();
                }
                else
                {
                    GUILayout.Space(gap);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawAssetBrowserDetailPane(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            ScenarioAuthoringInspectorSection selected = FindSection(window, "asset_browser_selected");
            GUILayout.BeginArea(inner);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, inner.width - 18f);
            Vector2 scroll = GetWindowScrollPosition("asset_browser.detail");
            RegisterScrollRegion("asset_browser.detail", inner);
            scroll = BeginMeasuredScrollView(scroll, inner);
            DrawAssetBrowserSelectedDetails(selected);
            GUILayout.Space(18f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition("asset_browser.detail", scroll);
        }

        private void DrawAssetBrowserSelectedDetails(ScenarioAuthoringInspectorSection section)
        {
            GUILayout.Label(section != null ? section.Title ?? "Asset Details" : "Asset Details", _sectionTitleStyle);
            ScenarioAuthoringInspectorItem preview = FindFirstPreviewItem(section);
            if (preview == null || preview.PreviewSprite == null)
            {
                GUILayout.Label("Select an asset to see details", _mutedTextStyle);
                return;
            }

            Rect previewRect = GUILayoutUtility.GetRect(160f, 160f, GUILayout.ExpandWidth(true), GUILayout.Height(160f));
            float previewSize = Math.Min(160f, previewRect.width);
            Rect centeredPreview = new Rect(previewRect.x + ((previewRect.width - previewSize) * 0.5f), previewRect.y, previewSize, previewSize);
            DrawSpritePreview(centeredPreview, preview.PreviewSprite, true, preview.HasPreviewTint ? preview.PreviewTint : Color.white);
            GUILayout.Space(8f);
            GUILayout.Label(ShortenToFit(preview.Value ?? string.Empty, GetSectionContentWidth(), _smallTitleStyle), _smallTitleStyle);
            if (!string.IsNullOrEmpty(preview.Detail))
                GUILayout.Label(preview.Detail, _mutedTextStyle);
            GUILayout.Space(8f);

            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || ReferenceEquals(item, preview))
                    continue;

                if (item.Action != null)
                {
                    Rect buttonRect = GUILayoutUtility.GetRect(120f, 30f, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
                    DrawButton(buttonRect, item.Action, false);
                    GUILayout.Space(5f);
                }
                else if (item.Kind == ScenarioAuthoringInspectorItemKind.Property)
                {
                    DrawItem(item, false);
                    GUILayout.Space(4f);
                }
            }
        }

        private static ScenarioAuthoringInspectorItem FindFirstPreviewItem(ScenarioAuthoringInspectorSection section)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.PreviewSprite != null)
                    return item;
            }

            return null;
        }

        private bool AssetBrowserCategoryMatches(ScenarioAuthoringInspectorSection section)
        {
            return string.IsNullOrEmpty(_assetBrowserCategoryFilter)
                || string.Equals(_assetBrowserCategoryFilter, CandidateFilterAll, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_assetBrowserCategoryFilter, ScenarioAssetBrowserUx.FavoritesFilter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_assetBrowserCategoryFilter, ScenarioAssetBrowserUx.RecentFilter, StringComparison.OrdinalIgnoreCase)
                || (section != null && string.Equals(section.Id, _assetBrowserCategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        private int CountVisibleAssetBrowserActions(ScenarioAuthoringInspectorSection section)
        {
            int count = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                if (IsVisibleAssetBrowserAction(section, action))
                    count++;
            }
            return count;
        }

        private bool IsVisibleAssetBrowserAction(ScenarioAuthoringInspectorSection section, ScenarioAuthoringInspectorAction action)
        {
            return CandidateActionMatches(section, action, _assetBrowserSearchText, CandidateFilterAll)
                && ScenarioAssetBrowserUx.ActionMatches(
                    _snapshot != null ? _snapshot.State : null,
                    action,
                    _assetBrowserCategoryFilter);
        }

        private bool HasMoreVisibleAssetBrowserAction(ScenarioAuthoringInspectorSection section, int startIndex)
        {
            for (int i = Math.Max(0, startIndex); section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                if (IsVisibleAssetBrowserAction(section, action))
                    return true;
            }
            return false;
        }

        private static bool IsAssetBrowserCandidateSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid
                && !string.Equals(section.Id, "asset_browser_selected", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountAssetBrowserCandidates(ScenarioAuthoringShellWindowViewModel window)
        {
            int count = 0;
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (IsAssetBrowserCandidateSection(section))
                    count += CountCandidateActions(section);
            }

            return count;
        }

        private float MeasureAssetBrowserRailWidth(ScenarioAuthoringShellWindowViewModel window, bool compact)
        {
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            float width = compact ? 138f : 172f;
            width = Math.Max(width, ScenarioUiMeasuredLabel.Width("Favorites  " + ScenarioAssetBrowserUx.CountMatches(window, state, ScenarioAssetBrowserUx.FavoritesFilter), _buttonStyle, 18f));
            width = Math.Max(width, ScenarioUiMeasuredLabel.Width("Recent  " + ScenarioAssetBrowserUx.CountMatches(window, state, ScenarioAssetBrowserUx.RecentFilter), _buttonStyle, 18f));
            width = Math.Max(width, ScenarioUiMeasuredLabel.Width("All  " + CountAssetBrowserCandidates(window), _buttonStyle, 18f));
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (!IsAssetBrowserCandidateSection(section))
                    continue;
                ScenarioAssetBrowserUx.CategoryLabel label = ScenarioAssetBrowserUx.GetCategoryLabel(section);
                width = Math.Max(width, ScenarioUiMeasuredLabel.Width(label.Primary + "  " + CountCandidateActions(section), _buttonStyle, 18f));
            }
            return width;
        }
    }
}

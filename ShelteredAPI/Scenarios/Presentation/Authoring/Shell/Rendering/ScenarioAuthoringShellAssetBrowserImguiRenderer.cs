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

        private Rect DrawAssetBrowserWorkshopPage(Rect bodyRect, ScenarioAuthoringShellWindowViewModel window)
        {
            Rect searchRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, 32f);
            DrawAssetBrowserSearch(searchRect);

            float contentY = searchRect.yMax + 8f;
            float contentHeight = Math.Max(100f, bodyRect.yMax - contentY);
            bool compact = bodyRect.width < 980f;
            float railWidth = compact ? 138f : 172f;
            float detailWidth = compact ? 252f : 312f;
            detailWidth = Math.Min(detailWidth, Math.Max(220f, bodyRect.width * 0.30f));
            Rect railRect = new Rect(bodyRect.x, contentY, railWidth, contentHeight);
            Rect detailRect = new Rect(bodyRect.xMax - detailWidth, contentY, detailWidth, contentHeight);
            Rect gridRect = new Rect(railRect.xMax + 10f, contentY, Math.Max(220f, detailRect.x - railRect.xMax - 20f), contentHeight);

            DrawAssetBrowserCategoryRail(railRect, window);
            DrawAssetBrowserGrid(gridRect, window);
            DrawAssetBrowserDetailPane(detailRect, window);
            return bodyRect;
        }

        private void DrawAssetBrowserSearch(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _mutedTextStyle, GUILayout.Width(54f), GUILayout.Height(28f));
            Rect searchRect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            bool searchTopmost = IsInteractiveVisualTopmost(searchRect);
            string nextSearchText;
            if (searchTopmost)
            {
                GUI.SetNextControlName("asset_browser_search");
                nextSearchText = GUI.TextField(searchRect, _assetBrowserSearchText ?? string.Empty, _uiContext.Styles.Field);
            }
            else
            {
                nextSearchText = _assetBrowserSearchText ?? string.Empty;
                GUI.Box(searchRect, nextSearchText, _uiContext.Styles.Field);
            }
            if (!string.Equals(nextSearchText, _assetBrowserSearchText ?? string.Empty, StringComparison.Ordinal))
                _assetBrowserSearchText = nextSearchText;

            Rect clearRect = GUILayoutUtility.GetRect(68f, 28f, GUILayout.Width(68f), GUILayout.Height(28f));
            if (DrawPlainButton(clearRect, new GUIContent("Clear"), _buttonStyle, true))
                _assetBrowserSearchText = string.Empty;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            _assetBrowserSearchFocused = searchTopmost && string.Equals(GUI.GetNameOfFocusedControl(), "asset_browser_search", StringComparison.Ordinal);
        }

        private void DrawAssetBrowserCategoryRail(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.Label("Categories", _sectionTitleStyle);
            DrawAssetBrowserCategoryButton("All", CandidateFilterAll, CountAssetBrowserCandidates(window));
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (!IsAssetBrowserCandidateSection(section))
                    continue;

                DrawAssetBrowserCategoryButton(section.Title, section.Id, CountCandidateActions(section));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawAssetBrowserCategoryButton(string label, string filter, int count)
        {
            bool active = string.Equals(_assetBrowserCategoryFilter, filter, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(_assetBrowserCategoryFilter) && string.Equals(filter, CandidateFilterAll, StringComparison.OrdinalIgnoreCase));
            string safeLabel = string.IsNullOrEmpty(label) ? "Assets" : label;
            string display = safeLabel + "  " + count;
            Rect rect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
            string fitted;
            string fitTooltip;
            ScenarioUiMeasuredLabel.TryFitLabelWithTooltip(display, Math.Max(0f, rect.width - 14f), _buttonStyle, out fitted, out fitTooltip);
            if (DrawPlainButton(rect, new GUIContent(fitted, string.IsNullOrEmpty(fitTooltip) ? safeLabel : fitTooltip), active ? _activeButtonStyle : _buttonStyle, true))
                _assetBrowserCategoryFilter = filter;
            GUILayout.Space(4f);
        }

        private void DrawAssetBrowserGrid(Rect rect, ScenarioAuthoringShellWindowViewModel window)
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

                int sectionVisibleCount = CountFilteredCandidateActions(section, _assetBrowserSearchText, CandidateFilterAll);
                if (sectionVisibleCount == 0)
                    continue;

                GUILayout.Label(section.Title ?? "Assets", _sectionTitleStyle);
                DrawAssetBrowserGridSection(section);
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

        private void DrawAssetBrowserGridSection(ScenarioAuthoringInspectorSection section)
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
                if (!CandidateActionMatches(section, item.Action, _assetBrowserSearchText, CandidateFilterAll))
                    continue;

                Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                DrawCandidateCard(cardRect, item.Action);
                count++;
                if (count % columns == 0 && HasMoreVisibleCandidate(section, i + 1, _assetBrowserSearchText, CandidateFilterAll))
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
            DrawSpritePreview(centeredPreview, preview.PreviewSprite, true);
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
                || (section != null && string.Equals(section.Id, _assetBrowserCategoryFilter, StringComparison.OrdinalIgnoreCase));
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
    }
}

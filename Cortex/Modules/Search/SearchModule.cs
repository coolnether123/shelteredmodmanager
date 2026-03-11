using System.IO;
using Cortex.Core.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Search
{
    public sealed class SearchModule
    {
        private Vector2 _scroll = Vector2.zero;
        private GUIStyle _summaryStyle;
        private GUIStyle _summaryCaptionStyle;
        private GUIStyle _documentStyle;
        private GUIStyle _documentMetaStyle;
        private GUIStyle _resultButtonStyle;
        private GUIStyle _resultButtonActiveStyle;
        private GUIStyle _emptyStateStyle;
        private Texture2D _summaryBackground;
        private Texture2D _activeBackground;
        private string _appliedTheme = string.Empty;

        public void Draw(WorkbenchSearchService searchService, CortexNavigationService navigationService, CortexShellState state)
        {
            EnsureStyles();

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawSummary(state);
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawResults(searchService, navigationService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSummary(CortexShellState state)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            var results = state != null && state.Search != null ? state.Search.Results : null;
            GUILayout.Label(results != null ? results.StatusMessage ?? "Search ready." : "Search results will appear here.", _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.Label(BuildScopeCaption(state), _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawResults(WorkbenchSearchService searchService, CortexNavigationService navigationService, CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            var results = searchState != null ? searchState.Results : null;
            if (searchService == null || searchState == null || results == null || results.TotalMatchCount <= 0)
            {
                GUILayout.Label("Run a search to populate results.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var flatIndex = 0;
            for (var i = 0; i < results.Documents.Count; i++)
            {
                var document = results.Documents[i];
                if (document == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Path.GetFileName(document.DocumentPath) + "  (" + document.Matches.Count + ")", _documentStyle ?? GUI.skin.label);
                GUILayout.Label(document.DocumentPath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                GUILayout.Space(2f);

                for (var j = 0; j < document.Matches.Count; j++)
                {
                    var match = document.Matches[j];
                    var isActive = flatIndex == searchState.ActiveMatchIndex;
                    if (GUILayout.Button(
                        "Ln " + match.LineNumber + ", Col " + match.ColumnNumber + "  " + (match.PreviewText ?? match.LineText ?? string.Empty),
                        isActive ? (_resultButtonActiveStyle ?? GUI.skin.button) : (_resultButtonStyle ?? GUI.skin.button),
                        GUILayout.Height(24f)))
                    {
                        searchState.ActiveMatchIndex = flatIndex;
                        searchService.NavigateToMatch(match, state, navigationService);
                    }

                    flatIndex++;
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private string BuildScopeCaption(CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return string.Empty;
            }

            return "Scope: " + searchState.Query.Scope;
        }

        private void EnsureStyles()
        {
            var themeKey = CortexIdeLayout.GetBackgroundColor().ToString() + "|" + CortexIdeLayout.GetAccentColor().ToString();
            if (string.Equals(_appliedTheme, themeKey) &&
                _summaryStyle != null &&
                _documentStyle != null &&
                _resultButtonStyle != null &&
                _resultButtonActiveStyle != null)
            {
                return;
            }

            _appliedTheme = themeKey;
            _summaryBackground = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.45f));
            _activeBackground = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.20f));

            _summaryStyle = new GUIStyle(GUI.skin.box);
            _summaryStyle.padding = new RectOffset(10, 10, 8, 8);
            GuiStyleUtil.ApplyBackgroundToAllStates(_summaryStyle, _summaryBackground);

            _summaryCaptionStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_summaryCaptionStyle, CortexIdeLayout.GetTextColor());

            _documentStyle = new GUIStyle(GUI.skin.label);
            _documentStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_documentStyle, CortexIdeLayout.GetTextColor());

            _documentMetaStyle = new GUIStyle(GUI.skin.label);
            _documentMetaStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_documentMetaStyle, CortexIdeLayout.GetMutedTextColor());

            _resultButtonStyle = new GUIStyle(GUI.skin.button);
            _resultButtonStyle.alignment = TextAnchor.MiddleLeft;
            _resultButtonStyle.wordWrap = false;

            _resultButtonActiveStyle = new GUIStyle(_resultButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_resultButtonActiveStyle, _activeBackground);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, CortexIdeLayout.GetMutedTextColor());
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}

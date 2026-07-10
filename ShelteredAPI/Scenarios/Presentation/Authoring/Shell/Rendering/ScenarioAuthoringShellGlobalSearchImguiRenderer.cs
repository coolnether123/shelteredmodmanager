using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.UiKit;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Creator command palette / global search overlay. Opens on Ctrl+K (routed through the
    /// keyboard router) or the top-bar "Find" affordance, filters a lazily-built index of
    /// editor commands, authored elements, and help topics, and activates a result by running
    /// its action route through the existing dispatch. Kept in its own partial so the core
    /// shell render module does not grow.
    /// </summary>
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const string GlobalSearchControlName = "scenario.globalsearch.field";
        private const float GlobalSearchRowHeight = 46f;
        private const int GlobalSearchMaxResults = 50;

        private List<ScenarioGlobalSearchEntry> _globalSearchIndex;
        private List<ScenarioGlobalSearchEntry> _globalSearchResults = new List<ScenarioGlobalSearchEntry>();
        private bool _globalSearchIndexBuilt;
        private string _globalSearchText = string.Empty;
        private bool _globalSearchFocused;
        private bool _globalSearchNeedsFocus;
        private int _globalSearchSelectedIndex;
        private int _globalSearchFirstVisible;
        private Rect _globalSearchButtonRect = RuntimeCompat.ZeroRect();

        private Rect BuildGlobalSearchRect(float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            float width = Mathf.Clamp(scaledWidth * 0.42f, 460f, 620f);
            float height = Mathf.Clamp(scaledHeight * 0.60f, 320f, 500f);
            return ScenarioAuthoringShellLayout.BuildCenteredPopupRect(scaledWidth, scaledHeight, width, height, hudReserveRect);
        }

        private void EnsureGlobalSearchIndex()
        {
            if (_globalSearchIndexBuilt)
                return;

            _globalSearchIndexBuilt = true;
            _globalSearchNeedsFocus = true;
            _globalSearchSelectedIndex = 0;
            _globalSearchFirstVisible = 0;

            ScenarioAuthoringShellViewModel shell = _snapshot != null ? _snapshot.ShellViewModel : null;
            ScenarioDefinition definition = ResolveWorkingDefinition();
            List<string> versions = ResolveNamedVersions();
            _globalSearchIndex = ScenarioGlobalSearchService.BuildEntries(shell, definition, versions);
        }

        private void ResetGlobalSearchIndex()
        {
            _globalSearchIndexBuilt = false;
            _globalSearchIndex = null;
            _globalSearchResults.Clear();
            _globalSearchText = string.Empty;
            _globalSearchFocused = false;
            _globalSearchNeedsFocus = false;
            _globalSearchSelectedIndex = 0;
            _globalSearchFirstVisible = 0;
        }

        private static ScenarioDefinition ResolveWorkingDefinition()
        {
            try
            {
                IScenarioEditorSessionStore store = ScenarioCompositionRoot.Resolve<IScenarioEditorSessionStore>();
                ScenarioEditorSession session = store != null ? store.Current : null;
                return session != null ? session.WorkingDefinition : null;
            }
            catch
            {
                return null;
            }
        }

        private static List<string> ResolveNamedVersions()
        {
            List<string> names = new List<string>();
            try
            {
                ScenarioDraftSnapshotService snapshots = ScenarioCompositionRoot.Resolve<ScenarioDraftSnapshotService>();
                ScenarioDraftSnapshotInfo[] all = snapshots != null ? snapshots.ListSnapshots() : null;
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    ScenarioDraftSnapshotInfo info = all[i];
                    if (info != null && !info.IsAutosave && !string.IsNullOrEmpty(info.Name))
                        names.Add(info.Name);
                }
            }
            catch
            {
            }

            return names;
        }

        private void DrawGlobalSearchOverlayCore(Rect rect, ScenarioAuthoringInputCaptureService inputCapture)
        {
            EnsureGlobalSearchIndex();

            // Dim the editor behind the palette so focus reads as modal.
            float scaledWidth = _chromeViewportRect.width;
            float scaledHeight = _chromeViewportRect.height;
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, scaledWidth, scaledHeight), Texture2D.whiteTexture);
            GUI.color = oldColor;

            _globalSearchResults = ScenarioGlobalSearchService.Rank(_globalSearchIndex, _globalSearchText, GlobalSearchMaxResults);
            int resultCount = _globalSearchResults.Count;
            if (_globalSearchSelectedIndex >= resultCount)
                _globalSearchSelectedIndex = Math.Max(0, resultCount - 1);
            if (_globalSearchSelectedIndex < 0)
                _globalSearchSelectedIndex = 0;

            HandleGlobalSearchKeys(resultCount);

            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            Rect inner = Inset(rect, 16f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "FIND ANYTHING", _sectionTitleStyle);
            float fieldY = inner.y + 30f;
            Rect fieldRect = new Rect(inner.x, fieldY, inner.width, 30f);

            GUI.SetNextControlName(GlobalSearchControlName);
            GUIStyle fieldStyle = new GUIStyle(GUI.skin.textField);
            fieldStyle.fontSize = 14;
            fieldStyle.alignment = TextAnchor.MiddleLeft;
            string typed = GUI.TextField(fieldRect, _globalSearchText ?? string.Empty, fieldStyle);
            if (!string.Equals(typed, _globalSearchText, StringComparison.Ordinal))
            {
                _globalSearchText = typed;
                _globalSearchSelectedIndex = 0;
                _globalSearchFirstVisible = 0;
            }

            if (_globalSearchNeedsFocus)
            {
                GUI.FocusControl(GlobalSearchControlName);
                _globalSearchNeedsFocus = false;
            }

            _globalSearchFocused = string.Equals(GUI.GetNameOfFocusedControl(), GlobalSearchControlName, StringComparison.Ordinal);

            if (string.IsNullOrEmpty(_globalSearchText))
                GUI.Label(new Rect(fieldRect.x + 8f, fieldRect.y, fieldRect.width - 16f, fieldRect.height), "Type a command, stage, character, help topic...", _mutedTextStyle);

            float footerHeight = 22f;
            float listY = fieldRect.yMax + 10f;
            Rect listRect = new Rect(inner.x, listY, inner.width, Math.Max(60f, inner.yMax - listY - footerHeight - 6f));
            DrawGlobalSearchResults(listRect);

            GUI.Label(
                new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight),
                resultCount > 0
                    ? (resultCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " results   |   Up/Down navigate   Enter open   Esc close")
                    : "No matches   |   Esc close",
                _mutedTextStyle);
        }

        private void DrawGlobalSearchResults(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            int count = _globalSearchResults.Count;
            if (count == 0)
                return;

            Rect viewRect = Inset(rect, 4f);
            int visibleRows = Math.Max(1, Mathf.FloorToInt(viewRect.height / GlobalSearchRowHeight));

            // Mouse wheel scrolls the (absolute-positioned) row window. Rows are drawn in the
            // same coordinate space as the rest of the overlay so click routing stays correct.
            Event e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel && viewRect.Contains(e.mousePosition) && count > visibleRows)
            {
                _globalSearchFirstVisible += e.delta.y > 0f ? 1 : -1;
                e.Use();
            }

            ClampGlobalSearchWindow(count, visibleRows);

            for (int row = 0; row < visibleRows; row++)
            {
                int index = _globalSearchFirstVisible + row;
                if (index >= count)
                    break;

                Rect rowRect = new Rect(viewRect.x, viewRect.y + (row * GlobalSearchRowHeight), viewRect.width, GlobalSearchRowHeight - 4f);
                DrawGlobalSearchRow(rowRect, _globalSearchResults[index], index == _globalSearchSelectedIndex);
            }

            if (count > visibleRows)
            {
                GUI.Label(
                    new Rect(viewRect.xMax - 60f, viewRect.yMax - 18f, 60f, 18f),
                    (_globalSearchFirstVisible + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "-"
                        + Math.Min(count, _globalSearchFirstVisible + visibleRows).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _mutedTextStyle);
            }
        }

        private void ClampGlobalSearchWindow(int count, int visibleRows)
        {
            int maxFirst = Math.Max(0, count - visibleRows);
            if (_globalSearchSelectedIndex < _globalSearchFirstVisible)
                _globalSearchFirstVisible = _globalSearchSelectedIndex;
            else if (_globalSearchSelectedIndex >= _globalSearchFirstVisible + visibleRows)
                _globalSearchFirstVisible = _globalSearchSelectedIndex - visibleRows + 1;

            if (_globalSearchFirstVisible > maxFirst)
                _globalSearchFirstVisible = maxFirst;
            if (_globalSearchFirstVisible < 0)
                _globalSearchFirstVisible = 0;
        }

        private void DrawGlobalSearchRow(Rect rect, ScenarioGlobalSearchEntry entry, bool selected)
        {
            if (entry == null)
                return;

            if (selected)
            {
                Color old = GUI.color;
                GUI.color = new Color(0.55f, 0.41f, 0.16f, 0.34f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
            }

            if (DrawPlainButton(rect, GUIContent.none, _buttonContentStyle, true))
            {
                _globalSearchSelectedIndex = IndexOfResult(entry);
                ActivateGlobalSearchEntry(entry);
                if (Event.current != null)
                    Event.current.Use();
                return;
            }

            float chipWidth = 108f;
            Rect chipRect = new Rect(rect.x + 6f, rect.y + 8f, chipWidth, rect.height - 16f);
            GUI.Box(chipRect, entry.KindLabel, _mutedTextStyle);

            Rect textRect = new Rect(chipRect.xMax + 8f, rect.y + 4f, rect.width - chipWidth - 20f, rect.height - 8f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f), entry.Name, _textStyle);
            if (!string.IsNullOrEmpty(entry.Context))
                GUI.Label(new Rect(textRect.x, textRect.y + 20f, textRect.width, 18f), entry.Context, _mutedTextStyle);
        }

        private int IndexOfResult(ScenarioGlobalSearchEntry entry)
        {
            for (int i = 0; i < _globalSearchResults.Count; i++)
            {
                if (ReferenceEquals(_globalSearchResults[i], entry))
                    return i;
            }

            return 0;
        }

        private void HandleGlobalSearchKeys(int resultCount)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            bool ctrl = e.control || e.command;
            if (e.keyCode == KeyCode.Escape || (ctrl && e.keyCode == KeyCode.K))
            {
                CloseGlobalSearch();
                e.Use();
                return;
            }

            if (resultCount <= 0)
                return;

            if (e.keyCode == KeyCode.DownArrow)
            {
                _globalSearchSelectedIndex = (_globalSearchSelectedIndex + 1) % resultCount;
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                _globalSearchSelectedIndex = (_globalSearchSelectedIndex - 1 + resultCount) % resultCount;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (_globalSearchSelectedIndex >= 0 && _globalSearchSelectedIndex < _globalSearchResults.Count)
                    ActivateGlobalSearchEntry(_globalSearchResults[_globalSearchSelectedIndex]);
                e.Use();
            }
        }

        private void ActivateGlobalSearchEntry(ScenarioGlobalSearchEntry entry)
        {
            if (entry == null)
                return;

            for (int i = 0; entry.ActionIds != null && i < entry.ActionIds.Length; i++)
            {
                string actionId = entry.ActionIds[i];
                if (!string.IsNullOrEmpty(actionId))
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
            }

            CloseGlobalSearch();
        }

        private void CloseGlobalSearch()
        {
            ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionShellCloseGlobalSearch);
            ResetGlobalSearchIndex();
        }

        // Top-bar discoverability affordance. Drawn inside the top bar next to the window
        // menu button so creators can find search without knowing the Ctrl+K chord.
        private Rect DrawTopBarGlobalSearchButton(Rect windowMenuButtonRect, Rect animatedRect, bool compact)
        {
            _globalSearchButtonRect = RuntimeCompat.ZeroRect();
            float width = compact ? 68f : 118f;
            float height = windowMenuButtonRect.height > 0f ? windowMenuButtonRect.height : 28f;
            float y = windowMenuButtonRect.height > 0f ? windowMenuButtonRect.y : animatedRect.y + 8f;
            float right = windowMenuButtonRect.width > 0f
                ? windowMenuButtonRect.x - (compact ? 4f : 8f)
                : animatedRect.xMax - 10f;
            float x = right - width;
            if (x < animatedRect.x + 8f)
                return RuntimeCompat.ZeroRect();

            Rect rect = new Rect(x, y, width, height);
            bool open = _snapshot != null && _snapshot.State != null && _snapshot.State.GlobalSearchOpen;
            GUIContent content = new GUIContent(
                compact ? "Find" : "Find (Ctrl+K)",
                "Search commands and scenario elements (Ctrl+K).");
            if (DrawPlainButton(rect, content, open ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionShellToggleGlobalSearch);
                if (Event.current != null)
                    Event.current.Use();
            }

            _globalSearchButtonRect = rect;
            return rect;
        }
    }
}

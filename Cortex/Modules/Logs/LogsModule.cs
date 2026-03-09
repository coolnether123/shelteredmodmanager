using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Modules.Logs
{
    /// <summary>
    /// VS-style log output panel. Renders a compact, clickable log list at the top and an
    /// inline detail strip below (rather than a side-by-side pane). Clicking any entry
    /// attempts to navigate directly to the originating source file/line. If the source
    /// file is not found among mapped project sources, Cortex falls back to runtime
    /// navigation/decompiled output and opens the resolved target in the editor.
    /// </summary>
    public sealed class LogsModule
    {
        // ── filter state ──────────────────────────────────────────────────────────────
        private string _levelFilter = string.Empty;
        private string _textFilter = string.Empty;
        private bool _errorsVisible = true;
        private bool _warningsVisible = true;
        private bool _infoVisible = true;
        private int _lastEntryCount;

        // ── selection state ───────────────────────────────────────────────────────────
        private Vector2 _listScroll = Vector2.zero;
        private Vector2 _detailScroll = Vector2.zero;

        // ── styles ────────────────────────────────────────────────────────────────────
        private string _appliedTheme = string.Empty;
        private GUIStyle _toolbarStyle;
        private GUIStyle _severityToggleStyle;
        private GUIStyle _severityToggleActiveStyle;
        private GUIStyle _filterInputStyle;
        private GUIStyle _entryButtonStyle;
        private GUIStyle _entryButtonSelectedStyle;
        private GUIStyle _entryButtonErrorStyle;
        private GUIStyle _entryButtonWarningStyle;
        private GUIStyle _entryMetaStyle;
        private GUIStyle _entryMessageStyle;
        private GUIStyle _detailLabelStyle;
        private GUIStyle _detailKeyStyle;
        private GUIStyle _frameButtonStyle;
        private GUIStyle _frameButtonSelectedStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _actionButtonActiveStyle;
        private Texture2D _toolbarBg;
        private Texture2D _entryNormalBg;
        private Texture2D _entrySelectedBg;
        private Texture2D _entryErrorBg;
        private Texture2D _entryWarningBg;
        private Texture2D _frameBg;
        private Texture2D _frameSelectedBg;

        // ── constants ─────────────────────────────────────────────────────────────────
        private const float EntryHeight = 34f;
        private const float ToolbarHeight = 26f;
        private const float SeverityButtonWidth = 72f;
        private const float DetailPanelMinHeight = 180f;

        public void Draw(
            IRuntimeLogFeed logFeed,
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state,
            bool detachedWindow)
        {
            EnsureStyles(state);

            var settings = state.Settings ?? new CortexSettings();
            var allEntries = logFeed.ReadRecent(_levelFilter, settings.MaxRecentLogs);
            var visibleEntries = ApplyFilters(allEntries, state);

            if (allEntries.Count != _lastEntryCount && settings.AutoScrollLogs)
            {
                _listScroll.y = float.MaxValue;
            }

            _lastEntryCount = allEntries.Count;

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawToolbar(allEntries, visibleEntries, settings, state, detachedWindow);
            DrawLogSurface(visibleEntries, navigationService, sourcePathResolver, state);
            GUILayout.EndVertical();
        }

        private void DrawLogSurface(
            IList<RuntimeLogEntry> visibleEntries,
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            if (state.Logs.SelectedEntry == null)
            {
                CortexIdeLayout.DrawGroup("Log Stream", delegate
                {
                    DrawLogList(visibleEntries, navigationService, sourcePathResolver, state);
                }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                return;
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            CortexIdeLayout.DrawGroup("Log Stream", delegate
            {
                DrawLogList(visibleEntries, navigationService, sourcePathResolver, state);
            }, GUILayout.ExpandWidth(true), GUILayout.MinHeight(120f), GUILayout.Height(180f));
            GUILayout.Space(6f);
            CortexIdeLayout.DrawGroup("Entry Details", delegate
            {
                DrawDetailStrip(navigationService, sourcePathResolver, state);
            }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
        }

        // ── Toolbar ───────────────────────────────────────────────────────────────────

        private void DrawToolbar(
            IList<RuntimeLogEntry> allEntries,
            IList<RuntimeLogEntry> visibleEntries,
            CortexSettings settings,
            CortexShellState state,
            bool detachedWindow)
        {
            GUILayout.BeginHorizontal(_toolbarStyle ?? GUI.skin.box, GUILayout.Height(ToolbarHeight));

            // Severity toggles styled like VS's Error/Warning/Info chips
            DrawSeverityToggle(ref _errorsVisible, "⊗ Errors", CountBySeverity(visibleEntries, RuntimeLogSeverity.Error) + CountBySeverity(visibleEntries, RuntimeLogSeverity.Fatal), CortexIdeLayout.GetErrorColor());
            DrawSeverityToggle(ref _warningsVisible, "⚠ Warnings", CountBySeverity(visibleEntries, RuntimeLogSeverity.Warning), CortexIdeLayout.GetWarningColor());
            DrawSeverityToggle(ref _infoVisible, "ℹ Info", CountBySeverity(visibleEntries, RuntimeLogSeverity.Info) + CountBySeverity(visibleEntries, RuntimeLogSeverity.Debug), CortexIdeLayout.GetMutedTextColor());

            GUILayout.Space(8f);
            GUILayout.Label("Filter:", GUILayout.Width(36f));
            _textFilter = GUILayout.TextField(_textFilter ?? string.Empty, _filterInputStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_textFilter) && GUILayout.Button("×", GUILayout.Width(20f)))
            {
                _textFilter = string.Empty;
            }

            GUILayout.Space(6f);
            settings.AutoScrollLogs = GUILayout.Toggle(settings.AutoScrollLogs, "↓ Auto", GUILayout.Width(60f));

            if (!detachedWindow)
            {
                if (GUILayout.Button(state.Logs.ShowDetachedWindow ? "Dock" : "Pop Out", GUILayout.Width(66f)))
                {
                    state.Logs.ShowDetachedWindow = !state.Logs.ShowDetachedWindow;
                }
            }

            if (state.Logs.SelectedEntry != null && GUILayout.Button("Clear", GUILayout.Width(52f)))
            {
                state.Logs.SelectedEntry = null;
                state.Logs.SelectedFrameIndex = -1;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSeverityToggle(ref bool visible, string label, int count, Color color)
        {
            var style = visible ? (_severityToggleActiveStyle ?? GUI.skin.toggle) : (_severityToggleStyle ?? GUI.skin.toggle);
            visible = GUILayout.Toggle(visible, label + " " + count, style, GUILayout.Width(SeverityButtonWidth));
        }

        // ── Log list ──────────────────────────────────────────────────────────────────

        private void DrawLogList(
            IList<RuntimeLogEntry> visibleEntries,
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            var viewRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(80f));
            var rowCount = visibleEntries != null ? visibleEntries.Count : 0;
            var contentHeight = Mathf.Max(viewRect.height - 2f, rowCount > 0 ? (rowCount * EntryHeight) + 2f : 42f);
            var contentRect = new Rect(0f, 0f, Mathf.Max(1f, viewRect.width - 18f), contentHeight);
            var rowStartY = Mathf.Max(0f, contentHeight - (rowCount * EntryHeight));

            _listScroll = GUI.BeginScrollView(viewRect, _listScroll, contentRect, false, true);
            if (rowCount == 0)
            {
                GUI.Label(new Rect(8f, Mathf.Max(6f, contentHeight - 28f), Mathf.Max(80f, contentRect.width - 16f), 20f), "No entries match the current filters.", _detailLabelStyle ?? GUI.skin.label);
            }
            else
            {
                for (var i = 0; i < rowCount; i++)
                {
                    DrawEntryRow(
                        new Rect(0f, rowStartY + (i * EntryHeight), contentRect.width, EntryHeight),
                        visibleEntries[i],
                        navigationService,
                        sourcePathResolver,
                        state);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawEntryRow(
            Rect rowRect,
            RuntimeLogEntry entry,
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            var isSelected = IsSameEntry(state.Logs.SelectedEntry, entry);
            var severity = RuntimeLogVisuals.GetSeverity(entry.Level);

            var previousBg = GUI.backgroundColor;
            var previousContent = GUI.contentColor;

            GUIStyle rowStyle;
            if (isSelected)
            {
                rowStyle = _entryButtonSelectedStyle ?? GUI.skin.button;
                GUI.backgroundColor = RuntimeLogVisuals.GetEntryBackgroundColor(entry.Level, true);
            }
            else if (severity == RuntimeLogSeverity.Error || severity == RuntimeLogSeverity.Fatal)
            {
                rowStyle = _entryButtonErrorStyle ?? GUI.skin.button;
                GUI.backgroundColor = RuntimeLogVisuals.GetEntryBackgroundColor(entry.Level, false);
            }
            else if (severity == RuntimeLogSeverity.Warning)
            {
                rowStyle = _entryButtonWarningStyle ?? GUI.skin.button;
                GUI.backgroundColor = RuntimeLogVisuals.GetEntryBackgroundColor(entry.Level, false);
            }
            else
            {
                rowStyle = _entryButtonStyle ?? GUI.skin.button;
                GUI.backgroundColor = RuntimeLogVisuals.GetEntryBackgroundColor(entry.Level, false);
            }

            GUI.contentColor = RuntimeLogVisuals.GetEntryTextColor(entry.Level, isSelected);

            GUI.Box(rowRect, BuildRowLabel(entry), rowStyle);

            var current = Event.current;
            if (current != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 &&
                rowRect.Contains(current.mousePosition))
            {
                state.Logs.SelectedEntry = entry;
                state.Logs.SelectedFrameIndex = -1;

                if (current.clickCount >= 2)
                {
                    MMLog.WriteInfo("[Cortex.Logs] Double-activate navigation requested for log entry '" +
                        (entry.Source ?? "Unknown") + "'.");
                    NavigateToEntry(entry, navigationService, sourcePathResolver, state);
                }

                current.Use();
            }

            GUI.backgroundColor = previousBg;
            GUI.contentColor = previousContent;
        }

        // ── Inline detail strip ───────────────────────────────────────────────────────

        private void DrawDetailStrip(
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            var entry = state.Logs.SelectedEntry;
            if (entry == null)
            {
                return;
            }

            _detailScroll = GUILayout.BeginScrollView(
                _detailScroll, false, false,
                GUILayout.MinHeight(DetailPanelMinHeight),
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true));

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Header row: timestamp, level, source + action buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label(BuildDetailHeader(entry), _detailKeyStyle ?? GUI.skin.label, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Copy", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(52f)))
            {
                GUIUtility.systemCopyBuffer = BuildEntryClipboard(entry);
                state.StatusMessage = "Log entry copied.";
            }

            var location = sourcePathResolver != null
                ? sourcePathResolver.ResolveTextLocation(entry.Message, state.SelectedProject, state.Settings)
                : new SourceLocationMatch { Success = false };

            if (location.Success)
            {
                if (GUILayout.Button("▶ Source", _actionButtonActiveStyle ?? GUI.skin.button, GUILayout.Width(78f)))
                {
                    NavigateToLocation(navigationService, state, location.ResolvedPath, location.LineNumber);
                }
            }
            else if (entry.StackFrames != null && entry.StackFrames.Count > 0)
            {
                if (GUILayout.Button("▶ Frame", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(70f)))
                {
                    TryNavigateFirstFrame(navigationService, state, entry);
                }
            }

            GUILayout.EndHorizontal();

            // Message preview
            GUILayout.TextArea(
                entry.Message ?? string.Empty,
                _detailLabelStyle ?? GUI.skin.textArea,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(42f),
                GUILayout.MaxHeight(80f));

            // Stack frames (compact horizontal list)
            if (entry.StackFrames != null && entry.StackFrames.Count > 0)
            {
                DrawStackFrameList(navigationService, documentService, state, entry);
            }
            else
            {
                var navigationNote = BuildNavigationNote(location, entry);
                if (!string.IsNullOrEmpty(navigationNote))
                {
                    GUILayout.Label(navigationNote, _entryMetaStyle ?? GUI.skin.label);
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawStackFrameList(
            CortexNavigationService navigationService,
            CortexShellState state,
            RuntimeLogEntry entry)
        {
            GUILayout.Label("Stack: " + entry.StackFrames.Count + " frames", _detailKeyStyle ?? GUI.skin.label);

            for (var i = 0; i < entry.StackFrames.Count && i < 8; i++)
            {
                var frame = entry.StackFrames[i];
                var isSelected = state.Logs.SelectedFrameIndex == i;
                var frameLabel = BuildFrameLabel(frame, i);
                var style = isSelected ? (_frameButtonSelectedStyle ?? GUI.skin.button) : (_frameButtonStyle ?? GUI.skin.button);

                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(isSelected, frameLabel, style, GUILayout.ExpandWidth(true)))
                {
                    state.Logs.SelectedFrameIndex = i;
                }
                if (GUILayout.Button("→", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(26f)))
                {
                    state.Logs.SelectedFrameIndex = i;
                    OpenOrDecompileFrame(navigationService, state, entry, i);
                }
                GUILayout.EndHorizontal();
            }

            if (entry.StackFrames.Count > 8)
            {
                GUILayout.Label("… and " + (entry.StackFrames.Count - 8) + " more frames.", _entryMetaStyle ?? GUI.skin.label);
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────────────

        private void NavigateToLocation(CortexNavigationService navigationService, CortexShellState state, string filePath, int line)
        {
            var opened = navigationService != null
                ? navigationService.OpenDocument(state, filePath, line, "Opened " + System.IO.Path.GetFileName(filePath) + " @ line " + line, "Could not open resolved source file.")
                : null;
            if (opened != null)
            {
                MMLog.WriteInfo("[Cortex.Logs] Opened source from log entry -> " + filePath + ":" + line);
            }
            else
            {
                MMLog.WriteWarning("[Cortex.Logs] Failed to open resolved source from log entry -> " + filePath + ":" + line);
            }
        }

        private void TryNavigateFirstFrame(CortexNavigationService navigationService, CortexShellState state, RuntimeLogEntry entry)
        {
            if (entry == null || entry.StackFrames == null || entry.StackFrames.Count == 0)
            {
                return;
            }

            OpenOrDecompileFrame(navigationService, state, entry, 0);
        }

        private void NavigateToEntry(
            RuntimeLogEntry entry,
            ISourcePathResolver sourcePathResolver,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            if (entry == null)
            {
                return;
            }

            var location = sourcePathResolver != null
                ? sourcePathResolver.ResolveTextLocation(entry.Message, state.SelectedProject, state.Settings)
                : new SourceLocationMatch { Success = false };

            if (location.Success && !string.IsNullOrEmpty(location.ResolvedPath))
            {
                MMLog.WriteInfo("[Cortex.Logs] Resolved source marker for log entry '" +
                    (entry.Source ?? "Unknown") + "' -> " + location.ResolvedPath + ":" + location.LineNumber);
                NavigateToLocation(navigationService, state, location.ResolvedPath, location.LineNumber);
                return;
            }

            if (entry.StackFrames != null && entry.StackFrames.Count > 0)
            {
                MMLog.WriteInfo("[Cortex.Logs] Falling back to runtime stack frame navigation for log entry '" +
                    (entry.Source ?? "Unknown") + "'.");
                OpenOrDecompileFrame(navigationService, state, entry, 0);
                return;
            }

            state.StatusMessage = BuildNavigationNote(location, entry);
            MMLog.WriteWarning("[Cortex.Logs] No source marker or stack frame navigation target was available for log entry '" +
                (entry.Source ?? "Unknown") + "'.");
        }

        private void OpenOrDecompileFrame(
            CortexNavigationService navigationService,
            CortexShellState state,
            RuntimeLogEntry entry,
            int frameIndex)
        {
            if (navigationService == null)
            {
                state.StatusMessage = "Runtime navigation is unavailable.";
                return;
            }

            var target = navigationService.ResolveRuntimeTarget(entry, frameIndex, state);

            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                state.StatusMessage = target != null
                    ? target.StatusMessage
                    : "Navigation failed — no source or decompiled output available for this frame.";
                MMLog.WriteWarning("[Cortex.Logs] Runtime frame navigation failed for log entry '" +
                    (entry != null ? (entry.Source ?? "Unknown") : "Unknown") + "': " + state.StatusMessage);
                return;
            }

            if (!navigationService.OpenRuntimeTarget(state, target, target.StatusMessage, "Could not open resolved or decompiled file."))
            {
                MMLog.WriteWarning("[Cortex.Logs] Failed to open runtime navigation target -> " + (target.FilePath ?? string.Empty));
                return;
            }

            MMLog.WriteInfo("[Cortex.Logs] Opened runtime navigation target -> " + target.FilePath + ":" + target.LineNumber);
        }

        // ── Filtering ─────────────────────────────────────────────────────────────────

        private IList<RuntimeLogEntry> ApplyFilters(IList<RuntimeLogEntry> all, CortexShellState state)
        {
            var result = new List<RuntimeLogEntry>();
            if (all == null)
            {
                return result;
            }

            for (var i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                if (!MatchesFilter(entry, state))
                {
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        private bool MatchesFilter(RuntimeLogEntry entry, CortexShellState state)
        {
            if (entry == null)
            {
                return false;
            }

            // Severity toggles
            var severity = RuntimeLogVisuals.GetSeverity(entry.Level);
            if ((severity == RuntimeLogSeverity.Error || severity == RuntimeLogSeverity.Fatal) && !_errorsVisible)
            {
                return false;
            }

            if (severity == RuntimeLogSeverity.Warning && !_warningsVisible)
            {
                return false;
            }

            if ((severity == RuntimeLogSeverity.Info || severity == RuntimeLogSeverity.Debug) && !_infoVisible)
            {
                return false;
            }

            // Text filter
            if (!string.IsNullOrEmpty(_textFilter))
            {
                var msg = entry.Message ?? string.Empty;
                var src = entry.Source ?? string.Empty;
                if (msg.IndexOf(_textFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    src.IndexOf(_textFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            // Active project filter: if a project is selected and there's a mod-scope filter, try to apply it
            if (state.SelectedProject != null &&
                !string.IsNullOrEmpty(state.SelectedProject.ModId) &&
                !string.IsNullOrEmpty(_levelFilter))
            {
                // _levelFilter is repurposed as a plain level string when non-empty
            }

            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static string BuildRowLabel(RuntimeLogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("HH:mm:ss");
            var level = entry.Level ?? "Info";
            var source = string.IsNullOrEmpty(entry.Source) ? "Unknown" : entry.Source;
            var repeat = entry.RepeatCount > 1 ? "  ×" + entry.RepeatCount : string.Empty;
            var firstLine = FirstLine(entry.Message);

            return timestamp + "   [" + level + "]   " + source + repeat + "\n" + firstLine;
        }

        private static string BuildDetailHeader(RuntimeLogEntry entry)
        {
            return "[" + (entry.Level ?? "Info") + "]  "
                + (string.IsNullOrEmpty(entry.Source) ? "Unknown" : entry.Source)
                + "   " + entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string BuildEntryClipboard(RuntimeLogEntry entry)
        {
            return BuildDetailHeader(entry) + "\n" + (entry.Message ?? string.Empty);
        }

        private static string BuildNavigationNote(SourceLocationMatch location, RuntimeLogEntry entry)
        {
            if (location != null && location.Success)
            {
                return string.Empty;
            }

            if (location != null && !string.IsNullOrEmpty(location.StatusMessage))
            {
                return location.StatusMessage;
            }

            return "No source marker found in this entry. Add a stack trace or file:line reference to enable direct navigation.";
        }

        private static string BuildFrameLabel(RuntimeStackFrame frame, int index)
        {
            if (frame == null)
            {
                return "#" + index + " <unknown>";
            }

            if (!string.IsNullOrEmpty(frame.DisplayText))
            {
                return "#" + index + " " + frame.DisplayText;
            }

            var typeName = string.IsNullOrEmpty(frame.TypeName) ? "?" : frame.TypeName;
            var methodName = string.IsNullOrEmpty(frame.MethodName) ? "?" : frame.MethodName;
            return "#" + index + " " + typeName + "." + methodName;
        }

        private static int CountBySeverity(IList<RuntimeLogEntry> entries, RuntimeLogSeverity target)
        {
            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (RuntimeLogVisuals.GetSeverity(entries[i].Level) == target)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsSameEntry(RuntimeLogEntry left, RuntimeLogEntry right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(left.EntryId) || !string.IsNullOrEmpty(right.EntryId))
            {
                return string.Equals(left.EntryId, right.EntryId, StringComparison.Ordinal);
            }

            return left.Sequence == right.Sequence && left.Timestamp == right.Timestamp;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = text.Replace("\r\n", "\n");
            var index = normalized.IndexOf('\n');
            var first = index >= 0 ? normalized.Substring(0, index) : normalized;
            return first.Length > 140 ? first.Substring(0, 137) + "…" : first;
        }

        // ── Style management ──────────────────────────────────────────────────────────

        private void EnsureStyles(CortexShellState state)
        {
            var themeId = state.Settings != null && !string.IsNullOrEmpty(state.Settings.ThemeId)
                ? state.Settings.ThemeId
                : "cortex.vs-dark";

            if (string.Equals(_appliedTheme, themeId, StringComparison.OrdinalIgnoreCase) &&
                _toolbarStyle != null)
            {
                return;
            }

            _appliedTheme = themeId;

            var textColor = CortexIdeLayout.GetTextColor();
            var mutedColor = CortexIdeLayout.GetMutedTextColor();
            var accentColor = CortexIdeLayout.GetAccentColor();
            var surfaceColor = CortexIdeLayout.GetSurfaceColor();
            var headerColor = CortexIdeLayout.GetHeaderColor();
            var bgColor = CortexIdeLayout.GetBackgroundColor();
            var errorColor = CortexIdeLayout.GetErrorColor();
            var warningColor = CortexIdeLayout.GetWarningColor();

            // Toolbar
            _toolbarBg = MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.3f));
            _toolbarStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_toolbarStyle, _toolbarBg);
            _toolbarStyle.padding = new RectOffset(4, 4, 2, 2);
            _toolbarStyle.margin = new RectOffset(0, 0, 0, 0);

            // Severity toggles
            _severityToggleStyle = new GUIStyle(GUI.skin.button);
            _severityToggleStyle.fontSize = 11;
            _severityToggleStyle.alignment = TextAnchor.MiddleCenter;
            _severityToggleStyle.padding = new RectOffset(8, 8, 1, 1);
            _severityToggleStyle.margin = new RectOffset(0, 2, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_severityToggleStyle, MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.55f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_severityToggleStyle, mutedColor);

            _severityToggleActiveStyle = new GUIStyle(_severityToggleStyle);
            _severityToggleActiveStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyBackgroundToAllStates(_severityToggleActiveStyle, MakeTex(CortexIdeLayout.Blend(accentColor, headerColor, 0.18f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_severityToggleActiveStyle, textColor);

            // Filter input
            _filterInputStyle = new GUIStyle(GUI.skin.textField);
            _filterInputStyle.fontSize = 11;
            _filterInputStyle.padding = new RectOffset(6, 4, 2, 2);

            // Entry buttons
            _entryNormalBg = MakeTex(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.4f));
            _entrySelectedBg = MakeTex(CortexIdeLayout.Blend(headerColor, accentColor, 0.35f));
            _entryErrorBg = MakeTex(CortexIdeLayout.Blend(bgColor, errorColor, 0.07f));
            _entryWarningBg = MakeTex(CortexIdeLayout.Blend(bgColor, warningColor, 0.05f));

            _entryButtonStyle = new GUIStyle(GUI.skin.button);
            _entryButtonStyle.alignment = TextAnchor.UpperLeft;
            _entryButtonStyle.padding = new RectOffset(8, 8, 4, 4);
            _entryButtonStyle.margin = new RectOffset(0, 0, 1, 0);
            _entryButtonStyle.fontSize = 11;
            GuiStyleUtil.ApplyBackgroundToAllStates(_entryButtonStyle, _entryNormalBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_entryButtonStyle, mutedColor);

            _entryButtonSelectedStyle = new GUIStyle(_entryButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_entryButtonSelectedStyle, _entrySelectedBg);
            _entryButtonSelectedStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_entryButtonSelectedStyle, Color.white);

            _entryButtonErrorStyle = new GUIStyle(_entryButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_entryButtonErrorStyle, _entryErrorBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_entryButtonErrorStyle, errorColor);

            _entryButtonWarningStyle = new GUIStyle(_entryButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_entryButtonWarningStyle, _entryWarningBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_entryButtonWarningStyle, warningColor);

            // Meta/message label
            _entryMetaStyle = new GUIStyle(GUI.skin.label);
            _entryMetaStyle.fontSize = 10;
            _entryMetaStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_entryMetaStyle, mutedColor);

            _entryMessageStyle = new GUIStyle(GUI.skin.label);
            _entryMessageStyle.fontSize = 11;
            _entryMessageStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_entryMessageStyle, textColor);

            // Detail labels
            _detailLabelStyle = new GUIStyle(GUI.skin.textArea);
            _detailLabelStyle.wordWrap = true;
            _detailLabelStyle.fontSize = 11;
            GuiStyleUtil.ApplyTextColorToAllStates(_detailLabelStyle, textColor);

            _detailKeyStyle = new GUIStyle(GUI.skin.label);
            _detailKeyStyle.fontStyle = FontStyle.Bold;
            _detailKeyStyle.fontSize = 11;
            GuiStyleUtil.ApplyTextColorToAllStates(_detailKeyStyle, accentColor);

            // Frame buttons
            _frameBg = MakeTex(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.6f));
            _frameSelectedBg = MakeTex(CortexIdeLayout.Blend(headerColor, accentColor, 0.2f));

            _frameButtonStyle = new GUIStyle(GUI.skin.button);
            _frameButtonStyle.alignment = TextAnchor.MiddleLeft;
            _frameButtonStyle.fontSize = 10;
            _frameButtonStyle.padding = new RectOffset(6, 6, 2, 2);
            _frameButtonStyle.margin = new RectOffset(0, 0, 1, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_frameButtonStyle, _frameBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_frameButtonStyle, mutedColor);

            _frameButtonSelectedStyle = new GUIStyle(_frameButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_frameButtonSelectedStyle, _frameSelectedBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_frameButtonSelectedStyle, textColor);

            // Action buttons
            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            _actionButtonStyle.fontSize = 11;
            _actionButtonStyle.padding = new RectOffset(6, 6, 2, 2);
            _actionButtonStyle.margin = new RectOffset(2, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_actionButtonStyle, mutedColor);

            _actionButtonActiveStyle = new GUIStyle(_actionButtonStyle);
            _actionButtonActiveStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_actionButtonActiveStyle, accentColor);
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}

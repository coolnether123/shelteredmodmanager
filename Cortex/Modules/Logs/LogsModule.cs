using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Logs
{
    public sealed class LogsModule
    {
        private Vector2 _entryScroll = Vector2.zero;
        private Vector2 _detailScroll = Vector2.zero;
        private Vector2 _frameScroll = Vector2.zero;
        private Vector2 _backlogScroll = Vector2.zero;
        private string _minimumLevel = "Info";
        private string _modFilter = string.Empty;
        private string _sourceFilter = string.Empty;
        private string _messageFilter = string.Empty;
        private int _lastEntryCount;
        private readonly GUIStyle _wrappedLabel = new GUIStyle();
        private readonly GUIStyle _entryMetaStyle = new GUIStyle();
        private readonly GUIStyle _entryMessageStyle = new GUIStyle();
        private readonly GUIStyle _entryButtonStyle = new GUIStyle();
        private readonly GUIStyle _selectedEntryButtonStyle = new GUIStyle();
        private readonly GUIStyle _summaryStyle = new GUIStyle();
        private readonly GUIStyle _selectedSummaryStyle = new GUIStyle();

        public LogsModule()
        {
            _wrappedLabel.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_wrappedLabel, new Color(0.9f, 0.9f, 0.92f, 1f));

            _entryMetaStyle.wordWrap = false;
            _entryMetaStyle.clipping = TextClipping.Clip;
            GuiStyleUtil.ApplyTextColorToAllStates(_entryMetaStyle, new Color(0.8f, 0.82f, 0.88f, 1f));

            _entryMessageStyle.wordWrap = false;
            _entryMessageStyle.clipping = TextClipping.Clip;
            GuiStyleUtil.ApplyTextColorToAllStates(_entryMessageStyle, Color.white);

            _entryButtonStyle = new GUIStyle(GUI.skin.button);
            _entryButtonStyle.alignment = TextAnchor.UpperLeft;
            _entryButtonStyle.padding = new RectOffset(6, 6, 6, 6);
            _entryButtonStyle.margin = new RectOffset(0, 0, 2, 2);
            GuiStyleUtil.ApplyTextColorToAllStates(_entryButtonStyle, new Color(0.9f, 0.93f, 0.97f, 1f));

            _selectedEntryButtonStyle = new GUIStyle(_entryButtonStyle);
            _selectedEntryButtonStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_selectedEntryButtonStyle, Color.white);

            _summaryStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_summaryStyle, new Color(0.83f, 0.86f, 0.9f, 1f));

            _selectedSummaryStyle.wordWrap = true;
            _selectedSummaryStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_selectedSummaryStyle, new Color(0.98f, 0.98f, 0.98f, 1f));
        }

        public void Draw(IRuntimeLogFeed logFeed, IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state, bool detachedWindow)
        {
            var settings = state.Settings ?? new CortexSettings();
            var entries = logFeed.ReadRecent(_minimumLevel, settings.MaxRecentLogs);
            var visibleEntries = FilterEntries(entries);
            var summary = BuildSummary(entries.Count, visibleEntries);

            if (entries.Count != _lastEntryCount && settings.AutoScrollLogs)
            {
                _entryScroll.y = 1000000f;
            }
            _lastEntryCount = entries.Count;

            GUILayout.BeginVertical();
            DrawToolbar(settings, state, detachedWindow);
            DrawSummary(summary, state);

            CortexIdeLayout.DrawTwoPane(
                settings.LogsPaneWidth,
                360f,
                delegate
                {
                    DrawEntriesPane(logFeed, entries, visibleEntries, settings, state, detachedWindow);
                },
                delegate
                {
                    DrawDetailsPane(navigationService, documentService, state, detachedWindow);
                });

            GUILayout.EndVertical();
        }

        private void DrawToolbar(CortexSettings settings, CortexShellState state, bool detachedWindow)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Level", GUILayout.Width(36f));
            _minimumLevel = GUILayout.TextField(_minimumLevel, GUILayout.Width(70f));
            GUILayout.Label("Mod", GUILayout.Width(28f));
            _modFilter = GUILayout.TextField(_modFilter, GUILayout.Width(160f));
            if (state.SelectedProject != null && !string.IsNullOrEmpty(state.SelectedProject.ModId) && GUILayout.Button("Active", GUILayout.Width(56f)))
            {
                _modFilter = state.SelectedProject.ModId;
            }
            GUILayout.Label("Source", GUILayout.Width(44f));
            _sourceFilter = GUILayout.TextField(_sourceFilter, GUILayout.Width(160f));
            GUILayout.Label("Text", GUILayout.Width(30f));
            _messageFilter = GUILayout.TextField(_messageFilter, GUILayout.ExpandWidth(true));
            settings.AutoScrollLogs = GUILayout.Toggle(settings.AutoScrollLogs, "Auto");
            settings.ShowLogBacklog = GUILayout.Toggle(settings.ShowLogBacklog, "File Tail");
            if (!detachedWindow && GUILayout.Button(state.Logs.ShowDetachedWindow ? "Hide Window" : "Pop Out", GUILayout.Width(90f)))
            {
                state.Logs.ShowDetachedWindow = !state.Logs.ShowDetachedWindow;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSummary(LogSummary summary, CortexShellState state)
        {
            CortexIdeLayout.DrawGroup("Log Overview", delegate
            {
                GUILayout.BeginHorizontal();
                DrawSummaryChip("Errors", summary.Errors, RuntimeLogVisuals.GetAccentColor("Error"));
                DrawSummaryChip("Warnings", summary.Warnings, RuntimeLogVisuals.GetAccentColor("Warning"));
                DrawSummaryChip("Info", summary.Info, RuntimeLogVisuals.GetAccentColor("Info"));
                DrawSummaryChip("Live", summary.TotalLive, CortexIdeLayout.GetMutedTextColor());
                DrawSummaryChip("Visible", summary.Visible, new Color(0.49f, 0.9f, 0.65f, 1f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (state.Logs.SelectedEntry == null)
                {
                    GUILayout.Label("Select a log entry to inspect the full message, stack frames, and source navigation.", _summaryStyle);
                    return;
                }

                GUILayout.Label(
                    "Selected: [" + (state.Logs.SelectedEntry.Level ?? "Unknown") + "] " +
                    (string.IsNullOrEmpty(state.Logs.SelectedEntry.Source) ? "Unknown" : state.Logs.SelectedEntry.Source),
                    _selectedSummaryStyle);
                GUILayout.Label(FirstLine(state.Logs.SelectedEntry.Message), _summaryStyle);
            });
        }

        private static void DrawSummaryChip(string label, int value, Color accent)
        {
            var previous = GUI.contentColor;
            GUI.contentColor = accent;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(92f));
            GUILayout.Label(label);
            GUILayout.Label(value.ToString(), GUILayout.Width(42f));
            GUILayout.EndVertical();
            GUI.contentColor = previous;
        }

        private void DrawEntriesPane(IRuntimeLogFeed logFeed, IList<RuntimeLogEntry> entries, IList<RuntimeLogEntry> visibleEntries, CortexSettings settings, CortexShellState state, bool detachedWindow)
        {
            CortexIdeLayout.DrawGroup("Live Entries (" + visibleEntries.Count + " / " + entries.Count + ")", delegate
            {
                _entryScroll = GUILayout.BeginScrollView(_entryScroll, GUI.skin.box, GUILayout.MinHeight(detachedWindow ? 248f : 260f), GUILayout.ExpandHeight(true));
                if (visibleEntries.Count == 0)
                {
                    GUILayout.Label("No live entries match the current level, mod, source, and text filters.", _wrappedLabel);
                }
                else
                {
                    for (var i = 0; i < visibleEntries.Count; i++)
                    {
                        DrawEntryButton(visibleEntries[i], state);
                    }
                }
                GUILayout.EndScrollView();

                if (settings.ShowLogBacklog)
                {
                    GUILayout.Label("File Tail History");
                    GUILayout.Label("Optional raw file tail for lines that existed before Cortex attached or were not captured in the live feed.", _summaryStyle);
                    var backlog = logFeed.ReadBacklog(settings.LogFilePath, 18);
                    _backlogScroll = GUILayout.BeginScrollView(_backlogScroll, GUI.skin.box, GUILayout.Height(110f));
                    if (backlog.Count == 0)
                    {
                        GUILayout.Label("No file-tail history is available for the configured log path.", _wrappedLabel);
                    }
                    else
                    {
                        for (var i = 0; i < backlog.Count; i++)
                        {
                            GUILayout.Label(backlog[i], _wrappedLabel);
                        }
                    }
                    GUILayout.EndScrollView();
                }
            });
        }

        private void DrawDetailsPane(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state, bool detachedWindow)
        {
            CortexIdeLayout.DrawGroup("Selected Entry", delegate
            {
                _detailScroll = GUILayout.BeginScrollView(_detailScroll, GUI.skin.box, GUILayout.MinHeight(detachedWindow ? 404f : 430f), GUILayout.ExpandHeight(true));
                DrawSelectedEntry(navigationService, documentService, state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandHeight(true));
        }

        private void DrawSelectedEntry(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            if (state.Logs.SelectedEntry == null)
            {
                GUILayout.Label("Select a log entry to inspect its full message, copy it, or open the related source file.", _wrappedLabel);
                return;
            }

            GUILayout.Label("Time: " + state.Logs.SelectedEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            GUILayout.Label("Level: " + state.Logs.SelectedEntry.Level + " | Source: " + state.Logs.SelectedEntry.Source + " | Category: " + state.Logs.SelectedEntry.Category, _wrappedLabel);
            GUILayout.Label("Sequence: " + state.Logs.SelectedEntry.Sequence + " | Thread: " + state.Logs.SelectedEntry.ThreadId + " | Frame: " + state.Logs.SelectedEntry.UnityFrame, _wrappedLabel);
            if (state.Logs.SelectedEntry.RepeatCount > 1)
            {
                GUILayout.Label("Repeated " + state.Logs.SelectedEntry.RepeatCount + " times.", _wrappedLabel);
            }
            GUILayout.Space(6f);
            GUILayout.TextArea(state.Logs.SelectedEntry.Message ?? string.Empty, GUILayout.MinHeight(160f), GUILayout.ExpandHeight(false));

            string filePath;
            int lineNumber;
            var hasSource = CortexModuleUtil.TryResolveSourceLocation(state.Logs.SelectedEntry.Message, state.SelectedProject, state.Settings, out filePath, out lineNumber);
            GUILayout.Space(6f);
            GUILayout.Label(CortexModuleUtil.BuildSourceResolutionExplanation(state.Logs.SelectedEntry, state.SelectedProject, state.Settings), _wrappedLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Entry", GUILayout.Width(120f)))
            {
                GUIUtility.systemCopyBuffer = BuildEntryLabel(state.Logs.SelectedEntry) + "\n" + (state.Logs.SelectedEntry.Message ?? string.Empty);
                state.StatusMessage = "Copied selected log entry.";
            }
            if (hasSource && GUILayout.Button("Open Source", GUILayout.Width(120f)))
            {
                var opened = CortexModuleUtil.OpenDocument(documentService, state, filePath, lineNumber);
                if (opened != null)
                {
                    state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                    state.StatusMessage = "Opened " + filePath + " from log.";
                }
                else
                {
                    state.StatusMessage = "Failed to open resolved source file.";
                }
            }
            GUILayout.EndHorizontal();

            DrawStackFrames(navigationService, documentService, state);
        }

        private void DrawStackFrames(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            var entry = state.Logs.SelectedEntry;
            if (entry == null || entry.StackFrames == null || entry.StackFrames.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("No structured runtime stack frames are available for this entry.", _wrappedLabel);
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label("Runtime Stack Frames (" + entry.StackFrames.Count + ")");
            _frameScroll = GUILayout.BeginScrollView(_frameScroll, GUI.skin.box, GUILayout.Height(190f));
            for (var i = 0; i < entry.StackFrames.Count; i++)
            {
                var frame = entry.StackFrames[i];
                var label = BuildFrameLabel(frame, i);
                var isSelected = state.Logs.SelectedFrameIndex == i;
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(isSelected, label, "button", GUILayout.ExpandWidth(true)))
                {
                    state.Logs.SelectedFrameIndex = i;
                }
                if (GUILayout.Button("Open", GUILayout.Width(72f)))
                {
                    state.Logs.SelectedFrameIndex = i;
                    OpenStackFrame(navigationService, documentService, state, i);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (state.Logs.SelectedFrameIndex >= 0 && state.Logs.SelectedFrameIndex < entry.StackFrames.Count)
            {
                var selectedFrame = entry.StackFrames[state.Logs.SelectedFrameIndex];
                GUILayout.Label("Frame Details", _wrappedLabel);
                GUILayout.TextArea(BuildFrameDetails(selectedFrame), GUILayout.MinHeight(100f), GUILayout.ExpandHeight(false));
                if (GUILayout.Button("Open Selected Frame", GUILayout.Width(160f)))
                {
                    OpenStackFrame(navigationService, documentService, state, state.Logs.SelectedFrameIndex);
                }
            }
        }

        private void OpenStackFrame(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state, int frameIndex)
        {
            if (navigationService == null)
            {
                state.StatusMessage = "Runtime source navigation is not available.";
                return;
            }

            var target = navigationService.Resolve(state.Logs.SelectedEntry, frameIndex, state.SelectedProject, state.Settings);
            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                state.StatusMessage = target != null ? target.StatusMessage : "Runtime source navigation failed.";
                return;
            }

            var opened = CortexModuleUtil.OpenDocument(documentService, state, target.FilePath, target.LineNumber);
            if (opened == null)
            {
                state.StatusMessage = "Resolved source target could not be opened.";
                return;
            }

            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            state.StatusMessage = target.StatusMessage;
        }

        private static string BuildFrameLabel(RuntimeStackFrame frame, int index)
        {
            if (frame == null)
            {
                return "#" + index + " Unknown frame";
            }

            var label = string.IsNullOrEmpty(frame.DisplayText)
                ? ((string.IsNullOrEmpty(frame.TypeName) ? "UnknownType" : frame.TypeName) + "." + (string.IsNullOrEmpty(frame.MethodName) ? "UnknownMethod" : frame.MethodName))
                : frame.DisplayText;
            return "#" + index + " " + label;
        }

        private static string BuildFrameDetails(RuntimeStackFrame frame)
        {
            if (frame == null)
            {
                return "No frame details are available.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Type: " + (frame.TypeName ?? string.Empty));
            builder.AppendLine("Method: " + (frame.MethodName ?? string.Empty));
            builder.AppendLine("Assembly: " + (frame.AssemblyPath ?? string.Empty));
            builder.AppendLine("File: " + (frame.FilePath ?? string.Empty));
            builder.AppendLine("Line: " + frame.LineNumber + " | Column: " + frame.ColumnNumber);
            builder.AppendLine("Metadata Token: " + frame.MetadataToken);
            builder.AppendLine("IL Offset: " + frame.IlOffset);
            return builder.ToString();
        }

        private List<RuntimeLogEntry> FilterEntries(IList<RuntimeLogEntry> entries)
        {
            var visibleEntries = new List<RuntimeLogEntry>();
            if (entries == null)
            {
                return visibleEntries;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (Matches(entry))
                {
                    visibleEntries.Add(entry);
                }
            }

            return visibleEntries;
        }

        private bool Matches(RuntimeLogEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_modFilter))
            {
                var modScope = RuntimeLogVisuals.GetModScope(entry.Source);
                if (modScope.IndexOf(_modFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(_sourceFilter) &&
                (entry.Source ?? string.Empty).IndexOf(_sourceFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_messageFilter) &&
                (entry.Message ?? string.Empty).IndexOf(_messageFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return true;
        }

        private static string BuildEntryLabel(RuntimeLogEntry entry)
        {
            var source = string.IsNullOrEmpty(entry.Source) ? "Unknown" : entry.Source;
            var message = (entry.Message ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ');
            if (message.Length > 130)
            {
                message = message.Substring(0, 127) + "...";
            }

            return "[" + entry.Timestamp.ToString("HH:mm:ss") + "] [" + entry.Level + "] [" + source + "] " + message;
        }

        private void DrawEntryButton(RuntimeLogEntry entry, CortexShellState state)
        {
            var isSelected = IsSameEntry(state.Logs.SelectedEntry, entry);
            if (isSelected)
            {
                state.Logs.SelectedEntry = entry;
            }
            var previousBackground = GUI.backgroundColor;
            var previousContentColor = GUI.contentColor;

            GUI.backgroundColor = RuntimeLogVisuals.GetEntryBackgroundColor(entry.Level, isSelected);
            GUI.contentColor = RuntimeLogVisuals.GetEntryTextColor(entry.Level, isSelected);

            if (GUILayout.Button(BuildEntryButtonLabel(entry), isSelected ? _selectedEntryButtonStyle : _entryButtonStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(44f)))
            {
                state.Logs.SelectedEntry = entry;
                state.Logs.SelectedFrameIndex = -1;
            }

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContentColor;
        }

        private string BuildEntryButtonLabel(RuntimeLogEntry entry)
        {
            var repeatSuffix = entry.RepeatCount > 1 ? "  x" + entry.RepeatCount : string.Empty;
            return entry.Timestamp.ToString("HH:mm:ss") +
                "   [" + (entry.Level ?? "Info") + "]   " +
                (string.IsNullOrEmpty(entry.Source) ? "Unknown" : entry.Source) +
                repeatSuffix +
                "\n" + FirstLine(entry.Message);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = text.Replace("\r\n", "\n");
            var index = normalized.IndexOf('\n');
            var firstLine = index >= 0 ? normalized.Substring(0, index) : normalized;
            if (firstLine.Length > 120)
            {
                return firstLine.Substring(0, 117) + "...";
            }

            return firstLine;
        }

        private static LogSummary BuildSummary(int totalLive, IList<RuntimeLogEntry> visibleEntries)
        {
            var summary = new LogSummary();
            summary.TotalLive = totalLive;
            if (visibleEntries == null)
            {
                return summary;
            }

            for (var i = 0; i < visibleEntries.Count; i++)
            {
                var severity = RuntimeLogVisuals.GetSeverity(visibleEntries[i].Level);
                summary.Visible++;
                if (severity == RuntimeLogSeverity.Error || severity == RuntimeLogSeverity.Fatal)
                {
                    summary.Errors++;
                }
                else if (severity == RuntimeLogSeverity.Warning)
                {
                    summary.Warnings++;
                }
                else
                {
                    summary.Info++;
                }
            }

            return summary;
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
        private sealed class LogSummary
        {
            public int TotalLive;
            public int Errors;
            public int Warnings;
            public int Info;
            public int Visible;
        }
    }
}

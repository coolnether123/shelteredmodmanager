using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using ModAPI.Core;
using UnityEngine;

namespace Cortex
{
    internal static class CortexModuleUtil
    {
        private static readonly Regex StackTraceLocationRegex = new Regex(@" in (?<path>.*):line (?<line>\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex CompilerLocationRegex = new Regex(@"^(?<path>.*)\((?<line>\d+)(,(?<column>\d+))?\):", RegexOptions.IgnoreCase);

        public static DocumentSession OpenDocument(IDocumentService documentService, CortexShellState state, string filePath, int highlightedLine)
        {
            if (documentService == null || state == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(filePath);
            for (var i = 0; i < state.OpenDocuments.Count; i++)
            {
                if (string.Equals(state.OpenDocuments[i].FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    state.ActiveDocument = state.OpenDocuments[i];
                    state.ActiveDocumentPath = fullPath;
                    state.ActiveDocument.HighlightedLine = highlightedLine;
                    return state.ActiveDocument;
                }
            }

            var session = documentService.Open(fullPath);
            session.HighlightedLine = highlightedLine;
            state.OpenDocuments.Add(session);
            state.ActiveDocument = session;
            state.ActiveDocumentPath = fullPath;
            return session;
        }

        public static void CloseDocument(CortexShellState state, string filePath)
        {
            if (state == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            for (var i = state.OpenDocuments.Count - 1; i >= 0; i--)
            {
                if (string.Equals(state.OpenDocuments[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    state.OpenDocuments.RemoveAt(i);
                }
            }

            if (state.ActiveDocument != null && string.Equals(state.ActiveDocument.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                state.ActiveDocument = state.OpenDocuments.Count > 0 ? state.OpenDocuments[0] : null;
                state.ActiveDocumentPath = state.ActiveDocument != null ? state.ActiveDocument.FilePath : string.Empty;
            }
        }

        public static string GetDocumentDisplayName(DocumentSession session)
        {
            if (session == null)
            {
                return "Unknown";
            }

            var name = Path.GetFileName(session.FilePath);
            if (string.IsNullOrEmpty(name))
            {
                name = session.FilePath ?? "Untitled";
            }

            return session.IsDirty ? name + "*" : name;
        }

        public static bool TryResolveSourceLocation(string text, CortexProjectDefinition project, CortexSettings settings, out string filePath, out int lineNumber)
        {
            filePath = string.Empty;
            lineNumber = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var match = StackTraceLocationRegex.Match(text);
            if (match.Success)
            {
                lineNumber = ParseInt(match.Groups["line"].Value);
                filePath = ResolveCandidatePath(project, settings, match.Groups["path"].Value);
                return !string.IsNullOrEmpty(filePath);
            }

            match = CompilerLocationRegex.Match(text);
            if (match.Success)
            {
                lineNumber = ParseInt(match.Groups["line"].Value);
                filePath = ResolveCandidatePath(project, settings, match.Groups["path"].Value);
                return !string.IsNullOrEmpty(filePath);
            }

            return false;
        }

        public static string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath))
            {
                return string.Empty;
            }

            rawPath = rawPath.Trim().Trim('"');
            if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            if (File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var searchRoots = BuildSearchRoots(project, settings);
            for (var i = 0; i < searchRoots.Count; i++)
            {
                var sourceRoot = searchRoots[i];
                var combined = Path.Combine(sourceRoot, rawPath);
                if (File.Exists(combined))
                {
                    return Path.GetFullPath(combined);
                }

                var fileName = Path.GetFileName(rawPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    var files = FindFilesSafe(sourceRoot, fileName);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }

            return string.Empty;
        }

        private static List<string> BuildSearchRoots(CortexProjectDefinition project, CortexSettings settings)
        {
            var roots = new List<string>();
            AddRoot(roots, project != null ? project.SourceRootPath : string.Empty);

            var rawRoots = settings != null ? settings.AdditionalSourceRoots : string.Empty;
            if (!string.IsNullOrEmpty(rawRoots))
            {
                var segments = rawRoots.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    AddRoot(roots, segments[i]);
                }
            }

            AddRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            AddRoot(roots, settings != null ? settings.ModsRootPath : string.Empty);
            return roots;
        }

        private static void AddRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root.Trim());
                if (Directory.Exists(fullPath) && !roots.Contains(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        public static string[] FindFilesSafe(string rootPath, string pattern)
        {
            try
            {
                if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                {
                    return Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories);
                }
            }
            catch
            {
            }

            return new string[0];
        }

        public static int ParseInt(string raw)
        {
            int value;
            return int.TryParse(raw, out value) ? value : 0;
        }

        public static string[] SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }
    }

    public sealed class LogsModule
    {
        private Vector2 _entryScroll = Vector2.zero;
        private Vector2 _detailScroll = Vector2.zero;
        private Vector2 _frameScroll = Vector2.zero;
        private string _minimumLevel = "Info";
        private string _sourceFilter = string.Empty;
        private string _messageFilter = string.Empty;
        private int _lastEntryCount;
        private readonly GUIStyle _wrappedLabel = new GUIStyle();
        private readonly GUIStyle _entryMetaStyle = new GUIStyle();
        private readonly GUIStyle _entryMessageStyle = new GUIStyle();
        private readonly GUIStyle _entryButtonStyle = new GUIStyle();

        public LogsModule()
        {
            _wrappedLabel.wordWrap = true;
            _wrappedLabel.normal.textColor = new Color(0.9f, 0.9f, 0.92f, 1f);
            _entryMetaStyle.wordWrap = false;
            _entryMetaStyle.clipping = TextClipping.Clip;
            _entryMetaStyle.normal.textColor = new Color(0.8f, 0.82f, 0.88f, 1f);
            _entryMessageStyle.wordWrap = false;
            _entryMessageStyle.clipping = TextClipping.Clip;
            _entryMessageStyle.normal.textColor = Color.white;
            _entryButtonStyle = new GUIStyle(GUI.skin.button);
            _entryButtonStyle.alignment = TextAnchor.UpperLeft;
            _entryButtonStyle.padding = new RectOffset(6, 6, 6, 6);
            _entryButtonStyle.margin = new RectOffset(0, 0, 2, 2);
        }

        public void Draw(IRuntimeLogFeed logFeed, IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state, bool detachedWindow)
        {
            var settings = state.Settings ?? new CortexSettings();
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Level", GUILayout.Width(36f));
            _minimumLevel = GUILayout.TextField(_minimumLevel, GUILayout.Width(70f));
            GUILayout.Label("Source", GUILayout.Width(44f));
            _sourceFilter = GUILayout.TextField(_sourceFilter, GUILayout.Width(160f));
            GUILayout.Label("Text", GUILayout.Width(30f));
            _messageFilter = GUILayout.TextField(_messageFilter, GUILayout.ExpandWidth(true));
            settings.AutoScrollLogs = GUILayout.Toggle(settings.AutoScrollLogs, "Auto");
            settings.ShowLogBacklog = GUILayout.Toggle(settings.ShowLogBacklog, "Backlog");
            if (!detachedWindow && GUILayout.Button(state.ShowDetachedLogWindow ? "Hide Window" : "Pop Out", GUILayout.Width(90f)))
            {
                state.ShowDetachedLogWindow = !state.ShowDetachedLogWindow;
            }
            GUILayout.EndHorizontal();

            var entries = logFeed.ReadRecent(_minimumLevel, settings.MaxRecentLogs);
            if (entries.Count != _lastEntryCount && settings.AutoScrollLogs)
            {
                _entryScroll.y = 1000000f;
            }
            _lastEntryCount = entries.Count;

            CortexIdeLayout.DrawTwoPane(
                settings.LogsPaneWidth,
                360f,
                delegate
                {
                    DrawEntriesPane(logFeed, entries, settings, state, detachedWindow);
                },
                delegate
                {
                    DrawDetailsPane(navigationService, documentService, state, detachedWindow);
                });
            GUILayout.EndVertical();
        }

        private void DrawEntriesPane(IRuntimeLogFeed logFeed, IList<RuntimeLogEntry> entries, CortexSettings settings, CortexShellState state, bool detachedWindow)
        {
            CortexIdeLayout.DrawGroup("Live Entries (" + entries.Count + ")", delegate
            {
                _entryScroll = GUILayout.BeginScrollView(_entryScroll, GUI.skin.box, GUILayout.Height(detachedWindow ? 300f : 330f));
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!Matches(entry))
                    {
                        continue;
                    }

                    DrawEntryButton(entry, state);
                }
                GUILayout.EndScrollView();

                if (settings.ShowLogBacklog)
                {
                    GUILayout.Label("Recent File Backlog");
                    var backlog = logFeed.ReadBacklog(settings.LogFilePath, 18);
                    CortexIdeLayout.DrawGroup(string.Empty, delegate
                    {
                        for (var i = 0; i < backlog.Count; i++)
                        {
                            GUILayout.Label(backlog[i], _wrappedLabel);
                        }
                    });
                }
            });
        }

        private void DrawDetailsPane(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state, bool detachedWindow)
        {
            CortexIdeLayout.DrawGroup("Selected Entry", delegate
            {
                _detailScroll = GUILayout.BeginScrollView(_detailScroll, GUI.skin.box, GUILayout.Height(detachedWindow ? 460f : 500f));
                DrawSelectedEntry(navigationService, documentService, state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandHeight(true));
        }

        private void DrawSelectedEntry(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            if (state.SelectedLogEntry == null)
            {
                GUILayout.Label("Select a log entry to inspect its full message, copy it, or open the related source file.", _wrappedLabel);
                return;
            }

            GUILayout.Label("Time: " + state.SelectedLogEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            GUILayout.Label("Level: " + state.SelectedLogEntry.Level + " | Source: " + state.SelectedLogEntry.Source + " | Category: " + state.SelectedLogEntry.Category, _wrappedLabel);
            GUILayout.Space(6f);
            GUILayout.TextArea(state.SelectedLogEntry.Message ?? string.Empty, GUILayout.MinHeight(160f), GUILayout.ExpandHeight(false));

            string filePath;
            int lineNumber;
            var hasSource = CortexModuleUtil.TryResolveSourceLocation(state.SelectedLogEntry.Message, state.SelectedProject, state.Settings, out filePath, out lineNumber);
            GUILayout.Space(6f);
            GUILayout.Label(hasSource
                ? "Resolved source: " + filePath + " @ line " + lineNumber
                : "No source mapping was found in this entry. Stack traces and compiler-style file markers are supported.",
                _wrappedLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Entry", GUILayout.Width(120f)))
            {
                GUIUtility.systemCopyBuffer = BuildEntryLabel(state.SelectedLogEntry) + "\n" + (state.SelectedLogEntry.Message ?? string.Empty);
                state.StatusMessage = "Copied selected log entry.";
                MMLog.WriteDebug("Cortex copied the selected log entry to the clipboard.", MMLog.LogCategory.UI);
            }
            if (hasSource && GUILayout.Button("Open Source", GUILayout.Width(120f)))
            {
                CortexModuleUtil.OpenDocument(documentService, state, filePath, lineNumber);
                state.RequestedTabIndex = 2;
                state.StatusMessage = "Opened " + filePath + " from log.";
                MMLog.WriteInfo("Cortex opened source directly from a log message marker: " + filePath, MMLog.LogCategory.UI);
            }
            GUILayout.EndHorizontal();

            DrawStackFrames(navigationService, documentService, state);
        }

        private void DrawStackFrames(IRuntimeSourceNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            var entry = state.SelectedLogEntry;
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
                var isSelected = state.SelectedLogFrameIndex == i;
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(isSelected, label, "button", GUILayout.ExpandWidth(true)))
                {
                    state.SelectedLogFrameIndex = i;
                }
                if (GUILayout.Button("Open", GUILayout.Width(72f)))
                {
                    state.SelectedLogFrameIndex = i;
                    OpenStackFrame(navigationService, documentService, state, i);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (state.SelectedLogFrameIndex >= 0 && state.SelectedLogFrameIndex < entry.StackFrames.Count)
            {
                var selectedFrame = entry.StackFrames[state.SelectedLogFrameIndex];
                GUILayout.Label("Frame Details", _wrappedLabel);
                GUILayout.TextArea(BuildFrameDetails(selectedFrame), GUILayout.MinHeight(100f), GUILayout.ExpandHeight(false));
                if (GUILayout.Button("Open Selected Frame", GUILayout.Width(160f)))
                {
                    OpenStackFrame(navigationService, documentService, state, state.SelectedLogFrameIndex);
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

            var target = navigationService.Resolve(state.SelectedLogEntry, frameIndex, state.SelectedProject);
            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                state.StatusMessage = target != null ? target.StatusMessage : "Runtime source navigation failed.";
                MMLog.WriteWarning("Cortex could not navigate from runtime stack frame: " + state.StatusMessage, MMLog.LogCategory.UI);
                return;
            }

            CortexModuleUtil.OpenDocument(documentService, state, target.FilePath, target.LineNumber);
            state.RequestedTabIndex = 2;
            state.StatusMessage = target.StatusMessage;
            MMLog.WriteInfo(
                "Cortex opened " + target.FilePath + " from runtime stack frame " + frameIndex + (target.IsDecompiledSource ? " using cached source." : " using PDB source."),
                MMLog.LogCategory.UI);
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

        private bool Matches(RuntimeLogEntry entry)
        {
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
            var background = GUI.color;
            GUI.color = GetLevelColor(entry.Level);
            GUILayout.BeginVertical(_entryButtonStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(42f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(entry.Timestamp.ToString("HH:mm:ss"), _entryMetaStyle, GUILayout.Width(64f));
            GUILayout.Label("[" + entry.Level + "]", _entryMetaStyle, GUILayout.Width(52f));
            GUILayout.Label(string.IsNullOrEmpty(entry.Source) ? "Unknown" : entry.Source, _entryMetaStyle, GUILayout.Width(118f));
            GUILayout.Label(FirstLine(entry.Message), _entryMessageStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            var clickRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndVertical();
            GUI.color = background;

            var fullRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && fullRect.Contains(Event.current.mousePosition))
            {
                state.SelectedLogEntry = entry;
                state.SelectedLogFrameIndex = -1;
                MMLog.WriteDebug("Cortex selected log entry from " + (entry.Source ?? "Unknown") + ".", MMLog.LogCategory.UI);
                Event.current.Use();
            }
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

        private static Color GetLevelColor(string level)
        {
            if (string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1f, 0.74f, 0.74f);
            }

            if (string.Equals(level, "Warning", StringComparison.OrdinalIgnoreCase) || string.Equals(level, "Warn", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1f, 0.91f, 0.65f);
            }

            return Color.white;
        }
    }

    public sealed class ProjectsModule
    {
        private string _modId = string.Empty;
        private string _sourceRoot = string.Empty;
        private string _projectFile = string.Empty;
        private string _buildOverride = string.Empty;
        private string _outputAssembly = string.Empty;
        private string _outputPdb = string.Empty;
        private string _searchText = string.Empty;
        private string _validationMessage = string.Empty;
        private Vector2 _projectScroll = Vector2.zero;

        public void Draw(IProjectCatalog catalog, CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            CortexIdeLayout.DrawTwoPane(
                settings.ProjectsPaneWidth,
                300f,
                delegate { DrawProjectList(catalog, state); },
                delegate { DrawProjectEditor(catalog, state); });
        }

        private void DrawProjectList(IProjectCatalog catalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.Width(360f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Configured Projects");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(46f));
            _searchText = GUILayout.TextField(_searchText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Import Workspace", GUILayout.Width(120f)))
            {
                ImportWorkspaceProjects(catalog, state);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            var projects = catalog.GetProjects();
            GUILayout.Label("Projects: " + projects.Count);
            _projectScroll = GUILayout.BeginScrollView(_projectScroll, GUI.skin.box, GUILayout.Height(540f));
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                var displayName = project.GetDisplayName();
                if (!string.IsNullOrEmpty(_searchText) &&
                    displayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (project.SourceRootPath ?? string.Empty).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var label = displayName + "\n" + (project.SourceRootPath ?? string.Empty);
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true), GUILayout.MinHeight(46f)))
                {
                    state.SelectedProject = project;
                    Load(project);
                    _validationMessage = BuildValidation(project);
                    state.StatusMessage = "Selected project " + project.GetDisplayName();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawProjectEditor(IProjectCatalog catalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Project Setup");
            DrawField("Mod ID", ref _modId);
            DrawField("Source Root", ref _sourceRoot);
            DrawField("Project File", ref _projectFile);
            DrawField("Build Override", ref _buildOverride);
            DrawField("Output DLL", ref _outputAssembly);
            DrawField("Output PDB", ref _outputPdb);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Detect .csproj", GUILayout.Width(140f)))
            {
                AutoDetectProjectFile();
            }
            if (GUILayout.Button("Use Defaults", GUILayout.Width(120f)))
            {
                ApplyDefaultsFromProjectPath();
            }
            if (GUILayout.Button("Validate", GUILayout.Width(90f)))
            {
                _validationMessage = BuildValidation(CreateDefinition());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project", GUILayout.Width(120f)))
            {
                var definition = CreateDefinition();
                catalog.Upsert(definition);
                state.SelectedProject = catalog.GetProject(definition.ModId) ?? definition;
                _validationMessage = BuildValidation(definition);
                state.StatusMessage = "Saved project " + definition.GetDisplayName();
            }

            if (state.SelectedProject != null && GUILayout.Button("Delete Selected", GUILayout.Width(120f)))
            {
                catalog.Remove(state.SelectedProject.ModId);
                state.SelectedProject = null;
                _validationMessage = string.Empty;
                ClearFields();
                state.StatusMessage = "Deleted selected project.";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Validation");
            GUILayout.TextArea(string.IsNullOrEmpty(_validationMessage) ? "Validate the project to confirm the source root, .csproj path, and build output paths." : _validationMessage, GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void ImportWorkspaceProjects(IProjectCatalog catalog, CortexShellState state)
        {
            var root = state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                state.StatusMessage = "Workspace root is not configured.";
                return;
            }

            string[] projectFiles;
            try
            {
                projectFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                state.StatusMessage = "Workspace import failed: " + ex.Message;
                return;
            }

            var imported = 0;
            for (var i = 0; i < projectFiles.Length; i++)
            {
                var projectFile = projectFiles[i];
                if (projectFile.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    projectFile.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var definition = new CortexProjectDefinition();
                definition.ProjectFilePath = projectFile;
                definition.SourceRootPath = Path.GetDirectoryName(projectFile);
                definition.ModId = Path.GetFileNameWithoutExtension(projectFile);
                definition.BuildCommandOverride = string.Empty;
                definition.OutputAssemblyPath = string.Empty;
                definition.OutputPdbPath = string.Empty;
                catalog.Upsert(definition);
                imported++;
            }

            state.StatusMessage = "Imported " + imported + " project definitions from workspace root.";
        }

        private void AutoDetectProjectFile()
        {
            if (string.IsNullOrEmpty(_sourceRoot) || !Directory.Exists(_sourceRoot))
            {
                return;
            }

            var files = CortexModuleUtil.FindFilesSafe(_sourceRoot, "*.csproj");
            if (files.Length > 0)
            {
                _projectFile = files[0];
                if (string.IsNullOrEmpty(_modId))
                {
                    _modId = Path.GetFileNameWithoutExtension(_projectFile);
                }
            }
        }

        private void ApplyDefaultsFromProjectPath()
        {
            if (!string.IsNullOrEmpty(_projectFile) && File.Exists(_projectFile))
            {
                if (string.IsNullOrEmpty(_sourceRoot))
                {
                    _sourceRoot = Path.GetDirectoryName(_projectFile);
                }
                if (string.IsNullOrEmpty(_modId))
                {
                    _modId = Path.GetFileNameWithoutExtension(_projectFile);
                }
            }

            if (string.IsNullOrEmpty(_modId) && !string.IsNullOrEmpty(_sourceRoot))
            {
                _modId = Path.GetFileName(_sourceRoot);
            }
        }

        private CortexProjectDefinition CreateDefinition()
        {
            ApplyDefaultsFromProjectPath();
            var definition = new CortexProjectDefinition();
            definition.ModId = _modId;
            definition.SourceRootPath = _sourceRoot;
            definition.ProjectFilePath = _projectFile;
            definition.BuildCommandOverride = _buildOverride;
            definition.OutputAssemblyPath = _outputAssembly;
            definition.OutputPdbPath = _outputPdb;
            return definition;
        }

        private string BuildValidation(CortexProjectDefinition definition)
        {
            if (definition == null)
            {
                return "No project selected.";
            }

            var lines = new List<string>();
            lines.Add("Mod ID: " + (string.IsNullOrEmpty(definition.ModId) ? "Missing" : definition.ModId));
            lines.Add("Source Root: " + (Directory.Exists(definition.SourceRootPath) ? "OK" : "Missing"));
            lines.Add("Project File: " + (File.Exists(definition.ProjectFilePath) ? "OK" : "Missing"));
            lines.Add(string.IsNullOrEmpty(definition.OutputAssemblyPath) ? "Output DLL: Resolved from project file." : "Output DLL: " + definition.OutputAssemblyPath);
            lines.Add(string.IsNullOrEmpty(definition.OutputPdbPath) ? "Output PDB: Resolved from project file." : "Output PDB: " + definition.OutputPdbPath);
            return string.Join("\n", lines.ToArray());
        }

        private void Load(CortexProjectDefinition project)
        {
            _modId = project.ModId ?? string.Empty;
            _sourceRoot = project.SourceRootPath ?? string.Empty;
            _projectFile = project.ProjectFilePath ?? string.Empty;
            _buildOverride = project.BuildCommandOverride ?? string.Empty;
            _outputAssembly = project.OutputAssemblyPath ?? string.Empty;
            _outputPdb = project.OutputPdbPath ?? string.Empty;
        }

        private void ClearFields()
        {
            _modId = string.Empty;
            _sourceRoot = string.Empty;
            _projectFile = string.Empty;
            _buildOverride = string.Empty;
            _outputAssembly = string.Empty;
            _outputPdb = string.Empty;
        }

        private static void DrawField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
    }

    public sealed class EditorModule
    {
        private string _openPath = string.Empty;
        private string _fileSearch = string.Empty;
        private string _cachedSourceRoot = string.Empty;
        private string _cachedDecompilerRoot = string.Empty;
        private string[] _cachedFiles = new string[0];
        private string[] _cachedDecompilerFiles = new string[0];
        private readonly Dictionary<string, bool> _folderExpanded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _tabScroll = Vector2.zero;
        private Vector2 _editorScroll = Vector2.zero;
        private Vector2 _navigatorScroll = Vector2.zero;

        public void Draw(IDocumentService documentService, CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            GUILayout.BeginVertical();
            DrawToolbar(documentService, state);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawEditorArea(documentService, state);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(280f, settings.EditorFilePaneWidth)));
            DrawNavigatorPane(documentService, state);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawToolbar(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Open", GUILayout.Width(36f));
            _openPath = GUILayout.TextField(_openPath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Open", GUILayout.Width(80f)))
            {
                if (File.Exists(_openPath))
                {
                    CortexModuleUtil.OpenDocument(documentService, state, _openPath, 0);
                    state.StatusMessage = "Opened " + _openPath;
                }
                else
                {
                    state.StatusMessage = "File not found: " + _openPath;
                }
            }
            state.EditorUnlocked = GUILayout.Toggle(state.EditorUnlocked, "Allow Editing", GUILayout.Width(110f));
            if (GUILayout.Button("Save All", GUILayout.Width(90f)))
            {
                SaveAll(documentService, state);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawNavigatorPane(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source Explorer");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                RefreshProjectFiles(state);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(34f));
            _fileSearch = GUILayout.TextField(_fileSearch, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Label("Right pane shows project files and decompiled cache files that Cortex can open.", GUI.skin.label);

            RefreshProjectFilesIfNeeded(state);
            _navigatorScroll = GUILayout.BeginScrollView(_navigatorScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawTreeGroup("Mod Source", state.SelectedProject != null ? state.SelectedProject.SourceRootPath : string.Empty, documentService, state);
            GUILayout.Space(6f);
            DrawTreeGroup("Decompiled Cache", state.Settings != null ? state.Settings.DecompilerCachePath : string.Empty, documentService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEditorArea(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (state.OpenDocuments.Count == 0 || state.ActiveDocument == null)
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
                GUILayout.Label("Open a project file or use the log/build panels to jump directly into source.");
                GUILayout.Label("Editing is intentionally locked by default so gameplay interactions do not accidentally modify files.");
                GUILayout.EndVertical();
                GUILayout.EndVertical();
                return;
            }

            DrawOpenTabs(state);

            var active = state.ActiveDocument;
            documentService.HasExternalChanges(active);

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(active.FilePath, GUILayout.ExpandWidth(true));
            if (active.HighlightedLine > 0)
            {
                GUILayout.Label("Line " + active.HighlightedLine, GUILayout.Width(72f));
            }
            if (active.HasExternalChanges)
            {
                GUILayout.Label("External change detected", GUILayout.Width(150f));
            }
            if (GUILayout.Button("Save", GUILayout.Width(70f)))
            {
                documentService.Save(active);
                state.StatusMessage = "Saved " + active.FilePath;
            }
            if (GUILayout.Button("Reload", GUILayout.Width(70f)))
            {
                documentService.Reload(active);
                state.StatusMessage = "Reloaded " + active.FilePath;
            }
            if (GUILayout.Button("Close", GUILayout.Width(70f)))
            {
                var closingPath = active.FilePath;
                CortexModuleUtil.CloseDocument(state, closingPath);
                state.StatusMessage = "Closed " + closingPath;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }
            GUILayout.EndHorizontal();

            _editorScroll = GUILayout.BeginScrollView(_editorScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.TextArea(BuildLineNumberGutter(active), GUILayout.Width(78f), GUILayout.ExpandHeight(true));
            var previousEnabled = GUI.enabled;
            GUI.enabled = state.EditorUnlocked;
            var updated = GUILayout.TextArea(active.Text ?? string.Empty, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (state.EditorUnlocked && !string.Equals(updated, active.Text, StringComparison.Ordinal))
            {
                active.Text = updated;
                active.IsDirty = true;
            }

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Editable: " + (state.EditorUnlocked ? "Yes" : "No") + " | Lines: " + CortexModuleUtil.SplitLines(active.Text).Length + " | Dirty: " + (active.IsDirty ? "Yes" : "No"));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawOpenTabs(CortexShellState state)
        {
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, false, false, GUIStyle.none, GUIStyle.none, GUI.skin.box, GUILayout.Height(52f));
            GUILayout.BeginHorizontal();
            for (var i = 0; i < state.OpenDocuments.Count; i++)
            {
                var session = state.OpenDocuments[i];
                GUILayout.BeginVertical(GUILayout.Width(170f));
                if (GUILayout.Button(CortexModuleUtil.GetDocumentDisplayName(session), GUILayout.Height(24f)))
                {
                    state.ActiveDocument = session;
                    state.ActiveDocumentPath = session.FilePath;
                }
                if (GUILayout.Button("Close", GUILayout.Height(20f)))
                {
                    CortexModuleUtil.CloseDocument(state, session.FilePath);
                    break;
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void SaveAll(IDocumentService documentService, CortexShellState state)
        {
            var saved = 0;
            for (var i = 0; i < state.OpenDocuments.Count; i++)
            {
                if (state.OpenDocuments[i].IsDirty && documentService.Save(state.OpenDocuments[i]))
                {
                    saved++;
                }
            }

            state.StatusMessage = "Saved " + saved + " open document(s).";
        }

        private void RefreshProjectFilesIfNeeded(CortexShellState state)
        {
            var sourceRoot = state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty;
            var decompilerRoot = state.Settings != null ? state.Settings.DecompilerCachePath ?? string.Empty : string.Empty;
            if (!string.Equals(_cachedSourceRoot, sourceRoot, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_cachedDecompilerRoot, decompilerRoot, StringComparison.OrdinalIgnoreCase))
            {
                RefreshProjectFiles(state);
            }
        }

        private void RefreshProjectFiles(CortexShellState state)
        {
            _cachedSourceRoot = state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty;
            _cachedDecompilerRoot = state.Settings != null ? state.Settings.DecompilerCachePath ?? string.Empty : string.Empty;
            _cachedFiles = GetProjectFiles(state.SelectedProject);
            _cachedDecompilerFiles = GetDecompiledFiles(_cachedDecompilerRoot);
        }

        private void DrawTreeGroup(string title, string rootPath, IDocumentService documentService, CortexShellState state)
        {
            CortexIdeLayout.DrawGroup(title, delegate
            {
                if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                {
                    GUILayout.Label("Path not available.");
                    return;
                }

                GUILayout.Label(rootPath, GUI.skin.label);
                DrawDirectoryNode(rootPath, rootPath, documentService, state, 0);
            });
        }

        private void DrawDirectoryNode(string rootPath, string currentPath, IDocumentService documentService, CortexShellState state, int depth)
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(currentPath);
            }
            catch
            {
                directories = new string[0];
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < directories.Length; i++)
            {
                var directory = directories[i];
                if (!DirectoryMatchesFilter(directory))
                {
                    continue;
                }

                var key = directory;
                bool expanded;
                if (!_folderExpanded.TryGetValue(key, out expanded))
                {
                    expanded = depth <= 0;
                    _folderExpanded[key] = expanded;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(depth * 14f);
                var label = (expanded ? "[-] " : "[+] ") + Path.GetFileName(directory);
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
                {
                    _folderExpanded[key] = !expanded;
                }
                GUILayout.EndHorizontal();

                if (_folderExpanded[key])
                {
                    DrawDirectoryNode(rootPath, directory, documentService, state, depth + 1);
                }
            }

            var files = GetBrowsableFiles(currentPath);
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (!MatchesFilter(file))
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(depth * 14f + 14f);
                var label = BuildRelativePath(rootPath, file);
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
                {
                    CortexModuleUtil.OpenDocument(documentService, state, file, 0);
                    _openPath = file;
                    state.StatusMessage = "Opened " + file;
                }
                GUILayout.EndHorizontal();
            }
        }

        private bool DirectoryMatchesFilter(string directoryPath)
        {
            if (string.IsNullOrEmpty(_fileSearch))
            {
                return true;
            }

            return directoryPath.IndexOf(_fileSearch, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesFilter(string path)
        {
            return string.IsNullOrEmpty(_fileSearch) || path.IndexOf(_fileSearch, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildRelativePath(string rootPath, string filePath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return filePath;
            }

            var relative = filePath.Replace(rootPath.TrimEnd('\\') + "\\", string.Empty);
            return relative.Replace("\\", " / ");
        }

        private static string BuildLineNumberGutter(DocumentSession session)
        {
            var lines = CortexModuleUtil.SplitLines(session != null ? session.Text : string.Empty);
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                builder.Append(session != null && session.HighlightedLine == lineNumber ? "> " : "  ");
                builder.Append(lineNumber.ToString("D4"));
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string[] GetProjectFiles(CortexProjectDefinition project)
        {
            if (project == null || string.IsNullOrEmpty(project.SourceRootPath) || !Directory.Exists(project.SourceRootPath))
            {
                return new string[0];
            }

            var results = new List<string>();
            CollectProjectFiles(project.SourceRootPath, results);
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results.ToArray();
        }

        private static string[] GetDecompiledFiles(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return new string[0];
            }

            var results = new List<string>();
            CollectBrowsableFiles(rootPath, results, true);
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results.ToArray();
        }

        private static string[] GetBrowsableFiles(string rootPath)
        {
            var results = new List<string>();
            CollectBrowsableFiles(rootPath, results, false);
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results.ToArray();
        }

        private static void CollectBrowsableFiles(string rootPath, List<string> results, bool decompiledOnly)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(rootPath);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var extension = Path.GetExtension(files[i]) ?? string.Empty;
                var include = string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".map", StringComparison.OrdinalIgnoreCase);
                if (!include)
                {
                    continue;
                }

                if (decompiledOnly && !string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".map", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(files[i]);
            }
        }

        private static void CollectProjectFiles(string root, List<string> results)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(root);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var extension = Path.GetExtension(files[i]) ?? string.Empty;
                if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(files[i]);
                }
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(root);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < directories.Length; i++)
            {
                var name = Path.GetFileName(directories[i]) ?? string.Empty;
                if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CollectProjectFiles(directories[i], results);
            }
        }
    }

    public sealed class BuildModule
    {
        private Vector2 _scroll = Vector2.zero;
        private string _configuration = string.Empty;

        public void Draw(IBuildCommandResolver resolver, IBuildExecutor executor, IRestartCoordinator restartCoordinator, IDocumentService documentService, CortexShellState state)
        {
            if (state.SelectedProject == null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Select a project first. The build view uses the selected project definition and verifies the output assembly before allowing restart.");
                GUILayout.EndVertical();
                return;
            }

            if (string.IsNullOrEmpty(_configuration))
            {
                _configuration = state.Settings != null ? state.Settings.DefaultBuildConfiguration : "Debug";
            }

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Configuration", GUILayout.Width(80f));
            _configuration = GUILayout.TextField(_configuration, GUILayout.Width(120f));
            if (GUILayout.Button("Build", GUILayout.Width(90f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, false, false, _configuration);
            }
            if (GUILayout.Button("Clean & Build", GUILayout.Width(120f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, true, false, _configuration);
            }
            if (GUILayout.Button("Verify & Restart", GUILayout.Width(130f)))
            {
                ExecuteBuild(resolver, executor, restartCoordinator, state, false, true, _configuration);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Timeout: " + (state.Settings != null ? state.Settings.BuildTimeoutMs / 1000 : 300) + "s");
            GUILayout.EndHorizontal();

            var result = state.LastBuildResult;
            if (result == null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("No build has been run yet.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Last build: " + (result.Success ? "Success" : "Failure") +
                " | ExitCode=" + result.ExitCode +
                " | Duration=" + result.Duration.TotalSeconds.ToString("F2") + "s" +
                " | TimedOut=" + (result.TimedOut ? "Yes" : "No") +
                " | OutputUpdated=" + (result.OutputAssemblyUpdated ? "Yes" : "No"));
            GUILayout.EndVertical();

            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            if (result.Diagnostics.Count > 0)
            {
                GUILayout.Label("Diagnostics");
                for (var i = 0; i < result.Diagnostics.Count; i++)
                {
                    var item = result.Diagnostics[i];
                    var label = item.Severity + ": " + item.FilePath + "(" + item.Line + "," + item.Column + ") " + item.Code + " " + item.Message;
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
                    {
                        var path = CortexModuleUtil.ResolveCandidatePath(state.SelectedProject, state.Settings, item.FilePath);
                        if (!string.IsNullOrEmpty(path))
                        {
                            CortexModuleUtil.OpenDocument(documentService, state, path, item.Line);
                            state.RequestedTabIndex = 2;
                            state.StatusMessage = "Opened " + path + " from build diagnostics.";
                        }
                    }
                }
                GUILayout.Space(8f);
            }

            GUILayout.Label("Output");
            for (var i = 0; i < result.OutputLines.Count; i++)
            {
                var line = result.OutputLines[i] ?? string.Empty;
                if (GUILayout.Button(line, GUILayout.ExpandWidth(true)))
                {
                    string filePath;
                    int lineNumber;
                    if (CortexModuleUtil.TryResolveSourceLocation(line, state.SelectedProject, state.Settings, out filePath, out lineNumber))
                    {
                        CortexModuleUtil.OpenDocument(documentService, state, filePath, lineNumber);
                        state.RequestedTabIndex = 2;
                        state.StatusMessage = "Opened " + filePath + " from build output.";
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void ExecuteBuild(IBuildCommandResolver resolver, IBuildExecutor executor, IRestartCoordinator restartCoordinator, CortexShellState state, bool clean, bool restartAfter, string configuration)
        {
            var command = resolver.Resolve(state.SelectedProject, clean, configuration);
            if (command == null)
            {
                state.StatusMessage = "No build command could be resolved.";
                return;
            }

            command.TimeoutMs = state.Settings != null ? state.Settings.BuildTimeoutMs : 300000;
            state.LastBuildResult = executor.Execute(command);

            if (!state.LastBuildResult.Success)
            {
                state.StatusMessage = state.LastBuildResult.TimedOut ? "Build timed out." : "Build failed.";
                return;
            }

            state.StatusMessage = "Build succeeded.";
            if (!restartAfter)
            {
                return;
            }

            string errorMessage;
            if (restartCoordinator.RequestCurrentSessionRestart(out errorMessage))
            {
                state.StatusMessage = "Build verified. Restart requested.";
            }
            else
            {
                state.StatusMessage = "Restart failed: " + errorMessage;
            }
        }
    }

    public sealed class ReferenceModule
    {
        private sealed class MethodItem
        {
            public string Label;
            public MethodBase Method;
        }

        private string _assemblyFilter = string.Empty;
        private string _typeName = string.Empty;
        private string _methodName = string.Empty;
        private bool _ignoreCache;
        private Vector2 _assemblyScroll = Vector2.zero;
        private Vector2 _typeScroll = Vector2.zero;
        private Vector2 _methodScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;
        private readonly List<Assembly> _assemblies = new List<Assembly>();
        private readonly List<Type> _types = new List<Type>();
        private readonly List<MethodItem> _methods = new List<MethodItem>();
        private string _selectedAssemblyPath = string.Empty;
        private string _selectedTypeName = string.Empty;
        private readonly GUIStyle _pathStyle = new GUIStyle();
        private readonly GUIStyle _xmlDocStyle = new GUIStyle();

        public ReferenceModule()
        {
            _pathStyle.wordWrap = true;
            _pathStyle.normal.textColor = new Color(0.82f, 0.84f, 0.9f, 1f);
            _xmlDocStyle.wordWrap = true;
            _xmlDocStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f, 1f);
        }

        public void Draw(ISourceReferenceService sourceReferenceService, IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical();
            EnsureAssembliesLoaded(state);
            CortexIdeLayout.DrawTwoPane(
                420f,
                340f,
                delegate { DrawBrowserPane(sourceReferenceService, state); },
                delegate { DrawPreviewPane(documentService, state); });
            GUILayout.EndVertical();
        }

        private void DrawBrowserPane(ISourceReferenceService sourceReferenceService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assembly", GUILayout.Width(58f));
            _assemblyFilter = GUILayout.TextField(_assemblyFilter, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                ReloadAssemblies(state);
            }
            GUILayout.EndHorizontal();
            _assemblyScroll = GUILayout.BeginScrollView(_assemblyScroll, GUI.skin.box, GUILayout.Height(160f));
            for (var i = 0; i < _assemblies.Count; i++)
            {
                var assembly = _assemblies[i];
                var path = SafeAssemblyPath(assembly);
                var label = assembly.GetName().Name + "\n" + path;
                if (!MatchesText(label, _assemblyFilter))
                {
                    continue;
                }

                if (GUILayout.Button(label, GUILayout.MinHeight(38f)))
                {
                    _selectedAssemblyPath = path;
                    LoadTypes(assembly);
                    state.StatusMessage = "Selected assembly " + assembly.GetName().Name;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(58f));
            _typeName = GUILayout.TextField(_typeName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            _typeScroll = GUILayout.BeginScrollView(_typeScroll, GUI.skin.box, GUILayout.Height(180f));
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (!MatchesText(type.FullName ?? type.Name, _typeName))
                {
                    continue;
                }

                if (GUILayout.Button(type.FullName ?? type.Name, GUILayout.ExpandWidth(true)))
                {
                    _selectedTypeName = type.FullName ?? type.Name;
                    LoadMethods(type);
                    state.StatusMessage = "Selected type " + _selectedTypeName;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Method", GUILayout.Width(58f));
            _methodName = GUILayout.TextField(_methodName, GUILayout.ExpandWidth(true));
            _ignoreCache = GUILayout.Toggle(_ignoreCache, "Ignore Cache", GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_selectedTypeName) && GUILayout.Button("Decompile Full Type", GUILayout.Width(160f)))
            {
                var type = FindSelectedType();
                if (type != null)
                {
                    DecompileType(sourceReferenceService, state, type);
                }
            }
            _methodScroll = GUILayout.BeginScrollView(_methodScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (var i = 0; i < _methods.Count; i++)
            {
                var item = _methods[i];
                if (!MatchesText(item.Label, _methodName))
                {
                    continue;
                }

                if (GUILayout.Button(item.Label, GUILayout.ExpandWidth(true)))
                {
                    DecompileMethod(sourceReferenceService, state, item.Method);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawPreviewPane(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Decompiler Preview");
            if (state.LastReferenceResult == null)
            {
                GUILayout.Label("Browse a loaded assembly, type, and method to generate decompiled source for game or mod code.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(state.LastReferenceResult.StatusMessage ?? string.Empty);
            GUILayout.Label("Assembly:", GUILayout.Width(70f));
            GUILayout.Label(_selectedAssemblyPath, _pathStyle);
            GUILayout.Label("Type:", GUILayout.Width(70f));
            GUILayout.Label(_selectedTypeName, _pathStyle);
            if (!string.IsNullOrEmpty(state.LastReferenceResult.ResolvedMemberDisplayName))
            {
                GUILayout.Label("Member:", GUILayout.Width(70f));
                GUILayout.Label(state.LastReferenceResult.ResolvedMemberDisplayName, _pathStyle);
            }
            if (!string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationPath))
            {
                GUILayout.Label("XML:", GUILayout.Width(70f));
                GUILayout.Label(state.LastReferenceResult.XmlDocumentationPath, _pathStyle);
            }
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(state.LastReferenceResult.CachePath) && File.Exists(state.LastReferenceResult.CachePath) &&
                GUILayout.Button("Open Cached Source", GUILayout.Width(150f)))
            {
                CortexModuleUtil.OpenDocument(documentService, state, state.LastReferenceResult.CachePath, 1);
                state.RequestedTabIndex = 2;
                state.StatusMessage = "Opened decompiled cache file.";
                MMLog.WriteInfo("Cortex opened decompiled cache file " + state.LastReferenceResult.CachePath, MMLog.LogCategory.UI);
            }
            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
            {
                state.LastReferenceResult = null;
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationText))
            {
                CortexIdeLayout.DrawGroup("XML Documentation", delegate
                {
                    GUILayout.Label(state.LastReferenceResult.XmlDocumentationText, _xmlDocStyle);
                });
            }
            _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.TextArea(state.LastReferenceResult.SourceText ?? string.Empty, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void EnsureAssembliesLoaded(CortexShellState state)
        {
            if (_assemblies.Count == 0)
            {
                ReloadAssemblies(state);
            }
        }

        private void ReloadAssemblies(CortexShellState state)
        {
            _assemblies.Clear();
            _types.Clear();
            _methods.Clear();
            _selectedAssemblyPath = string.Empty;
            _selectedTypeName = string.Empty;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var managedRoot = state.Settings != null ? state.Settings.ManagedAssemblyRootPath : string.Empty;
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var path = SafeAssemblyPath(assembly);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(managedRoot) &&
                    path.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _assemblies.Insert(0, assembly);
                    continue;
                }

                _assemblies.Add(assembly);
            }

            _assemblies.Sort(delegate(Assembly left, Assembly right)
            {
                return string.Compare(left.GetName().Name, right.GetName().Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void LoadTypes(Assembly assembly)
        {
            _types.Clear();
            _methods.Clear();
            _selectedTypeName = string.Empty;
            if (assembly == null)
            {
                return;
            }

            try
            {
                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    if (!types[i].IsNested)
                    {
                        _types.Add(types[i]);
                    }
                }
            }
            catch
            {
            }
        }

        private void LoadMethods(Type type)
        {
            _methods.Clear();
            if (type == null)
            {
                return;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (var i = 0; i < methods.Length; i++)
            {
                _methods.Add(new MethodItem
                {
                    Method = methods[i],
                    Label = BuildMethodLabel(methods[i])
                });
            }
        }

        private void DecompileMethod(ISourceReferenceService sourceReferenceService, CortexShellState state, MethodBase method)
        {
            if (method == null)
            {
                return;
            }

            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = method.Module.Assembly.Location,
                MetadataToken = method.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Method
            });
            state.StatusMessage = "Decompiled " + method.DeclaringType.FullName + "." + method.Name;
            MMLog.WriteInfo("Cortex decompiled " + method.DeclaringType.FullName + "." + method.Name, MMLog.LogCategory.UI);
        }

        private void DecompileType(ISourceReferenceService sourceReferenceService, CortexShellState state, Type type)
        {
            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = type.Assembly.Location,
                MetadataToken = type.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Type
            });
            state.StatusMessage = "Decompiled type " + (type.FullName ?? type.Name);
            MMLog.WriteInfo("Cortex decompiled full type " + (type.FullName ?? type.Name), MMLog.LogCategory.UI);
        }

        private Type FindSelectedType()
        {
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (string.Equals(type.FullName ?? type.Name, _selectedTypeName, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }

        private static bool MatchesText(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeAssemblyPath(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildMethodLabel(MethodBase method)
        {
            if (method == null)
            {
                return "Unknown";
            }

            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append(method.Name);
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[i].ParameterType.Name);
                builder.Append(' ');
                builder.Append(parameters[i].Name);
            }
            builder.Append(')');
            return builder.ToString();
        }
    }

    public sealed class RuntimeToolsModule
    {
        public void Draw(IRuntimeToolBridge toolBridge)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Legacy runtime tools remain available while Cortex absorbs the IDE workflow.");
            if (GUILayout.Button("Toggle Runtime Inspector (F9)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleRuntimeInspector();
            }
            if (GUILayout.Button("Toggle Runtime IL Inspector (F10)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleIlInspector();
            }
            if (GUILayout.Button("Toggle UI Debugger (F11)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleUiDebugger();
            }
            if (GUILayout.Button("Toggle Runtime Debugger (F7)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleRuntimeDebugger();
            }
            GUILayout.EndVertical();
        }
    }

    public sealed class SettingsModule
    {
        private bool _loaded;
        private string _workspaceRootPath = string.Empty;
        private string _modsRootPath = string.Empty;
        private string _managedAssemblyRootPath = string.Empty;
        private string _additionalSourceRoots = string.Empty;
        private string _logFilePath = string.Empty;
        private string _projectCatalogPath = string.Empty;
        private string _decompilerPath = string.Empty;
        private string _decompilerCachePath = string.Empty;
        private string _defaultBuildConfiguration = string.Empty;
        private string _buildTimeoutMs = string.Empty;
        private string _maxRecentLogs = string.Empty;
        private string _logsPaneWidth = string.Empty;
        private string _projectsPaneWidth = string.Empty;
        private string _editorFilePaneWidth = string.Empty;
        private string _windowX = string.Empty;
        private string _windowY = string.Empty;
        private string _windowWidth = string.Empty;
        private string _windowHeight = string.Empty;
        private bool _autoScrollLogs;
        private bool _showLogBacklog;

        public void Draw(ICortexSettingsStore settingsStore, CortexShellState state)
        {
            EnsureLoaded(state);

            GUILayout.BeginVertical();
            CortexIdeLayout.DrawTwoPane(
                520f,
                420f,
                delegate
                {
                    CortexIdeLayout.DrawGroup("Paths and Navigation", delegate
                    {
                        GUILayout.Label("Configure where Cortex looks for mod workspaces, game assemblies, and extra source roots.");
                        DrawField("Workspace Root", ref _workspaceRootPath);
                        DrawField("Mods Root", ref _modsRootPath);
                        DrawField("Managed Assemblies", ref _managedAssemblyRootPath);
                        DrawField("Extra Source Roots", ref _additionalSourceRoots);
                        DrawField("Log File", ref _logFilePath);
                        DrawField("Project Catalog", ref _projectCatalogPath);
                        DrawField("Decompiler Override", ref _decompilerPath);
                        DrawField("Decompiler Cache", ref _decompilerCachePath);
                    });
                },
                delegate
                {
                    CortexIdeLayout.DrawGroup("Behavior and Layout", delegate
                    {
                        DrawField("Default Config", ref _defaultBuildConfiguration);
                        DrawField("Build Timeout (ms)", ref _buildTimeoutMs);
                        DrawField("Max Recent Logs", ref _maxRecentLogs);
                        _autoScrollLogs = GUILayout.Toggle(_autoScrollLogs, "Auto-scroll log list");
                        _showLogBacklog = GUILayout.Toggle(_showLogBacklog, "Show file backlog under live logs");
                        GUILayout.Space(6f);
                        DrawField("Logs Pane Width", ref _logsPaneWidth);
                        DrawField("Projects Pane Width", ref _projectsPaneWidth);
                        DrawField("Explorer Width", ref _editorFilePaneWidth);
                        DrawField("Window X", ref _windowX);
                        DrawField("Window Y", ref _windowY);
                        DrawField("Window Width", ref _windowWidth);
                        DrawField("Window Height", ref _windowHeight);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Save Settings", GUILayout.Width(120f)))
                        {
                            Apply(state);
                            settingsStore.Save(state.Settings);
                            state.ReloadSettingsRequested = true;
                            _loaded = false;
                            state.StatusMessage = "Saved Cortex settings.";
                        }
                        if (GUILayout.Button("Reset Defaults", GUILayout.Width(120f)))
                        {
                            state.Settings = new CortexSettings();
                            _loaded = false;
                            EnsureLoaded(state);
                            state.StatusMessage = "Reset settings fields to defaults.";
                        }
                        if (GUILayout.Button("Show Logs Window", GUILayout.Width(140f)))
                        {
                            state.ShowDetachedLogWindow = true;
                        }
                        GUILayout.EndHorizontal();
                    });
                });
            GUILayout.EndVertical();
        }

        private void EnsureLoaded(CortexShellState state)
        {
            if (_loaded)
            {
                return;
            }

            var settings = state.Settings ?? new CortexSettings();
            _workspaceRootPath = settings.WorkspaceRootPath ?? string.Empty;
            _modsRootPath = settings.ModsRootPath ?? string.Empty;
            _managedAssemblyRootPath = settings.ManagedAssemblyRootPath ?? string.Empty;
            _additionalSourceRoots = settings.AdditionalSourceRoots ?? string.Empty;
            _logFilePath = settings.LogFilePath ?? string.Empty;
            _projectCatalogPath = settings.ProjectCatalogPath ?? string.Empty;
            _decompilerPath = settings.DecompilerPathOverride ?? string.Empty;
            _decompilerCachePath = settings.DecompilerCachePath ?? string.Empty;
            _defaultBuildConfiguration = settings.DefaultBuildConfiguration ?? "Debug";
            _buildTimeoutMs = settings.BuildTimeoutMs.ToString();
            _maxRecentLogs = settings.MaxRecentLogs.ToString();
            _logsPaneWidth = settings.LogsPaneWidth.ToString("F0");
            _projectsPaneWidth = settings.ProjectsPaneWidth.ToString("F0");
            _editorFilePaneWidth = settings.EditorFilePaneWidth.ToString("F0");
            _windowX = settings.WindowX.ToString("F0");
            _windowY = settings.WindowY.ToString("F0");
            _windowWidth = settings.WindowWidth.ToString("F0");
            _windowHeight = settings.WindowHeight.ToString("F0");
            _autoScrollLogs = settings.AutoScrollLogs;
            _showLogBacklog = settings.ShowLogBacklog;
            _loaded = true;
        }

        private void Apply(CortexShellState state)
        {
            if (state.Settings == null)
            {
                state.Settings = new CortexSettings();
            }

            state.Settings.WorkspaceRootPath = _workspaceRootPath;
            state.Settings.ModsRootPath = _modsRootPath;
            state.Settings.ManagedAssemblyRootPath = _managedAssemblyRootPath;
            state.Settings.AdditionalSourceRoots = _additionalSourceRoots;
            state.Settings.LogFilePath = _logFilePath;
            state.Settings.ProjectCatalogPath = _projectCatalogPath;
            state.Settings.DecompilerPathOverride = _decompilerPath;
            state.Settings.DecompilerCachePath = _decompilerCachePath;
            state.Settings.DefaultBuildConfiguration = string.IsNullOrEmpty(_defaultBuildConfiguration) ? "Debug" : _defaultBuildConfiguration;
            state.Settings.BuildTimeoutMs = ParseInt(_buildTimeoutMs, 300000);
            state.Settings.MaxRecentLogs = ParseInt(_maxRecentLogs, 300);
            state.Settings.LogsPaneWidth = ParseFloat(_logsPaneWidth, state.Settings.LogsPaneWidth);
            state.Settings.ProjectsPaneWidth = ParseFloat(_projectsPaneWidth, state.Settings.ProjectsPaneWidth);
            state.Settings.EditorFilePaneWidth = ParseFloat(_editorFilePaneWidth, state.Settings.EditorFilePaneWidth);
            state.Settings.WindowX = ParseFloat(_windowX, state.Settings.WindowX);
            state.Settings.WindowY = ParseFloat(_windowY, state.Settings.WindowY);
            state.Settings.WindowWidth = ParseFloat(_windowWidth, state.Settings.WindowWidth);
            state.Settings.WindowHeight = ParseFloat(_windowHeight, state.Settings.WindowHeight);
            state.Settings.AutoScrollLogs = _autoScrollLogs;
            state.Settings.ShowLogBacklog = _showLogBacklog;
        }

        private static void DrawField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            float value;
            return float.TryParse(raw, out value) ? value : fallback;
        }
    }
}

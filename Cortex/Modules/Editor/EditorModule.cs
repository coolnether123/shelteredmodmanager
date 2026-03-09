using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Modules.Editor
{
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
            state.Documents.EditorUnlocked = GUILayout.Toggle(state.Documents.EditorUnlocked, "Allow Editing", GUILayout.Width(110f));
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
            GUILayout.Label("Solution Explorer");
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
            GUILayout.Label("Browse configured mod source and generated decompiled files. Use Projects to map a loaded mod to its source root.", GUI.skin.label);

            RefreshProjectFilesIfNeeded(state);
            _navigatorScroll = GUILayout.BeginScrollView(_navigatorScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawSourceStatus(state);
            GUILayout.Space(6f);
            DrawTreeGroup("Mod Source", state.SelectedProject != null ? state.SelectedProject.SourceRootPath : string.Empty, documentService, state);
            GUILayout.Space(6f);
            DrawTreeGroup("Decompiled Cache", state.Settings != null ? state.Settings.DecompilerCachePath : string.Empty, documentService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEditorArea(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (state.Documents.OpenDocuments.Count == 0 || state.Documents.ActiveDocument == null)
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
                GUILayout.Label("Open a project file or use the log/build panels to jump directly into source.");
                GUILayout.Label("Editing is intentionally locked by default so gameplay interactions do not accidentally modify files.");
                GUILayout.EndVertical();
                GUILayout.EndVertical();
                return;
            }

            DrawOpenTabs(state);

            var active = state.Documents.ActiveDocument;
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
            GUI.enabled = state.Documents.EditorUnlocked;
            var updated = GUILayout.TextArea(active.Text ?? string.Empty, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (state.Documents.EditorUnlocked && !string.Equals(updated, active.Text, StringComparison.Ordinal))
            {
                active.Text = updated;
                active.IsDirty = true;
            }

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Editable: " + (state.Documents.EditorUnlocked ? "Yes" : "No") + " | Lines: " + CortexModuleUtil.SplitLines(active.Text).Length + " | Dirty: " + (active.IsDirty ? "Yes" : "No"));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawOpenTabs(CortexShellState state)
        {
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, false, false, GUIStyle.none, GUIStyle.none, GUI.skin.box, GUILayout.Height(52f));
            GUILayout.BeginHorizontal();
            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                var session = state.Documents.OpenDocuments[i];
                GUILayout.BeginVertical(GUILayout.Width(170f));
                if (GUILayout.Button(CortexModuleUtil.GetDocumentDisplayName(session), GUILayout.Height(24f)))
                {
                    state.Documents.ActiveDocument = session;
                    state.Documents.ActiveDocumentPath = session.FilePath;
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
            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                if (state.Documents.OpenDocuments[i].IsDirty && documentService.Save(state.Documents.OpenDocuments[i]))
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

        private void DrawSourceStatus(CortexShellState state)
        {
            var project = state.SelectedProject;
            var hasConfiguredSource = project != null &&
                !string.IsNullOrEmpty(project.SourceRootPath) &&
                Directory.Exists(project.SourceRootPath);
            var loadedMod = ResolveLoadedMod(project);

            CortexIdeLayout.DrawGroup("Source Mapping", delegate
            {
                if (project == null)
                {
                    GUILayout.Label("No Cortex project is selected. Open Projects and choose or create a mapping for the loaded mod you want to edit.");
                    if (GUILayout.Button("Open Projects Setup", GUILayout.Width(160f)))
                    {
                        state.Workbench.RequestedContainerId = CortexWorkbenchIds.ProjectsContainer;
                    }
                    return;
                }

                GUILayout.Label("Project: " + project.GetDisplayName());
                if (hasConfiguredSource)
                {
                    GUILayout.Label("Configured source root: " + project.SourceRootPath);
                    return;
                }

                GUILayout.Label("This project does not have a valid source root configured.");
                if (loadedMod != null)
                {
                    GUILayout.Label("Loaded mod detected: " + loadedMod.Id + " @ " + loadedMod.RootPath);
                }
                else
                {
                    GUILayout.Label("No matching loaded mod folder was found for this project ID.");
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Projects Setup", GUILayout.Width(160f)))
                {
                    state.Workbench.RequestedContainerId = CortexWorkbenchIds.ProjectsContainer;
                }
                if (GUILayout.Button("Open Settings", GUILayout.Width(120f)))
                {
                    state.Workbench.RequestedContainerId = CortexWorkbenchIds.SettingsContainer;
                }
                GUILayout.EndHorizontal();
            });
        }

        private static ModEntry ResolveLoadedMod(CortexProjectDefinition project)
        {
            if (project == null || string.IsNullOrEmpty(project.ModId))
            {
                return null;
            }

            return ModRegistry.GetMod(project.ModId);
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
}

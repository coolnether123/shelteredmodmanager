using System;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    public sealed class EditorModule
    {
        private string _openPath = string.Empty;
        private string _fileSearch = string.Empty;
        private string _cachedSourceRoot = string.Empty;
        private string _cachedDecompilerRoot = string.Empty;
        private WorkspaceTreeNode _sourceTree;
        private WorkspaceTreeNode _decompiledTree;
        private Vector2 _tabScroll = Vector2.zero;
        private Vector2 _editorScroll = Vector2.zero;
        private Vector2 _navigatorScroll = Vector2.zero;
        private Vector2 _diagnosticScroll = Vector2.zero;

        public void Draw(IDocumentService documentService, IWorkspaceBrowserService browserService, IProjectWorkspaceService workspaceService, ILoadedModCatalog loadedModCatalog, CortexShellState state)
        {
            var settings = state.Settings ?? new CortexSettings();
            GUILayout.BeginVertical();
            DrawToolbar(documentService, workspaceService, state);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawEditorArea(documentService, state);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(280f, settings.EditorFilePaneWidth)));
            DrawNavigatorPane(documentService, browserService, loadedModCatalog, state);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawToolbar(IDocumentService documentService, IProjectWorkspaceService workspaceService, CortexShellState state)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Open File", GUILayout.Width(58f));
            _openPath = GUILayout.TextField(_openPath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Open", GUILayout.Width(80f)))
            {
                if (System.IO.File.Exists(_openPath))
                {
                    CortexModuleUtil.OpenDocument(documentService, state, _openPath, 0);
                    state.StatusMessage = "Opened " + _openPath;
                }
                else if (System.IO.Directory.Exists(_openPath))
                {
                    ApplySourceFolder(workspaceService, state, _openPath);
                }
                else
                {
                    state.StatusMessage = "File or source folder not found: " + _openPath;
                    state.Diagnostics.Add(state.StatusMessage);
                }
            }
            state.Documents.EditorUnlocked = GUILayout.Toggle(state.Documents.EditorUnlocked, "Allow Editing", GUILayout.Width(110f));
            if (GUILayout.Button("Save All", GUILayout.Width(90f)))
            {
                SaveAll(documentService, state);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawNavigatorPane(IDocumentService documentService, IWorkspaceBrowserService browserService, ILoadedModCatalog loadedModCatalog, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Solution Explorer");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                RefreshProjectFiles(browserService, state);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Source Root", GUILayout.Width(74f));
            GUILayout.TextField(state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Projects", GUILayout.Width(90f)))
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.ProjectsContainer;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(34f));
            _fileSearch = GUILayout.TextField(_fileSearch, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Label("Browse the selected mod source tree and generated decompiled files. Paste a source folder into 'Open File' to auto-map it.", GUI.skin.label);

            RefreshProjectFilesIfNeeded(browserService, state);
            _navigatorScroll = GUILayout.BeginScrollView(_navigatorScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawSourceStatus(loadedModCatalog, state);
            GUILayout.Space(6f);
            DrawWorkflowDiagnostics(state);
            GUILayout.Space(6f);
            DrawTreeGroup("Mod Source", _sourceTree, documentService, state);
            GUILayout.Space(6f);
            DrawTreeGroup("Decompiled Cache", _decompiledTree, documentService, state);
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

        private void RefreshProjectFilesIfNeeded(IWorkspaceBrowserService browserService, CortexShellState state)
        {
            var sourceRoot = state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty;
            var decompilerRoot = state.Settings != null ? state.Settings.DecompilerCachePath ?? string.Empty : string.Empty;
            if (!string.Equals(_cachedSourceRoot, sourceRoot, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_cachedDecompilerRoot, decompilerRoot, StringComparison.OrdinalIgnoreCase))
            {
                RefreshProjectFiles(browserService, state);
            }
        }

        private void RefreshProjectFiles(IWorkspaceBrowserService browserService, CortexShellState state)
        {
            _cachedSourceRoot = state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty;
            _cachedDecompilerRoot = state.Settings != null ? state.Settings.DecompilerCachePath ?? string.Empty : string.Empty;
            _sourceTree = browserService != null ? browserService.BuildTree(_cachedSourceRoot, WorkspaceTreeKind.ProjectSource) : null;
            _decompiledTree = browserService != null ? browserService.BuildTree(_cachedDecompilerRoot, WorkspaceTreeKind.DecompiledCache) : null;
        }

        private void DrawTreeGroup(string title, WorkspaceTreeNode rootNode, IDocumentService documentService, CortexShellState state)
        {
            CortexIdeLayout.DrawGroup(title, delegate
            {
                if (rootNode == null)
                {
                    GUILayout.Label("Path not available.");
                    return;
                }

                GUILayout.Label(rootNode.FullPath, GUI.skin.label);
                DrawTreeNode(rootNode, documentService, state, 0);
            });
        }

        private void DrawSourceStatus(ILoadedModCatalog loadedModCatalog, CortexShellState state)
        {
            var project = state.SelectedProject;
            var hasConfiguredSource = project != null &&
                !string.IsNullOrEmpty(project.SourceRootPath) &&
                System.IO.Directory.Exists(project.SourceRootPath);
            var loadedMod = ResolveLoadedMod(loadedModCatalog, project);

            CortexIdeLayout.DrawGroup("Source Mapping", delegate
            {
                if (project == null)
                {
                    GUILayout.Label("No source folder is mapped yet. Open Projects and paste your mod's project-tree root, or paste that folder into 'Open File' above.");
                    if (GUILayout.Button("Open Projects Setup", GUILayout.Width(160f)))
                    {
                        state.Workbench.RequestedContainerId = CortexWorkbenchIds.ProjectsContainer;
                    }
                    return;
                }

                GUILayout.Label("Project: " + project.GetDisplayName());
                if (hasConfiguredSource)
                {
                    GUILayout.Label("Mapped source folder: " + project.SourceRootPath);
                    return;
                }

                GUILayout.Label("This project does not have a valid source root configured.");
                if (loadedMod != null)
                {
                    GUILayout.Label("Loaded mod detected: " + loadedMod.ModId + " @ " + loadedMod.RootPath);
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

        private static void ApplySourceFolder(IProjectWorkspaceService workspaceService, CortexShellState state, string sourceFolder)
        {
            if (state == null || string.IsNullOrEmpty(sourceFolder))
            {
                return;
            }

            var analysis = workspaceService != null ? workspaceService.AnalyzeSourceRoot(sourceFolder, state.SelectedProject != null ? state.SelectedProject.ModId : string.Empty) : null;
            if (analysis == null || !analysis.Success || analysis.Definition == null)
            {
                state.StatusMessage = analysis != null ? analysis.StatusMessage : "Source mapping analysis failed.";
                if (analysis != null)
                {
                    for (var i = 0; i < analysis.Diagnostics.Count; i++)
                    {
                        state.Diagnostics.Add(analysis.Diagnostics[i]);
                    }
                }
                return;
            }

            state.SelectedProject = analysis.Definition;
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            state.StatusMessage = "Mapped source folder: " + analysis.Definition.SourceRootPath;
            for (var i = 0; i < analysis.Diagnostics.Count; i++)
            {
                state.Diagnostics.Add(analysis.Diagnostics[i]);
            }
        }

        private void DrawWorkflowDiagnostics(CortexShellState state)
        {
            CortexIdeLayout.DrawGroup("Workflow Diagnostics", delegate
            {
                if (state == null || state.Diagnostics.Entries.Count == 0)
                {
                    GUILayout.Label("No Cortex workflow diagnostics yet.");
                    return;
                }

                _diagnosticScroll = GUILayout.BeginScrollView(_diagnosticScroll, GUI.skin.box, GUILayout.Height(108f));
                for (var i = state.Diagnostics.Entries.Count - 1; i >= 0; i--)
                {
                    GUILayout.Label(state.Diagnostics.Entries[i]);
                }
                GUILayout.EndScrollView();
            });
        }

        private static LoadedModInfo ResolveLoadedMod(ILoadedModCatalog loadedModCatalog, CortexProjectDefinition project)
        {
            if (loadedModCatalog == null || project == null || string.IsNullOrEmpty(project.ModId))
            {
                return null;
            }

            return loadedModCatalog.GetMod(project.ModId);
        }

        private void DrawTreeNode(WorkspaceTreeNode node, IDocumentService documentService, CortexShellState state, int depth)
        {
            if (node == null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.IsDirectory)
                {
                    if (!MatchesNode(child))
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(depth * 14f);
                    GUILayout.Label(child.Name, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();
                    DrawTreeNode(child, documentService, state, depth + 1);
                    continue;
                }

                if (!MatchesNode(child))
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(depth * 14f + 14f);
                if (GUILayout.Button(child.RelativePath.Replace("\\", " / "), GUILayout.ExpandWidth(true)))
                {
                    CortexModuleUtil.OpenDocument(documentService, state, child.FullPath, 0);
                    _openPath = child.FullPath;
                    state.StatusMessage = "Opened " + child.FullPath;
                }
                GUILayout.EndHorizontal();
            }
        }

        private bool MatchesNode(WorkspaceTreeNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(_fileSearch))
            {
                return true;
            }

            if ((node.Name ?? string.Empty).IndexOf(_fileSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (node.RelativePath ?? string.Empty).IndexOf(_fileSearch, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!node.IsDirectory)
            {
                return false;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (MatchesNode(node.Children[i]))
                {
                    return true;
                }
            }

            return false;
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
    }
}

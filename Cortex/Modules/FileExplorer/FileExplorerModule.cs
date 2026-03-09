using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.FileExplorer
{
    /// <summary>
    /// Standalone file-hierarchy explorer module, analogous to Visual Studio's Solution Explorer.
    /// Displays the active project's source tree and the decompiled cache tree in a compact,
    /// VS-style hierarchy. Selecting a file opens it in the code editor. Decoupled entirely from
    /// <see cref="Editor.EditorModule"/> so each honours the Single Responsibility Principle.
    /// </summary>
    public sealed class FileExplorerModule
    {
        // ── layout state ──────────────────────────────────────────────────────────────
        private Vector2 _scroll = Vector2.zero;
        private string _filterText = string.Empty;
        private string _cachedSourceRoot = string.Empty;
        private string _cachedDecompilerRoot = string.Empty;
        private readonly Dictionary<string, bool> _expandedNodes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // ── styles (created lazily once, reset on theme change) ───────────────────────
        private string _appliedTheme = string.Empty;
        private GUIStyle _folderLabelStyle;
        private GUIStyle _fileLabelStyle;
        private GUIStyle _fileButtonStyle;
        private GUIStyle _activeFileButtonStyle;
        private GUIStyle _filterBoxStyle;
        private GUIStyle _sectionHeaderStyle;
        private Texture2D _selectedFileBg;
        private Texture2D _fileHoverBg;
        private Texture2D _filterBg;
        private Texture2D _sectionHeaderBg;

        // ── tree data ─────────────────────────────────────────────────────────────────
        private WorkspaceTreeNode _sourceTree;
        private WorkspaceTreeNode _decompiledTree;

        // ── UI constants ─────────────────────────────────────────────────────────────
        private const float IndentWidth = 16f;
        private const float RowHeight = 20f;
        private const float FolderIconWidth = 14f;

        public void Draw(
            IDocumentService documentService,
            IWorkspaceBrowserService browserService,
            CortexShellState state)
        {
            EnsureStyles(state);
            RefreshIfNeeded(browserService, state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            DrawFilterBar();
            GUILayout.Space(2f);
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.ExpandHeight(true));
            DrawSection("Source", _sourceTree, documentService, state);
            GUILayout.Space(6f);
            DrawSection("Decompiled Cache", _decompiledTree, documentService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // ── Refresh ───────────────────────────────────────────────────────────────────

        private void RefreshIfNeeded(IWorkspaceBrowserService browserService, CortexShellState state)
        {
            var sourceRoot = state.SelectedProject != null
                ? state.SelectedProject.SourceRootPath ?? string.Empty
                : string.Empty;
            var decompilerRoot = state.Settings != null
                ? state.Settings.DecompilerCachePath ?? string.Empty
                : string.Empty;

            if (string.Equals(_cachedSourceRoot, sourceRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_cachedDecompilerRoot, decompilerRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _cachedSourceRoot = sourceRoot;
            _cachedDecompilerRoot = decompilerRoot;
            _sourceTree = browserService != null
                ? browserService.BuildTree(_cachedSourceRoot, WorkspaceTreeKind.ProjectSource)
                : null;
            _decompiledTree = browserService != null
                ? browserService.BuildTree(_cachedDecompilerRoot, WorkspaceTreeKind.DecompiledCache)
                : null;
        }

        // ── Filter bar ────────────────────────────────────────────────────────────────

        private void DrawFilterBar()
        {
            GUILayout.BeginHorizontal(_filterBoxStyle ?? GUI.skin.box, GUILayout.Height(26f));
            GUILayout.Label("\u25CA", GUILayout.Width(14f)); // small diamond as search icon
            _filterText = GUILayout.TextField(_filterText ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_filterText) && GUILayout.Button("×", GUILayout.Width(18f), GUILayout.Height(20f)))
            {
                _filterText = string.Empty;
            }
            GUILayout.EndHorizontal();
        }

        // ── Section ───────────────────────────────────────────────────────────────────

        private void DrawSection(string title, WorkspaceTreeNode root, IDocumentService documentService, CortexShellState state)
        {
            GUILayout.Label(title.ToUpperInvariant(), _sectionHeaderStyle ?? GUI.skin.label);
            if (root == null)
            {
                var msg = title == "Source"
                    ? (state.SelectedProject == null ? "No project selected." : "Source root not mapped.")
                    : "No decompiled cache found.";
                GUILayout.Label(msg, GUI.skin.label);
                return;
            }

            DrawTreeNode(root, documentService, state, 0);
        }

        // ── Tree rendering ────────────────────────────────────────────────────────────

        private void DrawTreeNode(WorkspaceTreeNode node, IDocumentService documentService, CortexShellState state, int depth)
        {
            if (node == null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (!MatchesFilter(child))
                {
                    continue;
                }

                if (child.IsDirectory)
                {
                    DrawFolderRow(child, documentService, state, depth);
                }
                else
                {
                    DrawFileRow(child, documentService, state, depth);
                }
            }
        }

        private void DrawFolderRow(WorkspaceTreeNode node, IDocumentService documentService, CortexShellState state, int depth)
        {
            var key = node.FullPath ?? node.Name;
            bool expanded;
            if (!_expandedNodes.TryGetValue(key, out expanded))
            {
                // Default: expand top-level folders that contain matching children when filter is active
                expanded = string.IsNullOrEmpty(_filterText);
                _expandedNodes[key] = expanded;
            }

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Space(depth * IndentWidth);
            var arrow = expanded ? "\u25BC" : "\u25B6";
            if (GUILayout.Button(arrow + " " + (node.Name ?? "Folder"), _folderLabelStyle ?? GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight)))
            {
                _expandedNodes[key] = !expanded;
                expanded = !expanded;
            }
            GUILayout.EndHorizontal();

            if (expanded)
            {
                DrawTreeNode(node, documentService, state, depth + 1);
            }
        }

        private void DrawFileRow(WorkspaceTreeNode node, IDocumentService documentService, CortexShellState state, int depth)
        {
            var isActive = state.Documents.ActiveDocument != null &&
                string.Equals(state.Documents.ActiveDocument.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase);

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Space(depth * IndentWidth + FolderIconWidth);
            var style = isActive ? (_activeFileButtonStyle ?? GUI.skin.button) : (_fileButtonStyle ?? GUI.skin.button);
            var displayName = node.Name ?? Path.GetFileName(node.FullPath);
            if (GUILayout.Button(displayName, style, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight)))
            {
                CortexModuleUtil.OpenDocument(documentService, state, node.FullPath, 0);
                state.StatusMessage = "Opened " + (node.Name ?? node.FullPath);
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            }
            GUILayout.EndHorizontal();
        }

        // ── Filter ────────────────────────────────────────────────────────────────────

        private bool MatchesFilter(WorkspaceTreeNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(_filterText))
            {
                return true;
            }

            var filter = _filterText;
            if ((node.Name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (node.RelativePath ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!node.IsDirectory)
            {
                return false;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (MatchesFilter(node.Children[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // ── Style management ──────────────────────────────────────────────────────────

        private void EnsureStyles(CortexShellState state)
        {
            var themeId = state.Settings != null && !string.IsNullOrEmpty(state.Settings.ThemeId)
                ? state.Settings.ThemeId
                : "cortex.vs-dark";

            if (string.Equals(_appliedTheme, themeId, StringComparison.OrdinalIgnoreCase) &&
                _folderLabelStyle != null)
            {
                return;
            }

            _appliedTheme = themeId;

            var textColor = CortexIdeLayout.GetTextColor();
            var mutedColor = CortexIdeLayout.GetMutedTextColor();
            var accentColor = CortexIdeLayout.GetAccentColor();
            var surfaceColor = CortexIdeLayout.GetSurfaceColor();
            var headerColor = CortexIdeLayout.GetHeaderColor();
            var borderColor = CortexIdeLayout.GetBorderColor();
            var bgColor = CortexIdeLayout.GetBackgroundColor();

            // Folder row - looks like a label but acts as a toggle button
            _folderLabelStyle = new GUIStyle(GUI.skin.label);
            _folderLabelStyle.alignment = TextAnchor.MiddleLeft;
            _folderLabelStyle.fontSize = 11;
            _folderLabelStyle.padding = new RectOffset(2, 2, 1, 1);
            _folderLabelStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_folderLabelStyle, mutedColor);
            _folderLabelStyle.fontStyle = FontStyle.Normal;

            // File row - subtle button style
            _fileHoverBg = MakeTex(CortexIdeLayout.Blend(surfaceColor, accentColor, 0.08f));
            _fileButtonStyle = new GUIStyle(GUI.skin.label);
            _fileButtonStyle.alignment = TextAnchor.MiddleLeft;
            _fileButtonStyle.fontSize = 11;
            _fileButtonStyle.padding = new RectOffset(4, 4, 1, 1);
            _fileButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_fileButtonStyle, textColor);
            _fileButtonStyle.hover.background = _fileHoverBg;
            GuiStyleUtil.ApplyTextColorToAllStates(_fileButtonStyle, textColor);

            // Active file row
            _selectedFileBg = MakeTex(CortexIdeLayout.Blend(headerColor, accentColor, 0.3f));
            _activeFileButtonStyle = new GUIStyle(_fileButtonStyle);
            _activeFileButtonStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyBackgroundToAllStates(_activeFileButtonStyle, _selectedFileBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_activeFileButtonStyle, Color.white);

            // Filter input bar
            _filterBg = MakeTex(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.5f));
            _filterBoxStyle = new GUIStyle(GUI.skin.textField);
            GuiStyleUtil.ApplyBackgroundToAllStates(_filterBoxStyle, _filterBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_filterBoxStyle, textColor);
            _filterBoxStyle.border = new RectOffset(1, 1, 1, 1);
            _filterBoxStyle.padding = new RectOffset(6, 4, 2, 2);
            _filterBoxStyle.margin = new RectOffset(0, 0, 0, 0);

            // Section header
            _sectionHeaderBg = MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.4f));
            _sectionHeaderStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_sectionHeaderStyle, _sectionHeaderBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_sectionHeaderStyle, mutedColor);
            _sectionHeaderStyle.fontSize = 10;
            _sectionHeaderStyle.fontStyle = FontStyle.Bold;
            _sectionHeaderStyle.padding = new RectOffset(6, 6, 3, 3);
            _sectionHeaderStyle.margin = new RectOffset(0, 0, 2, 2);
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

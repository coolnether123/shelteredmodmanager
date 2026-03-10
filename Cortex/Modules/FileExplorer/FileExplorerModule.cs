using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.FileExplorer
{
    /// <summary>
    /// Standalone VS-style explorer. Renders physical source files and logical
    /// decompiler symbols in one navigation surface, while leaving document and
    /// decompilation work to dedicated services.
    /// </summary>
    public sealed class FileExplorerModule
    {
        private Vector2 _scroll = Vector2.zero;
        private string _filterText = string.Empty;
        private string _cachedSourceRoot = string.Empty;
        private string _cachedManagedAssemblyRoot = string.Empty;
        private readonly Dictionary<string, bool> _expandedNodes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private string _appliedTheme = string.Empty;
        private GUIStyle _folderLabelStyle;
        private GUIStyle _hoverFolderLabelStyle;
        private GUIStyle _fileButtonStyle;
        private GUIStyle _hoverFileButtonStyle;
        private GUIStyle _activeFileButtonStyle;
        private GUIStyle _filterBoxStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _treeHeaderStyle;
        private Texture2D _selectedFileBg;
        private Texture2D _fileHoverBg;
        private Texture2D _filterBg;
        private Texture2D _sectionHeaderBg;

        private WorkspaceTreeNode _sourceTree;
        private WorkspaceTreeNode _decompilerTree;

        private const float IndentWidth = 16f;
        private const float RowHeight = 20f;

        public void Draw(
            IWorkspaceBrowserService browserService,
            IDecompilerExplorerService decompilerExplorerService,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            EnsureStyles(state);
            RefreshIfNeeded(browserService, decompilerExplorerService, state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            DrawFilterBar(browserService, decompilerExplorerService, state);
            GUILayout.Space(2f);
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.ExpandHeight(true));
            DrawSection("Workspace", _sourceTree, decompilerExplorerService, navigationService, state);
            GUILayout.Space(6f);
            DrawSection("Decompiler", _decompilerTree, decompilerExplorerService, navigationService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void RefreshIfNeeded(IWorkspaceBrowserService browserService, IDecompilerExplorerService decompilerExplorerService, CortexShellState state)
        {
            var sourceRoot = state.SelectedProject != null
                ? state.SelectedProject.SourceRootPath ?? string.Empty
                : string.Empty;
            var managedAssemblyRoot = state.Settings != null
                ? state.Settings.ManagedAssemblyRootPath ?? string.Empty
                : string.Empty;

            if (string.Equals(_cachedSourceRoot, sourceRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_cachedManagedAssemblyRoot, managedAssemblyRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _cachedSourceRoot = sourceRoot;
            _cachedManagedAssemblyRoot = managedAssemblyRoot;
            RefreshTrees(browserService, decompilerExplorerService);
        }

        private void DrawFilterBar(IWorkspaceBrowserService browserService, IDecompilerExplorerService decompilerExplorerService, CortexShellState state)
        {
            GUILayout.BeginHorizontal(_filterBoxStyle ?? GUI.skin.box, GUILayout.Height(26f));
            GUILayout.Label("F", GUILayout.Width(14f));
            _filterText = GUILayout.TextField(_filterText ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_filterText) && GUILayout.Button("x", GUILayout.Width(18f), GUILayout.Height(20f)))
            {
                _filterText = string.Empty;
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(64f), GUILayout.Height(20f)))
            {
                RefreshTrees(browserService, decompilerExplorerService);
                state.StatusMessage = "Explorer refreshed.";
            }
            GUILayout.EndHorizontal();
        }

        private void RefreshTrees(IWorkspaceBrowserService browserService, IDecompilerExplorerService decompilerExplorerService)
        {
            if (browserService != null && !string.IsNullOrEmpty(_cachedSourceRoot))
            {
                browserService.Refresh(_cachedSourceRoot, WorkspaceTreeKind.ProjectSource);
                _sourceTree = browserService.BuildTree(_cachedSourceRoot, WorkspaceTreeKind.ProjectSource);
            }
            else
            {
                _sourceTree = null;
            }

            _decompilerTree = decompilerExplorerService != null && !string.IsNullOrEmpty(_cachedManagedAssemblyRoot)
                ? decompilerExplorerService.BuildTree(_cachedManagedAssemblyRoot)
                : null;
        }

        private void DrawSection(
            string title,
            WorkspaceTreeNode root,
            IDecompilerExplorerService decompilerExplorerService,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            GUILayout.Label(title.ToUpperInvariant(), _sectionHeaderStyle ?? GUI.skin.label);
            if (root == null || (!root.HasChildren && root.NodeKind == WorkspaceTreeNodeKind.DecompilerRoot))
            {
                var message = title == "Workspace"
                    ? (state.SelectedProject == null ? "No project selected." : "Source root not mapped.")
                    : "No managed assemblies were discovered for decompilation.";
                GUILayout.Label(message, GUI.skin.label);
                return;
            }

            var hoverHighlightPath = string.Equals(title, "Workspace", StringComparison.Ordinal)
                ? ResolveVisibleHoverTargetPath(root, GetVisibleHoverDefinitionPath(state), 0)
                : string.Empty;
            DrawTreeNode(root, decompilerExplorerService, navigationService, state, hoverHighlightPath, 0, true);
        }

        private void DrawTreeNode(
            WorkspaceTreeNode node,
            IDecompilerExplorerService decompilerExplorerService,
            CortexNavigationService navigationService,
            CortexShellState state,
            string hoverHighlightPath,
            int depth,
            bool drawSelf)
        {
            if (node == null)
            {
                return;
            }

            if (drawSelf)
            {
                if (!MatchesFilter(node, decompilerExplorerService))
                {
                    return;
                }

                if (node.HasChildren)
                {
                    DrawExpandableNode(node, decompilerExplorerService, navigationService, state, hoverHighlightPath, depth);
                }
                else
                {
                    DrawLeafNode(node, navigationService, state, hoverHighlightPath, depth);
                }

                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                DrawTreeNode(node.Children[i], decompilerExplorerService, navigationService, state, hoverHighlightPath, depth, true);
            }
        }

        private void DrawExpandableNode(
            WorkspaceTreeNode node,
            IDecompilerExplorerService decompilerExplorerService,
            CortexNavigationService navigationService,
            CortexShellState state,
            string hoverHighlightPath,
            int depth)
        {
            var key = BuildNodeKey(node);
            var expanded = GetExpandedState(node, key, depth);
            var autoExpandedByFilter = false;
            if (!string.IsNullOrEmpty(_filterText))
            {
                expanded = true;
                autoExpandedByFilter = true;
                _expandedNodes[key] = true;
            }

            if (expanded && (!autoExpandedByFilter || node.ChildrenLoaded || !node.IsVirtual))
            {
                EnsureChildrenLoaded(node, decompilerExplorerService);
            }

            var folderStyle = IsHighlightedNode(node, hoverHighlightPath)
                ? (_hoverFolderLabelStyle ?? _folderLabelStyle ?? GUI.skin.button)
                : (_folderLabelStyle ?? GUI.skin.button);

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Space(depth * IndentWidth);
            var arrow = expanded ? "v" : ">";
            if (GUILayout.Button(arrow, _treeHeaderStyle ?? GUI.skin.button, GUILayout.Width(18f), GUILayout.Height(RowHeight)))
            {
                expanded = !expanded;
                _expandedNodes[key] = expanded;
                if (expanded)
                {
                    EnsureChildrenLoaded(node, decompilerExplorerService);
                }
            }

            if (GUILayout.Button(node.Name ?? "Node", folderStyle, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight)))
            {
                HandleExpandableNodeAction(node, navigationService, state, ref expanded);
                _expandedNodes[key] = expanded;
            }

            GUILayout.EndHorizontal();

            if (!expanded)
            {
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                DrawTreeNode(node.Children[i], decompilerExplorerService, navigationService, state, hoverHighlightPath, depth + 1, true);
            }
        }

        private void DrawLeafNode(
            WorkspaceTreeNode node,
            CortexNavigationService navigationService,
            CortexShellState state,
            string hoverHighlightPath,
            int depth)
        {
            var isActiveFile = node.NodeKind == WorkspaceTreeNodeKind.File &&
                state.Documents.ActiveDocument != null &&
                string.Equals(state.Documents.ActiveDocument.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase);
            var isHoverTarget = IsHighlightedNode(node, hoverHighlightPath);

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Space(depth * IndentWidth + 18f);
            var style = isActiveFile
                ? (_activeFileButtonStyle ?? GUI.skin.button)
                : (isHoverTarget ? (_hoverFileButtonStyle ?? _fileButtonStyle ?? GUI.skin.button) : (_fileButtonStyle ?? GUI.skin.button));
            if (GUILayout.Button(node.Name ?? "Item", style, GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight)))
            {
                ActivateLeafNode(node, navigationService, state);
            }

            GUILayout.EndHorizontal();
        }

        private void HandleExpandableNodeAction(
            WorkspaceTreeNode node,
            CortexNavigationService navigationService,
            CortexShellState state,
            ref bool expanded)
        {
            if (node == null)
            {
                return;
            }

            if (node.NodeKind == WorkspaceTreeNodeKind.Type)
            {
                OpenDecompilerNode(node, navigationService, state);
                return;
            }

            expanded = !expanded;
        }

        private void ActivateLeafNode(
            WorkspaceTreeNode node,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            if (node == null)
            {
                return;
            }

            if (node.NodeKind == WorkspaceTreeNodeKind.File)
            {
                if (navigationService != null)
                {
                    navigationService.OpenDocument(state, node.FullPath, 0, "Opened " + (node.Name ?? node.FullPath), "Could not open " + (node.Name ?? node.FullPath) + ".");
                }
                return;
            }

            OpenDecompilerNode(node, navigationService, state);
        }

        private void OpenDecompilerNode(
            WorkspaceTreeNode node,
            CortexNavigationService navigationService,
            CortexShellState state)
        {
            if (node == null || string.IsNullOrEmpty(node.AssemblyPath) || node.MetadataToken <= 0)
            {
                return;
            }

            var response = navigationService != null
                ? navigationService.RequestDecompilerSource(state, node.AssemblyPath, node.MetadataToken, node.EntityKind, false)
                : null;

            if (response == null)
            {
                state.StatusMessage = "Decompiler request failed.";
                return;
            }

            if (navigationService != null &&
                navigationService.OpenDecompilerResult(
                    state,
                    response,
                    "Opened " + (node.Name ?? "decompiled source") + ".",
                    response.StatusMessage ?? ("Generated decompiled source for " + (node.Name ?? "symbol") + ".")))
            {
                return;
            }

            state.StatusMessage = response.StatusMessage ?? ("Generated decompiled source for " + (node.Name ?? "symbol") + ".");
        }

        private bool MatchesFilter(WorkspaceTreeNode node, IDecompilerExplorerService decompilerExplorerService)
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
                (node.RelativePath ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (node.AssemblyPath ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (node.TypeName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!node.HasChildren)
            {
                return false;
            }

            if (!node.ChildrenLoaded)
            {
                return false;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (MatchesFilter(node.Children[i], decompilerExplorerService))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureChildrenLoaded(WorkspaceTreeNode node, IDecompilerExplorerService decompilerExplorerService)
        {
            if (node == null || node.ChildrenLoaded || !node.IsVirtual || decompilerExplorerService == null)
            {
                return;
            }

            decompilerExplorerService.EnsureChildren(node);
        }

        private bool GetExpandedState(WorkspaceTreeNode node, string key, int depth)
        {
            bool expanded;
            if (_expandedNodes.TryGetValue(key, out expanded))
            {
                return expanded;
            }

            expanded = depth == 0 || node.NodeKind == WorkspaceTreeNodeKind.DecompilerRoot;
            _expandedNodes[key] = expanded;
            return expanded;
        }

        private static string BuildNodeKey(WorkspaceTreeNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(node.FullPath))
            {
                return node.FullPath;
            }

            return (node.Name ?? string.Empty) + ":" + node.NodeKind;
        }

        private static string GetVisibleHoverDefinitionPath(CortexShellState state)
        {
            return state != null && state.Editor != null
                ? state.Editor.VisibleHoverDefinitionDocumentPath ?? string.Empty
                : string.Empty;
        }

        private string ResolveVisibleHoverTargetPath(WorkspaceTreeNode node, string hoverDefinitionPath, int depth)
        {
            if (node == null || string.IsNullOrEmpty(hoverDefinitionPath))
            {
                return string.Empty;
            }

            if (node.NodeKind == WorkspaceTreeNodeKind.File)
            {
                return string.Equals(node.FullPath, hoverDefinitionPath, StringComparison.OrdinalIgnoreCase)
                    ? node.FullPath ?? string.Empty
                    : string.Empty;
            }

            if (!NodeContainsHoverPath(node, hoverDefinitionPath))
            {
                return string.Empty;
            }

            var key = BuildNodeKey(node);
            var expanded = GetExpandedState(node, key, depth);
            if (!string.IsNullOrEmpty(_filterText))
            {
                expanded = true;
            }

            if (!expanded)
            {
                return node.FullPath ?? string.Empty;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (!NodeContainsHoverPath(child, hoverDefinitionPath))
                {
                    continue;
                }

                var childTarget = ResolveVisibleHoverTargetPath(child, hoverDefinitionPath, depth + 1);
                if (!string.IsNullOrEmpty(childTarget))
                {
                    return childTarget;
                }
            }

            return node.FullPath ?? string.Empty;
        }

        private static bool NodeContainsHoverPath(WorkspaceTreeNode node, string hoverDefinitionPath)
        {
            if (node == null || string.IsNullOrEmpty(hoverDefinitionPath))
            {
                return false;
            }

            if (node.NodeKind == WorkspaceTreeNodeKind.File)
            {
                return string.Equals(node.FullPath, hoverDefinitionPath, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrEmpty(node.FullPath))
            {
                return false;
            }

            var normalizedFolder = NormalizeDirectoryPath(node.FullPath);
            return hoverDefinitionPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHighlightedNode(WorkspaceTreeNode node, string hoverHighlightPath)
        {
            return node != null &&
                !string.IsNullOrEmpty(node.FullPath) &&
                !string.IsNullOrEmpty(hoverHighlightPath) &&
                string.Equals(node.FullPath, hoverHighlightPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

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
            var hoverHighlightColor = CortexIdeLayout.Blend(
                surfaceColor,
                CortexIdeLayout.ParseColor("#DCDCAA", textColor),
                0.28f);

            _folderLabelStyle = new GUIStyle(GUI.skin.button);
            _folderLabelStyle.alignment = TextAnchor.MiddleLeft;
            _folderLabelStyle.fontSize = 11;
            _folderLabelStyle.padding = new RectOffset(4, 6, 1, 1);
            _folderLabelStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_folderLabelStyle, MakeTex(surfaceColor));
            GuiStyleUtil.ApplyTextColorToAllStates(_folderLabelStyle, textColor);

            _hoverFolderLabelStyle = new GUIStyle(_folderLabelStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_hoverFolderLabelStyle, MakeTex(hoverHighlightColor));
            GuiStyleUtil.ApplyTextColorToAllStates(
                _hoverFolderLabelStyle,
                CortexIdeLayout.ParseColor("#FFF4B0", textColor));

            _fileButtonStyle = new GUIStyle(GUI.skin.button);
            _fileButtonStyle.alignment = TextAnchor.MiddleLeft;
            _fileButtonStyle.fontSize = 11;
            _fileButtonStyle.padding = new RectOffset(6, 6, 1, 1);
            _fileButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            _selectedFileBg = MakeTex(CortexIdeLayout.Blend(accentColor, surfaceColor, 0.72f));
            _fileHoverBg = MakeTex(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.32f));
            GuiStyleUtil.ApplyBackgroundToAllStates(_fileButtonStyle, MakeTex(surfaceColor));
            GuiStyleUtil.ApplyTextColorToAllStates(_fileButtonStyle, mutedColor);
            _fileButtonStyle.hover.background = _fileHoverBg;
            _fileButtonStyle.hover.textColor = textColor;

            _hoverFileButtonStyle = new GUIStyle(_fileButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_hoverFileButtonStyle, MakeTex(hoverHighlightColor));
            GuiStyleUtil.ApplyTextColorToAllStates(
                _hoverFileButtonStyle,
                CortexIdeLayout.ParseColor("#FFF4B0", textColor));

            _activeFileButtonStyle = new GUIStyle(_fileButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_activeFileButtonStyle, _selectedFileBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_activeFileButtonStyle, Color.white);
            _activeFileButtonStyle.fontStyle = FontStyle.Bold;

            _filterBg = MakeTex(CortexIdeLayout.Blend(surfaceColor, bgColor, 0.5f));
            _filterBoxStyle = new GUIStyle(GUI.skin.textField);
            GuiStyleUtil.ApplyBackgroundToAllStates(_filterBoxStyle, _filterBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_filterBoxStyle, textColor);
            _filterBoxStyle.margin = new RectOffset(0, 0, 0, 0);
            _filterBoxStyle.padding = new RectOffset(6, 6, 4, 4);

            _sectionHeaderBg = MakeTex(headerColor);
            _sectionHeaderStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_sectionHeaderStyle, _sectionHeaderBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_sectionHeaderStyle, textColor);
            _sectionHeaderStyle.fontSize = 10;
            _sectionHeaderStyle.fontStyle = FontStyle.Bold;
            _sectionHeaderStyle.padding = new RectOffset(6, 6, 4, 4);
            _sectionHeaderStyle.margin = new RectOffset(0, 0, 0, 2);

            _treeHeaderStyle = new GUIStyle(GUI.skin.button);
            _treeHeaderStyle.alignment = TextAnchor.MiddleCenter;
            _treeHeaderStyle.padding = new RectOffset(0, 0, 0, 0);
            _treeHeaderStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_treeHeaderStyle, MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.35f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_treeHeaderStyle, borderColor);
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}

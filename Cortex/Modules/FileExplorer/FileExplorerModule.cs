using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Explorer;
using Cortex.Services.Navigation;
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
        private string _cachedSourceRoot = string.Empty;
        private string _cachedManagedAssemblyRoot = string.Empty;
        private readonly Dictionary<string, bool> _expandedNodes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ExplorerFilterPlanBuilder _filterPlanBuilder = new ExplorerFilterPlanBuilder();

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
            IContributionRegistry contributionRegistry,
            IWorkspaceBrowserService browserService,
            IDecompilerExplorerService decompilerExplorerService,
            ICortexNavigationService navigationService,
            CortexShellState state)
        {
            if (state == null)
            {
                return;
            }

            EnsureStyles(state);
            RefreshIfNeeded(browserService, decompilerExplorerService, state);
            var explorerState = state.Explorer;
            SynchronizeFriendlyFilters(explorerState);
            var workspaceFilterPlan = _filterPlanBuilder.Build(contributionRegistry, state, ExplorerFilterScope.Workspace);
            var decompilerFilterPlan = _filterPlanBuilder.Build(contributionRegistry, state, ExplorerFilterScope.Decompiler);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            DrawFilterBar(explorerState, browserService, decompilerExplorerService, state, workspaceFilterPlan, decompilerFilterPlan);
            GUILayout.Space(2f);
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.ExpandHeight(true));
            DrawSection("Workspace", _sourceTree, decompilerExplorerService, navigationService, state, workspaceFilterPlan);
            GUILayout.Space(6f);
            DrawSection("Decompiler", _decompilerTree, decompilerExplorerService, navigationService, state, decompilerFilterPlan);
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

        private void DrawFilterBar(
            CortexExplorerInteractionState explorerState,
            IWorkspaceBrowserService browserService,
            IDecompilerExplorerService decompilerExplorerService,
            CortexShellState state,
            ExplorerFilterPlan workspaceFilterPlan,
            ExplorerFilterPlan decompilerFilterPlan)
        {
            GUILayout.BeginHorizontal(_filterBoxStyle ?? GUI.skin.box, GUILayout.Height(26f));
            GUILayout.Label("F", GUILayout.Width(14f));
            explorerState.FilterText = GUILayout.TextField(explorerState.FilterText ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(explorerState.FilterText) && GUILayout.Button("x", GUILayout.Width(18f), GUILayout.Height(20f)))
            {
                explorerState.FilterText = string.Empty;
            }
            if (GUILayout.Button(BuildRefineButtonLabel(explorerState), GUILayout.Width(108f), GUILayout.Height(20f)))
            {
                explorerState.FiltersVisible = !explorerState.FiltersVisible;
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(64f), GUILayout.Height(20f)))
            {
                RefreshTrees(browserService, decompilerExplorerService);
                state.StatusMessage = "Explorer refreshed.";
            }
            GUILayout.EndHorizontal();

            if (explorerState.FiltersVisible)
            {
                DrawFilterPanel(explorerState, workspaceFilterPlan, decompilerFilterPlan);
            }
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
            ICortexNavigationService navigationService,
            CortexShellState state,
            ExplorerFilterPlan filterPlan)
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
                ? ResolveVisibleHoverTargetPath(root, GetVisibleHoverDefinitionPath(state), filterPlan, 0)
                : string.Empty;
            DrawTreeNode(root, decompilerExplorerService, navigationService, state, hoverHighlightPath, filterPlan, 0, true);
        }

        private void DrawTreeNode(
            WorkspaceTreeNode node,
            IDecompilerExplorerService decompilerExplorerService,
            ICortexNavigationService navigationService,
            CortexShellState state,
            string hoverHighlightPath,
            ExplorerFilterPlan filterPlan,
            int depth,
            bool drawSelf)
        {
            if (node == null)
            {
                return;
            }

            if (drawSelf)
            {
                if (filterPlan != null && !filterPlan.Matches(node))
                {
                    return;
                }

                if (node.HasChildren)
                {
                    DrawExpandableNode(node, decompilerExplorerService, navigationService, state, hoverHighlightPath, filterPlan, depth);
                }
                else
                {
                    DrawLeafNode(node, navigationService, state, hoverHighlightPath, depth);
                }

                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                DrawTreeNode(node.Children[i], decompilerExplorerService, navigationService, state, hoverHighlightPath, filterPlan, depth, true);
            }
        }

        private void DrawExpandableNode(
            WorkspaceTreeNode node,
            IDecompilerExplorerService decompilerExplorerService,
            ICortexNavigationService navigationService,
            CortexShellState state,
            string hoverHighlightPath,
            ExplorerFilterPlan filterPlan,
            int depth)
        {
            var key = BuildNodeKey(node);
            bool expanded;
            var hasStoredState = _expandedNodes.TryGetValue(key, out expanded);
            if (!hasStoredState)
            {
                expanded = depth == 0 || node.NodeKind == WorkspaceTreeNodeKind.DecompilerRoot;
                if (filterPlan != null && filterPlan.HasAnyFilter)
                {
                    expanded = true;
                }

                _expandedNodes[key] = expanded;
            }

            if (filterPlan != null && filterPlan.HasAnyFilter && !hasStoredState)
            {
                _expandedNodes[key] = true;
            }

            if (expanded)
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
                DrawTreeNode(node.Children[i], decompilerExplorerService, navigationService, state, hoverHighlightPath, filterPlan, depth + 1, true);
            }
        }

        private void DrawLeafNode(
            WorkspaceTreeNode node,
            ICortexNavigationService navigationService,
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
            ICortexNavigationService navigationService,
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
            ICortexNavigationService navigationService,
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
            ICortexNavigationService navigationService,
            CortexShellState state)
        {
            if (node == null || string.IsNullOrEmpty(node.AssemblyPath) || node.MetadataToken <= 0)
            {
                return;
            }

            if (navigationService != null &&
                navigationService.DecompileAndOpen(
                    state,
                    node.AssemblyPath,
                    node.MetadataToken,
                    node.EntityKind,
                    false,
                    "Opened " + (node.Name ?? "decompiled source") + ".",
                    "Decompiler request failed."))
            {
                return;
            }

            state.StatusMessage = "Decompiler request failed.";
        }

        private void DrawFilterPanel(
            CortexExplorerInteractionState explorerState,
            ExplorerFilterPlan workspaceFilterPlan,
            ExplorerFilterPlan decompilerFilterPlan)
        {
            GUILayout.BeginVertical(_filterBoxStyle ?? GUI.skin.box);
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Explorer View", _treeHeaderStyle ?? GUI.skin.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(52f), GUILayout.Height(20f)))
            {
                explorerState.FilterText = string.Empty;
                explorerState.ActiveFilterIds.Clear();
                explorerState.FocusMode = CortexExplorerFocusMode.Everything;
            }
            GUILayout.EndHorizontal();

            DrawModeRow("Scope", delegate
            {
                DrawScopeButton(explorerState, CortexExplorerScopeMode.CurrentMod, "Current Mod");
                DrawScopeButton(explorerState, CortexExplorerScopeMode.AllRuntime, "All Runtime");
            });

            DrawModeRow("Focus", delegate
            {
                DrawFocusButton(explorerState, CortexExplorerFocusMode.Everything, "Everything");
                DrawFocusButton(explorerState, CortexExplorerFocusMode.HarmonyPatched, "Harmony Patched");
            });

            if (GUILayout.Button(explorerState.AdvancedFiltersVisible ? "Hide Advanced" : "Advanced", GUILayout.Width(108f), GUILayout.Height(20f)))
            {
                explorerState.AdvancedFiltersVisible = !explorerState.AdvancedFiltersVisible;
            }

            if (explorerState.AdvancedFiltersVisible)
            {
                DrawAdvancedFilterPanel(workspaceFilterPlan, decompilerFilterPlan, explorerState);
            }

            GUILayout.Space(2f);
            GUILayout.EndVertical();
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
                ? state.EditorContext.HoveredDefinitionDocumentPath ?? string.Empty
                : string.Empty;
        }

        private string ResolveVisibleHoverTargetPath(WorkspaceTreeNode node, string hoverDefinitionPath, ExplorerFilterPlan filterPlan, int depth)
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
            if (filterPlan != null && filterPlan.HasAnyFilter)
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

                var childTarget = ResolveVisibleHoverTargetPath(child, hoverDefinitionPath, filterPlan, depth + 1);
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

        private void DrawAdvancedFilterPanel(
            ExplorerFilterPlan workspaceFilterPlan,
            ExplorerFilterPlan decompilerFilterPlan,
            CortexExplorerInteractionState explorerState)
        {
            var drewAny = false;
            if (workspaceFilterPlan != null && HasVisibleAdvancedContribution(workspaceFilterPlan))
            {
                DrawContributionGroup("Workspace", workspaceFilterPlan, explorerState);
                drewAny = true;
            }

            if (decompilerFilterPlan != null && HasVisibleAdvancedContribution(decompilerFilterPlan))
            {
                DrawContributionGroup("Decompiler", decompilerFilterPlan, explorerState);
                drewAny = true;
            }

            if (!drewAny)
            {
                GUILayout.Label("No additional explorer filters are available.", GUI.skin.label);
            }
        }

        private void DrawModeRow(string title, Action drawButtons)
        {
            GUILayout.Space(2f);
            GUILayout.Label(title, _sectionHeaderStyle ?? GUI.skin.label);
            GUILayout.BeginHorizontal();
            drawButtons?.Invoke();
            GUILayout.EndHorizontal();
        }

        private void DrawScopeButton(CortexExplorerInteractionState explorerState, CortexExplorerScopeMode scopeMode, string label)
        {
            if (explorerState == null)
            {
                return;
            }

            var isActive = explorerState.ScopeMode == scopeMode;
            var style = isActive
                ? (_activeFileButtonStyle ?? GUI.skin.button)
                : (_fileButtonStyle ?? GUI.skin.button);
            if (GUILayout.Button(label, style, GUILayout.Height(20f)))
            {
                explorerState.ScopeMode = scopeMode;
            }
        }

        private void DrawFocusButton(CortexExplorerInteractionState explorerState, CortexExplorerFocusMode focusMode, string label)
        {
            if (explorerState == null)
            {
                return;
            }

            var isActive = explorerState.FocusMode == focusMode;
            var style = isActive
                ? (_activeFileButtonStyle ?? GUI.skin.button)
                : (_fileButtonStyle ?? GUI.skin.button);
            if (GUILayout.Button(label, style, GUILayout.Height(20f)))
            {
                explorerState.FocusMode = focusMode;
            }
        }

        private void DrawContributionGroup(string title, ExplorerFilterPlan filterPlan, CortexExplorerInteractionState explorerState)
        {
            if (filterPlan == null || explorerState == null || filterPlan.Contributions.Count == 0)
            {
                return;
            }

            GUILayout.Space(2f);
            GUILayout.Label(title, _sectionHeaderStyle ?? GUI.skin.label);
            for (var i = 0; i < filterPlan.Contributions.Count; i++)
            {
                var contribution = filterPlan.Contributions[i];
                if (contribution == null ||
                    string.IsNullOrEmpty(contribution.FilterId) ||
                    !ShouldShowInAdvanced(contribution.FilterId))
                {
                    continue;
                }

                var available = filterPlan.IsFilterAvailable(contribution.FilterId);
                var active = filterPlan.IsFilterActive(contribution.FilterId);

                GUI.enabled = available;
                var nextActive = GUILayout.Toggle(
                    active,
                    BuildContributionLabel(contribution),
                    GUILayout.Height(20f));
                GUI.enabled = true;

                if (nextActive != active)
                {
                    SetContributionActive(explorerState, contribution.FilterId, nextActive && available);
                }

                if (!string.IsNullOrEmpty(contribution.Description))
                {
                    GUILayout.Label(contribution.Description, GUI.skin.label);
                }
            }
        }

        private static void SynchronizeFriendlyFilters(CortexExplorerInteractionState explorerState)
        {
            if (explorerState == null)
            {
                return;
            }

            SetContributionActive(
                explorerState,
                ExplorerFilterWellKnownIds.HarmonyPatched,
                explorerState.FocusMode == CortexExplorerFocusMode.HarmonyPatched);
        }

        private static string BuildRefineButtonLabel(CortexExplorerInteractionState explorerState)
        {
            if (explorerState == null)
            {
                return "Refine";
            }

            if (explorerState.FocusMode == CortexExplorerFocusMode.HarmonyPatched)
            {
                return explorerState.ScopeMode == CortexExplorerScopeMode.AllRuntime
                    ? "All | Harmony"
                    : "Mod | Harmony";
            }

            return explorerState.ScopeMode == CortexExplorerScopeMode.AllRuntime
                ? "All Runtime"
                : "Current Mod";
        }

        private static bool HasVisibleAdvancedContribution(ExplorerFilterPlan filterPlan)
        {
            if (filterPlan == null)
            {
                return false;
            }

            for (var i = 0; i < filterPlan.Contributions.Count; i++)
            {
                var contribution = filterPlan.Contributions[i];
                if (contribution != null && ShouldShowInAdvanced(contribution.FilterId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldShowInAdvanced(string filterId)
        {
            return !string.Equals(filterId, ExplorerFilterWellKnownIds.HarmonyPatched, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetContributionActive(CortexExplorerInteractionState explorerState, string filterId, bool isActive)
        {
            if (explorerState == null || string.IsNullOrEmpty(filterId))
            {
                return;
            }

            if (isActive)
            {
                explorerState.ActiveFilterIds.Add(filterId);
                return;
            }

            explorerState.ActiveFilterIds.Remove(filterId);
        }

        private static string BuildContributionLabel(ExplorerFilterContribution contribution)
        {
            if (contribution == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(contribution.DisplayName)
                ? contribution.DisplayName
                : contribution.FilterId ?? string.Empty;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cortex.Bridge;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Presentation.Runtime;
using Cortex.Renderers.DearImgui.Native;
using Cortex.Services.Editor.Presentation;
using Cortex.Shell.Shared.Models;
using Cortex.Core.Models;

namespace Cortex.Renderers.DearImgui
{
    internal sealed class DearImguiShellPresenter
    {
        private const float WindowPadding = 6f;
        private const float HeaderHeight = 30f;
        private const float StatusHeight = 24f;
        private const float SplitterSpacing = 6f;
        private const float SecondaryPaneWidth = 300f;
        private const int DefaultInputCapacity = 2048;

        private readonly Dictionary<string, byte[]> _inputBuffers = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedWorkspaceNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly EditorClassificationService _classificationService = new EditorClassificationService();
        private string _buildConfiguration = string.Empty;

        public bool Draw(CortexShellController controller, float viewportWidth, float viewportHeight)
        {
            if (controller == null || !controller.PrepareRendererFrameForCurrentState())
            {
                return false;
            }

            var frame = controller.CreateShellFrameForRenderer() ?? new RendererShellFrameModel();
            if (!frame.IsVisible)
            {
                return false;
            }

            var presentation = controller.CreatePresentationSnapshotForRenderer() ?? new WorkbenchPresentationSnapshot();
            var bridge = controller.CreateWorkbenchBridgeSnapshotForRenderer() ?? new WorkbenchBridgeSnapshot();
            var document = controller.CreateActiveDocumentContentForRenderer() ?? new RendererDocumentContentModel();

            DrawMainMenu(controller, presentation);
            DrawShellWindow(controller, frame, presentation, bridge, document, viewportWidth, viewportHeight);
            if (frame.ShowDetachedLogsWindow && !frame.OnboardingActive)
            {
                DrawDetachedLogsWindow(controller, frame.LogsWindow);
            }

            return true;
        }

        private void DrawMainMenu(CortexShellController controller, WorkbenchPresentationSnapshot snapshot)
        {
            if (!DearImguiNative.igBeginMainMenuBar())
            {
                return;
            }

            var groups = GroupMenuItems(snapshot.MainMenuItems);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (!DearImguiNative.igBeginMenu(group.Key, true))
                {
                    continue;
                }

                var items = group.Value;
                for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    var item = items[itemIndex];
                    if (DearImguiNative.igMenuItem_Bool(item.DisplayName ?? item.CommandId, item.DefaultGesture, false, true))
                    {
                        controller.ExecuteRendererCommand(item.CommandId);
                    }
                }

                DearImguiNative.igEndMenu();
            }

            DearImguiNative.igEndMainMenuBar();
        }

        private void DrawShellWindow(
            CortexShellController controller,
            RendererShellFrameModel frame,
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchBridgeSnapshot bridge,
            RendererDocumentContentModel document,
            float viewportWidth,
            float viewportHeight)
        {
            var window = frame != null ? frame.MainWindow : new RendererShellWindowModel();
            var width = window.Width > 0f ? window.Width : Math.Max(920f, viewportWidth * 0.82f);
            var height = window.Height > 0f ? window.Height : Math.Max(580f, viewportHeight * 0.78f);
            DearImguiNative.igSetNextWindowPos(new DearImguiNative.ImVec2(window.X, window.Y), DearImguiNative.ImGuiCond.Always, new DearImguiNative.ImVec2(0f, 0f));
            DearImguiNative.igSetNextWindowSize(new DearImguiNative.ImVec2(width, height), DearImguiNative.ImGuiCond.Always);
            if (!DearImguiNative.igBegin(string.IsNullOrEmpty(window.Title) ? "Cortex IDE" : window.Title, IntPtr.Zero, DearImguiNative.ImGuiWindowFlags.None))
            {
                DearImguiNative.igEnd();
                return;
            }

            DrawToolbar(controller, snapshot);
            DearImguiNative.igSeparator();

            var contentWidth = Math.Max(0f, width - (WindowPadding * 2f));
            var contentHeight = Math.Max(0f, height - HeaderHeight - StatusHeight - (WindowPadding * 4f));
            if (DearImguiNative.igBeginChild_Str("cortex.layout.root", new DearImguiNative.ImVec2(contentWidth, contentHeight), false, DearImguiNative.ImGuiWindowFlags.None))
            {
                DrawLayoutNode(controller, snapshot, bridge, document, frame != null ? frame.LayoutRoot : null, contentWidth, contentHeight);
            }
            DearImguiNative.igEndChild();

            DearImguiNative.igSeparator();
            DrawInlineStatus(snapshot, bridge, controller);
            DearImguiNative.igEnd();
        }

        private void DrawDetachedLogsWindow(CortexShellController controller, RendererShellWindowModel window)
        {
            var width = window != null && window.Width > 0f ? window.Width : 760f;
            var height = window != null && window.Height > 0f ? window.Height : 420f;
            DearImguiNative.igSetNextWindowPos(new DearImguiNative.ImVec2(window != null ? window.X : 24f, window != null ? window.Y : 24f), DearImguiNative.ImGuiCond.Always, new DearImguiNative.ImVec2(0f, 0f));
            DearImguiNative.igSetNextWindowSize(new DearImguiNative.ImVec2(width, height), DearImguiNative.ImGuiCond.Always);
            if (!DearImguiNative.igBegin(window != null && !string.IsNullOrEmpty(window.Title) ? window.Title : "Cortex Logs", IntPtr.Zero, DearImguiNative.ImGuiWindowFlags.None))
            {
                DearImguiNative.igEnd();
                return;
            }

            DrawLogs(controller);
            DearImguiNative.igEnd();
        }

        private void DrawLayoutNode(
            CortexShellController controller,
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchBridgeSnapshot bridge,
            RendererDocumentContentModel document,
            RendererLayoutNodeModel node,
            float width,
            float height)
        {
            if (node == null)
            {
                DrawContainerContent(controller, snapshot, bridge, document, snapshot.ActiveContainerId ?? string.Empty);
                return;
            }

            if (!string.IsNullOrEmpty(node.SplitDirection) &&
                !string.Equals(node.SplitDirection, CortexLayoutSplitDirection.None.ToString(), StringComparison.OrdinalIgnoreCase) &&
                node.FirstChild != null &&
                node.SecondChild != null)
            {
                var ratio = node.SplitRatio > 0f ? node.SplitRatio : 0.5f;
                if (string.Equals(node.SplitDirection, CortexLayoutSplitDirection.Horizontal.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var firstWidth = Math.Max(180f, (width * ratio) - (SplitterSpacing * 0.5f));
                    var secondWidth = Math.Max(180f, width - firstWidth - SplitterSpacing);
                    if (DearImguiNative.igBeginChild_Str(node.NodeId + ".first", new DearImguiNative.ImVec2(firstWidth, height), true, DearImguiNative.ImGuiWindowFlags.None))
                    {
                        DrawLayoutNode(controller, snapshot, bridge, document, node.FirstChild, firstWidth, height);
                    }
                    DearImguiNative.igEndChild();
                    DearImguiNative.igSameLine(0f, SplitterSpacing);
                    if (DearImguiNative.igBeginChild_Str(node.NodeId + ".second", new DearImguiNative.ImVec2(0f, height), true, DearImguiNative.ImGuiWindowFlags.None))
                    {
                        DrawLayoutNode(controller, snapshot, bridge, document, node.SecondChild, secondWidth, height);
                    }
                    DearImguiNative.igEndChild();
                }
                else
                {
                    var firstHeight = Math.Max(140f, (height * ratio) - (SplitterSpacing * 0.5f));
                    var secondHeight = Math.Max(120f, height - firstHeight - SplitterSpacing);
                    if (DearImguiNative.igBeginChild_Str(node.NodeId + ".first", new DearImguiNative.ImVec2(width, firstHeight), true, DearImguiNative.ImGuiWindowFlags.None))
                    {
                        DrawLayoutNode(controller, snapshot, bridge, document, node.FirstChild, width, firstHeight);
                    }
                    DearImguiNative.igEndChild();
                    if (DearImguiNative.igBeginChild_Str(node.NodeId + ".second", new DearImguiNative.ImVec2(width, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
                    {
                        DrawLayoutNode(controller, snapshot, bridge, document, node.SecondChild, width, secondHeight);
                    }
                    DearImguiNative.igEndChild();
                }

                return;
            }

            DrawHostLeaf(controller, snapshot, bridge, document, node);
        }

        private void DrawHostLeaf(
            CortexShellController controller,
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchBridgeSnapshot bridge,
            RendererDocumentContentModel document,
            RendererLayoutNodeModel node)
        {
            var activeContainerId = node != null ? node.ActiveContainerId ?? string.Empty : snapshot.ActiveContainerId ?? string.Empty;
            var hostItems = ResolveHostItems(snapshot, node);
            for (var i = 0; i < hostItems.Count; i++)
            {
                var item = hostItems[i];
                if (i > 0)
                {
                    DearImguiNative.igSameLine(0f, 6f);
                }

                var selected = string.Equals(activeContainerId, item.ContainerId, StringComparison.OrdinalIgnoreCase);
                if (DearImguiNative.igSelectable_Bool(item.Title ?? item.ContainerId, selected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ActivateContainerFromRenderer(item.ContainerId);
                    activeContainerId = item.ContainerId ?? string.Empty;
                }
            }

            if (hostItems.Count > 0)
            {
                DearImguiNative.igSeparator();
            }

            DrawContainerContent(controller, snapshot, bridge, document, activeContainerId);
        }

        private void DrawContainerContent(
            CortexShellController controller,
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchBridgeSnapshot bridge,
            RendererDocumentContentModel document,
            string activeContainerId)
        {
            if (string.Equals(activeContainerId, CortexWorkbenchIds.SettingsContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawSettings(controller, bridge.Settings);
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.FileExplorerContainer, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(activeContainerId, CortexWorkbenchIds.ProjectsContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawWorkspace(controller, bridge.Workspace, string.Equals(activeContainerId, CortexWorkbenchIds.ProjectsContainer, StringComparison.OrdinalIgnoreCase));
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.SearchContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawSearch(controller, bridge.Search);
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.ReferenceContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawReference(controller, bridge.Reference);
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.BuildContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawBuild(controller);
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.RuntimeContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawRuntime(controller);
            }
            else if (string.Equals(activeContainerId, CortexWorkbenchIds.LogsContainer, StringComparison.OrdinalIgnoreCase))
            {
                DrawLogs(controller);
            }
            else
            {
                DrawEditor(controller, bridge.Editor, document);
            }
        }

        private void DrawToolbar(CortexShellController controller, WorkbenchPresentationSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.ToolbarItems.Count; i++)
            {
                var item = snapshot.ToolbarItems[i];
                if (i > 0)
                {
                    DearImguiNative.igSameLine(0f, 8f);
                }

                if (DearImguiNative.igButton(item.DisplayName ?? item.CommandId, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ExecuteRendererCommand(item.CommandId);
                }
            }
        }

        private void DrawSettings(CortexShellController controller, SettingsBridgeSnapshot snapshot)
        {
            var searchQuery = snapshot != null ? snapshot.SearchQuery ?? string.Empty : string.Empty;
            DearImguiNative.igSetNextItemWidth(260f);
            if (DrawInputText("Search##settings", ref searchQuery, 1024))
            {
                controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                {
                    IntentType = BridgeIntentType.SetSettingsSearchQuery,
                    SearchQuery = searchQuery
                });
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Save Settings", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                {
                    IntentType = BridgeIntentType.SaveSettings
                });
            }

            DearImguiNative.igSeparator();

            if (DearImguiNative.igBeginChild_Str("settings.sections", new DearImguiNative.ImVec2(240f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                for (var i = 0; i < snapshot.VisibleSections.Count; i++)
                {
                    var section = snapshot.VisibleSections[i];
                    var selected = string.Equals(snapshot.SelectedSectionId, section.SectionId, StringComparison.OrdinalIgnoreCase);
                    if (DearImguiNative.igSelectable_Bool(section.Title ?? section.SectionId, selected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                    {
                        controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                        {
                            IntentType = BridgeIntentType.SelectSettingsSection,
                            SectionId = section.SectionId
                        });
                    }
                }
            }
            DearImguiNative.igEndChild();

            DearImguiNative.igSameLine(0f, 12f);

            if (DearImguiNative.igBeginChild_Str("settings.values", new DearImguiNative.ImVec2(0f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                for (var i = 0; i < snapshot.ActiveSettings.Count; i++)
                {
                    var setting = snapshot.ActiveSettings[i];
                    var currentValue = ResolveDraftValue(snapshot, setting.SettingId, setting.DefaultValue);
                    DearImguiNative.igTextUnformatted(setting.DisplayName ?? setting.SettingId, IntPtr.Zero);
                    if (!string.IsNullOrEmpty(setting.Description))
                    {
                        DearImguiNative.igTextWrapped(setting.Description);
                    }

                    if (setting.EditorKind == ShellSettingEditorKind.Choice || setting.ValueKind == ShellSettingValueKind.Boolean)
                    {
                        DrawSettingChoices(controller, setting, currentValue);
                    }
                    else
                    {
                        var inputValue = currentValue;
                        DearImguiNative.igSetNextItemWidth(420f);
                        if (DrawInputText("##" + (setting.SettingId ?? string.Empty), ref inputValue, DefaultInputCapacity))
                        {
                            controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                            {
                                IntentType = BridgeIntentType.SetSettingValue,
                                SettingId = setting.SettingId,
                                SettingValue = inputValue
                            });
                        }
                    }

                    if (!string.IsNullOrEmpty(setting.HelpText))
                    {
                        DearImguiNative.igTextWrapped(setting.HelpText);
                    }

                    DearImguiNative.igSeparator();
                }
            }
            DearImguiNative.igEndChild();
        }

        private void DrawSettingChoices(CortexShellController controller, SettingDescriptor setting, string currentValue)
        {
            var options = setting != null && setting.Options != null ? setting.Options : new SettingChoiceDescriptor[0];
            if (options.Length == 0)
            {
                var value = string.Equals(currentValue, bool.TrueString, StringComparison.OrdinalIgnoreCase);
                var toggled = value;
                if (DearImguiNative.igCheckbox("##" + (setting.SettingId ?? string.Empty), ref toggled) && toggled != value)
                {
                    controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                    {
                        IntentType = BridgeIntentType.SetSettingValue,
                        SettingId = setting.SettingId,
                        SettingValue = toggled ? bool.TrueString : bool.FalseString
                    });
                }

                return;
            }

            for (var optionIndex = 0; optionIndex < options.Length; optionIndex++)
            {
                var option = options[optionIndex];
                var selected = string.Equals(currentValue, option.Value, StringComparison.OrdinalIgnoreCase);
                if (DearImguiNative.igSelectable_Bool(option.DisplayName ?? option.Value, selected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                    {
                        IntentType = BridgeIntentType.SetSettingValue,
                        SettingId = setting.SettingId,
                        SettingValue = option.Value
                    });
                }

                if (!string.IsNullOrEmpty(option.Description))
                {
                    DearImguiNative.igTextWrapped(option.Description);
                }
            }
        }

        private void DrawWorkspace(CortexShellController controller, WorkspaceBridgeSnapshot snapshot, bool preferProjectsView)
        {
            DearImguiNative.igTextUnformatted("Workspace Root: " + (snapshot.WorkspaceRootPath ?? string.Empty), IntPtr.Zero);
            if (DearImguiNative.igButton("Analyze", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage { IntentType = BridgeIntentType.AnalyzeWorkspace });
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Import Projects", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage { IntentType = BridgeIntentType.ImportWorkspace });
            }

            DearImguiNative.igSeparator();

            if (DearImguiNative.igBeginChild_Str("workspace.projects", new DearImguiNative.ImVec2(preferProjectsView ? 260f : 240f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                DearImguiNative.igTextUnformatted("Projects", IntPtr.Zero);
                DearImguiNative.igSeparator();
                for (var i = 0; i < snapshot.Projects.Count; i++)
                {
                    var project = snapshot.Projects[i];
                    var selected = string.Equals(snapshot.SelectedProjectId, project.ProjectId, StringComparison.OrdinalIgnoreCase);
                    if (DearImguiNative.igSelectable_Bool(project.DisplayName ?? project.ProjectId, selected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                    {
                        controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                        {
                            IntentType = BridgeIntentType.SelectProject,
                            ProjectId = project.ProjectId
                        });
                    }
                }
            }
            DearImguiNative.igEndChild();

            DearImguiNative.igSameLine(0f, 12f);

            var treeWidth = preferProjectsView ? 280f : SecondaryPaneWidth;
            if (DearImguiNative.igBeginChild_Str("workspace.tree", new DearImguiNative.ImVec2(treeWidth, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                DearImguiNative.igTextUnformatted("Files", IntPtr.Zero);
                DearImguiNative.igSeparator();
                DrawWorkspaceNode(controller, snapshot.WorkspaceTreeRoot, 0f);
            }
            DearImguiNative.igEndChild();

            DearImguiNative.igSameLine(0f, 12f);

            if (DearImguiNative.igBeginChild_Str("workspace.preview", new DearImguiNative.ImVec2(0f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                DearImguiNative.igTextUnformatted("Preview", IntPtr.Zero);
                DearImguiNative.igSeparator();
                if (!string.IsNullOrEmpty(snapshot.PreviewFilePath))
                {
                    DearImguiNative.igTextWrapped(snapshot.PreviewFilePath);
                    if (DearImguiNative.igButton("Open In Editor", new DearImguiNative.ImVec2(0f, 0f)))
                    {
                        controller.OpenDocumentFromRenderer(snapshot.PreviewFilePath, 1);
                    }
                    DearImguiNative.igSeparator();
                    DrawPlainTextBlock("workspace.preview.text", snapshot.PreviewText ?? string.Empty);
                }
                else
                {
                    DearImguiNative.igTextWrapped("Select a workspace file to preview it here.");
                }
            }
            DearImguiNative.igEndChild();
        }

        private void DrawWorkspaceNode(CortexShellController controller, WorkspaceFileNode node, float indent)
        {
            if (node == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(node.FullPath) && !string.IsNullOrEmpty(node.Name))
            {
                if (indent > 0f)
                {
                    DearImguiNative.igIndent(indent);
                }

                if (node.IsDirectory)
                {
                    var expanded = _expandedWorkspaceNodes.Contains(node.FullPath);
                    if (DearImguiNative.igSelectable_Bool((expanded ? "v " : "> ") + node.Name, false, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                    {
                        if (expanded)
                        {
                            _expandedWorkspaceNodes.Remove(node.FullPath);
                        }
                        else
                        {
                            _expandedWorkspaceNodes.Add(node.FullPath);
                        }
                    }

                    if (expanded)
                    {
                        for (var i = 0; i < node.Children.Count; i++)
                        {
                            DrawWorkspaceNode(controller, node.Children[i], 16f);
                        }
                    }
                }
                else if (DearImguiNative.igSelectable_Bool(node.Name, false, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                    {
                        IntentType = BridgeIntentType.OpenFilePreview,
                        FilePath = node.FullPath
                    });
                }

                if (indent > 0f)
                {
                    DearImguiNative.igUnindent(indent);
                }
            }
            else
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    DrawWorkspaceNode(controller, node.Children[i], indent);
                }
            }
        }

        private void DrawSearch(CortexShellController controller, SearchWorkbenchModel snapshot)
        {
            var query = snapshot != null && snapshot.Query != null ? snapshot.Query.SearchText ?? string.Empty : string.Empty;
            DearImguiNative.igSetNextItemWidth(340f);
            DrawInputText("Search##workbench", ref query, 2048);
            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Search", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                {
                    IntentType = BridgeIntentType.UpdateSearch,
                    SearchQuery = query,
                    SearchScope = snapshot.Query != null ? snapshot.Query.Scope : WorkbenchSearchScope.CurrentDocument,
                    MatchCase = snapshot.Query != null && snapshot.Query.MatchCase,
                    WholeWord = snapshot.Query != null && snapshot.Query.WholeWord
                });
            }

            DearImguiNative.igSameLine(0f, 12f);
            DrawSearchScopeSelector(controller, snapshot, query);
            DearImguiNative.igSeparator();
            DearImguiNative.igTextWrapped((snapshot.Title ?? "Search") + " | " + (snapshot.StatusMessage ?? string.Empty));
            if (snapshot.HasSemanticView)
            {
                DearImguiNative.igTextWrapped("Semantic workbench actions are active. This renderer currently shows the shared summary and standard text-search results.");
            }

            DearImguiNative.igSeparator();
            if (DearImguiNative.igBeginChild_Str("search.results", new DearImguiNative.ImVec2(0f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                for (var i = 0; i < snapshot.Documents.Count; i++)
                {
                    var document = snapshot.Documents[i];
                    DearImguiNative.igTextUnformatted(document.DisplayPath ?? document.DocumentPath, IntPtr.Zero);
                    for (var matchIndex = 0; matchIndex < document.Matches.Count; matchIndex++)
                    {
                        var match = document.Matches[matchIndex];
                        var label = match.LineNumber + ":" + match.ColumnNumber + "  " + BuildSingleLinePreview(match.PreviewText ?? match.LineText ?? string.Empty);
                        if (DearImguiNative.igSelectable_Bool(label, match.IsActive, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                        {
                            controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                            {
                                IntentType = BridgeIntentType.OpenSearchResult,
                                ResultIndex = match.ResultIndex
                            });
                        }
                    }

                    DearImguiNative.igSeparator();
                }
            }
            DearImguiNative.igEndChild();
        }

        private void DrawSearchScopeSelector(CortexShellController controller, SearchWorkbenchModel snapshot, string query)
        {
            var scopes = new[]
            {
                WorkbenchSearchScope.CurrentDocument,
                WorkbenchSearchScope.AllOpenDocuments,
                WorkbenchSearchScope.CurrentProject,
                WorkbenchSearchScope.Workspace
            };

            for (var i = 0; i < scopes.Length; i++)
            {
                if (i > 0)
                {
                    DearImguiNative.igSameLine(0f, 6f);
                }

                var scope = scopes[i];
                var selected = snapshot != null && snapshot.Query != null && snapshot.Query.Scope == scope;
                if (DearImguiNative.igSelectable_Bool(GetScopeLabel(scope), selected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ApplyBridgeIntentForRenderer(new BridgeIntentMessage
                    {
                        IntentType = BridgeIntentType.UpdateSearch,
                        SearchQuery = query,
                        SearchScope = scope,
                        MatchCase = snapshot.Query != null && snapshot.Query.MatchCase,
                        WholeWord = snapshot.Query != null && snapshot.Query.WholeWord
                    });
                }
            }
        }

        private void DrawReference(CortexShellController controller, ReferenceWorkbenchModel snapshot)
        {
            if (snapshot == null || !snapshot.HasDecompilerResult)
            {
                DearImguiNative.igTextWrapped("No reference preview is available.");
                return;
            }

            DearImguiNative.igTextUnformatted(snapshot.ResolvedTargetDisplayName ?? string.Empty, IntPtr.Zero);
            DearImguiNative.igTextWrapped(snapshot.StatusMessage ?? string.Empty);
            if (!string.IsNullOrEmpty(snapshot.CachePath))
            {
                DearImguiNative.igTextWrapped(snapshot.CachePath);
                if (DearImguiNative.igButton("Open Decompiled Source", new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.OpenDocumentFromRenderer(snapshot.CachePath, 1);
                }
            }

            DearImguiNative.igSeparator();
            DrawPlainTextBlock("reference.source", snapshot.SourceText ?? string.Empty);
        }

        private void DrawEditor(CortexShellController controller, EditorWorkbenchModel editorSnapshot, RendererDocumentContentModel document)
        {
            DrawEditorTabs(controller, editorSnapshot);
            DearImguiNative.igSeparator();
            DearImguiNative.igTextWrapped(document.CompactPath ?? document.FilePath ?? string.Empty);
            DearImguiNative.igTextWrapped("Ln " + (document.CaretLine + 1) + ", Col " + (document.CaretColumn + 1) + " | " + (document.LanguageStatusLabel ?? string.Empty) + " | " + (document.CompletionStatusLabel ?? string.Empty));
            if (DearImguiNative.igButton("Save All", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ExecuteRendererCommand("cortex.file.saveAll");
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Close Active", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ExecuteRendererCommand("cortex.file.closeActive");
            }

            DearImguiNative.igSeparator();
            DrawCodeEditor(document);
        }

        private void DrawEditorTabs(CortexShellController controller, EditorWorkbenchModel snapshot)
        {
            for (var i = 0; i < snapshot.OpenDocuments.Count; i++)
            {
                var tab = snapshot.OpenDocuments[i];
                if (i > 0)
                {
                    DearImguiNative.igSameLine(0f, 6f);
                }

                var label = (tab.IsDirty ? "* " : string.Empty) + (tab.DisplayName ?? Path.GetFileName(tab.FilePath ?? string.Empty));
                if (DearImguiNative.igSelectable_Bool(label, tab.IsActive, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ActivateDocumentFromRenderer(tab.FilePath);
                }
            }
        }

        private void DrawCodeEditor(RendererDocumentContentModel document)
        {
            if (DearImguiNative.igBeginChild_Str("editor.code", new DearImguiNative.ImVec2(0f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                var lines = SplitLines(document != null ? document.Text ?? string.Empty : string.Empty);
                var classifications = document != null && document.Classifications != null ? document.Classifications : new RendererClassifiedTextSpan[0];
                var textColor = ParseColor("#D4D4D4");
                var lineNumberColor = ParseColor("#858585");
                var highlightColor = ParseColor("#C8A155");

                var lineStart = 0;
                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    var lineText = lines[lineIndex];
                    var visualLineNumber = lineIndex + 1;
                    DrawColoredText((document != null && document.HasHighlightedLine && document.HighlightedLine == visualLineNumber ? "> " : "  ") + visualLineNumber.ToString("D4"), document != null && document.HasHighlightedLine && document.HighlightedLine == visualLineNumber ? highlightColor : lineNumberColor);
                    DearImguiNative.igSameLine(0f, 8f);
                    DrawClassifiedLine(lineText, lineStart, classifications, textColor);
                    lineStart += lineText.Length + 1;
                }
            }
            DearImguiNative.igEndChild();
        }

        private void DrawClassifiedLine(string lineText, int lineStart, RendererClassifiedTextSpan[] classifications, DearImguiNative.ImVec4 defaultColor)
        {
            var drawnAny = false;
            var cursor = lineStart;
            var lineEnd = lineStart + (lineText != null ? lineText.Length : 0);
            if (classifications != null)
            {
                for (var i = 0; i < classifications.Length; i++)
                {
                    var span = classifications[i];
                    if (span == null || span.Length <= 0)
                    {
                        continue;
                    }

                    var start = Math.Max(span.Start, lineStart);
                    var end = Math.Min(span.Start + span.Length, lineEnd);
                    if (end <= start)
                    {
                        continue;
                    }

                    if (start > cursor)
                    {
                        DrawLineSegment(lineText, cursor - lineStart, start - cursor, defaultColor, ref drawnAny);
                    }

                    var tokenColor = ParseColor(_classificationService.GetHexColor(span.Classification, span.SemanticTokenType));
                    DrawLineSegment(lineText, start - lineStart, end - start, tokenColor, ref drawnAny);
                    cursor = end;
                }
            }

            if (cursor < lineEnd)
            {
                DrawLineSegment(lineText, cursor - lineStart, lineEnd - cursor, defaultColor, ref drawnAny);
            }

            if (!drawnAny)
            {
                DearImguiNative.igTextUnformatted(string.Empty, IntPtr.Zero);
            }
        }

        private void DrawLineSegment(string lineText, int start, int length, DearImguiNative.ImVec4 color, ref bool drawnAny)
        {
            if (length <= 0)
            {
                return;
            }

            var value = lineText.Substring(start, length);
            if (drawnAny)
            {
                DearImguiNative.igSameLine(0f, 0f);
            }

            DrawColoredText(value.Length == 0 ? " " : value, color);
            drawnAny = true;
        }

        private void DrawBuild(CortexShellController controller)
        {
            if (string.IsNullOrEmpty(_buildConfiguration))
            {
                _buildConfiguration = "Debug";
            }

            DearImguiNative.igSetNextItemWidth(120f);
            DrawInputText("Configuration##build", ref _buildConfiguration, 128);
            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Build", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ExecuteBuildFromRenderer(_buildConfiguration, false, false);
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Clean & Build", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ExecuteBuildFromRenderer(_buildConfiguration, true, false);
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Verify & Restart", new DearImguiNative.ImVec2(0f, 0f)))
            {
                controller.ExecuteBuildFromRenderer(_buildConfiguration, false, true);
            }

            DearImguiNative.igSeparator();
            var result = controller.GetLastBuildResultForRenderer();
            if (result == null)
            {
                DearImguiNative.igTextWrapped("No build has been run yet.");
                return;
            }

            DearImguiNative.igTextWrapped(
                "Last build: " +
                (result.Success ? "Success" : "Failure") +
                " | ExitCode=" + result.ExitCode +
                " | Duration=" + result.Duration.TotalSeconds.ToString("F2") + "s" +
                " | TimedOut=" + (result.TimedOut ? "Yes" : "No"));
            DearImguiNative.igSeparator();
            DrawPlainTextBlock("build.output", string.Join(Environment.NewLine, result.OutputLines.ToArray()));
        }

        private void DrawRuntime(CortexShellController controller)
        {
            var runtime = controller.GetLanguageRuntimeSnapshotForRenderer();
            DearImguiNative.igTextWrapped("Provider: " + (runtime.Provider != null ? runtime.Provider.DisplayName ?? runtime.Provider.ProviderId ?? string.Empty : string.Empty));
            DearImguiNative.igTextWrapped("Lifecycle: " + runtime.LifecycleState);
            DearImguiNative.igTextWrapped("Health: " + runtime.HealthState);
            DearImguiNative.igTextWrapped("Status: " + (runtime.StatusMessage ?? string.Empty));
            if (!string.IsNullOrEmpty(runtime.LastErrorSummary))
            {
                DearImguiNative.igTextWrapped("Last Error: " + runtime.LastErrorSummary);
            }

            DearImguiNative.igSeparator();
            var tools = controller.GetRuntimeToolsForRenderer();
            for (var i = 0; i < tools.Length; i++)
            {
                var tool = tools[i];
                if (DearImguiNative.igButton(tool.DisplayName ?? tool.ToolId, new DearImguiNative.ImVec2(0f, 0f)))
                {
                    controller.ExecuteRuntimeToolFromRenderer(tool.ToolId);
                }

                if (!string.IsNullOrEmpty(tool.Description))
                {
                    DearImguiNative.igTextWrapped(tool.Description);
                }
            }
        }

        private void DrawLogs(CortexShellController controller)
        {
            var entries = controller.ReadRecentRuntimeLogsForRenderer(200);
            if (entries == null || entries.Length == 0)
            {
                DearImguiNative.igTextWrapped("No runtime logs are available.");
                return;
            }

            if (DearImguiNative.igBeginChild_Str("logs.entries", new DearImguiNative.ImVec2(0f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    DearImguiNative.igTextWrapped(
                        entry.Timestamp.ToString("HH:mm:ss") + "  " +
                        (entry.Level ?? string.Empty) + "  " +
                        (entry.Source ?? entry.Category ?? string.Empty) + "  " +
                        (entry.Message ?? string.Empty));
                    DearImguiNative.igSeparator();
                }
            }
            DearImguiNative.igEndChild();
        }

        private static List<ToolRailItem> ResolveHostItems(WorkbenchPresentationSnapshot snapshot, RendererLayoutNodeModel node)
        {
            var resolved = new List<ToolRailItem>();
            if (snapshot == null || snapshot.ToolRailItems == null)
            {
                return resolved;
            }

            var hostLocationId = node != null ? node.HostLocationId ?? string.Empty : string.Empty;
            var containedIds = node != null ? node.ContainedContainerIds : null;
            for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
            {
                var item = snapshot.ToolRailItems[i];
                if (item == null)
                {
                    continue;
                }

                if (containedIds != null && containedIds.Length > 0)
                {
                    for (var containedIndex = 0; containedIndex < containedIds.Length; containedIndex++)
                    {
                        if (string.Equals(item.ContainerId, containedIds[containedIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            resolved.Add(item);
                            break;
                        }
                    }

                    continue;
                }

                if (string.Equals(item.HostLocation.ToString(), hostLocationId, StringComparison.OrdinalIgnoreCase))
                {
                    resolved.Add(item);
                }
            }

            return resolved;
        }

        private void DrawInlineStatus(WorkbenchPresentationSnapshot snapshot, WorkbenchBridgeSnapshot bridge, CortexShellController controller)
        {
            RenderStatusItems(snapshot.LeftStatusItems);
            if (snapshot.LeftStatusItems.Count > 0)
            {
                DearImguiNative.igSameLine(0f, 24f);
            }

            DearImguiNative.igTextUnformatted((bridge != null ? bridge.StatusMessage : controller.CurrentStatusMessage) ?? controller.CurrentStatusMessage ?? string.Empty, IntPtr.Zero);
            if (snapshot.RightStatusItems.Count > 0)
            {
                DearImguiNative.igSameLine(0f, 24f);
            }

            RenderStatusItems(snapshot.RightStatusItems);
        }

        private static void RenderStatusItems(IList<StatusItemContribution> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    DearImguiNative.igSameLine(0f, 16f);
                }

                DearImguiNative.igTextUnformatted(items[i] != null ? items[i].Text ?? string.Empty : string.Empty, IntPtr.Zero);
            }
        }

        private static List<KeyValuePair<string, List<MenuItemProjection>>> GroupMenuItems(IList<MenuItemProjection> items)
        {
            var orderedGroups = new List<KeyValuePair<string, List<MenuItemProjection>>>();
            if (items == null)
            {
                return orderedGroups;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                var groupName = string.IsNullOrEmpty(item.Group) ? "General" : item.Group;
                List<MenuItemProjection> groupItems = null;
                for (var groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
                {
                    if (string.Equals(orderedGroups[groupIndex].Key, groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        groupItems = orderedGroups[groupIndex].Value;
                        break;
                    }
                }

                if (groupItems == null)
                {
                    groupItems = new List<MenuItemProjection>();
                    orderedGroups.Add(new KeyValuePair<string, List<MenuItemProjection>>(groupName, groupItems));
                }

                groupItems.Add(item);
            }

            return orderedGroups;
        }

        private static string ResolveDraftValue(SettingsBridgeSnapshot snapshot, string settingId, string fallback)
        {
            if (snapshot != null && snapshot.DraftValues != null)
            {
                for (var i = 0; i < snapshot.DraftValues.Count; i++)
                {
                    var value = snapshot.DraftValues[i];
                    if (value != null && string.Equals(value.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                    {
                        return value.Value ?? string.Empty;
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        private bool DrawInputText(string id, ref string value, int capacity)
        {
            var buffer = GetOrCreateBuffer(id, value, capacity);
            var changed = DearImguiNative.igInputText(id, buffer, (uint)buffer.Length, DearImguiNative.ImGuiInputTextFlags.None, IntPtr.Zero, IntPtr.Zero);
            var decoded = DecodeBuffer(buffer);
            if (changed || !string.Equals(value ?? string.Empty, decoded, StringComparison.Ordinal))
            {
                value = decoded;
            }

            return changed;
        }

        private byte[] GetOrCreateBuffer(string id, string value, int capacity)
        {
            byte[] buffer;
            if (!_inputBuffers.TryGetValue(id, out buffer) || buffer == null || buffer.Length < capacity)
            {
                buffer = new byte[Math.Max(32, capacity)];
                _inputBuffers[id] = buffer;
                CopyToBuffer(buffer, value);
            }
            else
            {
                var current = DecodeBuffer(buffer);
                if (!string.Equals(current, value ?? string.Empty, StringComparison.Ordinal))
                {
                    CopyToBuffer(buffer, value);
                }
            }

            return buffer;
        }

        private static void CopyToBuffer(byte[] buffer, string value)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            Array.Clear(buffer, 0, buffer.Length);
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var count = Math.Min(bytes.Length, buffer.Length - 1);
            Array.Copy(bytes, buffer, count);
            buffer[count] = 0;
        }

        private static string DecodeBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return string.Empty;
            }

            var length = 0;
            while (length < buffer.Length && buffer[length] != 0)
            {
                length++;
            }

            return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : string.Empty;
        }

        private static void DrawPlainTextBlock(string id, string text)
        {
            if (DearImguiNative.igBeginChild_Str(id, new DearImguiNative.ImVec2(0f, 0f), false, DearImguiNative.ImGuiWindowFlags.None))
            {
                var lines = SplitLines(text);
                for (var i = 0; i < lines.Count; i++)
                {
                    DearImguiNative.igTextUnformatted(lines[i], IntPtr.Zero);
                }
            }

            DearImguiNative.igEndChild();
        }

        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            using (var reader = new StringReader(normalized))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            if (normalized.EndsWith("\n", StringComparison.Ordinal))
            {
                lines.Add(string.Empty);
            }

            return lines.Count > 0 ? lines : new List<string>(new[] { string.Empty });
        }

        private static string BuildSingleLinePreview(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", " ").Replace("\n", " ");
        }

        private static string GetScopeLabel(WorkbenchSearchScope scope)
        {
            switch (scope)
            {
                case WorkbenchSearchScope.AllOpenDocuments: return "Open Docs";
                case WorkbenchSearchScope.CurrentProject: return "Project";
                case WorkbenchSearchScope.Workspace: return "Workspace";
                default: return "Current Doc";
            }
        }

        private static DearImguiNative.ImVec4 ParseColor(string hex)
        {
            UnityEngine.Color parsed;
            var color = UnityEngine.ColorUtility.TryParseHtmlString(hex ?? string.Empty, out parsed)
                ? parsed
                : UnityEngine.Color.white;
            return ToImVec4(color);
        }

        private static DearImguiNative.ImVec4 ToImVec4(UnityEngine.Color color)
        {
            return new DearImguiNative.ImVec4
            {
                X = color.r,
                Y = color.g,
                Z = color.b,
                W = color.a
            };
        }

        private static void DrawColoredText(string text, DearImguiNative.ImVec4 color)
        {
            DearImguiNative.igPushStyleColor_Vec4(DearImguiNative.ImGuiCol.Text, color);
            DearImguiNative.igTextUnformatted(text ?? string.Empty, IntPtr.Zero);
            DearImguiNative.igPopStyleColor(1);
        }
    }
}

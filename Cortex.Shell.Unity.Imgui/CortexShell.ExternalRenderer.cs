using System;
using System.Collections.Generic;
using System.Linq;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Presentation.Runtime;
using Cortex.Services.Editor.Presentation;
using Cortex.Shell.Shared.Models;
using Cortex.Rendering.Models;
using Cortex.Shell.Unity.Imgui;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private readonly EditorPresentationService _externalEditorPresentationService = new EditorPresentationService();

        public string CurrentRendererId
        {
            get
            {
                var renderer = GetRenderPipeline().WorkbenchRenderer;
                return renderer != null ? renderer.RendererId ?? string.Empty : string.Empty;
            }
        }

        public string CurrentStatusMessage
        {
            get { return _state.StatusMessage ?? string.Empty; }
        }

        public bool IsShellVisibleForRenderer
        {
            get { return _sessionCoordinator.Visible; }
        }

        public bool PrepareRendererFrameForCurrentState()
        {
            return PrepareShellFrameForRender();
        }

        public WorkbenchPresentationSnapshot CreatePresentationSnapshotForRenderer()
        {
            return BuildPresentationSnapshot();
        }

        public RendererShellFrameModel CreateShellFrameForRenderer()
        {
            var mainWindow = _viewState.MainWindow != null ? _viewState.MainWindow : new Shell.CortexShellWindowViewState(126f, 28f, 920f, 580f);
            var logsWindow = _viewState.LogsWindow != null ? _viewState.LogsWindow : new Shell.CortexShellWindowViewState(110f, 26f, 760f, 420f);
            return new RendererShellFrameModel
            {
                IsVisible = _sessionCoordinator.Visible,
                ShowDetachedLogsWindow = _viewState.ShowDetachedLogsWindow,
                OnboardingActive = _state.Onboarding != null && _state.Onboarding.IsActive,
                MainWindow = CreateWindowModel(mainWindow, "Cortex IDE"),
                LogsWindow = CreateWindowModel(logsWindow, "Cortex Logs"),
                LayoutRoot = CreateLayoutNodeModel(_viewState.LayoutRoot)
            };
        }

        public WorkbenchBridgeSnapshot CreateWorkbenchBridgeSnapshotForRenderer()
        {
            if (_desktopBridgeSession == null)
            {
                return new WorkbenchBridgeSnapshot
                {
                    StatusMessage = _state.StatusMessage ?? string.Empty,
                    RuntimeConnectionState = "ready"
                };
            }

            _desktopBridgeSession.SynchronizeFromRuntime();
            return _desktopBridgeSession.BuildSnapshot();
        }

        public bool ApplyBridgeIntentForRenderer(BridgeIntentMessage intent)
        {
            if (_desktopBridgeSession == null || intent == null)
            {
                return false;
            }

            var result = _desktopBridgeSession.ApplyIntent(intent);
            if (!string.IsNullOrEmpty(result.StatusMessage))
            {
                _state.StatusMessage = result.StatusMessage;
            }

            return result.Status != BridgeOperationStatus.Rejected;
        }

        public bool ExecuteRendererCommand(string commandId)
        {
            return !string.IsNullOrEmpty(commandId) && ExecuteCommand(commandId, null);
        }

        public void ActivateContainerFromRenderer(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            ActivateContainer(containerId);
            _state.Workbench.FocusedContainerId = containerId;
            if (_workbenchRuntime != null)
            {
                _workbenchRuntime.WorkbenchState.ActiveContainerId = containerId;
                _workbenchRuntime.WorkbenchState.ActiveEditorGroupId = containerId;
                _workbenchRuntime.FocusState.FocusedRegionId = containerId;
            }
        }

        public string GetModuleSettingValueForRenderer(string settingId)
        {
            return ReadModuleSettingValue(_state.Settings, settingId);
        }

        public void SetModuleSettingValueForRenderer(string settingId, string serializedValue)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                return;
            }

            WriteModuleSettingValue(_state.Settings, settingId, serializedValue);
            _state.ReloadSettingsRequested = true;
            _state.StatusMessage = "Applying Cortex settings.";
        }

        public void FallbackDearImguiToImguiFromRenderer(string reason, string detail)
        {
            var resolvedReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            var resolvedDetail = string.IsNullOrEmpty(detail) ? string.Empty : detail;
            MMLog.WriteWarning(
                "[Cortex.DearImgui] Falling back to IMGUI. Reason=" + resolvedReason +
                ", Detail=" + resolvedDetail +
                ", ShellVisible=" + _sessionCoordinator.Visible +
                ", CurrentRenderer=" + (CurrentRendererId ?? string.Empty) +
                ", FocusedContainer=" + (_state.Workbench.FocusedContainerId ?? string.Empty) +
                ", ActiveContainer=" + (_workbenchRuntime != null && _workbenchRuntime.WorkbenchState != null ? _workbenchRuntime.WorkbenchState.ActiveContainerId ?? string.Empty : string.Empty) +
                ", FocusedRegion=" + (_workbenchRuntime != null && _workbenchRuntime.FocusState != null ? _workbenchRuntime.FocusState.FocusedRegionId ?? string.Empty : string.Empty) +
                ", StatusMessage=" + (_state.StatusMessage ?? string.Empty) + ".");

            WriteModuleSettingValue(_state.Settings, RenderHostPresentationIds.RenderHostSettingId, RenderHostPresentationIds.ImguiInProcessRenderHostId);
            _state.ReloadSettingsRequested = true;
            _state.StatusMessage = "Dear ImGui fell back to IMGUI: " + resolvedReason + ".";

            ApplyRuntimeUiFactory(ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(GetWorkbenchFrameContext()));
        }

        public RendererDocumentContentModel CreateActiveDocumentContentForRenderer()
        {
            var active = _state.Documents != null ? _state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return new RendererDocumentContentModel();
            }

            var codeArea = _externalEditorPresentationService.BuildCodeAreaPresentation(_state);
            var pathBar = _externalEditorPresentationService.BuildPathBarPresentation(null, _state);
            var statusBar = _externalEditorPresentationService.BuildStatusBarPresentation(_state);
            var spans = active.LanguageAnalysis != null && active.LanguageAnalysis.Classifications != null
                ? active.LanguageAnalysis.Classifications
                : null;
            var classifications = new RendererClassifiedTextSpan[spans != null ? spans.Length : 0];
            for (var i = 0; i < classifications.Length; i++)
            {
                var span = spans[i];
                classifications[i] = span == null
                    ? new RendererClassifiedTextSpan()
                    : new RendererClassifiedTextSpan
                    {
                        Start = span.Start,
                        Length = span.Length,
                        Classification = span.Classification ?? string.Empty,
                        SemanticTokenType = span.SemanticTokenType ?? string.Empty
                    };
            }

            return new RendererDocumentContentModel
            {
                FilePath = active.FilePath ?? string.Empty,
                DisplayName = Cortex.Modules.Shared.CortexModuleUtil.GetDocumentDisplayName(active),
                CompactPath = pathBar != null ? pathBar.CompactPath ?? string.Empty : string.Empty,
                Text = active.Text ?? string.Empty,
                IsReadOnly = active.IsReadOnly,
                IsDirty = active.IsDirty,
                TextVersion = active.TextVersion,
                HighlightedLine = pathBar != null ? pathBar.HighlightedLine : active.HighlightedLine,
                HasHighlightedLine = pathBar != null && pathBar.HasHighlightedLine,
                CaretLine = statusBar != null ? statusBar.Line : 0,
                CaretColumn = statusBar != null ? statusBar.Column : 0,
                LineCount = statusBar != null ? statusBar.LineCount : 0,
                LanguageStatusLabel = statusBar != null ? statusBar.LanguageStatusLabel ?? string.Empty : string.Empty,
                CompletionStatusLabel = statusBar != null ? statusBar.CompletionStatusLabel ?? string.Empty : string.Empty,
                Classifications = classifications
            };
        }

        public bool ActivateDocumentFromRenderer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var document = Cortex.Modules.Shared.CortexModuleUtil.FindOpenDocument(_state, filePath);
            if (document == null)
            {
                return false;
            }

            _state.Documents.ActiveDocument = document;
            _state.Documents.ActiveDocumentPath = document.FilePath ?? string.Empty;
            ActivateContainer(CortexWorkbenchIds.EditorContainer);
            _state.StatusMessage = "Activated " + (document.FilePath != null ? System.IO.Path.GetFileName(document.FilePath) : "document") + ".";
            return true;
        }

        public bool OpenDocumentFromRenderer(string filePath, int highlightedLine)
        {
            if (string.IsNullOrEmpty(filePath) || System.IO.Directory.Exists(filePath) || NavigationService == null)
            {
                return false;
            }

            var opened = NavigationService.OpenDocument(
                _state,
                filePath,
                highlightedLine,
                "Opened " + System.IO.Path.GetFileName(filePath) + ".",
                "Could not open " + filePath + ".");
            if (opened == null)
            {
                return false;
            }

            ActivateContainer(CortexWorkbenchIds.EditorContainer);
            return true;
        }

        public bool CloseDocumentFromRenderer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var existing = Cortex.Modules.Shared.CortexModuleUtil.FindOpenDocument(_state, filePath);
            if (existing == null)
            {
                return false;
            }

            Cortex.Modules.Shared.CortexModuleUtil.CloseDocument(_state, filePath);
            _state.StatusMessage = "Closed " + System.IO.Path.GetFileName(filePath) + ".";
            return true;
        }

        public RuntimeLogEntry[] ReadRecentRuntimeLogsForRenderer(int maxCount)
        {
            var logFeed = _platformModule != null ? _platformModule.RuntimeLogFeed : null;
            var entries = logFeed != null ? logFeed.ReadRecent("Info", Math.Max(1, maxCount)) : null;
            return entries != null ? entries.ToArray() : new RuntimeLogEntry[0];
        }

        public BuildResult GetLastBuildResultForRenderer()
        {
            return _state.LastBuildResult;
        }

        public bool ExecuteBuildFromRenderer(string configuration, bool clean, bool restartAfter)
        {
            if (_state.SelectedProject == null || _services == null || _services.BuildCommandResolver == null || _services.BuildExecutor == null)
            {
                _state.StatusMessage = "Build services are not available.";
                return false;
            }

            var resolvedConfiguration = !string.IsNullOrEmpty(configuration)
                ? configuration
                : (_state.Settings != null ? _state.Settings.DefaultBuildConfiguration : "Debug");
            var command = _services.BuildCommandResolver.Resolve(_state.SelectedProject, clean, resolvedConfiguration);
            if (command == null)
            {
                _state.StatusMessage = "No build command could be resolved.";
                return false;
            }

            command.TimeoutMs = _state.Settings != null ? _state.Settings.BuildTimeoutMs : 300000;
            _state.LastBuildResult = _services.BuildExecutor.Execute(command);
            if (_state.LastBuildResult == null)
            {
                _state.StatusMessage = "Build did not produce a result.";
                return false;
            }

            if (!_state.LastBuildResult.Success)
            {
                _state.StatusMessage = _state.LastBuildResult.TimedOut ? "Build timed out." : "Build failed.";
                return false;
            }

            _state.StatusMessage = "Build succeeded.";
            if (!restartAfter)
            {
                return true;
            }

            var restartCoordinator = _platformModule != null ? _platformModule.RestartCoordinator : null;
            var errorMessage = string.Empty;
            if (restartCoordinator != null && restartCoordinator.RequestCurrentSessionRestart(out errorMessage))
            {
                _state.StatusMessage = "Build verified. Restart requested.";
                return true;
            }

            _state.StatusMessage = "Restart failed: " + (errorMessage ?? "restart coordinator unavailable.");
            return false;
        }

        public LanguageRuntimeSnapshot GetLanguageRuntimeSnapshotForRenderer()
        {
            return _state.LanguageRuntime ?? new LanguageRuntimeSnapshot();
        }

        public RuntimeToolStatus[] GetRuntimeToolsForRenderer()
        {
            var bridge = _platformModule != null ? _platformModule.RuntimeToolBridge : null;
            var tools = bridge != null ? bridge.GetTools() : null;
            return tools != null ? tools.ToArray() : new RuntimeToolStatus[0];
        }

        public bool ExecuteRuntimeToolFromRenderer(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || _platformModule == null || _platformModule.RuntimeToolBridge == null)
            {
                return false;
            }

            string statusMessage;
            var executed = _platformModule.RuntimeToolBridge.Execute(toolId, out statusMessage);
            if (!string.IsNullOrEmpty(statusMessage))
            {
                _state.StatusMessage = statusMessage;
            }

            return executed;
        }

        private static string ReadModuleSettingValue(CortexSettings settings, string settingId)
        {
            if (settings == null || string.IsNullOrEmpty(settingId) || settings.ModuleSettings == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < settings.ModuleSettings.Length; i++)
            {
                var entry = settings.ModuleSettings[i];
                if (entry != null && string.Equals(entry.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static void WriteModuleSettingValue(CortexSettings settings, string settingId, string serializedValue)
        {
            if (settings == null || string.IsNullOrEmpty(settingId))
            {
                return;
            }

            var entries = new List<ModuleSettingValue>();
            if (settings.ModuleSettings != null)
            {
                for (var i = 0; i < settings.ModuleSettings.Length; i++)
                {
                    if (settings.ModuleSettings[i] != null)
                    {
                        entries.Add(settings.ModuleSettings[i]);
                    }
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    entries[i].Value = serializedValue ?? string.Empty;
                    settings.ModuleSettings = entries.ToArray();
                    return;
                }
            }

            entries.Add(new ModuleSettingValue
            {
                SettingId = settingId,
                Value = serializedValue ?? string.Empty
            });
            settings.ModuleSettings = entries.ToArray();
        }

        private static RendererShellWindowModel CreateWindowModel(Shell.CortexShellWindowViewState windowState, string title)
        {
            var rect = windowState != null && windowState.CurrentRect.Width > 0f
                ? windowState.CurrentRect
                : windowState != null
                    ? windowState.ExpandedRect
                    : new RenderRect(0f, 0f, 0f, 0f);
            return new RendererShellWindowModel
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                IsCollapsed = windowState != null && windowState.IsCollapsed,
                Title = title ?? string.Empty
            };
        }

        private static RendererLayoutNodeModel CreateLayoutNodeModel(CortexLayoutNode node)
        {
            if (node == null)
            {
                return null;
            }

            var containedIds = new string[node.ContainedModuleIds.Count];
            for (var i = 0; i < containedIds.Length; i++)
            {
                containedIds[i] = node.ContainedModuleIds[i] ?? string.Empty;
            }

            return new RendererLayoutNodeModel
            {
                NodeId = node.NodeId ?? string.Empty,
                SplitDirection = node.Split.ToString(),
                SplitRatio = node.SplitRatio,
                HostLocationId = node.HostLocation.ToString(),
                ActiveContainerId = node.ActiveModuleId ?? string.Empty,
                ContainedContainerIds = containedIds,
                FirstChild = CreateLayoutNodeModel(node.ChildA),
                SecondChild = CreateLayoutNodeModel(node.ChildB)
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class RuntimeUiArchitectureTests
    {
        private static readonly string RepoRoot = ResolveRepoRoot();

        [Fact]
        public void RuntimeUiSources_DoNotDependOnUnityOrImguiEventSemantics()
        {
            var runtimeUiRoot = Path.Combine(RepoRoot, "Cortex.Rendering.RuntimeUi");
            var sourceFiles = Directory
                .GetFiles(runtimeUiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();

            foreach (var sourceFile in sourceFiles)
            {
                var sourceText = File.ReadAllText(sourceFile);

                Assert.DoesNotContain("UnityEngine", sourceText);
                Assert.DoesNotContain("Event.current", sourceText);
                Assert.DoesNotContain("EventType.", sourceText);
                Assert.DoesNotContain("Input.GetAxis", sourceText);
                Assert.DoesNotContain("Input.GetAxisRaw", sourceText);
                Assert.DoesNotContain("GUI.", sourceText);
                Assert.DoesNotContain("GUILayout.", sourceText);
                Assert.DoesNotContain("Screen.", sourceText);
                Assert.DoesNotContain("Imgui", sourceText);
            }
        }

        [Fact]
        public void RenderingContractSources_DoNotDependOnUnityOrImguiSemantics()
        {
            var renderingRoot = Path.Combine(RepoRoot, "Cortex.Rendering");
            var sourceFiles = Directory
                .GetFiles(renderingRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();

            foreach (var sourceFile in sourceFiles)
            {
                var sourceText = File.ReadAllText(sourceFile);

                Assert.DoesNotContain("UnityEngine", sourceText);
                Assert.DoesNotContain("Event.current", sourceText);
                Assert.DoesNotContain("GUI.", sourceText);
                Assert.DoesNotContain("GUILayout.", sourceText);
                Assert.DoesNotContain("Screen.", sourceText);
                Assert.DoesNotContain("Imgui", sourceText);
            }
        }

        [Fact]
        public void FrameInputContracts_AreOwnedByRendering_AndConsumedByRuntimeUi()
        {
            var frameContractsText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Rendering", "Frame", "WorkbenchFrameContracts.cs"));
            var runtimeUiContractsText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Rendering.RuntimeUi", "Runtime", "WorkbenchRuntimeUiContracts.cs"));
            var runtimeUiRoot = Path.Combine(RepoRoot, "Cortex.Rendering.RuntimeUi");
            var runtimeUiSources = Directory
                .GetFiles(runtimeUiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();

            Assert.Contains("namespace Cortex.Rendering", frameContractsText);
            Assert.DoesNotContain("namespace Cortex.Rendering.RuntimeUi", frameContractsText);
            Assert.Contains("using Cortex.Rendering;", runtimeUiContractsText);

            foreach (var sourceFile in runtimeUiSources)
            {
                var sourceText = File.ReadAllText(sourceFile);

                Assert.DoesNotContain("interface IWorkbenchFrameContext", sourceText);
                Assert.DoesNotContain("struct WorkbenchFrameInputSnapshot", sourceText);
                Assert.DoesNotContain("enum WorkbenchInputEventKind", sourceText);
                Assert.DoesNotContain("enum WorkbenchInputKey", sourceText);
            }
        }

        [Fact]
        public void ImguiBackend_ConsumesPortableRuntimeUiPlanners()
        {
            var popupRendererText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiPopupMenuRenderer.cs"));
            var panelRendererText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiPanelRenderer.cs"));
            var hoverRendererText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiHoverTooltipRenderer.cs"));

            Assert.Contains("PopupMenuLayoutPlanner.BuildDrawLayout", popupRendererText);
            Assert.Contains("PopupMenuInteractionController", popupRendererText);
            Assert.Contains("RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput", popupRendererText);
            Assert.DoesNotContain("var y = -preparedFrame.ScrollOffset;", popupRendererText);
            Assert.DoesNotContain("thumbTravel =", popupRendererText);
            Assert.DoesNotContain("Input.GetAxis", popupRendererText);
            Assert.DoesNotContain("Event.current", popupRendererText);

            Assert.Contains("PanelLayoutPlanner.BuildRootLayout", panelRendererText);
            Assert.Contains("PanelLayoutPlanner.BuildContentLayout", panelRendererText);
            Assert.Contains("PanelLayoutPlanner.BuildMetadataContentLayout", panelRendererText);
            Assert.DoesNotContain("private CardContentLayout BuildCardContentLayout", panelRendererText);
            Assert.DoesNotContain("BuildMetadataContentLayout(content.RowRects", panelRendererText);

            Assert.Contains("HoverTooltipLayoutPlanner.BuildLayout", hoverRendererText);
            Assert.Contains("HoverTooltipInteractionController.HandlePartPointerInput", hoverRendererText);
            Assert.Contains("RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput", hoverRendererText);
            Assert.DoesNotContain("ResolveTooltipWidth(", hoverRendererText);
            Assert.DoesNotContain("CalculateHeight(", hoverRendererText);
            Assert.DoesNotContain("Event.current", hoverRendererText);
        }

        [Fact]
        public void RecordingProofBackend_AlsoConsumesPortableRuntimeUiPlanners()
        {
            var recordingBackendText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Tests", "Rendering", "RecordingRuntimeUiBackendTests.cs"));

            Assert.Contains("PanelLayoutPlanner.BuildRootLayout", recordingBackendText);
            Assert.Contains("PanelLayoutPlanner.BuildContentLayout", recordingBackendText);
            Assert.Contains("PopupMenuLayoutPlanner.BuildLayout", recordingBackendText);
            Assert.Contains("PopupMenuLayoutPlanner.BuildDrawLayout", recordingBackendText);
            Assert.Contains("PopupMenuInteractionController", recordingBackendText);
            Assert.Contains("HoverTooltipLayoutPlanner.BuildLayout", recordingBackendText);
            Assert.Contains("HoverTooltipInteractionController.HandlePartPointerInput", recordingBackendText);
            Assert.Contains("RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput", recordingBackendText);
        }

        [Fact]
        public void UnityImguiShell_ConsumesPortableRuntimeUiFrameContext()
        {
            var shellText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.cs"));
            var shellRuntimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.Runtime.cs"));
            var shellBootstrapperText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "ShellBootstrapper.cs"));

            Assert.Contains("IWorkbenchFrameContext", shellText);
            Assert.Contains("GetWorkbenchFrameContext()", shellText);
            Assert.Contains("GetWorkbenchFrameContext()", shellRuntimeText);
            Assert.Contains("IWorkbenchFrameContext", shellBootstrapperText);

            Assert.DoesNotContain("ICortexShellHostUi", shellText);
            Assert.DoesNotContain("ICortexShellHostUi", shellRuntimeText);
            Assert.DoesNotContain("ICortexShellHostUi", shellBootstrapperText);
        }

        [Fact]
        public void ShellChrome_ConsumesPortableShellUiPolicies_WhileKeepingImguiExecutionLocal()
        {
            var shellChromeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.Chrome.cs"));
            var shellLayoutText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "Shell", "ShellLayoutCoordinator.cs"));
            var shellOverlayText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "Shell", "ShellOverlayCoordinator.cs"));
            var shellOnboardingPresenterText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "Shell", "ShellOnboardingOverlayPresenter.cs"));

            Assert.Contains("ShellMenuPopupController.BuildPopupRect", shellChromeText);
            Assert.Contains("ShellMenuPopupController.EvaluateDismissal", shellChromeText);
            Assert.Contains("GUILayout.BeginArea", shellChromeText);

            Assert.Contains("ShellSplitLayoutPlanner.BuildHorizontal", shellLayoutText);
            Assert.Contains("ShellSplitLayoutPlanner.BuildVertical", shellLayoutText);
            Assert.Contains("CortexWindowChromeController.DrawVerticalSplitter", shellLayoutText);
            Assert.Contains("GUILayout.BeginArea", shellLayoutText);

            Assert.Contains("ShellOverlayInteractionController.ResolveInputCapture", shellOverlayText);
            Assert.Contains("ShellOnboardingOverlayPresenter", shellOverlayText);
            Assert.Contains("ShellOverlayInteractionController.EvaluateOnboardingInput", shellOnboardingPresenterText);
            Assert.Contains("GUI.ModalWindow", shellOnboardingPresenterText);
        }

        [Fact]
        public void GenericShellAndUnityHost_DoNotMentionImguiRuntimeTypes()
        {
            var genericDirectories = new[]
            {
                Path.Combine(RepoRoot, "Cortex"),
                Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui"),
                Path.Combine(RepoRoot, "Cortex.Host.Unity")
            };

            foreach (var sourceFile in EnumerateSourceFiles(genericDirectories))
            {
                var sourceText = File.ReadAllText(sourceFile);

                Assert.DoesNotContain("using Cortex.Renderers.Imgui;", sourceText);
                Assert.DoesNotContain("ImguiWorkbenchRuntimeUiFactory", sourceText);
                Assert.DoesNotContain("ImguiWorkbenchRuntimeUi", sourceText);
                Assert.DoesNotContain("ImguiRenderPipeline", sourceText);
            }
        }

        [Fact]
        public void WorkbenchUiSurfaceImplementation_IsHostOwned_NotShellOwned()
        {
            var shellRuntimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.Runtime.cs"));
            var moduleRenderServiceText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "CortexShellModuleRenderService.cs"));
            var shelteredCompositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredUnityHostComposition.cs"));
            var shelteredUiSurfaceText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredWorkbenchUiSurface.cs"));
            var runtimeUiFactoryText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiWorkbenchRuntimeUiFactory.cs"));
            var runtimeUiText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiWorkbenchRuntimeUi.cs"));

            Assert.False(File.Exists(Path.Combine(RepoRoot, "Cortex", "Layout", "CortexUi.cs")));
            Assert.DoesNotContain("CortexWorkbenchUiSurface", shellRuntimeText);
            Assert.DoesNotContain("new ShelteredWorkbenchUiSurface()", shellRuntimeText);
            Assert.DoesNotContain(": IWorkbenchUiSurface", moduleRenderServiceText);
            Assert.Contains("new ShelteredWorkbenchUiSurface()", shelteredCompositionText);
            Assert.Contains("ImguiWorkbenchRuntimeUiFactory", shelteredCompositionText);
            Assert.Contains("class ShelteredWorkbenchUiSurface : IWorkbenchUiSurface", shelteredUiSurfaceText);
            Assert.Contains("IWorkbenchUiSurface workbenchUiSurface", runtimeUiFactoryText);
            Assert.Contains("IWorkbenchFrameContext frameContext", runtimeUiFactoryText);
            Assert.Contains("return new ImguiWorkbenchRuntimeUi(_workbenchUiSurface, _frameContext);", runtimeUiFactoryText);
            Assert.Contains("_workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;", runtimeUiText);
            Assert.Contains("return runtimeUi != null ? runtimeUi.WorkbenchUiSurface : NullWorkbenchUiSurface.Instance;", shellRuntimeText);
        }

        [Fact]
        public void UnityHost_OwnsFrameContextAdaptation_AndSharesItWithRuntimeUi()
        {
            var unityFrameContextText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "UnityWorkbenchFrameContext.cs"));
            var shelteredCompositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredUnityHostComposition.cs"));
            var imguiRuntimeUiText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiWorkbenchRuntimeUi.cs"));

            Assert.Contains("using Cortex.Rendering;", unityFrameContextText);
            Assert.Contains("IWorkbenchFrameContext", unityFrameContextText);
            Assert.Contains("Event.current", unityFrameContextText);
            Assert.Contains("Input.mousePosition", unityFrameContextText);

            Assert.Contains("new UnityWorkbenchFrameContext()", shelteredCompositionText);
            Assert.Contains("ImguiWorkbenchRuntimeUiFactory", shelteredCompositionText);
            Assert.Contains("ShelteredWorkbenchUiSurface", shelteredCompositionText);
            Assert.Contains("frameContext", shelteredCompositionText);

            Assert.Contains("using Cortex.Rendering;", imguiRuntimeUiText);
            Assert.Contains("IWorkbenchFrameContext", imguiRuntimeUiText);
            Assert.Contains("NullWorkbenchFrameContext.Instance", imguiRuntimeUiText);
        }

        [Fact]
        public void RendererAndHostGuide_DocumentsConcreteExtensibilitySeams()
        {
            var guideText = File.ReadAllText(Path.Combine(RepoRoot, "documentation", "Cortex_Renderer_Host_Extensibility_Guide.md"));

            Assert.Contains("IWorkbenchRuntimeUiFactory", guideText);
            Assert.Contains("IWorkbenchFrameContext", guideText);
            Assert.Contains("WorkbenchFrameInputSnapshot", guideText);
            Assert.Contains("Cortex.Rendering", guideText);
            Assert.Contains("Cortex.Rendering.RuntimeUi", guideText);
            Assert.Contains("RuntimeUiPointerInputAdapter", guideText);
            Assert.Contains("Cortex.Host.Unity", guideText);
            Assert.Contains("Cortex.Renderers.Imgui", guideText);
            Assert.Contains("recording backend", guideText);
            Assert.Contains("Remaining Debt", guideText);
            Assert.Contains("shell split-layout", guideText);
        }

        private static string[] EnumerateSourceFiles(IEnumerable<string> directories)
        {
            return directories
                .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ResolveRepoRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Cortex.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate the repository root from the test base directory.");
        }
    }
}

using System;
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
        public void ShellGenericCode_ConsumesPortableRuntimeUiFrameContext()
        {
            var shellText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "CortexShell.cs"));
            var shellRuntimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "CortexShell.Runtime.cs"));
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
        public void UnityHost_OwnsFrameContextAdaptation_AndSharesItWithRuntimeUi()
        {
            var unityFrameContextText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "UnityWorkbenchFrameContext.cs"));
            var shelteredCompositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredUnityHostComposition.cs"));
            var imguiRuntimeUiText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Renderers.Imgui", "ImguiWorkbenchRuntimeUi.cs"));

            Assert.Contains("IWorkbenchFrameContext", unityFrameContextText);
            Assert.Contains("Event.current", unityFrameContextText);
            Assert.Contains("Input.mousePosition", unityFrameContextText);

            Assert.Contains("new UnityWorkbenchFrameContext()", shelteredCompositionText);
            Assert.Contains("ImguiWorkbenchRuntimeUiFactory", shelteredCompositionText);
            Assert.Contains("frameContext", shelteredCompositionText);

            Assert.Contains("IWorkbenchFrameContext", imguiRuntimeUiText);
            Assert.Contains("NullWorkbenchFrameContext.Instance", imguiRuntimeUiText);
        }

        [Fact]
        public void RendererAndHostGuide_DocumentsConcreteExtensibilitySeams()
        {
            var guideText = File.ReadAllText(Path.Combine(RepoRoot, "documentation", "Cortex_Renderer_Host_Extensibility_Guide.md"));

            Assert.Contains("IWorkbenchRuntimeUiFactory", guideText);
            Assert.Contains("IWorkbenchFrameContext", guideText);
            Assert.Contains("RuntimeUiPointerInputAdapter", guideText);
            Assert.Contains("Cortex.Host.Unity", guideText);
            Assert.Contains("Cortex.Renderers.Imgui", guideText);
            Assert.Contains("recording backend", guideText);
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

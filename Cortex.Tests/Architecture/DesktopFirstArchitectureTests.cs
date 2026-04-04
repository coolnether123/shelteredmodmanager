using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class DesktopFirstArchitectureTests
    {
        private static readonly string[] HostSpecificProjectNames =
        {
            "Cortex",
            "Cortex.Host.Avalonia",
            "Cortex.Shell.Unity.Imgui",
            "Cortex.Host.Sheltered",
            "Cortex.Host.Unity",
            "Cortex.Platform.ModAPI",
            "Cortex.Renderers.Imgui"
        };

        private static readonly string[] ToolingProjectNames =
        {
            "Cortex.PathPicker.Host",
            "Cortex.Roslyn.Worker",
            "Cortex.Tabby.Server"
        };

        [Fact]
        public void DesktopSharedProject_RemainsHostNeutral_AndFreeOfUnityOrImguiTokens()
        {
            var projectText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Contracts", "Cortex.Contracts.csproj");
            var markerText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Contracts", "AssemblyMarker.cs");
            var protocolText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Contracts", "LanguageService", "LanguageServiceProtocol.cs");
            var completionText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Contracts", "Completion", "CompletionAugmentationPromptContract.cs");
            var semanticText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Contracts", "Text", "SemanticTokenClassification.cs");
            var propsText = ArchitectureTestEnvironment.ReadRepoFile("Directory.Build.props");
            var referencedProjects = ArchitectureTestEnvironment.LoadProjectReferenceNames("Cortex.Contracts");

            Assert.Contains("<TargetFrameworks>net35;net8.0</TargetFrameworks>", projectText);
            Assert.DoesNotContain("<TargetFramework>netstandard2.0</TargetFramework>", projectText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Contracts'", propsText);
            Assert.Contains("<CortexProjectRole>DesktopShared</CortexProjectRole>", propsText);
            Assert.Empty(referencedProjects.Where(name => HostSpecificProjectNames.Contains(name, StringComparer.Ordinal)));
            Assert.Contains("namespace Cortex.LanguageService.Protocol", protocolText);
            Assert.Contains("namespace Cortex.Contracts.Completion", completionText);
            Assert.Contains("namespace Cortex.Contracts.Text", semanticText);
            Assert.DoesNotContain("UnityEngine", markerText);
            Assert.DoesNotContain("UnityEngine", protocolText);
            Assert.DoesNotContain("UnityEngine", completionText);
            Assert.DoesNotContain("UnityEngine", semanticText);
            Assert.DoesNotContain("Imgui", markerText);
            Assert.DoesNotContain("Imgui", protocolText);
            Assert.DoesNotContain("Imgui", completionText);
            Assert.DoesNotContain("Imgui", semanticText);
            Assert.DoesNotContain("Sheltered", markerText);
            Assert.DoesNotContain("Sheltered", protocolText);
            Assert.DoesNotContain("Sheltered", completionText);
            Assert.DoesNotContain("Sheltered", semanticText);
            Assert.DoesNotContain("ModAPI", markerText);
            Assert.DoesNotContain("ModAPI", protocolText);
            Assert.DoesNotContain("ModAPI", completionText);
            Assert.DoesNotContain("ModAPI", semanticText);
        }

        [Fact]
        public void DesktopSharedShellProject_RemainsHostNeutral_AndDesktopConsumable()
        {
            var projectText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Shared", "Cortex.Shell.Shared.csproj");
            var propsText = ArchitectureTestEnvironment.ReadRepoFile("Directory.Build.props");
            var referencedProjects = ArchitectureTestEnvironment.LoadProjectReferenceNames("Cortex.Shell.Shared");
            var sourceFiles = ArchitectureTestEnvironment.GetProjectSourceFiles("Cortex.Shell.Shared");

            Assert.Contains("<TargetFrameworks>net35;net8.0</TargetFrameworks>", projectText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Shell.Shared'", propsText);
            Assert.Contains("<CortexProjectRole>DesktopShared</CortexProjectRole>", propsText);
            Assert.Empty(referencedProjects.Where(name => HostSpecificProjectNames.Contains(name, StringComparer.Ordinal)));
            Assert.All(sourceFiles, sourcePath =>
            {
                var sourceText = File.ReadAllText(sourcePath);
                Assert.DoesNotContain("UnityEngine", sourceText);
                Assert.DoesNotContain("Imgui", sourceText);
                Assert.DoesNotContain("Avalonia", sourceText);
                Assert.DoesNotContain("Dock", sourceText);
                Assert.DoesNotContain("Sheltered", sourceText);
                Assert.DoesNotContain("ModAPI", sourceText);
            });
        }

        [Fact]
        public void DesktopBridgeProject_RemainsHostNeutral_AndDependsOnlyOnSharedShellContracts()
        {
            var projectText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Bridge", "Cortex.Bridge.csproj");
            var bridgeText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Bridge", "BridgeMessageModels.cs");
            var propsText = ArchitectureTestEnvironment.ReadRepoFile("Directory.Build.props");
            var referencedProjects = ArchitectureTestEnvironment.LoadProjectReferenceNames("Cortex.Bridge");
            var sourceFiles = ArchitectureTestEnvironment.GetProjectSourceFiles("Cortex.Bridge");

            Assert.Contains("<TargetFrameworks>net35;net8.0</TargetFrameworks>", projectText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Bridge'", propsText);
            Assert.Contains("<CortexProjectRole>DesktopShared</CortexProjectRole>", propsText);
            Assert.Contains("Cortex.Shell.Shared", referencedProjects);
            Assert.DoesNotContain("Cortex.Host.Avalonia", referencedProjects);
            Assert.DoesNotContain("Cortex", referencedProjects);
            Assert.Contains("BridgeMessageType", bridgeText);
            Assert.Contains("BridgeIntentType", bridgeText);
            Assert.Contains("PipeNameEnvironmentVariable", bridgeText);
            Assert.DoesNotContain("Cortex.Host.Avalonia", bridgeText);
            Assert.All(sourceFiles, sourcePath =>
            {
                var sourceText = File.ReadAllText(sourcePath);
                Assert.DoesNotContain("UnityEngine", sourceText);
                Assert.DoesNotContain("Imgui", sourceText);
                Assert.DoesNotContain("Avalonia", sourceText);
                Assert.DoesNotContain("Dock", sourceText);
                Assert.DoesNotContain("Sheltered", sourceText);
                Assert.DoesNotContain("ModAPI", sourceText);
            });
        }

        [Fact]
        public void DesktopAvaloniaHost_UsesSharedShellContracts_AndStructuredLogging()
        {
            var projectText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Cortex.Host.Avalonia.csproj");
            var bridgeClientText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Bridge", "NamedPipeDesktopBridgeClient.cs");
            var loggingText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Logging", "DesktopHostLogging.cs");
            var appText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "App.axaml.cs");
            var startupServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Composition", "DesktopSessionStartupService.cs");
            var pathPolicyText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Composition", "DesktopHostPathPolicy.cs");
            var sessionText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Composition", "DesktopHostApplicationSession.cs");
            var compositionRootText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Composition", "DesktopHostCompositionRoot.cs");
            var shellStateStoreText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Services", "DesktopShellStateStore.cs");
            var dockLayoutPersistenceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Services", "DesktopDockLayoutPersistenceService.cs");
            var compositionServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Services", "DesktopWorkbenchCompositionService.cs");
            var surfaceRegistryText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Services", "DesktopWorkbenchSurfaceRegistry.cs");
            var appStylesText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "App.axaml");
            var shellText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "MainWindow.axaml");
            var mainWindowText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "MainWindow.axaml.cs");
            var dockFactoryText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Services", "DesktopWorkbenchDockFactory.cs");
            var editorViewText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Views", "EditorDocumentView.axaml");
            var searchViewText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Views", "SearchToolView.axaml");
            var referenceViewText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Views", "ReferenceToolView.axaml");
            var statusViewText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Host.Avalonia", "Views", "StatusToolView.axaml");
            var references = ArchitectureTestEnvironment.LoadProjectReferenceNames("Cortex.Host.Avalonia");

            Assert.Contains("<TargetFramework>net8.0</TargetFramework>", projectText);
            Assert.Contains("Avalonia", projectText);
            Assert.Contains("Dock.Avalonia", projectText);
            Assert.Contains("Dock.Model.Mvvm", projectText);
            Assert.Contains("Serilog", projectText);
            Assert.Contains(@"..\Cortex.Bridge\Cortex.Bridge.csproj", projectText);
            Assert.Contains(@"..\Cortex.Shell.Shared\Cortex.Shell.Shared.csproj", projectText);
            Assert.Contains("Cortex.Bridge", references);
            Assert.Contains("Cortex.Shell.Shared", references);
            Assert.DoesNotContain("Cortex.Host.Unity", references);
            Assert.DoesNotContain("Cortex.Host.Sheltered", references);
            Assert.DoesNotContain("Cortex.Shell.Unity.Imgui", references);
            Assert.Contains("WriteTo.File", loggingText);
            Assert.Contains("WriteTo.Debug", loggingText);
            Assert.Contains("DesktopSessionStartupService", appText);
            Assert.Contains("CreateMainWindow()", appText);
            Assert.DoesNotContain("--pipe-name", appText);
            Assert.DoesNotContain("CORTEX_DESKTOP_BRIDGE_PIPE_NAME", appText);
            Assert.Contains("ResolvePipeName", startupServiceText);
            Assert.Contains("--pipe-name", startupServiceText);
            Assert.Contains("CORTEX_DESKTOP_BRIDGE_PIPE_NAME", startupServiceText);
            Assert.Contains("ResolveDataRootPath", pathPolicyText);
            Assert.Contains("ResolveLogFilePath", pathPolicyText);
            Assert.Contains("ResolveShellStateFilePath", pathPolicyText);
            Assert.Contains("ResolveDockLayoutFilePath", pathPolicyText);
            Assert.Contains("DesktopHostLogging.Initialize", sessionText);
            Assert.Contains("DesktopHostLogging.Dispose", sessionText);
            Assert.Contains("DesktopHostOptions", compositionRootText);
            Assert.Contains("NamedPipeDesktopBridgeClient", compositionRootText);
            Assert.Contains("DesktopShellStateStore", compositionRootText);
            Assert.Contains("DesktopWorkbenchCompositionService", compositionRootText);
            Assert.Contains("JsonSerializer", shellStateStoreText);
            Assert.Contains("DesktopShellState", shellStateStoreText);
            Assert.Contains("desktop-shell-state.json", pathPolicyText);
            Assert.Contains("desktop-dock-layout.json", pathPolicyText);
            Assert.Contains("DesktopDockLayoutState", dockLayoutPersistenceText);
            Assert.Contains("IRootDock", dockLayoutPersistenceText);
            Assert.Contains("DesktopWorkbenchSurfaceRegistry", compositionServiceText);
            Assert.Contains("DesktopWorkbenchDockFactory", compositionServiceText);
            Assert.Contains("WorkspaceSurfaceId", surfaceRegistryText);
            Assert.Contains("EditorSurfaceId", surfaceRegistryText);
            Assert.Contains("ReferenceSurfaceId", surfaceRegistryText);
            Assert.Contains("SearchSurfaceId", surfaceRegistryText);
            Assert.Contains("StatusSurfaceId", surfaceRegistryText);
            Assert.Contains("DockFluentTheme", appStylesText);
            Assert.Contains("<dock:DockControl", shellText);
            Assert.Contains("Dock-owned structure", shellText);
            Assert.Contains("SurfaceToggles", shellText);
            Assert.Contains("SaveLayout_OnClick", shellText);
            Assert.Contains("ResetLayout_OnClick", shellText);
            Assert.Contains("ApplyWorkbenchLayout()", mainWindowText);
            Assert.Contains("WorkbenchDockControl.Layout = composition.RootDock;", mainWindowText);
            Assert.Contains("CreateToolDock()", dockFactoryText);
            Assert.Contains("CreateDocumentDock()", dockFactoryText);
            Assert.Contains("CreateProportionalDock()", dockFactoryText);
            Assert.Contains("CreateRootDock()", dockFactoryText);
            Assert.Contains("EditorDocumentView", dockFactoryText);
            Assert.Contains("SearchToolView", dockFactoryText);
            Assert.Contains("ReferenceToolView", dockFactoryText);
            Assert.Contains("StatusToolView", dockFactoryText);
            Assert.Contains("Open Documents", editorViewText);
            Assert.Contains("SearchResultsListBox", searchViewText);
            Assert.Contains("ReferenceTargetDisplayName", referenceViewText);
            Assert.Contains("Runtime and Host Status", statusViewText);
            Assert.Contains("DesktopBridgeClientOptions", bridgeClientText);
            Assert.Contains("NamedPipeClientStream", bridgeClientText);
            Assert.Contains("ClientDisplayName", bridgeClientText);
            Assert.Contains("BridgeMessageType.OpenSessionRequest", bridgeClientText);
        }

        [Fact]
        public void ExternalToolProjects_RemainOutOfProcess_AndAreNotReferencedByHostProjects()
        {
            foreach (var projectName in ToolingProjectNames)
            {
                var projectText = File.ReadAllText(ArchitectureTestEnvironment.GetProjectPath(projectName));

                Assert.Contains("<OutputType>", projectText);
                Assert.Contains("<TargetFramework>net8.0", projectText);
                Assert.DoesNotContain("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", projectText);
            }

            foreach (var projectName in HostSpecificProjectNames)
            {
                var referencedProjects = ArchitectureTestEnvironment.LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(name => ToolingProjectNames.Contains(name, StringComparer.Ordinal)).ToArray();

                Assert.True(
                    violations.Length == 0,
                    projectName + " references external tools in-process: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void DesktopFirstDirection_IsExplicitAcrossArchitectureDocs()
        {
            var architectureGuideText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Architecture_Guide.md");
            var portabilityReportText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Portability_Report.md");
            var buildTopologyGuideText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Build_Topology_Guide.md");
            var guardrailText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Runtime_Shell_Separation_Guardrails.md");
            var hostGuideText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Avalonia_Host_Guide.md");
            var bridgeGuideText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Desktop_Bridge_Guide.md");

            Assert.Contains("desktop-first", architectureGuideText);
            Assert.Contains("legacy host/shell path", architectureGuideText);
            Assert.Contains("Cortex.Bridge", architectureGuideText);
            Assert.Contains("Cortex.Contracts", architectureGuideText);
            Assert.Contains("Cortex.Shell.Shared", architectureGuideText);
            Assert.Contains("Cortex.Host.Avalonia", architectureGuideText);
            Assert.Contains("Avalonia", architectureGuideText);
            Assert.Contains("Serilog", architectureGuideText);
            Assert.Contains("Dock", architectureGuideText);
            Assert.Contains("named-pipe bridge", architectureGuideText);

            Assert.Contains("desktop-first architecture", portabilityReportText);
            Assert.Contains("net35", portabilityReportText);
            Assert.Contains("legacy shell/backend", portabilityReportText);
            Assert.Contains("Cortex.Bridge", portabilityReportText);
            Assert.Contains("Cortex.Contracts", portabilityReportText);
            Assert.Contains("Cortex.Shell.Shared", portabilityReportText);
            Assert.Contains("Cortex.Host.Avalonia", portabilityReportText);
            Assert.Contains("External-Tool Cortex", portabilityReportText);
            Assert.Contains("Dock", portabilityReportText);
            Assert.Contains("named pipe", portabilityReportText);

            Assert.Contains("Desktop-shareable contracts and models", buildTopologyGuideText);
            Assert.Contains("Legacy Unity IMGUI host path", buildTopologyGuideText);
            Assert.Contains("External workers and tools", buildTopologyGuideText);
            Assert.Contains("Desktop host lane", buildTopologyGuideText);
            Assert.Contains("Cortex.Bridge", buildTopologyGuideText);
            Assert.Contains("Cortex.Host.Avalonia", buildTopologyGuideText);
            Assert.Contains("Dock", buildTopologyGuideText);
            Assert.Contains("host\\lib\\", buildTopologyGuideText);

            Assert.Contains("Desktop-first direction for this phase", guardrailText);
            Assert.Contains("legacy concrete host/shell/backend path", guardrailText);
            Assert.Contains("Cortex.Bridge", guardrailText);
            Assert.Contains("Cortex.Shell.Shared", guardrailText);
            Assert.Contains("bridge snapshot publishing and semantic intent handling", guardrailText);
            Assert.Contains("Dock", guardrailText);

            Assert.Contains("Cortex.Host.Avalonia", hostGuideText);
            Assert.Contains("Cortex.Bridge", hostGuideText);
            Assert.Contains("cortex-desktop.log", hostGuideText);
            Assert.Contains("dotnet run --project", hostGuideText);
            Assert.Contains("Dock", hostGuideText);
            Assert.Contains("DesktopSessionStartupService", hostGuideText);
            Assert.Contains("DesktopHostApplicationSession", hostGuideText);
            Assert.Contains("DesktopShellStateStore", hostGuideText);
            Assert.Contains("DesktopDockLayoutPersistenceService", hostGuideText);
            Assert.Contains("desktop-shell-state.json", hostGuideText);
            Assert.Contains("desktop-dock-layout.json", hostGuideText);
            Assert.Contains("CORTEX_DESKTOP_BRIDGE_PIPE_NAME", hostGuideText);
            Assert.Contains("legacy runtime process", hostGuideText);

            Assert.Contains("out-of-process desktop bridge", bridgeGuideText);
            Assert.Contains("WorkbenchBridgeSnapshot", bridgeGuideText);
            Assert.Contains("BridgeIntentMessage", bridgeGuideText);
            Assert.Contains("named pipe", bridgeGuideText);
            Assert.Contains("CORTEX_DESKTOP_BRIDGE_PIPE_NAME", bridgeGuideText);
            Assert.Contains("RuntimeDesktopBridgeWorkbenchFeature", bridgeGuideText);
            Assert.Contains("SearchWorkbenchModel", bridgeGuideText);
            Assert.Contains("ReferenceWorkbenchModel", bridgeGuideText);
        }
    }
}

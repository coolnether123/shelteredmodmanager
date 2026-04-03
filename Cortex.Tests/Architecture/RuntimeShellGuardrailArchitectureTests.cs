using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class RuntimeShellGuardrailArchitectureTests
    {
        private static readonly string[] PortableRuntimeFacingProjectNames =
        {
            "Cortex.CompletionProviders",
            "Cortex.Core",
            "Cortex.Ollama",
            "Cortex.OpenRouter",
            "Cortex.Plugins.Abstractions",
            "Cortex.Presentation",
            "Cortex.Rendering",
            "Cortex.Rendering.RuntimeUi",
            "Cortex.Tabby"
        };

        private static readonly string[] HostSpecificProjectNames =
        {
            "Cortex",
            "Cortex.Shell.Unity.Imgui",
            "Cortex.Host.Sheltered",
            "Cortex.Host.Unity",
            "Cortex.Platform.ModAPI",
            "Cortex.Renderers.Imgui"
        };

        [Fact]
        public void PortableRuntimeFacingSources_DoNotDependOnUnityOrImguiExecutionSemantics()
        {
            var disallowedTokens = new[]
            {
                "UnityEngine",
                "Event.current",
                "EventType.",
                "GUI.",
                "GUILayout.",
                "Input.GetAxis",
                "Input.GetAxisRaw",
                "Imgui"
            };

            AssertSourcesDoNotContainTokens(PortableRuntimeFacingProjectNames, disallowedTokens);
        }

        [Fact]
        public void PortableRuntimeFacingProjects_DoNotReferenceHostSpecificCortexProjects()
        {
            var hostSpecificProjects = new HashSet<string>(HostSpecificProjectNames, StringComparer.Ordinal);

            foreach (var projectName in PortableRuntimeFacingProjectNames)
            {
                var referencedProjects = ArchitectureTestEnvironment.LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(hostSpecificProjects.Contains).ToArray();

                Assert.True(
                    violations.Length == 0,
                    projectName + " references host-specific Cortex projects: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void PortableGenericSources_DoNotUseUnityOrHostSpecificRuntimeTypeNames()
        {
            var disallowedTokens = new[]
            {
                "UnityWorkbenchRuntime",
                "UnityWorkbenchFrameContext",
                "ImguiRenderPipeline",
                "ImguiWorkbenchRuntimeUi",
                "ShelteredWorkbenchUiSurface",
                "ShelteredUnityHostComposition",
                "ModApiCortexRuntimeBootstrap",
                "using Cortex.Renderers.Imgui;",
                "using Cortex.Host.Unity;",
                "using Cortex.Host.Sheltered;",
                "using Cortex.Platform.ModAPI;"
            };

            AssertSourcesDoNotContainTokens(PortableRuntimeFacingProjectNames, disallowedTokens);
        }

        [Fact]
        public void PresentationBoundary_UsesShellOwnedSnapshotAssembly_InsteadOfRuntimeSnapshotFactories()
        {
            var interfacesText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Presentation", "Abstractions", "Interfaces.cs");
            var presenterText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Presentation", "Services", "WorkbenchPresenter.cs");
            var shellText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Unity.Imgui", "CortexShell.cs");
            var shellRuntimeText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Unity.Imgui", "CortexShell.Runtime.cs");

            Assert.DoesNotContain("WorkbenchPresentationSnapshot CreateSnapshot()", interfacesText);
            Assert.Contains("BuildSnapshot(IWorkbenchRuntime runtime, WorkbenchPresentationMetadata metadata)", interfacesText);
            Assert.Contains("BuildSnapshot(IWorkbenchRuntime runtime, WorkbenchPresentationMetadata metadata)", presenterText);
            Assert.Contains("BuildPresentationSnapshot()", shellText);
            Assert.Contains("_snapshotPresenter.BuildSnapshot(_workbenchRuntime, BuildPresentationMetadata())", shellRuntimeText);
        }

        [Fact]
        public void ShellGeometryBoundary_MovesWindowChromeAndDetachedLogsOutOfGenericShellState()
        {
            var shellStateText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "State", "CortexShellState.cs");
            var shellViewStateText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Shell", "CortexShellViewState.cs");
            var shellLayoutText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Unity.Imgui", "Shell", "ShellLayoutCoordinator.cs");

            Assert.DoesNotContain("CortexWindowChromeState", shellStateText);
            Assert.DoesNotContain("CortexWindowChromeWorkspaceState", shellStateText);
            Assert.DoesNotContain("ShowDetachedWindow", shellStateText);
            Assert.DoesNotContain("LayoutRoot", shellStateText);
            Assert.Contains("CortexShellViewState", shellViewStateText);
            Assert.Contains("RenderRect", shellViewStateText);
            Assert.Contains("ShowDetachedLogsWindow", shellViewStateText);
            Assert.Contains("MainWindow", shellViewStateText);
            Assert.Contains("LogsWindow", shellViewStateText);
            Assert.DoesNotContain("using UnityEngine;", shellViewStateText);
            Assert.Contains("SynchronizeRuntimeLayoutState()", shellLayoutText);
            Assert.DoesNotContain("runtime.WorkbenchState.PrimarySideHostVisible = leftVisible", shellLayoutText);
            Assert.DoesNotContain("runtime.WorkbenchState.SecondarySideHostVisible = rightVisible", shellLayoutText);
            Assert.DoesNotContain("runtime.WorkbenchState.PanelHostVisible = panelVisible", shellLayoutText);
        }

        [Fact]
        public void OnboardingBoundary_KeepsCoordinatorHeadless_AndMovesOverlayDrawingToShellPresenter()
        {
            var coordinatorText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Onboarding", "CortexOnboardingCoordinator.cs");
            var flowServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Onboarding", "CortexOnboardingFlowService.cs");
            var shellOverlayText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Unity.Imgui", "Shell", "ShellOverlayCoordinator.cs");
            var shellPresenterText = ArchitectureTestEnvironment.ReadRepoFile("Cortex.Shell.Unity.Imgui", "Shell", "ShellOnboardingOverlayPresenter.cs");
            var onboardingModuleText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Modules", "Onboarding", "OnboardingModule.cs");

            Assert.DoesNotContain("using UnityEngine;", coordinatorText);
            Assert.DoesNotContain("using Cortex.Modules.Onboarding;", coordinatorText);
            Assert.DoesNotContain("Rect ", coordinatorText);
            Assert.DoesNotContain("DrawModalContent(", coordinatorText);
            Assert.DoesNotContain("OnboardingModule", coordinatorText);
            Assert.Contains("BuildCatalog(IContributionRegistry contributionRegistry)", coordinatorText);
            Assert.Contains("CortexOnboardingService InteractionService", coordinatorText);
            Assert.Contains("class CortexOnboardingFlowService", flowServiceText);
            Assert.DoesNotContain("using UnityEngine;", flowServiceText);
            Assert.Contains("CortexOnboardingFlowService", onboardingModuleText);
            Assert.DoesNotContain("BuildSteps(", onboardingModuleText);

            Assert.Contains("ShellOnboardingOverlayPresenter", shellOverlayText);
            Assert.Contains("OnboardingModule", shellPresenterText);
            Assert.Contains("GUI.ModalWindow", shellPresenterText);
            Assert.Contains("ShellOverlayInteractionController.BuildOnboardingModalRect", shellPresenterText);
            Assert.Contains("ShellOverlayInteractionController.EvaluateOnboardingInput", shellPresenterText);
        }

        [Fact]
        public void SettingsAndEditorBoundaries_MoveHeadlessBehaviorIntoServices()
        {
            var settingsModuleText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Modules", "Settings", "SettingsModule.cs");
            var settingsApplicationServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Settings", "SettingsApplicationService.cs");
            var settingsDraftServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Settings", "SettingsDraftService.cs");
            var settingsCollectionServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Settings", "SettingsContributionCollectionService.cs");
            var settingsSessionServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Settings", "SettingsSessionService.cs");
            var editorModuleText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Modules", "Editor", "EditorModule.cs");
            var editorPresentationServiceText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Services", "Editor", "Presentation", "EditorPresentationService.cs");

            Assert.Contains("SettingsDraftService", settingsModuleText);
            Assert.Contains("SettingsApplicationService", settingsModuleText);
            Assert.Contains("SettingsContributionCollectionService", settingsModuleText);
            Assert.Contains("SettingsSessionService", settingsModuleText);
            Assert.Contains("_documentBuilder.BuildDocument(snapshot)", settingsModuleText);
            Assert.DoesNotContain("private SettingsDocument BuildDocument(", settingsModuleText);
            Assert.DoesNotContain("private void Apply(", settingsModuleText);
            Assert.DoesNotContain("private SettingValidationResult BuildDefaultValidationResult(", settingsModuleText);
            Assert.Contains("class SettingsApplicationService", settingsApplicationServiceText);
            Assert.Contains("class SettingsDraftService", settingsDraftServiceText);
            Assert.Contains("class SettingsContributionCollectionService", settingsCollectionServiceText);
            Assert.Contains("class SettingsSessionService", settingsSessionServiceText);

            Assert.Contains("EditorPresentationService", editorModuleText);
            Assert.Contains("_presentationService.BuildStatusBarPresentation(state)", editorModuleText);
            Assert.Contains("_presentationService.ResolveSearchShortcutCommand(", editorModuleText);
            Assert.DoesNotContain("private static string BuildLanguageRuntimeLabel(", editorModuleText);
            Assert.DoesNotContain("private static string BuildCompletionAugmentationLabel(", editorModuleText);
            Assert.Contains("class EditorPresentationService", editorPresentationServiceText);
        }

        [Fact]
        public void RuntimeCompositionBoundary_MovesHeadlessStartupOutOfShellBootstrapper()
        {
            var shellBootstrapperText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Shell", "ShellBootstrapper.cs");
            var runtimeCompositionText = ArchitectureTestEnvironment.ReadRepoFile("Cortex", "Runtime", "CortexRuntimeCompositionService.cs");

            Assert.Contains("CortexRuntimeCompositionService", shellBootstrapperText);
            Assert.Contains("ApplyShellWindowSettings", shellBootstrapperText);
            Assert.DoesNotContain("new JsonCortexSettingsStore", shellBootstrapperText);
            Assert.DoesNotContain("new ProjectCatalog", shellBootstrapperText);

            Assert.Contains("class CortexRuntimeCompositionService", runtimeCompositionText);
            Assert.Contains("InitializeSettings()", runtimeCompositionText);
            Assert.Contains("InitializeWorkbenchRuntime(", runtimeCompositionText);
            Assert.Contains("InitializeServices(CortexSettings settings)", runtimeCompositionText);
            Assert.DoesNotContain("CortexShellViewState", runtimeCompositionText);
            Assert.DoesNotContain("using UnityEngine;", runtimeCompositionText);
        }

        [Fact]
        public void RuntimeShellGuardrailDoc_DocumentsCurrentViolationsTargetBoundariesAndTestCoverage()
        {
            var docText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Runtime_Shell_Separation_Guardrails.md");

            Assert.Contains("# Cortex Runtime and Shell Separation Guardrails", docText);
            Assert.Contains("Current violations to shrink", docText);
            Assert.Contains("CortexShellState", docText);
            Assert.Contains("UnityWorkbenchRuntime", docText);
            Assert.Contains("IWorkbenchRuntimeUi", docText);
            Assert.Contains("Runtime ownership", docText);
            Assert.Contains("Shell ownership", docText);
            Assert.Contains("Bridge/host ownership", docText);
            Assert.Contains("Concrete backend ownership", docText);
            Assert.Contains("IMGUI remains supported", docText);
            Assert.Contains("shell/presentation-owned snapshot assembly", docText);
            Assert.Contains("onboarding coordinator should stay headless", docText);
            Assert.Contains("runtime composition and startup/configuration logic", docText);
            Assert.Contains("settings session/apply behavior", docText);
            Assert.Contains("shell-owned onboarding presenter", docText);
            Assert.Contains("settings document building, validation, contribution collection, and apply logic", docText);
            Assert.Contains("editor decisions and status presentation", docText);
            Assert.Contains("RuntimeShellGuardrailArchitectureTests", docText);
            Assert.Contains("RuntimeUiArchitectureTests", docText);
            Assert.Contains("CortexProjectTopologyBuildTests", docText);
            Assert.Contains("HostPlatformDependencyArchitectureTests", docText);
        }

        [Fact]
        public void CoreArchitectureDocs_PointToRuntimeShellGuardrailDoc()
        {
            var architectureGuideText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Architecture_Guide.md");
            var portabilityReportText = ArchitectureTestEnvironment.ReadRepoFile("documentation", "Cortex_Portability_Report.md");

            Assert.Contains("Cortex_Runtime_Shell_Separation_Guardrails.md", architectureGuideText);
            Assert.Contains("Cortex_Runtime_Shell_Separation_Guardrails.md", portabilityReportText);
            Assert.Contains("snapshot construction", architectureGuideText);
        }

        private static void AssertSourcesDoNotContainTokens(IEnumerable<string> projectNames, IEnumerable<string> disallowedTokens)
        {
            foreach (var sourcePath in ArchitectureTestEnvironment.GetProjectSourceFiles(projectNames))
            {
                var sourceText = File.ReadAllText(sourcePath);

                foreach (var token in disallowedTokens)
                {
                    Assert.DoesNotContain(token, sourceText);
                }
            }
        }
    }
}

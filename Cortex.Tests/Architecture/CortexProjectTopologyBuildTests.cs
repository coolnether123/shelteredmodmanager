using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class CortexProjectTopologyBuildTests
    {
        private static readonly string RepoRoot = ResolveRepoRoot();

        private static readonly string[] PortableProjectNames =
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

        private static readonly string[] DesktopSharedProjectNames =
        {
            "Cortex.Bridge",
            "Cortex.Contracts",
            "Cortex.Shell.Shared"
        };

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

        private static readonly string[] PluginProjectNames =
        {
            "Cortex.Plugin.Harmony"
        };

        private static readonly string[] ToolingProjectNames =
        {
            "Cortex.PathPicker.Host",
            "Cortex.Roslyn.Worker",
            "Cortex.Tabby.Server"
        };

        private static readonly string[] TestProjectNames =
        {
            "Cortex.Roslyn.Worker.Tests",
            "Cortex.Tests"
        };

        [Fact]
        public void AllCortexProjects_AreClassifiedByRole()
        {
            var discovered = GetCortexProjectPaths()
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = PortableProjectNames
                .Concat(DesktopSharedProjectNames)
                .Concat(HostSpecificProjectNames)
                .Concat(PluginProjectNames)
                .Concat(ToolingProjectNames)
                .Concat(TestProjectNames)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, discovered);
        }

        [Fact]
        public void PortableProjects_DoNotReferenceHostSpecificCortexProjects()
        {
            var hostSpecificProjects = new HashSet<string>(HostSpecificProjectNames, StringComparer.Ordinal);

            foreach (var projectName in PortableProjectNames)
            {
                var referencedProjects = LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(hostSpecificProjects.Contains).ToArray();

                Assert.True(
                    violations.Length == 0,
                    projectName + " references host-specific Cortex projects: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void DesktopSharedProjects_AreMultiTargetedForRuntimeAndDesktopConsumption_AndDoNotReferenceHostSpecificCortexProjects()
        {
            var hostSpecificProjects = new HashSet<string>(HostSpecificProjectNames, StringComparer.Ordinal);

            foreach (var projectName in DesktopSharedProjectNames)
            {
                var projectText = File.ReadAllText(GetProjectPath(projectName));
                var referencedProjects = LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(hostSpecificProjects.Contains).ToArray();

                Assert.Contains("<TargetFrameworks>net35;net8.0</TargetFrameworks>", projectText);
                Assert.DoesNotContain("<TargetFramework>netstandard2.0</TargetFramework>", projectText);
                Assert.True(
                    violations.Length == 0,
                    projectName + " references host-specific Cortex projects: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void AvaloniaDesktopHost_ProjectStaysOnDesktopLane_AndAvoidsLegacyHostDependencies()
        {
            var projectText = File.ReadAllText(GetProjectPath("Cortex.Host.Avalonia"));
            var referencedProjects = LoadProjectReferenceNames("Cortex.Host.Avalonia");
            var appText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Avalonia", "App.axaml"));
            var appCodeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Avalonia", "App.axaml.cs"));
            var shellText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Avalonia", "MainWindow.axaml"));
            var bridgeClientText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Avalonia", "Bridge", "NamedPipeDesktopBridgeClient.cs"));
            var dockFactoryText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Avalonia", "Services", "DesktopWorkbenchDockFactory.cs"));

            Assert.Contains("<TargetFramework>net8.0</TargetFramework>", projectText);
            Assert.Contains("Avalonia", projectText);
            Assert.Contains("Dock.Avalonia", projectText);
            Assert.Contains("Dock.Model.Mvvm", projectText);
            Assert.Contains("Serilog", projectText);
            Assert.Contains("Cortex.Bridge", referencedProjects);
            Assert.Contains("Cortex.Shell.Shared", referencedProjects);
            Assert.DoesNotContain("Cortex", referencedProjects);
            Assert.DoesNotContain("Cortex.Host.Unity", referencedProjects);
            Assert.DoesNotContain("Cortex.Host.Sheltered", referencedProjects);
            Assert.DoesNotContain("Cortex.Shell.Unity.Imgui", referencedProjects);
            Assert.DoesNotContain("Cortex.Renderers.Imgui", referencedProjects);
            Assert.DoesNotContain("Cortex.PathPicker.Host", referencedProjects);
            Assert.DoesNotContain("Cortex.Roslyn.Worker", referencedProjects);
            Assert.DoesNotContain("Cortex.Tabby.Server", referencedProjects);
            Assert.Contains("DockFluentTheme", appText);
            Assert.Contains("--pipe-name", appCodeText);
            Assert.Contains("DockControl", shellText);
            Assert.Contains("NamedPipeClientStream", bridgeClientText);
            Assert.Contains("DesktopWorkbenchDockFactory", dockFactoryText);
            Assert.Contains("CreateDocumentDock()", dockFactoryText);
            Assert.Contains("CreateToolDock()", dockFactoryText);
        }

        [Fact]
        public void DesktopBridgeProject_SitsOnDesktopSharedLane_AndAvoidsHostSpecificDependencies()
        {
            var projectText = File.ReadAllText(GetProjectPath("Cortex.Bridge"));
            var bridgeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Bridge", "BridgeMessageModels.cs"));
            var referencedProjects = LoadProjectReferenceNames("Cortex.Bridge");

            Assert.Contains("<TargetFrameworks>net35;net8.0</TargetFrameworks>", projectText);
            Assert.Contains("Cortex.Shell.Shared", referencedProjects);
            Assert.DoesNotContain("Cortex.Host.Avalonia", referencedProjects);
            Assert.DoesNotContain("Cortex", referencedProjects);
            Assert.DoesNotContain("Avalonia", bridgeText);
            Assert.DoesNotContain("Unity", bridgeText);
            Assert.DoesNotContain("Imgui", bridgeText);
            Assert.Contains("BridgeMessageEnvelope", bridgeText);
            Assert.Contains("WorkbenchBridgeSnapshot", bridgeText);
        }

        [Fact]
        public void WorkerFacingContracts_AreOwnedByCortexContracts_InsteadOfLinkedSourceCopies()
        {
            var contractsSources = GetProjectSourceFiles("Cortex.Contracts");
            var coreProjectText = File.ReadAllText(GetProjectPath("Cortex.Core"));
            var workerProjectText = File.ReadAllText(GetProjectPath("Cortex.Roslyn.Worker"));
            var tabbyServerProjectText = File.ReadAllText(GetProjectPath("Cortex.Tabby.Server"));

            Assert.Contains(Path.Combine(RepoRoot, "Cortex.Contracts", "LanguageService", "LanguageServiceProtocol.cs"), contractsSources);
            Assert.Contains(Path.Combine(RepoRoot, "Cortex.Contracts", "Completion", "CompletionAugmentationPromptContract.cs"), contractsSources);
            Assert.Contains(Path.Combine(RepoRoot, "Cortex.Contracts", "Text", "SemanticTokenClassification.cs"), contractsSources);

            Assert.Contains(@"..\Cortex.Contracts\Cortex.Contracts.csproj", coreProjectText);
            Assert.Contains(@"..\Cortex.Contracts\Cortex.Contracts.csproj", workerProjectText);
            Assert.Contains(@"..\Cortex.Contracts\Cortex.Contracts.csproj", tabbyServerProjectText);

            Assert.DoesNotContain(@"..\Cortex.LanguageService.Protocol\LanguageServiceProtocol.cs", coreProjectText);
            Assert.DoesNotContain(@"..\Shared\CompletionAugmentationPromptContract.cs", coreProjectText);
            Assert.DoesNotContain(@"Models\SemanticTokenClassification.cs", coreProjectText);
            Assert.DoesNotContain(@"..\Cortex.Core\Models\SemanticTokenClassification.cs", workerProjectText);
            Assert.DoesNotContain(@"..\Cortex.LanguageService.Protocol\LanguageServiceProtocol.cs", workerProjectText);
            Assert.DoesNotContain(@"..\Shared\CompletionAugmentationPromptContract.cs", tabbyServerProjectText);
        }

        [Fact]
        public void RenderingContracts_StayBelowPresentationAndRuntimeUiLayers()
        {
            var renderingReferences = LoadProjectReferenceNames("Cortex.Rendering");
            var presentationReferences = LoadProjectReferenceNames("Cortex.Presentation");
            var runtimeUiReferences = LoadProjectReferenceNames("Cortex.Rendering.RuntimeUi");

            Assert.DoesNotContain("Cortex.Presentation", renderingReferences);
            Assert.Contains("Cortex.Rendering", presentationReferences);
            Assert.DoesNotContain("Cortex.Rendering.RuntimeUi", presentationReferences);
            Assert.Contains("Cortex.Core", runtimeUiReferences);
            Assert.Contains("Cortex.Rendering", runtimeUiReferences);
        }

        [Fact]
        public void RenderingAndRuntimeUiProjects_DoNotContainStaleCompileIncludes()
        {
            var projectNames = new[]
            {
                "Cortex",
                "Cortex.Host.Sheltered",
                "Cortex.Rendering",
                "Cortex.Rendering.RuntimeUi",
                "Cortex.Presentation",
                "Cortex.Host.Unity",
                "Cortex.Shell.Unity.Imgui",
                "Cortex.Renderers.Imgui"
            };

            foreach (var projectName in projectNames)
            {
                var compileIncludes = LoadCompileIncludes(projectName);
                Assert.All(
                    compileIncludes,
                    path => Assert.True(File.Exists(path), projectName + " includes missing source file " + path + "."));
            }

            var renderingCompileIncludes = LoadCompileIncludeValues("Cortex.Rendering");
            var runtimeUiCompileIncludes = LoadCompileIncludeValues("Cortex.Rendering.RuntimeUi");
            var shelteredHostCompileIncludes = LoadCompileIncludeValues("Cortex.Host.Sheltered");
            var imguiShellCompileIncludes = LoadCompileIncludeValues("Cortex.Shell.Unity.Imgui");

            Assert.Contains(@"Frame\WorkbenchFrameContracts.cs", renderingCompileIncludes);
            Assert.DoesNotContain(@"Runtime\WorkbenchFrameContext.cs", renderingCompileIncludes);
            Assert.Contains(@"Shell\ShellMenuPopupController.cs", runtimeUiCompileIncludes);
            Assert.Contains(@"Shell\ShellOverlayInteractionController.cs", runtimeUiCompileIncludes);
            Assert.Contains(@"Shell\ShellSplitLayoutPlanner.cs", runtimeUiCompileIncludes);
            Assert.DoesNotContain(
                runtimeUiCompileIncludes,
                include => include.IndexOf("WorkbenchFrameContext", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           include.IndexOf("WorkbenchFrameContracts", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(@"Runtime\ShelteredWorkbenchUiSurface.cs", shelteredHostCompileIncludes);
            Assert.False(File.Exists(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredWorkbenchUiSurface.cs")));
            Assert.DoesNotContain(@"ImguiStyleUtil.cs", imguiShellCompileIncludes);
            Assert.Contains(@"Composition\ImguiWorkbenchRuntimeUiComposition.cs", imguiShellCompileIncludes);
            Assert.Contains(@"Styling\ImguiStyleUtil.cs", imguiShellCompileIncludes);
            Assert.Contains(@"Layout\ImguiWorkbenchLayout.cs", imguiShellCompileIncludes);
            Assert.Contains(@"Ui\ImguiWorkbenchUiSurface.cs", imguiShellCompileIncludes);
        }

        [Fact]
        public void ImguiShellComposition_OwnsBackendSelection_WhileGenericCortexAndUnityHostStayBackendNeutral()
        {
            var cortexReferences = LoadProjectReferenceNames("Cortex");
            var shellReferences = LoadProjectReferenceNames("Cortex.Shell.Unity.Imgui");
            var unityHostReferences = LoadProjectReferenceNames("Cortex.Host.Unity");
            var imguiReferences = LoadProjectReferenceNames("Cortex.Renderers.Imgui");
            var shelteredHostReferences = LoadProjectReferenceNames("Cortex.Host.Sheltered");

            Assert.DoesNotContain("Cortex.Renderers.Imgui", cortexReferences);
            Assert.Contains("Cortex.Renderers.Imgui", shellReferences);
            Assert.DoesNotContain("Cortex.Renderers.Imgui", unityHostReferences);
            Assert.DoesNotContain("Cortex.Renderers.Imgui", shelteredHostReferences);
            Assert.Contains("Cortex.Plugins.Abstractions", imguiReferences);
            Assert.Contains("Cortex.Rendering.RuntimeUi", imguiReferences);
            Assert.Contains("Cortex.Rendering", shelteredHostReferences);
            Assert.Contains("Cortex.Rendering.RuntimeUi", shelteredHostReferences);

            var imguiShellCompositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "Composition", "ImguiWorkbenchRuntimeUiComposition.cs"));
            var cortexShellRuntimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.Runtime.cs"));
            var cortexShellText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Shell.Unity.Imgui", "CortexShell.cs"));
            var shellBootstrapperText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "ShellBootstrapper.cs"));
            var hostInterfacesText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Presentation", "Abstractions", "HostInterfaces.cs"));
            var unityRuntimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "UnityWorkbenchRuntime.cs"));
            var unityFactoryText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "UnityWorkbenchRuntimeFactory.cs"));
            var shelteredCompositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredUnityHostComposition.cs"));

            Assert.DoesNotContain("ImguiRenderPipeline", cortexShellRuntimeText);
            Assert.DoesNotContain("using Cortex.Renderers.Imgui;", cortexShellRuntimeText);
            Assert.DoesNotContain("ICortexShellHostUi", cortexShellText);
            Assert.DoesNotContain("ICortexShellHostUi", shellBootstrapperText);
            Assert.DoesNotContain("ICortexShellHostUi", hostInterfacesText);
            Assert.Contains("IWorkbenchFrameContext FrameContext", hostInterfacesText);
            Assert.DoesNotContain("ImguiRenderPipeline", unityRuntimeText);
            Assert.DoesNotContain("using Cortex.Renderers.Imgui;", unityRuntimeText);
            Assert.DoesNotContain("using Cortex.Renderers.Imgui;", unityFactoryText);
            Assert.Contains("using Cortex.Renderers.Imgui;", imguiShellCompositionText);
            Assert.Contains("ImguiWorkbenchRuntimeUiFactory", imguiShellCompositionText);
            Assert.Contains("ImguiWorkbenchUiSurface", imguiShellCompositionText);
            Assert.Contains("ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext)", shelteredCompositionText);
            Assert.Contains("UnityWorkbenchFrameContext", shelteredCompositionText);
            Assert.Contains("frameContext", shelteredCompositionText);
            Assert.DoesNotContain("ImguiWorkbenchRuntimeUiFactory", shelteredCompositionText);
            Assert.DoesNotContain("ImguiWorkbenchUiSurface", shelteredCompositionText);
            Assert.DoesNotContain("new ImguiWorkbenchRuntimeUiFactory", cortexShellText);
            Assert.DoesNotContain("new ImguiWorkbenchRuntimeUiFactory", shellBootstrapperText);
        }

        [Fact]
        public void PluginProjects_DoNotReferenceHostSpecificCortexProjects()
        {
            var hostSpecificProjects = new HashSet<string>(HostSpecificProjectNames, StringComparer.Ordinal);

            foreach (var projectName in PluginProjectNames)
            {
                var referencedProjects = LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(hostSpecificProjects.Contains).ToArray();

                Assert.True(
                    violations.Length == 0,
                    projectName + " references host-specific Cortex projects: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void ToolingProjects_DoNotReferenceHostSpecificCortexProjects()
        {
            var hostSpecificProjects = new HashSet<string>(HostSpecificProjectNames, StringComparer.Ordinal);

            foreach (var projectName in ToolingProjectNames)
            {
                var referencedProjects = LoadProjectReferenceNames(projectName);
                var violations = referencedProjects.Where(hostSpecificProjects.Contains).ToArray();

                Assert.True(
                    violations.Length == 0,
                    projectName + " references host-specific Cortex projects: " + string.Join(", ", violations));
            }
        }

        [Fact]
        public void CortexProjects_UseCentralizedBuildOutputs()
        {
            foreach (var projectPath in GetCortexProjectPaths())
            {
                var projectText = File.ReadAllText(projectPath);

                Assert.DoesNotContain("<OutputPath>", projectText);
                Assert.DoesNotContain("<BaseOutputPath>", projectText);
                Assert.DoesNotContain("<BaseIntermediateOutputPath>", projectText);
            }
        }

        [Fact]
        public void UnityHostedProjects_DoNotHardcodeMachineLocalShelteredHintPaths()
        {
            foreach (var projectName in HostSpecificProjectNames.Concat(PluginProjectNames).Concat(new[] { "Cortex.Tests" }))
            {
                var projectText = File.ReadAllText(GetProjectPath(projectName));

                Assert.DoesNotContain(@"D:\Epic Games\Sheltered", projectText);
                Assert.DoesNotContain(@"ShelteredWindows64_EOS_Data\Managed\UnityEngine.dll", projectText);
            }
        }

        [Fact]
        public void UnityHostedProjects_ResolveUnityReferencesThroughCentralBuildContract()
        {
            var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
            var targetsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.targets"));
            var resolverText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Tests", "Testing", "UnityManagedAssemblyResolver.cs"));

            Assert.Contains("CortexUsesUnityEngine", propsText);
            Assert.Contains("CortexUnityManagedDir", propsText);
            Assert.Contains("CortexUnityEngineReferencePath", propsText);
            Assert.Contains("CORTEX_UNITY_MANAGED_DIR", propsText);
            Assert.Contains("CORTEX_UNITY_ENGINE_PATH", propsText);
            Assert.Contains(@"libs\UnityEngine.dll", propsText);
            Assert.Contains("CortexValidateUnityEngineReference", targetsText);
            Assert.Contains("CORTEX_UNITY_MANAGED_DIR", resolverText);
            Assert.Contains("CORTEX_UNITY_ENGINE_PATH", resolverText);
            Assert.DoesNotContain(@"D:\Epic Games\Sheltered", resolverText);
        }

        [Fact]
        public void PortableAndToolingProjects_DoNotHardcodeShelteredBundlePaths()
        {
            foreach (var projectName in PortableProjectNames.Concat(DesktopSharedProjectNames).Concat(PluginProjectNames).Concat(ToolingProjectNames))
            {
                var projectText = File.ReadAllText(GetProjectPath(projectName));

                Assert.DoesNotContain(@"Dist\SMM", projectText);
                Assert.DoesNotContain(@"Dist/SMM", projectText);
                Assert.DoesNotContain(@"bin\decompiler", projectText);
                Assert.DoesNotContain(@"bin\roslyn", projectText);
                Assert.DoesNotContain(@"bin\tabby", projectText);
                Assert.DoesNotContain(@"bin\plugins", projectText);
            }
        }

        [Fact]
        public void PortableProjectSources_DoNotEmbedShelteredOrSmmPaths()
        {
            foreach (var sourcePath in GetProjectSourceFiles(PortableProjectNames.Concat(DesktopSharedProjectNames)))
            {
                var sourceText = File.ReadAllText(sourcePath);

                Assert.DoesNotContain("Sheltered_Data", sourceText);
                Assert.DoesNotContain(@"Dist\SMM", sourceText);
                Assert.DoesNotContain(@"Dist/SMM", sourceText);
                Assert.DoesNotContain(@"\SMM\", sourceText);
            }
        }

        [Fact]
        public void PortableProjectSources_DoNotReuseLegacyHostPathContractNames_OutsideMigrationLayer()
        {
            var migrationLayerPath = Path.Combine(RepoRoot, "Cortex.Core", "Services", "JsonCortexSettingsStore.cs");

            foreach (var sourcePath in GetProjectSourceFiles(PortableProjectNames.Concat(DesktopSharedProjectNames)))
            {
                if (string.Equals(sourcePath, migrationLayerPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sourceText = File.ReadAllText(sourcePath);
                Assert.DoesNotContain("ModsRootPath", sourceText);
                Assert.DoesNotContain("ManagedAssemblyRootPath", sourceText);
                Assert.DoesNotContain("GameRootPath", sourceText);
            }
        }

        [Fact]
        public void PluginDiscovery_UsesOnlyBundledAndExplicitConfiguredRoots()
        {
            var loaderText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "WorkbenchPluginLoader.cs"));
            var bootstrapperText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "ShellBootstrapper.cs"));

            Assert.Contains("BundledPluginSearchRoots", loaderText);
            Assert.Contains("CortexPluginSearchRoots", loaderText);
            Assert.DoesNotContain("RuntimeContentRootPath", loaderText);
            Assert.DoesNotContain("ModsRootPath", loaderText);
            Assert.DoesNotContain("Path.Combine(normalizedRoot, \"Plugins\")", loaderText);
            Assert.Contains("ConfiguredPluginSearchRoots", bootstrapperText);
        }

        [Fact]
        public void MetadataNavigation_DoesNotInventShelteredSearchRoots()
        {
            var navigationText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Services", "Navigation", "Metadata", "AssemblyMetadataNavigationService.cs"));

            Assert.DoesNotContain("Path.Combine(baseDirectory, \"SMM\")", navigationText);
            Assert.DoesNotContain("smmRoot", navigationText);
            Assert.Contains("SourceRootSetBuilder.LanguageServiceRoots", navigationText);
        }

        [Fact]
        public void HostUnityProject_DoesNotBuildToolingProjectDirectly()
        {
            var projectText = File.ReadAllText(GetProjectPath("Cortex.Host.Unity"));

            Assert.DoesNotContain("BuildPathPickerHost", projectText);
            Assert.DoesNotContain(@"..\Cortex.PathPicker.Host\Cortex.PathPicker.Host.csproj", projectText);
        }

        [Fact]
        public void ShelteredSpecificPaths_AreIsolatedToDedicatedHostModules()
        {
            foreach (var sourcePath in GetProjectSourceFiles(new[] { "Cortex", "Cortex.Shell.Unity.Imgui", "Cortex.Renderers.Imgui", "Cortex.Host.Unity", "Cortex.Plugin.Harmony" }))
            {
                var sourceText = File.ReadAllText(sourcePath);

                Assert.DoesNotContain("Sheltered", sourceText);
                Assert.DoesNotContain("Sheltered_Data", sourceText);
                Assert.DoesNotContain("SMM", sourceText);
                Assert.DoesNotContain("mod_manager.ini", sourceText);
            }
        }

        [Fact]
        public void BundleProfiles_AreCentralizedAndCoverShelteredAndDesktop()
        {
            var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
            var targetsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.targets"));

            Assert.Contains("CortexBundleProfile", propsText);
            Assert.Contains("Sheltered", propsText);
            Assert.Contains("Desktop", propsText);
            Assert.Contains(@"Dist\SMM\", propsText);
            Assert.Contains(@"artifacts\bundles\Desktop\", propsText);
            Assert.Contains("CortexBundleContentKind", propsText);
            Assert.Contains("PortableRuntimeAssembly", propsText);
            Assert.Contains("HostRuntimeAssembly", propsText);
            Assert.Contains("BundledPlugin", propsText);
            Assert.Contains("ExternalTool", propsText);
            Assert.Contains("CortexCopyBundleOutputs", targetsText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Bridge'", propsText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Contracts'", propsText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Host.Avalonia'", propsText);
            Assert.Contains("'$(MSBuildProjectName)' == 'Cortex.Shell.Shared'", propsText);
        }

        [Fact]
        public void BundleProfiles_DefineSeparateRuntimePluginAndToolRoots()
        {
            var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));

            Assert.Contains(@"CortexBundlePortableRuntimeRootRelativePath>bin\decompiler\", propsText);
            Assert.Contains(@"CortexBundleHostRuntimeRootRelativePath>bin\decompiler\", propsText);
            Assert.Contains(@"CortexBundlePluginRootRelativePath>bin\plugins\", propsText);
            Assert.Contains(@"CortexBundleToolRootRelativePath>bin\tools\", propsText);

            Assert.Contains(@"CortexBundlePortableRuntimeRootRelativePath>portable\lib\", propsText);
            Assert.Contains(@"CortexBundlePluginRootRelativePath>plugins\", propsText);
            Assert.Contains(@"CortexBundleToolRootRelativePath>tooling\", propsText);
            Assert.Contains("Cortex.Plugin.Harmony", propsText);
            Assert.Contains("Cortex.Roslyn.Worker", propsText);
            Assert.Contains("Cortex.Tabby.Server", propsText);
            Assert.DoesNotContain("'$(CortexBundleProfile)' == 'FutureHostReady'", propsText);
        }

        [Fact]
        public void ExternalToolPackaging_IsSeparatedFromInProcessRuntimePackaging()
        {
            var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
            var targetsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.targets"));

            Assert.Contains("Cortex.PathPicker.Host", propsText);
            Assert.Contains("Cortex.Roslyn.Worker", propsText);
            Assert.Contains("Cortex.Tabby.Server", propsText);
            Assert.Contains("CortexBundleComponentId>windows-path-picker<", propsText);
            Assert.Contains("CortexBundleComponentId>roslyn<", propsText);
            Assert.Contains("CortexBundleComponentId>tabby<", propsText);
            Assert.Contains(@"CortexBundleToolRootRelativePath>bin\tools\", propsText);
            Assert.Contains("CortexRemoveLegacyBundleOutputs", targetsText);
            Assert.Contains("CortexLegacyPortableBundleFiles", targetsText);
        }

        [Fact]
        public void SolutionFolders_ExposeDesktopSharedDesktopHostLegacyHostAndExternalToolLanes()
        {
            var solutionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.sln"));

            Assert.Contains(@"""Desktop"", ""Desktop"", ""{B2BF7298-65F5-4C4A-B13B-3FD4EF5182DF}""", solutionText);
            Assert.Contains(@"""Desktop Shared"", ""Desktop Shared"", ""{A8C48835-F53B-41D1-B062-B03E6517F331}""", solutionText);
            Assert.Contains(@"""Desktop Host"", ""Desktop Host"", ""{A76A96F9-6592-4C92-A9E8-4E43A7F4E293}""", solutionText);
            Assert.Contains(@"""Legacy Unity IMGUI"", ""Legacy Unity IMGUI"", ""{B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}""", solutionText);
            Assert.Contains(@"""External Workers and Tools"", ""External Workers and Tools"", ""{6D3901D8-58B2-4BCE-9CB0-2E20E9B4D1A1}""", solutionText);
            Assert.Contains(@"""Cortex.Contracts"", ""Cortex.Contracts\Cortex.Contracts.csproj"", ""{C458EE62-EED9-4B14-9EC3-910105D9BDFC}""", solutionText);
            Assert.Contains(@"""Cortex.Bridge"", ""Cortex.Bridge\Cortex.Bridge.csproj"", ""{40A0D7B5-7EAD-4B32-8DA2-B619B5DE5C2F}""", solutionText);
            Assert.Contains(@"""Cortex.Shell.Shared"", ""Cortex.Shell.Shared\Cortex.Shell.Shared.csproj"", ""{E11C9B2D-2E3A-4E91-A2C1-15AB449E6C01}""", solutionText);
            Assert.Contains(@"""Cortex.Host.Avalonia"", ""Cortex.Host.Avalonia\Cortex.Host.Avalonia.csproj"", ""{A4F4068D-EB9A-4C72-8FD5-4F48CE0BC781}""", solutionText);
            Assert.Contains(@"{A8C48835-F53B-41D1-B062-B03E6517F331} = {B2BF7298-65F5-4C4A-B13B-3FD4EF5182DF}", solutionText);
            Assert.Contains(@"{A76A96F9-6592-4C92-A9E8-4E43A7F4E293} = {B2BF7298-65F5-4C4A-B13B-3FD4EF5182DF}", solutionText);
            Assert.Contains(@"{C458EE62-EED9-4B14-9EC3-910105D9BDFC} = {A8C48835-F53B-41D1-B062-B03E6517F331}", solutionText);
            Assert.Contains(@"{40A0D7B5-7EAD-4B32-8DA2-B619B5DE5C2F} = {A8C48835-F53B-41D1-B062-B03E6517F331}", solutionText);
            Assert.Contains(@"{E11C9B2D-2E3A-4E91-A2C1-15AB449E6C01} = {A8C48835-F53B-41D1-B062-B03E6517F331}", solutionText);
            Assert.Contains(@"{A4F4068D-EB9A-4C72-8FD5-4F48CE0BC781} = {A76A96F9-6592-4C92-A9E8-4E43A7F4E293}", solutionText);
            Assert.Contains(@"{40A0D7B5-7EAD-4B32-8DA2-B619B5DE5C2F}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{E11C9B2D-2E3A-4E91-A2C1-15AB449E6C01}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{A4F4068D-EB9A-4C72-8FD5-4F48CE0BC781}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"""Cortex.Shell.Unity.Imgui"", ""Cortex.Shell.Unity.Imgui\Cortex.Shell.Unity.Imgui.csproj"", ""{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3}""", solutionText);
            Assert.Contains(@"{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3} = {86FA87A4-554D-7FD3-E7FA-07CF08F2607B}", solutionText);
            Assert.Contains(@"{86FA87A4-554D-7FD3-E7FA-07CF08F2607B} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
            Assert.Contains(@"{D459D5A1-8460-2948-B6C6-81F3C1216BDB} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
            Assert.Contains(@"{748B7F6E-D557-48FA-B393-6374A7582255} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
            Assert.Contains(@"{5A6E9E3A-0E10-4F09-A3FD-9B7C0D61A2A7} = {401E6CF6-6FDB-819D-B348-2A4162B7B24D}", solutionText);
            Assert.DoesNotContain(@"{5A6E9E3A-0E10-4F09-A3FD-9B7C0D61A2A7} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
        }

        [Fact]
        public void ManagerBuild_UsesShelteredBundleProfile_ForCortexRuntimeGraph()
        {
            var managerProjectText = File.ReadAllText(Path.Combine(RepoRoot, "Manager", "ManagerGUI.csproj"));
            var hostUnityReferences = LoadProjectReferenceNames("Cortex.Host.Unity");

            Assert.Contains("Target Name=\"BuildCortexRuntime\"", managerProjectText);
            Assert.Contains(@"..\Cortex.Core\Cortex.Core.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Plugins.Abstractions\Cortex.Plugins.Abstractions.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Presentation\Cortex.Presentation.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Rendering\Cortex.Rendering.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Rendering.RuntimeUi\Cortex.Rendering.RuntimeUi.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.CompletionProviders\Cortex.CompletionProviders.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Tabby\Cortex.Tabby.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Ollama\Cortex.Ollama.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.OpenRouter\Cortex.OpenRouter.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex\Cortex.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Renderers.Imgui\Cortex.Renderers.Imgui.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Host.Unity\Cortex.Host.Unity.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Host.Sheltered\Cortex.Host.Sheltered.csproj", managerProjectText);
            Assert.Contains(@"..\Cortex.Platform.ModAPI\Cortex.Platform.ModAPI.csproj", managerProjectText);
            Assert.Contains("Targets=\"Rebuild\"", managerProjectText);
            Assert.Contains("CortexBundleProfile=Sheltered", managerProjectText);
            Assert.Contains("Cortex.Shell.Unity.Imgui", hostUnityReferences);
        }

        [Fact]
        public void ShelteredManagerSolution_IncludesCurrentCortexRuntimeProjects()
        {
            var solutionText = File.ReadAllText(Path.Combine(RepoRoot, "ShelteredModManager.sln"));

            Assert.Contains(@"""Cortex.Rendering.RuntimeUi"", ""Cortex.Rendering.RuntimeUi\Cortex.Rendering.RuntimeUi.csproj"", ""{8F6A2F38-54DF-4B49-8FC4-7F909D1AF6D3}""", solutionText);
            Assert.Contains(@"""Cortex.Shell.Unity.Imgui"", ""Cortex.Shell.Unity.Imgui\Cortex.Shell.Unity.Imgui.csproj"", ""{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3}""", solutionText);
            Assert.Contains(@"""Cortex.Host.Sheltered"", ""Cortex.Host.Sheltered\Cortex.Host.Sheltered.csproj"", ""{748B7F6E-D557-48FA-B393-6374A7582255}""", solutionText);
            Assert.Contains(@"{D982DFB0-FEA1-4DC8-A001-35F5181F212A} = {D982DFB0-FEA1-4DC8-A001-35F5181F212A}", solutionText);
            Assert.Contains(@"{7A570620-E51D-499E-A28E-13A1D8156417} = {7A570620-E51D-499E-A28E-13A1D8156417}", solutionText);
            Assert.Contains(@"{748B7F6E-D557-48FA-B393-6374A7582255} = {748B7F6E-D557-48FA-B393-6374A7582255}", solutionText);
            Assert.Contains(@"{2343835F-8DA6-4CAB-B5A9-D86C83A9A40D} = {2343835F-8DA6-4CAB-B5A9-D86C83A9A40D}", solutionText);
            Assert.Contains(@"{8F6A2F38-54DF-4B49-8FC4-7F909D1AF6D3}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{748B7F6E-D557-48FA-B393-6374A7582255}.Debug|Any CPU.Build.0 = Debug|Any CPU", solutionText);
            Assert.Contains(@"{8F6A2F38-54DF-4B49-8FC4-7F909D1AF6D3} = {ECA9F4AD-77F7-253B-20D4-45A77E590D54}", solutionText);
            Assert.Contains(@"{6CB42E40-4058-4A0D-BEFA-F117F8F0F5D3} = {86FA87A4-554D-7FD3-E7FA-07CF08F2607B}", solutionText);
            Assert.Contains(@"{748B7F6E-D557-48FA-B393-6374A7582255} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
        }

        [Fact]
        public void HostToolLookup_UsesDedicatedToolFolders()
        {
            var roslynFactoryText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "RoslynLanguageProviderFactory.cs"));
            var tabbyControllerText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Tabby", "BundledTabbyServerController.cs"));
            var pathPickerServiceText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "WindowsPathInteractionService.cs"));

            Assert.Contains("ResolveFromToolRoot", roslynFactoryText);
            Assert.Contains("ResolveFromHostBin", roslynFactoryText);
            Assert.Contains("ResolveFromToolRoot", tabbyControllerText);
            Assert.Contains("ResolveFromHostBin", tabbyControllerText);
            Assert.Contains("ResolveFromToolRoot", pathPickerServiceText);
            Assert.DoesNotContain(@"..\tools\tabby", tabbyControllerText);
            Assert.Contains(@"windows-path-picker", pathPickerServiceText);
        }

        [Fact]
        public void PortabilityReport_DocumentsRoleBoundaries_AndDesktopHostCompletion()
        {
            var reportText = File.ReadAllText(Path.Combine(RepoRoot, "documentation", "Cortex_Portability_Report.md"));

            Assert.Contains("desktop-first architecture", reportText);
            Assert.Contains("Cortex.Bridge", reportText);
            Assert.Contains("Cortex.Contracts", reportText);
            Assert.Contains("Cortex.Shell.Shared", reportText);
            Assert.Contains("Cortex.Host.Avalonia", reportText);
            Assert.Contains("net35", reportText);
            Assert.Contains("LanguageServiceProtocol", reportText);
            Assert.Contains("CompletionAugmentationPromptContract", reportText);
            Assert.Contains("SemanticTokenClassification", reportText);
            Assert.Contains("Avalonia", reportText);
            Assert.Contains("Dock", reportText);
            Assert.Contains("Serilog", reportText);
            Assert.Contains("Portable Cortex", reportText);
            Assert.Contains("Host-Specific Cortex", reportText);
            Assert.Contains("Host-specific Cortex projects:", reportText);
            Assert.Contains("Plugin-Specific Cortex", reportText);
            Assert.Contains("External-Tool Cortex", reportText);
            Assert.Contains("Desktop Host Follow-Up Boundaries", reportText);
            Assert.Contains("Host bundle B: `Desktop`", reportText);
            Assert.Contains("Cortex.Host.Sheltered", reportText);
            Assert.Contains("Cortex.Rendering.RuntimeUi", reportText);
            Assert.Contains("Cortex.Bridge -> Cortex.Shell.Shared", reportText);
            Assert.Contains("Cortex.Shell.Shared -> none", reportText);
            Assert.Contains("BundledPluginSearchRoots", reportText);
            Assert.Contains("BundledToolRootPath", reportText);
            Assert.Contains("CortexPluginSearchRoots", reportText);
            Assert.Contains("ConfiguredPluginSearchRoots", reportText);
            Assert.Contains("CortexUnityManagedDir", reportText);
            Assert.Contains("CORTEX_UNITY_MANAGED_DIR", reportText);
        }

        private static string[] GetCortexProjectPaths()
        {
            return Directory
                .GetFiles(RepoRoot, "Cortex*.csproj", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\Decompiled\", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetProjectPath(string projectName)
        {
            var projectPath = GetCortexProjectPaths()
                .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), projectName, StringComparison.Ordinal));

            Assert.True(projectPath != null, "Could not locate project file for " + projectName + ".");
            return projectPath;
        }

        private static string[] GetProjectSourceFiles(IEnumerable<string> projectNames)
        {
            return projectNames
                .SelectMany(GetProjectSourceFiles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] GetProjectSourceFiles(string projectName)
        {
            var projectDirectory = Path.GetDirectoryName(GetProjectPath(projectName));
            Assert.True(!string.IsNullOrEmpty(projectDirectory), "Could not resolve project directory for " + projectName + ".");

            return Directory
                .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IList<string> LoadProjectReferenceNames(string projectName)
        {
            var document = LoadProjectDocument(projectName);
            XNamespace xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            var references = document
                .Descendants()
                .Where(element => element.Name == xmlNamespace + "ProjectReference" || element.Name.LocalName == "ProjectReference")
                .Select(element => element.Attribute("Include"))
                .Where(attribute => attribute != null && !string.IsNullOrEmpty(attribute.Value))
                .Select(attribute => Path.GetFileNameWithoutExtension(attribute.Value))
                .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("Cortex", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            return references;
        }

        private static IList<string> LoadCompileIncludes(string projectName)
        {
            var projectDirectory = Path.GetDirectoryName(GetProjectPath(projectName));
            Assert.True(!string.IsNullOrEmpty(projectDirectory), "Could not resolve project directory for " + projectName + ".");

            return LoadCompileIncludeValues(projectName)
                .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include)))
                .ToArray();
        }

        private static IList<string> LoadCompileIncludeValues(string projectName)
        {
            var document = LoadProjectDocument(projectName);
            XNamespace xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            return document
                .Descendants()
                .Where(element => element.Name == xmlNamespace + "Compile" || element.Name.LocalName == "Compile")
                .Select(element => element.Attribute("Include"))
                .Where(attribute => attribute != null && !string.IsNullOrEmpty(attribute.Value))
                .Select(attribute => attribute.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static XDocument LoadProjectDocument(string projectName)
        {
            return XDocument.Load(GetProjectPath(projectName));
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

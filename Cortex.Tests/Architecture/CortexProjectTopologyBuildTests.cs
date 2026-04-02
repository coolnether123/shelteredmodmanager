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
            "Cortex.Tabby"
        };

        private static readonly string[] HostSpecificProjectNames =
        {
            "Cortex",
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
            Assert.Contains("CortexValidateUnityEngineReference", targetsText);
            Assert.Contains("CORTEX_UNITY_MANAGED_DIR", resolverText);
            Assert.Contains("CORTEX_UNITY_ENGINE_PATH", resolverText);
            Assert.DoesNotContain(@"D:\Epic Games\Sheltered", resolverText);
        }

        [Fact]
        public void PortableAndToolingProjects_DoNotHardcodeShelteredBundlePaths()
        {
            foreach (var projectName in PortableProjectNames.Concat(PluginProjectNames).Concat(ToolingProjectNames))
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
            foreach (var sourcePath in GetProjectSourceFiles(PortableProjectNames))
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

            foreach (var sourcePath in GetProjectSourceFiles(PortableProjectNames))
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
            foreach (var sourcePath in GetProjectSourceFiles(new[] { "Cortex", "Cortex.Renderers.Imgui", "Cortex.Host.Unity", "Cortex.Plugin.Harmony" }))
            {
                var sourceText = File.ReadAllText(sourcePath);

                Assert.DoesNotContain("Sheltered", sourceText);
                Assert.DoesNotContain("Sheltered_Data", sourceText);
                Assert.DoesNotContain("SMM", sourceText);
                Assert.DoesNotContain("mod_manager.ini", sourceText);
            }
        }

        [Fact]
        public void BundleProfiles_AreCentralizedAndCoverShelteredAndFutureHostReady()
        {
            var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
            var targetsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.targets"));

            Assert.Contains("CortexBundleProfile", propsText);
            Assert.Contains("Sheltered", propsText);
            Assert.Contains("FutureHostReady", propsText);
            Assert.Contains(@"Dist\SMM\", propsText);
            Assert.Contains(@"artifacts\bundles\FutureHostReady\", propsText);
            Assert.Contains("CortexBundleContentKind", propsText);
            Assert.Contains("PortableRuntimeAssembly", propsText);
            Assert.Contains("HostRuntimeAssembly", propsText);
            Assert.Contains("BundledPlugin", propsText);
            Assert.Contains("ExternalTool", propsText);
            Assert.Contains("CortexCopyBundleOutputs", targetsText);
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
        public void SolutionFolders_ClassifyShelteredAdapterAndHarmonyPluginSeparately()
        {
            var solutionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.sln"));

            Assert.Contains(@"{748B7F6E-D557-48FA-B393-6374A7582255} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
            Assert.Contains(@"{5A6E9E3A-0E10-4F09-A3FD-9B7C0D61A2A7} = {401E6CF6-6FDB-819D-B348-2A4162B7B24D}", solutionText);
            Assert.DoesNotContain(@"{5A6E9E3A-0E10-4F09-A3FD-9B7C0D61A2A7} = {B9F3EFD9-8792-DBB5-53D5-5D013E0311F9}", solutionText);
        }

        [Fact]
        public void HostToolLookup_UsesDedicatedToolFolders()
        {
            var roslynFactoryText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex", "Shell", "RoslynLanguageProviderFactory.cs"));
            var tabbyControllerText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Tabby", "BundledTabbyServerController.cs"));
            var pathPickerServiceText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Runtime", "WindowsPathInteractionService.cs"));

            Assert.Contains("ResolveFromHostBin", roslynFactoryText);
            Assert.Contains("ResolveFromHostBin", tabbyControllerText);
            Assert.DoesNotContain(@"..\tools\tabby", tabbyControllerText);
            Assert.Contains(@"windows-path-picker", pathPickerServiceText);
        }

        [Fact]
        public void PortabilityReport_DocumentsRoleBoundaries_AndFutureHostCompletion()
        {
            var reportText = File.ReadAllText(Path.Combine(RepoRoot, "documentation", "Cortex_Portability_Report.md"));

            Assert.Contains("Portable Cortex", reportText);
            Assert.Contains("Host-Specific Cortex", reportText);
            Assert.Contains("Plugin-Specific Cortex", reportText);
            Assert.Contains("External-Tool Cortex", reportText);
            Assert.Contains("Future Host Completion Steps", reportText);
            Assert.Contains("FutureHostReady", reportText);
            Assert.Contains("Cortex.Host.Sheltered", reportText);
            Assert.Contains("BundledPluginSearchRoots", reportText);
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
            var document = XDocument.Load(GetProjectPath(projectName));
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

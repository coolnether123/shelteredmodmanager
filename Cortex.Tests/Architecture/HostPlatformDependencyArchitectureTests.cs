using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cortex.Host.Sheltered.Runtime;
using Cortex.Host.Unity.Runtime;
using Cortex.Platform.ModAPI.Runtime;
using ModAPI.Core;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class HostPlatformDependencyArchitectureTests
    {
        private static readonly string RepoRoot = ResolveRepoRoot();

        [Fact]
        public void HostUnityProject_DoesNotReference_ModApiProjects()
        {
            var projectText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Unity", "Cortex.Host.Unity.csproj"));

            Assert.DoesNotContain(@"..\Cortex.Platform.ModAPI\Cortex.Platform.ModAPI.csproj", projectText);
            Assert.DoesNotContain(@"..\ModAPI\ModAPI.csproj", projectText);
            Assert.DoesNotContain(@"..\Cortex.Host.Sheltered\Cortex.Host.Sheltered.csproj", projectText);
        }

        [Fact]
        public void HostUnityAssembly_DoesNotReference_ModApiAssemblies()
        {
            var referencedAssemblyNames = typeof(UnityCortexHostCompositionRoot)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.DoesNotContain("Cortex.Platform.ModAPI", referencedAssemblyNames);
            Assert.DoesNotContain("ModAPI", referencedAssemblyNames);
        }

        [Fact]
        public void HostUnitySource_DoesNotContain_ModApiBootstrapCoupling()
        {
            var hostRoot = Path.Combine(RepoRoot, "Cortex.Host.Unity");
            var hostSource = Directory
                .GetFiles(hostRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .ToArray();

            Assert.DoesNotContain(hostSource, source => source.Contains("using Cortex.Platform.ModAPI"));
            Assert.DoesNotContain(hostSource, source => source.Contains("using ModAPI"));
            Assert.DoesNotContain(hostSource, source => source.Contains("ModApiCortexPlatformModule"));
            Assert.DoesNotContain(hostSource, source => source.Contains("IGameRuntimeBootstrap"));
        }

        [Fact]
        public void ShelteredHostPaths_AreCentralizedInDedicatedShelteredHostProject()
        {
            var layoutText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredHostPathLayout.cs"));
            var environmentText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredCortexHostEnvironment.cs"));
            var settingsText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Composition", "ShelteredWorkbenchSettingContributions.cs"));
            var diagnosticText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Platform.ModAPI", "Runtime", "ModApiCortexDiagnosticConfiguration.cs"));

            Assert.Contains("Sheltered_Data", layoutText);
            Assert.Contains("\"SMM\"", layoutText);
            Assert.Contains("mod_manager.ini", layoutText);

            Assert.Contains("ShelteredHostPathLayout", environmentText);
            Assert.Contains("ShelteredHostPathLayout", settingsText);
            Assert.Contains("ShelteredHostPathLayout", diagnosticText);

            Assert.DoesNotContain("Sheltered_Data", environmentText);
            Assert.DoesNotContain("\"SMM\"", environmentText);
            Assert.DoesNotContain(@"D:\Games\Sheltered\SMM\mods", settingsText);
            Assert.DoesNotContain(@"D:\Games\Sheltered\Sheltered_Data\Managed", settingsText);
            Assert.DoesNotContain("Directory.GetCurrentDirectory(), \"SMM\"", diagnosticText);
        }

        [Fact]
        public void ShelteredHostLayout_SeedsConfiguredPluginRoots_ForLegacyThirdPartyDiscovery()
        {
            var layout = ShelteredHostPathLayout.CreateIllustrativeLayout();

            Assert.Equal(
                @"D:\Games\Sheltered\SMM\mods;D:\Games\Sheltered\SMM\mods\Plugins",
                layout.ConfiguredPluginSearchRoots);
        }

        [Fact]
        public void GenericUnityHost_DoesNotContainShelteredIdentity()
        {
            var hostSource = Directory
                .GetFiles(Path.Combine(RepoRoot, "Cortex.Host.Unity"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .ToArray();

            Assert.DoesNotContain(hostSource, source => source.Contains("Sheltered"));
        }

        [Fact]
        public void ShelteredWorkbenchComposition_LivesInDedicatedShelteredHostProject()
        {
            var compositionText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Composition", "ShelteredWorkbenchComposition.cs"));
            var runtimeText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Host.Sheltered", "Runtime", "ShelteredUnityWorkbenchContributionRegistrar.cs"));

            Assert.Contains("ShelteredWorkbenchComposition", compositionText);
            Assert.Contains("ShelteredWorkbenchComposition.RegisterBuiltIns", runtimeText);
        }

        [Fact]
        public void ModApiAssembly_OwnsGameRuntimeBootstrapComposition()
        {
            var bootstrapType = typeof(ModApiCortexRuntimeBootstrap);

            Assert.True(typeof(IGameRuntimeBootstrap).IsAssignableFrom(bootstrapType));
            Assert.Equal(typeof(ModApiCortexRuntimeBootstrap).Assembly, bootstrapType.Assembly);
        }

        [Fact]
        public void ModApiSource_ComposesThroughDedicatedShelteredHostAdapter()
        {
            var bootstrapText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Platform.ModAPI", "Runtime", "ModApiCortexRuntimeBootstrap.cs"));
            var projectText = File.ReadAllText(Path.Combine(RepoRoot, "Cortex.Platform.ModAPI", "Cortex.Platform.ModAPI.csproj"));

            Assert.Contains("ShelteredUnityHostComposition.Create", bootstrapText);
            Assert.Contains(@"..\Cortex.Host.Sheltered\Cortex.Host.Sheltered.csproj", projectText);
        }

        [Fact]
        public void ShelteredHostAssembly_DependsOnGenericUnityHostButUnityHostDoesNotDependOnShelteredHost()
        {
            var hostUnityReferences = typeof(UnityCortexHostCompositionRoot)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();
            var shelteredHostReferences = typeof(ShelteredUnityHostComposition)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.DoesNotContain("Cortex.Host.Sheltered", hostUnityReferences);
            Assert.Contains("Cortex.Host.Unity", shelteredHostReferences);
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

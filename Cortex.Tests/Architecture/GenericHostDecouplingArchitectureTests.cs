using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Editor;
using Cortex.Plugin.Harmony;
using Cortex.Plugins.Abstractions;
using Cortex.Services.Inspector.Actions;
using Xunit;

namespace Cortex.Tests.Architecture
{
    public sealed class GenericHostDecouplingArchitectureTests
    {
        private static readonly string RepoRoot = ResolveRepoRoot();

        [Fact]
        public void PlatformModuleContract_DoesNotExposeHarmonySpecificMembers()
        {
            var memberNames = typeof(ICortexPlatformModule)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();

            Assert.DoesNotContain(memberNames, name => name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void GenericShellAndEditorContracts_DoNotExposeHarmonySpecificMembers()
        {
            AssertNoHarmonyMemberNames(typeof(CortexShellState));
            AssertNoHarmonyMemberNames(typeof(CortexWorkbenchIds));
            AssertNoHarmonyMemberNames(typeof(IEditorModuleServices));
            AssertNoHarmonyMemberNames(typeof(EditorSurfaceServices));
            AssertNoHarmonyMemberNames(typeof(EditorMethodInspectorNavigationActionFactory));
            AssertNoHarmonyMemberNames(typeof(EditorMethodInspectorNavigationActionCodec));
            AssertNoHarmonyMemberNames(typeof(IWorkbenchRuntimeAccess));
            AssertNoHarmonyMemberNames(typeof(IWorkbenchModuleRuntime));
        }

        [Fact]
        public void GenericAssemblies_DoNotOwnHarmonySpecificTypes()
        {
            AssertNoHarmonyTypes(typeof(CortexProjectDefinition).Assembly);
            AssertNoHarmonyTypes(typeof(IWorkbenchRuntimeAccess).Assembly);
            AssertNoHarmonyTypes(typeof(CortexShellController).Assembly);
        }

        [Fact]
        public void RuntimeAccessShell_StaysNarrowAndTyped()
        {
            var runtimeMembers = typeof(IWorkbenchRuntimeAccess)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var moduleRuntimeMembers = typeof(IWorkbenchModuleRuntime)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "Feedback", "Modules" }, runtimeMembers);
            Assert.Equal(new[] { "Commands", "Documents", "Editor", "Lifecycle", "Navigation", "Projects", "State" }, moduleRuntimeMembers);
        }

        [Fact]
        public void GenericEditorAndShellSource_DoesNotContainHarmonyCoupling()
        {
            var sourceFiles = new[]
            {
                Path.Combine(RepoRoot, "Cortex.Plugins.Abstractions", "WorkbenchExtensionAbstractions.cs"),
                Path.Combine(RepoRoot, "Cortex.Plugins.Abstractions", "WorkbenchRuntimeAbstractions.cs"),
                Path.Combine(RepoRoot, "Cortex.Core", "Abstractions", "PlatformInterfaces.cs"),
                Path.Combine(RepoRoot, "Cortex.Core", "Models", "ContributionModels.cs"),
                Path.Combine(RepoRoot, "Cortex.Core", "Models", "WorkbenchIds.cs"),
                Path.Combine(RepoRoot, "Cortex", "State", "CortexShellState.cs"),
                Path.Combine(RepoRoot, "Cortex", "Shell", "CortexShellModuleCapabilities.cs"),
                Path.Combine(RepoRoot, "Cortex", "Shell", "ModuleRuntime.cs"),
                Path.Combine(RepoRoot, "Cortex", "Shell", "ShellBootstrapper.cs"),
                Path.Combine(RepoRoot, "Cortex", "Shell", "CortexShellBuiltInModuleContributions.cs"),
                Path.Combine(RepoRoot, "Cortex", "Shell", "WorkbenchExtensionServices.cs"),
                Path.Combine(RepoRoot, "Cortex", "WorkbenchPluginLoader.cs"),
                Path.Combine(RepoRoot, "Cortex", "Modules", "FileExplorer", "FileExplorerModule.cs")
            }
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot, "Cortex", "Modules", "Editor"), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot, "Cortex", "Services", "Inspector", "Actions"), "*.cs", SearchOption.AllDirectories))
            .Concat(new[]
            {
                Path.Combine(RepoRoot, "Cortex", "Services", "Navigation", "Symbols", "LanguageSymbolNavigationService.cs"),
                Path.Combine(RepoRoot, "Cortex", "Services", "Inspector", "Composition", "EditorMethodInspectorHostViewComposer.cs"),
                Path.Combine(RepoRoot, "Cortex", "Services", "Inspector", "EditorMethodInspectorHostPresentationService.cs"),
                Path.Combine(RepoRoot, "Cortex", "Services", "Inspector", "EditorMethodInspectorPreparedView.cs")
            })
            .ToArray();

            var violatingFile = sourceFiles.FirstOrDefault(path => File.ReadAllText(path).IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.True(violatingFile == null, "Found Harmony coupling in generic source: " + violatingFile);
        }

        [Fact]
        public void HostAssembly_DoesNotContainBuiltInHarmonyNamespaces()
        {
            var namespaces = typeof(CortexShellController).Assembly
                .GetTypes()
                .Select(type => type.Namespace ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.DoesNotContain(namespaces, value => value.StartsWith("Cortex.Modules.Harmony", StringComparison.Ordinal));
            Assert.DoesNotContain(namespaces, value => value.StartsWith("Cortex.Services.Harmony", StringComparison.Ordinal));
        }

        [Fact]
        public void GenericHostAssemblies_DoNotReferenceHarmonyPluginAssembly()
        {
            var hostReferences = typeof(CortexShellController).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();
            var coreReferences = typeof(CortexProjectDefinition).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();
            var pluginAbstractionReferences = typeof(IWorkbenchRuntimeAccess).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.DoesNotContain("Cortex.Plugin.Harmony", hostReferences);
            Assert.DoesNotContain("Cortex.Plugin.Harmony", coreReferences);
            Assert.DoesNotContain("Cortex.Plugin.Harmony", pluginAbstractionReferences);
        }

        [Fact]
        public void PluginAbstractions_ExposeSingleContributorEntryPoint()
        {
            var pluginAssembly = typeof(IWorkbenchPluginContributor).Assembly;

            Assert.NotNull(pluginAssembly.GetType("Cortex.Plugins.Abstractions.IWorkbenchPluginContributor", false));
            Assert.Null(pluginAssembly.GetType("Cortex.Plugins.Abstractions.IWorkbenchPlugin", false));
        }

        [Fact]
        public void ExternalHarmonyPlugin_DoesNotReferenceHostShellAssemblies()
        {
            var references = typeof(HarmonyPluginContributor).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.DoesNotContain("Cortex", references);
            Assert.DoesNotContain("Cortex.Shell.Unity.Imgui", references);
            Assert.Contains("Cortex.Core", references);
            Assert.Contains("Cortex.Plugins.Abstractions", references);
            Assert.Contains("Cortex.Presentation", references);
        }

        [Fact]
        public void HarmonyPlugin_OwnsHarmonySpecificTypes()
        {
            var pluginAssembly = typeof(HarmonyPluginContributor).Assembly;
            var typeNames = pluginAssembly.GetTypes().Select(type => type.Name).ToArray();

            Assert.Contains(typeNames, name => string.Equals(name, "HarmonyPatchNavigationTarget", StringComparison.Ordinal));
            Assert.Contains(typeNames, name => string.Equals(name, "HarmonyPatchGenerationRequest", StringComparison.Ordinal));
        }

        private static void AssertNoHarmonyMemberNames(Type type)
        {
            var memberNames = type
                .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(member => member.MemberType == MemberTypes.Property ||
                    member.MemberType == MemberTypes.Field ||
                    member.MemberType == MemberTypes.Method)
                .Select(member => member.Name)
                .ToArray();

            Assert.DoesNotContain(memberNames, name => name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AssertNoHarmonyTypes(Assembly assembly)
        {
            var typeNames = assembly
                .GetTypes()
                .Select(type => type.Name)
                .ToArray();

            Assert.DoesNotContain(typeNames, name => name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0);
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

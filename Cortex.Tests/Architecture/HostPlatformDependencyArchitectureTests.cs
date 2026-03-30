using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void ModApiAssembly_OwnsGameRuntimeBootstrapComposition()
        {
            var bootstrapType = typeof(ModApiCortexRuntimeBootstrap);

            Assert.True(typeof(IGameRuntimeBootstrap).IsAssignableFrom(bootstrapType));
            Assert.Equal(typeof(ModApiCortexRuntimeBootstrap).Assembly, bootstrapType.Assembly);
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

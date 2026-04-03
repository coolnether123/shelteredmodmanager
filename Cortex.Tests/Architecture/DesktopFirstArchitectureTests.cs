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

            Assert.Contains("desktop-first", architectureGuideText);
            Assert.Contains("legacy host/shell path", architectureGuideText);
            Assert.Contains("Cortex.Contracts", architectureGuideText);
            Assert.Contains("Avalonia", architectureGuideText);
            Assert.Contains("Dock", architectureGuideText);
            Assert.Contains("Serilog", architectureGuideText);

            Assert.Contains("desktop-first architecture", portabilityReportText);
            Assert.Contains("net35", portabilityReportText);
            Assert.Contains("legacy shell/backend", portabilityReportText);
            Assert.Contains("Cortex.Contracts", portabilityReportText);
            Assert.Contains("External-Tool Cortex", portabilityReportText);

            Assert.Contains("Desktop-shareable contracts and models", buildTopologyGuideText);
            Assert.Contains("Legacy Unity IMGUI host path", buildTopologyGuideText);
            Assert.Contains("External workers and tools", buildTopologyGuideText);
            Assert.Contains("Future desktop host lane", buildTopologyGuideText);

            Assert.Contains("Desktop-first direction for this phase", guardrailText);
            Assert.Contains("legacy concrete host/shell/backend path", guardrailText);
            Assert.Contains("new desktop-facing contracts/models must not stay trapped only in `net35` projects", guardrailText);
        }
    }
}

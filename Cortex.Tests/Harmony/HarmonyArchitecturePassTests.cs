using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Policy;
using Cortex.Services.Harmony.Resolution;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Harmony.Workflow;

namespace Cortex.Tests.Harmony
{
    public sealed class HarmonyArchitecturePassTests
    {
        [Fact]
        public void TryResolveFromInspectionRequest_ResolvesRuntimeMethod()
        {
            var method = typeof(HarmonyArchitecturePassTests).GetMethod("SampleTarget", BindingFlags.NonPublic | BindingFlags.Static);
            var request = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = method.DeclaringType.Assembly.Location,
                MetadataToken = method.MetadataToken
            };

            var service = new HarmonyPatchResolutionService(
                new HarmonyMetadataTargetResolver(
                    new Cortex.Services.Navigation.Metadata.AssemblyMetadataNavigationService(),
                    new HarmonyMethodIdentityService(),
                    new HarmonyRuntimeMethodLookupService()));

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            var success = service.TryResolveFromInspectionRequest(new TestProjectCatalog(), request, out resolvedTarget, out reason);

            Assert.True(success);
            Assert.NotNull(resolvedTarget);
            Assert.Equal(method.MetadataToken, resolvedTarget.Method.MetadataToken);
            Assert.Contains("SampleTarget", resolvedTarget.DisplayName);
        }

        [Fact]
        public void BuildPreview_SelectedContext_InsertsAfterContainingType()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "cortex-harmony-preview-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                File.WriteAllText(tempPath,
@"namespace Demo
{
    internal class Holder
    {
        private void Existing()
        {
        }
    }
}");

                var service = new HarmonyPatchInsertionService();
                var request = new HarmonyPatchGenerationRequest
                {
                    DestinationFilePath = tempPath,
                    InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext,
                    InsertionLine = 5,
                    InsertionAbsolutePosition = File.ReadAllText(tempPath).IndexOf("Existing", StringComparison.Ordinal)
                };
                var preview = service.BuildPreview(
                    new CortexShellState(),
                    request,
                    new HarmonyPatchGenerationPreview
                    {
                        SnippetText = "internal static class GeneratedPatch\n{\n}\n",
                        CanApply = true
                    });

                Assert.True(preview.CanApply);
                Assert.Contains("after class Holder", preview.InsertionContextLabel);
                Assert.Contains("GeneratedPatch", preview.PreviewText);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void CreateDefaultRequest_UsesResolvedMethodIdentity()
        {
            var method = typeof(HarmonyArchitecturePassTests).GetMethod("SampleInstanceTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            var resolvedTarget = new HarmonyResolvedMethodTarget
            {
                Method = method,
                InspectionRequest = new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = method.DeclaringType.Assembly.Location,
                    MetadataToken = method.MetadataToken
                },
                Project = new CortexProjectDefinition
                {
                    ModId = "Example.Mod"
                }
            };

            var factory = new HarmonyPatchGenerationRequestFactory();
            var request = factory.CreateDefaultRequest(resolvedTarget, HarmonyPatchGenerationKind.Prefix);

            Assert.Equal(method.MetadataToken, request.TargetMetadataToken);
            Assert.Equal("Example_Mod.Harmony", request.NamespaceName);
            Assert.Contains("SampleInstanceTarget", request.PatchClassName);
            Assert.True(request.IncludeInstanceParameter);
            Assert.True(request.IncludeArgumentParameters);
        }

        [Fact]
        public void BeginGeneration_PopulatesHarmonyGenerationState()
        {
            var method = typeof(HarmonyArchitecturePassTests).GetMethod("SampleTarget", BindingFlags.NonPublic | BindingFlags.Static);
            var state = new CortexShellState();
            state.Harmony.ActiveInspectionRequest = new HarmonyPatchInspectionRequest
            {
                AssemblyPath = method.DeclaringType.Assembly.Location,
                MetadataToken = method.MetadataToken
            };

            var workspaceService = new HarmonyPatchWorkspaceService();
            var resolutionService = new HarmonyPatchResolutionService(
                new HarmonyMetadataTargetResolver(
                    new Cortex.Services.Navigation.Metadata.AssemblyMetadataNavigationService(),
                    new HarmonyMethodIdentityService(),
                    new HarmonyRuntimeMethodLookupService()));
            var generationService = new HarmonyPatchGenerationService(new HarmonyPatchTemplateService(), new HarmonyPatchInsertionService());

            var started = workspaceService.BeginGeneration(
                state,
                new TestProjectCatalog(),
                resolutionService,
                generationService,
                HarmonyPatchGenerationKind.Postfix);

            Assert.True(started);
            Assert.NotNull(state.Harmony.GenerationRequest);
            Assert.Equal(method.MetadataToken, state.Harmony.GenerationRequest.TargetMetadataToken);
            Assert.Equal(HarmonyPatchGenerationKind.Postfix, state.Harmony.GenerationRequest.GenerationKind);
        }

        [Fact]
        public void RefreshSummary_TypeScope_RefreshesActiveTypeSummaries()
        {
            var state = new CortexShellState();
            var declaringType = typeof(HarmonyArchitecturePassTests);
            state.Harmony.ActiveTypeAssemblyPath = declaringType.Assembly.Location;
            state.Harmony.ActiveTypeName = declaringType.FullName;
            state.Harmony.ActiveTypeDisplayName = declaringType.FullName;
            state.Harmony.ActiveTypeSummaries = new[]
            {
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = declaringType.Assembly.Location,
                    DeclaringType = declaringType.FullName,
                    MethodName = "StaleMethod",
                    Counts = new HarmonyPatchCounts()
                }
            };
            state.Harmony.RefreshRequested = true;

            var refreshedSummary = new HarmonyMethodPatchSummary
            {
                AssemblyPath = declaringType.Assembly.Location,
                DeclaringType = declaringType.FullName,
                MethodName = "SampleTarget",
                Counts = new HarmonyPatchCounts
                {
                    PrefixCount = 1,
                    TotalCount = 1
                }
            };
            var inspectionService = new HarmonyPatchInspectionService(
                new FakeHarmonyRuntimeInspectionService(new[] { refreshedSummary }),
                new HarmonyPatchOwnershipService(),
                new HarmonyPatchOrderService());

            var workspaceService = new HarmonyPatchWorkspaceService();
            workspaceService.RefreshSummary(
                state,
                new TestLoadedModCatalog(),
                new TestProjectCatalog(),
                inspectionService);

            Assert.False(state.Harmony.RefreshRequested);
            Assert.Single(state.Harmony.ActiveTypeSummaries);
            Assert.Equal("SampleTarget", state.Harmony.ActiveTypeSummaries[0].MethodName);
            Assert.Equal("Loaded Harmony patch details for " + declaringType.FullName + ".", state.StatusMessage);
        }

        [Fact]
        public void ReturnToTypeScope_ClearsMethodSelection_AndPreservesTypeScope()
        {
            var state = new CortexShellState();
            state.Harmony.ActiveTypeName = "Example.Type";
            state.Harmony.ActiveTypeDisplayName = "Example.Type";
            state.Harmony.ActiveSummary = new HarmonyMethodPatchSummary
            {
                MethodName = "ExampleMethod"
            };
            state.Harmony.ActiveInspectionRequest = new HarmonyPatchInspectionRequest
            {
                MethodName = "ExampleMethod"
            };
            state.Harmony.ActiveSummaryKey = "example";
            state.Harmony.GenerationRequest = new HarmonyPatchGenerationRequest();
            state.Harmony.GenerationPreview = new HarmonyPatchGenerationPreview();
            state.Harmony.IsInsertionPickActive = true;
            state.Harmony.InsertionTargets.Add(new HarmonyPatchInsertionTarget());

            var workspaceService = new HarmonyPatchWorkspaceService();
            workspaceService.ReturnToTypeScope(state);

            Assert.Null(state.Harmony.ActiveSummary);
            Assert.Null(state.Harmony.ActiveInspectionRequest);
            Assert.Equal(string.Empty, state.Harmony.ActiveSummaryKey);
            Assert.Null(state.Harmony.GenerationRequest);
            Assert.Null(state.Harmony.GenerationPreview);
            Assert.False(state.Harmony.IsInsertionPickActive);
            Assert.Empty(state.Harmony.InsertionTargets);
            Assert.Equal("Example.Type", state.Harmony.ActiveTypeName);
            Assert.Equal("Showing patched methods for Example.Type.", state.StatusMessage);
        }

        private static int SampleTarget(int value)
        {
            return value + 1;
        }

        private string SampleInstanceTarget(string value)
        {
            return value ?? string.Empty;
        }

        private sealed class FakeHarmonyRuntimeInspectionService : IHarmonyRuntimeInspectionService
        {
            private readonly HarmonyMethodPatchSummary[] _methods;

            public FakeHarmonyRuntimeInspectionService(HarmonyMethodPatchSummary[] methods)
            {
                _methods = methods ?? new HarmonyMethodPatchSummary[0];
            }

            public bool IsAvailable
            {
                get { return true; }
            }

            public HarmonyPatchSnapshot CaptureSnapshot()
            {
                return new HarmonyPatchSnapshot
                {
                    GeneratedUtc = DateTime.UtcNow,
                    StatusMessage = "Snapshot refreshed.",
                    Methods = _methods
                };
            }

            public HarmonyMethodPatchSummary Inspect(HarmonyPatchInspectionRequest request)
            {
                return _methods.Length > 0
                    ? _methods[0]
                    : null;
            }
        }
    }
}

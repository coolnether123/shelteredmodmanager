using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Harmony.Resolution;
using Xunit;

namespace Cortex.Tests.Harmony
{
    public sealed class HarmonyResolutionPipelineTests
    {
        [Fact]
        public void TryResolveFromSourceTarget_UsesFirstSuccessfulStep()
        {
            var expected = new HarmonyResolvedMethodTarget();
            var stepA = new FakeSourceStep(false, null, "attribute");
            var stepB = new FakeSourceStep(true, expected, string.Empty);
            var stepC = new FakeSourceStep(false, null, "fallback");
            var resolver = CreateSourceResolver(stepA, stepB, stepC);

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            var success = resolver.TryResolveFromSourceTarget(
                new CortexShellState(),
                null,
                null,
                new EditorCommandTarget
                {
                    DocumentPath = "Example.cs",
                    SymbolText = "Target"
                },
                out resolvedTarget,
                out reason);

            Assert.True(success);
            Assert.Same(expected, resolvedTarget);
            Assert.Equal(string.Empty, reason);
            Assert.Equal(1, stepA.CallCount);
            Assert.Equal(1, stepB.CallCount);
            Assert.Equal(0, stepC.CallCount);
        }

        [Fact]
        public void TryResolveFromSourceTarget_ReturnsFirstMeaningfulFailureReason()
        {
            var stepA = new FakeSourceStep(false, null, "attribute failed");
            var stepB = new FakeSourceStep(false, null, "hover failed");
            var resolver = CreateSourceResolver(stepA, stepB);

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            var success = resolver.TryResolveFromSourceTarget(
                new CortexShellState(),
                null,
                null,
                new EditorCommandTarget
                {
                    DocumentPath = "Example.cs",
                    SymbolText = "Target"
                },
                out resolvedTarget,
                out reason);

            Assert.False(success);
            Assert.Null(resolvedTarget);
            Assert.Equal("attribute failed", reason);
        }

        [Fact]
        public void TryResolveFromEditorTarget_UsesDecompilerResolverForDecompilerDocuments()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "cortex-harmony-cache");
            var metadataResolver = new FakeMetadataResolver
            {
                DecompiledResult = new HarmonyResolvedMethodTarget()
            };
            var sourceResolver = new FakeSourceResolver();
            var service = new HarmonyPatchResolutionService(metadataResolver, sourceResolver);

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            var success = service.TryResolveFromEditorTarget(
                new CortexShellState
                {
                    Settings = new CortexSettings
                    {
                        DecompilerCachePath = cacheRoot
                    }
                },
                null,
                null,
                new EditorCommandTarget
                {
                    DocumentPath = Path.Combine(cacheRoot, "Assembly", "Type_0x06000001.cs"),
                    SymbolText = "Target",
                    AbsolutePosition = 42
                },
                out resolvedTarget,
                out reason);

            Assert.True(success);
            Assert.Same(metadataResolver.DecompiledResult, resolvedTarget);
            Assert.Equal(1, metadataResolver.DecompilerDocumentCalls);
            Assert.Equal(0, sourceResolver.SourceCalls);
        }

        private static HarmonySourceTargetResolver CreateSourceResolver(params IHarmonySourceTargetResolutionStep[] steps)
        {
            var symbolService = new HarmonySourceSymbolService();
            return new HarmonySourceTargetResolver(
                symbolService,
                new HarmonySourceAttributeTargetResolver(symbolService, new HarmonyMethodIdentityService(), new HarmonyRuntimeMethodLookupService()),
                steps);
        }

        private sealed class FakeSourceStep : IHarmonySourceTargetResolutionStep
        {
            private readonly bool _success;
            private readonly HarmonyResolvedMethodTarget _target;
            private readonly string _reason;

            public FakeSourceStep(bool success, HarmonyResolvedMethodTarget target, string reason)
            {
                _success = success;
                _target = target;
                _reason = reason;
            }

            public int CallCount { get; private set; }

            public bool TryResolve(HarmonySourceResolutionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                CallCount++;
                resolvedTarget = _target;
                reason = _reason;
                return _success;
            }
        }

        private sealed class FakeSourceResolver : IHarmonySourceTargetResolver
        {
            public int SourceCalls;

            public bool TryResolveFromSourceTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                SourceCalls++;
                resolvedTarget = null;
                reason = "not used";
                return false;
            }

            public bool TryResolveSourcePatchContext(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonySourcePatchContext context, out string reason)
            {
                context = null;
                reason = string.Empty;
                return false;
            }
        }

        private sealed class FakeMetadataResolver : IHarmonyMetadataTargetResolver
        {
            public int DecompiledDocumentCalls;
            public HarmonyResolvedMethodTarget DecompiledResult;

            public bool TryResolveFromInspectionRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;
                return false;
            }

            public bool TryResolveTypeFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedTypeTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;
                return false;
            }

            public bool TryResolveFromCallHierarchyItem(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, LanguageServiceCallHierarchyItem item, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;
                return false;
            }

            public bool TryResolveFromDecompilerDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string documentPath, string symbolText, int absolutePosition, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                DecompiledDocumentCalls++;
                resolvedTarget = DecompiledResult;
                reason = string.Empty;
                return DecompiledResult != null;
            }

            public bool TryResolveFromMetadataSymbol(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, string containingAssemblyName, string documentationCommentId, string containingTypeName, string symbolKind, string displayName, string documentPath, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;
                return false;
            }
        }
    }
}

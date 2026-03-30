using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Navigation.Metadata;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonySourceTargetResolver : IHarmonySourceTargetResolver
    {
        private readonly HarmonySourceSymbolService _symbolService;
        private readonly HarmonySourceAttributeTargetResolver _attributeResolver;
        private readonly IHarmonySourceTargetResolutionStep[] _steps;

        public HarmonySourceTargetResolver()
            : this(
                new HarmonyMetadataTargetResolver(
                    new AssemblyMetadataNavigationService(),
                    new HarmonyMethodIdentityService(),
                    new HarmonyRuntimeMethodLookupService()))
        {
        }

        internal HarmonySourceTargetResolver(IHarmonyMetadataTargetResolver metadataTargetResolver)
        {
            _symbolService = new HarmonySourceSymbolService();
            _attributeResolver = new HarmonySourceAttributeTargetResolver(_symbolService, new HarmonyMethodIdentityService(), new HarmonyRuntimeMethodLookupService());
            _steps = new IHarmonySourceTargetResolutionStep[]
            {
                _attributeResolver,
                new HarmonySourceHoverTargetResolver(metadataTargetResolver),
                new HarmonySourceFallbackTargetResolver(_symbolService, new HarmonyMethodIdentityService(), new HarmonyRuntimeMethodLookupService())
            };
        }

        internal HarmonySourceTargetResolver(
            HarmonySourceSymbolService symbolService,
            HarmonySourceAttributeTargetResolver attributeResolver,
            IHarmonySourceTargetResolutionStep[] steps)
        {
            _symbolService = symbolService ?? new HarmonySourceSymbolService();
            _attributeResolver = attributeResolver ?? new HarmonySourceAttributeTargetResolver(_symbolService, new HarmonyMethodIdentityService(), new HarmonyRuntimeMethodLookupService());
            _steps = steps != null && steps.Length > 0
                ? steps
                : new IHarmonySourceTargetResolutionStep[] { _attributeResolver };
        }

        public bool TryResolveFromSourceTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                reason = "Select a resolvable method before using Harmony actions.";
                return false;
            }

            if (CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                reason = "Harmony source resolution is only available for writable source documents.";
                return false;
            }

            var request = new HarmonySourceResolutionRequest
            {
                State = state,
                SourceLookupIndex = sourceLookupIndex,
                ProjectCatalog = projectCatalog,
                Target = target
            };

            var firstReason = string.Empty;
            for (var i = 0; i < _steps.Length; i++)
            {
                string stepReason;
                if (_steps[i].TryResolve(request, out resolvedTarget, out stepReason))
                {
                    reason = string.Empty;
                    return true;
                }

                if (string.IsNullOrEmpty(firstReason) && !string.IsNullOrEmpty(stepReason))
                {
                    firstReason = stepReason;
                }
            }

            reason = !string.IsNullOrEmpty(firstReason)
                ? firstReason
                : "Cortex could not map the current editor context to a runtime method.";
            return false;
        }

        public bool TryResolveSourcePatchContext(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonySourcePatchContext context, out string reason)
        {
            context = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                reason = "Select a Harmony source method to inspect its patch context.";
                return false;
            }

            if (CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                reason = "Harmony source patch context is only available for writable source methods.";
                return false;
            }

            var text = _symbolService.GetDocumentText(state, target.DocumentPath);
            if (string.IsNullOrEmpty(text))
            {
                reason = "Source text was not available for Harmony source context.";
                return false;
            }

            HarmonyMethodLookupHint methodHint;
            if (!_symbolService.TryBuildLookupHint(state, target.DocumentPath, target.AbsolutePosition, target.SymbolText, out methodHint) ||
                methodHint == null)
            {
                reason = "The selected source location does not map to a method declaration.";
                return false;
            }

            string declarationHeader;
            if (!_symbolService.TryExtractEnclosingMethodHeader(text, target.AbsolutePosition, out declarationHeader))
            {
                reason = "The enclosing source method header could not be read for Harmony context.";
                return false;
            }

            bool resolvedFromAttribute;
            var patchKind = _symbolService.ResolveSourcePatchKind(declarationHeader, methodHint.Name, out resolvedFromAttribute);
            if (string.IsNullOrEmpty(patchKind))
            {
                reason = "The selected source method is not a Harmony Prefix, Postfix, Transpiler, or Finalizer.";
                return false;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            if (!_attributeResolver.TryResolve(
                new HarmonySourceResolutionRequest
                {
                    State = state,
                    ProjectCatalog = projectCatalog,
                    Target = target
                },
                out resolvedTarget,
                out reason) ||
                resolvedTarget == null)
            {
                return false;
            }

            context = new HarmonySourcePatchContext
            {
                PatchKind = patchKind,
                SourceMethodName = !string.IsNullOrEmpty(methodHint.Name) ? methodHint.Name : _symbolService.NormalizeMethodName(target.SymbolText),
                ResolutionSource = resolvedFromAttribute ? "attribute" : "convention",
                Target = resolvedTarget
            };
            reason = string.Empty;
            return true;
        }
    }
}

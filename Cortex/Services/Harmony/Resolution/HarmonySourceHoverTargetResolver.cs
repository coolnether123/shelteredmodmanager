using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonySourceHoverTargetResolver : IHarmonySourceTargetResolutionStep
    {
        private readonly IHarmonyMetadataTargetResolver _metadataTargetResolver;

        public HarmonySourceHoverTargetResolver(IHarmonyMetadataTargetResolver metadataTargetResolver)
        {
            _metadataTargetResolver = metadataTargetResolver;
        }

        public bool TryResolve(HarmonySourceResolutionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (request == null || request.Target == null)
            {
                reason = "Hover metadata was not available for the selected symbol.";
                return false;
            }

            var hoverResponse = GetMatchingHoverResponse(request.State, request.Target);
            if (hoverResponse == null || !hoverResponse.Success)
            {
                reason = "Hover metadata was not available for the selected symbol.";
                return false;
            }

            return _metadataTargetResolver.TryResolveFromMetadataSymbol(
                request.State,
                request.SourceLookupIndex,
                request.ProjectCatalog,
                hoverResponse.ContainingAssemblyName,
                hoverResponse.DocumentationCommentId,
                hoverResponse.ContainingTypeName,
                hoverResponse.SymbolKind,
                hoverResponse.SymbolDisplay,
                hoverResponse.DefinitionDocumentPath ?? request.Target.DocumentPath ?? string.Empty,
                out resolvedTarget,
                out reason);
        }

        private static LanguageServiceHoverResponse GetMatchingHoverResponse(CortexShellState state, EditorCommandTarget target)
        {
            if (state == null || state.EditorContext == null || target == null)
            {
                return null;
            }

            var key = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition;
            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                var snapshot = pair.Value;
                if (snapshot == null ||
                    snapshot.Semantic == null ||
                    !string.Equals(snapshot.HoverKey ?? string.Empty, key, StringComparison.Ordinal))
                {
                    continue;
                }

                return snapshot.Semantic.HoverResponse;
            }

            return null;
        }
    }
}

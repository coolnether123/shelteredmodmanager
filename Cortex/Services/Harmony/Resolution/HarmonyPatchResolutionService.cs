using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services.Navigation.Metadata;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonyPatchResolutionService
    {
        private readonly IHarmonyMetadataTargetResolver _metadataTargetResolver;
        private readonly IHarmonySourceTargetResolver _sourceTargetResolver;
        private readonly HarmonyResolutionTargetClassifier _targetClassifier;

        public HarmonyPatchResolutionService()
            : this(
                new HarmonyMetadataTargetResolver(
                    new AssemblyMetadataNavigationService(),
                    new HarmonyMethodIdentityService(),
                    new HarmonyRuntimeMethodLookupService()))
        {
        }

        internal HarmonyPatchResolutionService(IHarmonyMetadataTargetResolver metadataTargetResolver)
        {
            _metadataTargetResolver = metadataTargetResolver;
            _sourceTargetResolver = new HarmonySourceTargetResolver(metadataTargetResolver);
            _targetClassifier = new HarmonyResolutionTargetClassifier();
        }

        internal HarmonyPatchResolutionService(IHarmonyMetadataTargetResolver metadataTargetResolver, IHarmonySourceTargetResolver sourceTargetResolver)
        {
            _metadataTargetResolver = metadataTargetResolver;
            _sourceTargetResolver = sourceTargetResolver;
            _targetClassifier = new HarmonyResolutionTargetClassifier();
        }

        public bool TryResolveFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            HarmonyResolutionTargetRequest request;
            if (!_targetClassifier.TryClassifyEditorTarget(state, target, out request, out reason))
            {
                return false;
            }

            return TryResolveRequest(state, sourceLookupIndex, projectCatalog, request, out resolvedTarget, out reason);
        }

        public bool TryResolveFromInspectionRequest(IProjectCatalog projectCatalog, HarmonyPatchInspectionRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            return _metadataTargetResolver.TryResolveFromInspectionRequest(projectCatalog, request, out resolvedTarget, out reason);
        }

        public bool TryResolveTypeFromEditorTarget(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonyResolvedTypeTarget resolvedTarget, out string reason)
        {
            return _metadataTargetResolver.TryResolveTypeFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out reason);
        }

        public bool TryResolveFromCallHierarchyItem(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, LanguageServiceCallHierarchyItem item, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            return _metadataTargetResolver.TryResolveFromCallHierarchyItem(state, sourceLookupIndex, projectCatalog, item, out resolvedTarget, out reason);
        }

        public bool TryResolveFromDocument(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, DocumentSession session, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            HarmonyResolutionTargetRequest request;
            if (!_targetClassifier.TryClassifyDocument(state, session, out request, out reason))
            {
                return false;
            }

            if (TryResolveRequest(state, sourceLookupIndex, projectCatalog, request, out resolvedTarget, out reason))
            {
                return true;
            }

            if (string.IsNullOrEmpty(reason))
            {
                reason = "The active document does not resolve to a specific Harmony target yet.";
            }

            return false;
        }

        public bool TryResolveSourcePatchContext(CortexShellState state, IProjectCatalog projectCatalog, EditorCommandTarget target, out HarmonySourcePatchContext context, out string reason)
        {
            return _sourceTargetResolver.TryResolveSourcePatchContext(state, projectCatalog, target, out context, out reason);
        }

        private bool TryResolveRequest(CortexShellState state, ISourceLookupIndex sourceLookupIndex, IProjectCatalog projectCatalog, HarmonyResolutionTargetRequest request, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (request == null || request.Target == null)
            {
                reason = "Select a resolvable method before using Harmony actions.";
                return false;
            }

            if (request.Kind == HarmonyResolutionTargetKind.Decompiled)
            {
                if (_metadataTargetResolver.TryResolveFromDecompilerDocument(
                    state,
                    sourceLookupIndex,
                    projectCatalog,
                    request.Target.DocumentPath,
                    request.Target.SymbolText,
                    request.Target.AbsolutePosition,
                    out resolvedTarget,
                    out reason))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(reason))
                {
                    reason = "The selected decompiled member could not be resolved to a unique runtime method.";
                }

                return false;
            }

            return _sourceTargetResolver.TryResolveFromSourceTarget(state, sourceLookupIndex, projectCatalog, request.Target, out resolvedTarget, out reason);
        }
    }
}

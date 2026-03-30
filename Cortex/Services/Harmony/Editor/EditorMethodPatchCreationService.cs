using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Editor
{
    internal sealed class EditorMethodPatchCreationService
    {
        public bool CanPreparePatch(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService,
            out string reason)
        {
            reason = string.Empty;
            HarmonyResolvedMethodTarget resolvedTarget;
            if (!TryResolveGenerationTarget(state, target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, out resolvedTarget, out reason))
            {
                return false;
            }

            return true;
        }

        public bool PreparePatch(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService,
            HarmonyPatchGenerationKind generationKind,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!TryResolveGenerationTarget(state, target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, out resolvedTarget, out reason))
            {
                statusMessage = reason;
                return false;
            }

            var request = harmonyGenerationService.CreateDefaultRequest(resolvedTarget, generationKind);
            var insertionTargets = harmonyGenerationService.BuildInsertionTargets(state, projectCatalog, resolvedTarget, request);
            state.Harmony.ActiveInspectionRequest = resolvedTarget.InspectionRequest;
            state.Harmony.ActiveSummaryKey = string.Empty;
            state.Harmony.GenerationRequest = request;
            state.Harmony.GenerationPreview = harmonyGenerationService.BuildPreview(state, resolvedTarget, request);
            state.Harmony.GenerationStatusMessage = state.Harmony.GenerationPreview != null
                ? state.Harmony.GenerationPreview.StatusMessage ?? string.Empty
                : string.Empty;
            state.Harmony.InsertionTargets.Clear();
            for (var i = 0; i < insertionTargets.Length; i++)
            {
                state.Harmony.InsertionTargets.Add(insertionTargets[i]);
            }

            if (insertionTargets.Length > 0)
            {
                request.DestinationFilePath = insertionTargets[0].FilePath ?? string.Empty;
                request.InsertionAnchorKind = insertionTargets[0].DefaultAnchorKind;
                request.InsertionLine = insertionTargets[0].SuggestedLine;
                request.InsertionAbsolutePosition = insertionTargets[0].SuggestedAbsolutePosition;
                request.InsertionContextLabel = insertionTargets[0].SuggestedContextLabel ?? string.Empty;
            }

            statusMessage = "Choose a destination for the generated " + generationKind + " patch.";
            state.StatusMessage = statusMessage;
            return true;
        }

        public bool IsPreparedForTarget(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService)
        {
            if (state == null || state.Harmony == null || state.Harmony.GenerationRequest == null || target == null || harmonyResolutionService == null)
            {
                return false;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!harmonyResolutionService.TryResolveFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                resolvedTarget.InspectionRequest == null)
            {
                return false;
            }

            var request = state.Harmony.GenerationRequest;
            return string.Equals(request.TargetAssemblyPath ?? string.Empty, resolvedTarget.InspectionRequest.AssemblyPath ?? string.Empty, System.StringComparison.OrdinalIgnoreCase) &&
                request.TargetMetadataToken == resolvedTarget.InspectionRequest.MetadataToken;
        }

        public bool OpenInsertionTarget(
            CortexShellState state,
            IDocumentService documentService,
            HarmonyPatchGenerationService harmonyGenerationService,
            HarmonyPatchInsertionTarget insertionTarget,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || harmonyGenerationService == null || insertionTarget == null)
            {
                statusMessage = "Harmony patch generation is not ready.";
                return false;
            }

            DocumentSession session;
            if (!harmonyGenerationService.TryOpenInsertionTarget(state, documentService, insertionTarget, out session, out statusMessage))
            {
                return false;
            }

            if (session != null && session.EditorState != null)
            {
                session.EditorState.EditModeEnabled = true;
            }

            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            return true;
        }

        private static bool TryResolveGenerationTarget(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService,
            out HarmonyResolvedMethodTarget resolvedTarget,
            out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (state == null || state.Harmony == null || target == null || harmonyResolutionService == null || harmonyGenerationService == null)
            {
                reason = "Harmony patch generation is not available for the selected method.";
                return false;
            }

            if (!harmonyResolutionService.TryResolveFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out reason))
            {
                return false;
            }

            if (!harmonyGenerationService.TryValidateGenerationTarget(state, resolvedTarget, out reason))
            {
                return false;
            }

            return true;
        }
    }
}

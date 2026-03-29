using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Workflow
{
    internal sealed class HarmonyPatchWorkspaceService
    {
        public void EnsureSummary(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService)
        {
            if (state == null || state.Harmony == null || inspectionService == null)
            {
                return;
            }

            if (HasActiveMethodScope(state))
            {
                if (state.Harmony.ActiveSummary == null || state.Harmony.RefreshRequested)
                {
                    RefreshMethodSummary(state, loadedModCatalog, projectCatalog, inspectionService, state.Harmony.RefreshRequested);
                }
            }
            else if (HasActiveTypeScope(state) && state.Harmony.RefreshRequested)
            {
                RefreshTypeSummary(state, loadedModCatalog, projectCatalog, inspectionService, true);
            }
        }

        public void RefreshSummary(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService)
        {
            if (state == null || state.Harmony == null || inspectionService == null)
            {
                return;
            }

            state.Harmony.RefreshRequested = true;
            EnsureSummary(state, loadedModCatalog, projectCatalog, inspectionService);
        }

        public void LoadMethodSummary(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService)
        {
            if (state == null || state.Harmony == null || resolvedTarget == null || inspectionService == null)
            {
                return;
            }

            ClearTypeScope(state);
            state.Harmony.ActiveInspectionRequest = resolvedTarget.InspectionRequest;
            RefreshMethodSummary(state, loadedModCatalog, projectCatalog, inspectionService, false);
        }

        public void LoadTypeSummary(CortexShellState state, HarmonyResolvedTypeTarget resolvedTypeTarget, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService, bool forceRefresh)
        {
            if (state == null || state.Harmony == null || resolvedTypeTarget == null || resolvedTypeTarget.DeclaringType == null || inspectionService == null)
            {
                return;
            }

            ClearGenerationState(state);
            state.Harmony.ActiveInspectionRequest = null;
            state.Harmony.ActiveSummaryKey = string.Empty;
            state.Harmony.ActiveSummary = null;
            state.Harmony.ActiveTypeAssemblyPath = resolvedTypeTarget.AssemblyPath ?? string.Empty;
            state.Harmony.ActiveTypeName = resolvedTypeTarget.DeclaringType.FullName ?? resolvedTypeTarget.DeclaringType.Name ?? string.Empty;
            state.Harmony.ActiveTypeDisplayName = resolvedTypeTarget.DisplayName ?? state.Harmony.ActiveTypeName;
            RefreshTypeSummary(state, loadedModCatalog, projectCatalog, inspectionService, forceRefresh);
        }

        public bool CanBeginGeneration(CortexShellState state, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService)
        {
            string reason;
            HarmonyResolvedMethodTarget resolvedTarget;
            return TryResolveGenerationTarget(state, projectCatalog, resolutionService, generationService, out resolvedTarget, out reason);
        }

        public string GetGenerationAvailabilityReason(CortexShellState state, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService)
        {
            string reason;
            HarmonyResolvedMethodTarget resolvedTarget;
            return TryResolveGenerationTarget(state, projectCatalog, resolutionService, generationService, out resolvedTarget, out reason)
                ? string.Empty
                : reason ?? string.Empty;
        }

        public bool BeginGeneration(CortexShellState state, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService, HarmonyPatchGenerationKind generationKind)
        {
            if (state == null || state.Harmony == null || generationService == null || resolutionService == null)
            {
                return false;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!TryResolveGenerationTarget(state, projectCatalog, resolutionService, generationService, out resolvedTarget, out reason))
            {
                ClearGenerationState(state);
                state.Harmony.GenerationStatusMessage = reason;
                state.StatusMessage = reason;
                return false;
            }

            var request = generationService.CreateDefaultRequest(resolvedTarget, generationKind);
            var insertionTargets = generationService.BuildInsertionTargets(state, projectCatalog, resolvedTarget, request);
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
            }

            state.Harmony.GenerationRequest = request;
            RefreshGenerationPreview(state, projectCatalog, resolutionService, generationService);
            generationService.ArmEditorInsertionPick(state);
            return true;
        }

        public void RefreshGenerationPreview(CortexShellState state, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService)
        {
            if (state == null || state.Harmony == null || generationService == null || resolutionService == null || state.Harmony.GenerationRequest == null)
            {
                return;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!TryResolveGenerationTarget(state, projectCatalog, resolutionService, generationService, out resolvedTarget, out reason))
            {
                ClearGenerationState(state);
                state.Harmony.GenerationStatusMessage = reason;
                state.StatusMessage = reason;
                return;
            }

            state.Harmony.GenerationPreview = generationService.BuildPreview(state, resolvedTarget, state.Harmony.GenerationRequest);
            if (state.Harmony.GenerationRequest != null && state.Harmony.GenerationPreview != null)
            {
                state.Harmony.GenerationRequest.InsertionContextLabel = state.Harmony.GenerationPreview.InsertionContextLabel ?? state.Harmony.GenerationRequest.InsertionContextLabel;
            }

            state.Harmony.GenerationStatusMessage = state.Harmony.GenerationPreview != null
                ? state.Harmony.GenerationPreview.StatusMessage ?? string.Empty
                : "Harmony patch preview is not available.";
        }

        public bool ApplyGeneration(CortexShellState state, IDocumentService documentService, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService, GeneratedTemplateNavigationService templateNavigationService, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || generationService == null || resolutionService == null)
            {
                return false;
            }

            RefreshGenerationPreview(state, projectCatalog, resolutionService, generationService);
            var request = state.Harmony.GenerationRequest;
            var preview = state.Harmony.GenerationPreview;
            if (request == null || preview == null || !preview.CanApply)
            {
                statusMessage = "Harmony patch preview is not ready to apply.";
                return false;
            }

            DocumentSession session;
            if (!generationService.Apply(state, documentService, request, preview, out session, out statusMessage))
            {
                return false;
            }

            if (session != null && session.EditorState != null)
            {
                session.EditorState.EditModeEnabled = true;
            }

            if (templateNavigationService != null && session != null)
            {
                templateNavigationService.StartSession(
                    state,
                    session,
                    preview.Placeholders,
                    preview.InsertionOffset,
                    preview.InsertionOffset + ((preview.SnippetText ?? string.Empty).Length));
            }

            generationService.ClearEditorInsertionPick(state);
            return true;
        }

        public void ActivateMethodSummary(CortexShellState state, HarmonyMethodPatchSummary summary)
        {
            if (state == null || state.Harmony == null || summary == null)
            {
                return;
            }

            state.Harmony.ActiveSummary = summary;
            state.Harmony.ActiveSummaryKey = BuildSummaryKey(summary);
            state.Harmony.ActiveInspectionRequest = BuildInspectionRequest(summary);
            state.Harmony.ResolutionFailureReason = string.Empty;
            state.StatusMessage = "Loaded Harmony patch details for " + (summary.MethodName ?? string.Empty) + ".";
        }

        public void ReturnToTypeScope(CortexShellState state)
        {
            if (state == null || state.Harmony == null || !HasActiveTypeScope(state))
            {
                return;
            }

            var typeLabel = !string.IsNullOrEmpty(state.Harmony.ActiveTypeDisplayName)
                ? state.Harmony.ActiveTypeDisplayName
                : state.Harmony.ActiveTypeName;
            state.Harmony.ActiveSummary = null;
            state.Harmony.ActiveInspectionRequest = null;
            state.Harmony.ActiveSummaryKey = string.Empty;
            ClearGenerationState(state);
            state.StatusMessage = "Showing patched methods for " + (typeLabel ?? string.Empty) + ".";
        }

        public void ClearGenerationState(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.GenerationRequest = null;
            state.Harmony.GenerationPreview = null;
            state.Harmony.IsInsertionPickActive = false;
            state.Harmony.InsertionTargets.Clear();
        }

        public void ClearTypeScope(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.ActiveTypeAssemblyPath = string.Empty;
            state.Harmony.ActiveTypeName = string.Empty;
            state.Harmony.ActiveTypeDisplayName = string.Empty;
            state.Harmony.ActiveTypeSummaries = new HarmonyMethodPatchSummary[0];
        }

        private static bool HasActiveMethodScope(CortexShellState state)
        {
            return state != null &&
                state.Harmony != null &&
                state.Harmony.ActiveInspectionRequest != null;
        }

        private static bool HasActiveTypeScope(CortexShellState state)
        {
            return state != null &&
                state.Harmony != null &&
                !string.IsNullOrEmpty(state.Harmony.ActiveTypeName);
        }

        private static void RefreshMethodSummary(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService, bool forceRefresh)
        {
            if (!HasActiveMethodScope(state) || inspectionService == null)
            {
                return;
            }

            string statusMessage;
            state.Harmony.ActiveSummary = inspectionService.GetSummary(
                state,
                state.Harmony.ActiveInspectionRequest,
                loadedModCatalog,
                projectCatalog,
                forceRefresh,
                out statusMessage);
            state.Harmony.ActiveSummaryKey = inspectionService.BuildKey(state.Harmony.ActiveInspectionRequest);
            state.Harmony.RefreshRequested = false;
            state.Harmony.ResolutionFailureReason = string.Empty;
            state.StatusMessage = statusMessage;
        }

        private static void RefreshTypeSummary(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, HarmonyPatchInspectionService inspectionService, bool forceRefresh)
        {
            if (!HasActiveTypeScope(state) || inspectionService == null)
            {
                return;
            }

            string statusMessage;
            state.Harmony.ActiveTypeSummaries = inspectionService.GetTypeSummaries(
                state,
                state.Harmony.ActiveTypeAssemblyPath,
                state.Harmony.ActiveTypeName,
                loadedModCatalog,
                projectCatalog,
                forceRefresh,
                out statusMessage) ?? new HarmonyMethodPatchSummary[0];
            state.Harmony.RefreshRequested = false;
            state.Harmony.ResolutionFailureReason = string.Empty;
            state.StatusMessage = statusMessage;
        }

        private static string BuildSummaryKey(HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(summary.AssemblyPath) && summary.Target != null && summary.Target.MetadataToken > 0)
            {
                return summary.AssemblyPath + "|0x" + summary.Target.MetadataToken.ToString("X8");
            }

            return (summary.AssemblyPath ?? string.Empty) + "|" +
                (summary.DeclaringType ?? string.Empty) + "|" +
                (summary.MethodName ?? string.Empty) + "|" +
                (summary.Signature ?? string.Empty);
        }

        private static HarmonyPatchInspectionRequest BuildInspectionRequest(HarmonyMethodPatchSummary summary)
        {
            return summary == null
                ? null
                : new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = summary.AssemblyPath ?? string.Empty,
                    MetadataToken = summary.Target != null ? summary.Target.MetadataToken : 0,
                    DeclaringTypeName = summary.DeclaringType ?? string.Empty,
                    MethodName = summary.MethodName ?? string.Empty,
                    Signature = summary.Signature ?? string.Empty,
                    DisplayName = summary.ResolvedMemberDisplayName ?? string.Empty,
                    DocumentPath = summary.DocumentPath ?? string.Empty,
                    CachePath = summary.CachePath ?? string.Empty
                };
        }

        private static bool TryResolveGenerationTarget(CortexShellState state, IProjectCatalog projectCatalog, HarmonyPatchResolutionService resolutionService, HarmonyPatchGenerationService generationService, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (state == null || state.Harmony == null || generationService == null || state.Harmony.ActiveInspectionRequest == null)
            {
                reason = "Select a resolvable external runtime method before generating a Harmony patch.";
                return false;
            }

            if (!resolutionService.TryResolveFromInspectionRequest(projectCatalog, state.Harmony.ActiveInspectionRequest, out resolvedTarget, out reason))
            {
                return false;
            }

            return generationService.TryValidateGenerationTarget(state, resolvedTarget, out reason);
        }
    }
}

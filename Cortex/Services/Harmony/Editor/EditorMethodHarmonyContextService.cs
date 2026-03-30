using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Inspector.Relationships;

namespace Cortex.Services.Harmony.Editor
{
    internal sealed class EditorIndirectHarmonyCallerContext
    {
        public LanguageServiceCallHierarchyItem Caller;
        public HarmonyMethodPatchSummary Summary;
    }

    internal sealed class EditorIndirectHarmonyContext
    {
        public bool IsLoading;
        public string StatusMessage = string.Empty;
        public int IncomingCallerCount;
        public int PatchedCallerCount;
        public int UnresolvedCallerCount;
        public EditorIndirectHarmonyCallerContext[] PatchedCallers = new EditorIndirectHarmonyCallerContext[0];
    }

    internal sealed class EditorSourceHarmonyContext
    {
        public bool IsPatchMethod;
        public string StatusMessage = string.Empty;
        public string PatchKind = string.Empty;
        public string SourceMethodName = string.Empty;
        public string ResolutionSource = string.Empty;
        public string TargetDisplayName = string.Empty;
        public string TargetTypeName = string.Empty;
        public string TargetMethodName = string.Empty;
        public string TargetSignature = string.Empty;
        public HarmonyPatchInspectionRequest TargetInspectionRequest;
    }

    internal sealed class EditorMethodHarmonyContextService
    {
        public EditorSourceHarmonyContext BuildSourcePatchContext(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            HarmonyPatchResolutionService harmonyResolutionService)
        {
            var context = new EditorSourceHarmonyContext();
            if (state == null || target == null || projectCatalog == null || harmonyResolutionService == null)
            {
                context.StatusMessage = "Harmony source context is not available for this method.";
                return context;
            }

            HarmonySourcePatchContext sourceContext;
            string reason;
            if (!harmonyResolutionService.TryResolveSourcePatchContext(state, projectCatalog, target, out sourceContext, out reason) ||
                sourceContext == null ||
                sourceContext.Target == null ||
                sourceContext.Target.Method == null)
            {
                context.StatusMessage = !string.IsNullOrEmpty(reason)
                    ? reason
                    : "The selected method is not recognized as a Harmony patch entry point.";
                return context;
            }

            context.IsPatchMethod = true;
            context.PatchKind = sourceContext.PatchKind ?? string.Empty;
            context.SourceMethodName = sourceContext.SourceMethodName ?? string.Empty;
            context.ResolutionSource = sourceContext.ResolutionSource ?? string.Empty;
            context.TargetDisplayName = sourceContext.Target.DisplayName ?? string.Empty;
            context.TargetTypeName = sourceContext.Target.Method.DeclaringType != null
                ? sourceContext.Target.Method.DeclaringType.FullName ?? sourceContext.Target.Method.DeclaringType.Name ?? string.Empty
                : string.Empty;
            context.TargetMethodName = sourceContext.Target.Method.Name ?? string.Empty;
            context.TargetSignature = new HarmonyMethodIdentityService().BuildMethodSignature(sourceContext.Target.Method);
            context.TargetInspectionRequest = sourceContext.Target.InspectionRequest;
            context.StatusMessage = "This method is a Harmony " + context.PatchKind + " patch for " +
                (!string.IsNullOrEmpty(context.TargetDisplayName) ? context.TargetDisplayName : "the resolved runtime target") + ".";
            return context;
        }

        public EditorIndirectHarmonyContext BuildIndirectContext(
            CortexShellState state,
            EditorMethodRelationshipsContext relationshipsContext,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService)
        {
            var context = new EditorIndirectHarmonyContext();
            if (state == null || relationshipsContext == null || harmonyInspectionService == null || harmonyResolutionService == null || projectCatalog == null)
            {
                context.StatusMessage = "Indirect Harmony context is not available.";
                return context;
            }

            if (!relationshipsContext.IsExpanded)
            {
                context.StatusMessage = "Expand Relationships to analyze indirect Harmony relevance.";
                return context;
            }

            if (relationshipsContext.IsLoading)
            {
                context.IsLoading = true;
                context.StatusMessage = !string.IsNullOrEmpty(relationshipsContext.StatusMessage)
                    ? relationshipsContext.StatusMessage
                    : "Analyzing method relationships.";
                return context;
            }

            if (!relationshipsContext.HasResponse)
            {
                context.StatusMessage = !string.IsNullOrEmpty(relationshipsContext.StatusMessage)
                    ? relationshipsContext.StatusMessage
                    : "Method relationship analysis has not produced any results yet.";
                return context;
            }

            var incomingCalls = relationshipsContext.IncomingCallHierarchy ?? new LanguageServiceCallHierarchyItem[0];
            context.IncomingCallerCount = incomingCalls.Length;
            if (incomingCalls.Length == 0)
            {
                context.StatusMessage = "No incoming callers were found for this method.";
                return context;
            }

            var patchedCallers = new List<EditorIndirectHarmonyCallerContext>();
            var seenKeys = new HashSet<string>();
            for (var i = 0; i < incomingCalls.Length; i++)
            {
                var caller = incomingCalls[i];
                if (caller == null)
                {
                    continue;
                }

                HarmonyResolvedMethodTarget resolvedCaller;
                string resolutionReason;
                if (!harmonyResolutionService.TryResolveFromCallHierarchyItem(state, sourceLookupIndex, projectCatalog, caller, out resolvedCaller, out resolutionReason) ||
                    resolvedCaller == null ||
                    resolvedCaller.InspectionRequest == null)
                {
                    context.UnresolvedCallerCount++;
                    continue;
                }

                var summary = harmonyInspectionService.GetSummary(
                    state,
                    resolvedCaller.InspectionRequest,
                    loadedModCatalog,
                    projectCatalog,
                    false,
                    out resolutionReason);
                if (summary == null || !summary.IsPatched)
                {
                    continue;
                }

                var summaryKey = harmonyInspectionService.BuildKey(resolvedCaller.InspectionRequest);
                if (!string.IsNullOrEmpty(summaryKey) && !seenKeys.Add(summaryKey))
                {
                    continue;
                }

                patchedCallers.Add(new EditorIndirectHarmonyCallerContext
                {
                    Caller = caller,
                    Summary = summary
                });
            }

            context.PatchedCallers = patchedCallers.ToArray();
            context.PatchedCallerCount = context.PatchedCallers.Length;
            if (context.PatchedCallerCount > 0)
            {
                context.StatusMessage = context.PatchedCallerCount == 1
                    ? "This method is called by 1 directly patched caller."
                    : "This method is called by " + context.PatchedCallerCount + " directly patched callers.";
                return context;
            }

            context.StatusMessage = context.UnresolvedCallerCount > 0
                ? "No patched incoming callers were confirmed. Some callers could not be mapped to runtime methods."
                : "No directly patched incoming callers were found for this method.";
            return context;
        }
    }
}

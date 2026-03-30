using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Harmony.Editor;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Composition;
using Cortex.Services.Inspector.Lifecycle;
using Cortex.Services.Inspector.Relationships;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Inspector.Identity;

namespace Cortex.Services.Inspector
{
    internal sealed class EditorMethodInspectorPreparedView
    {
        public CortexMethodInspectorState Inspector;
        public EditorCommandInvocation Invocation;
        public EditorCommandTarget Target;
        public MethodInspectorViewModel ViewModel;
    }

    internal sealed class EditorMethodInspectorPresentationService
    {
        private readonly IEditorContextService _contextService;
        private readonly EditorMethodInspectorService _inspectorService;
        private readonly EditorMethodHarmonyContextService _harmonyContextService = new EditorMethodHarmonyContextService();
        private readonly EditorMethodRelationshipsContextService _relationshipsContextService;
        private readonly IEditorMethodTargetContextEnricher _targetContextEnricher;
        private readonly EditorMethodPatchCreationService _patchCreationService = new EditorMethodPatchCreationService();
        private readonly HarmonyPatchDisplayService _fallbackHarmonyDisplayService = new HarmonyPatchDisplayService();
        private readonly IEditorMethodInspectorViewComposer _viewComposer;

        public EditorMethodInspectorPresentationService(IEditorContextService contextService)
            : this(
                contextService,
                new EditorMethodRelationshipsContextService(),
                new EditorMethodTargetContextEnricher(contextService),
                new EditorMethodInspectorViewComposer(new EditorMethodInspectorNavigationActionFactory()))
        {
        }

        internal EditorMethodInspectorPresentationService(
            IEditorContextService contextService,
            EditorMethodRelationshipsContextService relationshipsContextService,
            IEditorMethodTargetContextEnricher targetContextEnricher,
            IEditorMethodInspectorViewComposer viewComposer)
        {
            _contextService = contextService;
            _inspectorService = new EditorMethodInspectorService(contextService);
            _relationshipsContextService = relationshipsContextService;
            _targetContextEnricher = targetContextEnricher;
            _viewComposer = viewComposer;
        }

        public EditorMethodInspectorPreparedView Prepare(
            CortexShellState state,
            DocumentSession session,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var invocation = _contextService != null ? _contextService.ResolveInvocation(state, inspector != null ? inspector.ContextKey : string.Empty) : null;
            var target = invocation != null ? invocation.Target : null;
            if (target == null)
            {
                _inspectorService.Close(state);
                return null;
            }

            _targetContextEnricher.EnsureSymbolContextRequest(state, target);
            _targetContextEnricher.Enrich(target, session, state);

            if (inspector != null && inspector.RelationshipsExpanded)
            {
                _inspectorService.EnsureRelationshipsRequest(state);
            }

            var relationshipsContext = _relationshipsContextService.BuildContext(inspector, target);
            var sourceHarmonyContext = _harmonyContextService.BuildSourcePatchContext(
                state,
                target,
                projectCatalog,
                harmonyResolutionService);

            string harmonyStatusMessage;
            var harmonySummary = TryLoadConditionalHarmonySummary(
                state,
                target,
                sourceHarmonyContext,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService,
                out harmonyStatusMessage);

            var indirectHarmonyContext = _harmonyContextService.BuildIndirectContext(
                state,
                relationshipsContext,
                loadedModCatalog,
                projectCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService);

            var showHarmony = ShouldShowHarmony(sourceHarmonyContext, harmonySummary, indirectHarmonyContext);

            string patchAvailabilityReason = string.Empty;
            var canCreatePatch = false;
            var hasPreparedPatch = false;
            if (showHarmony)
            {
                canCreatePatch = _patchCreationService.CanPreparePatch(
                    state,
                    target,
                    projectCatalog,
                    sourceLookupIndex,
                    harmonyResolutionService,
                    harmonyGenerationService,
                    out patchAvailabilityReason);
                hasPreparedPatch = _patchCreationService.IsPreparedForTarget(
                    state,
                    target,
                    projectCatalog,
                    sourceLookupIndex,
                    harmonyResolutionService);
            }

            return new EditorMethodInspectorPreparedView
            {
                Inspector = inspector,
                Invocation = invocation,
                Target = target,
                ViewModel = _viewComposer.Compose(
                    state,
                    session,
                    inspector,
                    target,
                    relationshipsContext,
                    sourceHarmonyContext,
                    harmonySummary,
                    harmonyStatusMessage,
                    indirectHarmonyContext,
                    harmonyDisplayService ?? _fallbackHarmonyDisplayService,
                    showHarmony,
                    canCreatePatch,
                    hasPreparedPatch,
                    patchAvailabilityReason)
            };
        }

        internal static bool ShouldShowHarmony(
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            EditorIndirectHarmonyContext indirectHarmonyContext)
        {
            return EditorMethodInspectorViewComposer.ShouldShowHarmony(sourceHarmonyContext, harmonySummary, indirectHarmonyContext);
        }

        internal MethodInspectorViewModel BuildViewModel(
            CortexShellState state,
            DocumentSession session,
            CortexMethodInspectorState inspector,
            EditorCommandTarget target,
            EditorMethodRelationshipsContext relationshipsContext,
            EditorSourceHarmonyContext sourceHarmonyContext,
            HarmonyMethodPatchSummary harmonySummary,
            string harmonyStatusMessage,
            EditorIndirectHarmonyContext indirectHarmonyContext,
            HarmonyPatchDisplayService harmonyDisplayService,
            bool showHarmony,
            bool canCreatePatch,
            bool hasPreparedPatch,
            string patchAvailabilityReason)
        {
            return _viewComposer.Compose(
                state,
                session,
                inspector,
                target,
                relationshipsContext,
                sourceHarmonyContext,
                harmonySummary,
                harmonyStatusMessage,
                indirectHarmonyContext,
                harmonyDisplayService,
                showHarmony,
                canCreatePatch,
                hasPreparedPatch,
                patchAvailabilityReason);
        }

        private HarmonyMethodPatchSummary TryLoadConditionalHarmonySummary(
            CortexShellState state,
            EditorCommandTarget target,
            EditorSourceHarmonyContext sourceHarmonyContext,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || harmonyInspectionService == null || projectCatalog == null)
            {
                return null;
            }

            HarmonyPatchInspectionRequest inspectionRequest = null;
            if (sourceHarmonyContext != null &&
                sourceHarmonyContext.IsPatchMethod &&
                sourceHarmonyContext.TargetInspectionRequest != null)
            {
                inspectionRequest = sourceHarmonyContext.TargetInspectionRequest;
            }
            else
            {
                if (target == null || harmonyResolutionService == null)
                {
                    return null;
                }

                HarmonyResolvedMethodTarget resolvedTarget;
                string resolutionReason;
                if (!harmonyResolutionService.TryResolveFromEditorTarget(state, sourceLookupIndex, projectCatalog, target, out resolvedTarget, out resolutionReason) ||
                    resolvedTarget == null ||
                    resolvedTarget.InspectionRequest == null)
                {
                    statusMessage = resolutionReason ?? string.Empty;
                    return null;
                }

                inspectionRequest = resolvedTarget.InspectionRequest;
            }

            string snapshotStatus;
            var summary = harmonyInspectionService.GetCachedSummary(
                state,
                inspectionRequest,
                loadedModCatalog,
                projectCatalog,
                true,
                out snapshotStatus);
            if (summary != null && summary.IsPatched)
            {
                statusMessage = !string.IsNullOrEmpty(snapshotStatus)
                    ? snapshotStatus
                    : "Loaded Harmony patch details for the resolved runtime method.";
                return summary;
            }

            statusMessage = sourceHarmonyContext != null && sourceHarmonyContext.IsPatchMethod
                ? "No live Harmony patches are registered for the patched runtime target."
                : "No live Harmony patches are registered for this method.";
            return null;
        }
    }
}

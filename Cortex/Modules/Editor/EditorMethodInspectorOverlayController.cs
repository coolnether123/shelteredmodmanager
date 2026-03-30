using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    internal interface IEditorMethodInspectorOverlayController
    {
        Rect PredictRect(CortexShellState state, DocumentSession session, Rect anchorRect, Vector2 surfaceSize);
        void HandlePreDrawInput(Event current, Rect panelRect, Vector2 localPointer);
        Rect Draw(
            CortexShellState state,
            DocumentSession session,
            Rect anchorRect,
            Vector2 surfaceSize,
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle containerStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService,
            IPanelRenderer panelRenderer);
    }

    internal sealed class EditorMethodInspectorOverlayController : IEditorMethodInspectorOverlayController
    {
        private readonly EditorMethodInspectorSurface _surface;

        public EditorMethodInspectorOverlayController(IEditorContextService contextService)
        {
            _surface = new EditorMethodInspectorSurface(contextService);
        }

        public Rect PredictRect(CortexShellState state, DocumentSession session, Rect anchorRect, Vector2 surfaceSize)
        {
            return _surface.PredictRect(state, session != null ? session.FilePath : string.Empty, anchorRect, surfaceSize);
        }

        public void HandlePreDrawInput(Event current, Rect panelRect, Vector2 localPointer)
        {
            _surface.TryHandlePreDrawInput(current, panelRect, localPointer);
        }

        public Rect Draw(
            CortexShellState state,
            DocumentSession session,
            Rect anchorRect,
            Vector2 surfaceSize,
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle containerStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService,
            IPanelRenderer panelRenderer)
        {
            return session != null
                ? _surface.Draw(
                    state,
                    session,
                    session.FilePath,
                    anchorRect,
                    surfaceSize,
                    navigationService,
                    commandRegistry,
                    contributionRegistry,
                    containerStyle,
                    buttonStyle,
                    headerStyle,
                    documentService,
                    projectCatalog,
                    loadedModCatalog,
                    sourceLookupIndex,
                    harmonyInspectionService,
                    harmonyResolutionService,
                    harmonyDisplayService,
                    harmonyGenerationService,
                    panelRenderer)
                : new Rect(0f, 0f, 0f, 0f);
        }
    }
}

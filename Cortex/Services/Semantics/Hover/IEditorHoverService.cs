using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services.Navigation;

namespace Cortex.Services.Semantics.Hover
{
    internal interface IEditorHoverService
    {
        bool TryCreateInteractionTarget(DocumentSession session, CortexShellState state, bool editingEnabled, int absolutePosition, out EditorCommandTarget target);
        bool TryCreateSourceHoverTarget(DocumentSession session, CortexShellState state, bool editingEnabled, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, int absolutePosition, RenderRect anchorRect, string tokenClassification, out EditorHoverTarget hoverTarget);
        bool TryCreateReadOnlyHoverTarget(DocumentSession session, CortexShellState state, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, int absolutePosition, int line, int column, string tokenText, bool canNavigateToDefinition, string tokenClassification, RenderRect anchorRect, out EditorHoverTarget hoverTarget);
        void UpdateHoverRequest(CortexShellState state, string surfaceId, EditorHoverTarget hoverTarget, bool allowHover, bool hasMouse, RenderPoint pointerPosition);
        void RequestHoverNow(CortexShellState state, string surfaceId, EditorHoverTarget hoverTarget);
        bool DrawHover(IHoverTooltipRenderer hoverTooltipRenderer, ICortexNavigationService navigationService, CortexShellState state, string surfaceId, EditorHoverTarget hoverTarget, RenderPoint pointerPosition, RenderSize viewportSize, bool hasMouse, HoverTooltipThemePalette theme, float tooltipWidth, string telemetrySurfaceKind);
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey);
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, DocumentSession session, EditorCommandTarget target);
        bool IsPointerWithinHoverSurface(string surfaceId, RenderPoint pointerPosition);
        void ClearSurfaceHover(CortexShellState state, string surfaceId, IHoverTooltipRenderer hoverTooltipRenderer, string reason = "");
    }
}

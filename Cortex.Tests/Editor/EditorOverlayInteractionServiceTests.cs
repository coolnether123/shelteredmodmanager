using UnityEngine;
using Xunit;
using Cortex.Services.Editor.Input;

namespace Cortex.Tests.Editor
{
    public sealed class EditorOverlayInteractionServiceTests
    {
        [Fact]
        public void ResolvePointerState_UsesHoverSurfaceRoute_WhenPointerIsOnHoverTooltip()
        {
            var service = new EditorOverlayInteractionService();

            var pointerState = service.ResolvePointerState(
                new Rect(0f, 0f, 0f, 0f),
                new Rect(0f, 0f, 0f, 0f),
                false,
                true,
                true,
                new Vector2(24f, 32f));

            Assert.True(pointerState.PointerOnHoverSurface);
            Assert.True(pointerState.PointerOnOverlaySurface);
            Assert.Equal("hover-surface", pointerState.PointerRoute);
            Assert.Equal("hover-surface", pointerState.ScrollOwner);
        }

        [Fact]
        public void ShouldBypassSurfaceInput_ReturnsTrue_ForHoverSurfaceMouseInput()
        {
            var service = new EditorOverlayInteractionService();
            var pointerState = new EditorOverlayPointerState
            {
                PointerOnHoverSurface = true
            };

            Assert.True(service.ShouldBypassSurfaceInput(EventType.MouseDown, pointerState));
        }

        [Fact]
        public void ShouldCloseMethodInspectorOnPointerDown_ReturnsFalse_ForHoverSurfaceClicks()
        {
            var service = new EditorOverlayInteractionService();
            var state = new CortexShellState();
            state.Editor.MethodInspector.IsVisible = true;
            var pointerState = new EditorOverlayPointerState
            {
                PointerOnHoverSurface = true
            };

            Assert.False(service.ShouldCloseMethodInspectorOnPointerDown(EventType.MouseDown, pointerState, state));
        }
    }
}

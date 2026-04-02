using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Shell;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class RuntimeUiShellPlannerTests
    {
        [Fact]
        public void ShellSplitLayoutPlanner_ClampsHorizontalSplit_ForDockHosts()
        {
            var plan = ShellSplitLayoutPlanner.BuildHorizontal(
                new RenderRect(0f, 0f, 1000f, 620f),
                0.95f,
                5f,
                180f,
                180f);

            Assert.Equal(815f, plan.SplitPoint);
            Assert.Equal(815f, plan.FirstRect.Width);
            Assert.Equal(5f, plan.SplitterRect.Width);
            Assert.Equal(180f, plan.SecondRect.Width);
        }

        [Fact]
        public void ShellSplitLayoutPlanner_UsesDragBoundsThatMatchExistingPanelBehavior()
        {
            var bounds = new RenderRect(0f, 0f, 900f, 600f);

            var horizontalMax = ShellSplitLayoutPlanner.ResolveHorizontalDragMaxSplitPoint(bounds, 180f, 180f);
            var verticalMax = ShellSplitLayoutPlanner.ResolveVerticalDragMaxSplitPoint(bounds, 140f, 120f);
            var verticalPlan = ShellSplitLayoutPlanner.BuildVerticalFromSplitPoint(bounds, 470f, 5f);

            Assert.Equal(720f, horizontalMax);
            Assert.Equal(480f, verticalMax);
            Assert.Equal(470f, verticalPlan.FirstRect.Height);
            Assert.Equal(125f, verticalPlan.SecondRect.Height);
        }

        [Fact]
        public void ShellMenuPopupController_ClampsPopupRectToWindowBounds()
        {
            var popupRect = ShellMenuPopupController.BuildPopupRect(
                new RenderRect(0f, 20f, 392f, 30f),
                new RenderRect(390f, 4f, 44f, 18f),
                400f,
                3);

            Assert.Equal(172f, popupRect.X);
            Assert.Equal(44f, popupRect.Y);
            Assert.Equal(220f, popupRect.Width);
            Assert.Equal(94f, popupRect.Height);
        }

        [Fact]
        public void ShellMenuPopupController_PreservesMenuGroupHits_AndConsumesEscape()
        {
            var headerRect = new RenderRect(12f, 24f, 500f, 30f);
            var popupRect = new RenderRect(20f, 48f, 220f, 94f);
            var groupRects = new[] { new RenderRect(18f, 2f, 42f, 20f) };
            var pointerOnGroup = new WorkbenchFrameInputSnapshot
            {
                HasCurrentEvent = true,
                CurrentEventKind = WorkbenchInputEventKind.MouseDown,
                CurrentMousePosition = new RenderPoint(34f, 30f)
            };
            var escape = new WorkbenchFrameInputSnapshot
            {
                HasCurrentEvent = true,
                CurrentEventKind = WorkbenchInputEventKind.KeyDown,
                CurrentKey = WorkbenchInputKey.Escape
            };

            var groupResult = ShellMenuPopupController.EvaluateDismissal(pointerOnGroup, headerRect, popupRect, groupRects);
            var escapeResult = ShellMenuPopupController.EvaluateDismissal(escape, headerRect, popupRect, groupRects);

            Assert.False(groupResult.ShouldClose);
            Assert.True(escapeResult.ShouldClose);
            Assert.True(escapeResult.ShouldConsumeInput);
        }

        [Fact]
        public void ShellOverlayInteractionController_CapturesMouse_ForVisibleCollapsedChrome()
        {
            var input = new WorkbenchFrameInputSnapshot
            {
                PointerPosition = new RenderPoint(24f, 18f)
            };
            var capture = ShellOverlayInteractionController.ResolveInputCapture(
                true,
                false,
                input,
                new[]
                {
                    new ShellChromeHitRegion
                    {
                        Visible = true,
                        IsCollapsed = true,
                        CollapsedRect = new RenderRect(12f, 12f, 100f, 24f)
                    }
                });

            Assert.True(capture.CaptureMouse);
            Assert.False(capture.CaptureKeyboard);
        }

        [Fact]
        public void ShellOverlayInteractionController_BuildsModalAndPromptLayouts()
        {
            var modalRect = ShellOverlayInteractionController.BuildOnboardingModalRect(new RenderRect(0f, 0f, 1280f, 720f));
            var promptRect = ShellOverlayInteractionController.BuildOnboardingPromptRect(modalRect, RenderPoint.Zero, false);

            Assert.Equal(170f, modalRect.X);
            Assert.Equal(86f, modalRect.Y);
            Assert.Equal(940f, modalRect.Width);
            Assert.Equal(548f, modalRect.Height);
            Assert.Equal(880f, promptRect.X);
            Assert.Equal(554f, promptRect.Y);
            Assert.Equal(220f, promptRect.Width);
            Assert.Equal(72f, promptRect.Height);
        }

        [Fact]
        public void ShellOverlayInteractionController_EvaluatesOnboardingPromptActions()
        {
            var fullscreenRect = new RenderRect(0f, 0f, 1280f, 720f);
            var modalRect = ShellOverlayInteractionController.BuildOnboardingModalRect(fullscreenRect);
            var promptRect = ShellOverlayInteractionController.BuildOnboardingPromptRect(modalRect, RenderPoint.Zero, false);
            var outsideClick = new WorkbenchFrameInputSnapshot
            {
                HasCurrentEvent = true,
                CurrentEventKind = WorkbenchInputEventKind.MouseDown,
                CurrentMousePosition = new RenderPoint(40f, 60f)
            };
            var escape = new WorkbenchFrameInputSnapshot
            {
                HasCurrentEvent = true,
                CurrentEventKind = WorkbenchInputEventKind.KeyDown,
                CurrentKey = WorkbenchInputKey.Escape
            };

            var outsideResult = ShellOverlayInteractionController.EvaluateOnboardingInput(outsideClick, fullscreenRect, modalRect, promptRect);
            var escapeResult = ShellOverlayInteractionController.EvaluateOnboardingInput(escape, fullscreenRect, modalRect, promptRect);

            Assert.Equal(ShellOnboardingOverlayActionKind.ShowPrompt, outsideResult.ActionKind);
            Assert.Equal(40f, outsideResult.PromptAnchor.X);
            Assert.Equal(60f, outsideResult.PromptAnchor.Y);
            Assert.True(outsideResult.ShouldConsumeInput);

            Assert.Equal(ShellOnboardingOverlayActionKind.ShowPrompt, escapeResult.ActionKind);
            Assert.Equal(990f, escapeResult.PromptAnchor.X);
            Assert.Equal(580f, escapeResult.PromptAnchor.Y);
            Assert.True(escapeResult.ShouldConsumeInput);
        }
    }
}

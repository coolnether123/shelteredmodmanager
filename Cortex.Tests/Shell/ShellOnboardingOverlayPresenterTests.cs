using Cortex;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class ShellOnboardingOverlayPresenterTests
    {
        [Fact]
        public void BuildPresentation_UsesOverlayPlannerGeometry_AndPreviewFlag()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var presenter = new ShellOnboardingOverlayPresenter();
                var onboardingState = new CortexOnboardingState();
                var input = new WorkbenchFrameInputSnapshot
                {
                    ViewportSize = new RenderSize(1280f, 720f),
                    HasCurrentEvent = true,
                    CurrentMousePosition = new RenderPoint(40f, 60f)
                };

                var presentation = presenter.BuildPresentation(onboardingState, input);

                Assert.Equal(170f, presentation.ModalRect.x);
                Assert.Equal(86f, presentation.ModalRect.y);
                Assert.Equal(940f, presentation.ModalRect.width);
                Assert.Equal(548f, presentation.ModalRect.height);
                Assert.Equal(878f, presentation.PromptRect.x);
                Assert.Equal(554f, presentation.PromptRect.y);
                Assert.True(presentation.PreviewBackground);
            });
        }

        [Fact]
        public void ApplyInteraction_UpdatesFinishPromptState_FromOverlayPlannerActions()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var presenter = new ShellOnboardingOverlayPresenter();
                var onboardingState = new CortexOnboardingState();
                var presentation = presenter.BuildPresentation(
                    onboardingState,
                    new WorkbenchFrameInputSnapshot
                    {
                        ViewportSize = new RenderSize(1280f, 720f)
                    });
                var consumed = false;

                presenter.ApplyInteraction(
                    onboardingState,
                    new WorkbenchFrameInputSnapshot
                    {
                        HasCurrentEvent = true,
                        CurrentEventKind = WorkbenchInputEventKind.MouseDown,
                        CurrentMousePosition = new RenderPoint(40f, 60f)
                    },
                    presentation,
                    delegate { consumed = true; });

                Assert.True(consumed);
                Assert.True(onboardingState.FinishPrompt.IsVisible);
                Assert.Equal(40f, onboardingState.FinishPrompt.Anchor.x);
                Assert.Equal(60f, onboardingState.FinishPrompt.Anchor.y);

                presenter.ApplyInteraction(
                    onboardingState,
                    new WorkbenchFrameInputSnapshot
                    {
                        HasCurrentEvent = true,
                        CurrentEventKind = WorkbenchInputEventKind.MouseDown,
                        CurrentMousePosition = new RenderPoint(200f, 120f)
                    },
                    presentation,
                    null);

                Assert.False(onboardingState.FinishPrompt.IsVisible);
            });
        }
    }
}

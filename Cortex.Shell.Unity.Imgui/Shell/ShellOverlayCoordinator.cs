using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Shell;
using UnityEngine;
using Cortex.Services.Onboarding;

namespace Cortex.Shell
{
    internal sealed class ShellOverlayCoordinator
    {
        private const string OverlayInputCaptureOwnerId = "Cortex.Shell";

        private readonly CortexShellState _state;
        private readonly CortexShellViewState _viewState;
        private readonly CortexOnboardingCoordinator _onboardingCoordinator;
        private readonly CortexShellOnboardingLifecycle _onboardingLifecycle;
        private readonly ShellOnboardingOverlayPresenter _onboardingPresenter;
        private readonly Func<IOverlayInputCaptureService> _overlayInputCaptureServiceProvider;
        private readonly Func<WorkbenchFrameInputSnapshot> _frameInputProvider;
        private readonly Action _consumeEventAction;

        private bool _lastOverlayMouseCapture;
        private bool _lastOverlayKeyboardCapture;

        public ShellOverlayCoordinator(
            CortexShellState state,
            CortexShellViewState viewState,
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellOnboardingLifecycle onboardingLifecycle,
            ShellOnboardingOverlayPresenter onboardingPresenter,
            Func<IOverlayInputCaptureService> overlayInputCaptureServiceProvider,
            Func<WorkbenchFrameInputSnapshot> frameInputProvider,
            Action consumeEventAction)
        {
            _state = state;
            _viewState = viewState ?? new CortexShellViewState();
            _onboardingCoordinator = onboardingCoordinator;
            _onboardingLifecycle = onboardingLifecycle;
            _onboardingPresenter = onboardingPresenter ?? new ShellOnboardingOverlayPresenter();
            _overlayInputCaptureServiceProvider = overlayInputCaptureServiceProvider;
            _frameInputProvider = frameInputProvider;
            _consumeEventAction = consumeEventAction;
        }

        public void UpdateOverlayInputCapture(bool visible, IList<ShellChromeHitRegion> chromeRegions)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var capture = ShellOverlayInteractionController.ResolveInputCapture(
                visible,
                _state.Onboarding.IsActive,
                input,
                chromeRegions);
            ApplyOverlayInputCapture(capture.CaptureMouse, capture.CaptureKeyboard);
        }

        public void ReleaseOverlayInputCapture() => ApplyOverlayInputCapture(false, false);

        private void ApplyOverlayInputCapture(bool captureMouse, bool captureKeyboard)
        {
            if (_lastOverlayMouseCapture == captureMouse && _lastOverlayKeyboardCapture == captureKeyboard) return;
            var captureService = _overlayInputCaptureServiceProvider();
            if (captureService == null) return;

            if (captureMouse || captureKeyboard) captureService.ReportCapture(OverlayInputCaptureOwnerId, captureMouse, captureKeyboard);
            else captureService.ReleaseCapture(OverlayInputCaptureOwnerId);

            _lastOverlayMouseCapture = captureMouse; _lastOverlayKeyboardCapture = captureKeyboard;
        }

        public void DrawOnboardingOverlay(IProjectCatalog projectCatalog, IProjectWorkspaceService projectWorkspaceService, IWorkbenchRuntime workbenchRuntime, IPathInteractionService pathInteractionService, Action<string> activateContainerAction, Action persistSessionAction, Action persistWindowSettingsAction)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            _onboardingPresenter.DrawOverlay(
                _onboardingCoordinator,
                _state,
                workbenchRuntime != null ? workbenchRuntime.ContributionRegistry : null,
                pathInteractionService,
                input,
                _consumeEventAction,
                delegate
                {
                    CompleteOnboarding(projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, workbenchRuntime, activateContainerAction);
                });
        }

        private void CompleteOnboarding(IProjectCatalog projectCatalog, IProjectWorkspaceService projectWorkspaceService, Action persistSessionAction, Action persistWindowSettingsAction, IWorkbenchRuntime workbenchRuntime, Action<string> activateContainerAction)
        {
            _onboardingLifecycle.Complete(_onboardingCoordinator, _state, _viewState, workbenchRuntime, workbenchRuntime?.ContributionRegistry, projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, result => {
                if (result?.ContainersToActivate != null) foreach (var c in result.ContainersToActivate) activateContainerAction(c);
            });
        }

    }
}

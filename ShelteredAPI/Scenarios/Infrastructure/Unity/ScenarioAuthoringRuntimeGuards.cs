using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal static class ScenarioAuthoringRuntimeGuards
    {
        private static ScenarioAuthoringState GetState()
        {
            return ScenarioAuthoringBackendService.Instance.CurrentState;
        }

        public static bool IsAuthoringActive()
        {
            return ScenarioAuthoringBootstrapService.Instance.IsEditingDraftActive();
        }

        public static bool IsAuthoringPending()
        {
            return ScenarioAuthoringBootstrapService.Instance.HasPendingDraftLaunch();
        }

        public static bool ShouldSuspendCameraUpdateForAuthoring()
        {
            if (!ScenarioWorldReady.IsShelterSceneActive())
                return false;

            return IsAuthoringPending() && !IsAuthoringActive();
        }

        public static bool IsPlaytesting()
        {
            if (!IsAuthoringActive())
                return false;

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            return session != null && session.PlaytestState == ScenarioPlaytestState.Playtesting;
        }

        public static bool ShouldCaptureGameplayInput()
        {
            if (!IsAuthoringActive())
                return false;

            ScenarioAuthoringState state = GetState();
            return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCameraGuardService>()
                .ShouldCaptureGameplayInput(state, IsPlaytesting());
        }

        public static bool ShouldResolveSelection()
        {
            return ShouldCaptureGameplayInput();
        }

        public static bool ShouldMaintainPausedSimulation()
        {
            return IsAuthoringActive() && !IsPlaytesting();
        }

        public static bool ShouldSuppressGlobalGameplayUi()
        {
            return ShouldCaptureGameplayInput();
        }

        public static bool ShouldBlockGameplayAxis(PlatformInput.InputAxis axis)
        {
            if (!IsAuthoringActive())
                return false;

            switch (axis)
            {
                case PlatformInput.InputAxis.CameraHorizontal:
                case PlatformInput.InputAxis.CameraVertical:
                    ScenarioAuthoringState state = GetState();
                    return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCameraGuardService>()
                        .ShouldBlockCameraInput(state, IsPlaytesting());
                default:
                    return false;
            }
        }

        public static bool ShouldBlockMenuAxis(PlatformInput.MenuInputAxis axis)
        {
            if (!IsAuthoringActive())
                return false;

            ScenarioAuthoringState state = GetState();
            return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCameraGuardService>()
                .ShouldConsumeScroll(state, IsPlaytesting())
                && axis == PlatformInput.MenuInputAxis.UIscroll;
        }

        public static bool ShouldSuppressCtrlCameraBehavior()
        {
            if (!IsAuthoringActive())
                return false;

            ScenarioAuthoringState state = GetState();
            return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCameraGuardService>()
                .ShouldSuppressCtrlCameraBehavior(state, IsPlaytesting());
        }

        public static bool ShouldBlockGameplayButton(PlatformInput.InputButton button)
        {
            if (!ShouldCaptureGameplayInput())
                return false;

            switch (button)
            {
                case PlatformInput.InputButton.Cancel:
                case PlatformInput.InputButton.CancelJob:
                case PlatformInput.InputButton.Action:
                case PlatformInput.InputButton.Interact:
                case PlatformInput.InputButton.Context:
                case PlatformInput.InputButton.GoHere:
                case PlatformInput.InputButton.NextChar:
                case PlatformInput.InputButton.PrevChar:
                case PlatformInput.InputButton.ToggleAutomation:
                case PlatformInput.InputButton.AcceptTransmission:
                case PlatformInput.InputButton.Dismiss:
                case PlatformInput.InputButton.Pause:
                case PlatformInput.InputButton.OpenMap:
                case PlatformInput.InputButton.Clipboard:
                case PlatformInput.InputButton.Info:
                case PlatformInput.InputButton.Zoom:
                case PlatformInput.InputButton.CameraSpeed:
                    return true;
                default:
                    return false;
            }
        }
    }
}

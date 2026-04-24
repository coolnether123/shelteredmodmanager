namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringRuntimeGuards
    {
        private static ScenarioAuthoringState GetState()
        {
            return ScenarioAuthoringBackendService.Instance.CurrentState;
        }

        public static bool IsAuthoringActive()
        {
            ScenarioAuthoringState state = GetState();
            return state != null && state.IsActive;
        }

        public static bool IsPlaytesting()
        {
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            return session != null && session.PlaytestState == ScenarioPlaytestState.Playtesting;
        }

        public static bool ShouldCaptureGameplayInput()
        {
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
            ScenarioAuthoringState state = GetState();
            return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCameraGuardService>()
                .ShouldConsumeScroll(state, IsPlaytesting())
                && axis == PlatformInput.MenuInputAxis.UIscroll;
        }

        public static bool ShouldSuppressCtrlCameraBehavior()
        {
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

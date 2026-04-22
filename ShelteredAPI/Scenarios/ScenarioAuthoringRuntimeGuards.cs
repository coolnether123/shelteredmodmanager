namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringRuntimeGuards
    {
        public static bool IsAuthoringActive()
        {
            ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
            return state != null && state.IsActive;
        }

        public static bool IsPlaytesting()
        {
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            return session != null && session.PlaytestState == ScenarioPlaytestState.Playtesting;
        }

        public static bool ShouldCaptureGameplayInput()
        {
            return IsAuthoringActive() && !IsPlaytesting();
        }

        public static bool ShouldResolveSelection()
        {
            return ShouldCaptureGameplayInput();
        }

        public static bool ShouldMaintainPausedSimulation()
        {
            return ShouldCaptureGameplayInput();
        }

        public static bool ShouldSuppressGlobalGameplayUi()
        {
            return ShouldCaptureGameplayInput();
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
                    return true;
                default:
                    return false;
            }
        }
    }
}

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringCameraGuardService
    {
        private readonly ScenarioAuthoringInputCaptureService _inputCaptureService;

        public ScenarioAuthoringCameraGuardService(ScenarioAuthoringInputCaptureService inputCaptureService)
        {
            _inputCaptureService = inputCaptureService;
        }

        public bool ShouldCaptureGameplayInput(ScenarioAuthoringState state, bool playtesting)
        {
            if (state == null || !state.IsActive)
                return false;

            if (!playtesting)
                return true;

            return _inputCaptureService.PointerOverAuthoringUi
                || _inputCaptureService.PopupOpen
                || _inputCaptureService.DraggingShellChrome
                || _inputCaptureService.KeyboardCaptured;
        }

        public bool ShouldBlockCameraInput(ScenarioAuthoringState state, bool playtesting)
        {
            if (state == null || !state.IsActive || state.Settings == null)
                return false;

            if (!state.Settings.GetBool("input.block_vanilla_camera", true))
                return false;

            return _inputCaptureService.ShouldBlockGameCameraInput();
        }

        public bool ShouldConsumeScroll(ScenarioAuthoringState state, bool playtesting)
        {
            return ShouldBlockCameraInput(state, playtesting);
        }

        public bool ShouldSuppressCtrlCameraBehavior(ScenarioAuthoringState state, bool playtesting)
        {
            return ShouldBlockCameraInput(state, playtesting);
        }
    }
}

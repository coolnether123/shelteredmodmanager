using ModAPI.Core;
using System.Diagnostics;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed class ScenarioAuthoringPresentationService
    {
        private const int PresentationBuildLogSampleFrames = 300;
        private readonly IScenarioAuthoringBackend _backend;
        private readonly ScenarioAuthoringShellImguiRenderModule _renderer;
        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private bool _missingModuleLogged;
        private int _presentationBuildFrame;

        internal ScenarioAuthoringPresentationService(
            IScenarioAuthoringBackend backend,
            ScenarioAuthoringShellImguiRenderModule renderer,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            _backend = backend;
            _renderer = renderer;
            _inputCapture = inputCapture;
        }

        public void Update()
        {
            ScenarioAuthoringState state = _backend.CurrentState;
            if (state == null || !state.IsActive)
            {
                HideActiveModule();
                return;
            }

            if (_renderer == null || !_renderer.CanRender())
            {
                if (!_missingModuleLogged)
                {
                    _missingModuleLogged = true;
                    MMLog.WriteWarning("[ScenarioAuthoringPresentation] No render module is currently available for scenario authoring UI.");
                }

                return;
            }

            if (_missingModuleLogged)
            {
                _missingModuleLogged = false;
                MMLog.WriteInfo("[ScenarioAuthoringPresentation] Render module availability restored.");
            }

            _presentationBuildFrame++;
            bool sampleBuildCost = _presentationBuildFrame >= PresentationBuildLogSampleFrames;
            if (sampleBuildCost)
                _presentationBuildFrame = 0;

            Stopwatch buildTimer = sampleBuildCost ? Stopwatch.StartNew() : null;
            ScenarioAuthoringShellViewModel shellViewModel = _backend.GetShellViewModel();
            long shellViewTicks = sampleBuildCost ? buildTimer.ElapsedTicks : 0L;
            ScenarioAuthoringInspectorDocument shellDocument = _backend.GetShellDocument();
            long shellDocumentTicks = sampleBuildCost ? buildTimer.ElapsedTicks : 0L;
            ScenarioAuthoringInspectorDocument inspectorDocument = _backend.GetInspectorDocument();
            long inspectorTicks = sampleBuildCost ? buildTimer.ElapsedTicks : 0L;
            ScenarioAuthoringInspectorDocument hoverDocument = _backend.GetHoverDocument();
            long totalTicks = sampleBuildCost ? buildTimer.ElapsedTicks : 0L;

            _renderer.Render(new ScenarioAuthoringPresentationSnapshot
            {
                State = state,
                ShellViewModel = shellViewModel,
                ShellDocument = shellDocument,
                InspectorDocument = inspectorDocument,
                HoverDocument = hoverDocument
            });

            if (sampleBuildCost)
            {
                double millisecondsPerTick = 1000d / Stopwatch.Frequency;
                MMLog.WriteInfo(string.Format(
                    CultureInfo.InvariantCulture,
                    "[ScenarioAuthoringPresentation] Sampled document build cost (1/{0} frames): shellView={1:0.###}ms, shellDocument={2:0.###}ms, inspector={3:0.###}ms, hover={4:0.###}ms, total={5:0.###}ms.",
                    PresentationBuildLogSampleFrames,
                    shellViewTicks * millisecondsPerTick,
                    (shellDocumentTicks - shellViewTicks) * millisecondsPerTick,
                    (inspectorTicks - shellDocumentTicks) * millisecondsPerTick,
                    (totalTicks - inspectorTicks) * millisecondsPerTick,
                    totalTicks * millisecondsPerTick));
            }
        }

        private void HideActiveModule()
        {
            if (_renderer != null)
                _renderer.Hide();

            if (_inputCapture != null)
                _inputCapture.Clear();
        }

    }
}

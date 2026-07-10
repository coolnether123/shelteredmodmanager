using ModAPI.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioAuthoringPresentationService
    {
        private const int PresentationBuildLogSampleFrames = 300;
        private readonly object _sync = new object();
        private readonly List<IScenarioAuthoringRenderModule> _modules = new List<IScenarioAuthoringRenderModule>();
        private readonly IScenarioAuthoringBackend _backend;
        private IScenarioAuthoringRenderModule _activeModule;
        private string _lastResolvedModuleId;
        private bool _missingModuleLogged;
        private int _presentationBuildFrame;

        public static ScenarioAuthoringPresentationService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringPresentationService>(); }
        }

        internal ScenarioAuthoringPresentationService(
            IScenarioAuthoringBackend backend,
            IEnumerable<IScenarioAuthoringRenderModule> modules)
        {
            _backend = backend;
            foreach (IScenarioAuthoringRenderModule module in modules ?? new IScenarioAuthoringRenderModule[0])
                Register(module);
        }

        public void Register(IScenarioAuthoringRenderModule module)
        {
            if (module == null)
                return;

            lock (_sync)
            {
                _modules.Add(module);
                _modules.Sort(CompareModules);
            }

            MMLog.WriteInfo("[ScenarioAuthoringPresentation] Registered render module '" + module.ModuleId
                + "' with priority " + module.Priority + ".");
        }

        public void Update()
        {
            ScenarioAuthoringState state = _backend.CurrentState;
            if (state == null || !state.IsActive)
            {
                HideActiveModule();
                return;
            }

            IScenarioAuthoringRenderModule module = ResolveModule();
            if (module == null)
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

            if (!object.ReferenceEquals(_activeModule, module))
            {
                HideActiveModule();
                _activeModule = module;
                if (!string.Equals(_lastResolvedModuleId, module.ModuleId))
                {
                    _lastResolvedModuleId = module.ModuleId;
                    MMLog.WriteInfo("[ScenarioAuthoringPresentation] Using render module '" + module.ModuleId + "'.");
                }
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

            module.Render(new ScenarioAuthoringPresentationSnapshot
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
                MMLog.WriteDebug(string.Format(
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

        private IScenarioAuthoringRenderModule ResolveModule()
        {
            lock (_sync)
            {
                for (int i = 0; i < _modules.Count; i++)
                {
                    IScenarioAuthoringRenderModule module = _modules[i];
                    if (module != null && module.CanRender())
                        return module;
                }
            }

            return null;
        }

        private void HideActiveModule()
        {
            if (_activeModule != null)
            {
                _activeModule.Hide();
                _activeModule = null;
            }

            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.Clear();
            }
            catch
            {
            }
        }

        private static int CompareModules(IScenarioAuthoringRenderModule left, IScenarioAuthoringRenderModule right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return right.Priority.CompareTo(left.Priority);
        }
    }
}

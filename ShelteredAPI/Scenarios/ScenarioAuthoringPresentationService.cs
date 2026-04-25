using ModAPI.Core;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringPresentationService
    {
        private readonly object _sync = new object();
        private readonly List<IScenarioAuthoringRenderModule> _modules = new List<IScenarioAuthoringRenderModule>();
        private readonly IScenarioAuthoringBackend _backend;
        private IScenarioAuthoringRenderModule _activeModule;
        private string _lastResolvedModuleId;
        private bool _missingModuleLogged;

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

            module.Render(new ScenarioAuthoringPresentationSnapshot
            {
                State = state,
                ShellViewModel = _backend.GetShellViewModel(),
                ShellDocument = _backend.GetShellDocument(),
                InspectorDocument = _backend.GetInspectorDocument(),
                HoverDocument = _backend.GetHoverDocument()
            });
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

using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
namespace ShelteredAPI.Scenarios.Application.Stages{
    internal sealed class ScenarioStageCoordinator
    {
        private readonly ScenarioStageRegistry _registry;
        private readonly List<IScenarioStageModule> _modules;
        private ScenarioStageKind _activeStageKind;

        public ScenarioStageCoordinator(
            ScenarioStageRegistry registry,
            IEnumerable<IScenarioStageModule> modules)
        {
            _registry = registry;
            _modules = new List<IScenarioStageModule>();
            foreach (IScenarioStageModule module in modules ?? new IScenarioStageModule[0])
            {
                if (module != null)
                    _modules.Add(module);
            }
        }

        public ScenarioStageDefinition ActiveStage
        {
            get { return _registry.Find(_activeStageKind); }
        }

        public ScenarioStageDefinition Synchronize(ScenarioAuthoringContext authoringContext)
        {
            ScenarioAuthoringState state = authoringContext != null ? authoringContext.State : null;
            ScenarioEditorSession editorSession = authoringContext != null ? authoringContext.EditorSession : null;
            ScenarioAuthoringSession authoringSession = authoringContext != null ? authoringContext.AuthoringSession : null;
            return Synchronize(state, editorSession, authoringSession);
        }

        public ScenarioStageDefinition Synchronize(ScenarioAuthoringState state, ScenarioEditorSession editorSession, ScenarioAuthoringSession authoringSession)
        {
            ScenarioStageKind nextKind = ResolveStageKind(state);
            ApplyCompatibilityState(state, nextKind);
            if (nextKind == _activeStageKind)
            {
                UpdateModules(BuildContext(nextKind, state, editorSession, authoringSession));
                return _registry.Find(nextKind);
            }

            ScenarioStageContext previous = BuildContext(_activeStageKind, state, editorSession, authoringSession);
            NotifyExit(previous);
            _activeStageKind = nextKind;

            ScenarioStageContext next = BuildContext(nextKind, state, editorSession, authoringSession);
            NotifyEnter(next);
            UpdateModules(next);
            return next.Stage;
        }

        public ScenarioStageDefinition Resolve(ScenarioAuthoringState state)
        {
            return _registry.Find(ResolveStageKind(state));
        }

        public ScenarioStageDefinition SelectStage(ScenarioAuthoringState state, ScenarioStageKind requestedKind)
        {
            if (state == null)
                return _registry.Find(ScenarioStageKind.None);

            ScenarioAuthoringWorkflowTransition transition = ScenarioAuthoringWorkflowRules.ApplyStageSelection(state, requestedKind);
            return _registry.Find(transition.ActiveStage);
        }

        public ScenarioAuthoringWorkflowTransition SelectTool(ScenarioAuthoringState state, ScenarioAuthoringTool requestedTool)
        {
            return ScenarioAuthoringWorkflowRules.ApplyToolSelection(state, requestedTool);
        }

        private ScenarioStageContext BuildContext(
            ScenarioStageKind stageKind,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession)
        {
            return new ScenarioStageContext
            {
                Stage = _registry.Find(stageKind),
                State = state,
                EditorSession = editorSession,
                AuthoringSession = authoringSession
            };
        }

        private void NotifyEnter(ScenarioStageContext context)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                IScenarioStageModule module = _modules[i];
                if (module != null && module.StageKind == (context.Stage != null ? context.Stage.Kind : ScenarioStageKind.None))
                    module.OnEnter(context);
            }
        }

        private void NotifyExit(ScenarioStageContext context)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                IScenarioStageModule module = _modules[i];
                if (module != null && module.StageKind == (context.Stage != null ? context.Stage.Kind : ScenarioStageKind.None))
                    module.OnExit(context);
            }
        }

        private void UpdateModules(ScenarioStageContext context)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                IScenarioStageModule module = _modules[i];
                if (module != null && module.StageKind == (context.Stage != null ? context.Stage.Kind : ScenarioStageKind.None))
                    module.Update(context);
            }
        }

        private static ScenarioStageKind ResolveStageKind(ScenarioAuthoringState state)
        {
            return ScenarioAuthoringWorkflowRules.ResolveStageKind(state);
        }

        private static void ApplyCompatibilityState(ScenarioAuthoringState state, ScenarioStageKind stageKind)
        {
            ScenarioAuthoringWorkflowRules.ApplyStageSynchronization(state, stageKind);
        }
    }
}

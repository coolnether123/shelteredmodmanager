using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
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

            ScenarioStageKind nextKind = NormalizeRequestedStage(requestedKind, state);
            state.ActiveStage = nextKind;
            if (!IsBunkerStage(nextKind))
                state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
            else if (nextKind != ScenarioStageKind.Bunker)
                state.ActiveBunkerStage = nextKind;

            ApplyCompatibilityState(state, nextKind);
            return _registry.Find(nextKind);
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
            if (state == null)
                return ScenarioStageKind.None;

            ScenarioStageKind explicitStage = NormalizeRequestedStage(state.ActiveStage, state);
            if (explicitStage != ScenarioStageKind.None)
                return explicitStage;

            switch (state.ActiveShellTab)
            {
                case ScenarioAuthoringShellTab.Shelter:
                case ScenarioAuthoringShellTab.Build:
                    return ResolveBunkerSubstage(state);
                case ScenarioAuthoringShellTab.Survivors:
                    return ScenarioStageKind.People;
                case ScenarioAuthoringShellTab.Stockpile:
                    return ScenarioStageKind.InventoryStorage;
                case ScenarioAuthoringShellTab.Triggers:
                    return ScenarioStageKind.Events;
                case ScenarioAuthoringShellTab.Quests:
                    return ScenarioStageKind.Quests;
                case ScenarioAuthoringShellTab.Map:
                    return ScenarioStageKind.Map;
                case ScenarioAuthoringShellTab.Test:
                    return ScenarioStageKind.Test;
                case ScenarioAuthoringShellTab.Publish:
                    return ScenarioStageKind.Publish;
                case ScenarioAuthoringShellTab.Art:
                    return ResolveArtStage(state);
                default:
                    return ScenarioStageKind.None;
            }
        }

        private static ScenarioStageKind ResolveBunkerSubstage(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioStageKind.Bunker;

            if (state.ActiveBunkerStage == ScenarioStageKind.BunkerBackground
                || state.ActiveBunkerStage == ScenarioStageKind.BunkerSurface
                || state.ActiveBunkerStage == ScenarioStageKind.BunkerInside)
            {
                return state.ActiveBunkerStage;
            }

            switch (state.ActiveTool)
            {
                case ScenarioAuthoringTool.Wiring:
                    return ScenarioStageKind.BunkerBackground;
                case ScenarioAuthoringTool.Assets:
                case ScenarioAuthoringTool.Objects:
                    return ScenarioStageKind.BunkerInside;
                default:
                    return ScenarioStageKind.BunkerSurface;
            }
        }

        private static ScenarioStageKind ResolveArtStage(ScenarioAuthoringState state)
        {
            if (state != null && state.SelectedTarget != null && state.SelectedTarget.Kind == ScenarioAuthoringTargetKind.Character)
                return ScenarioStageKind.People;

            return ScenarioStageKind.BunkerInside;
        }

        private static ScenarioStageKind NormalizeRequestedStage(ScenarioStageKind requestedKind, ScenarioAuthoringState state)
        {
            switch (requestedKind)
            {
                case ScenarioStageKind.BunkerBackground:
                case ScenarioStageKind.BunkerSurface:
                case ScenarioStageKind.BunkerInside:
                    return requestedKind;
                case ScenarioStageKind.Bunker:
                    if (state != null
                        && (state.ActiveBunkerStage == ScenarioStageKind.BunkerBackground
                            || state.ActiveBunkerStage == ScenarioStageKind.BunkerSurface
                            || state.ActiveBunkerStage == ScenarioStageKind.BunkerInside))
                    {
                        return state.ActiveBunkerStage;
                    }

                    return ScenarioStageKind.BunkerInside;
                case ScenarioStageKind.InventoryStorage:
                case ScenarioStageKind.People:
                case ScenarioStageKind.Events:
                case ScenarioStageKind.Quests:
                case ScenarioStageKind.Map:
                case ScenarioStageKind.Test:
                case ScenarioStageKind.Publish:
                    return requestedKind;
                default:
                    return ScenarioStageKind.None;
            }
        }

        private static bool IsBunkerStage(ScenarioStageKind stageKind)
        {
            return stageKind == ScenarioStageKind.Bunker
                || stageKind == ScenarioStageKind.BunkerBackground
                || stageKind == ScenarioStageKind.BunkerSurface
                || stageKind == ScenarioStageKind.BunkerInside;
        }

        private static void ApplyCompatibilityState(ScenarioAuthoringState state, ScenarioStageKind stageKind)
        {
            if (state == null)
                return;

            state.ActiveStage = stageKind;
            switch (stageKind)
            {
                case ScenarioStageKind.BunkerBackground:
                    state.ActiveBunkerStage = ScenarioStageKind.BunkerBackground;
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    state.ActiveTool = ScenarioAuthoringTool.Wiring;
                    break;
                case ScenarioStageKind.BunkerSurface:
                    state.ActiveBunkerStage = ScenarioStageKind.BunkerSurface;
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    if (state.ActiveTool == ScenarioAuthoringTool.Wiring || state.ActiveTool == ScenarioAuthoringTool.Assets)
                        state.ActiveTool = ScenarioAuthoringTool.Shelter;
                    break;
                case ScenarioStageKind.BunkerInside:
                    state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    if (state.ActiveTool != ScenarioAuthoringTool.Assets && state.ActiveTool != ScenarioAuthoringTool.Objects)
                        state.ActiveTool = ScenarioAuthoringTool.Objects;
                    break;
                case ScenarioStageKind.InventoryStorage:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Stockpile;
                    state.ActiveTool = ScenarioAuthoringTool.Inventory;
                    break;
                case ScenarioStageKind.People:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Survivors;
                    state.ActiveTool = ScenarioAuthoringTool.Family;
                    break;
                case ScenarioStageKind.Events:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Triggers;
                    state.ActiveTool = ScenarioAuthoringTool.Select;
                    break;
                case ScenarioStageKind.Quests:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Quests;
                    state.ActiveTool = ScenarioAuthoringTool.Select;
                    break;
                case ScenarioStageKind.Map:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Map;
                    state.ActiveTool = ScenarioAuthoringTool.Select;
                    break;
                case ScenarioStageKind.Test:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Test;
                    state.ActiveTool = ScenarioAuthoringTool.Select;
                    break;
                case ScenarioStageKind.Publish:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Publish;
                    state.ActiveTool = ScenarioAuthoringTool.Select;
                    break;
            }
        }
    }
}

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
namespace ShelteredAPI.Scenarios.Application.Stages{
    internal sealed class ScenarioAuthoringWorkflowTransition
    {
        public ScenarioStageKind PreviousStage { get; set; }
        public ScenarioStageKind ActiveStage { get; set; }
        public ScenarioAuthoringTool PreviousTool { get; set; }
        public ScenarioAuthoringTool ActiveTool { get; set; }

        public bool StageChanged
        {
            get { return PreviousStage != ActiveStage; }
        }

        public bool ToolChanged
        {
            get { return PreviousTool != ActiveTool; }
        }

        public bool Changed
        {
            get { return StageChanged || ToolChanged; }
        }
    }

    internal static class ScenarioAuthoringWorkflowRules
    {
        public static ScenarioStageKind ResolveStageKind(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioStageKind.None;

            ScenarioStageKind explicitStage = NormalizeStage(state.ActiveStage, state);
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

        public static ScenarioStageKind NormalizeStage(ScenarioStageKind requestedKind, ScenarioAuthoringState state)
        {
            switch (requestedKind)
            {
                case ScenarioStageKind.BunkerBackground:
                case ScenarioStageKind.BunkerSurface:
                case ScenarioStageKind.BunkerInside:
                    return requestedKind;
                case ScenarioStageKind.Bunker:
                    if (state != null && IsBunkerSubstage(state.ActiveBunkerStage))
                        return state.ActiveBunkerStage;

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

        public static ScenarioAuthoringWorkflowTransition ApplyStageSelection(
            ScenarioAuthoringState state,
            ScenarioStageKind requestedKind)
        {
            return ApplyTransition(state, NormalizeStage(requestedKind, state), false, ScenarioAuthoringTool.Select);
        }

        public static ScenarioAuthoringWorkflowTransition ApplyStageSynchronization(
            ScenarioAuthoringState state,
            ScenarioStageKind requestedKind)
        {
            return ApplyTransition(
                state,
                NormalizeStage(requestedKind, state),
                state != null,
                state != null ? state.ActiveTool : ScenarioAuthoringTool.Select);
        }

        public static ScenarioAuthoringWorkflowTransition ApplyToolSelection(
            ScenarioAuthoringState state,
            ScenarioAuthoringTool requestedTool)
        {
            ScenarioStageKind nextStage = ResolveStageForTool(state, requestedTool);
            return ApplyTransition(state, nextStage, true, requestedTool);
        }

        public static bool IsBunkerStage(ScenarioStageKind stageKind)
        {
            return stageKind == ScenarioStageKind.Bunker
                || IsBunkerSubstage(stageKind);
        }

        public static bool IsBunkerSubstage(ScenarioStageKind stageKind)
        {
            return stageKind == ScenarioStageKind.BunkerBackground
                || stageKind == ScenarioStageKind.BunkerSurface
                || stageKind == ScenarioStageKind.BunkerInside;
        }

        public static bool ShouldShowToolWorkspace(ScenarioAuthoringState state)
        {
            return state != null
                && IsBunkerSubstage(state.ActiveStage)
                && UsesToolWorkspace(state.ActiveTool);
        }

        public static bool UsesToolWorkspace(ScenarioAuthoringTool tool)
        {
            return tool == ScenarioAuthoringTool.Shelter
                || tool == ScenarioAuthoringTool.Objects
                || tool == ScenarioAuthoringTool.Wiring
                || tool == ScenarioAuthoringTool.Assets;
        }

        private static ScenarioAuthoringWorkflowTransition ApplyTransition(
            ScenarioAuthoringState state,
            ScenarioStageKind stageKind,
            bool hasPreferredTool,
            ScenarioAuthoringTool preferredTool)
        {
            ScenarioAuthoringWorkflowTransition transition = new ScenarioAuthoringWorkflowTransition
            {
                PreviousStage = state != null ? state.ActiveStage : ScenarioStageKind.None,
                PreviousTool = state != null ? state.ActiveTool : ScenarioAuthoringTool.Select,
                ActiveStage = state != null ? state.ActiveStage : ScenarioStageKind.None,
                ActiveTool = state != null ? state.ActiveTool : ScenarioAuthoringTool.Select
            };

            if (state == null)
                return transition;

            ApplyCompatibilityState(state, stageKind, hasPreferredTool, preferredTool);
            transition.ActiveStage = state.ActiveStage;
            transition.ActiveTool = state.ActiveTool;
            return transition;
        }

        private static ScenarioStageKind ResolveStageForTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool)
        {
            switch (tool)
            {
                case ScenarioAuthoringTool.Wiring:
                    return ScenarioStageKind.BunkerBackground;
                case ScenarioAuthoringTool.Objects:
                    return ScenarioStageKind.BunkerInside;
                case ScenarioAuthoringTool.Assets:
                    return state != null && IsBunkerSubstage(state.ActiveStage)
                        ? state.ActiveStage
                        : ScenarioStageKind.BunkerInside;
                case ScenarioAuthoringTool.Shelter:
                case ScenarioAuthoringTool.Select:
                    return ScenarioStageKind.BunkerSurface;
                case ScenarioAuthoringTool.Family:
                case ScenarioAuthoringTool.People:
                    return ScenarioStageKind.People;
                case ScenarioAuthoringTool.Inventory:
                    return ScenarioStageKind.InventoryStorage;
                case ScenarioAuthoringTool.WinLoss:
                    return ScenarioStageKind.Events;
                case ScenarioAuthoringTool.Vehicle:
                    return ScenarioStageKind.BunkerSurface;
                default:
                    return state != null ? NormalizeStage(state.ActiveStage, state) : ScenarioStageKind.None;
            }
        }

        private static ScenarioStageKind ResolveBunkerSubstage(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioStageKind.Bunker;

            if (IsBunkerSubstage(state.ActiveBunkerStage))
                return state.ActiveBunkerStage;

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

        private static void ApplyCompatibilityState(
            ScenarioAuthoringState state,
            ScenarioStageKind stageKind,
            bool hasPreferredTool,
            ScenarioAuthoringTool preferredTool)
        {
            if (state == null)
                return;

            state.ActiveStage = stageKind;
            if (!IsBunkerStage(stageKind))
                state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
            else if (stageKind != ScenarioStageKind.Bunker)
                state.ActiveBunkerStage = stageKind;

            switch (stageKind)
            {
                case ScenarioStageKind.BunkerBackground:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    state.ActiveTool = hasPreferredTool && preferredTool == ScenarioAuthoringTool.Assets
                        ? ScenarioAuthoringTool.Assets
                        : ScenarioAuthoringTool.Wiring;
                    break;
                case ScenarioStageKind.BunkerSurface:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    state.ActiveTool = ResolveBunkerSurfaceTool(hasPreferredTool, preferredTool);
                    break;
                case ScenarioStageKind.BunkerInside:
                    state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                    state.ActiveTool = hasPreferredTool && preferredTool == ScenarioAuthoringTool.Assets
                        ? ScenarioAuthoringTool.Assets
                        : ScenarioAuthoringTool.Objects;
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
                    state.ActiveTool = hasPreferredTool && preferredTool == ScenarioAuthoringTool.WinLoss
                        ? ScenarioAuthoringTool.WinLoss
                        : ScenarioAuthoringTool.Select;
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

        private static ScenarioAuthoringTool ResolveBunkerSurfaceTool(bool hasPreferredTool, ScenarioAuthoringTool preferredTool)
        {
            if (!hasPreferredTool)
                return ScenarioAuthoringTool.Shelter;

            if (preferredTool == ScenarioAuthoringTool.Select
                || preferredTool == ScenarioAuthoringTool.Assets
                || preferredTool == ScenarioAuthoringTool.Shelter)
            {
                return preferredTool;
            }

            return ScenarioAuthoringTool.Shelter;
        }
    }
}

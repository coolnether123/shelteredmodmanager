using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSelectionScopeService
    {
        private readonly ScenarioTargetClassifier _classifier;

        public ScenarioSelectionScopeService(ScenarioTargetClassifier classifier)
        {
            _classifier = classifier;
        }

        public ScenarioTargetScope ResolveActiveScope(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioTargetScope.Unknown;

            switch (state.ActiveStage)
            {
                case ScenarioStageKind.BunkerBackground:
                    return ScenarioTargetScope.BunkerBackground;
                case ScenarioStageKind.BunkerSurface:
                    return ScenarioTargetScope.BunkerSurface;
                case ScenarioStageKind.BunkerInside:
                case ScenarioStageKind.Bunker:
                    return ScenarioTargetScope.BunkerInside;
                case ScenarioStageKind.InventoryStorage:
                    return ScenarioTargetScope.Inventory;
                case ScenarioStageKind.People:
                    return ScenarioTargetScope.People;
                case ScenarioStageKind.Events:
                    return ScenarioTargetScope.Events;
                case ScenarioStageKind.Quests:
                    return ScenarioTargetScope.Quests;
                case ScenarioStageKind.Map:
                    return ScenarioTargetScope.Map;
                default:
                    return ScenarioTargetScope.Unknown;
            }
        }

        public bool CanSelectTargetForCurrentStage(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            string reason;
            return CanSelectTargetForCurrentStage(state, target, out reason);
        }

        public bool CanSelectTargetForCurrentStage(ScenarioAuthoringState state, ScenarioAuthoringTarget target, out string reason)
        {
            reason = null;
            if (state == null || target == null)
                return false;

            ScenarioTargetScope activeScope = ResolveActiveScope(state);
            ScenarioTargetClassification classification = _classifier.Classify(target);
            if (classification != null && classification.Matches(activeScope))
                return true;

            reason = "Filtered by scope: " + (_classifier.FormatScopeLabel(classification)) + " target while selecting " + ScenarioTargetClassifier.FormatScopeLabel(activeScope) + ".";
            return false;
        }

        public bool ClearSelectionIfOutOfScope(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            bool changed = false;
            string reason;
            if (state.HoveredTarget != null && !CanSelectTargetForCurrentStage(state, state.HoveredTarget, out reason))
            {
                state.HoveredTarget = null;
                changed = true;
            }

            if (state.SelectedTarget != null && !CanSelectTargetForCurrentStage(state, state.SelectedTarget, out reason))
            {
                state.SelectedTarget = null;
                if (state.MultiSelection != null)
                    state.MultiSelection.Clear();
                state.StatusMessage = reason;
                changed = true;
            }

            if (state.MultiSelection != null && state.MultiSelection.Count > 0)
            {
                for (int i = state.MultiSelection.Count - 1; i >= 0; i--)
                {
                    if (!CanSelectTargetForCurrentStage(state, state.MultiSelection[i], out reason))
                    {
                        state.MultiSelection.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        public List<ScenarioSpriteCatalogService.SpriteCandidate> FilterCandidatesForScope(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            ScenarioAuthoringState state)
        {
            return FilterCandidatesForScope(candidates, ResolveActiveScope(state));
        }

        public List<ScenarioSpriteCatalogService.SpriteCandidate> FilterCandidatesForScope(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            ScenarioTargetScope scope)
        {
            List<ScenarioSpriteCatalogService.SpriteCandidate> filtered = new List<ScenarioSpriteCatalogService.SpriteCandidate>();
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (CandidateMatchesScope(candidate, scope))
                    filtered.Add(candidate);
            }

            return filtered;
        }

        private static bool CandidateMatchesScope(ScenarioSpriteCatalogService.SpriteCandidate candidate, ScenarioTargetScope scope)
        {
            if (candidate == null)
                return false;

            string text = ((candidate.Label ?? string.Empty) + " "
                + (candidate.SourceName ?? string.Empty) + " "
                + (candidate.Hint ?? string.Empty)).ToLowerInvariant();

            return ScenarioTargetScopeTextMatcher.CandidateMatchesScope(text, scope);
        }
    }
}

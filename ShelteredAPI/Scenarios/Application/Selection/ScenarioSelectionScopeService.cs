using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Application.Selection{
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

        public ScenarioTargetScope ResolveSelectionScope(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioTargetScope.Unknown;

            if (state.ActiveTool == ScenarioAuthoringTool.Wiring)
                return ScenarioTargetScope.BunkerInside;

            return ResolveActiveScope(state);
        }

        public bool CanSelectTargetForCurrentStage(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            string reason;
            return CanSelectTargetForCurrentStage(state, target, out reason);
        }

        public bool CanSelectTargetForCurrentStage(ScenarioAuthoringState state, ScenarioAuthoringTarget target, out string reason)
        {
            reason = null;
            if (state == null)
            {
                reason = "Scenario authoring state is unavailable.";
                return false;
            }

            if (target == null)
            {
                reason = "Select a target before using this action.";
                return false;
            }

            ScenarioTargetScope activeScope = ResolveSelectionScope(state);
            ScenarioTargetClassification classification = _classifier.Classify(target);
            if (TargetMatchesCurrentStageScope(activeScope, classification, out reason))
            {
                if (IsLayerLocked(state, activeScope))
                {
                    reason = ScenarioTargetClassifier.FormatScopeLabel(activeScope) + " layer is locked in Editor Settings.";
                    return false;
                }

                return true;
            }

            reason = BuildScopeFilterReason(activeScope, classification);
            return false;
        }

        private bool IsTargetInCurrentStageScope(ScenarioAuthoringState state, ScenarioAuthoringTarget target, out string reason)
        {
            reason = null;
            if (state == null || target == null)
                return false;

            ScenarioTargetScope activeScope = ResolveSelectionScope(state);
            ScenarioTargetClassification classification = _classifier.Classify(target);
            if (TargetMatchesCurrentStageScope(activeScope, classification, out reason))
                return true;

            reason = BuildScopeFilterReason(activeScope, classification);
            return false;
        }

        private string BuildScopeFilterReason(ScenarioTargetScope activeScope, ScenarioTargetClassification classification)
        {
            if (!IsWorldScope(activeScope))
                return "This target is not available in the current workspace.";

            return "Filtered by scope: " + (_classifier.FormatScopeLabel(classification)) + " target while selecting " + ScenarioTargetClassifier.FormatScopeLabel(activeScope) + ".";
        }

        private static bool TargetMatchesCurrentStageScope(ScenarioTargetScope activeScope, ScenarioTargetClassification classification, out string reason)
        {
            reason = null;
            return classification != null && classification.Matches(activeScope);
        }

        private static bool IsLayerLocked(ScenarioAuthoringState state, ScenarioTargetScope scope)
        {
            if (state == null || state.Settings == null)
                return false;

            switch (scope)
            {
                case ScenarioTargetScope.BunkerBackground:
                    return state.Settings.GetBool("layers.lock_background", false);
                case ScenarioTargetScope.BunkerSurface:
                    return state.Settings.GetBool("layers.lock_surface", false);
                case ScenarioTargetScope.BunkerInside:
                    return state.Settings.GetBool("layers.lock_inside", false);
                default:
                    return false;
            }
        }

        public bool ClearSelectionIfOutOfScope(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            bool changed = false;
            string reason;
            if (state.HoveredTarget != null && !IsTargetInCurrentStageScope(state, state.HoveredTarget, out reason))
            {
                state.HoveredTarget = null;
                changed = true;
            }

            if (state.SelectedTarget != null && !IsTargetInCurrentStageScope(state, state.SelectedTarget, out reason))
            {
                state.SelectedTarget = null;
                if (state.MultiSelection != null)
                    state.MultiSelection.Clear();
                if (ShouldReportScopeFilter(state))
                    state.StatusMessage = reason;
                changed = true;
            }

            if (state.MultiSelection != null && state.MultiSelection.Count > 0)
            {
                for (int i = state.MultiSelection.Count - 1; i >= 0; i--)
                {
                    if (!IsTargetInCurrentStageScope(state, state.MultiSelection[i], out reason))
                    {
                        state.MultiSelection.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private bool ShouldReportScopeFilter(ScenarioAuthoringState state)
        {
            return IsWorldScope(ResolveSelectionScope(state));
        }

        private static bool IsWorldScope(ScenarioTargetScope scope)
        {
            return scope == ScenarioTargetScope.BunkerBackground
                || scope == ScenarioTargetScope.BunkerSurface
                || scope == ScenarioTargetScope.BunkerInside;
        }

        public List<ScenarioSpriteCatalogService.SpriteCandidate> FilterCandidatesForScope(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            ScenarioAuthoringState state)
        {
            return FilterCandidatesForScope(candidates, ResolveSelectionScope(state));
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

            if (scope == ScenarioTargetScope.BunkerInside && IsWallWiringCandidate(candidate))
                return true;

            string text = ((candidate.Label ?? string.Empty) + " "
                + (candidate.SourceName ?? string.Empty) + " "
                + (candidate.Hint ?? string.Empty)).ToLowerInvariant();

            return ScenarioTargetScopeTextMatcher.CandidateMatchesScope(text, scope);
        }

        private static bool IsWallWiringCandidate(ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            string labelAndHint = ((candidate.Label ?? string.Empty) + " " + (candidate.Hint ?? string.Empty)).ToLowerInvariant();
            return ScenarioTargetScopeTextMatcher.ContainsWallWiringToken(labelAndHint);
        }
    }
}

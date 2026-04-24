using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class BunkerDependencyValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            HashSet<string> foundations = CollectFoundations(definition);
            HashSet<string> expansions = CollectExpansions(definition);
            HashSet<string> gates = CollectGates(definition);

            ValidateExpansionGates(definition, summary, gates);
            ValidatePlacementSupport(definition, summary, foundations, expansions, gates);
            ValidateSceneSpriteSupport(definition, summary, foundations, expansions, gates);
        }

        private static void ValidateExpansionGates(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> gates)
        {
            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                string id = TrimToNull(expansion != null ? expansion.Id : null);
                if (id == null)
                    summary.AddError("bunker.expansion.id_required", "[Bunker] Expansion #" + i + " is missing id.");

                string gate = TrimToNull(expansion != null ? expansion.UnlockGateId : null);
                if (gate != null && !gates.Contains(gate))
                    summary.AddError("bunker.expansion.unknown_gate", "[Bunker] Expansion '" + (id ?? ("#" + i)) + "' references unknown unlock gate '" + gate + "'.");
            }

            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null && i < definition.BunkerGrid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = definition.BunkerGrid.Foundations[i];
                string id = TrimToNull(foundation != null ? foundation.Id : null);
                if (id == null)
                    summary.AddError("bunker.foundation.id_required", "[Bunker] Foundation #" + i + " is missing id.");
                if (foundation != null && foundation.Width <= 0)
                    summary.AddError("bunker.foundation.width", "[Bunker] Foundation '" + (id ?? ("#" + i)) + "' width must be greater than zero.");
                if (foundation != null && foundation.Height <= 0)
                    summary.AddError("bunker.foundation.height", "[Bunker] Foundation '" + (id ?? ("#" + i)) + "' height must be greater than zero.");
            }
        }

        private static void ValidatePlacementSupport(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> foundations, HashSet<string> expansions, HashSet<string> gates)
        {
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                string label = "[Bunker] Object placement '" + (ObjectStartStateValidationRule.ResolveObjectId(placement, i) ?? ("#" + i)) + "'";
                ValidateSupport(summary, label, placement != null ? placement.RequiredFoundationId : null, placement != null ? placement.RequiredBunkerExpansionId : null, placement != null ? placement.UnlockGateId : null, placement != null ? placement.StartState : ScenarioObjectStartState.StartsEnabled, foundations, expansions, gates);
            }
        }

        private static void ValidateSceneSpriteSupport(ScenarioDefinition definition, ValidationSummary summary, HashSet<string> foundations, HashSet<string> expansions, HashSet<string> gates)
        {
            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                string id = TrimToNull(placement != null ? placement.ScenarioObjectId : null) ?? TrimToNull(placement != null ? placement.Id : null);
                string label = "[Map] Scene sprite placement '" + (id ?? ("#" + i)) + "'";
                ValidateSupport(summary, label, placement != null ? placement.RequiredFoundationId : null, placement != null ? placement.RequiredBunkerExpansionId : null, placement != null ? placement.UnlockGateId : null, placement != null ? placement.StartState : ScenarioObjectStartState.StartsEnabled, foundations, expansions, gates);
            }
        }

        private static void ValidateSupport(ValidationSummary summary, string label, string foundationId, string expansionId, string gateId, ScenarioObjectStartState startState, HashSet<string> foundations, HashSet<string> expansions, HashSet<string> gates)
        {
            string foundation = TrimToNull(foundationId);
            if (foundation != null && !foundations.Contains(foundation))
                summary.AddError("bunker.object.unknown_foundation", label + " references unknown foundation '" + foundation + "'.");

            string expansion = TrimToNull(expansionId);
            if (expansion != null && !expansions.Contains(expansion))
                summary.AddError("bunker.object.unknown_expansion", label + " references unknown expansion '" + expansion + "'.");

            string gate = TrimToNull(gateId);
            if (gate != null && !gates.Contains(gate))
                summary.AddError("bunker.object.unknown_gate", label + " references unknown unlock gate '" + gate + "'.");

            if (startState == ScenarioObjectStartState.StartsEnabled && expansion != null)
                summary.AddWarning("bunker.object.start_expansion", label + " starts enabled inside an expansion; make sure the expansion is active at start.");
            if ((startState == ScenarioObjectStartState.AppearsLater || startState == ScenarioObjectStartState.StartsLocked) && foundation == null && expansion == null && gate == null)
                summary.AddWarning("bunker.object.future_support", label + " is delayed/locked but has no foundation, expansion, or gate dependency.");
        }

        private static HashSet<string> CollectFoundations(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null && i < definition.BunkerGrid.Foundations.Count; i++)
            {
                string id = TrimToNull(definition.BunkerGrid.Foundations[i] != null ? definition.BunkerGrid.Foundations[i].Id : null);
                if (id != null)
                    ids.Add(id);
            }
            return ids;
        }

        private static HashSet<string> CollectExpansions(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                string id = TrimToNull(definition.BunkerGrid.Expansions[i] != null ? definition.BunkerGrid.Expansions[i].Id : null);
                if (id != null)
                    ids.Add(id);
            }
            return ids;
        }

        private static HashSet<string> CollectGates(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.Gates != null && i < definition.Gates.Count; i++)
            {
                string id = TrimToNull(definition.Gates[i] != null ? definition.Gates[i].Id : null);
                if (id != null)
                    ids.Add(id);
            }
            return ids;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}

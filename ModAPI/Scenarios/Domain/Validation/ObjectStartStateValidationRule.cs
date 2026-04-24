using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class ObjectStartStateValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            HashSet<string> objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                string id = ResolveObjectId(placement, i);
                ValidateId(summary, objectIds, id, "[Bunker] Object placement #" + i);
                ValidateStartState(summary, placement != null ? placement.StartState : ScenarioObjectStartState.StartsEnabled, "[Bunker] Object placement '" + (id ?? ("#" + i)) + "'");
            }

            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                string id = TrimToNull(placement != null ? placement.ScenarioObjectId : null) ?? TrimToNull(placement != null ? placement.Id : null);
                ValidateId(summary, objectIds, id, "[Map] Scene sprite placement #" + i);
                ValidateStartState(summary, placement != null ? placement.StartState : ScenarioObjectStartState.StartsEnabled, "[Map] Scene sprite placement '" + (id ?? ("#" + i)) + "'");
            }
        }

        public static string ResolveObjectId(ObjectPlacement placement, int index)
        {
            string id = TrimToNull(placement != null ? placement.ScenarioObjectId : null);
            if (id != null)
                return id;
            return TrimToNull(placement != null ? placement.DefinitionReference : null) ?? TrimToNull(placement != null ? placement.PrefabReference : null);
        }

        private static void ValidateId(ValidationSummary summary, HashSet<string> ids, string id, string label)
        {
            if (TrimToNull(id) == null)
            {
                summary.AddError("object.id.required", label + " is missing scenarioObjectId.");
                return;
            }

            if (ids.Contains(id))
                summary.AddError("object.id.duplicate", label + " has duplicate scenarioObjectId '" + id + "'.");
            else
                ids.Add(id);
        }

        private static void ValidateStartState(ValidationSummary summary, ScenarioObjectStartState state, string label)
        {
            if (!Enum.IsDefined(typeof(ScenarioObjectStartState), state))
                summary.AddError("object.start_state.invalid", label + " has invalid start state.");
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

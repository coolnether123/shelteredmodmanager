using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioObjectStartStateApplyService
    {
        private readonly ScenarioRuntimeStateService _stateService;

        public ScenarioObjectStartStateApplyService(ScenarioRuntimeStateService stateService)
        {
            _stateService = stateService;
        }

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : "object_" + i.ToString();
                bool supported = IsSupportActive(definition, placement.RequiredFoundationId, placement.RequiredBunkerExpansionId);
                if (placement.StartState == ScenarioObjectStartState.StartsEnabled && !supported && result != null)
                    result.AddMessage("Object '" + id + "' starts enabled but its support is not active at start.");
                Record(state, id, placement.RuntimeBindingKey, placement.StartState);
            }
        }

        internal static void Record(ScenarioRuntimeState state, string id, string bindingKey, ScenarioObjectStartState startState)
        {
            if (state == null || string.IsNullOrEmpty(id))
                return;
            ScenarioObjectRuntimeStateRecord record = Find(state, id);
            if (record == null)
            {
                record = new ScenarioObjectRuntimeStateRecord();
                state.ObjectStates.Add(record);
            }
            record.ScenarioObjectId = id;
            record.RuntimeBindingKey = bindingKey;
            record.State = startState;
            record.Active = startState == ScenarioObjectStartState.StartsEnabled || startState == ScenarioObjectStartState.StartsLocked;
            record.Locked = startState == ScenarioObjectStartState.StartsLocked;
            record.Hidden = startState == ScenarioObjectStartState.StartsHidden || startState == ScenarioObjectStartState.AppearsLater || startState == ScenarioObjectStartState.RemovedAtStart;
        }

        private static ScenarioObjectRuntimeStateRecord Find(ScenarioRuntimeState state, string id)
        {
            for (int i = 0; state.ObjectStates != null && i < state.ObjectStates.Count; i++)
            {
                ScenarioObjectRuntimeStateRecord record = state.ObjectStates[i];
                if (record != null && string.Equals(record.ScenarioObjectId, id, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
            return null;
        }

        private static bool IsSupportActive(ScenarioDefinition definition, string foundationId, string expansionId)
        {
            if (string.IsNullOrEmpty(foundationId) && string.IsNullOrEmpty(expansionId))
                return true;
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null && i < definition.BunkerGrid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = definition.BunkerGrid.Foundations[i];
                if (foundation != null && foundation.ActiveAtStart && string.Equals(foundation.Id, foundationId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion != null && expansion.ActiveAtStart && string.Equals(expansion.Id, expansionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScheduledObjectRuntimeService : IScenarioEffectHandler
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.ActivateObject || kind == ScenarioEffectKind.DeactivateObject;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null || state == null)
            {
                message = "Object runtime state is not ready.";
                return false;
            }

            string objectId = effect.ObjectId ?? effect.TargetId;
            if (string.IsNullOrEmpty(objectId))
            {
                message = "Object id is missing.";
                return false;
            }

            ScenarioObjectRuntimeStateRecord record = FindOrCreate(state, objectId);
            if (effect.Kind == ScenarioEffectKind.ActivateObject)
            {
                record.Active = true;
                record.Hidden = false;
                record.Locked = false;
                record.State = ScenarioObjectStartState.StartsEnabled;
                if (ScenarioObjectPlacementRuntimeBinding.Find(objectId) == null)
                    BunkerApplyService.TryMaterializePlacement(definition, objectId, new ScenarioApplyResult());
                ScenarioObjectPlacementRuntimeBinding.ApplyActiveState(objectId, true);
            }
            else
            {
                record.Active = false;
                record.State = ScenarioObjectStartState.StartsDisabled;
                ScenarioObjectPlacementRuntimeBinding.ApplyActiveState(objectId, false);
            }

            message = "Object runtime state recorded for " + objectId + ".";
            return true;
        }

        private static ScenarioObjectRuntimeStateRecord FindOrCreate(ScenarioRuntimeState state, string objectId)
        {
            for (int i = 0; state.ObjectStates != null && i < state.ObjectStates.Count; i++)
            {
                ScenarioObjectRuntimeStateRecord record = state.ObjectStates[i];
                if (record != null && string.Equals(record.ScenarioObjectId, objectId, StringComparison.OrdinalIgnoreCase))
                    return record;
            }

            ScenarioObjectRuntimeStateRecord created = new ScenarioObjectRuntimeStateRecord();
            created.ScenarioObjectId = objectId;
            state.ObjectStates.Add(created);
            return created;
        }
    }
}

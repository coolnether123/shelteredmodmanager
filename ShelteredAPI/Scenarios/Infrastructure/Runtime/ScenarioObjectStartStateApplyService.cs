using System;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
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

        internal static bool ShouldMaterializeAtStart(ObjectPlacement placement)
        {
            if (placement == null)
                return false;

            return placement.StartState != ScenarioObjectStartState.RemovedAtStart;
        }

        internal static bool ShouldMaterializeStructureAtStart(ObjectPlacement placement)
        {
            if (placement == null)
                return false;

            return placement.StartState == ScenarioObjectStartState.StartsEnabled
                || placement.StartState == ScenarioObjectStartState.StartsDisabled
                || placement.StartState == ScenarioObjectStartState.StartsLocked;
        }

        internal static void ApplyToObject(Obj_Base obj, ObjectPlacement placement, ScenarioApplyResult result)
        {
            if (obj == null || placement == null)
                return;

            string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.DefinitionReference;
            switch (placement.StartState)
            {
                case ScenarioObjectStartState.StartsEnabled:
                    obj.EnableObject();
                    obj.selectable = true;
                    SetGameObjectActive(obj.gameObject, true);
                    break;

                case ScenarioObjectStartState.StartsDisabled:
                    obj.DisableObject();
                    SetGameObjectActive(obj.gameObject, true);
                    break;

                case ScenarioObjectStartState.StartsHidden:
                case ScenarioObjectStartState.AppearsLater:
                    SetGameObjectActive(obj.gameObject, false);
                    break;

                case ScenarioObjectStartState.StartsLocked:
                    obj.EnableObject();
                    obj.selectable = false;
                    obj.lockDeconstructOption = true;
                    SetGameObjectActive(obj.gameObject, true);
                    break;

                case ScenarioObjectStartState.RemovedAtStart:
                    RemoveObject(obj);
                    break;

                default:
                    if (result != null)
                        result.AddMessage("Object '" + (id ?? string.Empty) + "' has an unknown start state; leaving it enabled.");
                    break;
            }
        }

        internal static void ApplyToStructure(GameObject target, ObjectPlacement placement)
        {
            if (target == null || placement == null)
                return;

            if (placement.StartState == ScenarioObjectStartState.StartsHidden
                || placement.StartState == ScenarioObjectStartState.AppearsLater
                || placement.StartState == ScenarioObjectStartState.RemovedAtStart)
            {
                target.SetActive(false);
            }
        }

        private static void SetGameObjectActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void RemoveObject(Obj_Base obj)
        {
            if (obj == null)
                return;

            if (ObjectManager.Instance != null)
                ObjectManager.Instance.RemoveObject(obj);
            else if (obj.gameObject != null)
                UnityEngine.Object.Destroy(obj.gameObject);
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

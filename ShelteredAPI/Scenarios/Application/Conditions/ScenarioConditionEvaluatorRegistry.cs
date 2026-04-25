using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioConditionEvaluatorRegistry
    {
        private readonly List<IScenarioConditionEvaluator> _evaluators = new List<IScenarioConditionEvaluator>();

        public void Register(IScenarioConditionEvaluator evaluator)
        {
            if (evaluator != null && !_evaluators.Contains(evaluator))
                _evaluators.Add(evaluator);
        }

        public bool IsGateSatisfied(ScenarioDefinition definition, string gateId, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(gateId))
                return true;

            ScenarioGateDefinition gate = FindGate(definition, gateId);
            if (gate == null)
            {
                reason = "Unknown gate: " + gateId;
                return false;
            }

            return IsGroupSatisfied(definition, gate.Conditions, state, out reason);
        }

        public bool AreConditionsSatisfied(ScenarioDefinition definition, IList<ScenarioConditionRef> conditions, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                if (!IsConditionSatisfied(definition, conditions[i], state, out reason))
                    return false;
            }
            return true;
        }

        private bool IsGroupSatisfied(ScenarioDefinition definition, ScenarioConditionGroup group, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (group == null)
                return true;

            bool anySatisfied = false;
            bool sawAny = false;
            for (int i = 0; group.Conditions != null && i < group.Conditions.Count; i++)
            {
                sawAny = true;
                bool ok = IsConditionSatisfied(definition, group.Conditions[i], state, out reason);
                if (group.Mode == ScenarioConditionGroupMode.All && !ok)
                    return false;
                if (group.Mode == ScenarioConditionGroupMode.Any && ok)
                    anySatisfied = true;
            }

            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
            {
                sawAny = true;
                bool ok = IsGroupSatisfied(definition, group.Groups[i], state, out reason);
                if (group.Mode == ScenarioConditionGroupMode.All && !ok)
                    return false;
                if (group.Mode == ScenarioConditionGroupMode.Any && ok)
                    anySatisfied = true;
            }

            return group.Mode == ScenarioConditionGroupMode.Any ? !sawAny || anySatisfied : true;
        }

        private bool IsConditionSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (condition == null)
                return true;

            for (int i = 0; i < _evaluators.Count; i++)
            {
                IScenarioConditionEvaluator evaluator = _evaluators[i];
                if (evaluator != null && evaluator.CanEvaluate(condition.Kind))
                    return evaluator.IsSatisfied(definition, condition, state, out reason);
            }

            reason = "No evaluator registered for condition kind " + condition.Kind + ".";
            return false;
        }

        private static ScenarioGateDefinition FindGate(ScenarioDefinition definition, string gateId)
        {
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                if (gate != null && string.Equals(gate.Id, gateId, StringComparison.OrdinalIgnoreCase))
                    return gate;
            }
            return null;
        }
    }
}

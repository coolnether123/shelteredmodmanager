using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class ScenarioValidationPipeline
    {
        private readonly List<IScenarioValidationRule> _rules = new List<IScenarioValidationRule>();

        public ScenarioValidationPipeline()
        {
        }

        public ScenarioValidationPipeline(IEnumerable<IScenarioValidationRule> rules)
        {
            if (rules == null)
                return;

            foreach (IScenarioValidationRule rule in rules)
                AddRule(rule);
        }

        public void AddRule(IScenarioValidationRule rule)
        {
            if (rule != null)
                _rules.Add(rule);
        }

        public ValidationSummary Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            ValidationSummary summary = new ValidationSummary();
            for (int i = 0; i < _rules.Count; i++)
            {
                IScenarioValidationRule rule = _rules[i];
                if (rule == null)
                    continue;

                rule.Validate(definition, scenarioFilePath, summary);
            }

            return summary;
        }

        public ScenarioValidationResult ValidateLegacy(ScenarioDefinition definition, string scenarioFilePath)
        {
            return Validate(definition, scenarioFilePath).ToLegacyResult();
        }
    }
}

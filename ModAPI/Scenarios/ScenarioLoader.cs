using System;
using System.Collections.Generic;
using System.IO;

namespace ModAPI.Scenarios
{
    public sealed class ScenarioLoader
    {
        private readonly ScenarioCatalog _catalog;
        private readonly ScenarioDefinitionSerializer _serializer;
        private readonly ScenarioValidator _validator;

        public ScenarioLoader()
            : this(new ScenarioCatalog(), new ScenarioDefinitionSerializer(), new ScenarioValidator())
        {
        }

        public ScenarioLoader(ScenarioCatalog catalog, ScenarioDefinitionSerializer serializer, ScenarioValidator validator)
        {
            _catalog = catalog ?? new ScenarioCatalog();
            _serializer = serializer ?? new ScenarioDefinitionSerializer();
            _validator = validator ?? new ScenarioValidator();
        }

        public ScenarioInfo[] ListAll()
        {
            return _catalog.ListAll();
        }

        public ScenarioDefinition Load(string scenarioId)
        {
            ScenarioInfo info;
            if (!_catalog.TryGet(scenarioId, out info) || info == null)
                throw new InvalidOperationException("Scenario is not indexed: " + scenarioId);

            ScenarioDefinition definition = _serializer.Load(info.FilePath);
            ScenarioValidationResult validation = _validator.Validate(definition, info.FilePath);
            if (!validation.IsValid)
                throw new InvalidDataException("Scenario '" + scenarioId + "' failed validation: " + JoinIssues(validation.Issues));

            return definition;
        }

        public bool TryLoad(string scenarioId, out ScenarioDefinition definition, out ScenarioValidationResult validation)
        {
            definition = null;
            validation = new ScenarioValidationResult();

            ScenarioInfo info;
            if (!_catalog.TryGet(scenarioId, out info) || info == null)
            {
                validation.AddError("Scenario is not indexed: " + scenarioId);
                return false;
            }

            try
            {
                definition = _serializer.Load(info.FilePath);
                validation = _validator.Validate(definition, info.FilePath);
                return validation.IsValid;
            }
            catch (Exception ex)
            {
                validation.AddError("Scenario load failed: " + ex.Message);
                return false;
            }
        }

        public ScenarioValidationResult Validate(string scenarioId)
        {
            ScenarioInfo info;
            if (!_catalog.TryGet(scenarioId, out info) || info == null)
            {
                ScenarioValidationResult missing = new ScenarioValidationResult();
                missing.AddError("Scenario is not indexed: " + scenarioId);
                return missing;
            }

            try
            {
                ScenarioDefinition definition = _serializer.Load(info.FilePath);
                return _validator.Validate(definition, info.FilePath);
            }
            catch (Exception ex)
            {
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError("Scenario validation failed during XML load: " + ex.Message);
                return failed;
            }
        }

        private static string JoinIssues(ScenarioValidationIssue[] issues)
        {
            if (issues == null || issues.Length == 0)
                return string.Empty;

            List<string> parts = new List<string>();
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    parts.Add(issues[i].Severity + ": " + issues[i].Message);
            }

            return string.Join("; ", parts.ToArray());
        }
    }

    public static class ScenarioDefinitionCloner
    {
        public static ScenarioDefinition Clone(ScenarioDefinition definition)
        {
            if (definition == null)
                return null;

            // A serializer round-trip is used on purpose instead of hand-copying every
            // nested list. That keeps future schema fields DRY: adding a field requires
            // updating the serializer once, and editor undo/revert immediately follows.
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            return serializer.FromXml(serializer.ToXml(definition));
        }
    }
}

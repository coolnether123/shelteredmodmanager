using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Runtime;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDefinitionReader : IScenarioDefinitionReader
    {
        private readonly IScenarioDefinitionSerializer _definitionSerializer;
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly IScenarioDefinitionValidator _definitionValidator;

        public ScenarioDefinitionReader(
            IScenarioDefinitionSerializer definitionSerializer,
            IScenarioDefinitionCatalog definitionCatalog,
            IScenarioDefinitionValidator definitionValidator)
        {
            _definitionSerializer = definitionSerializer;
            _definitionCatalog = definitionCatalog;
            _definitionValidator = definitionValidator;
        }

        public ScenarioInfo[] ListAll()
        {
            List<ScenarioInfo> combined = new List<ScenarioInfo>();
            Dictionary<string, ScenarioInfo> byId = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            AddDefinitionInfos(byId, _definitionCatalog.ListAll());

            foreach (KeyValuePair<string, ScenarioInfo> pair in byId)
                combined.Add(pair.Value);

            combined.Sort(CompareScenarioDefinitionInfo);
            return combined.ToArray();
        }

        public bool TryGetInfo(string scenarioId, out ScenarioInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            return _definitionCatalog.TryGet(scenarioId, out info) && info != null;
        }

        public ScenarioValidationResult Validate(string scenarioId)
        {
            ScenarioDefinition definition;
            string scenarioFilePath;
            string errorMessage;
            if (!TryLoadUnchecked(scenarioId, out definition, out scenarioFilePath, out errorMessage))
            {
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError(errorMessage);
                return failed;
            }

            try
            {
                return _definitionValidator.Validate(definition, scenarioFilePath);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioDefinitionReader.Validate." + (scenarioId ?? string.Empty),
                    "[ScenarioDefinitionReader] Scenario validation threw for '" + (scenarioId ?? string.Empty) + "' at '" + (scenarioFilePath ?? string.Empty) + "': " + ex.Message);
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError("Scenario XML could not be loaded: " + ex.Message);
                return failed;
            }
        }

        public bool TryLoad(
            string scenarioId,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out ScenarioValidationResult validation)
        {
            validation = new ScenarioValidationResult();

            string errorMessage;
            if (!TryLoadUnchecked(scenarioId, out definition, out scenarioFilePath, out errorMessage))
            {
                validation.AddError(errorMessage);
                return false;
            }

            try
            {
                validation = _definitionValidator.Validate(definition, scenarioFilePath);
                return validation.IsValid;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioDefinitionReader.TryLoad." + (scenarioId ?? string.Empty),
                    "[ScenarioDefinitionReader] Scenario validation threw for '" + (scenarioId ?? string.Empty) + "' at '" + (scenarioFilePath ?? string.Empty) + "': " + ex.Message);
                validation = new ScenarioValidationResult();
                validation.AddError("Scenario XML could not be loaded: " + ex.Message);
                return false;
            }
        }

        public bool TryLoadUnchecked(
            string scenarioId,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out string errorMessage)
        {
            definition = null;
            scenarioFilePath = null;
            errorMessage = null;

            ScenarioInfo info;
            if (!TryGetInfo(scenarioId, out info) || info == null)
            {
                errorMessage = "Scenario is not indexed: " + scenarioId;
                return false;
            }

            scenarioFilePath = info.FilePath;
            try
            {
                string recoveryMessage;
                bool recovered;
                if (!_definitionSerializer.TryLoadWithRecovery(info.FilePath, out definition, out recoveryMessage, out recovered))
                {
                    errorMessage = string.IsNullOrEmpty(recoveryMessage) ? "Scenario XML could not be loaded." : recoveryMessage;
                    return false;
                }

                if (recovered)
                {
                    MMLog.WriteWarning("[ScenarioDefinitionReader] " + recoveryMessage);
                }

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioDefinitionReader.TryLoadUnchecked." + (scenarioId ?? string.Empty),
                    "[ScenarioDefinitionReader] Scenario XML load failed for '" + (scenarioId ?? string.Empty) + "' at '" + (scenarioFilePath ?? string.Empty) + "': " + ex.Message);
                errorMessage = "Scenario XML could not be loaded: " + ex.Message;
                return false;
            }
        }

        private static void AddDefinitionInfos(Dictionary<string, ScenarioInfo> target, ScenarioInfo[] source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                ScenarioInfo info = source[i];
                if (info == null || string.IsNullOrEmpty(info.Id) || target.ContainsKey(info.Id))
                    continue;

                target[info.Id] = info;
            }
        }

        private static int CompareScenarioDefinitionInfo(ScenarioInfo left, ScenarioInfo right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}

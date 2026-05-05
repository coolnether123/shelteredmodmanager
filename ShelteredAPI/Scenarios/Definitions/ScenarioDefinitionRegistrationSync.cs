using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDefinitionRegistrationSync : IScenarioDefinitionCatalogService
    {
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly IScenarioDefinitionReader _definitionReader;
        private readonly IScenarioRegistrationStore _store;
        private readonly ScenarioRecordFactory _recordFactory;
        private readonly ScenarioSaveDescriptorMirror _saveDescriptorMirror;
        private readonly IScenarioDefinitionDependencyReader _dependencyReader;
        private readonly IScenarioDefinitionFactory _definitionFactory;

        public ScenarioDefinitionRegistrationSync(
            IScenarioDefinitionCatalog definitionCatalog,
            IScenarioDefinitionReader definitionReader,
            IScenarioRegistrationStore store,
            ScenarioRecordFactory recordFactory,
            ScenarioSaveDescriptorMirror saveDescriptorMirror,
            IScenarioDefinitionDependencyReader dependencyReader,
            IScenarioDefinitionFactory definitionFactory)
        {
            _definitionCatalog = definitionCatalog;
            _definitionReader = definitionReader;
            _store = store;
            _recordFactory = recordFactory;
            _saveDescriptorMirror = saveDescriptorMirror;
            _dependencyReader = dependencyReader;
            _definitionFactory = definitionFactory;
        }

        public void RefreshDefinitionCatalog()
        {
            _definitionCatalog.Refresh();
            SyncDefinitionRegistrations();
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return _definitionReader.ListAll();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            return _definitionReader.Validate(scenarioId);
        }

        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            return _definitionReader.TryLoad(scenarioId, out definition, out scenarioFilePath, out validation);
        }

        private void SyncDefinitionRegistrations()
        {
            ScenarioInfo[] definitions = _definitionReader.ListAll();
            Dictionary<string, ScenarioInfo> current = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && !string.IsNullOrEmpty(definitions[i].Id))
                    current[definitions[i].Id] = definitions[i];
            }

            ScenarioRecord[] records = _store.ListRecords();
            for (int i = 0; i < records.Length; i++)
            {
                ScenarioRecord record = records[i];
                if (record != null && record.Info != null && record.IsDefinitionBacked && !current.ContainsKey(record.Info.Id))
                {
                    ScenarioRecord removed;
                    _store.Remove(record.Info.Id, out removed);
                }
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioInfo definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                ScenarioRecord existing;
                if (_store.TryGet(definition.Id, out existing) && existing != null && !existing.IsDefinitionBacked)
                    continue;

                ScenarioRecord record = CreateDefinitionRecord(definition);
                ScenarioRecord previous;
                _store.Upsert(record, out previous);
                _saveDescriptorMirror.Mirror(record.Info);
            }
        }

        private ScenarioRecord CreateDefinitionRecord(ScenarioInfo definition)
        {
            CustomScenarioRegistration registration = new CustomScenarioRegistration
            {
                Id = definition.Id,
                DisplayName = TrimToNull(definition.DisplayName) ?? definition.Id,
                Description = "XML scenario pack from " + (TrimToNull(definition.OwnerModId) ?? "loaded mod") + ".",
                Version = TrimToNull(definition.Version) ?? "1.0",
                OwnerModId = TrimToNull(definition.OwnerModId),
                RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(_dependencyReader.LoadDefinitionDependencies(definition.Id)),
                DefinitionFactory = new CustomScenarioDefinitionFactory(
                    delegate(CustomScenarioBuildContext context) { return _definitionFactory.BuildScenarioDefFromDefinition(definition.Id); })
            };

            ScenarioRecord record = _recordFactory.CreateRecord(registration);
            record.IsDefinitionBacked = true;
            return record;
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

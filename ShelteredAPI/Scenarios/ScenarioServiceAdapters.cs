using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioDefinitionSerializerAdapter : IScenarioDefinitionSerializer
    {
        private readonly ScenarioDefinitionSerializer _inner;

        public ScenarioDefinitionSerializerAdapter(ScenarioDefinitionSerializer inner)
        {
            _inner = inner ?? new ScenarioDefinitionSerializer();
        }

        public ScenarioDefinition Load(string filePath)
        {
            return _inner.Load(filePath);
        }

        public ScenarioDefinition FromXml(string xml)
        {
            return _inner.FromXml(xml);
        }

        public void Save(ScenarioDefinition definition, string filePath)
        {
            _inner.Save(definition, filePath);
        }

        public string ToXml(ScenarioDefinition definition)
        {
            return _inner.ToXml(definition);
        }

        public ScenarioInfo LoadInfo(string filePath, string ownerModId)
        {
            return _inner.LoadInfo(filePath, ownerModId);
        }
    }

    internal sealed class ScenarioDefinitionCatalogAdapter : IScenarioDefinitionCatalog
    {
        private readonly ScenarioCatalog _inner;

        public ScenarioDefinitionCatalogAdapter(ScenarioCatalog inner)
        {
            _inner = inner ?? new ScenarioCatalog();
        }

        public void Refresh()
        {
            _inner.Refresh();
        }

        public ScenarioInfo[] ListAll()
        {
            return _inner.ListAll();
        }

        public bool TryGet(string scenarioId, out ScenarioInfo info)
        {
            return _inner.TryGet(scenarioId, out info);
        }
    }

    internal sealed class ScenarioDefinitionValidatorAdapter : IScenarioDefinitionValidator
    {
        private readonly ScenarioValidatorImpl _inner;

        public ScenarioDefinitionValidatorAdapter(ScenarioValidatorImpl inner)
        {
            _inner = inner ?? new ScenarioValidatorImpl();
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            return _inner.Validate(definition, scenarioFilePath);
        }
    }
}

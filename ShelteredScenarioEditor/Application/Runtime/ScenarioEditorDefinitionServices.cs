using ShelteredScenarioEditor.Application.Runtime;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Runtime
{
    internal interface IScenarioDefinitionSerializer
    {
        ScenarioDefinition Load(string filePath);
        bool TryLoadWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered);
        ScenarioDefinition FromXml(string xml);
        void Save(ScenarioDefinition definition, string filePath);
        string ToXml(ScenarioDefinition definition);
        ScenarioInfo LoadInfo(string filePath, string ownerModId);
    }

    internal sealed class ScenarioEditorDefinitionSerializer : IScenarioDefinitionSerializer
    {
        public const string DefaultFileName = ShelteredScenarioAuthoring.DefaultFileName;

        public ScenarioDefinition Load(string filePath) { return ShelteredScenarioAuthoring.LoadDefinition(filePath); }
        public bool TryLoadWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered)
        {
            return ShelteredScenarioAuthoring.TryLoadDefinitionWithRecovery(filePath, out definition, out recoveryMessage, out recovered);
        }
        public ScenarioDefinition FromXml(string xml) { return ShelteredScenarioAuthoring.FromXml(xml); }
        public void Save(ScenarioDefinition definition, string filePath) { ShelteredScenarioAuthoring.SaveDefinition(definition, filePath); }
        public string ToXml(ScenarioDefinition definition) { return ShelteredScenarioAuthoring.ToXml(definition); }
        public ScenarioInfo LoadInfo(string filePath, string ownerModId) { return ShelteredScenarioAuthoring.LoadDefinitionInfo(filePath, ownerModId); }
    }

    internal interface IScenarioDefinitionValidator
    {
        ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath);
    }

    internal sealed class ScenarioEditorDefinitionValidator : IScenarioDefinitionValidator
    {
        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            return ShelteredScenarioAuthoring.ValidateDefinition(definition, scenarioFilePath);
        }
    }

    internal interface IScenarioDefinitionCatalogService
    {
        int CatalogRevision { get; }
        void RefreshDefinitionCatalog();
        ScenarioInfo[] ListDefinitions();
        ScenarioValidationResult ValidateDefinition(string scenarioId);
        bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation);
    }

    internal sealed class ScenarioEditorDefinitionCatalogService : IScenarioDefinitionCatalogService
    {
        private int _revision;
        public int CatalogRevision { get { return _revision; } }
        public void RefreshDefinitionCatalog() { ShelteredScenarios.RefreshXmlDefinitions(); _revision++; }
        public ScenarioInfo[] ListDefinitions() { return ShelteredScenarios.ListXmlDefinitions(); }
        public ScenarioValidationResult ValidateDefinition(string scenarioId) { return ShelteredScenarioAuthoring.ValidateXmlDefinition(scenarioId); }
        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            return ShelteredScenarioAuthoring.TryLoadXmlDefinition(scenarioId, out definition, out scenarioFilePath, out validation);
        }
    }
}

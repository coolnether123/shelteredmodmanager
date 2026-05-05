using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Diagnostics;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Stable XML scenario authoring facade for loading, saving, and validating Sheltered scenario definitions.
    /// </summary>
    public static class ShelteredScenarioAuthoring
    {
        public const string DefaultFileName = ScenarioDefinitionSerializer.DefaultFileName;

        public static ScenarioDefinition CreateDefinition()
        {
            return new ScenarioDefinition();
        }

        public static ScenarioDefinition CreateDefinition(ScenarioBaseGameMode baseGameMode)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.BaseGameMode = baseGameMode;
            return definition;
        }

        public static ScenarioDefinition LoadDefinition(string filePath)
        {
            return new ScenarioDefinitionSerializer().Load(filePath);
        }

        public static ScenarioDefinition FromXml(string xml)
        {
            return new ScenarioDefinitionSerializer().FromXml(xml);
        }

        public static void SaveDefinition(ScenarioDefinition definition, string filePath)
        {
            new ScenarioDefinitionSerializer().Save(definition, filePath);
        }

        public static string ToXml(ScenarioDefinition definition)
        {
            return new ScenarioDefinitionSerializer().ToXml(definition);
        }

        public static ScenarioValidationResult ValidateDefinition(ScenarioDefinition definition, string scenarioFilePath)
        {
            return new ScenarioValidatorImpl().Validate(definition, scenarioFilePath);
        }

        public static ScenarioValidationResult ValidateXmlDefinition(string scenarioId)
        {
            return ShelteredCustomScenarioService.Instance.ValidateDefinition(scenarioId);
        }

        public static bool TryLoadXmlDefinition(
            string scenarioId,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out ScenarioValidationResult validation)
        {
            return ShelteredCustomScenarioService.Instance.TryLoadDefinition(
                scenarioId,
                out definition,
                out scenarioFilePath,
                out validation);
        }

        public static ScenarioValidationResult RunFrameworkVerification()
        {
            return ScenarioFrameworkVerification.Run();
        }
    }
}

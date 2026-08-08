using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Runtime
{
    internal static class ScenarioEditorDefinitionCloner
    {
        public static ScenarioDefinition Clone(ScenarioDefinition definition)
        {
            if (definition == null) return null;
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            return serializer.FromXml(serializer.ToXml(definition));
        }
    }
}

using System;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioRuntimeDefinitionOverrideProvider : IScenarioRuntimeDefinitionOverrideProvider
    {
        private readonly IScenarioEditorSessionStore _sessionStore;

        public ScenarioRuntimeDefinitionOverrideProvider(IScenarioEditorSessionStore sessionStore)
        {
            if (sessionStore == null)
                throw new ArgumentNullException("sessionStore");

            _sessionStore = sessionStore;
        }

        public bool TryGetDefinitionOverride(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath)
        {
            definition = null;
            scenarioFilePath = null;

            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null || session.WorkingDefinition == null)
                return false;

            if (!string.IsNullOrEmpty(scenarioId)
                && !string.Equals(session.WorkingDefinition.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            definition = ScenarioDefinitionCloner.Clone(session.WorkingDefinition);
            scenarioFilePath = _sessionStore.CurrentFilePath;
            return definition != null;
        }
    }
}

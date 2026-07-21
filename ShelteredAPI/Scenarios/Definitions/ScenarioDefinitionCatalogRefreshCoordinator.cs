using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;

namespace ShelteredAPI.Scenarios.Definitions
{
    internal sealed class ScenarioDefinitionCatalogRefreshCoordinator : IScenarioDefinitionCatalogService
    {
        private readonly IScenarioDefinitionCatalogService _inner;
        private readonly Func<IScenarioRuntimeOrchestrator> _runtimeOrchestratorResolver;

        public ScenarioDefinitionCatalogRefreshCoordinator(
            IScenarioDefinitionCatalogService inner,
            Func<IScenarioRuntimeOrchestrator> runtimeOrchestratorResolver)
        {
            _inner = inner;
            _runtimeOrchestratorResolver = runtimeOrchestratorResolver;
        }

        public int CatalogRevision
        {
            get { return _inner.CatalogRevision; }
        }

        public void RefreshDefinitionCatalog()
        {
            int previousRevision = _inner.CatalogRevision;
            _inner.RefreshDefinitionCatalog();
            if (_inner.CatalogRevision != previousRevision)
                RetryActiveScenarioApply();
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return _inner.ListDefinitions();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            return _inner.ValidateDefinition(scenarioId);
        }

        public bool TryLoadDefinition(
            string scenarioId,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out ScenarioValidationResult validation)
        {
            return _inner.TryLoadDefinition(scenarioId, out definition, out scenarioFilePath, out validation);
        }

        private void RetryActiveScenarioApply()
        {
            try
            {
                IScenarioRuntimeOrchestrator orchestrator = _runtimeOrchestratorResolver != null
                    ? _runtimeOrchestratorResolver()
                    : null;

                if (orchestrator != null)
                    orchestrator.UpdateActiveScenarioApply();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioDefinitionCatalogRefresh] Active scenario apply retry after catalog refresh failed: " + ex.Message);
            }
        }
    }
}

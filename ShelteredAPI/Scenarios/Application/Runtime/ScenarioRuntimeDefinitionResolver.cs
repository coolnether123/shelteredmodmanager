using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;

namespace ShelteredAPI.Scenarios.Application.Runtime
{
    /// <summary>
    /// Canonical definition source for an active runtime binding. Installed runs resolve
    /// through the catalog; preview runs resolve through one process-local session.
    /// </summary>
    internal sealed class ScenarioRuntimeDefinitionResolver
    {
        private readonly object _sync = new object();
        private readonly IScenarioDefinitionCatalogService _catalog;
        private ScenarioDefinition _previewDefinition;
        private string _previewFilePath;
        private string _previewRunId;
        private int _previewRevision;

        public ScenarioRuntimeDefinitionResolver(IScenarioDefinitionCatalogService catalog)
        {
            _catalog = catalog;
        }

        public int Revision
        {
            get
            {
                lock (_sync)
                {
                    int catalogRevision = _catalog != null ? _catalog.CatalogRevision : 0;
                    return unchecked((catalogRevision * 397) ^ _previewRevision);
                }
            }
        }

        public void SetPreview(ScenarioDefinition definition, string scenarioFilePath, string runId)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (string.IsNullOrEmpty(runId))
                throw new ArgumentException("A preview run id is required.", "runId");

            lock (_sync)
            {
                _previewDefinition = definition;
                _previewFilePath = scenarioFilePath;
                _previewRunId = runId;
                _previewRevision++;
            }
        }

        public void ClearPreview(string runId)
        {
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(runId)
                    && !string.Equals(_previewRunId, runId, StringComparison.Ordinal))
                {
                    return;
                }

                if (_previewDefinition == null && string.IsNullOrEmpty(_previewRunId))
                    return;

                _previewDefinition = null;
                _previewFilePath = null;
                _previewRunId = null;
                _previewRevision++;
            }
        }

        public bool HasPreview(ScenarioRuntimeBinding binding)
        {
            if (binding == null || !binding.IsPreview || string.IsNullOrEmpty(binding.RunId))
                return false;

            lock (_sync)
            {
                return _previewDefinition != null
                    && string.Equals(_previewRunId, binding.RunId, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(binding.ScenarioId)
                        || string.Equals(_previewDefinition.Id, binding.ScenarioId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool TryResolve(
            ScenarioRuntimeBinding binding,
            out ScenarioDefinition definition,
            out string scenarioFilePath,
            out ScenarioValidationResult validation)
        {
            definition = null;
            scenarioFilePath = null;
            validation = null;
            if (binding == null || string.IsNullOrEmpty(binding.ScenarioId))
                return false;

            if (!binding.IsPreview)
            {
                return _catalog != null
                    && _catalog.TryLoadDefinition(
                        binding.ScenarioId,
                        out definition,
                        out scenarioFilePath,
                        out validation);
            }

            lock (_sync)
            {
                if (!HasPreviewUnsafe(binding))
                {
                    validation = new ScenarioValidationResult();
                    validation.AddError("The process-local preview definition is no longer available.");
                    return false;
                }

                definition = _previewDefinition;
                scenarioFilePath = _previewFilePath;
                validation = new ScenarioValidationResult();
                return true;
            }
        }

        private bool HasPreviewUnsafe(ScenarioRuntimeBinding binding)
        {
            return _previewDefinition != null
                && binding != null
                && binding.IsPreview
                && string.Equals(_previewRunId, binding.RunId, StringComparison.Ordinal)
                && (string.IsNullOrEmpty(binding.ScenarioId)
                    || string.Equals(_previewDefinition.Id, binding.ScenarioId, StringComparison.OrdinalIgnoreCase));
        }
    }
}

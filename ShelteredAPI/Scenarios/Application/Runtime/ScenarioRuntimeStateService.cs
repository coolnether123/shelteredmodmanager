using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioRuntimeStateService
    {
        private readonly ScenarioRuntimeExecutionJournalRepository _repository;

        public ScenarioRuntimeStateService(ScenarioRuntimeExecutionJournalRepository repository)
        {
            _repository = repository;
        }

        public ScenarioRuntimeState State
        {
            get { return _repository.State; }
        }

        public void EnsureHooked()
        {
            _repository.EnsureHooked();
        }

        public ScenarioRuntimeState Bind(ScenarioDefinition definition, ScenarioRuntimeBinding binding)
        {
            string scenarioId = definition != null ? definition.Id : null;
            string version = definition != null ? definition.Version : null;
            string runtimeBindingId = BuildRuntimeBindingId(binding, scenarioId, version);
            ScenarioRuntimeState state = _repository.State;
            if (state == null
                || !string.Equals(state.ScenarioId ?? string.Empty, scenarioId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(state.RuntimeBindingId ?? string.Empty, runtimeBindingId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                state = new ScenarioRuntimeState();
                _repository.Replace(state);
            }

            state.ScenarioId = scenarioId;
            state.ScenarioVersion = version;
            state.RuntimeBindingId = runtimeBindingId;
            return state;
        }

        private static string BuildRuntimeBindingId(ScenarioRuntimeBinding binding, string scenarioId, string version)
        {
            if (binding == null)
                return (scenarioId ?? string.Empty) + "@" + (version ?? string.Empty);
            return (binding.ScenarioId ?? scenarioId ?? string.Empty)
                + "@"
                + (binding.VersionApplied ?? version ?? string.Empty)
                + "#"
                + binding.DayCreated.ToString()
                + "#"
                + (binding.LastEditorSaveTick.HasValue ? ("playtest:" + binding.LastEditorSaveTick.Value.ToString()) : "normal");
        }
    }
}

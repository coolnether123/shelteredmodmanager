using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class TriggerRuntimeAdapter
    {
        private readonly ScenarioScheduleRuntimeCoordinator _scheduleCoordinator;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;

        public TriggerRuntimeAdapter(
            ScenarioScheduleRuntimeCoordinator scheduleCoordinator,
            IScenarioRuntimeBindingService runtimeBindingService)
        {
            _scheduleCoordinator = scheduleCoordinator;
            _runtimeBindingService = runtimeBindingService;
        }

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null)
                return;

            ScenarioScheduledGameplayRuntime.Install(
                definition,
                _scheduleCoordinator,
                _runtimeBindingService != null ? _runtimeBindingService.GetActiveBindingForStartup() : null);

            result.TriggerChanges += definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count : 0;
            result.ConditionChanges += definition.WinLossConditions != null
                ? (definition.WinLossConditions.WinConditions.Count + definition.WinLossConditions.LossConditions.Count)
                : 0;
        }
    }
}

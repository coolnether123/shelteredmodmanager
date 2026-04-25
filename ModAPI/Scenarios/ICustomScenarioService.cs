using System;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Shared service for custom scenario registration and custom-scenario lifecycle state.
    /// The game-specific runtime assembly owns the implementation.
    /// </summary>
    public interface ICustomScenarioService
    {
        event Action<CustomScenarioEventArgs> ScenarioRegistered;
        event Action<CustomScenarioEventArgs> ScenarioUnregistered;
        event Action<CustomScenarioEventArgs> ScenarioSelected;
        event Action<CustomScenarioEventArgs> ScenarioSpawned;
        event Action<CustomScenarioEventArgs> StateChanged;

        CustomScenarioState CurrentState { get; }

        CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
        bool Unregister(string scenarioId);
        bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
        CustomScenarioInfo[] List();
        bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
    }
}

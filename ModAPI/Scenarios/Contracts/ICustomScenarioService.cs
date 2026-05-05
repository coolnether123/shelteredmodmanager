using System;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Shared service for custom scenario registration and custom-scenario lifecycle state.
    /// The game-specific runtime owns the implementation and translates neutral registrations into game objects.
    /// </summary>
    public interface ICustomScenarioService
    {
        /// <summary>Raised after a scenario registration is accepted.</summary>
        event Action<CustomScenarioEventArgs> ScenarioRegistered;
        /// <summary>Raised after a scenario registration is removed.</summary>
        event Action<CustomScenarioEventArgs> ScenarioUnregistered;
        /// <summary>Raised when the player selects a custom scenario.</summary>
        event Action<CustomScenarioEventArgs> ScenarioSelected;
        /// <summary>Raised when the selected custom scenario has been spawned into the game runtime.</summary>
        event Action<CustomScenarioEventArgs> ScenarioSpawned;
        /// <summary>Raised whenever the current custom scenario state changes.</summary>
        event Action<CustomScenarioEventArgs> StateChanged;

        /// <summary>Current custom scenario state, or <see cref="CustomScenarioState.None"/> when no custom scenario is active.</summary>
        CustomScenarioState CurrentState { get; }

        /// <summary>Registers or replaces a custom scenario.</summary>
        CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration);
        /// <summary>Removes a registered scenario by ID.</summary>
        bool Unregister(string scenarioId);
        /// <summary>Attempts to get a read-only registration view by scenario ID.</summary>
        bool TryGet(string scenarioId, out CustomScenarioInfo scenario);
        /// <summary>Lists registered custom scenarios in runtime-defined display order.</summary>
        CustomScenarioInfo[] List();
        /// <summary>Builds the game-specific scenario definition for a registered scenario.</summary>
        bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage);
    }
}

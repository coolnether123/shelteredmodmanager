using System;
using System.Reflection;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Builds a game-specific scenario definition when the runtime needs one.
    /// </summary>
    public delegate object CustomScenarioDefinitionFactory(CustomScenarioBuildContext context);

    /// <summary>
    /// Current lifecycle state for the active custom scenario, if any.
    /// </summary>
    public enum CustomScenarioLifecycleState
    {
        None = 0,
        Pending = 1,
        Active = 2
    }

    /// <summary>
    /// Event categories raised by <see cref="ICustomScenarioService"/>.
    /// </summary>
    public enum CustomScenarioEventType
    {
        Registered = 0,
        Unregistered = 1,
        Selected = 2,
        Spawned = 3,
        Cleared = 4
    }

    /// <summary>
    /// Mod-authored custom scenario registration data.
    /// Provide either a ready definition or a factory when definition construction needs runtime context.
    /// </summary>
    public class CustomScenarioRegistration
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public int Order { get; set; }
        public string OwnerModId { get; set; }
        public Assembly OwnerAssembly { get; set; }
        public ScenarioModDependency[] RequiredMods { get; set; }
        public object Definition { get; set; }
        public CustomScenarioDefinitionFactory DefinitionFactory { get; set; }
        public Action<CustomScenarioEventArgs> OnSelected { get; set; }
        public Action<CustomScenarioEventArgs> OnSpawned { get; set; }
        public object UserData { get; set; }
    }

    /// <summary>
    /// Public read model for a registered custom scenario.
    /// This is safe to expose to other mods because it omits callbacks and mutable registration internals.
    /// </summary>
    public class CustomScenarioInfo
    {
        public CustomScenarioInfo(
            string id,
            string displayName,
            string description,
            string version,
            int order,
            string ownerModId,
            ScenarioModDependency[] requiredMods,
            bool hasDefinition,
            bool hasDefinitionFactory)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Version = version;
            Order = order;
            OwnerModId = ownerModId;
            RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(requiredMods);
            HasDefinition = hasDefinition;
            HasDefinitionFactory = hasDefinitionFactory;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string Version { get; private set; }
        public int Order { get; private set; }
        public string OwnerModId { get; private set; }
        public ScenarioModDependency[] RequiredMods { get; private set; }
        public bool HasDefinition { get; private set; }
        public bool HasDefinitionFactory { get; private set; }
    }

    /// <summary>
    /// Context passed to a scenario definition factory.
    /// Use this to tailor the game-specific definition to the requesting mod and current scenario state.
    /// </summary>
    public class CustomScenarioBuildContext
    {
        public string ScenarioId { get; set; }
        public string OwnerModId { get; set; }
        public string RequestedByModId { get; set; }
        public CustomScenarioState State { get; set; }
        public object UserData { get; set; }
    }

    /// <summary>
    /// Current custom scenario state.
    /// This intentionally tracks custom scenarios only; base-game scenario state remains owned by the game runtime.
    /// </summary>
    public class CustomScenarioState
    {
        public CustomScenarioState()
        {
            LifecycleState = CustomScenarioLifecycleState.None;
        }

        public string ScenarioId { get; set; }
        public CustomScenarioLifecycleState LifecycleState { get; set; }
        public bool HasCustomScenario
        {
            get { return !string.IsNullOrEmpty(ScenarioId) && LifecycleState != CustomScenarioLifecycleState.None; }
        }

        public static CustomScenarioState None()
        {
            return new CustomScenarioState();
        }

        public CustomScenarioState Copy()
        {
            return new CustomScenarioState
            {
                ScenarioId = ScenarioId,
                LifecycleState = LifecycleState
            };
        }
    }

    /// <summary>
    /// Event arguments raised for custom scenario registration, selection, spawn, and state changes.
    /// The state is copied so handlers cannot mutate the service's current state.
    /// </summary>
    public class CustomScenarioEventArgs : EventArgs
    {
        public CustomScenarioEventArgs(CustomScenarioEventType eventType, CustomScenarioInfo scenario, CustomScenarioState state)
        {
            EventType = eventType;
            Scenario = scenario;
            State = state != null ? state.Copy() : CustomScenarioState.None();
        }

        public CustomScenarioEventType EventType { get; private set; }
        public CustomScenarioInfo Scenario { get; private set; }
        public CustomScenarioState State { get; private set; }
    }

    /// <summary>
    /// Result returned when a mod attempts to register a custom scenario.
    /// Read <see cref="Success"/> before using the registered scenario ID.
    /// </summary>
    public class CustomScenarioRegistrationResult
    {
        public bool Success { get; private set; }
        public string ScenarioId { get; private set; }
        public bool ReplacedExisting { get; private set; }
        public string ErrorMessage { get; private set; }

        public static CustomScenarioRegistrationResult Ok(string scenarioId, bool replacedExisting)
        {
            return new CustomScenarioRegistrationResult
            {
                Success = true,
                ScenarioId = scenarioId,
                ReplacedExisting = replacedExisting
            };
        }

        public static CustomScenarioRegistrationResult Failed(string errorMessage)
        {
            return new CustomScenarioRegistrationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}

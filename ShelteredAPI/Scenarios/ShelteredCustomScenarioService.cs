using System;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Sheltered runtime implementation of the neutral custom scenario service contract.
    /// </summary>
    public sealed class ShelteredCustomScenarioService : ICustomScenarioService, IShelteredCustomScenarioService
    {
        private readonly IScenarioRegistrationStore _registrations;
        private readonly ScenarioRegistrationService _registrationService;
        private readonly ScenarioDefinitionService _definitionService;
        private readonly ScenarioDefinitionRegistrationSync _definitionRegistrationSync;
        private readonly ScenarioDependencyService _dependencyService;
        private readonly ScenarioLifecycleService _lifecycleService;
        private readonly ScenarioEventHub _events;
        private readonly IScenarioStateManager _stateManager;

        public static ShelteredCustomScenarioService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ShelteredCustomScenarioService>(); }
        }

        public event Action<CustomScenarioEventArgs> ScenarioRegistered
        {
            add { _events.ScenarioRegistered += value; }
            remove { _events.ScenarioRegistered -= value; }
        }

        public event Action<CustomScenarioEventArgs> ScenarioUnregistered
        {
            add { _events.ScenarioUnregistered += value; }
            remove { _events.ScenarioUnregistered -= value; }
        }

        public event Action<CustomScenarioEventArgs> ScenarioSelected
        {
            add { _events.ScenarioSelected += value; }
            remove { _events.ScenarioSelected -= value; }
        }

        public event Action<CustomScenarioEventArgs> ScenarioSpawned
        {
            add { _events.ScenarioSpawned += value; }
            remove { _events.ScenarioSpawned -= value; }
        }

        public event Action<CustomScenarioEventArgs> StateChanged
        {
            add { _events.StateChanged += value; }
            remove { _events.StateChanged -= value; }
        }

        public CustomScenarioState CurrentState
        {
            get { return _stateManager.GetCustomScenarioState(); }
        }

        internal ShelteredCustomScenarioService(
            IScenarioRegistrationStore registrations,
            ScenarioRegistrationService registrationService,
            ScenarioDefinitionService definitionService,
            ScenarioDefinitionRegistrationSync definitionRegistrationSync,
            ScenarioDependencyService dependencyService,
            ScenarioLifecycleService lifecycleService,
            ScenarioEventHub events,
            IScenarioStateManager stateManager)
        {
            _registrations = registrations;
            _registrationService = registrationService;
            _definitionService = definitionService;
            _definitionRegistrationSync = definitionRegistrationSync;
            _dependencyService = dependencyService;
            _lifecycleService = lifecycleService;
            _events = events;
            _stateManager = stateManager;
        }

        public void RefreshDefinitionCatalog()
        {
            _definitionRegistrationSync.RefreshDefinitionCatalog();
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return _definitionRegistrationSync.ListDefinitions();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            return _definitionRegistrationSync.ValidateDefinition(scenarioId);
        }

        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            return _definitionRegistrationSync.TryLoadDefinition(scenarioId, out definition, out scenarioFilePath, out validation);
        }

        public CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration)
        {
            return _registrationService.Register(registration);
        }

        public bool Unregister(string scenarioId)
        {
            return _registrationService.Unregister(scenarioId);
        }

        public bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
        {
            scenario = null;
            ScenarioRecord record;
            if (!_registrations.TryGet(scenarioId, out record))
                return false;

            scenario = record.Info;
            return true;
        }

        public CustomScenarioInfo[] List()
        {
            return _registrations.ListInfos();
        }

        public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
        {
            return _definitionService.TryCreateDefinition(scenarioId, context, out definition, out errorMessage);
        }

        public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
        {
            return _definitionService.TryCreateScenarioDef(scenarioId, context, out definition, out errorMessage);
        }

        public bool MarkSelected(string scenarioId)
        {
            return _lifecycleService.MarkSelected(scenarioId);
        }

        public bool MarkSpawned(string scenarioId)
        {
            return _lifecycleService.MarkSpawned(scenarioId);
        }

        public void ClearState()
        {
            _lifecycleService.ClearState();
        }

        public SlotManifest CreateDependencyManifest(CustomScenarioInfo info)
        {
            return _dependencyService.CreateDependencyManifest(info);
        }

        public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
        {
            return _dependencyService.VerifyDependencies(info);
        }
    }
}

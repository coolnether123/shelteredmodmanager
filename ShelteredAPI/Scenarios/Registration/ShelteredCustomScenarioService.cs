using System;
using System.Reflection;
using ShelteredAPI.Saves.Paging;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Sheltered runtime implementation of the neutral custom scenario service contract.
    /// </summary>
    internal sealed class ShelteredCustomScenarioService : ICustomScenarioService,
        IShelteredCustomScenarioService,
        ICustomScenarioRegistry,
        IScenarioDefinitionCatalogService,
        IScenarioDefinitionFactory,
        ICustomScenarioLifecycleService,
        IScenarioDependencyVerifier
    {
        private readonly IScenarioRegistrationStore _registrations;
        private readonly ScenarioRegistrationService _registrationService;
        private readonly IScenarioDefinitionFactory _definitionFactory;
        private readonly IScenarioDefinitionCatalogService _definitionCatalog;
        private readonly IScenarioDependencyVerifier _dependencyVerifier;
        private readonly ICustomScenarioLifecycleService _lifecycleService;
        private readonly ScenarioEventHub _events;
        private readonly object _definitionCatalogSync = new object();
        private bool _definitionCatalogLoaded;

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
            get { return _lifecycleService.CurrentState; }
        }

        internal ShelteredCustomScenarioService(
            IScenarioRegistrationStore registrations,
            ScenarioRegistrationService registrationService,
            IScenarioDefinitionFactory definitionFactory,
            IScenarioDefinitionCatalogService definitionCatalog,
            IScenarioDependencyVerifier dependencyVerifier,
            ICustomScenarioLifecycleService lifecycleService,
            ScenarioEventHub events)
        {
            _registrations = registrations;
            _registrationService = registrationService;
            _definitionFactory = definitionFactory;
            _definitionCatalog = definitionCatalog;
            _dependencyVerifier = dependencyVerifier;
            _lifecycleService = lifecycleService;
            _events = events;
        }

        public void RefreshDefinitionCatalog()
        {
            _definitionCatalog.RefreshDefinitionCatalog();
            lock (_definitionCatalogSync)
            {
                _definitionCatalogLoaded = true;
            }
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return _definitionCatalog.ListDefinitions();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            return _definitionCatalog.ValidateDefinition(scenarioId);
        }

        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            return _definitionCatalog.TryLoadDefinition(scenarioId, out definition, out scenarioFilePath, out validation);
        }

        public CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration)
        {
            Assembly callerAssembly = null;
            try { callerAssembly = Assembly.GetCallingAssembly(); } catch { }
            return _registrationService.Register(registration, callerAssembly);
        }

        public bool Unregister(string scenarioId)
        {
            return _registrationService.Unregister(scenarioId);
        }

        public bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
        {
            EnsureDefinitionCatalogLoaded();
            scenario = null;
            ScenarioRecord record;
            if (!_registrations.TryGet(scenarioId, out record))
                return false;

            scenario = record.Info;
            return true;
        }

        public CustomScenarioInfo[] List()
        {
            EnsureDefinitionCatalogLoaded();
            return _registrations.ListInfos();
        }

        private void EnsureDefinitionCatalogLoaded()
        {
            if (_definitionCatalogLoaded)
                return;

            lock (_definitionCatalogSync)
            {
                if (_definitionCatalogLoaded)
                    return;

                _definitionCatalog.RefreshDefinitionCatalog();
                _definitionCatalogLoaded = true;
            }
        }

        public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
        {
            return _definitionFactory.TryCreateDefinition(scenarioId, context, out definition, out errorMessage);
        }

        public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
        {
            return _definitionFactory.TryCreateScenarioDef(scenarioId, context, out definition, out errorMessage);
        }

        public ScenarioDef BuildScenarioDefFromDefinition(string scenarioId)
        {
            return _definitionFactory.BuildScenarioDefFromDefinition(scenarioId);
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
            return _dependencyVerifier.CreateDependencyManifest(info);
        }

        public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
        {
            return _dependencyVerifier.VerifyDependencies(info);
        }
    }
}

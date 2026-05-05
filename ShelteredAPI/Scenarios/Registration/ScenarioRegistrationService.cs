using System.Reflection;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.Scenarios.Registration{
    internal sealed class ScenarioRegistrationService
    {
        private readonly ScenarioRegistrationValidator _validator;
        private readonly ScenarioRecordFactory _factory;
        private readonly IScenarioRegistrationStore _store;
        private readonly ScenarioSaveDescriptorMirror _saveDescriptorMirror;
        private readonly ScenarioEventHub _events;
        private readonly IScenarioStateManager _stateManager;

        public ScenarioRegistrationService(
            ScenarioRegistrationValidator validator,
            ScenarioRecordFactory factory,
            IScenarioRegistrationStore store,
            ScenarioSaveDescriptorMirror saveDescriptorMirror,
            ScenarioEventHub events,
            IScenarioStateManager stateManager)
        {
            _validator = validator;
            _factory = factory;
            _store = store;
            _saveDescriptorMirror = saveDescriptorMirror;
            _events = events;
            _stateManager = stateManager;
        }

        /// <summary>
        /// ScenarioRegistered fires for both new registrations and replacements. Only the direct Register
        /// caller receives CustomScenarioRegistrationResult.ReplacedExisting. Event listeners cannot
        /// distinguish add vs. replace from this event.
        /// </summary>
        public CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration, Assembly callerAssembly)
        {
            string error;
            CustomScenarioRegistration normalized = _validator.Normalize(registration, callerAssembly, out error);
            if (normalized == null)
                return CustomScenarioRegistrationResult.Failed(error);

            ScenarioRecord record = _factory.CreateRecord(normalized);
            ScenarioRecord previous;
            bool replacedExisting = _store.Upsert(record, out previous);
            _saveDescriptorMirror.Mirror(record.Info);
            _events.RaiseRegistered(record.Info);
            return CustomScenarioRegistrationResult.Ok(record.Info.Id, replacedExisting);
        }

        public bool Unregister(string scenarioId)
        {
            ScenarioRecord removed;
            if (!_store.Remove(scenarioId, out removed))
                return false;

            bool clearedState = false;
            CustomScenarioState currentState = _stateManager.GetCustomScenarioState();
            if (currentState != null && string.Equals(currentState.ScenarioId, scenarioId, System.StringComparison.OrdinalIgnoreCase))
                clearedState = true;

            if (clearedState)
                _stateManager.SetCustomScenarioState(CustomScenarioState.None(), "custom-scenario", "Scenario unregistered.");

            _events.RaiseUnregistered(removed.Info);
            if (clearedState)
                _events.RaiseCleared(removed.Info);
            return true;
        }
    }
}

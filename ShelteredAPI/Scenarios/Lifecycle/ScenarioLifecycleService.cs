using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Lifecycle{
    internal sealed class ScenarioLifecycleService : ICustomScenarioLifecycleService
    {
        private readonly IScenarioRegistrationStore _store;
        private readonly IScenarioDependencyVerifier _dependencyVerifier;
        private readonly IScenarioStateManager _stateManager;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly ScenarioEventHub _events;

        public ScenarioLifecycleService(
            IScenarioRegistrationStore store,
            IScenarioDependencyVerifier dependencyVerifier,
            IScenarioStateManager stateManager,
            IScenarioRuntimeBindingService runtimeBindingService,
            ScenarioEventHub events)
        {
            _store = store;
            _dependencyVerifier = dependencyVerifier;
            _stateManager = stateManager;
            _runtimeBindingService = runtimeBindingService;
            _events = events;
        }

        public CustomScenarioState CurrentState
        {
            get { return _stateManager.GetCustomScenarioState(); }
        }

        public bool MarkSelected(string scenarioId)
        {
            ScenarioRecord record;
            if (!_store.TryGet(scenarioId, out record))
                return false;

            if (_dependencyVerifier.VerifyDependencies(record.Info) != ScenarioDependencyVerificationState.Match)
            {
                MMLog.WriteWarning("[ScenarioLifecycleService] Custom scenario dependencies are not satisfied: " + scenarioId);
                return false;
            }

            _stateManager.SetCustomScenarioState(new CustomScenarioState
            {
                ScenarioId = record.Info.Id,
                LifecycleState = CustomScenarioLifecycleState.Pending
            }, "custom-scenario", "Scenario selected.");

            CustomScenarioEventArgs args = _events.CreateArgs(CustomScenarioEventType.Selected, record.Info);
            InvokeRegistrationCallback(record.Registration.OnSelected, args, record.Info.Id, "OnSelected");
            _events.RaiseSelected(args);
            _events.RaiseStateChanged(args);
            return true;
        }

        public bool MarkSpawned(string scenarioId)
        {
            ScenarioRecord record;
            if (!_store.TryGet(scenarioId, out record))
                return false;

            _stateManager.SetCustomScenarioState(new CustomScenarioState
            {
                ScenarioId = record.Info.Id,
                LifecycleState = CustomScenarioLifecycleState.Active
            }, "custom-scenario", "Scenario spawned.");

            _runtimeBindingService.SetBinding(CreateRuntimeBinding(record.Info));
            CustomScenarioEventArgs args = _events.CreateArgs(CustomScenarioEventType.Spawned, record.Info);
            InvokeRegistrationCallback(record.Registration.OnSpawned, args, record.Info.Id, "OnSpawned");
            _events.RaiseSpawned(args);
            _events.RaiseStateChanged(args);
            return true;
        }

        public void ClearState()
        {
            CustomScenarioInfo previousInfo = null;
            bool hadState = false;
            CustomScenarioState currentState = _stateManager.GetCustomScenarioState();
            if (currentState != null && !string.IsNullOrEmpty(currentState.ScenarioId))
            {
                hadState = true;
                ScenarioRecord record;
                if (_store.TryGet(currentState.ScenarioId, out record))
                    previousInfo = record.Info;
            }

            _stateManager.SetCustomScenarioState(CustomScenarioState.None(), "custom-scenario", "State cleared.");
            if (hadState)
                _events.RaiseCleared(previousInfo);
        }

        private static ScenarioRuntimeBinding CreateRuntimeBinding(CustomScenarioInfo info)
        {
            return new ScenarioRuntimeBinding
            {
                ScenarioId = info != null ? info.Id : null,
                VersionApplied = info != null ? info.Version : null,
                IsActive = true,
                IsConvertedToNormalSave = false,
                DayCreated = GetCurrentDay()
            };
        }

        private static int GetCurrentDay()
        {
            try { return GameTime.Day; }
            catch { return 0; }
        }

        private static void InvokeRegistrationCallback(
            Action<CustomScenarioEventArgs> callback,
            CustomScenarioEventArgs args,
            string scenarioId,
            string callbackName)
        {
            if (callback == null)
                return;

            try
            {
                callback(args);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioLifecycleService." + callbackName + "." + scenarioId, ex.Message);
            }
        }
    }
}

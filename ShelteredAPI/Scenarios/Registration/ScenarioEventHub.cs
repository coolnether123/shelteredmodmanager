using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
namespace ShelteredAPI.Scenarios.Registration{
    internal sealed class ScenarioEventHub
    {
        private readonly IScenarioStateManager _stateManager;

        public event Action<CustomScenarioEventArgs> ScenarioRegistered;
        public event Action<CustomScenarioEventArgs> ScenarioUnregistered;
        public event Action<CustomScenarioEventArgs> ScenarioSelected;
        public event Action<CustomScenarioEventArgs> ScenarioSpawned;
        public event Action<CustomScenarioEventArgs> StateChanged;

        public ScenarioEventHub(IScenarioStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public void RaiseRegistered(CustomScenarioInfo info)
        {
            Raise(ScenarioRegistered, CustomScenarioEventType.Registered, info);
        }

        public void RaiseUnregistered(CustomScenarioInfo info)
        {
            Raise(ScenarioUnregistered, CustomScenarioEventType.Unregistered, info);
        }

        public void RaiseSelected(CustomScenarioInfo info)
        {
            Raise(ScenarioSelected, CustomScenarioEventType.Selected, info);
        }

        public void RaiseSelected(CustomScenarioEventArgs args)
        {
            Raise(ScenarioSelected, args);
        }

        public void RaiseSpawned(CustomScenarioInfo info)
        {
            Raise(ScenarioSpawned, CustomScenarioEventType.Spawned, info);
        }

        public void RaiseSpawned(CustomScenarioEventArgs args)
        {
            Raise(ScenarioSpawned, args);
        }

        public void RaiseCleared(CustomScenarioInfo info)
        {
            Raise(StateChanged, CustomScenarioEventType.Cleared, info);
        }

        public void RaiseStateChanged(CustomScenarioEventArgs args)
        {
            Raise(StateChanged, args);
        }

        public CustomScenarioEventArgs CreateArgs(CustomScenarioEventType eventType, CustomScenarioInfo info)
        {
            return new CustomScenarioEventArgs(eventType, info, _stateManager.GetCustomScenarioState());
        }

        private void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventType eventType, CustomScenarioInfo info)
        {
            Raise(handler, CreateArgs(eventType, info));
        }

        private static void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventArgs args)
        {
            if (handler == null || args == null)
                return;

            try
            {
                handler(args);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioEventHub.Event." + args.EventType, ex.Message);
            }
        }
    }
}

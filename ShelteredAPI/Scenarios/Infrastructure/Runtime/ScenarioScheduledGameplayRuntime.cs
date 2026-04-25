using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioScheduledGameplayRuntime : MonoBehaviour
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioScheduledGameplayRuntime";
        private ScenarioScheduleRuntimeCoordinator _coordinator;
        private int _lastDay = -1;
        private int _lastHour = -1;
        private int _lastMinute = -1;
        private int _fallbackFrameCounter;

        public static void Install(ScenarioDefinition definition, ScenarioScheduleRuntimeCoordinator coordinator, ScenarioRuntimeBinding binding)
        {
            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                Object.DontDestroyOnLoad(runtimeObject);
            }

            ScenarioScheduledGameplayRuntime runtime = runtimeObject.GetComponent<ScenarioScheduledGameplayRuntime>();
            if (runtime == null)
                runtime = runtimeObject.AddComponent<ScenarioScheduledGameplayRuntime>();
            runtime.Initialize(definition, coordinator, binding);
        }

        private void Initialize(ScenarioDefinition definition, ScenarioScheduleRuntimeCoordinator coordinator, ScenarioRuntimeBinding binding)
        {
            _coordinator = coordinator;
            if (_coordinator != null)
                _coordinator.Initialize(definition, binding);
            _lastDay = -1;
            _lastHour = -1;
            _lastMinute = -1;
        }

        private void Update()
        {
            if (_coordinator == null)
                return;

            bool changed = GameTime.Day != _lastDay || GameTime.Hour != _lastHour || GameTime.Minute != _lastMinute;
            _fallbackFrameCounter++;
            if (!changed && _fallbackFrameCounter < 300)
                return;

            _fallbackFrameCounter = 0;
            _lastDay = GameTime.Day;
            _lastHour = GameTime.Hour;
            _lastMinute = GameTime.Minute;
            _coordinator.TickOnGameTimeChanged();
        }
    }
}

using System;
using System.Reflection;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScheduledWeatherRuntimeService : IScenarioEffectHandler
    {
        private static readonly MethodInfo ActivateWeatherMethod = typeof(WeatherManager).GetMethod("ActivateWeather", BindingFlags.NonPublic | BindingFlags.Instance);

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.SetWeather || kind == ScenarioEffectKind.RestoreWeather;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null || WeatherManager.Instance == null || ActivateWeatherMethod == null)
            {
                message = "WeatherManager is not ready.";
                return false;
            }

            WeatherManager.WeatherState weather;
            if (!TryParseWeather(effect.WeatherState, out weather))
            {
                message = "Invalid weather state: " + (effect.WeatherState ?? string.Empty);
                return false;
            }

            ActivateWeatherMethod.Invoke(WeatherManager.Instance, new object[] { weather });
            return true;
        }

        private static bool TryParseWeather(string value, out WeatherManager.WeatherState state)
        {
            state = WeatherManager.WeatherState.None;
            if (string.IsNullOrEmpty(value))
                return false;
            try
            {
                object parsed = Enum.Parse(typeof(WeatherManager.WeatherState), value, true);
                state = (WeatherManager.WeatherState)parsed;
                return state != WeatherManager.WeatherState.Max;
            }
            catch
            {
                return false;
            }
        }
    }
}

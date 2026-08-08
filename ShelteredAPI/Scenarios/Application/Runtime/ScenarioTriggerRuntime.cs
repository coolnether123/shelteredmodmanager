using System;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Composition;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal static class ScenarioTriggerRuntime
    {
        public static bool Fire(string triggerId)
        {
            string message;
            return Fire(triggerId, "api", out message);
        }

        public static bool Fire(string triggerId, string source, out string message)
        {
            message = null;
            try
            {
                IScenarioTriggerRuntimeService service = ScenarioRuntimeCompositionRoot.Resolve<IScenarioTriggerRuntimeService>();
                return service != null && service.Fire(triggerId, source, out message);
            }
            catch (Exception ex)
            {
                message = "Scenario trigger runtime is unavailable: " + ex.Message;
                return false;
            }
        }
    }
}

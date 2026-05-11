using System;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Stable runtime facade for custom scenario interactions.
    /// </summary>
    public static class ShelteredScenarioRuntime
    {
        public static bool FireTrigger(string triggerId)
        {
            return ScenarioTriggerRuntime.Fire(triggerId);
        }

        public static bool FireTrigger(string triggerId, string source, out string message)
        {
            return ScenarioTriggerRuntime.Fire(triggerId, source, out message);
        }

        public static ScenarioScoreSnapshot GetScoreSnapshot()
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioCompositionRoot.ResolveRuntime<IScenarioScoreSnapshotService>();
                return service != null ? service.GetSnapshot() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void SetScoreSnapshot(ScenarioScoreSnapshot snapshot)
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioCompositionRoot.ResolveRuntime<IScenarioScoreSnapshotService>();
                if (service != null)
                    service.SetSnapshot(snapshot);
            }
            catch
            {
            }
        }

        public static void ClearScoreSnapshot()
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioCompositionRoot.ResolveRuntime<IScenarioScoreSnapshotService>();
                if (service != null)
                    service.ClearSnapshot();
            }
            catch
            {
            }
        }
    }
}

using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Core;
using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Sheltered lifecycle trigger adapter for deferred governed Harmony patch groups.
    /// </summary>
    internal static class ShelteredDeferredPatchTriggers
    {
        public static void ApplyMenuCritical(string trigger)
        {
            HarmonyBootstrap.ApplyDeferredPatchGroup(PatchStartupTiming.MenuCritical, trigger);
        }

        public static void ApplySaveFlowCritical(string trigger)
        {
            HarmonyBootstrap.ApplyDeferredPatchGroup(PatchStartupTiming.SaveFlowCritical, trigger);
            ShelteredApiRuntimeBootstrap.EnsureSaveProtectionPatches();
        }

        public static void ApplyGameplayDeferred(string trigger)
        {
            HarmonyBootstrap.ApplyDeferredPatchGroup(PatchStartupTiming.GameplayDeferred, trigger);
        }

        public static void ApplyEditorDeferred(string trigger)
        {
            if (!ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
                return;

            HarmonyBootstrap.ApplyDeferredPatchGroup(PatchStartupTiming.EditorDeferred, trigger);
            ScenarioAuthoringInputActions.EnsureRegistered();
            ScenarioAuthoringRuntimeDriver.EnsureCreated();
        }

        public static void ApplyDebugDeferred(string trigger)
        {
            if (!HarmonyBootstrap.ReadManagerBool("EnableDebugPatches", false))
            {
                MMLog.WriteDebug("[ShelteredDeferredPatchTriggers] Debug deferred patches are disabled.");
                return;
            }

            HarmonyBootstrap.ApplyDeferredPatchGroup(PatchStartupTiming.DebugDeferred, trigger);
        }
    }
}

using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredScenarioEditor.Shared;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Infrastructure.Unity;

namespace ShelteredScenarioEditor
{
    internal static class ScenarioEditorRuntime
    {
        private static readonly object Sync = new object();
        private static bool _initialized;

        public static bool Enabled
        {
            get { return ScenarioEditorFeature.Enabled; }
        }

        public static bool IsInitialized
        {
            get
            {
                lock (Sync)
                    return _initialized;
            }
        }

        public static void Initialize()
        {
            bool enabled;
            lock (Sync)
            {
                if (_initialized)
                    return;

                ScenarioEditorFeature.RegisterOptions();
                enabled = Enabled;
                if (enabled)
                {
                    ScenarioCompositionRoot.EnsureAuthoringInitialized();
                    ScenarioAuthoringRuntimeDriver.EnsureCreated();
                    HarmonyBootstrap.ApplyDeferredPatchGroup(
                        PatchStartupTiming.EditorDeferred,
                        "ShelteredScenarioEditor enabled bootstrap");
                }

                _initialized = true;
            }

            MMLog.WriteInfo(
                enabled
                    ? "[ShelteredScenarioEditor] Editor feature is enabled."
                    : "[ShelteredScenarioEditor] Editor feature is disabled; scenario runtime remains available through ShelteredAPI.");
        }
    }

    internal sealed class ScenarioEditorGameRuntimeBootstrap : IGameRuntimeBootstrap
    {
        public void Initialize()
        {
            ScenarioEditorRuntime.Initialize();
        }
    }
}

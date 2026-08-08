using ShelteredScenarioEditor.Core;
using ShelteredScenarioEditor.Shared;
using System;

namespace ShelteredScenarioEditor.Composition
{
    internal static class ScenarioCompositionRoot
    {
        private static readonly object Sync = new object();
        private static ServiceProvider _provider;

        public static void EnsureAuthoringInitialized()
        {
            if (!ScenarioEditorFeature.Enabled)
                throw new InvalidOperationException(
                    "The scenario editor composition graph cannot be created while ShelteredScenarioEditor.Enabled is false.");

            if (_provider != null)
                return;

            lock (Sync)
            {
                if (_provider != null)
                    return;
                if (!ScenarioEditorFeature.Enabled)
                    throw new InvalidOperationException(
                        "The scenario editor was disabled before its composition graph could be created.");

                ServiceCollection services = new ServiceCollection();
                services.AddScenarioAuthoringModule();
                services.AddScenarioPresentationModule();
                _provider = services.Build();
            }
        }

        public static T Resolve<T>() where T : class
        {
            EnsureAuthoringInitialized();
            return _provider.Get<T>();
        }

    }
}

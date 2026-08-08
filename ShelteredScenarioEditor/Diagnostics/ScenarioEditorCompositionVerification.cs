using System;
using System.Collections.Generic;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Core;

namespace ShelteredScenarioEditor.Diagnostics
{
    /// <summary>
    /// Executable regression fixture that resolves the editor's production service graph.
    /// </summary>
    internal static class ScenarioEditorCompositionVerification
    {
        public static string[] Run()
        {
            List<string> errors = new List<string>();
            try
            {
                ServiceCollection services = new ServiceCollection();
                services.AddScenarioAuthoringModule();
                services.AddScenarioPresentationModule();
                ServiceProvider provider = services.Build();

                Require(provider.Get<IScenarioEditorSessionStore>(), "scenario editor session store", errors);
                Require(provider.Get<IScenarioEditorService>(), "scenario editor service", errors);
                Require(provider.Get<IScenarioAuthoringBackend>(), "scenario authoring backend", errors);
                Require(provider.Get<ScenarioAuthoringBootstrapService>(), "scenario authoring bootstrap service", errors);
            }
            catch (Exception exception)
            {
                errors.Add("Editor composition graph failed to resolve: " + Describe(exception));
            }

            return errors.ToArray();
        }

        private static void Require(object service, string name, List<string> errors)
        {
            if (service == null)
                errors.Add("Editor composition returned null for " + name + ".");
        }

        private static string Describe(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
                current = current.InnerException;
            return current.GetType().FullName + ": " + current.Message;
        }
    }
}

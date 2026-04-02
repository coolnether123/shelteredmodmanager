using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Tabby
{
    /// <summary>
    /// Factory for the Tabby completion augmentation provider.
    /// </summary>
    public sealed class TabbyCompletionProviderFactory : ICompletionAugmentationProviderFactory
    {
        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.Tabby; }
        }

        public string DisplayName
        {
            get { return "Tabby"; }
        }

        public bool CanCreate(CortexSettings settings)
        {
            return settings != null &&
                settings.EnableTabbyCompletion &&
                (!string.IsNullOrEmpty(settings.TabbyServerUrl) || !string.IsNullOrEmpty(settings.OllamaModel));
        }

        public ICompletionAugmentationClient Create(CortexSettings settings, CompletionAugmentationProviderContext context, Action<string> log)
        {
            if (!CanCreate(settings))
            {
                return null;
            }

            var managedServer = new BundledTabbyServerController(log);
            string endpoint;
            if (!managedServer.TryResolveCompletionEndpoint(settings, context, out endpoint))
            {
                if (log != null && !string.IsNullOrEmpty(managedServer.LastError))
                {
                    log("[Cortex.Tabby] " + managedServer.LastError);
                }

                managedServer.Dispose();
                return null;
            }

            if (log != null)
            {
                log("[Cortex.Tabby] Using " +
                    (string.IsNullOrEmpty(settings.TabbyServerUrl) ? "bundled" : "external") +
                    " Tabby endpoint " + endpoint + ".");
            }

            var effectiveTimeoutMs = TabbyRuntimeSettings.GetEffectiveTimeoutMs(settings);
            return new TabbyCompletionClient(
                endpoint,
                settings.TabbyApiToken,
                effectiveTimeoutMs,
                log,
                string.IsNullOrEmpty(settings.TabbyServerUrl) ? (IDisposable)managedServer : null);
        }
    }
}

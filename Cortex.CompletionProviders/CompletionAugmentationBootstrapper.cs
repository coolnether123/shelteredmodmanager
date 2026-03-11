using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Ollama;
using Cortex.OpenRouter;
using Cortex.Tabby;

namespace Cortex.CompletionProviders
{
    /// <summary>
    /// Resolves the active completion augmentation provider from Cortex settings.
    /// The shell depends only on this registry and the generic provider contract.
    /// </summary>
    public static class CompletionAugmentationBootstrapper
    {
        private static readonly ICompletionAugmentationProviderFactory[] Factories =
        {
            new TabbyCompletionProviderFactory(),
            new OllamaCompletionProviderFactory(),
            new OpenRouterCompletionProviderFactory()
        };

        public static ICompletionAugmentationClient Create(CortexSettings settings, Action<string> log)
        {
            if (settings == null || !settings.EnableCompletionAugmentation)
            {
                if (log != null)
                {
                    log("[Cortex.Completion.Augmentation] Provider bootstrap skipped. SettingsPresent=" +
                        (settings != null) +
                        ", EnableCompletionAugmentation=" + (settings != null && settings.EnableCompletionAugmentation) + ".");
                }
                return null;
            }

            var providerId = string.IsNullOrEmpty(settings.CompletionAugmentationProviderId)
                ? CompletionAugmentationProviderIds.Tabby
                : settings.CompletionAugmentationProviderId;
            if (log != null)
            {
                log("[Cortex.Completion.Augmentation] Provider bootstrap starting. RequestedProvider=" +
                    providerId +
                    ", EnableTabby=" + settings.EnableTabbyCompletion +
                    ", TabbyUrl=" + (settings.TabbyServerUrl ?? string.Empty) +
                    ", OllamaModel=" + (settings.OllamaModel ?? string.Empty) +
                    ", OpenRouterModel=" + (settings.OpenRouterModel ?? string.Empty) + ".");
            }

            for (var i = 0; i < Factories.Length; i++)
            {
                var factory = Factories[i];
                if (factory == null ||
                    !string.Equals(factory.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!factory.CanCreate(settings))
                {
                    if (log != null)
                    {
                        log("[Cortex.Completion.Augmentation] Provider factory declined creation. Provider=" +
                            factory.ProviderId + ".");
                    }

                    return null;
                }

                var client = factory.Create(settings, log);
                if (log != null)
                {
                    log("[Cortex.Completion.Augmentation] Provider factory returned " +
                        (client != null ? "client" : "null") +
                        " for provider " + factory.ProviderId + ".");
                }

                return client;
            }

            if (log != null)
            {
                log("[Cortex.Completion.Augmentation] No provider factory matched requested provider " +
                    providerId + ".");
            }

            return null;
        }
    }
}

using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.OpenRouter
{
    public sealed class OpenRouterCompletionProviderFactory : ICompletionAugmentationProviderFactory
    {
        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.OpenRouter; }
        }

        public string DisplayName
        {
            get { return "OpenRouter"; }
        }

        public bool CanCreate(CortexSettings settings)
        {
            return settings != null &&
                !string.IsNullOrEmpty(settings.OpenRouterBaseUrl) &&
                !string.IsNullOrEmpty(settings.OpenRouterApiKey) &&
                !string.IsNullOrEmpty(settings.OpenRouterModel);
        }

        public ICompletionAugmentationClient Create(CortexSettings settings, CompletionAugmentationProviderContext context, Action<string> log)
        {
            if (!CanCreate(settings))
            {
                return null;
            }

            return new OpenRouterCompletionClient(settings, log);
        }
    }
}

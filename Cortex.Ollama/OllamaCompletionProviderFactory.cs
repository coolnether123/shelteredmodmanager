using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Ollama
{
    public sealed class OllamaCompletionProviderFactory : ICompletionAugmentationProviderFactory
    {
        public string ProviderId
        {
            get { return CompletionAugmentationProviderIds.Ollama; }
        }

        public string DisplayName
        {
            get { return "Ollama"; }
        }

        public bool CanCreate(CortexSettings settings)
        {
            return settings != null &&
                !string.IsNullOrEmpty(settings.OllamaServerUrl) &&
                !string.IsNullOrEmpty(settings.OllamaModel);
        }

        public ICompletionAugmentationClient Create(CortexSettings settings, Action<string> log)
        {
            if (!CanCreate(settings))
            {
                return null;
            }

            return new OllamaCompletionClient(settings, log);
        }
    }
}

using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Shell
{
    internal sealed class NullLanguageRuntimeService :
        ILanguageRuntimeControl,
        ILanguageRuntimeQuery,
        ILanguageEditorOperations
    {
        private LanguageRuntimeSnapshot _snapshot = CreateSnapshot(
            LanguageRuntimeLifecycleState.Disabled,
            LanguageRuntimeHealthState.NoProviders,
            "No language providers are registered.",
            "No language providers are registered.",
            0,
            new LanguageProviderDescriptor());

        public void Start(LanguageRuntimeConfiguration configuration)
        {
            ApplyConfiguration(configuration, false);
        }

        public void Reload(LanguageRuntimeConfiguration configuration)
        {
            ApplyConfiguration(configuration, true);
        }

        public void Advance()
        {
        }

        public void Shutdown()
        {
            _snapshot = CreateSnapshot(
                LanguageRuntimeLifecycleState.Disabled,
                _snapshot != null ? _snapshot.HealthState : LanguageRuntimeHealthState.NoProviders,
                _snapshot != null ? _snapshot.StatusMessage : string.Empty,
                _snapshot != null ? _snapshot.LastErrorSummary : string.Empty,
                _snapshot != null ? _snapshot.ActiveGeneration : 0,
                _snapshot != null ? CloneDescriptor(_snapshot.Provider) : new LanguageProviderDescriptor());
        }

        public LanguageRuntimeSnapshot GetSnapshot()
        {
            return CloneSnapshot(_snapshot);
        }

        public void DispatchDocumentAnalysis()
        {
        }

        public void DispatchHover()
        {
        }

        public void DispatchDefinition()
        {
        }

        public void DispatchCompletion()
        {
        }

        public void DispatchSignatureHelp()
        {
        }

        public void DispatchSemanticOperations()
        {
        }

        public void DispatchMethodInspectorCallHierarchy()
        {
        }

        private void ApplyConfiguration(LanguageRuntimeConfiguration configuration, bool isReload)
        {
            var generation = _snapshot != null ? _snapshot.ActiveGeneration : 0;
            if (isReload)
            {
                generation++;
            }

            var descriptor = BuildDescriptor(configuration);
            if (IsExplicitlyDisabled(configuration))
            {
                _snapshot = CreateSnapshot(
                    LanguageRuntimeLifecycleState.Disabled,
                    LanguageRuntimeHealthState.Healthy,
                    "Language runtime disabled by settings.",
                    string.Empty,
                    generation,
                    descriptor);
                return;
            }

            _snapshot = CreateSnapshot(
                LanguageRuntimeLifecycleState.Disabled,
                LanguageRuntimeHealthState.NoProviders,
                "No language provider is available for the current host.",
                "No provider session was created for " + (descriptor.DisplayName ?? descriptor.ProviderId ?? string.Empty) + ".",
                generation,
                descriptor);
        }

        private static bool IsExplicitlyDisabled(LanguageRuntimeConfiguration configuration)
        {
            var providerId = configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty;
            return string.Equals(providerId, LanguageRuntimeConstants.NoneProviderId, StringComparison.OrdinalIgnoreCase);
        }

        private static LanguageProviderDescriptor BuildDescriptor(LanguageRuntimeConfiguration configuration)
        {
            var descriptor = new LanguageProviderDescriptor();
            descriptor.ProviderId = configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty;
            descriptor.DisplayName = string.IsNullOrEmpty(descriptor.ProviderId) ? "Language Runtime" : descriptor.ProviderId;
            descriptor.Version = string.Empty;
            descriptor.Source = "shell";
            return descriptor;
        }

        private static LanguageRuntimeSnapshot CreateSnapshot(
            LanguageRuntimeLifecycleState lifecycleState,
            LanguageRuntimeHealthState healthState,
            string statusMessage,
            string lastErrorSummary,
            int activeGeneration,
            LanguageProviderDescriptor descriptor)
        {
            return new LanguageRuntimeSnapshot
            {
                LifecycleState = lifecycleState,
                HealthState = healthState,
                StatusMessage = statusMessage ?? string.Empty,
                LastErrorSummary = lastErrorSummary ?? string.Empty,
                ActiveGeneration = activeGeneration,
                Provider = descriptor != null ? CloneDescriptor(descriptor) : new LanguageProviderDescriptor(),
                Capabilities = new LanguageCapabilitiesSnapshot()
            };
        }

        private static LanguageRuntimeSnapshot CloneSnapshot(LanguageRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new LanguageRuntimeSnapshot();
            }

            return new LanguageRuntimeSnapshot
            {
                Provider = CloneDescriptor(snapshot.Provider),
                LifecycleState = snapshot.LifecycleState,
                HealthState = snapshot.HealthState,
                Capabilities = CloneCapabilities(snapshot.Capabilities),
                StatusMessage = snapshot.StatusMessage ?? string.Empty,
                LastErrorSummary = snapshot.LastErrorSummary ?? string.Empty,
                ActiveGeneration = snapshot.ActiveGeneration
            };
        }

        private static LanguageProviderDescriptor CloneDescriptor(LanguageProviderDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return new LanguageProviderDescriptor();
            }

            return new LanguageProviderDescriptor
            {
                ProviderId = descriptor.ProviderId ?? string.Empty,
                DisplayName = descriptor.DisplayName ?? string.Empty,
                Version = descriptor.Version ?? string.Empty,
                Source = descriptor.Source ?? string.Empty
            };
        }

        private static LanguageCapabilitiesSnapshot CloneCapabilities(LanguageCapabilitiesSnapshot capabilities)
        {
            if (capabilities == null)
            {
                return new LanguageCapabilitiesSnapshot();
            }

            var copy = new LanguageCapabilitiesSnapshot
            {
                SupportsAnalysis = capabilities.SupportsAnalysis,
                SupportsDiagnostics = capabilities.SupportsDiagnostics,
                SupportsSemanticTokens = capabilities.SupportsSemanticTokens,
                SupportsHover = capabilities.SupportsHover,
                SupportsDefinition = capabilities.SupportsDefinition,
                SupportsCompletion = capabilities.SupportsCompletion,
                SupportsSignatureHelp = capabilities.SupportsSignatureHelp,
                SupportsRename = capabilities.SupportsRename,
                SupportsReferences = capabilities.SupportsReferences,
                SupportsImplementations = capabilities.SupportsImplementations,
                SupportsBaseSymbols = capabilities.SupportsBaseSymbols,
                SupportsCallHierarchy = capabilities.SupportsCallHierarchy,
                SupportsValueSource = capabilities.SupportsValueSource,
                SupportsDocumentTransforms = capabilities.SupportsDocumentTransforms,
                CapabilityIds = capabilities.CapabilityIds != null
                    ? (string[])capabilities.CapabilityIds.Clone()
                    : new string[0]
            };
            return copy;
        }
    }
}

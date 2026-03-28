using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    public static class LanguageRuntimeConstants
    {
        public const string NoneProviderId = "none";
    }

    public enum LanguageRuntimeLifecycleState
    {
        Disabled = 0,
        Starting = 1,
        Running = 2,
        Reloading = 3,
        Stopping = 4
    }

    public enum LanguageRuntimeHealthState
    {
        Healthy = 0,
        Degraded = 1,
        Faulted = 2,
        Unavailable = 3,
        NoProviders = 4
    }

    public enum LanguageRuntimeMessageKind
    {
        LifecycleEvent = 0,
        CapabilitySnapshot = 1,
        RequestResult = 2,
        RequestFailure = 3,
        ProviderFault = 4,
        Diagnostic = 5
    }

    [Serializable]
    public sealed class LanguageProviderDescriptor
    {
        public string ProviderId;
        public string DisplayName;
        public string Version;
        public string Source;

        public LanguageProviderDescriptor()
        {
            ProviderId = string.Empty;
            DisplayName = string.Empty;
            Version = string.Empty;
            Source = string.Empty;
        }
    }

    [Serializable]
    public sealed class LanguageCapabilitiesSnapshot
    {
        public bool SupportsAnalysis;
        public bool SupportsDiagnostics;
        public bool SupportsSemanticTokens;
        public bool SupportsHover;
        public bool SupportsDefinition;
        public bool SupportsCompletion;
        public bool SupportsSignatureHelp;
        public bool SupportsRename;
        public bool SupportsReferences;
        public bool SupportsImplementations;
        public bool SupportsBaseSymbols;
        public bool SupportsCallHierarchy;
        public bool SupportsValueSource;
        public bool SupportsDocumentTransforms;
        public string[] CapabilityIds;

        public LanguageCapabilitiesSnapshot()
        {
            CapabilityIds = new string[0];
        }
    }

    [Serializable]
    public sealed class LanguageRuntimeSnapshot
    {
        public LanguageProviderDescriptor Provider;
        public LanguageRuntimeLifecycleState LifecycleState;
        public LanguageRuntimeHealthState HealthState;
        public LanguageCapabilitiesSnapshot Capabilities;
        public string StatusMessage;
        public string LastErrorSummary;
        public int ActiveGeneration;

        public LanguageRuntimeSnapshot()
        {
            Provider = new LanguageProviderDescriptor();
            Capabilities = new LanguageCapabilitiesSnapshot();
            StatusMessage = string.Empty;
            LastErrorSummary = string.Empty;
        }
    }

    [Serializable]
    public sealed class LanguageRuntimeConfiguration
    {
        public string ProviderId;
        public string HostBinPath;
        public CortexSettings Settings;

        public LanguageRuntimeConfiguration()
        {
            ProviderId = string.Empty;
            HostBinPath = string.Empty;
            Settings = null;
        }
    }

    [Serializable]
    public sealed class LanguageRuntimeMessage
    {
        public LanguageRuntimeMessageKind Kind;
        public int Generation;
        public string RequestId;
        public int DocumentVersion;
        public string Message;
        public LanguageRuntimeLifecycleState LifecycleState;
        public LanguageRuntimeHealthState HealthState;
        public LanguageCapabilitiesSnapshot Capabilities;
        public LanguageServiceEnvelope Envelope;

        public LanguageRuntimeMessage()
        {
            RequestId = string.Empty;
            Message = string.Empty;
            Capabilities = null;
            Envelope = null;
        }
    }
}

using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;

namespace Cortex
{
    internal sealed class CortexShellLanguageRuntimeState
    {
        public string LastAnalyzedDocumentFingerprint = string.Empty;
        public string PendingLanguageAnalysisFingerprint = string.Empty;
        public bool ServiceReady;
        public bool ServiceInitializing;
        public bool AnalysisInFlight;
        public bool HoverInFlight;
        public bool DefinitionInFlight;
        public bool CompletionInFlight;
        public bool SignatureHelpInFlight;
        public bool SemanticOperationInFlight;
        public bool MethodInspectorRelationshipsInFlight;
        public string InitializeRequestId = string.Empty;
        public string StatusRequestId = string.Empty;
        public DocumentLanguageAnalysisRequestState PendingAnalysis;
        public PendingLanguageHoverRequest PendingHover;
        public PendingLanguageDefinitionRequest PendingDefinition;
        public DocumentLanguageCompletionRequestState PendingCompletion;
        public PendingLanguageSignatureHelpRequest PendingSignatureHelp;
        public PendingSemanticOperationRequest PendingSemanticOperation;
        public PendingMethodInspectorRelationshipsRequest PendingMethodInspectorRelationships;
        public int ServiceGeneration;
        public DateTime LastAnalysisRequestUtc = DateTime.MinValue;
        public DateTime InitializeQueuedUtc = DateTime.MinValue;
        public DateTime LastInitializationProgressLogUtc = DateTime.MinValue;
        public string ServiceConfigurationFingerprint = string.Empty;
        public LanguageServiceInitializeRequest InitializeRequest;
        public bool LastStatusSucceeded;
        public string RuntimeStatusMessage = string.Empty;
        public string ProviderDisplayName = string.Empty;
        public string ProviderVersion = string.Empty;
        public int CachedProjectCount;
        public string[] CapabilityIds = new string[0];
    }
}

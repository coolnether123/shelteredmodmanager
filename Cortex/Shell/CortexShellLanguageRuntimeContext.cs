using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Analysis;
using Cortex.Services.Semantics.Completion;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Semantics.Requests;
using Cortex.Services.Semantics.SignatureHelp;

namespace Cortex
{
    internal sealed class CortexShellLanguageRuntimeContext
    {
        private readonly Func<ILanguageProviderSession> _languageServiceClientAccessor;
        private readonly Func<ICortexNavigationService> _navigationServiceAccessor;
        private readonly Func<bool> _completionAugmentationInFlightAccessor;
        private readonly Action _ensureLanguageServiceStarted;
        private readonly Action _processCompletionAugmentationResponses;
        private readonly Action _logLanguageInitializationProgress;
        private readonly Action _resetLanguageTrackingForInactiveDocument;
        private readonly Func<string, DocumentSession> _findOpenDocument;
        private readonly Func<string, CortexProjectDefinition> _resolveProjectForDocument;
        private readonly Func<CortexSettings, CortexProjectDefinition, string[]> _buildLanguageSourceRoots;
        private readonly Func<DocumentSession, string> _buildLanguageFingerprint;
        private readonly Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest> _buildCompletionAugmentationRequest;
        private readonly Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest, LanguageServiceCompletionResponse, bool> _tryQueueCompletionAugmentation;
        private readonly Action _dispatchDeferredCompletionAugmentation;

        public CortexShellLanguageRuntimeContext(
            CortexShellState state,
            CortexShellLanguageRuntimeState runtimeState,
            IDocumentLanguageAnalysisService documentLanguageAnalysisService,
            IEditorLanguageRequestFactory languageRequestFactory,
            IEditorCompletionService editorCompletionService,
            IEditorSignatureHelpService editorSignatureHelpService,
            IEditorContextService editorContextService,
            Func<ILanguageProviderSession> languageServiceClientAccessor,
            Func<ICortexNavigationService> navigationServiceAccessor,
            Func<bool> completionAugmentationInFlightAccessor,
            double languageAnalysisDebounceMs,
            Action ensureLanguageServiceStarted,
            Action processCompletionAugmentationResponses,
            Action logLanguageInitializationProgress,
            Action resetLanguageTrackingForInactiveDocument,
            Func<string, DocumentSession> findOpenDocument,
            Func<string, CortexProjectDefinition> resolveProjectForDocument,
            Func<CortexSettings, CortexProjectDefinition, string[]> buildLanguageSourceRoots,
            Func<DocumentSession, string> buildLanguageFingerprint,
            Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest> buildCompletionAugmentationRequest,
            Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest, LanguageServiceCompletionResponse, bool> tryQueueCompletionAugmentation,
            Action dispatchDeferredCompletionAugmentation)
        {
            State = state;
            RuntimeState = runtimeState;
            DocumentLanguageAnalysisService = documentLanguageAnalysisService;
            LanguageRequestFactory = languageRequestFactory;
            EditorCompletionService = editorCompletionService;
            EditorSignatureHelpService = editorSignatureHelpService;
            EditorContextService = editorContextService;
            _languageServiceClientAccessor = languageServiceClientAccessor;
            _navigationServiceAccessor = navigationServiceAccessor;
            _completionAugmentationInFlightAccessor = completionAugmentationInFlightAccessor;
            LanguageAnalysisDebounceMs = languageAnalysisDebounceMs;
            _ensureLanguageServiceStarted = ensureLanguageServiceStarted;
            _processCompletionAugmentationResponses = processCompletionAugmentationResponses;
            _logLanguageInitializationProgress = logLanguageInitializationProgress;
            _resetLanguageTrackingForInactiveDocument = resetLanguageTrackingForInactiveDocument;
            _findOpenDocument = findOpenDocument;
            _resolveProjectForDocument = resolveProjectForDocument;
            _buildLanguageSourceRoots = buildLanguageSourceRoots;
            _buildLanguageFingerprint = buildLanguageFingerprint;
            _buildCompletionAugmentationRequest = buildCompletionAugmentationRequest;
            _tryQueueCompletionAugmentation = tryQueueCompletionAugmentation;
            _dispatchDeferredCompletionAugmentation = dispatchDeferredCompletionAugmentation;
        }

        public CortexShellState State { get; private set; }

        public CortexShellLanguageRuntimeState RuntimeState { get; private set; }

        public IDocumentLanguageAnalysisService DocumentLanguageAnalysisService { get; private set; }

        public IEditorLanguageRequestFactory LanguageRequestFactory { get; private set; }

        public IEditorCompletionService EditorCompletionService { get; private set; }

        public IEditorSignatureHelpService EditorSignatureHelpService { get; private set; }

        public IEditorContextService EditorContextService { get; private set; }

        public ILanguageProviderSession LanguageServiceClient
        {
            get { return _languageServiceClientAccessor != null ? _languageServiceClientAccessor() : null; }
        }

        public ICortexNavigationService NavigationService
        {
            get { return _navigationServiceAccessor != null ? _navigationServiceAccessor() : null; }
        }

        public bool CompletionAugmentationInFlight
        {
            get { return _completionAugmentationInFlightAccessor != null && _completionAugmentationInFlightAccessor(); }
        }

        public double LanguageAnalysisDebounceMs { get; private set; }

        public bool IsLanguageServiceReadyForDocumentWork
        {
            get { return LanguageServiceClient != null && RuntimeState.ServiceReady && !RuntimeState.ServiceInitializing; }
        }

        public bool HasLanguageCapability(string capability)
        {
            var runtime = State != null ? State.LanguageRuntime : null;
            var capabilities = runtime != null && runtime.Capabilities != null
                ? runtime.Capabilities.CapabilityIds
                : null;
            if (capabilities == null || string.IsNullOrEmpty(capability))
            {
                return false;
            }

            for (var i = 0; i < capabilities.Length; i++)
            {
                if (string.Equals(capabilities[i], capability, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void EnsureLanguageServiceStarted()
        {
            if (_ensureLanguageServiceStarted != null)
            {
                _ensureLanguageServiceStarted();
            }
        }

        public void ProcessCompletionAugmentationResponses()
        {
            if (_processCompletionAugmentationResponses != null)
            {
                _processCompletionAugmentationResponses();
            }
        }

        public void LogLanguageInitializationProgress()
        {
            if (_logLanguageInitializationProgress != null)
            {
                _logLanguageInitializationProgress();
            }
        }

        public void ResetLanguageTrackingForInactiveDocument()
        {
            if (_resetLanguageTrackingForInactiveDocument != null)
            {
                _resetLanguageTrackingForInactiveDocument();
            }
        }

        public DocumentSession FindOpenDocument(string filePath)
        {
            return _findOpenDocument != null ? _findOpenDocument(filePath) : null;
        }

        public CortexProjectDefinition ResolveProjectForDocument(string filePath)
        {
            return _resolveProjectForDocument != null ? _resolveProjectForDocument(filePath) : null;
        }

        public string[] BuildLanguageSourceRoots(CortexSettings settings, CortexProjectDefinition project)
        {
            return _buildLanguageSourceRoots != null ? _buildLanguageSourceRoots(settings, project) : new string[0];
        }

        public string BuildLanguageFingerprint(DocumentSession session)
        {
            return _buildLanguageFingerprint != null ? _buildLanguageFingerprint(session) : string.Empty;
        }

        public CompletionAugmentationRequest BuildCompletionAugmentationRequest(DocumentSession session, DocumentLanguageCompletionRequestState pending)
        {
            return _buildCompletionAugmentationRequest != null ? _buildCompletionAugmentationRequest(session, pending) : null;
        }

        public bool TryQueueCompletionAugmentation(DocumentSession session, DocumentLanguageCompletionRequestState pending, CompletionAugmentationRequest request, LanguageServiceCompletionResponse primaryResponse)
        {
            return _tryQueueCompletionAugmentation != null && _tryQueueCompletionAugmentation(session, pending, request, primaryResponse);
        }

        public void DispatchDeferredCompletionAugmentation()
        {
            if (_dispatchDeferredCompletionAugmentation != null)
            {
                _dispatchDeferredCompletionAugmentation();
            }
        }
    }
}

using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;

namespace Cortex
{
    internal sealed class CortexShellLanguageRuntimeContext
    {
        private readonly Func<ILanguageServiceClient> _languageServiceClientAccessor;
        private readonly Func<CortexNavigationService> _navigationServiceAccessor;
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
            DocumentLanguageAnalysisService documentLanguageAnalysisService,
            DocumentLanguageInteractionService documentLanguageInteractionService,
            EditorCompletionService editorCompletionService,
            Func<ILanguageServiceClient> languageServiceClientAccessor,
            Func<CortexNavigationService> navigationServiceAccessor,
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
            DocumentLanguageInteractionService = documentLanguageInteractionService;
            EditorCompletionService = editorCompletionService;
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

        public DocumentLanguageAnalysisService DocumentLanguageAnalysisService { get; private set; }

        public DocumentLanguageInteractionService DocumentLanguageInteractionService { get; private set; }

        public EditorCompletionService EditorCompletionService { get; private set; }

        public ILanguageServiceClient LanguageServiceClient
        {
            get { return _languageServiceClientAccessor != null ? _languageServiceClientAccessor() : null; }
        }

        public CortexNavigationService NavigationService
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

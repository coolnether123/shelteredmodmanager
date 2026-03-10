using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services;
using GameModding.Shared.Serialization;
using ModAPI.Core;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private ILanguageServiceClient _languageServiceClient;
        private readonly DocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly DocumentLanguageInteractionService _documentLanguageInteractionService = new DocumentLanguageInteractionService();
        private LanguageServiceInitializeRequest _languageServiceInitializeRequest;
        private string _lastAnalyzedDocumentFingerprint = string.Empty;
        private string _pendingLanguageAnalysisFingerprint = string.Empty;
        private bool _languageServiceReady;
        private bool _languageServiceInitializing;
        private bool _languageAnalysisInFlight;
        private bool _languageHoverInFlight;
        private bool _languageDefinitionInFlight;
        private bool _languageCompletionInFlight;
        private string _languageInitializeRequestId = string.Empty;
        private string _languageStatusRequestId = string.Empty;
        private DocumentLanguageAnalysisRequestState _pendingLanguageAnalysis;
        private PendingLanguageHoverRequest _pendingLanguageHover;
        private PendingLanguageDefinitionRequest _pendingLanguageDefinition;
        private DocumentLanguageCompletionRequestState _pendingLanguageCompletion;
        private int _languageServiceGeneration;
        private DateTime _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
        private DateTime _languageInitializeQueuedUtc = DateTime.MinValue;
        private DateTime _lastLanguageInitializationProgressLogUtc = DateTime.MinValue;
        private string _languageServiceConfigurationFingerprint = string.Empty;
        private const double LanguageAnalysisDebounceMs = 280d;

        private void InitializeLanguageService(string smmBin, CortexSettings settings)
        {
            if (settings == null || !settings.EnableRoslynLanguageService)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Language service disabled by settings. ExistingClient=" + (_languageServiceClient != null) + ".");
                ShutdownLanguageService();
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = "disabled",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                return;
            }

            var workerPath = ResolveLanguageWorkerPath(settings, smmBin);
            if (string.IsNullOrEmpty(workerPath))
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Language service worker path could not be resolved. ExistingClient=" + (_languageServiceClient != null) + ".");
                ShutdownLanguageService();
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = "worker not found",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                _state.Diagnostics.Add(_state.LanguageServiceStatus.StatusMessage);
                return;
            }

            var initializeRequest = BuildLanguageInitializeRequest();
            var configurationFingerprint = BuildLanguageServiceConfigurationFingerprint(workerPath, settings, initializeRequest);
            if (_languageServiceClient != null &&
                string.Equals(_languageServiceConfigurationFingerprint, configurationFingerprint, StringComparison.Ordinal))
            {
                _languageServiceInitializeRequest = initializeRequest;
                MMLog.WriteInfo("[Cortex.Roslyn] Reusing existing language service configuration. Generation=" + _languageServiceGeneration +
                    ", Ready=" + _languageServiceReady +
                    ", Initializing=" + _languageServiceInitializing +
                    ", WorkspaceRoot=" + (initializeRequest.WorkspaceRootPath ?? string.Empty) +
                    ", Projects=" + (initializeRequest.ProjectFilePaths != null ? initializeRequest.ProjectFilePaths.Length : 0) +
                    ", SourceRoots=" + (initializeRequest.SourceRoots != null ? initializeRequest.SourceRoots.Length : 0) + ".");
                return;
            }

            if (_languageServiceClient != null)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Reinitializing language service due to configuration change. PreviousGeneration=" + _languageServiceGeneration +
                    ", PreviousFingerprintLength=" + (_languageServiceConfigurationFingerprint ?? string.Empty).Length +
                    ", NewFingerprintLength=" + (configurationFingerprint ?? string.Empty).Length + ".");
            }

            ShutdownLanguageService();

            MMLog.WriteInfo("[Cortex.Roslyn] Resolved worker path: " + workerPath);
            _languageServiceClient = new RoslynLanguageServiceClient(
                workerPath,
                settings.RoslynServiceTimeoutMs,
                delegate(string message) { MMLog.WriteInfo(message); });
            _languageServiceInitializeRequest = initializeRequest;
            _lastAnalyzedDocumentFingerprint = string.Empty;
            _pendingLanguageAnalysisFingerprint = string.Empty;
            _languageServiceReady = false;
            _languageServiceInitializing = false;
            _languageAnalysisInFlight = false;
            _languageHoverInFlight = false;
            _languageDefinitionInFlight = false;
            _languageCompletionInFlight = false;
            _languageInitializeRequestId = string.Empty;
            _languageStatusRequestId = string.Empty;
            _pendingLanguageAnalysis = null;
            _pendingLanguageHover = null;
            _pendingLanguageDefinition = null;
            _pendingLanguageCompletion = null;
            _state.Editor.ActiveCompletionKey = string.Empty;
            _state.Editor.ActiveCompletionResponse = null;
            _state.Editor.RequestedCompletionKey = string.Empty;
            _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
            _languageInitializeQueuedUtc = DateTime.MinValue;
            _lastLanguageInitializationProgressLogUtc = DateTime.MinValue;
            _languageServiceConfigurationFingerprint = configurationFingerprint;
            Interlocked.Increment(ref _languageServiceGeneration);

            _state.LanguageServiceStatus = new LanguageServiceStatusResponse
            {
                Success = true,
                StatusMessage = "standby",
                IsRunning = false,
                Capabilities = new string[0],
                LoadedProjectPaths = new string[0]
            };
        }

        private void EnsureLanguageServiceStarted()
        {
            if (_languageServiceClient == null || _languageServiceReady || _languageServiceInitializing || !string.IsNullOrEmpty(_languageInitializeRequestId))
            {
                return;
            }

            if (!_visible)
            {
                return;
            }

            var generation = _languageServiceGeneration;
            _languageServiceInitializing = true;
            MMLog.WriteInfo("[Cortex.Roslyn] Starting language service worker in background. Generation=" + generation +
                ", FingerprintLength=" + (_languageServiceConfigurationFingerprint ?? string.Empty).Length +
                ", WorkspaceRoot=" + (_languageServiceInitializeRequest != null ? (_languageServiceInitializeRequest.WorkspaceRootPath ?? string.Empty) : string.Empty) +
                ", Projects=" + (_languageServiceInitializeRequest != null && _languageServiceInitializeRequest.ProjectFilePaths != null ? _languageServiceInitializeRequest.ProjectFilePaths.Length : 0) +
                ", SourceRoots=" + (_languageServiceInitializeRequest != null && _languageServiceInitializeRequest.SourceRoots != null ? _languageServiceInitializeRequest.SourceRoots.Length : 0) + ".");
            _state.LanguageServiceStatus = new LanguageServiceStatusResponse
            {
                Success = true,
                StatusMessage = "starting",
                IsRunning = false,
                Capabilities = new string[0],
                LoadedProjectPaths = new string[0]
            };
            _languageInitializeRequestId = _languageServiceClient.QueueInitialize(_languageServiceInitializeRequest ?? BuildLanguageInitializeRequest());
            if (string.IsNullOrEmpty(_languageInitializeRequestId))
            {
                _languageServiceInitializing = false;
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = string.IsNullOrEmpty(_languageServiceClient.LastError) ? "startup failed" : _languageServiceClient.LastError,
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                _state.Diagnostics.Add("Roslyn worker failed to queue initialization: " + _state.LanguageServiceStatus.StatusMessage);
                MMLog.WriteWarning("[Cortex.Roslyn] Worker initialize queue failed. Generation=" + generation +
                    ", Error=" + _state.LanguageServiceStatus.StatusMessage + ".");
                return;
            }

            _languageInitializeQueuedUtc = DateTime.UtcNow;
            _lastLanguageInitializationProgressLogUtc = DateTime.UtcNow;
            MMLog.WriteInfo("[Cortex.Roslyn] Worker initialize queued. Generation=" + generation +
                ", RequestId=" + _languageInitializeRequestId + ".");
        }

        private void ShutdownLanguageService()
        {
            var hadClient = _languageServiceClient != null;
            if (hadClient || _languageServiceReady || _languageServiceInitializing || _languageAnalysisInFlight || _languageHoverInFlight || _languageDefinitionInFlight)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Shutting down language service. HadClient=" + hadClient +
                    ", Ready=" + _languageServiceReady +
                    ", Initializing=" + _languageServiceInitializing +
                    ", AnalysisInFlight=" + _languageAnalysisInFlight +
                    ", HoverInFlight=" + _languageHoverInFlight +
                    ", DefinitionInFlight=" + _languageDefinitionInFlight +
                    ", CompletionInFlight=" + _languageCompletionInFlight +
                    ", Generation=" + _languageServiceGeneration + ".");
            }

            Interlocked.Increment(ref _languageServiceGeneration);
            var disposable = _languageServiceClient as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            _languageServiceClient = null;
            _languageServiceInitializeRequest = null;
            _lastAnalyzedDocumentFingerprint = string.Empty;
            _pendingLanguageAnalysisFingerprint = string.Empty;
            _languageServiceReady = false;
            _languageServiceInitializing = false;
            _languageAnalysisInFlight = false;
            _languageHoverInFlight = false;
            _languageDefinitionInFlight = false;
            _languageCompletionInFlight = false;
            _languageInitializeRequestId = string.Empty;
            _languageStatusRequestId = string.Empty;
            _pendingLanguageAnalysis = null;
            _pendingLanguageHover = null;
            _pendingLanguageDefinition = null;
            _pendingLanguageCompletion = null;
            _state.Editor.ActiveCompletionKey = string.Empty;
            _state.Editor.ActiveCompletionResponse = null;
            _state.Editor.RequestedCompletionKey = string.Empty;
            _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
            _languageInitializeQueuedUtc = DateTime.MinValue;
            _lastLanguageInitializationProgressLogUtc = DateTime.MinValue;
            _languageServiceConfigurationFingerprint = string.Empty;
        }

        private void UpdateLanguageService()
        {
            EnsureLanguageServiceStarted();
            ProcessLanguageResponses();
            LogLanguageInitializationProgress();
            if (_languageServiceClient == null)
            {
                return;
            }

            if (!_languageServiceReady || _languageServiceInitializing)
            {
                return;
            }

            UpdateLanguageHover();
            UpdateLanguageDefinition();
            UpdateLanguageCompletion();

            var active = _state.Documents.ActiveDocument;
            if (active == null || string.IsNullOrEmpty(active.FilePath))
            {
                _lastAnalyzedDocumentFingerprint = string.Empty;
                _pendingLanguageAnalysisFingerprint = string.Empty;
                _state.Editor.ActiveCompletionKey = string.Empty;
                _state.Editor.ActiveCompletionResponse = null;
                return;
            }

            _documentLanguageAnalysisService.TryRestoreFromRecentCache(active, null);
            var fingerprint = BuildLanguageFingerprint(active);
            var needsClassifications = active.LastLanguageClassificationVersion != active.TextVersion;
            var needsDiagnostics = active.LastLanguageDiagnosticVersion != active.TextVersion;
            if (!needsClassifications && !needsDiagnostics)
            {
                _lastAnalyzedDocumentFingerprint = fingerprint;
                return;
            }

            var includeClassifications = needsClassifications;
            var includeDiagnostics = !needsClassifications && needsDiagnostics;
            var analysisWorkKey = _documentLanguageAnalysisService.BuildAnalysisWorkKey(fingerprint, includeDiagnostics, includeClassifications);
            if (_languageAnalysisInFlight || string.Equals(analysisWorkKey, _pendingLanguageAnalysisFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            if ((DateTime.UtcNow - active.LastTextMutationUtc).TotalMilliseconds < LanguageAnalysisDebounceMs ||
                (DateTime.UtcNow - _lastLanguageAnalysisRequestUtc).TotalMilliseconds < 100d)
            {
                return;
            }

            var classificationRange = includeClassifications ? _documentLanguageAnalysisService.BuildIncrementalClassificationRange(active) : null;
            var project = ResolveProjectForDocument(active.FilePath);
            var request = _documentLanguageAnalysisService.BuildDocumentRequest(
                active,
                _state.Settings,
                project,
                BuildLanguageSourceRoots(_state.Settings, project),
                includeDiagnostics,
                includeClassifications,
                classificationRange);
            var generation = _languageServiceGeneration;
            var client = _languageServiceClient;
            _languageAnalysisInFlight = true;
            _pendingLanguageAnalysisFingerprint = analysisWorkKey;
            _lastLanguageAnalysisRequestUtc = DateTime.UtcNow;
            var requestId = client != null ? client.QueueAnalyzeDocument(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageAnalysisInFlight = false;
                _pendingLanguageAnalysisFingerprint = string.Empty;
                return;
            }

            _pendingLanguageAnalysis = new DocumentLanguageAnalysisRequestState
            {
                RequestId = requestId,
                Generation = generation,
                Fingerprint = fingerprint,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                IncludeDiagnostics = includeDiagnostics,
                IncludeClassifications = includeClassifications,
                IsPartialClassification = classificationRange != null,
                OldClassificationStart = classificationRange != null ? classificationRange.OldSpanStart : 0,
                OldClassificationLength = classificationRange != null ? classificationRange.OldSpanLength : 0,
                NewClassificationStart = classificationRange != null ? classificationRange.NewSpanStart : 0,
                NewClassificationLength = classificationRange != null ? classificationRange.NewSpanLength : 0
            };
        }

        private void UpdateLanguageHover()
        {
            if (_languageHoverInFlight || _state.Editor == null)
            {
                return;
            }

            var requestKey = _state.Editor.RequestedHoverKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey) || string.Equals(requestKey, _state.Editor.ActiveHoverKey, StringComparison.Ordinal))
            {
                return;
            }

            var session = FindOpenDocument(_state.Editor.RequestedHoverDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = ResolveProjectForDocument(session.FilePath);
            var sourceRoots = BuildLanguageSourceRoots(_state.Settings, project);
            var request = _documentLanguageInteractionService.BuildHoverRequest(
                session,
                _state.Settings,
                project,
                sourceRoots,
                _state.Editor.RequestedHoverLine,
                _state.Editor.RequestedHoverColumn,
                _state.Editor.RequestedHoverAbsolutePosition);
            var client = _languageServiceClient;
            var generation = _languageServiceGeneration;
            var hoverKey = requestKey;
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            _languageHoverInFlight = true;
            MMLog.WriteInfo("[Cortex.Roslyn] Queueing hover for " +
                (_state.Editor.RequestedHoverTokenText ?? string.Empty) +
                " @ " + _state.Editor.RequestedHoverLine + ":" + _state.Editor.RequestedHoverColumn +
                " in " + Path.GetFileName(requestDocumentPath) + ".");
            var requestId = client != null ? client.QueueHover(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageHoverInFlight = false;
                MMLog.WriteWarning("[Cortex.Roslyn] Failed to queue hover for " +
                    (_state.Editor.RequestedHoverTokenText ?? string.Empty) +
                    ": " + (_languageServiceClient != null ? _languageServiceClient.LastError : "Roslyn client was not available."));
                return;
            }

            _pendingLanguageHover = new PendingLanguageHoverRequest
            {
                RequestId = requestId,
                Generation = generation,
                HoverKey = hoverKey,
                DocumentPath = requestDocumentPath,
                DocumentVersion = requestDocumentVersion
            };
        }

        private void UpdateLanguageDefinition()
        {
            if (_languageDefinitionInFlight || _state.Editor == null)
            {
                return;
            }

            var requestKey = _state.Editor.RequestedDefinitionKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = FindOpenDocument(_state.Editor.RequestedDefinitionDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = ResolveProjectForDocument(session.FilePath);
            var sourceRoots = BuildLanguageSourceRoots(_state.Settings, project);
            var request = _documentLanguageInteractionService.BuildDefinitionRequest(
                session,
                _state.Settings,
                project,
                sourceRoots,
                _state.Editor.RequestedDefinitionLine,
                _state.Editor.RequestedDefinitionColumn,
                _state.Editor.RequestedDefinitionAbsolutePosition);
            var client = _languageServiceClient;
            var generation = _languageServiceGeneration;
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            _languageDefinitionInFlight = true;
            _state.Editor.RequestedDefinitionKey = string.Empty;
            var requestId = client != null ? client.QueueGoToDefinition(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageDefinitionInFlight = false;
                _state.StatusMessage = "Definition lookup failed: " + (_languageServiceClient != null ? _languageServiceClient.LastError : "Roslyn client was not available.");
                return;
            }

            _pendingLanguageDefinition = new PendingLanguageDefinitionRequest
            {
                RequestId = requestId,
                Generation = generation,
                DocumentPath = requestDocumentPath,
                DocumentVersion = requestDocumentVersion,
                TokenText = _state.Editor.RequestedDefinitionTokenText ?? string.Empty
            };
        }

        private void UpdateLanguageCompletion()
        {
            if (_languageCompletionInFlight || _state.Editor == null)
            {
                return;
            }

            var requestKey = _state.Editor.RequestedCompletionKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey) ||
                string.Equals(requestKey, _state.Editor.ActiveCompletionKey ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            var session = FindOpenDocument(_state.Editor.RequestedCompletionDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = ResolveProjectForDocument(session.FilePath);
            var sourceRoots = BuildLanguageSourceRoots(_state.Settings, project);
            var request = _documentLanguageInteractionService.BuildCompletionRequest(
                session,
                _state.Settings,
                project,
                sourceRoots,
                _state.Editor.RequestedCompletionLine,
                _state.Editor.RequestedCompletionColumn,
                _state.Editor.RequestedCompletionAbsolutePosition,
                _state.Editor.RequestedCompletionExplicit,
                _state.Editor.RequestedCompletionTriggerCharacter);
            var client = _languageServiceClient;
            var generation = _languageServiceGeneration;
            _languageCompletionInFlight = true;
            var requestId = client != null ? client.QueueCompletion(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageCompletionInFlight = false;
                return;
            }

            _pendingLanguageCompletion = new DocumentLanguageCompletionRequestState
            {
                RequestId = requestId,
                Generation = generation,
                RequestKey = requestKey,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion
            };
        }

        private void ProcessLanguageResponses()
        {
            if (_languageServiceClient == null)
            {
                return;
            }

            LanguageServiceEnvelope envelope;
            while (_languageServiceClient.TryDequeueResponse(out envelope))
            {
                if (envelope == null || string.IsNullOrEmpty(envelope.RequestId))
                {
                    continue;
                }

                if (string.Equals(envelope.RequestId, _languageInitializeRequestId, StringComparison.Ordinal))
                {
                    HandleLanguageInitializeResponse(envelope);
                    continue;
                }

                if (string.Equals(envelope.RequestId, _languageStatusRequestId, StringComparison.Ordinal))
                {
                    HandleLanguageStatusResponse(envelope);
                    continue;
                }

                if (_pendingLanguageAnalysis != null &&
                    string.Equals(envelope.RequestId, _pendingLanguageAnalysis.RequestId, StringComparison.Ordinal))
                {
                    HandleLanguageAnalysisResponse(envelope, _pendingLanguageAnalysis);
                    _pendingLanguageAnalysis = null;
                    continue;
                }

                if (_pendingLanguageHover != null &&
                    string.Equals(envelope.RequestId, _pendingLanguageHover.RequestId, StringComparison.Ordinal))
                {
                    HandleLanguageHoverResponse(envelope, _pendingLanguageHover);
                    _pendingLanguageHover = null;
                    continue;
                }

                if (_pendingLanguageDefinition != null &&
                    string.Equals(envelope.RequestId, _pendingLanguageDefinition.RequestId, StringComparison.Ordinal))
                {
                    HandleLanguageDefinitionResponse(envelope, _pendingLanguageDefinition);
                    _pendingLanguageDefinition = null;
                    continue;
                }

                if (_pendingLanguageCompletion != null &&
                    string.Equals(envelope.RequestId, _pendingLanguageCompletion.RequestId, StringComparison.Ordinal))
                {
                    HandleLanguageCompletionResponse(envelope, _pendingLanguageCompletion);
                    _pendingLanguageCompletion = null;
                }
            }
        }

        private void HandleLanguageInitializeResponse(LanguageServiceEnvelope envelope)
        {
            _languageInitializeRequestId = string.Empty;
            _languageServiceInitializing = false;
            _languageInitializeQueuedUtc = DateTime.MinValue;
            _lastLanguageInitializationProgressLogUtc = DateTime.MinValue;
            if (envelope == null || !envelope.Success)
            {
                _languageServiceReady = false;
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = envelope != null ? envelope.ErrorMessage ?? "startup failed" : "startup failed",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                _state.Diagnostics.Add("Roslyn worker failed to initialize: " + _state.LanguageServiceStatus.StatusMessage);
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceInitializeResponse>(envelope);
            _languageServiceReady = response != null && response.Success;
            if (!_languageServiceReady)
            {
                var message = response != null ? response.StatusMessage : "Roslyn worker failed to initialize.";
                _state.Diagnostics.Add("Roslyn worker failed to initialize: " + message);
                return;
            }

            _state.Diagnostics.Add("Roslyn worker ready: " +
                (response.WorkerVersion ?? string.Empty) +
                " on " +
                (response.RuntimeVersion ?? string.Empty) +
                ".");
            _languageStatusRequestId = _languageServiceClient.QueueStatus();
        }

        private void HandleLanguageStatusResponse(LanguageServiceEnvelope envelope)
        {
            _languageStatusRequestId = string.Empty;
            if (envelope == null || !envelope.Success)
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceStatusResponse>(envelope);
            if (response == null)
            {
                return;
            }

            _state.LanguageServiceStatus = response;
            MMLog.WriteInfo("[Cortex.Roslyn] Worker ready. CachedProjects=" +
                (_state.LanguageServiceStatus != null ? _state.LanguageServiceStatus.CachedProjectCount.ToString() : "0") +
                ", Capabilities=" + BuildCapabilitySummary(_state.LanguageServiceStatus));
        }

        private void LogLanguageInitializationProgress()
        {
            if (!_languageServiceInitializing ||
                _languageServiceClient == null ||
                string.IsNullOrEmpty(_languageInitializeRequestId) ||
                _languageInitializeQueuedUtc == DateTime.MinValue)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastLanguageInitializationProgressLogUtc).TotalSeconds < 5d)
            {
                return;
            }

            _lastLanguageInitializationProgressLogUtc = now;
            MMLog.WriteInfo("[Cortex.Roslyn] Waiting for initialize response. RequestId=" + _languageInitializeRequestId +
                ", ElapsedMs=" + (int)(now - _languageInitializeQueuedUtc).TotalMilliseconds +
                ", ClientRunning=" + _languageServiceClient.IsRunning +
                ", LastError=" + (_languageServiceClient.LastError ?? string.Empty) + ".");
        }

        private void HandleLanguageAnalysisResponse(LanguageServiceEnvelope envelope, DocumentLanguageAnalysisRequestState pending)
        {
            _languageAnalysisInFlight = false;
            _pendingLanguageAnalysisFingerprint = string.Empty;
            if (pending == null || pending.Generation != _languageServiceGeneration)
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceAnalysisResponse>(envelope);
            var target = FindOpenDocument(response != null ? response.DocumentPath : pending.DocumentPath);
            if (target == null)
            {
                return;
            }

            if (response == null)
            {
                _state.Diagnostics.Add("Roslyn analysis failed for " + Path.GetFileName(target.FilePath) + ": unreadable payload.");
                return;
            }

            if (response.DocumentVersion > 0 &&
                target.TextVersion > 0 &&
                response.DocumentVersion != target.TextVersion)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Ignored stale analysis for " + Path.GetFileName(target.FilePath) +
                    ". ResponseVersion=" + response.DocumentVersion +
                    ", LiveVersion=" + target.TextVersion);
                return;
            }

            target.LanguageAnalysis = _documentLanguageAnalysisService.MergeAnalysis(target.LanguageAnalysis, response, pending);
            if (response.Success)
            {
                target.LastLanguageAnalysisUtc = DateTime.UtcNow;
                target.LastLanguageAnalysisVersion = response.DocumentVersion;
                if (pending.IncludeClassifications)
                {
                    target.LastLanguageClassificationVersion = response.DocumentVersion;
                    target.PendingLanguageInvalidation = new EditorInvalidation();
                }

                if (pending.IncludeDiagnostics)
                {
                    target.LastLanguageDiagnosticVersion = response.DocumentVersion;
                }

                _documentLanguageAnalysisService.RememberSnapshot(target);
            }

            if (target.LastLanguageClassificationVersion == target.TextVersion &&
                target.LastLanguageDiagnosticVersion == target.TextVersion)
            {
                _lastAnalyzedDocumentFingerprint = pending.Fingerprint ?? string.Empty;
            }

            MMLog.WriteInfo("[Cortex.Roslyn] Analysis complete for " +
                Path.GetFileName(target.FilePath) +
                ". Phase=" + _documentLanguageAnalysisService.BuildAnalysisPhaseLabel(pending) +
                ", Diagnostics=" + CountDiagnostics(target.LanguageAnalysis) +
                ", Classifications=" + CountClassifications(target.LanguageAnalysis) +
                ", Summary=" + BuildClassificationSummary(target.LanguageAnalysis));

            if (!response.Success)
            {
                _state.Diagnostics.Add("Roslyn analysis failed for " + Path.GetFileName(target.FilePath) + ": " + response.StatusMessage);
                MMLog.WriteWarning("[Cortex.Roslyn] Analysis failed for " + Path.GetFileName(target.FilePath) + ": " + response.StatusMessage);
            }
        }

        private void HandleLanguageHoverResponse(LanguageServiceEnvelope envelope, PendingLanguageHoverRequest pending)
        {
            _languageHoverInFlight = false;
            if (pending == null || pending.Generation != _languageServiceGeneration)
            {
                return;
            }

            var liveSession = FindOpenDocument(pending.DocumentPath);
            if (!string.Equals(_state.Editor.RequestedHoverKey, pending.HoverKey, StringComparison.Ordinal) ||
                (liveSession != null &&
                 pending.DocumentVersion > 0 &&
                 liveSession.TextVersion > 0 &&
                 liveSession.TextVersion != pending.DocumentVersion))
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceHoverResponse>(envelope);
            if (response == null)
            {
                response = new LanguageServiceHoverResponse
                {
                    Success = false,
                    StatusMessage = envelope != null && !envelope.Success
                        ? envelope.ErrorMessage
                        : "Roslyn hover payload was unreadable."
                };
            }

            _state.Editor.ActiveHoverKey = pending.HoverKey;
            _state.Editor.ActiveHoverResponse = response;
            _state.Editor.RequestedHoverKey = string.Empty;
            _state.Editor.RequestedHoverDocumentPath = string.Empty;
            _state.Editor.RequestedHoverLine = 0;
            _state.Editor.RequestedHoverColumn = 0;
            _state.Editor.RequestedHoverAbsolutePosition = -1;
            var requestedHoverTokenText = _state.Editor.RequestedHoverTokenText ?? string.Empty;
            _state.Editor.RequestedHoverTokenText = string.Empty;
            if (response.Success)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Hover resolved for " + requestedHoverTokenText + ".");
                return;
            }

            MMLog.WriteWarning("[Cortex.Roslyn] Hover failed for " +
                requestedHoverTokenText +
                ": " + (response.StatusMessage ?? "Unknown Roslyn hover failure."));
        }

        private void HandleLanguageDefinitionResponse(LanguageServiceEnvelope envelope, PendingLanguageDefinitionRequest pending)
        {
            _languageDefinitionInFlight = false;
            if (pending == null || pending.Generation != _languageServiceGeneration)
            {
                return;
            }

            try
            {
                var liveSession = FindOpenDocument(pending.DocumentPath);
                if (liveSession != null &&
                    pending.DocumentVersion > 0 &&
                    liveSession.TextVersion > 0 &&
                    liveSession.TextVersion != pending.DocumentVersion)
                {
                    MMLog.WriteInfo("[Cortex.Roslyn] Ignored stale definition response for " +
                        (pending.TokenText ?? string.Empty) +
                        ". ResponseVersion=" + pending.DocumentVersion +
                        ", LiveVersion=" + liveSession.TextVersion);
                    return;
                }

                var response = DeserializeEnvelopePayload<LanguageServiceDefinitionResponse>(envelope);
                if (response == null || !response.Success)
                {
                    _state.StatusMessage = response != null && !string.IsNullOrEmpty(response.StatusMessage)
                        ? response.StatusMessage
                        : (envelope != null && !string.IsNullOrEmpty(envelope.ErrorMessage) ? envelope.ErrorMessage : "Definition was not found.");
                    return;
                }

                _state.Editor.RequestedDefinitionAbsolutePosition = -1;

                var opened = _navigationService != null && _navigationService.OpenLanguageSymbolTarget(
                    _state,
                    response.SymbolDisplay,
                    response.SymbolKind,
                    response.MetadataName,
                    response.ContainingTypeName,
                    response.ContainingAssemblyName,
                    response.DocumentationCommentId,
                    response.DocumentPath,
                    response.Range,
                    "Opened definition: " + (response.SymbolDisplay ?? (!string.IsNullOrEmpty(response.DocumentPath) ? Path.GetFileName(response.DocumentPath) : response.MetadataName ?? string.Empty)),
                    !string.IsNullOrEmpty(response.DocumentPath)
                        ? "Could not open definition source file."
                        : "Could not open decompiled definition.")
                    ? FindOpenDocument(!string.IsNullOrEmpty(response.DocumentPath) ? response.DocumentPath : _state.Documents.ActiveDocumentPath)
                    : null;
                if (opened != null)
                {
                    MMLog.WriteInfo("[Cortex.Roslyn] Opened definition for " +
                        (pending.TokenText ?? string.Empty) +
                        " -> " +
                        (!string.IsNullOrEmpty(response.DocumentPath)
                            ? response.DocumentPath
                            : (response.SymbolDisplay ?? response.MetadataName ?? string.Empty)));
                    return;
                }

                _state.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                    ? response.StatusMessage
                    : "Definition was not found.";
            }
            catch (Exception ex)
            {
                _state.StatusMessage = "Definition lookup failed.";
                MMLog.WriteError("[Cortex.Roslyn] Definition response handling crashed for '" +
                    (pending.TokenText ?? string.Empty) + "': " + ex);
            }
        }

        private void HandleLanguageCompletionResponse(LanguageServiceEnvelope envelope, DocumentLanguageCompletionRequestState pending)
        {
            _languageCompletionInFlight = false;
            if (pending == null || pending.Generation != _languageServiceGeneration || _state.Editor == null)
            {
                return;
            }

            if (!string.Equals(_state.Editor.RequestedCompletionKey ?? string.Empty, pending.RequestKey ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceCompletionResponse>(envelope);
            _state.Editor.RequestedCompletionKey = string.Empty;
            _state.Editor.RequestedCompletionDocumentPath = string.Empty;
            _state.Editor.RequestedCompletionLine = 0;
            _state.Editor.RequestedCompletionColumn = 0;
            _state.Editor.RequestedCompletionAbsolutePosition = -1;
            _state.Editor.RequestedCompletionTriggerCharacter = string.Empty;
            _state.Editor.RequestedCompletionExplicit = false;
            if (response == null)
            {
                _state.Editor.ActiveCompletionKey = string.Empty;
                _state.Editor.ActiveCompletionResponse = null;
                return;
            }

            var target = FindOpenDocument(response.DocumentPath);
            if (target == null ||
                (response.DocumentVersion > 0 && target.TextVersion > 0 && response.DocumentVersion != target.TextVersion))
            {
                return;
            }

            if (!_documentLanguageInteractionService.HasCompletionItems(response))
            {
                _state.Editor.ActiveCompletionKey = string.Empty;
                _state.Editor.ActiveCompletionResponse = null;
                return;
            }

            _state.Editor.ActiveCompletionKey = pending.RequestKey ?? string.Empty;
            _state.Editor.ActiveCompletionResponse = response;
        }

        private static TResponse DeserializeEnvelopePayload<TResponse>(LanguageServiceEnvelope envelope)
            where TResponse : LanguageServiceOperationResponse, new()
        {
            if (envelope == null)
            {
                return null;
            }

            if (!envelope.Success)
            {
                return new TResponse
                {
                    Success = false,
                    StatusMessage = envelope.ErrorMessage ?? string.Empty
                };
            }

            if (string.IsNullOrEmpty(envelope.PayloadJson))
            {
                return new TResponse
                {
                    Success = true,
                    StatusMessage = string.Empty
                };
            }

            return ManualJson.Deserialize<TResponse>(envelope.PayloadJson);
        }

        private bool TryOpenMetadataDefinition(LanguageServiceDefinitionResponse response)
        {
            try
            {
                if (response == null || _navigationService == null)
                {
                    MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback was unavailable because a required Cortex service was null.");
                    return false;
                }

                MMLog.WriteInfo("[Cortex.Roslyn] Metadata definition fallback starting for '" +
                    (_state.Editor.RequestedDefinitionTokenText ?? response.SymbolDisplay ?? response.MetadataName ?? string.Empty) +
                    "' in assembly '" + (response.ContainingAssemblyName ?? string.Empty) + "'.");

                string assemblyPath;
                if (!MetadataNavigationResolver.TryResolveAssemblyPath(_state, _sourceLookupIndex, response.ContainingAssemblyName, out assemblyPath))
                {
                    MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not resolve assembly '" + (response.ContainingAssemblyName ?? string.Empty) + "'.");
                    return false;
                }

                MMLog.WriteInfo("[Cortex.Roslyn] Metadata definition fallback resolved assembly path: " + assemblyPath);

                int metadataToken;
                DecompilerEntityKind entityKind;
                if (!TryResolveMetadataTarget(response, assemblyPath, out metadataToken, out entityKind))
                {
                    MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not resolve token for '" +
                        (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) +
                        "' in " + assemblyPath + ".");
                    return false;
                }

                MMLog.WriteInfo("[Cortex.Roslyn] Metadata definition fallback resolved token 0x" +
                    metadataToken.ToString("X8") + " (" + entityKind + ").");

                var decompiled = _navigationService.RequestDecompilerSource(
                    _state,
                    assemblyPath,
                    metadataToken,
                    entityKind,
                    false);

                if (decompiled == null)
                {
                    MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback decompiler request returned null for '" +
                        (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) + "'.");
                    return false;
                }

                MMLog.WriteInfo("[Cortex.Roslyn] Metadata definition fallback decompiler returned cache path: " +
                    (decompiled.CachePath ?? string.Empty));

                if (!_navigationService.OpenDecompilerResult(
                    _state,
                    decompiled,
                    "Opened decompiled definition: " + (response.SymbolDisplay ?? response.MetadataName ?? Path.GetFileName(assemblyPath)),
                    string.Empty))
                {
                    MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not open decompiled output for '" +
                        (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) +
                        "' from " + assemblyPath + ".");
                    return false;
                }
                MMLog.WriteInfo("[Cortex.Roslyn] Opened metadata definition for " +
                    (_state.Editor.RequestedDefinitionTokenText ?? string.Empty) +
                    " -> " + assemblyPath + " token 0x" + metadataToken.ToString("X8"));
                return true;
            }
            catch (Exception ex)
            {
                var symbolText = _state.Editor.RequestedDefinitionTokenText ?? (response != null ? (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) : string.Empty);
                MMLog.WriteError("[Cortex.Roslyn] Metadata definition fallback crashed for '" +
                    symbolText +
                    "': " + ex);
                return false;
            }
        }

        private bool TryResolveMetadataTarget(LanguageServiceDefinitionResponse response, string assemblyPath, out int metadataToken, out DecompilerEntityKind entityKind)
        {
            metadataToken = 0;
            entityKind = DecompilerEntityKind.Type;
            return response != null && MetadataNavigationResolver.TryResolveMetadataTarget(
                assemblyPath,
                response.DocumentationCommentId,
                response.ContainingTypeName,
                response.SymbolKind,
                out metadataToken,
                out entityKind);
        }

        private DocumentSession FindOpenDocument(string filePath)
        {
            return CortexModuleUtil.FindOpenDocument(_state, filePath);
        }

        private LanguageServiceInitializeRequest BuildLanguageInitializeRequest()
        {
            return new LanguageServiceInitializeRequest
            {
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, _state.SelectedProject),
                ProjectFilePaths = CollectLanguageWarmupProjectPaths(),
                SolutionFilePaths = new string[0]
            };
        }

        private string[] CollectLanguageWarmupProjectPaths()
        {
            var projectPaths = new List<string>();
            AddLanguageWarmupProjectPath(projectPaths, _state.SelectedProject);

            if (_state.Documents.ActiveDocument != null)
            {
                AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(_state.Documents.ActiveDocument.FilePath));
            }

            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                if (session == null || string.IsNullOrEmpty(session.FilePath))
                {
                    continue;
                }

                AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(session.FilePath));
            }

            return projectPaths.ToArray();
        }

        private static void AddLanguageWarmupProjectPath(List<string> projectPaths, CortexProjectDefinition project)
        {
            if (projectPaths == null || project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(project.ProjectFilePath);
                if (File.Exists(fullPath) && !ContainsLanguageWarmupProjectPath(projectPaths, fullPath))
                {
                    projectPaths.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool ContainsLanguageWarmupProjectPath(List<string> projectPaths, string candidatePath)
        {
            if (projectPaths == null || string.IsNullOrEmpty(candidatePath))
            {
                return false;
            }

            for (var i = 0; i < projectPaths.Count; i++)
            {
                if (string.Equals(projectPaths[i], candidatePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private CortexProjectDefinition ResolveProjectForDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || _projectCatalog == null)
            {
                return _state.SelectedProject;
            }

            if (_state.SelectedProject != null &&
                IsPathWithinRoot(filePath, _state.SelectedProject.SourceRootPath))
            {
                return _state.SelectedProject;
            }

            var projects = _projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project != null && IsPathWithinRoot(filePath, project.SourceRootPath))
                {
                    return project;
                }
            }

            return _state.SelectedProject;
        }

        private static string[] BuildLanguageSourceRoots(CortexSettings settings, CortexProjectDefinition project)
        {
            return SourceRootSetBuilder.Build(project, settings, SourceRootSetBuilder.LanguageServiceRoots).ToArray();
        }

        private static bool IsPathWithinRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var fullFile = Path.GetFullPath(filePath);
                var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveLanguageWorkerPath(CortexSettings settings, string smmBin)
        {
            var candidates = new List<string>();
            if (settings != null && !string.IsNullOrEmpty(settings.RoslynServicePathOverride))
            {
                candidates.Add(settings.RoslynServicePathOverride);
            }

            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.dll"));
            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.exe"));

            for (var i = 0; i < candidates.Count; i++)
            {
                try
                {
                    var candidate = Path.GetFullPath(candidates[i]);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static string BuildLanguageFingerprint(DocumentSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            return (session.FilePath ?? string.Empty) + "|" + session.TextVersion;
        }

        private static int CountDiagnostics(LanguageServiceAnalysisResponse response)
        {
            return response != null && response.Diagnostics != null ? response.Diagnostics.Length : 0;
        }

        private static int CountClassifications(LanguageServiceAnalysisResponse response)
        {
            return response != null && response.Classifications != null ? response.Classifications.Length : 0;
        }

        private static string BuildCapabilitySummary(LanguageServiceStatusResponse response)
        {
            if (response == null || response.Capabilities == null || response.Capabilities.Length == 0)
            {
                return "(none)";
            }

            return string.Join(",", response.Capabilities);
        }

        private static string BuildLanguageServiceConfigurationFingerprint(
            string workerPath,
            CortexSettings settings,
            LanguageServiceInitializeRequest request)
        {
            var builder = new StringBuilder();
            builder.Append(workerPath ?? string.Empty);
            builder.Append("|timeout=");
            builder.Append(settings != null ? settings.RoslynServiceTimeoutMs : 0);
            builder.Append("|workspace=");
            builder.Append(request != null ? request.WorkspaceRootPath ?? string.Empty : string.Empty);
            builder.Append("|projects=");
            AppendFingerprintValues(builder, request != null ? request.ProjectFilePaths : null);
            builder.Append("|sources=");
            AppendFingerprintValues(builder, request != null ? request.SourceRoots : null);
            return builder.ToString();
        }

        private static void AppendFingerprintValues(StringBuilder builder, string[] values)
        {
            if (builder == null || values == null || values.Length == 0)
            {
                return;
            }

            var copy = new List<string>(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    copy.Add(values[i]);
                }
            }

            copy.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < copy.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(";");
                }

                builder.Append(copy[i]);
            }
        }

        private static string BuildClassificationSummary(LanguageServiceAnalysisResponse response)
        {
            if (response == null || response.Classifications == null || response.Classifications.Length == 0)
            {
                return "(none)";
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < response.Classifications.Length; i++)
            {
                var classification = response.Classifications[i] != null
                    ? response.Classifications[i].Classification ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrEmpty(classification))
                {
                    classification = "(empty)";
                }

                int current;
                counts.TryGetValue(classification, out current);
                counts[classification] = current + 1;
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add(pair.Key + "=" + pair.Value);
                if (parts.Count >= 8)
                {
                    break;
                }
            }

            return string.Join("; ", parts.ToArray());
        }

        private sealed class PendingLanguageHoverRequest
        {
            public string RequestId;
            public int Generation;
            public string DocumentPath;
            public int DocumentVersion;
            public string HoverKey;
        }

        private sealed class PendingLanguageDefinitionRequest
        {
            public string RequestId;
            public int Generation;
            public string DocumentPath;
            public int DocumentVersion;
            public string TokenText;
        }
    }
}

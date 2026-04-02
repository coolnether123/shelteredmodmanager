using System;
using Cortex.CompletionProviders;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Completion;
using Cortex.Services.Semantics.Completion.Augmentation;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private const int CompletionAugmentationDebounceMs = 450;
        private ICompletionAugmentationClient _completionAugmentationClient;
        private bool _completionAugmentationInFlight;
        private PendingCompletionAugmentationRequest _pendingCompletionAugmentation;
        private DeferredCompletionAugmentationRequest _deferredCompletionAugmentation;

        private void InitializeCompletionAugmentation(CortexSettings settings)
        {
            ShutdownCompletionAugmentation();
            SetCompletionAugmentationStatus("starting",
                settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty,
                string.Empty);
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Initializing. EnableCompletionAugmentation=" +
                (settings != null && settings.EnableCompletionAugmentation) +
                ", RequestedProvider=" + (settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty) +
                ", EnableTabby=" + (settings != null && settings.EnableTabbyCompletion) +
                ", TabbyServerUrl=" + (settings != null ? settings.TabbyServerUrl ?? string.Empty : string.Empty) +
                ", OllamaModel=" + (settings != null ? settings.OllamaModel ?? string.Empty : string.Empty) + ".");
            _completionAugmentationClient = CompletionAugmentationBootstrapper.Create(
                settings,
                new CompletionAugmentationProviderContext
                {
                    HostBinPath = _bootstrapper.HostEnvironment != null
                        ? _bootstrapper.HostEnvironment.HostBinPath ?? string.Empty
                        : string.Empty
                },
                delegate(string message)
                {
                    MMLog.WriteDebug(message);
                });
            SetCompletionAugmentationStatus(
                _completionAugmentationClient != null && _completionAugmentationClient.IsEnabled ? "ready" : "offline",
                _completionAugmentationClient != null ? _completionAugmentationClient.ProviderId ?? string.Empty : settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty,
                _completionAugmentationClient != null ? _completionAugmentationClient.LastError ?? string.Empty : string.Empty);
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Provider initialized: " +
                (_completionAugmentationClient != null ? _completionAugmentationClient.ProviderId ?? string.Empty : "<none>") +
                ", Enabled=" + (_completionAugmentationClient != null && _completionAugmentationClient.IsEnabled) +
                ", RequestedProvider=" + (settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty) + ".");
        }

        private void ShutdownCompletionAugmentation()
        {
            if (_completionAugmentationClient != null)
            {
                _completionAugmentationClient.Dispose();
            }

            _completionAugmentationClient = null;
            _completionAugmentationInFlight = false;
            _pendingCompletionAugmentation = null;
            _deferredCompletionAugmentation = null;
            SetCompletionAugmentationStatus("offline", string.Empty, string.Empty);
        }

        private void ProcessCompletionAugmentationResponses()
        {
            if (_completionAugmentationClient == null)
            {
                SetCompletionAugmentationStatus("offline", string.Empty, string.Empty);
                return;
            }

            CompletionAugmentationResult result;
            while (_completionAugmentationClient.TryDequeueResponse(out result))
            {
                if (result == null || _pendingCompletionAugmentation == null ||
                    !string.Equals(result.RequestId, _pendingCompletionAugmentation.RequestId, StringComparison.Ordinal))
                {
                    continue;
                }

                HandleCompletionAugmentationResponse(result, _pendingCompletionAugmentation);
                _pendingCompletionAugmentation = null;
                DispatchDeferredCompletionAugmentation();
            }
        }

        private bool TryQueueCompletionAugmentation(
            DocumentSession session,
            DocumentLanguageCompletionRequestState pending,
            CompletionAugmentationRequest request,
            LanguageServiceCompletionResponse primaryResponse)
        {
            var preQueueReason = CompletionAugmentationDispatchPolicy.GetPreQueueReason(request);
            if (!string.IsNullOrEmpty(preQueueReason))
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=" +
                    preQueueReason +
                    ", Document=" + (request != null ? request.DocumentPath ?? string.Empty : string.Empty) + ".");
                return true;
            }

            var skipReason = CompletionAugmentationDispatchPolicy.GetSkipReason(
                _state != null ? _state.Editor.Completion : null,
                session,
                request,
                _editorCompletionService);
            if (!string.IsNullOrEmpty(skipReason))
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=" +
                    skipReason +
                    ", Document=" + (request != null ? request.DocumentPath ?? string.Empty : string.Empty) + ".");
                return true;
            }

            if (_completionAugmentationClient == null)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=NoClient.");
                SetCompletionAugmentationStatus("offline", string.Empty, string.Empty);
                return false;
            }

            if (!_completionAugmentationClient.IsEnabled)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=ClientDisabled, Provider=" +
                    (_completionAugmentationClient.ProviderId ?? string.Empty) +
                    ", LastError=" + (_completionAugmentationClient.LastError ?? string.Empty) + ".");
                SetCompletionAugmentationStatus("error",
                    _completionAugmentationClient.ProviderId ?? string.Empty,
                    _completionAugmentationClient.LastError ?? string.Empty);
                return false;
            }

            if (_completionAugmentationInFlight)
            {
                _deferredCompletionAugmentation = new DeferredCompletionAugmentationRequest
                {
                    Pending = ClonePendingRequest(pending),
                    Request = request,
                    PreferredReplacementRange = primaryResponse != null ? CloneRange(primaryResponse.ReplacementRange) : null,
                    EarliestDispatchUtc = CompletionAugmentationDispatchPolicy.GetDeferredDispatchUtc(request, CompletionAugmentationDebounceMs)
                };
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue deferred. Reason=InFlight, PendingRequestId=" +
                    (_pendingCompletionAugmentation != null ? _pendingCompletionAugmentation.RequestId ?? string.Empty : string.Empty) +
                    ", DeferredRequestKey=" + (pending != null ? pending.RequestKey ?? string.Empty : string.Empty) + ".");
                SetCompletionAugmentationStatus("thinking",
                    _completionAugmentationClient.ProviderId ?? string.Empty,
                    "queued");
                return true;
            }

            if (pending == null)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=PendingStateMissing.");
                return false;
            }

            if (request == null)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue skipped. Reason=RequestMissing, Document=" +
                    (pending.DocumentPath ?? string.Empty) +
                    ", Position=" + pending.AbsolutePosition + ".");
                return false;
            }

            if (_completionAugmentationClient == null ||
                !_completionAugmentationClient.IsEnabled ||
                _completionAugmentationInFlight ||
                pending == null ||
                request == null)
            {
                return false;
            }

            if (CompletionAugmentationDispatchPolicy.ShouldDebounce(request))
            {
                _deferredCompletionAugmentation = new DeferredCompletionAugmentationRequest
                {
                    Pending = ClonePendingRequest(pending),
                    Request = request,
                    PreferredReplacementRange = primaryResponse != null ? CloneRange(primaryResponse.ReplacementRange) : null,
                    EarliestDispatchUtc = CompletionAugmentationDispatchPolicy.GetDeferredDispatchUtc(request, CompletionAugmentationDebounceMs)
                };
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queue deferred. Reason=Debounce, DeferredRequestKey=" +
                    (pending != null ? pending.RequestKey ?? string.Empty : string.Empty) + ".");
                SetCompletionAugmentationStatus("thinking",
                    _completionAugmentationClient.ProviderId ?? string.Empty,
                    "waiting for typing pause");
                return true;
            }

            return QueueCompletionAugmentationCore(
                ClonePendingRequest(pending),
                request,
                primaryResponse != null ? CloneRange(primaryResponse.ReplacementRange) : null);
        }

        private bool QueueCompletionAugmentationCore(
            PendingCompletionAugmentationRequest pending,
            CompletionAugmentationRequest request,
            LanguageServiceRange preferredReplacementRange)
        {
            var requestId = _completionAugmentationClient.QueueCompletion(request);
            if (string.IsNullOrEmpty(requestId))
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Request was not queued. Provider=" +
                    (_completionAugmentationClient.ProviderId ?? string.Empty) +
                    ", LastError=" + (_completionAugmentationClient.LastError ?? string.Empty) + ".");
                SetCompletionAugmentationStatus("error",
                    _completionAugmentationClient.ProviderId ?? string.Empty,
                    _completionAugmentationClient.LastError ?? string.Empty);
                return false;
            }

            _completionAugmentationInFlight = true;
            SetCompletionAugmentationStatus("thinking",
                _completionAugmentationClient.ProviderId ?? string.Empty,
                string.Empty);
            _pendingCompletionAugmentation = new PendingCompletionAugmentationRequest
            {
                RequestId = requestId,
                RequestKey = pending.RequestKey ?? string.Empty,
                DocumentPath = pending.DocumentPath ?? string.Empty,
                DocumentVersion = pending.DocumentVersion,
                AbsolutePosition = pending.AbsolutePosition,
                PreferredReplacementRange = preferredReplacementRange
            };
            MMLog.WriteDebug("[Cortex.Completion.Augmentation] Queued request " + requestId +
                ". Provider=" + (_completionAugmentationClient.ProviderId ?? string.Empty) +
                ", Document=" + (request.DocumentPath ?? string.Empty) +
                ", Position=" + request.AbsolutePosition + ".");
            return true;
        }

        private void HandleCompletionAugmentationResponse(
            CompletionAugmentationResult result,
            PendingCompletionAugmentationRequest pending)
        {
            _completionAugmentationInFlight = false;
            if (pending == null || _state.Editor == null)
            {
                return;
            }

            var target = FindOpenDocument(result.Response != null ? result.Response.DocumentPath : pending.DocumentPath);
            if (target == null || result.Response == null || !result.Response.Success)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Response was not applied. Provider=" +
                    (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                    ", HasTarget=" + (target != null) +
                    ", Success=" + (result != null && result.Response != null && result.Response.Success) +
                    ", Status=" + (result != null && result.Response != null ? result.Response.StatusMessage ?? string.Empty : string.Empty) + ".");
                SetCompletionAugmentationStatus(
                    result != null && result.Response != null && result.Response.Success ? "ready" : "error",
                    result != null ? result.ProviderId ?? string.Empty : _state.Editor.Completion.AugmentationProviderId ?? string.Empty,
                    result != null && result.Response != null ? result.Response.StatusMessage ?? string.Empty : string.Empty);
                if (_state.Editor.Completion.Response == null)
                {
                    _editorCompletionService.ClearPendingRequest(_state.Editor.Completion);
                }
                return;
            }

            if (pending.DocumentVersion > 0 &&
                target.TextVersion > 0 &&
                target.TextVersion != pending.DocumentVersion)
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Accepting version-shifted response. Provider=" +
                    (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                    ", PendingVersion=" + pending.DocumentVersion +
                    ", LiveVersion=" + target.TextVersion +
                    ", Document=" + (target.FilePath ?? string.Empty) + ".");
            }

            if (pending.PreferredReplacementRange != null)
            {
                result.Response.ReplacementRange = CloneRange(pending.PreferredReplacementRange);
            }

            // The editor may continue typing while the AI request is in flight.
            // Once we accept the shifted response above, normalize it to the live
            // document version so the merge path does not reject it as stale.
            result.Response.DocumentPath = target.FilePath ?? result.Response.DocumentPath ?? pending.DocumentPath ?? string.Empty;
            if (target.TextVersion > 0)
            {
                result.Response.DocumentVersion = target.TextVersion;
            }

            var inlineSet = _editorCompletionService.SetInlineSuggestion(
                _state.Editor.Completion,
                target,
                new DocumentLanguageCompletionRequestState
                {
                    RequestKey = pending.RequestKey,
                    DocumentPath = pending.DocumentPath,
                    DocumentVersion = pending.DocumentVersion,
                    AbsolutePosition = pending.AbsolutePosition
                },
                result.Response,
                result != null ? result.ProviderId ?? string.Empty : string.Empty);
            MMLog.WriteDebug("[Cortex.Completion.Augmentation] Inline suggestion updated. Provider=" +
                (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                ", Visible=" + inlineSet +
                ", Items=" + (result != null && result.Response != null && result.Response.Items != null ? result.Response.Items.Length : 0) +
                ", Document=" + (target != null ? target.FilePath ?? string.Empty : string.Empty) + ".");
            SetCompletionAugmentationStatus(
                inlineSet ? "suggestion" : "ready",
                result != null ? result.ProviderId ?? string.Empty : string.Empty,
                inlineSet ? "inline suggestion ready" : "response received");

            var merged = _editorCompletionService.MergeSupplementalResponse(
                _state.Editor.Completion,
                target,
                new DocumentLanguageCompletionRequestState
                {
                    RequestKey = pending.RequestKey,
                    DocumentPath = pending.DocumentPath,
                    DocumentVersion = pending.DocumentVersion,
                    AbsolutePosition = pending.AbsolutePosition
                },
                result.Response);
            MMLog.WriteDebug("[Cortex.Completion.Augmentation] Response processed. Provider=" +
                (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                ", Merged=" + merged +
                ", Items=" + (result != null && result.Response != null && result.Response.Items != null ? result.Response.Items.Length : 0) +
                ", Document=" + (target != null ? target.FilePath ?? string.Empty : string.Empty) + ".");
            if (!merged && _state.Editor.Completion.Response == null)
            {
                _editorCompletionService.ClearPendingRequest(_state.Editor.Completion);
            }
        }

        private void SetCompletionAugmentationStatus(string status, string providerId, string statusMessage)
        {
            if (_state == null || _state.Editor == null)
            {
                return;
            }

            _state.Editor.Completion.AugmentationStatus = status ?? string.Empty;
            _state.Editor.Completion.AugmentationProviderId = providerId ?? string.Empty;
            _state.Editor.Completion.AugmentationStatusMessage = statusMessage ?? string.Empty;
        }

        private void DispatchDeferredCompletionAugmentation()
        {
            if (_deferredCompletionAugmentation == null ||
                _completionAugmentationClient == null ||
                !_completionAugmentationClient.IsEnabled)
            {
                return;
            }

            var deferred = _deferredCompletionAugmentation;
            if (deferred.Pending == null || deferred.Request == null)
            {
                _deferredCompletionAugmentation = null;
                return;
            }

            if (deferred.EarliestDispatchUtc > DateTime.UtcNow)
            {
                return;
            }

            if (_completionAugmentationInFlight)
            {
                if (!deferred.CancelRequested &&
                    _pendingCompletionAugmentation != null &&
                    !string.IsNullOrEmpty(_pendingCompletionAugmentation.RequestId) &&
                    _completionAugmentationClient.CancelCompletion(_pendingCompletionAugmentation.RequestId))
                {
                    deferred.CancelRequested = true;
                    _deferredCompletionAugmentation = deferred;
                    MMLog.WriteDebug("[Cortex.Completion.Augmentation] Superseding in-flight request. RequestId=" +
                        (_pendingCompletionAugmentation.RequestId ?? string.Empty) +
                        ", NewRequestKey=" + (deferred.Pending.RequestKey ?? string.Empty) + ".");
                }
                return;
            }

            _deferredCompletionAugmentation = null;

            var deferredSession = FindOpenDocument(deferred.Request.DocumentPath);
            var skipReason = CompletionAugmentationDispatchPolicy.GetSkipReason(
                _state != null ? _state.Editor.Completion : null,
                deferredSession,
                deferred.Request,
                _editorCompletionService);
            if (!string.IsNullOrEmpty(skipReason))
            {
                MMLog.WriteDebug("[Cortex.Completion.Augmentation] Deferred request skipped. Reason=" +
                    skipReason +
                    ", RequestKey=" + (deferred.Pending.RequestKey ?? string.Empty) +
                    ", Document=" + (deferred.Request.DocumentPath ?? string.Empty) + ".");
                return;
            }

            MMLog.WriteDebug("[Cortex.Completion.Augmentation] Dispatching deferred request. RequestKey=" +
                (deferred.Pending.RequestKey ?? string.Empty) +
                ", Document=" + (deferred.Request.DocumentPath ?? string.Empty) + ".");
            QueueCompletionAugmentationCore(deferred.Pending, deferred.Request, deferred.PreferredReplacementRange);
        }

        private CompletionAugmentationRequest BuildCompletionAugmentationRequest(
            DocumentSession session,
            DocumentLanguageCompletionRequestState pending)
        {
            var documentPath = session != null ? session.FilePath ?? string.Empty : pending != null ? pending.DocumentPath ?? string.Empty : string.Empty;
            return CompletionAugmentationRequestBuilder.Build(
                session,
                pending,
                _state.Settings,
                _state.Editor != null ? _state.Editor.Completion : null,
                _state.Documents != null ? _state.Documents.OpenDocuments : null,
                MapLanguageId(documentPath));
        }

        private static LanguageServiceRange CloneRange(LanguageServiceRange range)
        {
            return range == null
                ? null
                : new LanguageServiceRange
                {
                    StartLine = range.StartLine,
                    StartColumn = range.StartColumn,
                    EndLine = range.EndLine,
                    EndColumn = range.EndColumn,
                    Start = range.Start,
                    Length = range.Length
                };
        }

        private sealed class PendingCompletionAugmentationRequest
        {
            public string RequestId;
            public string RequestKey;
            public string DocumentPath;
            public int DocumentVersion;
            public int AbsolutePosition;
            public LanguageServiceRange PreferredReplacementRange;
        }

        private static PendingCompletionAugmentationRequest ClonePendingRequest(DocumentLanguageCompletionRequestState pending)
        {
            if (pending == null)
            {
                return null;
            }

            return new PendingCompletionAugmentationRequest
            {
                RequestKey = pending.RequestKey ?? string.Empty,
                DocumentPath = pending.DocumentPath ?? string.Empty,
                DocumentVersion = pending.DocumentVersion,
                AbsolutePosition = pending.AbsolutePosition
            };
        }

        private sealed class DeferredCompletionAugmentationRequest
        {
            public PendingCompletionAugmentationRequest Pending;
            public CompletionAugmentationRequest Request;
            public LanguageServiceRange PreferredReplacementRange;
            public DateTime EarliestDispatchUtc;
            public bool CancelRequested;
        }

        private static string MapLanguageId(string documentPath)
        {
            var extension = !string.IsNullOrEmpty(documentPath)
                ? System.IO.Path.GetExtension(documentPath)
                : string.Empty;
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".cs":
                    return "csharp";
                case ".js":
                    return "javascript";
                case ".ts":
                    return "typescript";
                case ".tsx":
                    return "typescriptreact";
                case ".jsx":
                    return "javascriptreact";
                case ".py":
                    return "python";
                case ".json":
                    return "json";
                case ".xml":
                    return "xml";
                case ".java":
                    return "java";
                case ".cpp":
                case ".cc":
                case ".cxx":
                    return "cpp";
                case ".c":
                    return "c";
                default:
                    return "plaintext";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.Shell
{
    internal interface ICortexLanguageProviderSession
    {
        CortexShellLanguageRuntimeState RuntimeState { get; }
    }

    internal sealed class RoslynLanguageProviderSession :
        ILanguageProviderSession,
        ICortexLanguageProviderSession
    {
        private readonly RoslynLanguageServiceClient _client;
        // Providers only enqueue normalized messages. The runtime service is the only layer allowed to drain and apply them.
        private readonly Queue<LanguageRuntimeMessage> _messages = new Queue<LanguageRuntimeMessage>();
        private readonly object _sync = new object();
        private readonly LanguageProviderDescriptor _descriptor;
        private readonly string _configurationFingerprint;
        private readonly int _generation;
        private readonly CortexShellLanguageRuntimeState _runtimeState = new CortexShellLanguageRuntimeState();
        private bool _started;
        private bool _shutdown;
        private bool _faultReported;

        public RoslynLanguageProviderSession(
            LanguageProviderDescriptor descriptor,
            string configurationFingerprint,
            string workerPath,
            int timeoutMs,
            int generation)
        {
            _descriptor = descriptor ?? new LanguageProviderDescriptor();
            _configurationFingerprint = configurationFingerprint ?? string.Empty;
            _generation = generation;
            _client = new RoslynLanguageServiceClient(
                workerPath,
                timeoutMs,
                delegate(string message) { MMLog.WriteDebug(message); });
            _runtimeState.ServiceGeneration = generation;
            _runtimeState.ServiceConfigurationFingerprint = _configurationFingerprint;
        }

        public LanguageProviderDescriptor Descriptor
        {
            get { return _descriptor; }
        }

        public string ConfigurationFingerprint
        {
            get { return _configurationFingerprint; }
        }

        public string LastError
        {
            get { return _client != null ? _client.LastError : string.Empty; }
        }

        public bool IsRunning
        {
            get { return _client != null && _client.IsRunning; }
        }

        public CortexShellLanguageRuntimeState RuntimeState
        {
            get { return _runtimeState; }
        }

        public void Start(LanguageServiceInitializeRequest request)
        {
            if (_started || _shutdown)
            {
                return;
            }

            _started = true;
            _runtimeState.InitializeRequest = request;
            _runtimeState.ServiceReady = false;
            _runtimeState.ServiceInitializing = true;
            _runtimeState.InitializeQueuedUtc = DateTime.UtcNow;
            _runtimeState.LastInitializationProgressLogUtc = _runtimeState.InitializeQueuedUtc;
            _runtimeState.InitializeRequestId = _client != null
                ? _client.QueueInitialize(request ?? new LanguageServiceInitializeRequest())
                : string.Empty;
            if (!string.IsNullOrEmpty(_runtimeState.InitializeRequestId))
            {
                return;
            }

            _runtimeState.ServiceInitializing = false;
            Enqueue(
                LanguageRuntimeMessageKind.ProviderFault,
                string.Empty,
                0,
                _client != null && !string.IsNullOrEmpty(_client.LastError)
                    ? _client.LastError
                    : "Language provider failed to queue initialization.",
                null);
        }

        public void Advance()
        {
            if (_shutdown || _client == null)
            {
                return;
            }

            LanguageServiceEnvelope envelope;
            while (_client.TryDequeueResponse(out envelope))
            {
                Enqueue(
                    envelope != null && envelope.Success
                        ? LanguageRuntimeMessageKind.RequestResult
                        : LanguageRuntimeMessageKind.RequestFailure,
                    envelope != null ? envelope.RequestId : string.Empty,
                    ExtractDocumentVersion(envelope),
                    envelope != null ? envelope.ErrorMessage : string.Empty,
                    envelope);
            }

            if (_started &&
                !_faultReported &&
                !_client.IsRunning &&
                !string.IsNullOrEmpty(_client.LastError))
            {
                _faultReported = true;
                Enqueue(
                    LanguageRuntimeMessageKind.ProviderFault,
                    string.Empty,
                    0,
                    _client.LastError,
                    null);
            }
        }

        public bool TryCancelRequest(string requestId)
        {
            return _client != null && _client.CancelRequest(requestId);
        }

        public bool TryDequeueMessage(out LanguageRuntimeMessage message)
        {
            lock (_sync)
            {
                if (_messages.Count == 0)
                {
                    message = null;
                    return false;
                }

                message = _messages.Dequeue();
                return true;
            }
        }

        public string QueueStatus()
        {
            return _client != null ? _client.QueueStatus() : string.Empty;
        }

        public string QueueAnalyzeDocument(LanguageServiceDocumentRequest request)
        {
            return _client != null ? _client.QueueAnalyzeDocument(request) : string.Empty;
        }

        public string QueueHover(LanguageServiceHoverRequest request)
        {
            return _client != null ? _client.QueueHover(request) : string.Empty;
        }

        public string QueueGoToDefinition(LanguageServiceDefinitionRequest request)
        {
            return _client != null ? _client.QueueGoToDefinition(request) : string.Empty;
        }

        public string QueueCompletion(LanguageServiceCompletionRequest request)
        {
            return _client != null ? _client.QueueCompletion(request) : string.Empty;
        }

        public string QueueSignatureHelp(LanguageServiceSignatureHelpRequest request)
        {
            return _client != null ? _client.QueueSignatureHelp(request) : string.Empty;
        }

        public string QueueSymbolContext(LanguageServiceSymbolContextRequest request)
        {
            return _client != null ? _client.QueueSymbolContext(request) : string.Empty;
        }

        public string QueueRenamePreview(LanguageServiceRenameRequest request)
        {
            return _client != null ? _client.QueueRenamePreview(request) : string.Empty;
        }

        public string QueueReferences(LanguageServiceReferencesRequest request)
        {
            return _client != null ? _client.QueueReferences(request) : string.Empty;
        }

        public string QueueGoToBase(LanguageServiceBaseSymbolRequest request)
        {
            return _client != null ? _client.QueueGoToBase(request) : string.Empty;
        }

        public string QueueGoToImplementation(LanguageServiceImplementationRequest request)
        {
            return _client != null ? _client.QueueGoToImplementation(request) : string.Empty;
        }

        public string QueueCallHierarchy(LanguageServiceCallHierarchyRequest request)
        {
            return _client != null ? _client.QueueCallHierarchy(request) : string.Empty;
        }

        public string QueueValueSource(LanguageServiceValueSourceRequest request)
        {
            return _client != null ? _client.QueueValueSource(request) : string.Empty;
        }

        public string QueueDocumentTransformPreview(LanguageServiceDocumentTransformRequest request)
        {
            return _client != null ? _client.QueueDocumentTransformPreview(request) : string.Empty;
        }

        public void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            if (_client != null)
            {
                _client.Dispose();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private void Enqueue(
            LanguageRuntimeMessageKind kind,
            string requestId,
            int documentVersion,
            string message,
            LanguageServiceEnvelope envelope)
        {
            lock (_sync)
            {
                _messages.Enqueue(new LanguageRuntimeMessage
                {
                    Kind = kind,
                    Generation = _generation,
                    RequestId = requestId ?? string.Empty,
                    DocumentVersion = documentVersion,
                    Message = message ?? string.Empty,
                    Envelope = envelope,
                    LifecycleState = _runtimeState.ServiceReady
                        ? LanguageRuntimeLifecycleState.Running
                        : LanguageRuntimeLifecycleState.Starting,
                    HealthState = kind == LanguageRuntimeMessageKind.ProviderFault
                        ? LanguageRuntimeHealthState.Faulted
                        : LanguageRuntimeHealthState.Healthy
                });
            }
        }

        private static int ExtractDocumentVersion(LanguageServiceEnvelope envelope)
        {
            if (envelope == null || string.IsNullOrEmpty(envelope.PayloadJson))
            {
                return 0;
            }

            try
            {
                switch (envelope.Command ?? string.Empty)
                {
                    case LanguageServiceCommands.AnalyzeDocument:
                        var analysis = ManualJson.Deserialize<LanguageServiceAnalysisResponse>(envelope.PayloadJson);
                        return analysis != null ? analysis.DocumentVersion : 0;
                    case LanguageServiceCommands.Hover:
                        var hover = ManualJson.Deserialize<LanguageServiceHoverResponse>(envelope.PayloadJson);
                        return hover != null ? hover.DocumentVersion : 0;
                    case LanguageServiceCommands.GoToDefinition:
                        var definition = ManualJson.Deserialize<LanguageServiceDefinitionResponse>(envelope.PayloadJson);
                        return definition != null ? definition.DocumentVersion : 0;
                    case LanguageServiceCommands.Completion:
                        var completion = ManualJson.Deserialize<LanguageServiceCompletionResponse>(envelope.PayloadJson);
                        return completion != null ? completion.DocumentVersion : 0;
                    case LanguageServiceCommands.SignatureHelp:
                        var signatureHelp = ManualJson.Deserialize<LanguageServiceSignatureHelpResponse>(envelope.PayloadJson);
                        return signatureHelp != null ? signatureHelp.DocumentVersion : 0;
                    case LanguageServiceCommands.CallHierarchy:
                        var callHierarchy = ManualJson.Deserialize<LanguageServiceCallHierarchyResponse>(envelope.PayloadJson);
                        return callHierarchy != null ? callHierarchy.DocumentVersion : 0;
                    case LanguageServiceCommands.FindReferences:
                        var references = ManualJson.Deserialize<LanguageServiceReferencesResponse>(envelope.PayloadJson);
                        return references != null ? references.DocumentVersion : 0;
                    case LanguageServiceCommands.RenamePreview:
                        var rename = ManualJson.Deserialize<LanguageServiceRenameResponse>(envelope.PayloadJson);
                        return rename != null ? rename.DocumentVersion : 0;
                    case LanguageServiceCommands.GoToBase:
                        var baseSymbol = ManualJson.Deserialize<LanguageServiceBaseSymbolResponse>(envelope.PayloadJson);
                        return baseSymbol != null ? baseSymbol.DocumentVersion : 0;
                    case LanguageServiceCommands.GoToImplementation:
                        var implementation = ManualJson.Deserialize<LanguageServiceImplementationResponse>(envelope.PayloadJson);
                        return implementation != null ? implementation.DocumentVersion : 0;
                    case LanguageServiceCommands.ValueSource:
                        var valueSource = ManualJson.Deserialize<LanguageServiceValueSourceResponse>(envelope.PayloadJson);
                        return valueSource != null ? valueSource.DocumentVersion : 0;
                    case LanguageServiceCommands.DocumentTransformPreview:
                        var transform = ManualJson.Deserialize<LanguageServiceDocumentTransformResponse>(envelope.PayloadJson);
                        return transform != null ? transform.DocumentVersion : 0;
                }
            }
            catch
            {
            }

            return 0;
        }
    }
}

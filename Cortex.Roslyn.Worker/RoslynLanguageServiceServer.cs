using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;
using Microsoft.CodeAnalysis;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
        private static readonly AsyncLocal<CancellationToken> _ambientCancellationToken = new AsyncLocal<CancellationToken>();
        private string _workspaceRootPath = string.Empty;
        private string[] _sourceRoots = new string[0];
        private string[] _projectFilePaths = new string[0];
        private string[] _solutionFilePaths = new string[0];
        private string[] _referenceAssemblyPaths = new string[0];
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PortableExecutableReference> _metadataReferenceCache = new System.Collections.Concurrent.ConcurrentDictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);
        private readonly object _requestSync = new object();
        private readonly Dictionary<string, CancellationTokenSource> _activeRequests = new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _projectLoadGate = new SemaphoreSlim(1, 1);

        public RoslynLanguageServiceServer()
        {
            RegisterMsBuild();
        }

        public Task<LanguageServiceEnvelope> HandleQueuedAsync(LanguageServiceEnvelope request)
        {
            if (request == null)
            {
                return Task.FromResult(BuildErrorEnvelope(null, "Language request was empty."));
            }

            if (string.Equals(request.Command, LanguageServiceCommands.CancelRequest, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HandleCancel(request));
            }

            if (string.Equals(request.Command, LanguageServiceCommands.Shutdown, StringComparison.OrdinalIgnoreCase))
            {
                CancelAllActiveRequests();
            }

            return ProcessRequestAsync(request);
        }

        private async Task<LanguageServiceEnvelope> ProcessRequestAsync(LanguageServiceEnvelope request)
        {
            var requestId = request != null ? request.RequestId ?? string.Empty : string.Empty;
            var cancellationSource = new CancellationTokenSource();
            RegisterActiveRequest(requestId, cancellationSource);
            try
            {
                _ambientCancellationToken.Value = cancellationSource.Token;
                try
                {
                    return await HandleAsync(request, cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return BuildErrorEnvelope(request, "Operation cancelled.");
                }
                finally
                {
                    _ambientCancellationToken.Value = default(CancellationToken);
                }
            }
            finally
            {
                UnregisterActiveRequest(requestId, cancellationSource);
            }
        }

        private Task WarmProjectCacheAsync(string[] projectFilePaths)
        {
            WarmProjectCache(projectFilePaths);
            return Task.CompletedTask;
        }

        private Task<LanguageServiceAnalysisResponse> AnalyzeDocumentAsync(LanguageServiceDocumentRequest request)
        {
            return Task.FromResult(AnalyzeDocument(request));
        }

        private Task<LanguageServiceHoverResponse> GetHoverAsync(LanguageServiceHoverRequest request)
        {
            return Task.FromResult(GetHover(request));
        }

        private Task<LanguageServiceDefinitionResponse> GoToDefinitionAsync(LanguageServiceDefinitionRequest request)
        {
            return Task.FromResult(GoToDefinition(request));
        }

        private Task<LanguageServiceCompletionResponse> GetCompletionAsync(LanguageServiceCompletionRequest request)
        {
            return Task.FromResult(GetCompletion(request));
        }

        private Task<LanguageServiceSignatureHelpResponse> GetSignatureHelpAsync(LanguageServiceSignatureHelpRequest request)
        {
            return Task.FromResult(GetSignatureHelp(request));
        }

        private Task<LanguageServiceSymbolContextResponse> GetSymbolContextAsync(LanguageServiceSymbolContextRequest request)
        {
            return Task.FromResult(GetSymbolContext(request));
        }

        private Task<LanguageServiceRenameResponse> PreviewRenameAsync(LanguageServiceRenameRequest request)
        {
            return Task.FromResult(PreviewRename(request));
        }

        private Task<LanguageServiceReferencesResponse> FindReferencesAsync(LanguageServiceReferencesRequest request)
        {
            return Task.FromResult(FindReferences(request));
        }

        private Task<LanguageServiceBaseSymbolResponse> GetBaseSymbolsAsync(LanguageServiceBaseSymbolRequest request)
        {
            return Task.FromResult(GetBaseSymbols(request));
        }

        private Task<LanguageServiceImplementationResponse> GetImplementationsAsync(LanguageServiceImplementationRequest request)
        {
            return Task.FromResult(GetImplementations(request));
        }

        private Task<LanguageServiceCallHierarchyResponse> GetCallHierarchyAsync(LanguageServiceCallHierarchyRequest request)
        {
            return Task.FromResult(GetCallHierarchy(request));
        }

        private Task<LanguageServiceValueSourceResponse> GetValueSourceAsync(LanguageServiceValueSourceRequest request)
        {
            return Task.FromResult(GetValueSource(request));
        }

        private Task<LanguageServiceDocumentTransformResponse> PreviewDocumentTransformAsync(LanguageServiceDocumentTransformRequest request)
        {
            return Task.FromResult(PreviewDocumentTransform(request));
        }

        public async Task<LanguageServiceEnvelope> HandleAsync(LanguageServiceEnvelope request, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var command = request != null ? request.Command : string.Empty;
                if (string.Equals(command, LanguageServiceCommands.Initialize, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleInitializeAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Status, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleStatus(request);
                }

                if (string.Equals(command, LanguageServiceCommands.AnalyzeDocument, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleAnalyzeDocumentAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Hover, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleHoverAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.GoToDefinition, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleGoToDefinitionAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Completion, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleCompletionAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.SignatureHelp, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleSignatureHelpAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.SymbolContext, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleSymbolContextAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.RenamePreview, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleRenamePreviewAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.FindReferences, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleReferencesAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.GoToBase, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleGoToBaseAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.GoToImplementation, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleGoToImplementationAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.CallHierarchy, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleCallHierarchyAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.ValueSource, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleValueSourceAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.DocumentTransformPreview, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleDocumentTransformPreviewAsync(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Shutdown, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildSuccessEnvelope(request, new LanguageServiceOperationResponse
                    {
                        Success = true,
                        StatusMessage = "Roslyn worker shutting down."
                    });
                }

                return BuildErrorEnvelope(request, "Unknown language service command '" + command + "'.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return BuildErrorEnvelope(request, ex.Message);
            }
        }

        private LanguageServiceEnvelope HandleCancel(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCancelRequest>(request);
            var targetRequestId = payload != null ? payload.TargetRequestId ?? string.Empty : string.Empty;
            var cancelled = TryCancelRequest(targetRequestId);
            return BuildSuccessEnvelope(request, new LanguageServiceCancelResponse
            {
                Success = cancelled,
                StatusMessage = cancelled
                    ? "Cancellation requested."
                    : "The target request was not active.",
                TargetRequestId = targetRequestId,
                Cancelled = cancelled
            });
        }

        private async Task<LanguageServiceEnvelope> HandleInitializeAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceInitializeRequest>(request);
            _workspaceRootPath = payload.WorkspaceRootPath ?? string.Empty;
            _sourceRoots = payload.SourceRoots ?? new string[0];
            _projectFilePaths = payload.ProjectFilePaths ?? new string[0];
            _solutionFilePaths = payload.SolutionFilePaths ?? new string[0];
            _referenceAssemblyPaths = payload.ReferenceAssemblyPaths ?? new string[0];
            _documentContextCache.Clear();
            _documentProjectPathCache.Clear();
            await WarmProjectCacheAsync(_projectFilePaths);
            Console.Error.WriteLine("Initialized Roslyn worker. WorkspaceRoot=" + _workspaceRootPath +
                ", Projects=" + _projectFilePaths.Length +
                ", SourceRoots=" + _sourceRoots.Length +
                ", References=" + _referenceAssemblyPaths.Length + ".");

            return BuildSuccessEnvelope(request, new LanguageServiceInitializeResponse
            {
                Success = true,
                StatusMessage = "Roslyn language service initialized.",
                WorkerName = "Cortex.Roslyn.Worker",
                WorkerVersion = Assembly.GetExecutingAssembly().GetName().Version != null
                    ? Assembly.GetExecutingAssembly().GetName().Version.ToString()
                    : "1.0.0",
                RuntimeVersion = Environment.Version.ToString(),
                Capabilities = BuildCapabilities()
            });
        }

        private LanguageServiceEnvelope HandleStatus(LanguageServiceEnvelope request)
        {
            return BuildSuccessEnvelope(request, new LanguageServiceStatusResponse
            {
                Success = true,
                StatusMessage = "Roslyn worker ready.",
                WorkerName = "Cortex.Roslyn.Worker",
                WorkerVersion = Assembly.GetExecutingAssembly().GetName().Version != null
                    ? Assembly.GetExecutingAssembly().GetName().Version.ToString()
                    : "1.0.0",
                RuntimeVersion = Environment.Version.ToString(),
                Capabilities = BuildCapabilities(),
                CachedProjectCount = GetCachedProjectCount(),
                LoadedProjectPaths = GetLoadedProjectPaths(),
                IsRunning = true
            });
        }

        private async Task<LanguageServiceEnvelope> HandleAnalyzeDocumentAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDocumentRequest>(request);
            return BuildSuccessEnvelope(request, await AnalyzeDocumentAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleHoverAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceHoverRequest>(request);
            return BuildSuccessEnvelope(request, await GetHoverAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleGoToDefinitionAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDefinitionRequest>(request);
            return BuildSuccessEnvelope(request, await GoToDefinitionAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleCompletionAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCompletionRequest>(request);
            return BuildSuccessEnvelope(request, await GetCompletionAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleSignatureHelpAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceSignatureHelpRequest>(request);
            return BuildSuccessEnvelope(request, await GetSignatureHelpAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleSymbolContextAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceSymbolContextRequest>(request);
            return BuildSuccessEnvelope(request, await GetSymbolContextAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleRenamePreviewAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceRenameRequest>(request);
            return BuildSuccessEnvelope(request, await PreviewRenameAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleReferencesAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceReferencesRequest>(request);
            return BuildSuccessEnvelope(request, await FindReferencesAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleGoToBaseAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceBaseSymbolRequest>(request);
            return BuildSuccessEnvelope(request, await GetBaseSymbolsAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleGoToImplementationAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceImplementationRequest>(request);
            return BuildSuccessEnvelope(request, await GetImplementationsAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleCallHierarchyAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCallHierarchyRequest>(request);
            return BuildSuccessEnvelope(request, await GetCallHierarchyAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleValueSourceAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceValueSourceRequest>(request);
            return BuildSuccessEnvelope(request, await GetValueSourceAsync(payload));
        }

        private async Task<LanguageServiceEnvelope> HandleDocumentTransformPreviewAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDocumentTransformRequest>(request);
            return BuildSuccessEnvelope(request, await PreviewDocumentTransformAsync(payload));
        }

        private T DeserializePayload<T>(LanguageServiceEnvelope request) where T : class, new()
        {
            if (request == null || string.IsNullOrEmpty(request.PayloadJson))
            {
                return new T();
            }

            return ManualJson.Deserialize<T>(request.PayloadJson) ?? new T();
        }

        private LanguageServiceEnvelope BuildSuccessEnvelope(LanguageServiceEnvelope request, object payload)
        {
            return new LanguageServiceEnvelope
            {
                RequestId = request != null ? request.RequestId : string.Empty,
                Command = request != null ? request.Command : string.Empty,
                Success = true,
                PayloadJson = ManualJson.Serialize(payload),
                ErrorMessage = string.Empty
            };
        }

        private static LanguageServiceEnvelope BuildErrorEnvelope(LanguageServiceEnvelope request, string message)
        {
            return new LanguageServiceEnvelope
            {
                RequestId = request != null ? request.RequestId : string.Empty,
                Command = request != null ? request.Command : string.Empty,
                Success = false,
                PayloadJson = string.Empty,
                ErrorMessage = message ?? "Roslyn worker failed."
            };
        }

        private void RegisterActiveRequest(string requestId, CancellationTokenSource cancellationSource)
        {
            if (string.IsNullOrEmpty(requestId) || cancellationSource == null)
            {
                return;
            }

            lock (_requestSync)
            {
                _activeRequests[requestId] = cancellationSource;
            }
        }

        private void UnregisterActiveRequest(string requestId, CancellationTokenSource cancellationSource)
        {
            if (!string.IsNullOrEmpty(requestId))
            {
                lock (_requestSync)
                {
                    _activeRequests.Remove(requestId);
                }
            }

            if (cancellationSource != null)
            {
                cancellationSource.Dispose();
            }
        }

        private bool TryCancelRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            lock (_requestSync)
            {
                CancellationTokenSource cancellationSource;
                if (!_activeRequests.TryGetValue(requestId, out cancellationSource) || cancellationSource == null)
                {
                    return false;
                }

                cancellationSource.Cancel();
                return true;
            }
        }

        private void CancelAllActiveRequests()
        {
            lock (_requestSync)
            {
                foreach (var pair in _activeRequests)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.Cancel();
                    }
                }
            }
        }

        private static CancellationToken CurrentCancellationToken
        {
            get { return _ambientCancellationToken.Value; }
        }

        private static void ThrowIfCancellationRequested()
        {
            if (CurrentCancellationToken.CanBeCanceled)
            {
                CurrentCancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static string[] BuildCapabilities()
        {
            return new[]
            {
                "classifications",
                "diagnostics",
                "hover",
                "definition",
                "completion",
                "signature-help",
                "symbol-context",
                "rename",
                "references",
                "base-symbol",
                "implementations",
                "call-hierarchy",
                "value-source",
                "document-transforms"
            };
        }
    }
}

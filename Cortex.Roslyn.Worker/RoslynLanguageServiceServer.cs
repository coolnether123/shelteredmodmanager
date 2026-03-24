using System;
using System.Collections.Generic;
using System.Reflection;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;
using Microsoft.CodeAnalysis;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
        private string _workspaceRootPath = string.Empty;
        private string[] _sourceRoots = new string[0];
        private string[] _projectFilePaths = new string[0];
        private string[] _solutionFilePaths = new string[0];
        private string[] _referenceAssemblyPaths = new string[0];
        private readonly Dictionary<string, PortableExecutableReference> _metadataReferenceCache = new Dictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);

        public RoslynLanguageServiceServer()
        {
            RegisterMsBuild();
        }

        public LanguageServiceEnvelope Handle(LanguageServiceEnvelope request)
        {
            return HandleAsync(request).GetAwaiter().GetResult();
        }

        private System.Threading.Tasks.Task WarmProjectCacheAsync(string[] projectFilePaths)
        {
            WarmProjectCache(projectFilePaths);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task<LanguageServiceAnalysisResponse> AnalyzeDocumentAsync(LanguageServiceDocumentRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(AnalyzeDocument(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceHoverResponse> GetHoverAsync(LanguageServiceHoverRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetHover(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceDefinitionResponse> GoToDefinitionAsync(LanguageServiceDefinitionRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GoToDefinition(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceCompletionResponse> GetCompletionAsync(LanguageServiceCompletionRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetCompletion(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceSignatureHelpResponse> GetSignatureHelpAsync(LanguageServiceSignatureHelpRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetSignatureHelp(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceSymbolContextResponse> GetSymbolContextAsync(LanguageServiceSymbolContextRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetSymbolContext(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceRenameResponse> PreviewRenameAsync(LanguageServiceRenameRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(PreviewRename(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceReferencesResponse> FindReferencesAsync(LanguageServiceReferencesRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(FindReferences(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceBaseSymbolResponse> GetBaseSymbolsAsync(LanguageServiceBaseSymbolRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetBaseSymbols(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceImplementationResponse> GetImplementationsAsync(LanguageServiceImplementationRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetImplementations(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceCallHierarchyResponse> GetCallHierarchyAsync(LanguageServiceCallHierarchyRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetCallHierarchy(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceValueSourceResponse> GetValueSourceAsync(LanguageServiceValueSourceRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(GetValueSource(request));
        }

        private System.Threading.Tasks.Task<LanguageServiceDocumentTransformResponse> PreviewDocumentTransformAsync(LanguageServiceDocumentTransformRequest request)
        {
            return System.Threading.Tasks.Task.FromResult(PreviewDocumentTransform(request));
        }

        public async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleAsync(LanguageServiceEnvelope request)
        {
            try
            {
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

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleInitializeAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceInitializeRequest>(request);
            _workspaceRootPath = payload.WorkspaceRootPath ?? string.Empty;
            _sourceRoots = payload.SourceRoots ?? new string[0];
            _projectFilePaths = payload.ProjectFilePaths ?? new string[0];
            _solutionFilePaths = payload.SolutionFilePaths ?? new string[0];
            _referenceAssemblyPaths = payload.ReferenceAssemblyPaths ?? new string[0];
            _documentContextCache.Clear();
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

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleAnalyzeDocumentAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDocumentRequest>(request);
            return BuildSuccessEnvelope(request, await AnalyzeDocumentAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleHoverAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceHoverRequest>(request);
            return BuildSuccessEnvelope(request, await GetHoverAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleGoToDefinitionAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDefinitionRequest>(request);
            return BuildSuccessEnvelope(request, await GoToDefinitionAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleCompletionAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCompletionRequest>(request);
            return BuildSuccessEnvelope(request, await GetCompletionAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleSignatureHelpAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceSignatureHelpRequest>(request);
            return BuildSuccessEnvelope(request, await GetSignatureHelpAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleSymbolContextAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceSymbolContextRequest>(request);
            return BuildSuccessEnvelope(request, await GetSymbolContextAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleRenamePreviewAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceRenameRequest>(request);
            return BuildSuccessEnvelope(request, await PreviewRenameAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleReferencesAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceReferencesRequest>(request);
            return BuildSuccessEnvelope(request, await FindReferencesAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleGoToBaseAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceBaseSymbolRequest>(request);
            return BuildSuccessEnvelope(request, await GetBaseSymbolsAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleGoToImplementationAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceImplementationRequest>(request);
            return BuildSuccessEnvelope(request, await GetImplementationsAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleCallHierarchyAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCallHierarchyRequest>(request);
            return BuildSuccessEnvelope(request, await GetCallHierarchyAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleValueSourceAsync(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceValueSourceRequest>(request);
            return BuildSuccessEnvelope(request, await GetValueSourceAsync(payload));
        }

        private async System.Threading.Tasks.Task<LanguageServiceEnvelope> HandleDocumentTransformPreviewAsync(LanguageServiceEnvelope request)
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

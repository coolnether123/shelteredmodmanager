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
            try
            {
                var command = request != null ? request.Command : string.Empty;
                if (string.Equals(command, LanguageServiceCommands.Initialize, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleInitialize(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Status, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleStatus(request);
                }

                if (string.Equals(command, LanguageServiceCommands.AnalyzeDocument, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleAnalyzeDocument(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Hover, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleHover(request);
                }

                if (string.Equals(command, LanguageServiceCommands.GoToDefinition, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleGoToDefinition(request);
                }

                if (string.Equals(command, LanguageServiceCommands.Completion, StringComparison.OrdinalIgnoreCase))
                {
                    return HandleCompletion(request);
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

        private LanguageServiceEnvelope HandleInitialize(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceInitializeRequest>(request);
            _workspaceRootPath = payload.WorkspaceRootPath ?? string.Empty;
            _sourceRoots = payload.SourceRoots ?? new string[0];
            _projectFilePaths = payload.ProjectFilePaths ?? new string[0];
            _solutionFilePaths = payload.SolutionFilePaths ?? new string[0];
            _referenceAssemblyPaths = payload.ReferenceAssemblyPaths ?? new string[0];
            _documentContextCache.Clear();
            WarmProjectCache(_projectFilePaths);
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
                Capabilities = new[]
                {
                    "classifications",
                    "diagnostics",
                    "hover",
                    "definition",
                    "completion"
                }
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
                Capabilities = new[]
                {
                    "classifications",
                    "diagnostics",
                    "hover",
                    "definition",
                    "completion"
                },
                CachedProjectCount = GetCachedProjectCount(),
                LoadedProjectPaths = GetLoadedProjectPaths(),
                IsRunning = true
            });
        }

        private LanguageServiceEnvelope HandleAnalyzeDocument(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDocumentRequest>(request);
            return BuildSuccessEnvelope(request, AnalyzeDocument(payload));
        }

        private LanguageServiceEnvelope HandleHover(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceHoverRequest>(request);
            return BuildSuccessEnvelope(request, GetHover(payload));
        }

        private LanguageServiceEnvelope HandleGoToDefinition(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceDefinitionRequest>(request);
            return BuildSuccessEnvelope(request, GoToDefinition(payload));
        }

        private LanguageServiceEnvelope HandleCompletion(LanguageServiceEnvelope request)
        {
            var payload = DeserializePayload<LanguageServiceCompletionRequest>(request);
            return BuildSuccessEnvelope(request, GetCompletion(payload));
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
    }
}

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
using GameModding.Shared.Serialization;
using ModAPI.Core;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private ILanguageServiceClient _languageServiceClient;
        private LanguageServiceInitializeRequest _languageServiceInitializeRequest;
        private string _lastAnalyzedDocumentFingerprint = string.Empty;
        private string _pendingLanguageAnalysisFingerprint = string.Empty;
        private bool _languageServiceReady;
        private bool _languageServiceInitializing;
        private bool _languageAnalysisInFlight;
        private bool _languageHoverInFlight;
        private bool _languageDefinitionInFlight;
        private string _languageInitializeRequestId = string.Empty;
        private string _languageStatusRequestId = string.Empty;
        private PendingLanguageAnalysisRequest _pendingLanguageAnalysis;
        private PendingLanguageHoverRequest _pendingLanguageHover;
        private PendingLanguageDefinitionRequest _pendingLanguageDefinition;
        private int _languageServiceGeneration;
        private DateTime _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
        private const double LanguageAnalysisDebounceMs = 280d;

        private void InitializeLanguageService(string smmBin, CortexSettings settings)
        {
            ShutdownLanguageService();

            if (settings == null || !settings.EnableRoslynLanguageService)
            {
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

            _languageServiceClient = new RoslynLanguageServiceClient(workerPath, settings.RoslynServiceTimeoutMs);
            _languageServiceInitializeRequest = BuildLanguageInitializeRequest();
            _lastAnalyzedDocumentFingerprint = string.Empty;
            _pendingLanguageAnalysisFingerprint = string.Empty;
            _languageServiceReady = false;
            _languageServiceInitializing = false;
            _languageAnalysisInFlight = false;
            _languageHoverInFlight = false;
            _languageDefinitionInFlight = false;
            _languageInitializeRequestId = string.Empty;
            _languageStatusRequestId = string.Empty;
            _pendingLanguageAnalysis = null;
            _pendingLanguageHover = null;
            _pendingLanguageDefinition = null;
            _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
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
            if (_languageServiceClient == null || _languageServiceReady || _languageServiceInitializing)
            {
                return;
            }

            if (!_visible)
            {
                return;
            }

            var generation = _languageServiceGeneration;
            _languageServiceInitializing = true;
            MMLog.WriteInfo("[Cortex.Roslyn] Starting language service worker in background.");
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
            }
        }

        private void ShutdownLanguageService()
        {
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
            _languageInitializeRequestId = string.Empty;
            _languageStatusRequestId = string.Empty;
            _pendingLanguageAnalysis = null;
            _pendingLanguageHover = null;
            _pendingLanguageDefinition = null;
            _lastLanguageAnalysisRequestUtc = DateTime.MinValue;
        }

        private void UpdateLanguageService()
        {
            EnsureLanguageServiceStarted();
            ProcessLanguageResponses();
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

            var active = _state.Documents.ActiveDocument;
            if (active == null || string.IsNullOrEmpty(active.FilePath))
            {
                _lastAnalyzedDocumentFingerprint = string.Empty;
                _pendingLanguageAnalysisFingerprint = string.Empty;
                return;
            }

            var fingerprint = BuildLanguageFingerprint(active);
            if (string.Equals(fingerprint, _lastAnalyzedDocumentFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            if (_languageAnalysisInFlight || string.Equals(fingerprint, _pendingLanguageAnalysisFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            if ((DateTime.UtcNow - active.LastTextMutationUtc).TotalMilliseconds < LanguageAnalysisDebounceMs ||
                (DateTime.UtcNow - _lastLanguageAnalysisRequestUtc).TotalMilliseconds < 100d)
            {
                return;
            }

            var request = BuildLanguageDocumentRequest(active, true, true);
            var generation = _languageServiceGeneration;
            var client = _languageServiceClient;
            _languageAnalysisInFlight = true;
            _pendingLanguageAnalysisFingerprint = fingerprint;
            _lastLanguageAnalysisRequestUtc = DateTime.UtcNow;
            var requestId = client != null ? client.QueueAnalyzeDocument(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageAnalysisInFlight = false;
                _pendingLanguageAnalysisFingerprint = string.Empty;
                return;
            }

            _pendingLanguageAnalysis = new PendingLanguageAnalysisRequest
            {
                RequestId = requestId,
                Generation = generation,
                Fingerprint = fingerprint,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion
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

            var request = BuildLanguageHoverRequest(session, _state.Editor.RequestedHoverLine, _state.Editor.RequestedHoverColumn);
            var client = _languageServiceClient;
            var generation = _languageServiceGeneration;
            var hoverKey = requestKey;
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            _languageHoverInFlight = true;
            var requestId = client != null ? client.QueueHover(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                _languageHoverInFlight = false;
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

            var request = BuildLanguageDefinitionRequest(session, _state.Editor.RequestedDefinitionLine, _state.Editor.RequestedDefinitionColumn);
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
                }
            }
        }

        private void HandleLanguageInitializeResponse(LanguageServiceEnvelope envelope)
        {
            _languageInitializeRequestId = string.Empty;
            _languageServiceInitializing = false;
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

        private void HandleLanguageAnalysisResponse(LanguageServiceEnvelope envelope, PendingLanguageAnalysisRequest pending)
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

            target.LanguageAnalysis = response;
            target.LastLanguageAnalysisUtc = DateTime.UtcNow;
            target.LastLanguageAnalysisVersion = response.DocumentVersion;
            _lastAnalyzedDocumentFingerprint = pending.Fingerprint ?? string.Empty;
            MMLog.WriteInfo("[Cortex.Roslyn] Analysis complete for " +
                Path.GetFileName(target.FilePath) +
                ". Diagnostics=" + CountDiagnostics(response) +
                ", Classifications=" + CountClassifications(response) +
                ", Summary=" + BuildClassificationSummary(response));

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
            if (response.Success)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Hover resolved for " + (_state.Editor.RequestedHoverTokenText ?? string.Empty) + ".");
            }
        }

        private void HandleLanguageDefinitionResponse(LanguageServiceEnvelope envelope, PendingLanguageDefinitionRequest pending)
        {
            _languageDefinitionInFlight = false;
            if (pending == null || pending.Generation != _languageServiceGeneration)
            {
                return;
            }

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

            if (!string.IsNullOrEmpty(response.DocumentPath) && File.Exists(response.DocumentPath))
            {
                var opened = CortexModuleUtil.OpenDocument(_documentService, _state, response.DocumentPath, response.Range != null ? response.Range.StartLine : 1);
                if (opened != null)
                {
                    opened.HighlightedLine = response.Range != null ? response.Range.StartLine : 1;
                }

                _state.StatusMessage = "Opened definition: " + (response.SymbolDisplay ?? Path.GetFileName(response.DocumentPath));
                MMLog.WriteInfo("[Cortex.Roslyn] Opened definition for " + (pending.TokenText ?? string.Empty) + " -> " + response.DocumentPath);
                return;
            }

            if (TryOpenMetadataDefinition(response))
            {
                return;
            }

            _state.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : "Definition was not found.";
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
            if (response == null || _sourceReferenceService == null || _documentService == null)
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback was unavailable because a required Cortex service was null.");
                return false;
            }

            string assemblyPath;
            if (!TryResolveAssemblyPath(response.ContainingAssemblyName, out assemblyPath))
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not resolve assembly '" + (response.ContainingAssemblyName ?? string.Empty) + "'.");
                return false;
            }

            int metadataToken;
            DecompilerEntityKind entityKind;
            if (!TryResolveMetadataTarget(response, assemblyPath, out metadataToken, out entityKind))
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not resolve token for '" +
                    (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) +
                    "' in " + assemblyPath + ".");
                return false;
            }

            var decompiled = CortexModuleUtil.RequestDecompilerSource(
                _sourceReferenceService,
                _state,
                assemblyPath,
                metadataToken,
                entityKind,
                false);

            if (decompiled == null || !CortexModuleUtil.OpenDecompilerResult(_documentService, _state, decompiled))
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Metadata definition fallback could not open decompiled output for '" +
                    (response.SymbolDisplay ?? response.MetadataName ?? string.Empty) +
                    "' from " + assemblyPath + ".");
                return false;
            }

            _state.StatusMessage = "Opened decompiled definition: " + (response.SymbolDisplay ?? response.MetadataName ?? Path.GetFileName(assemblyPath));
            MMLog.WriteInfo("[Cortex.Roslyn] Opened metadata definition for " +
                (_state.Editor.RequestedDefinitionTokenText ?? string.Empty) +
                " -> " + assemblyPath + " token 0x" + metadataToken.ToString("X8"));
            return true;
        }

        private bool TryResolveAssemblyPath(string assemblyName, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var assembly = loadedAssemblies[i];
                if (assembly == null || !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    assemblyPath = assembly.Location;
                }
                catch
                {
                    assemblyPath = string.Empty;
                }

                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                {
                    return true;
                }
            }

            var searchRoots = new List<string>();
            AddSearchRoot(searchRoots, AppDomain.CurrentDomain.BaseDirectory);
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            var smmRoot = Path.Combine(baseDirectory, "SMM");
            var smmBinRoot = Path.Combine(smmRoot, "bin");
            AddSearchRoot(searchRoots, smmRoot);
            AddSearchRoot(searchRoots, smmBinRoot);
            AddSearchRoot(searchRoots, Path.Combine(smmBinRoot, "decompiler"));
            if (_state.Settings != null)
            {
                AddSearchRoot(searchRoots, _state.Settings.WorkspaceRootPath);
                if (!string.IsNullOrEmpty(_state.Settings.ManagedAssemblyRootPath))
                {
                    AddSearchRoot(searchRoots, _state.Settings.ManagedAssemblyRootPath);
                }

                if (!string.IsNullOrEmpty(_state.Settings.ModsRootPath))
                {
                    AddSearchRoot(searchRoots, _state.Settings.ModsRootPath);
                }

                if (!string.IsNullOrEmpty(_state.Settings.DecompilerCachePath))
                {
                    AddSearchRoot(searchRoots, _state.Settings.DecompilerCachePath);
                }
            }

            for (var i = 0; i < searchRoots.Count; i++)
            {
                var candidate = Path.Combine(searchRoots[i], assemblyName + ".dll");
                if (File.Exists(candidate))
                {
                    assemblyPath = candidate;
                    return true;
                }
            }

            for (var i = 0; i < searchRoots.Count; i++)
            {
                try
                {
                    var matches = Directory.GetFiles(searchRoots[i], assemblyName + ".dll", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        assemblyPath = matches[0];
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static void AddSearchRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root);
                if (!Directory.Exists(fullPath))
                {
                    return;
                }

                for (var i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                roots.Add(fullPath);
            }
            catch
            {
            }
        }

        private bool TryResolveMetadataTarget(LanguageServiceDefinitionResponse response, string assemblyPath, out int metadataToken, out DecompilerEntityKind entityKind)
        {
            metadataToken = 0;
            entityKind = DecompilerEntityKind.Type;

            var assembly = LoadAssemblyForMetadata(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var documentationCommentId = response.DocumentationCommentId ?? string.Empty;
            var containingTypeName = NormalizeMetadataTypeName(response.ContainingTypeName);
            var symbolKind = response.SymbolKind ?? string.Empty;

            if (IsTypeLikeSymbol(symbolKind))
            {
                var type = ResolveTypeByName(assembly, !string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("T:", StringComparison.Ordinal) ? documentationCommentId.Substring(2) : containingTypeName);
                if (type == null)
                {
                    return false;
                }

                metadataToken = type.MetadataToken;
                entityKind = DecompilerEntityKind.Type;
                return true;
            }

            if (!string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("M:", StringComparison.Ordinal))
            {
                var method = ResolveMethodByDocumentationId(assembly, documentationCommentId);
                if (method != null)
                {
                    metadataToken = method.MetadataToken;
                    entityKind = DecompilerEntityKind.Method;
                    return true;
                }
            }

            var containingType = ResolveTypeByName(assembly, containingTypeName);
            if (containingType == null)
            {
                return false;
            }

            metadataToken = containingType.MetadataToken;
            entityKind = DecompilerEntityKind.Type;
            return true;
        }

        private static Assembly LoadAssemblyForMetadata(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(loadedAssemblies[i].Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAssemblies[i];
                    }
                }
                catch
                {
                }
            }

            try
            {
                return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static Type ResolveTypeByName(Assembly assembly, string typeName)
        {
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var normalized = NormalizeMetadataTypeName(typeName);
            try
            {
                var direct = assembly.GetType(normalized, false);
                if (direct != null)
                {
                    return direct;
                }

                var allTypes = assembly.GetTypes();
                for (var i = 0; i < allTypes.Length; i++)
                {
                    var type = allTypes[i];
                    if (type != null &&
                        string.Equals((type.FullName ?? type.Name).Replace('+', '.'), normalized, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId)
        {
            if (assembly == null || string.IsNullOrEmpty(documentationCommentId))
            {
                return null;
            }

            try
            {
                var allTypes = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < allTypes.Length; typeIndex++)
                {
                    var type = allTypes[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    var methods = type.GetMethods(flags);
                    for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(methods[methodIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return methods[methodIndex];
                        }
                    }

                    var constructors = type.GetConstructors(flags);
                    for (var ctorIndex = 0; ctorIndex < constructors.Length; ctorIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(constructors[ctorIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return constructors[ctorIndex];
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string BuildMethodDocumentationId(MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append("M:");
            builder.Append(GetXmlTypeName(method.DeclaringType));
            builder.Append(".");
            builder.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                builder.Append("(");
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(parameters[i].ParameterType));
                }
                builder.Append(")");
            }

            return builder.ToString();
        }

        private static string GetXmlTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsByRef)
            {
                return GetXmlTypeName(type.GetElementType()) + "@";
            }

            if (type.IsArray)
            {
                return GetXmlTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var baseName = (genericType.FullName ?? genericType.Name).Replace('+', '.');
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    baseName = baseName.Substring(0, tickIndex);
                }

                var args = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(baseName);
                builder.Append("{");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(args[i]));
                }
                builder.Append("}");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string NormalizeMetadataTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace("global::", string.Empty).Replace('+', '.');
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Namespace", StringComparison.OrdinalIgnoreCase);
        }

        private DocumentSession FindOpenDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                if (session != null && string.Equals(session.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }

            return null;
        }

        private LanguageServiceInitializeRequest BuildLanguageInitializeRequest()
        {
            var projects = _projectCatalog != null ? _projectCatalog.GetProjects() : new List<CortexProjectDefinition>();
            var projectPaths = new List<string>();
            for (var i = 0; i < projects.Count; i++)
            {
                var projectPath = projects[i] != null ? projects[i].ProjectFilePath : string.Empty;
                if (!string.IsNullOrEmpty(projectPath) && File.Exists(projectPath))
                {
                    projectPaths.Add(projectPath);
                }
            }

            return new LanguageServiceInitializeRequest
            {
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, _state.SelectedProject),
                ProjectFilePaths = projectPaths.ToArray(),
                SolutionFilePaths = new string[0]
            };
        }

        private LanguageServiceDocumentRequest BuildLanguageDocumentRequest(DocumentSession session, bool includeDiagnostics, bool includeClassifications)
        {
            var project = ResolveProjectForDocument(session != null ? session.FilePath : string.Empty);
            return new LanguageServiceDocumentRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, project),
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                IncludeDiagnostics = includeDiagnostics,
                IncludeClassifications = includeClassifications
            };
        }

        private LanguageServiceHoverRequest BuildLanguageHoverRequest(DocumentSession session, int line, int column)
        {
            var project = ResolveProjectForDocument(session != null ? session.FilePath : string.Empty);
            return new LanguageServiceHoverRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, project),
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column
            };
        }

        private LanguageServiceDefinitionRequest BuildLanguageDefinitionRequest(DocumentSession session, int line, int column)
        {
            var project = ResolveProjectForDocument(session != null ? session.FilePath : string.Empty);
            return new LanguageServiceDefinitionRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, project),
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column
            };
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
            var roots = new List<string>();
            AddLanguageRoot(roots, project != null ? project.SourceRootPath : string.Empty);
            AddLanguageRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            AddLanguageRoot(roots, settings != null ? settings.ModsRootPath : string.Empty);
            AddLanguageRoot(roots, settings != null ? settings.ManagedAssemblyRootPath : string.Empty);

            var additional = settings != null ? settings.AdditionalSourceRoots : string.Empty;
            if (!string.IsNullOrEmpty(additional))
            {
                var segments = additional.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    AddLanguageRoot(roots, segments[i]);
                }
            }

            return roots.ToArray();
        }

        private static void AddLanguageRoot(List<string> roots, string path)
        {
            if (roots == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path.Trim());
                if ((Directory.Exists(fullPath) || File.Exists(fullPath)) && !roots.Contains(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
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

            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.exe"));
            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.dll"));

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

        private sealed class PendingLanguageAnalysisRequest
        {
            public string RequestId;
            public int Generation;
            public string Fingerprint;
            public string DocumentPath;
            public int DocumentVersion;
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

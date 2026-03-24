using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
namespace Cortex
{
    internal sealed class CortexShellLanguageRequestDispatcher
    {
        public void ProcessDocumentLanguageAnalysis(CortexShellLanguageRuntimeContext context)
        {
            var active = context != null ? context.State.Documents.ActiveDocument : null;
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null || active == null || string.IsNullOrEmpty(active.FilePath))
            {
                return;
            }

            context.DocumentLanguageAnalysisService.TryRestoreFromRecentCache(active, null);
            var fingerprint = context.BuildLanguageFingerprint(active);
            var needsClassifications = active.LastLanguageClassificationVersion != active.TextVersion;
            var needsDiagnostics = active.LastLanguageDiagnosticVersion != active.TextVersion;
            if (!needsClassifications && !needsDiagnostics)
            {
                return;
            }

            var includeClassifications = needsClassifications;
            var includeDiagnostics = !needsClassifications && needsDiagnostics;
            var analysisWorkKey = context.DocumentLanguageAnalysisService.BuildAnalysisWorkKey(fingerprint, includeDiagnostics, includeClassifications);
            if (runtime.AnalysisInFlight || string.Equals(analysisWorkKey, runtime.PendingLanguageAnalysisFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            if ((DateTime.UtcNow - active.LastTextMutationUtc).TotalMilliseconds < context.LanguageAnalysisDebounceMs ||
                (DateTime.UtcNow - runtime.LastAnalysisRequestUtc).TotalMilliseconds < 100d)
            {
                return;
            }

            var classificationRange = includeClassifications ? context.DocumentLanguageAnalysisService.BuildIncrementalClassificationRange(active) : null;
            var project = context.ResolveProjectForDocument(active.FilePath);
            var request = context.DocumentLanguageAnalysisService.BuildDocumentRequest(
                active,
                context.State.Settings,
                project,
                context.BuildLanguageSourceRoots(context.State.Settings, project),
                includeDiagnostics,
                includeClassifications,
                classificationRange);
            var client = context.LanguageServiceClient;
            runtime.AnalysisInFlight = true;
            runtime.PendingLanguageAnalysisFingerprint = analysisWorkKey;
            runtime.LastAnalysisRequestUtc = DateTime.UtcNow;
            var requestId = client != null ? client.QueueAnalyzeDocument(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.AnalysisInFlight = false;
                runtime.PendingLanguageAnalysisFingerprint = string.Empty;
                return;
            }

            runtime.PendingAnalysis = new DocumentLanguageAnalysisRequestState
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
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

        public void UpdateLanguageHover(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null || runtime.HoverInFlight || context.State.Editor == null)
            {
                return;
            }

            var requestKey = context.State.Editor.RequestedHoverKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey) || string.Equals(requestKey, context.State.Editor.ActiveHoverKey, StringComparison.Ordinal))
            {
                return;
            }

            var session = context.FindOpenDocument(context.State.Editor.RequestedHoverDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.DocumentLanguageInteractionService.BuildHoverRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                context.State.Editor.RequestedHoverLine,
                context.State.Editor.RequestedHoverColumn,
                context.State.Editor.RequestedHoverAbsolutePosition);
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            runtime.HoverInFlight = true;
            MMLog.WriteDebug("[Cortex.Roslyn] Queueing hover for " +
                (context.State.Editor.RequestedHoverTokenText ?? string.Empty) +
                " @ " + context.State.Editor.RequestedHoverLine + ":" + context.State.Editor.RequestedHoverColumn +
                " in " + Path.GetFileName(requestDocumentPath) + ".");
            var requestId = context.LanguageServiceClient != null ? context.LanguageServiceClient.QueueHover(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.HoverInFlight = false;
                MMLog.WriteWarning("[Cortex.Roslyn] Failed to queue hover for " +
                    (context.State.Editor.RequestedHoverTokenText ?? string.Empty) +
                    ": " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available."));
                return;
            }

            runtime.PendingHover = new PendingLanguageHoverRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                HoverKey = requestKey,
                DocumentPath = requestDocumentPath,
                DocumentVersion = requestDocumentVersion
            };
        }

        public void UpdateLanguageDefinition(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null || runtime.DefinitionInFlight || context.State.Editor == null)
            {
                return;
            }

            var requestKey = context.State.Editor.RequestedDefinitionKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(context.State.Editor.RequestedDefinitionDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.DocumentLanguageInteractionService.BuildDefinitionRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                context.State.Editor.RequestedDefinitionLine,
                context.State.Editor.RequestedDefinitionColumn,
                context.State.Editor.RequestedDefinitionAbsolutePosition);
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            runtime.DefinitionInFlight = true;
            context.State.Editor.RequestedDefinitionKey = string.Empty;
            var requestId = context.LanguageServiceClient != null ? context.LanguageServiceClient.QueueGoToDefinition(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.DefinitionInFlight = false;
                context.State.StatusMessage = "Definition lookup failed: " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                return;
            }

            runtime.PendingDefinition = new PendingLanguageDefinitionRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                DocumentPath = requestDocumentPath,
                DocumentVersion = requestDocumentVersion,
                TokenText = context.State.Editor.RequestedDefinitionTokenText ?? string.Empty
            };
        }

        public void UpdateLanguageCompletion(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null)
            {
                return;
            }

            context.DispatchDeferredCompletionAugmentation();

            if (!context.EditorCompletionService.ShouldDispatch(context.State.Editor, runtime.CompletionInFlight))
            {
                return;
            }

            var session = context.FindOpenDocument(context.State.Editor.RequestedCompletionDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.EditorCompletionService.BuildWorkerRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                context.State.Editor);
            if (request == null)
            {
                return;
            }

            var requestKey = context.State.Editor.RequestedCompletionKey ?? string.Empty;
            var augmentationPending = new DocumentLanguageCompletionRequestState
            {
                Generation = runtime.ServiceGeneration,
                RequestKey = requestKey,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                AbsolutePosition = request.AbsolutePosition
            };
            var augmentationRequest = context.BuildCompletionAugmentationRequest(session, augmentationPending);
            var roslEnabled = context.LanguageServiceClient != null && runtime.ServiceReady && !runtime.ServiceInitializing;
            if (roslEnabled)
            {
                runtime.CompletionInFlight = true;
                var requestId = context.LanguageServiceClient.QueueCompletion(request);
                if (!string.IsNullOrEmpty(requestId))
                {
                    runtime.PendingCompletion = new DocumentLanguageCompletionRequestState
                    {
                        RequestId = requestId,
                        Generation = runtime.ServiceGeneration,
                        RequestKey = requestKey,
                        DocumentPath = request.DocumentPath ?? string.Empty,
                        DocumentVersion = request.DocumentVersion,
                        AbsolutePosition = request.AbsolutePosition
                    };
                    MMLog.WriteInfo("[Cortex.Completion] Roslyn completion queued. RequestId=" +
                        requestId +
                        ", Document=" + (request.DocumentPath ?? string.Empty) +
                        ", Position=" + request.AbsolutePosition + ".");
                    var augmentationQueued = context.TryQueueCompletionAugmentation(session, augmentationPending, augmentationRequest, null);
                    MMLog.WriteInfo("[Cortex.Completion] Tabby augmentation queued alongside Roslyn=" +
                        augmentationQueued +
                        ". RequestKey=" + requestKey +
                        ", Document=" + (request.DocumentPath ?? string.Empty) + ".");
                    return;
                }

                runtime.CompletionInFlight = false;
                MMLog.LogOnce("Cortex.Completion.DispatchFailed", delegate
                {
                    MMLog.WriteWarning("[Cortex.Completion] Cortex failed to queue a Roslyn completion request. Enable Debug logging to capture per-request failure details.");
                });
                MMLog.WriteInfo("[Cortex.Completion] QueueCompletion returned an empty request id. LastError=" +
                    (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError ?? string.Empty : "client unavailable") + ".");
            }

            if (!context.TryQueueCompletionAugmentation(session, augmentationPending, augmentationRequest, null))
            {
                context.EditorCompletionService.ClearPendingRequest(context.State.Editor);
            }
        }

        public void UpdateLanguageSignatureHelp(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null || runtime.SignatureHelpInFlight || context.State.Editor == null)
            {
                return;
            }

            if (!context.HasLanguageCapability("signature-help"))
            {
                return;
            }

            if (!context.EditorSignatureHelpService.ShouldDispatch(context.State.Editor, runtime.SignatureHelpInFlight))
            {
                return;
            }

            var session = context.FindOpenDocument(context.State.Editor.RequestedSignatureHelpDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.EditorSignatureHelpService.BuildWorkerRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                context.State.Editor);
            if (request == null)
            {
                return;
            }

            runtime.SignatureHelpInFlight = true;
            var requestId = context.LanguageServiceClient != null ? context.LanguageServiceClient.QueueSignatureHelp(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.SignatureHelpInFlight = false;
                return;
            }

            runtime.PendingSignatureHelp = new PendingLanguageSignatureHelpRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                RequestKey = context.State.Editor.RequestedSignatureHelpKey ?? string.Empty,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                AbsolutePosition = request.AbsolutePosition
            };
        }

        public void UpdateSemanticOperation(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            var semantic = context != null && context.State != null ? context.State.Semantic : null;
            if (context == null || runtime == null || semantic == null || runtime.SemanticOperationInFlight || semantic.RequestedKind == SemanticRequestKind.None)
            {
                return;
            }

            var requestKey = semantic.RequestedKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(semantic.RequestedDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var requestId = string.Empty;
            switch (semantic.RequestedKind)
            {
                case SemanticRequestKind.SymbolContext:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueSymbolContext(
                            context.DocumentLanguageInteractionService.BuildSymbolContextRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.RenamePreview:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueRenamePreview(
                            context.DocumentLanguageInteractionService.BuildRenameRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition,
                                semantic.RequestedNewName))
                        : string.Empty;
                    break;
                case SemanticRequestKind.References:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueReferences(
                            context.DocumentLanguageInteractionService.BuildReferencesRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.PeekDefinition:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueGoToDefinition(
                            context.DocumentLanguageInteractionService.BuildDefinitionRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.BaseSymbol:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueGoToBase(
                            context.DocumentLanguageInteractionService.BuildBaseSymbolRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.Implementations:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueGoToImplementation(
                            context.DocumentLanguageInteractionService.BuildImplementationRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.CallHierarchy:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueCallHierarchy(
                            context.DocumentLanguageInteractionService.BuildCallHierarchyRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.ValueSource:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueValueSource(
                            context.DocumentLanguageInteractionService.BuildValueSourceRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedLine,
                                semantic.RequestedColumn,
                                semantic.RequestedAbsolutePosition))
                        : string.Empty;
                    break;
                case SemanticRequestKind.DocumentTransformPreview:
                    requestId = context.LanguageServiceClient != null && context.HasLanguageCapability("document-transforms")
                        ? context.LanguageServiceClient.QueueDocumentTransformPreview(
                            context.DocumentLanguageInteractionService.BuildDocumentTransformRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                semantic.RequestedCommandId,
                                semantic.RequestedTitle,
                                semantic.RequestedApplyLabel,
                                semantic.RequestedOrganizeImports,
                                semantic.RequestedSimplifyNames,
                                semantic.RequestedFormatDocument))
                        : string.Empty;
                    break;
            }

            if (string.IsNullOrEmpty(requestId))
            {
                context.State.StatusMessage = "Semantic operation failed: " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                semantic.RequestedKey = string.Empty;
                semantic.RequestedKind = SemanticRequestKind.None;
                return;
            }

            runtime.SemanticOperationInFlight = true;
            runtime.PendingSemanticOperation = new PendingSemanticOperationRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                Kind = semantic.RequestedKind,
                RequestKey = requestKey,
                DocumentPath = session.FilePath ?? string.Empty,
                DocumentVersion = session.TextVersion,
                SymbolText = semantic.RequestedSymbolText ?? string.Empty,
                NewName = semantic.RequestedNewName ?? string.Empty
            };
            semantic.RequestedKey = string.Empty;
            semantic.RequestedKind = SemanticRequestKind.None;
        }

        public void UpdateMethodInspectorCallHierarchy(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            var inspector = context != null && context.State != null && context.State.Editor != null
                ? context.State.Editor.MethodInspector
                : null;
            var invocation = inspector != null ? inspector.Invocation : null;
            var target = invocation != null ? invocation.Target : null;
            if (context == null ||
                runtime == null ||
                inspector == null ||
                !inspector.IsVisible ||
                target == null ||
                runtime.MethodInspectorCallHierarchyInFlight ||
                string.IsNullOrEmpty(inspector.CallHierarchyRequestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(target.DocumentPath);
            if (session == null)
            {
                inspector.CallHierarchyRequestKey = string.Empty;
                inspector.CallHierarchyStatusMessage = "Incoming-call inference is not available because the source document is no longer open.";
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.DocumentLanguageInteractionService.BuildCallHierarchyRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                target.Line,
                target.Column,
                target.AbsolutePosition);
            var requestKey = inspector.CallHierarchyRequestKey ?? string.Empty;
            var requestId = context.LanguageServiceClient != null
                ? context.LanguageServiceClient.QueueCallHierarchy(request)
                : string.Empty;
            inspector.CallHierarchyRequestKey = string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                inspector.CallHierarchyStatusMessage = "Incoming-call inference failed: " +
                    (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                return;
            }

            runtime.MethodInspectorCallHierarchyInFlight = true;
            runtime.PendingMethodInspectorCallHierarchy = new PendingMethodInspectorCallHierarchyRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                RequestKey = requestKey,
                TargetKey = inspector.CallHierarchyTargetKey ?? string.Empty,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion
            };
        }
    }
}

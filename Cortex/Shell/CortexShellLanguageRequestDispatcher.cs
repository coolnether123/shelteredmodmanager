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

            var hoverState = context.State.Editor.Hover;
            var requestKey = hoverState.RequestedKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(hoverState.RequestedDocumentPath);
            if (session == null)
            {
                CortexDeveloperLog.WriteHoverPipelineStage(
                    "RequestDispatched",
                    false,
                    string.Empty,
                    string.Empty,
                    requestKey,
                    hoverState.RequestedContextKey ?? string.Empty,
                    hoverState.RequestedDocumentPath ?? string.Empty,
                    0,
                    hoverState.RequestedAbsolutePosition,
                    hoverState.RequestedTokenText ?? string.Empty,
                    "open-document-not-found");
                hoverState.RequestedKey = string.Empty;
                hoverState.RequestedContextKey = string.Empty;
                hoverState.RequestedDocumentPath = string.Empty;
                hoverState.RequestedLine = 0;
                hoverState.RequestedColumn = 0;
                hoverState.RequestedAbsolutePosition = -1;
                hoverState.RequestedTokenText = string.Empty;
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var request = context.DocumentLanguageInteractionService.BuildHoverRequest(
                session,
                context.State.Settings,
                project,
                sourceRoots,
                hoverState.RequestedLine,
                hoverState.RequestedColumn,
                hoverState.RequestedAbsolutePosition);
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            runtime.HoverInFlight = true;
            CortexDeveloperLog.WriteHoverPipelineStage(
                "RequestDispatched",
                true,
                string.Empty,
                string.Empty,
                requestKey,
                hoverState.RequestedContextKey ?? string.Empty,
                requestDocumentPath,
                requestDocumentVersion,
                hoverState.RequestedAbsolutePosition,
                hoverState.RequestedTokenText ?? string.Empty,
                "line=" + hoverState.RequestedLine + ",column=" + hoverState.RequestedColumn);
            var requestId = context.LanguageServiceClient != null ? context.LanguageServiceClient.QueueHover(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.HoverInFlight = false;
                CortexDeveloperLog.WriteHoverPipelineStage(
                    "RequestDispatched",
                    false,
                    string.Empty,
                    string.Empty,
                    requestKey,
                    hoverState.RequestedContextKey ?? string.Empty,
                    requestDocumentPath,
                    requestDocumentVersion,
                    hoverState.RequestedAbsolutePosition,
                    hoverState.RequestedTokenText ?? string.Empty,
                    context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "roslyn-client-unavailable");
                hoverState.RequestedContextKey = string.Empty;
                MMLog.WriteWarning("[Cortex.Roslyn] Failed to queue hover for " +
                    (hoverState.RequestedTokenText ?? string.Empty) +
                    ": " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available."));
                return;
            }

            runtime.PendingHover = new PendingLanguageHoverRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                ContextKey = hoverState.RequestedContextKey ?? string.Empty,
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

            var definitionState = context.State.Editor.Definition;
            var requestKey = definitionState.RequestedKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(definitionState.RequestedDocumentPath);
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
                definitionState.RequestedLine,
                definitionState.RequestedColumn,
                definitionState.RequestedAbsolutePosition);
            var requestDocumentPath = request.DocumentPath ?? string.Empty;
            var requestDocumentVersion = request.DocumentVersion;
            runtime.DefinitionInFlight = true;
            definitionState.RequestedKey = string.Empty;
            var requestId = context.LanguageServiceClient != null ? context.LanguageServiceClient.QueueGoToDefinition(request) : string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                runtime.DefinitionInFlight = false;
                definitionState.RequestedContextKey = string.Empty;
                context.State.StatusMessage = "Definition lookup failed: " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                return;
            }

            runtime.PendingDefinition = new PendingLanguageDefinitionRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                ContextKey = definitionState.RequestedContextKey ?? string.Empty,
                DocumentPath = requestDocumentPath,
                DocumentVersion = requestDocumentVersion,
                TokenText = definitionState.RequestedTokenText ?? string.Empty
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

            var completionState = context.State.Editor.Completion;
            if (!context.EditorCompletionService.ShouldDispatch(completionState, runtime.CompletionInFlight))
            {
                return;
            }

            var session = context.FindOpenDocument(completionState.RequestedDocumentPath);
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
                completionState);
            if (request == null)
            {
                return;
            }

            var requestKey = completionState.RequestedKey ?? string.Empty;
            var augmentationPending = new DocumentLanguageCompletionRequestState
            {
                Generation = runtime.ServiceGeneration,
                RequestKey = requestKey,
                ContextKey = completionState.RequestedContextKey ?? string.Empty,
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
                        ContextKey = completionState.RequestedContextKey ?? string.Empty,
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
                context.EditorCompletionService.ClearPendingRequest(completionState);
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

            var signatureHelpState = context.State.Editor.SignatureHelp;
            if (!context.EditorSignatureHelpService.ShouldDispatch(signatureHelpState, runtime.SignatureHelpInFlight))
            {
                return;
            }

            var session = context.FindOpenDocument(signatureHelpState.RequestedDocumentPath);
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
                signatureHelpState);
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
                ContextKey = signatureHelpState.RequestedContextKey ?? string.Empty,
                RequestKey = signatureHelpState.RequestedKey ?? string.Empty,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                AbsolutePosition = request.AbsolutePosition
            };
        }

        public void UpdateSemanticOperation(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            var semantic = context != null && context.State != null ? context.State.Semantic : null;
            var requestState = semantic != null ? semantic.Request : null;
            if (context == null || runtime == null || requestState == null || runtime.SemanticOperationInFlight || requestState.RequestedKind == SemanticRequestKind.None)
            {
                return;
            }

            var requestKey = requestState.RequestedKey ?? string.Empty;
            if (string.IsNullOrEmpty(requestKey))
            {
                return;
            }

            var session = context.FindOpenDocument(requestState.RequestedDocumentPath);
            if (session == null)
            {
                return;
            }

            var project = context.ResolveProjectForDocument(session.FilePath);
            var sourceRoots = context.BuildLanguageSourceRoots(context.State.Settings, project);
            var requestId = string.Empty;
            switch (requestState.RequestedKind)
            {
                case SemanticRequestKind.SymbolContext:
                    requestId = context.LanguageServiceClient != null
                        ? context.LanguageServiceClient.QueueSymbolContext(
                            context.DocumentLanguageInteractionService.BuildSymbolContextRequest(
                                session,
                                context.State.Settings,
                                project,
                                sourceRoots,
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition,
                                requestState.RequestedNewName))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedLine,
                                requestState.RequestedColumn,
                                requestState.RequestedAbsolutePosition))
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
                                requestState.RequestedCommandId,
                                requestState.RequestedTitle,
                                requestState.RequestedApplyLabel,
                                requestState.RequestedOrganizeImports,
                                requestState.RequestedSimplifyNames,
                                requestState.RequestedFormatDocument))
                        : string.Empty;
                    break;
            }

            if (string.IsNullOrEmpty(requestId))
            {
                context.State.StatusMessage = "Semantic operation failed: " + (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                requestState.RequestedKey = string.Empty;
                requestState.RequestedKind = SemanticRequestKind.None;
                return;
            }

            runtime.SemanticOperationInFlight = true;
            runtime.PendingSemanticOperation = new PendingSemanticOperationRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                Kind = requestState.RequestedKind,
                RequestKey = requestKey,
                ContextKey = requestState.RequestedContextKey ?? string.Empty,
                DocumentPath = session.FilePath ?? string.Empty,
                DocumentVersion = session.TextVersion,
                SymbolText = requestState.RequestedSymbolText ?? string.Empty,
                NewName = requestState.RequestedNewName ?? string.Empty
            };
            requestState.RequestedKey = string.Empty;
            requestState.RequestedContextKey = string.Empty;
            requestState.RequestedKind = SemanticRequestKind.None;
        }

        public void UpdateMethodInspectorCallHierarchy(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            var inspector = context != null && context.State != null && context.State.Editor != null
                ? context.State.Editor.MethodInspector
                : null;
            if (context == null ||
                runtime == null ||
                inspector == null ||
                !inspector.IsVisible ||
                !inspector.RelationshipsExpanded ||
                runtime.MethodInspectorRelationshipsInFlight ||
                string.IsNullOrEmpty(inspector.RelationshipsRequestKey))
            {
                return;
            }

            var target = context.EditorContextService != null ? context.EditorContextService.ResolveTarget(
                context.State,
                inspector != null ? inspector.ContextKey : string.Empty) : null;
            if (target == null)
            {
                return;
            }

            var session = context.FindOpenDocument(target.DocumentPath);
            if (session == null)
            {
                inspector.RelationshipsRequestKey = string.Empty;
                inspector.RelationshipsStatusMessage = "Method relationships are not available because the source document is no longer open.";
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
            var requestKey = inspector.RelationshipsRequestKey ?? string.Empty;
            var requestId = context.LanguageServiceClient != null
                ? context.LanguageServiceClient.QueueCallHierarchy(request)
                : string.Empty;
            inspector.RelationshipsRequestKey = string.Empty;
            if (string.IsNullOrEmpty(requestId))
            {
                inspector.RelationshipsStatusMessage = "Method relationships failed: " +
                    (context.LanguageServiceClient != null ? context.LanguageServiceClient.LastError : "Roslyn client was not available.");
                return;
            }

            runtime.MethodInspectorRelationshipsInFlight = true;
            runtime.PendingMethodInspectorRelationships = new PendingMethodInspectorRelationshipsRequest
            {
                RequestId = requestId,
                Generation = runtime.ServiceGeneration,
                RequestKey = requestKey,
                ContextKey = inspector.ContextKey ?? string.Empty,
                TargetKey = inspector.RelationshipsTargetKey ?? string.Empty,
                DocumentPath = request.DocumentPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion
            };
        }
    }
}

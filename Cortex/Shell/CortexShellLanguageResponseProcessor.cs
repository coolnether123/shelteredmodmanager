using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using GameModding.Shared.Serialization;

namespace Cortex
{
    internal sealed class CortexShellLanguageResponseProcessor
    {
        public void ProcessLanguageResponses(CortexShellLanguageRuntimeContext context)
        {
            var runtime = context != null ? context.RuntimeState : null;
            if (context == null || runtime == null || context.LanguageServiceClient == null)
            {
                return;
            }

            LanguageServiceEnvelope envelope;
            while (context.LanguageServiceClient.TryDequeueResponse(out envelope))
            {
                if (envelope == null || string.IsNullOrEmpty(envelope.RequestId))
                {
                    continue;
                }

                if (string.Equals(envelope.RequestId, runtime.InitializeRequestId, StringComparison.Ordinal))
                {
                    HandleLanguageInitializeResponse(context, envelope);
                    continue;
                }

                if (string.Equals(envelope.RequestId, runtime.StatusRequestId, StringComparison.Ordinal))
                {
                    HandleLanguageStatusResponse(context, envelope);
                    continue;
                }

                if (runtime.PendingAnalysis != null &&
                    string.Equals(envelope.RequestId, runtime.PendingAnalysis.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingAnalysis;
                    runtime.PendingAnalysis = null;
                    HandleLanguageAnalysisResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingHover != null &&
                    string.Equals(envelope.RequestId, runtime.PendingHover.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingHover;
                    runtime.PendingHover = null;
                    HandleLanguageHoverResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingDefinition != null &&
                    string.Equals(envelope.RequestId, runtime.PendingDefinition.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingDefinition;
                    runtime.PendingDefinition = null;
                    HandleLanguageDefinitionResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingCompletion != null &&
                    string.Equals(envelope.RequestId, runtime.PendingCompletion.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingCompletion;
                    runtime.PendingCompletion = null;
                    HandleLanguageCompletionResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingSignatureHelp != null &&
                    string.Equals(envelope.RequestId, runtime.PendingSignatureHelp.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingSignatureHelp;
                    runtime.PendingSignatureHelp = null;
                    HandleLanguageSignatureHelpResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingSemanticOperation != null &&
                    string.Equals(envelope.RequestId, runtime.PendingSemanticOperation.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingSemanticOperation;
                    runtime.PendingSemanticOperation = null;
                    HandleSemanticOperationResponse(context, envelope, pending);
                    continue;
                }

                if (runtime.PendingMethodInspectorCallHierarchy != null &&
                    string.Equals(envelope.RequestId, runtime.PendingMethodInspectorCallHierarchy.RequestId, StringComparison.Ordinal))
                {
                    var pending = runtime.PendingMethodInspectorCallHierarchy;
                    runtime.PendingMethodInspectorCallHierarchy = null;
                    HandleMethodInspectorCallHierarchyResponse(context, envelope, pending);
                }
            }
        }

        private static void HandleLanguageInitializeResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var runtime = context.RuntimeState;
            runtime.InitializeRequestId = string.Empty;
            runtime.ServiceInitializing = false;
            runtime.InitializeQueuedUtc = DateTime.MinValue;
            runtime.LastInitializationProgressLogUtc = DateTime.MinValue;
            if (envelope == null || !envelope.Success)
            {
                runtime.ServiceReady = false;
                context.State.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = envelope != null ? envelope.ErrorMessage ?? "startup failed" : "startup failed",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                context.State.Diagnostics.Add("Roslyn worker failed to initialize: " + context.State.LanguageServiceStatus.StatusMessage);
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceInitializeResponse>(envelope);
            runtime.ServiceReady = response != null && response.Success;
            if (!runtime.ServiceReady)
            {
                var message = response != null ? response.StatusMessage : "Roslyn worker failed to initialize.";
                context.State.Diagnostics.Add("Roslyn worker failed to initialize: " + message);
                return;
            }

            context.State.Diagnostics.Add("Roslyn worker ready: " +
                (response.WorkerVersion ?? string.Empty) +
                " on " +
                (response.RuntimeVersion ?? string.Empty) +
                ".");
            runtime.StatusRequestId = context.LanguageServiceClient.QueueStatus();
        }

        private static void HandleLanguageStatusResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var runtime = context.RuntimeState;
            runtime.StatusRequestId = string.Empty;
            if (envelope == null || !envelope.Success)
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceStatusResponse>(envelope);
            if (response == null)
            {
                return;
            }

            context.State.LanguageServiceStatus = response;
            MMLog.WriteInfo("[Cortex.Roslyn] Worker ready. CachedProjects=" +
                (context.State.LanguageServiceStatus != null ? context.State.LanguageServiceStatus.CachedProjectCount.ToString() : "0") +
                ", Capabilities=" + BuildCapabilitySummary(context.State.LanguageServiceStatus));
        }

        private static void HandleLanguageAnalysisResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, DocumentLanguageAnalysisRequestState pending)
        {
            var runtime = context.RuntimeState;
            runtime.AnalysisInFlight = false;
            runtime.PendingLanguageAnalysisFingerprint = string.Empty;
            if (pending == null || pending.Generation != runtime.ServiceGeneration)
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceAnalysisResponse>(envelope);
            var target = context.FindOpenDocument(response != null ? response.DocumentPath : pending.DocumentPath);
            if (target == null)
            {
                return;
            }

            if (response == null)
            {
                context.State.Diagnostics.Add("Roslyn analysis failed for " + Path.GetFileName(target.FilePath) + ": unreadable payload.");
                return;
            }

            if (response.DocumentVersion > 0 &&
                target.TextVersion > 0 &&
                response.DocumentVersion != target.TextVersion)
            {
                MMLog.WriteDebug("[Cortex.Roslyn] Ignored stale analysis for " + Path.GetFileName(target.FilePath) +
                    ". ResponseVersion=" + response.DocumentVersion +
                    ", LiveVersion=" + target.TextVersion);
                return;
            }

            target.LanguageAnalysis = context.DocumentLanguageAnalysisService.MergeAnalysis(target.LanguageAnalysis, response, pending);
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

                context.DocumentLanguageAnalysisService.RememberSnapshot(target);
            }

            if (target.LastLanguageClassificationVersion == target.TextVersion &&
                target.LastLanguageDiagnosticVersion == target.TextVersion)
            {
                runtime.LastAnalyzedDocumentFingerprint = pending.Fingerprint ?? string.Empty;
            }

            MMLog.WriteDebug("[Cortex.Roslyn] Analysis complete for " +
                Path.GetFileName(target.FilePath) +
                ". Phase=" + context.DocumentLanguageAnalysisService.BuildAnalysisPhaseLabel(pending) +
                ", Diagnostics=" + CountDiagnostics(target.LanguageAnalysis) +
                ", Classifications=" + CountClassifications(target.LanguageAnalysis) +
                ", Summary=" + BuildClassificationSummary(target.LanguageAnalysis));

            if (!response.Success)
            {
                context.State.Diagnostics.Add("Roslyn analysis failed for " + Path.GetFileName(target.FilePath) + ": " + response.StatusMessage);
                MMLog.WriteWarning("[Cortex.Roslyn] Analysis failed for " + Path.GetFileName(target.FilePath) + ": " + response.StatusMessage);
            }
        }

        private static void HandleLanguageHoverResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, PendingLanguageHoverRequest pending)
        {
            var runtime = context.RuntimeState;
            runtime.HoverInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration)
            {
                return;
            }

            CortexDeveloperLog.WriteHoverPipelineStage(
                "ResponseReceived",
                envelope != null && envelope.Success,
                string.Empty,
                string.Empty,
                pending.HoverKey ?? string.Empty,
                pending.ContextKey ?? string.Empty,
                pending.DocumentPath ?? string.Empty,
                pending.DocumentVersion,
                -1,
                context.State != null && context.State.Editor != null && context.State.Editor.Hover != null
                    ? context.State.Editor.Hover.RequestedTokenText ?? string.Empty
                    : string.Empty,
                envelope != null && !string.IsNullOrEmpty(envelope.ErrorMessage)
                    ? envelope.ErrorMessage
                    : "request-id=" + (pending.RequestId ?? string.Empty));

            var liveSession = context.FindOpenDocument(pending.DocumentPath);
            if (!string.Equals(context.State.Editor.Hover.RequestedKey, pending.HoverKey, StringComparison.Ordinal) ||
                (liveSession != null &&
                 pending.DocumentVersion > 0 &&
                 liveSession.TextVersion > 0 &&
                 liveSession.TextVersion != pending.DocumentVersion))
            {
                CortexDeveloperLog.WriteHoverPipelineStage(
                    "ResponseApplied",
                    false,
                    string.Empty,
                    string.Empty,
                    pending.HoverKey ?? string.Empty,
                    pending.ContextKey ?? string.Empty,
                    pending.DocumentPath ?? string.Empty,
                    liveSession != null ? liveSession.TextVersion : pending.DocumentVersion,
                    -1,
                    context.State.Editor.Hover.RequestedTokenText ?? string.Empty,
                    !string.Equals(context.State.Editor.Hover.RequestedKey, pending.HoverKey, StringComparison.Ordinal)
                        ? "stale-request-key"
                        : "stale-document-version");
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

            var requestedHoverTokenText = context.State.Editor.Hover.RequestedTokenText ?? string.Empty;
            var effectiveContextKey = pending.ContextKey ?? string.Empty;
            context.State.Editor.Hover.RequestedKey = string.Empty;
            context.State.Editor.Hover.RequestedContextKey = string.Empty;
            context.State.Editor.Hover.RequestedDocumentPath = string.Empty;
            context.State.Editor.Hover.RequestedLine = 0;
            context.State.Editor.Hover.RequestedColumn = 0;
            context.State.Editor.Hover.RequestedAbsolutePosition = -1;
            if (context.EditorContextService != null)
            {
                effectiveContextKey = context.EditorContextService.ApplyHoverResponse(context.State, pending.ContextKey, pending.HoverKey, response);
            }
            context.State.Editor.Hover.ActiveContextKey = !string.IsNullOrEmpty(effectiveContextKey)
                ? effectiveContextKey
                : pending.ContextKey ?? string.Empty;
            context.State.Editor.Hover.RequestedTokenText = string.Empty;
            context.State.Editor.Hover.VisualRefreshHoverKey = response.Success
                ? pending.HoverKey ?? string.Empty
                : string.Empty;
            context.State.Editor.Hover.VisualRefreshRequestedUtc = response.Success
                ? DateTime.UtcNow
                : DateTime.MinValue;
            CortexDeveloperLog.WriteHoverPipelineStage(
                "ResponseApplied",
                response.Success,
                string.Empty,
                string.Empty,
                pending.HoverKey ?? string.Empty,
                context.State.Editor.Hover.ActiveContextKey ?? string.Empty,
                pending.DocumentPath ?? string.Empty,
                response.DocumentVersion > 0 ? response.DocumentVersion : pending.DocumentVersion,
                response.DefinitionRange != null ? response.DefinitionRange.Start : -1,
                requestedHoverTokenText,
                response != null ? response.StatusMessage ?? string.Empty : string.Empty);
            if (response.Success)
            {
                CortexDeveloperLog.WriteSymbolHoverPayload(requestedHoverTokenText, response);
                return;
            }

            MMLog.WriteWarning("[Cortex.Roslyn] Hover failed for " +
                requestedHoverTokenText +
                ": " + (response.StatusMessage ?? "Unknown Roslyn hover failure."));
        }

        private static void HandleLanguageDefinitionResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, PendingLanguageDefinitionRequest pending)
        {
            var runtime = context.RuntimeState;
            runtime.DefinitionInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration)
            {
                return;
            }

            try
            {
                var liveSession = context.FindOpenDocument(pending.DocumentPath);
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
                    context.State.Editor.Definition.RequestedContextKey = string.Empty;
                    context.State.StatusMessage = response != null && !string.IsNullOrEmpty(response.StatusMessage)
                        ? response.StatusMessage
                        : (envelope != null && !string.IsNullOrEmpty(envelope.ErrorMessage) ? envelope.ErrorMessage : "Definition was not found.");
                    return;
                }

                context.State.Editor.Definition.RequestedAbsolutePosition = -1;
                context.State.Editor.Definition.RequestedContextKey = string.Empty;

                var opened = context.NavigationService != null && context.NavigationService.OpenLanguageSymbolTarget(
                    context.State,
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
                    ? context.FindOpenDocument(!string.IsNullOrEmpty(response.DocumentPath) ? response.DocumentPath : context.State.Documents.ActiveDocumentPath)
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

                context.State.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                    ? response.StatusMessage
                    : "Definition was not found.";
            }
            catch (Exception ex)
            {
                context.State.StatusMessage = "Definition lookup failed.";
                MMLog.WriteError("[Cortex.Roslyn] Definition response handling crashed for '" +
                    (pending.TokenText ?? string.Empty) + "': " + ex);
            }
        }

        private static void HandleLanguageCompletionResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, DocumentLanguageCompletionRequestState pending)
        {
            var runtime = context.RuntimeState;
            runtime.CompletionInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration || context.State.Editor == null)
            {
                return;
            }

            if (!string.Equals(context.State.Editor.Completion.RequestedKey ?? string.Empty, pending.RequestKey ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceCompletionResponse>(envelope);
            var target = context.FindOpenDocument(response != null ? response.DocumentPath : pending.DocumentPath);
            var accepted = context.EditorCompletionService.AcceptResponse(context.State.Editor.Completion, target, pending, response);
            MMLog.WriteInfo("[Cortex.Completion] Roslyn completion response received. Accepted=" +
                accepted +
                ", Success=" + (response != null && response.Success) +
                ", Items=" + (response != null && response.Items != null ? response.Items.Length : 0) +
                ", Document=" + (target != null ? target.FilePath ?? string.Empty : pending.DocumentPath ?? string.Empty) +
                ", Status=" + (response != null ? response.StatusMessage ?? string.Empty : string.Empty) + ".");
            if (!context.CompletionAugmentationInFlight)
            {
                context.TryQueueCompletionAugmentation(target, pending, context.BuildCompletionAugmentationRequest(target, pending), response);
            }
        }

        private static void HandleLanguageSignatureHelpResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, PendingLanguageSignatureHelpRequest pending)
        {
            var runtime = context.RuntimeState;
            runtime.SignatureHelpInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration || context.State.Editor == null)
            {
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceSignatureHelpResponse>(envelope);
            var target = context.FindOpenDocument(response != null ? response.DocumentPath : pending.DocumentPath);
            context.EditorSignatureHelpService.AcceptResponse(context.State.Editor.SignatureHelp, target, pending, response);
        }

        private static void HandleSemanticOperationResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, PendingSemanticOperationRequest pending)
        {
            var runtime = context.RuntimeState;
            runtime.SemanticOperationInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration || context.State == null || context.State.Semantic == null)
            {
                return;
            }

            var liveSession = context.FindOpenDocument(pending.DocumentPath);
            if (liveSession != null &&
                pending.DocumentVersion > 0 &&
                liveSession.TextVersion > 0 &&
                liveSession.TextVersion != pending.DocumentVersion)
            {
                context.State.StatusMessage = "Ignored stale semantic result for " + (pending.SymbolText ?? string.Empty) + ".";
                return;
            }

            switch (pending.Kind)
            {
                case SemanticRequestKind.SymbolContext:
                    HandleSymbolContextResponse(context, envelope, pending);
                    return;
                case SemanticRequestKind.RenamePreview:
                    HandleRenamePreviewResponse(context, envelope);
                    return;
                case SemanticRequestKind.References:
                    HandleReferencesResponse(context, envelope);
                    return;
                case SemanticRequestKind.PeekDefinition:
                    HandlePeekDefinitionResponse(context, envelope);
                    return;
                case SemanticRequestKind.BaseSymbol:
                    HandleBaseSymbolResponse(context, envelope);
                    return;
                case SemanticRequestKind.Implementations:
                    HandleImplementationResponse(context, envelope);
                    return;
                case SemanticRequestKind.CallHierarchy:
                    HandleCallHierarchyResponse(context, envelope);
                    return;
                case SemanticRequestKind.ValueSource:
                    HandleValueSourceResponse(context, envelope);
                    return;
                case SemanticRequestKind.DocumentTransformPreview:
                    HandleDocumentTransformPreviewResponse(context, envelope);
                    return;
            }
        }

        private static void HandleSymbolContextResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope, PendingSemanticOperationRequest pending)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceSymbolContextResponse>(envelope);
            if (context.EditorContextService != null)
            {
                context.EditorContextService.ApplySymbolContext(
                    context.State,
                    pending != null ? pending.ContextKey ?? string.Empty : string.Empty,
                    response);
            }
            if (response != null)
            {
                context.State.StatusMessage = response.StatusMessage ?? string.Empty;
            }
        }

        private static void HandleRenamePreviewResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceRenameResponse>(envelope);
            context.State.Semantic.Workbench.RenamePreview = response;
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.RenamePreview;
            context.State.StatusMessage = response != null ? response.StatusMessage ?? string.Empty : "Rename preview failed.";
        }

        private static void HandleReferencesResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceReferencesResponse>(envelope);
            context.State.Semantic.Workbench.References = response;
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.References;
            context.State.StatusMessage = response != null ? response.StatusMessage ?? string.Empty : "Reference lookup failed.";
        }

        private static void HandlePeekDefinitionResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceDefinitionResponse>(envelope);
            context.State.Semantic.Workbench.PeekDefinition = response;
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.PeekDefinition;
            context.State.StatusMessage = response != null ? response.StatusMessage ?? string.Empty : "Peek definition failed.";
        }

        private static void HandleBaseSymbolResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceBaseSymbolResponse>(envelope);
            context.State.Semantic.Workbench.BaseSymbols = response;
            var opened = TryOpenSingleSemanticLocation(context, response != null ? response.Locations : null, "Opened base symbol.");
            context.State.Semantic.Workbench.ActiveView = opened ? SemanticWorkbenchViewKind.None : SemanticWorkbenchViewKind.BaseSymbols;
            context.State.StatusMessage = response != null && !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : (opened ? "Opened base symbol." : "Base symbol lookup failed.");
        }

        private static void HandleImplementationResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceImplementationResponse>(envelope);
            context.State.Semantic.Workbench.Implementations = response;
            var opened = TryOpenSingleSemanticLocation(context, response != null ? response.Locations : null, "Opened implementation.");
            context.State.Semantic.Workbench.ActiveView = opened ? SemanticWorkbenchViewKind.None : SemanticWorkbenchViewKind.Implementations;
            context.State.StatusMessage = response != null && !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : (opened ? "Opened implementation." : "Implementation lookup failed.");
        }

        private static void HandleCallHierarchyResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceCallHierarchyResponse>(envelope);
            context.State.Semantic.Workbench.CallHierarchy = response;
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.CallHierarchy;
            context.State.StatusMessage = response != null ? response.StatusMessage ?? string.Empty : "Call hierarchy lookup failed.";
        }

        private static void HandleValueSourceResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceValueSourceResponse>(envelope);
            context.State.Semantic.Workbench.ValueSource = response;
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.ValueSource;
            context.State.StatusMessage = response != null ? response.StatusMessage ?? string.Empty : "Value source lookup failed.";
        }

        private static void HandleDocumentTransformPreviewResponse(CortexShellLanguageRuntimeContext context, LanguageServiceEnvelope envelope)
        {
            var response = DeserializeEnvelopePayload<LanguageServiceDocumentTransformResponse>(envelope);
            if (response == null || !response.Success)
            {
                context.State.StatusMessage = response != null ? response.StatusMessage ?? "Document cleanup preview failed." : "Document cleanup preview failed.";
                return;
            }

            var documents = response.Documents != null ? response.Documents : new LanguageServiceDocumentChange[0];
            if (documents.Length == 0)
            {
                context.State.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                    ? response.StatusMessage
                    : "Document cleanup preview failed.";
                return;
            }

            context.State.Semantic.Workbench.DocumentEditPreview = new DocumentEditPreviewPlan
            {
                CommandId = response.CommandId ?? string.Empty,
                Title = response.Title ?? string.Empty,
                ApplyLabel = response.ApplyLabel ?? string.Empty,
                StatusMessage = response.StatusMessage ?? string.Empty,
                PrimaryDocumentPath = response.DocumentPath ?? string.Empty,
                Documents = documents,
                CanApply = response.CanApply
            };
            context.State.Semantic.Workbench.ActiveView = SemanticWorkbenchViewKind.DocumentEditPreview;
            context.State.StatusMessage = response.StatusMessage ?? string.Empty;
        }

        private static void HandleMethodInspectorCallHierarchyResponse(
            CortexShellLanguageRuntimeContext context,
            LanguageServiceEnvelope envelope,
            PendingMethodInspectorCallHierarchyRequest pending)
        {
            var runtime = context.RuntimeState;
            runtime.MethodInspectorCallHierarchyInFlight = false;
            if (pending == null || pending.Generation != runtime.ServiceGeneration || context.State == null || context.State.Editor == null)
            {
                return;
            }

            var inspector = context.State.Editor.MethodInspector;
            var liveTarget = context.EditorContextService != null
                ? context.EditorContextService.ResolveTarget(context.State, inspector != null ? inspector.ContextKey : string.Empty)
                : null;
            if (inspector == null ||
                !inspector.IsVisible ||
                liveTarget == null ||
                !string.Equals(inspector.CallHierarchyTargetKey ?? string.Empty, pending.TargetKey ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(EditorMethodInspectorService.BuildTargetKey(liveTarget), pending.TargetKey ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            var liveSession = context.FindOpenDocument(pending.DocumentPath);
            if (liveSession != null &&
                pending.DocumentVersion > 0 &&
                liveSession.TextVersion > 0 &&
                liveSession.TextVersion != pending.DocumentVersion)
            {
                inspector.CallHierarchyStatusMessage = "Incoming-call inference was skipped because the document changed.";
                return;
            }

            var response = DeserializeEnvelopePayload<LanguageServiceCallHierarchyResponse>(envelope);
            if (response == null)
            {
                response = new LanguageServiceCallHierarchyResponse
                {
                    Success = false,
                    StatusMessage = envelope != null && !string.IsNullOrEmpty(envelope.ErrorMessage)
                        ? envelope.ErrorMessage
                        : "Incoming-call inference payload was unreadable."
                };
            }

            inspector.CallHierarchy = response;
            inspector.CallHierarchyStatusMessage = response.StatusMessage ?? string.Empty;
        }

        private static bool TryOpenSingleSemanticLocation(CortexShellLanguageRuntimeContext context, LanguageServiceSymbolLocation[] locations, string successMessage)
        {
            if (context == null || context.NavigationService == null || locations == null || locations.Length != 1 || locations[0] == null)
            {
                return false;
            }

            var location = locations[0];
            context.NavigationService.OpenDocument(
                context.State,
                location.DocumentPath,
                location.Range != null ? location.Range.StartLine : 1,
                successMessage,
                "Could not open semantic target.");
            return true;
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
    }
}

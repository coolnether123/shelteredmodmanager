using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services.Editor.Context;
using Cortex.Services.Search;
using Cortex.Services.Semantics.Diagnostics;

namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorPresentationService
    {
        private const string FindQueryControlName = "cortex.find.query";
        private const double HoverVisualRefreshWindowMs = 10000d;

        private readonly IEditorService _editorService;
        private readonly EditorDocumentModeService _documentModeService;

        public EditorPresentationService()
            : this(new EditorService(), new EditorDocumentModeService())
        {
        }

        public EditorPresentationService(IEditorService editorService, EditorDocumentModeService documentModeService)
        {
            _editorService = editorService ?? new EditorService();
            _documentModeService = documentModeService ?? new EditorDocumentModeService();
        }

        public EditorHoverRefreshPlan BuildPendingHoverRefreshPlan(CortexShellState state, DateTime utcNow)
        {
            var plan = new EditorHoverRefreshPlan();
            var hoverState = state != null && state.Editor != null ? state.Editor.Hover : null;
            if (hoverState == null || string.IsNullOrEmpty(hoverState.VisualRefreshHoverKey))
            {
                return plan;
            }

            if ((utcNow - hoverState.VisualRefreshRequestedUtc).TotalMilliseconds > HoverVisualRefreshWindowMs)
            {
                CortexDeveloperLog.WriteHoverDiagnostic(
                    "force-hover-refresh-expired",
                    hoverState.VisualRefreshHoverKey,
                    "editor-presentation-service");
                hoverState.VisualRefreshHoverKey = string.Empty;
                hoverState.VisualRefreshRequestedUtc = DateTime.MinValue;
                return plan;
            }

            plan.ShouldInvalidateSurfaces = true;
            plan.HoverKey = hoverState.VisualRefreshHoverKey ?? string.Empty;
            CortexDeveloperLog.WriteHoverDiagnostic(
                "force-hover-refresh",
                hoverState.VisualRefreshHoverKey,
                "editor-presentation-service");
            return plan;
        }

        public string ResolveSearchShortcutCommand(EditorSearchShortcutInput input, CortexShellState state)
        {
            if (input == null || state == null || state.Documents.ActiveDocument == null)
            {
                return string.Empty;
            }

            if (input.HasFocusedControl &&
                !string.IsNullOrEmpty(input.FocusedControlName) &&
                !string.Equals(input.FocusedControlName, FindQueryControlName, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (input.Control && !input.Alt && !input.Shift && string.Equals(input.KeyCode, "F", StringComparison.Ordinal))
            {
                return "cortex.editor.find";
            }

            if (!state.Search.IsVisible)
            {
                return string.Empty;
            }

            if (!input.Control && !input.Alt && !input.Shift && string.Equals(input.KeyCode, "F3", StringComparison.Ordinal))
            {
                return "cortex.search.next";
            }

            if (!input.Control && !input.Alt && input.Shift && string.Equals(input.KeyCode, "F3", StringComparison.Ordinal))
            {
                return "cortex.search.previous";
            }

            if (!input.Control && !input.Alt && !input.Shift && string.Equals(input.KeyCode, "Escape", StringComparison.Ordinal))
            {
                return "cortex.search.close";
            }

            return string.Empty;
        }

        public void StabilizeActiveDocument(IDocumentService documentService, CortexShellState state)
        {
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return;
            }

            _editorService.EnsureDocumentState(active);
            if (documentService != null)
            {
                documentService.HasExternalChanges(active);
            }
        }

        public EditorCodeAreaPresentation BuildCodeAreaPresentation(CortexShellState state)
        {
            var presentation = new EditorCodeAreaPresentation();
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return presentation;
            }

            _editorService.EnsureDocumentState(active);
            var settings = state.Settings;
            presentation.UsesUnifiedSourceSurface = _documentModeService.UsesUnifiedSourceSurface(active);
            presentation.IsEditingEnabled = _documentModeService.IsEditingEnabled(settings, active);
            return presentation;
        }

        public IList<EditorTabPresentation> BuildTabStripPresentation(CortexShellState state)
        {
            var tabs = new List<EditorTabPresentation>();
            if (state == null || state.Documents == null)
            {
                return tabs;
            }

            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                var session = state.Documents.OpenDocuments[i];
                if (session == null)
                {
                    continue;
                }

                tabs.Add(new EditorTabPresentation
                {
                    Session = session,
                    DisplayName = CortexModuleUtil.GetDocumentDisplayName(session),
                    IsActive = session == state.Documents.ActiveDocument,
                    IsDirty = session.IsDirty
                });
            }

            return tabs;
        }

        public EditorPathBarPresentation BuildPathBarPresentation(IDocumentService documentService, CortexShellState state)
        {
            var presentation = new EditorPathBarPresentation();
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return presentation;
            }

            presentation.CompactPath = BuildCompactPath(active.FilePath);
            presentation.HighlightedLine = active.HighlightedLine;
            presentation.HasHighlightedLine = active.HighlightedLine > 0;
            presentation.HasExternalChanges = active.HasExternalChanges;
            presentation.AllowSaving = state != null &&
                state.Settings != null &&
                state.Settings.EnableFileSaving &&
                active.SupportsSaving;
            presentation.CanReload = documentService != null;
            return presentation;
        }

        public EditorFindOverlayPresentation BuildFindOverlayPresentation(WorkbenchSearchService workbenchSearchService, CortexShellState state)
        {
            var presentation = new EditorFindOverlayPresentation();
            if (state == null || state.Search == null)
            {
                return presentation;
            }

            presentation.SummaryText = BuildFindSummary(workbenchSearchService, state);
            presentation.ScopeLabel = SearchWorkbenchPresentationService.BuildScopeLabel(state.Search.Query.Scope);
            return presentation;
        }

        public EditorStatusBarPresentation BuildStatusBarPresentation(CortexShellState state)
        {
            var presentation = new EditorStatusBarPresentation();
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return presentation;
            }

            _editorService.EnsureDocumentState(active);
            var settings = state != null ? state.Settings : null;
            var caret = active.EditorState != null
                ? _editorService.GetCaretPosition(active, active.EditorState.CaretIndex)
                : new EditorCaretPosition();
            var analysis = active.LanguageAnalysis;
            var errorCount = CountDiagnostics(analysis, "Error");
            var warningCount = CountDiagnostics(analysis, "Warning");

            presentation.CanToggleEditMode = _documentModeService.CanToggleEditing(settings, active);
            presentation.IsEditing = _documentModeService.IsEditingEnabled(settings, active);
            presentation.IsDirty = active.IsDirty;
            presentation.CanSaveAll = settings != null && settings.EnableFileSaving && active.SupportsSaving;
            presentation.Line = caret.Line;
            presentation.Column = caret.Column;
            presentation.LineCount = _editorService.GetLineCount(active);
            presentation.LanguageStatusLabel = BuildLanguageRuntimeLabel(state, analysis, errorCount, warningCount);
            presentation.CompletionStatusLabel = BuildCompletionAugmentationLabel(state, active);
            presentation.EditModeTooltip = _documentModeService.BuildEditModeTooltip(active, settings);
            return presentation;
        }

        public bool TryToggleEditMode(CortexShellState state, out string statusMessage)
        {
            statusMessage = string.Empty;
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            var settings = state != null ? state.Settings : null;
            if (active == null || !_documentModeService.SetEditingEnabled(settings, active, !_documentModeService.IsEditingEnabled(settings, active)))
            {
                return false;
            }

            statusMessage = _documentModeService.IsEditingEnabled(settings, active)
                ? "Edit mode enabled for " + CortexModuleUtil.GetDocumentDisplayName(active) + "."
                : "Read mode enabled for " + CortexModuleUtil.GetDocumentDisplayName(active) + ".";
            return true;
        }

        private static int CountDiagnostics(LanguageServiceAnalysisResponse analysis, string severity)
        {
            if (analysis == null || analysis.Diagnostics == null || analysis.Diagnostics.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < analysis.Diagnostics.Length; i++)
            {
                if (string.Equals(analysis.Diagnostics[i].Severity, severity, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildLanguageRuntimeLabel(CortexShellState state, LanguageServiceAnalysisResponse analysis, int errorCount, int warningCount)
        {
            var runtime = state != null ? state.LanguageRuntime : null;
            var providerLabel = runtime != null &&
                runtime.Provider != null &&
                !string.IsNullOrEmpty(runtime.Provider.DisplayName)
                ? runtime.Provider.DisplayName
                : "Language";
            if (runtime == null)
            {
                return providerLabel + ": offline";
            }

            if (runtime.LifecycleState == LanguageRuntimeLifecycleState.Disabled &&
                runtime.HealthState == LanguageRuntimeHealthState.Healthy)
            {
                return providerLabel + ": off";
            }

            if (runtime.HealthState == LanguageRuntimeHealthState.NoProviders)
            {
                return providerLabel + ": no providers";
            }

            if (runtime.HealthState == LanguageRuntimeHealthState.Unavailable)
            {
                return providerLabel + ": unavailable";
            }

            if (runtime.HealthState == LanguageRuntimeHealthState.Faulted)
            {
                return providerLabel + ": faulted";
            }

            if (runtime.LifecycleState == LanguageRuntimeLifecycleState.Starting)
            {
                return providerLabel + ": starting";
            }

            if (runtime.LifecycleState == LanguageRuntimeLifecycleState.Reloading)
            {
                return providerLabel + ": reloading";
            }

            if (analysis == null)
            {
                return providerLabel + ": ready";
            }

            if (!HasResolvedAnalysis(analysis))
            {
                return providerLabel + ": analyzing";
            }

            if (!analysis.Success)
            {
                return providerLabel + ": " + (string.IsNullOrEmpty(analysis.StatusMessage) ? "analysis failed" : analysis.StatusMessage);
            }

            return providerLabel + " E:" + errorCount + " W:" + warningCount;
        }

        private static string BuildCompletionAugmentationLabel(CortexShellState state, DocumentSession active)
        {
            var settings = state != null ? state.Settings : null;
            if (settings == null || !settings.EnableCompletionAugmentation)
            {
                return "AI: off";
            }

            var editor = state != null ? state.Editor : null;
            var completion = editor != null ? editor.Completion : null;
            var providerId = completion != null ? completion.AugmentationProviderId ?? string.Empty : string.Empty;
            var status = completion != null ? completion.AugmentationStatus ?? string.Empty : string.Empty;
            var statusMessage = completion != null ? completion.AugmentationStatusMessage ?? string.Empty : string.Empty;
            var hasInlineSuggestion = editor != null &&
                active != null &&
                completion != null &&
                completion.InlineResponse != null &&
                string.Equals(completion.InlineProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(providerId) &&
                !string.IsNullOrEmpty(settings.CompletionAugmentationProviderId))
            {
                providerId = settings.CompletionAugmentationProviderId ?? string.Empty;
            }

            if (string.IsNullOrEmpty(providerId))
            {
                return "AI: standby";
            }

            if (string.Equals(providerId, "tabby", StringComparison.OrdinalIgnoreCase) && !settings.EnableTabbyCompletion)
            {
                return "Tabby: off";
            }

            var providerLabel = CompletionAugmentationProviderIds.GetDisplayName(providerId);
            if (hasInlineSuggestion)
            {
                return providerLabel + ": suggestion";
            }

            switch ((status ?? string.Empty).ToLowerInvariant())
            {
                case "starting":
                    return providerLabel + ": starting";
                case "thinking":
                    return providerLabel + ": thinking";
                case "suggestion":
                    return providerLabel + ": suggestion";
                case "ready":
                    return providerLabel + ": ready";
                case "offline":
                    return providerLabel + ": offline";
                case "error":
                    return providerLabel + ": " + BuildCompletionStatusMessage(statusMessage);
            }

            return providerLabel + ": ready";
        }

        private static string BuildCompletionStatusMessage(string statusMessage)
        {
            if (string.IsNullOrEmpty(statusMessage))
            {
                return "error";
            }

            return statusMessage.Length <= 24
                ? statusMessage
                : statusMessage.Substring(0, 21) + "...";
        }

        private static bool HasResolvedAnalysis(LanguageServiceAnalysisResponse analysis)
        {
            return analysis != null &&
                (analysis.Success ||
                 !string.IsNullOrEmpty(analysis.StatusMessage) ||
                 !string.IsNullOrEmpty(analysis.DocumentPath) ||
                 analysis.DocumentVersion > 0 ||
                 (analysis.Diagnostics != null && analysis.Diagnostics.Length > 0) ||
                 (analysis.Classifications != null && analysis.Classifications.Length > 0));
        }

        private static string BuildFindSummary(WorkbenchSearchService workbenchSearchService, CortexShellState state)
        {
            if (state == null || state.Search == null)
            {
                return string.Empty;
            }

            if (state.Search.PendingRefresh)
            {
                return string.IsNullOrEmpty(state.Search.QueryText) ? "Type to search" : "Press Enter";
            }

            var results = state.Search.Results;
            if (results == null)
            {
                return "Press Enter";
            }

            var total = workbenchSearchService != null ? workbenchSearchService.CountMatches(results) : results.TotalMatchCount;
            if (total <= 0)
            {
                return "0/0";
            }

            var activeIndex = state.Search.ActiveMatchIndex;
            if (activeIndex < 0 || activeIndex >= total)
            {
                activeIndex = 0;
            }

            return (activeIndex + 1) + "/" + total;
        }

        private static string BuildCompactPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            var parts = filePath.Replace('\\', '/').Split('/');
            if (parts.Length >= 3)
            {
                return ".../" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
            }

            return filePath;
        }
    }
}

using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Generation
{
    internal sealed class HarmonyPatchGenerationService
    {
        private readonly IHarmonyPatchGenerationRequestFactory _requestFactory;
        private readonly HarmonyPatchTemplateService _templateService;
        private readonly HarmonyPatchInsertionService _insertionService;
        private readonly IEditorService _editorService = new EditorService();

        public HarmonyPatchGenerationService(HarmonyPatchTemplateService templateService, HarmonyPatchInsertionService insertionService)
            : this(new HarmonyPatchGenerationRequestFactory(), templateService, insertionService)
        {
        }

        internal HarmonyPatchGenerationService(IHarmonyPatchGenerationRequestFactory requestFactory, HarmonyPatchTemplateService templateService, HarmonyPatchInsertionService insertionService)
        {
            _requestFactory = requestFactory;
            _templateService = templateService;
            _insertionService = insertionService;
        }

        public HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
        {
            return _requestFactory.CreateDefaultRequest(resolvedTarget, generationKind);
        }

        public HarmonyPatchInsertionTarget[] BuildInsertionTargets(CortexShellState state, IProjectCatalog projectCatalog, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            return _insertionService.BuildInsertionTargets(state, projectCatalog, resolvedTarget, request);
        }

        public bool TryValidateGenerationTarget(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            return _requestFactory.TryValidateGenerationTarget(state, resolvedTarget, out reason);
        }

        public HarmonyPatchGenerationPreview BuildPreview(CortexShellState state, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            var snippetPreview = _templateService.BuildSnippet(resolvedTarget, request);
            if (!snippetPreview.CanApply)
            {
                return snippetPreview;
            }

            return _insertionService.BuildPreview(state, request, snippetPreview);
        }

        public bool Apply(CortexShellState state, IDocumentService documentService, HarmonyPatchGenerationRequest request, HarmonyPatchGenerationPreview preview, out DocumentSession session, out string statusMessage)
        {
            return _insertionService.ApplyPreview(state, documentService, request, preview, out session, out statusMessage);
        }

        public void ArmEditorInsertionPick(CortexShellState state)
        {
            if (state == null || state.Harmony == null || state.Harmony.GenerationRequest == null)
            {
                return;
            }

            state.Harmony.IsInsertionPickActive = true;
            state.Harmony.GenerationStatusMessage = "Click a writable source editor line to choose where the Harmony patch should be inserted.";
            state.StatusMessage = state.Harmony.GenerationStatusMessage;
        }

        public void ClearEditorInsertionPick(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.IsInsertionPickActive = false;
        }

        public bool TryApplyEditorInsertionSelection(CortexShellState state, DocumentSession session, int lineNumber, int absolutePosition, out string statusMessage)
        {
            statusMessage = "Harmony patch generation is not active.";
            if (state == null || state.Harmony == null || state.Harmony.GenerationRequest == null)
            {
                return false;
            }

            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                statusMessage = "Open a writable source editor before choosing a Harmony insertion point.";
                return false;
            }

            if (!session.SupportsSaving || session.IsReadOnly || CortexModuleUtil.IsDecompilerDocumentPath(state, session.FilePath))
            {
                statusMessage = "Select the Harmony insertion point from a writable source editor, not decompiled output.";
                state.Harmony.GenerationStatusMessage = statusMessage;
                state.StatusMessage = statusMessage;
                return false;
            }

            var normalizedPath = NormalizePath(session.FilePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                statusMessage = "The selected editor path was invalid for Harmony insertion.";
                state.Harmony.GenerationStatusMessage = statusMessage;
                state.StatusMessage = statusMessage;
                return false;
            }

            var request = state.Harmony.GenerationRequest;
            request.DestinationFilePath = normalizedPath;
            request.InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext;
            request.InsertionLine = Math.Max(1, lineNumber);
            request.InsertionAbsolutePosition = Math.Max(0, absolutePosition);
            request.InsertionContextLabel = "Selected editor slot";
            UpsertEditorInsertionTarget(state, normalizedPath, request.InsertionLine, request.InsertionAbsolutePosition, request.InsertionContextLabel);
            state.Harmony.IsInsertionPickActive = false;
            state.Harmony.GenerationPreview = null;
            statusMessage = "Selected " + Path.GetFileName(normalizedPath) + ":" + request.InsertionLine + " for Harmony patch insertion.";
            state.Harmony.GenerationStatusMessage = statusMessage;
            state.StatusMessage = statusMessage;
            return true;
        }

        public bool TryOpenInsertionTarget(CortexShellState state, IDocumentService documentService, HarmonyPatchInsertionTarget insertionTarget, out DocumentSession session, out string statusMessage)
        {
            session = null;
            statusMessage = "Harmony patch generation is not ready.";
            if (state == null ||
                state.Harmony == null ||
                state.Harmony.GenerationRequest == null ||
                insertionTarget == null ||
                string.IsNullOrEmpty(insertionTarget.FilePath) ||
                documentService == null)
            {
                return false;
            }

            var request = state.Harmony.GenerationRequest;
            var destinationPath = NormalizePath(insertionTarget.FilePath);
            if (string.IsNullOrEmpty(destinationPath))
            {
                statusMessage = "The selected patch destination path was invalid.";
                return false;
            }

            request.DestinationFilePath = destinationPath;
            request.InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext;
            request.InsertionLine = Math.Max(1, insertionTarget.SuggestedLine);
            request.InsertionAbsolutePosition = Math.Max(0, insertionTarget.SuggestedAbsolutePosition);
            request.InsertionContextLabel = !string.IsNullOrEmpty(insertionTarget.SuggestedContextLabel)
                ? insertionTarget.SuggestedContextLabel
                : "selected editor slot";

            UpsertEditorInsertionTarget(state, destinationPath, request.InsertionLine, request.InsertionAbsolutePosition, request.InsertionContextLabel);

            session = ResolveOrCreateDestinationSession(state, documentService, destinationPath, request.InsertionLine);
            if (session == null)
            {
                statusMessage = "Cortex could not open the selected patch destination.";
                return false;
            }

            if (session.EditorState != null)
            {
                session.EditorState.EditModeEnabled = true;
            }

            _editorService.SetCaret(session, request.InsertionAbsolutePosition, false, false);
            ArmEditorInsertionPick(state);
            statusMessage = "Opened " + Path.GetFileName(destinationPath) + ". Move the caret to the insertion point and press Tab to insert the Harmony patch.";
            state.Harmony.GenerationStatusMessage = statusMessage;
            state.StatusMessage = statusMessage;
            return true;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void UpsertEditorInsertionTarget(CortexShellState state, string filePath, int lineNumber, int absolutePosition, string contextLabel)
        {
            if (state == null || state.Harmony == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var targets = state.Harmony.InsertionTargets;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !PathsEqual(target.FilePath, filePath))
                {
                    continue;
                }

                target.IsWritable = true;
                target.DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext;
                target.SuggestedLine = Math.Max(1, lineNumber);
                target.SuggestedAbsolutePosition = Math.Max(0, absolutePosition);
                target.SuggestedContextLabel = contextLabel ?? string.Empty;
                target.Reason = "Selected editor insertion point";
                return;
            }

            targets.Insert(0, new HarmonyPatchInsertionTarget
            {
                FilePath = filePath,
                DisplayName = Path.GetFileName(filePath),
                IsNewFile = !File.Exists(filePath),
                IsWritable = true,
                DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext,
                SuggestedLine = Math.Max(1, lineNumber),
                SuggestedAbsolutePosition = Math.Max(0, absolutePosition),
                SuggestedContextLabel = contextLabel ?? string.Empty,
                Reason = "Selected editor insertion point"
            });
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static DocumentSession ResolveOrCreateDestinationSession(CortexShellState state, IDocumentService documentService, string filePath, int highlightedLine)
        {
            var existing = CortexModuleUtil.FindOpenDocument(state, filePath);
            if (existing != null)
            {
                existing.Kind = DocumentKind.SourceCode;
                existing.IsReadOnly = false;
                state.Documents.ActiveDocument = existing;
                state.Documents.ActiveDocumentPath = existing.FilePath ?? string.Empty;
                existing.HighlightedLine = highlightedLine;
                return existing;
            }

            if (File.Exists(filePath))
            {
                return CortexModuleUtil.OpenDocument(documentService, state, filePath, highlightedLine, DocumentKind.SourceCode);
            }

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var session = new DocumentSession();
            session.FilePath = fullPath;
            session.Kind = DocumentKind.SourceCode;
            session.IsReadOnly = false;
            session.Text = string.Empty;
            session.OriginalTextSnapshot = string.Empty;
            session.TextVersion = 1;
            session.LastKnownWriteUtc = DateTime.MinValue;
            session.LastTextMutationUtc = DateTime.UtcNow;
            session.EditorState = new EditorDocumentState();
            session.HighlightedLine = highlightedLine;
            state.Documents.OpenDocuments.Add(session);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = fullPath;
            return session;
        }
    }
}

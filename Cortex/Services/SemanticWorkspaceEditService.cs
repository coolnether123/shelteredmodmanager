using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class SemanticWorkspaceEditService
    {
        private readonly IEditorService _editorService = new EditorService();

        public bool ApplyRenamePreview(CortexShellState state, IDocumentService documentService, LanguageServiceRenameResponse response, out string statusMessage)
        {
            if (response == null || response.Documents == null || response.Documents.Length == 0)
            {
                statusMessage = "Rename preview did not contain any document edits.";
                return false;
            }

            return ApplyDocumentChanges(
                state,
                documentService,
                response.Documents,
                "Applied semantic rename across ",
                "Semantic rename preview could not be applied to any documents.",
                out statusMessage);
        }

        public bool ApplyDocumentEditPreview(CortexShellState state, IDocumentService documentService, DocumentEditPreviewPlan plan, out string statusMessage)
        {
            if (plan == null || !plan.CanApply || plan.Documents == null || plan.Documents.Length == 0)
            {
                statusMessage = "Document edit preview was not ready to apply.";
                return false;
            }

            return ApplyDocumentChanges(
                state,
                documentService,
                plan.Documents,
                "Applied preview changes across ",
                "Document edit preview could not be applied to any documents.",
                out statusMessage);
        }

        public bool ApplyUnitTestPlan(UnitTestGenerationPlan plan, out string statusMessage)
        {
            statusMessage = "Unit test generation plan was not ready to apply.";
            if (plan == null || !plan.CanApply || string.IsNullOrEmpty(plan.OutputFilePath))
            {
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(plan.OutputFilePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(plan.OutputFilePath, plan.GeneratedText ?? string.Empty);
                statusMessage = "Created unit test scaffold at " + plan.OutputFilePath + ".";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = "Unit test scaffold could not be written: " + ex.Message;
                return false;
            }
        }

        private static bool TryReadText(DocumentSession session, string documentPath, out string text)
        {
            text = string.Empty;
            if (session != null)
            {
                text = session.Text ?? string.Empty;
                return true;
            }

            try
            {
                text = File.ReadAllText(documentPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string ApplyEdits(string originalText, LanguageServiceTextEdit[] edits, ref int updatedEditCount)
        {
            var workingText = originalText ?? string.Empty;
            var orderedEdits = new List<LanguageServiceTextEdit>(edits);
            orderedEdits.Sort(delegate (LanguageServiceTextEdit left, LanguageServiceTextEdit right)
            {
                var leftStart = left != null && left.Range != null ? left.Range.Start : 0;
                var rightStart = right != null && right.Range != null ? right.Range.Start : 0;
                return rightStart.CompareTo(leftStart);
            });

            for (var i = 0; i < orderedEdits.Count; i++)
            {
                var edit = orderedEdits[i];
                if (edit == null || edit.Range == null)
                {
                    continue;
                }

                var start = Math.Max(0, Math.Min(edit.Range.Start, workingText.Length));
                var length = Math.Max(0, Math.Min(edit.Range.Length, workingText.Length - start));
                workingText = workingText.Substring(0, start) +
                    (edit.NewText ?? string.Empty) +
                    workingText.Substring(start + length);
                updatedEditCount++;
            }

            return workingText;
        }

        private bool ApplyDocumentChanges(
            CortexShellState state,
            IDocumentService documentService,
            LanguageServiceDocumentChange[] documents,
            string successPrefix,
            string failureMessage,
            out string statusMessage)
        {
            statusMessage = failureMessage ?? string.Empty;
            if (documents == null || documents.Length == 0)
            {
                return false;
            }

            var updatedDocuments = 0;
            var updatedEdits = 0;
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null || string.IsNullOrEmpty(document.DocumentPath) || document.Edits == null || document.Edits.Length == 0)
                {
                    continue;
                }

                var session = CortexModuleUtil.FindOpenDocument(state, document.DocumentPath);
                string originalText;
                if (!TryReadText(session, document.DocumentPath, out originalText))
                {
                    continue;
                }

                var updatedText = ApplyEdits(originalText, document.Edits, ref updatedEdits);
                if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryWriteText(session, document.DocumentPath, updatedText, documentService))
                {
                    continue;
                }

                updatedDocuments++;
            }

            statusMessage = updatedDocuments > 0
                ? (successPrefix ?? "Applied changes across ") + updatedDocuments + " document(s) and " + updatedEdits + " edit(s)."
                : failureMessage ?? "Preview changes could not be applied.";
            return updatedDocuments > 0;
        }

        private bool TryWriteText(DocumentSession session, string documentPath, string updatedText, IDocumentService documentService)
        {
            if (session != null)
            {
                if (!_editorService.SetText(session, updatedText))
                {
                    return false;
                }

                if (documentService != null && session.SupportsSaving)
                {
                    documentService.Save(session);
                }

                return true;
            }

            try
            {
                File.WriteAllText(documentPath, updatedText);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

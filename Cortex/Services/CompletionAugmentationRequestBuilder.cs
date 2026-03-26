using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services
{
    internal static class CompletionAugmentationRequestBuilder
    {
        public static CompletionAugmentationRequest Build(
            DocumentSession session,
            DocumentLanguageCompletionRequestState pending,
            CortexSettings settings,
            CortexCompletionInteractionState editorState,
            IList<DocumentSession> openDocuments,
            string languageId)
        {
            if (session == null || pending == null)
            {
                return null;
            }

            var prefixText = BuildPrefixText(session.Text, pending.AbsolutePosition);
            var suffixText = BuildSuffixText(session.Text, pending.AbsolutePosition);
            var cursorContext = CompletionAugmentationCursorContextBuilder.Build(session.Text, pending.AbsolutePosition);
            return new CompletionAugmentationRequest
            {
                ProviderId = settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty,
                DocumentPath = session.FilePath ?? pending.DocumentPath ?? string.Empty,
                ProjectFilePath = string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty,
                RelativeDocumentPath = BuildRelativePath(
                    session.FilePath ?? pending.DocumentPath ?? string.Empty,
                    settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty),
                LanguageId = languageId ?? string.Empty,
                DocumentText = session.Text ?? string.Empty,
                DocumentVersion = session.TextVersion,
                AbsolutePosition = pending.AbsolutePosition,
                ExplicitInvocation = editorState != null && editorState.RequestedExplicit,
                TriggerCharacter = editorState != null ? editorState.RequestedTriggerCharacter ?? string.Empty : string.Empty,
                PrefixText = prefixText,
                SuffixText = suffixText,
                CurrentLinePrefixText = cursorContext != null ? cursorContext.CurrentLinePrefixText ?? string.Empty : string.Empty,
                CurrentLineSuffixText = cursorContext != null ? cursorContext.CurrentLineSuffixText ?? string.Empty : string.Empty,
                CurrentLineIndentation = cursorContext != null ? cursorContext.CurrentLineIndentation ?? string.Empty : string.Empty,
                SelectedText = BuildSelectedText(session),
                AdditionalInstructions = settings != null ? settings.CompletionAugmentationAdditionalInstructions ?? string.Empty : string.Empty,
                ReplaceProviderPrompt = settings != null && settings.CompletionAugmentationReplaceProviderPrompt,
                Declarations = CompletionAugmentationDeclarationBuilder.BuildDeclarations(session, pending.AbsolutePosition),
                RelatedSnippets = BuildRelatedSnippets(session, settings, openDocuments)
            };
        }

        private static CompletionAugmentationSnippet[] BuildRelatedSnippets(
            DocumentSession activeSession,
            CortexSettings settings,
            IList<DocumentSession> openDocuments)
        {
            if (settings == null ||
                !settings.CompletionAugmentationIncludeOpenDocumentSnippets ||
                openDocuments == null)
            {
                return new CompletionAugmentationSnippet[0];
            }

            var maxDocuments = Math.Max(0, settings.CompletionAugmentationSnippetDocumentLimit);
            var maxCharacters = Math.Max(64, settings.CompletionAugmentationSnippetCharacterLimit);
            if (maxDocuments == 0)
            {
                return new CompletionAugmentationSnippet[0];
            }

            var snippets = new List<CompletionAugmentationSnippet>();
            for (var i = 0; i < openDocuments.Count; i++)
            {
                var candidate = openDocuments[i];
                if (candidate == null ||
                    candidate == activeSession ||
                    candidate.Kind != DocumentKind.SourceCode ||
                    string.IsNullOrEmpty(candidate.Text))
                {
                    continue;
                }

                snippets.Add(new CompletionAugmentationSnippet
                {
                    SourceId = candidate.FilePath ?? string.Empty,
                    DisplayName = System.IO.Path.GetFileName(candidate.FilePath ?? string.Empty),
                    RelativePath = BuildRelativePath(
                        candidate.FilePath ?? string.Empty,
                        settings.WorkspaceRootPath ?? string.Empty),
                    Content = TrimSnippet(candidate.Text, maxCharacters),
                    Score = candidate.IsDirty ? 1f : 0.5f
                });
                if (snippets.Count >= maxDocuments)
                {
                    break;
                }
            }

            return snippets.ToArray();
        }

        private static string BuildSelectedText(DocumentSession session)
        {
            if (session == null || session.EditorState == null || string.IsNullOrEmpty(session.Text))
            {
                return string.Empty;
            }

            var selection = session.EditorState.PrimarySelection;
            if (selection == null || !selection.HasSelection)
            {
                return string.Empty;
            }

            var start = Math.Max(0, Math.Min(session.Text.Length, selection.Start));
            var end = Math.Max(start, Math.Min(session.Text.Length, selection.End));
            return end > start ? session.Text.Substring(start, end - start) : string.Empty;
        }

        private static string BuildPrefixText(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            return caret > 0 ? value.Substring(0, caret) : string.Empty;
        }

        private static string BuildSuffixText(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            return caret < value.Length ? value.Substring(caret) : string.Empty;
        }

        private static string TrimSnippet(string text, int maxCharacters)
        {
            var value = text ?? string.Empty;
            if (value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters);
        }

        private static string BuildRelativePath(string documentPath, string workspaceRootPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(workspaceRootPath))
            {
                return documentPath.Replace('\\', '/');
            }

            try
            {
                var rootPath = workspaceRootPath;
                if (!rootPath.EndsWith("\\", StringComparison.Ordinal) &&
                    !rootPath.EndsWith("/", StringComparison.Ordinal))
                {
                    rootPath += System.IO.Path.DirectorySeparatorChar;
                }

                var relative = new Uri(rootPath).MakeRelativeUri(new Uri(documentPath)).ToString();
                return Uri.UnescapeDataString(relative ?? string.Empty).Replace('\\', '/');
            }
            catch
            {
                return documentPath.Replace('\\', '/');
            }
        }
    }
}

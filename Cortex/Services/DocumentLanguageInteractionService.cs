using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class DocumentLanguageInteractionService
    {
        public LanguageServiceHoverRequest BuildHoverRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            int line,
            int column,
            int absolutePosition)
        {
            return new LanguageServiceHoverRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition
            };
        }

        public LanguageServiceDefinitionRequest BuildDefinitionRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            int line,
            int column,
            int absolutePosition)
        {
            return new LanguageServiceDefinitionRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition
            };
        }

        public LanguageServiceCompletionRequest BuildCompletionRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            int line,
            int column,
            int absolutePosition,
            bool explicitInvocation,
            string triggerCharacter)
        {
            return new LanguageServiceCompletionRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition,
                ExplicitInvocation = explicitInvocation,
                TriggerCharacter = triggerCharacter ?? string.Empty
            };
        }

        public string BuildCompletionRequestKey(
            string documentPath,
            int documentVersion,
            int absolutePosition,
            bool explicitInvocation,
            string triggerCharacter)
        {
            return (documentPath ?? string.Empty) +
                "|" + documentVersion +
                "|" + absolutePosition +
                "|" + explicitInvocation +
                "|" + (triggerCharacter ?? string.Empty);
        }

        public bool ShouldTriggerCompletion(char character)
        {
            return character == '.' ||
                character == '_' ||
                char.IsLetterOrDigit(character);
        }

        public bool ShouldContinueCompletion(DocumentSession session, int caretIndex)
        {
            if (session == null || string.IsNullOrEmpty(session.Text))
            {
                return false;
            }

            var text = session.Text;
            var leftIndex = Math.Max(0, Math.Min(text.Length - 1, caretIndex - 1));
            var rightIndex = Math.Max(0, Math.Min(text.Length - 1, caretIndex));
            return (caretIndex > 0 && IsCompletionCharacter(text[leftIndex])) ||
                (caretIndex < text.Length && IsCompletionCharacter(text[rightIndex]));
        }

        public bool HasCompletionItems(LanguageServiceCompletionResponse response)
        {
            return response != null &&
                response.Success &&
                response.Items != null &&
                response.Items.Length > 0;
        }

        public int NormalizeSelectedIndex(LanguageServiceCompletionResponse response, int selectedIndex)
        {
            if (!HasCompletionItems(response))
            {
                return -1;
            }

            if (selectedIndex < 0 || selectedIndex >= response.Items.Length)
            {
                return 0;
            }

            return selectedIndex;
        }

        public bool ApplyCompletion(
            DocumentSession session,
            IEditorService editorService,
            LanguageServiceCompletionResponse response,
            LanguageServiceCompletionItem item)
        {
            if (session == null || editorService == null || response == null || item == null)
            {
                return false;
            }

            var replacementRange = response.ReplacementRange;
            var start = replacementRange != null ? Math.Max(0, replacementRange.Start) : 0;
            var length = replacementRange != null ? Math.Max(0, replacementRange.Length) : 0;
            var textLength = session.Text != null ? session.Text.Length : 0;
            var end = Math.Max(start, Math.Min(textLength, start + length));
            editorService.SetSelection(session, start, end);
            var insertText = !string.IsNullOrEmpty(item.InsertText)
                ? item.InsertText
                : (item.DisplayText ?? string.Empty);
            return editorService.InsertText(session, insertText);
        }

        private static bool IsCompletionCharacter(char value)
        {
            return value == '.' || value == '_' || char.IsLetterOrDigit(value);
        }
    }

    internal sealed class DocumentLanguageCompletionRequestState
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string DocumentPath;
        public int DocumentVersion;
    }
}

using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion
{
    internal sealed class EditorCompletionInteractionPolicy : IEditorCompletionInteractionPolicy
    {
        public bool ShouldTriggerCompletion(char character)
        {
            return character == '.' || character == '_' || char.IsLetterOrDigit(character);
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

        public bool ApplyCompletion(DocumentSession session, IEditorService editorService, LanguageServiceCompletionResponse response, LanguageServiceCompletionItem item)
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
}

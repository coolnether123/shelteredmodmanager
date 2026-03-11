using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorSymbolInteractionService
    {
        public const string EditorSymbolContextId = EditorContextIds.Symbol;

        private readonly IEditorService _editorService = new EditorService();

        public bool TryCreateTargetFromPosition(
            DocumentSession session,
            int absolutePosition,
            LanguageServiceHoverResponse hoverResponse,
            out EditorCommandTarget target)
        {
            target = null;
            if (session == null || string.IsNullOrEmpty(session.Text))
            {
                return false;
            }

            _editorService.EnsureDocumentState(session);
            var text = session.Text ?? string.Empty;
            if (text.Length == 0)
            {
                return false;
            }

            var pivot = Math.Max(0, Math.Min(absolutePosition, text.Length - 1));
            if (!IsIdentifierCharacter(text[pivot]) && pivot > 0 && IsIdentifierCharacter(text[pivot - 1]))
            {
                pivot--;
            }

            if (!IsIdentifierCharacter(text[pivot]))
            {
                return false;
            }

            var start = pivot;
            while (start > 0 && IsIdentifierCharacter(text[start - 1]))
            {
                start--;
            }

            var end = pivot + 1;
            while (end < text.Length && IsIdentifierCharacter(text[end]))
            {
                end++;
            }

            return TryCreateTarget(session, start, end - start, hoverResponse, out target);
        }

        public bool TryCreateTargetFromToken(
            DocumentSession session,
            int absolutePosition,
            int line,
            int column,
            string rawText,
            LanguageServiceHoverResponse hoverResponse,
            bool canGoToDefinition,
            out EditorCommandTarget target)
        {
            target = null;
            var symbolText = rawText != null ? rawText.Trim() : string.Empty;
            if (session == null || string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            target = new EditorCommandTarget
            {
                ContextId = EditorSymbolContextId,
                DocumentPath = session.FilePath ?? string.Empty,
                SymbolText = symbolText,
                HoverText = BuildHoverCopyText(hoverResponse),
                Line = Math.Max(1, line),
                Column = Math.Max(1, column),
                AbsolutePosition = Math.Max(0, absolutePosition),
                SupportsEditing = session.SupportsEditing,
                CanGoToDefinition = canGoToDefinition
            };
            return true;
        }

        public void RequestDefinition(CortexShellState state, EditorCommandTarget target)
        {
            if (state == null || state.Editor == null || target == null || !target.CanGoToDefinition)
            {
                return;
            }

            state.Editor.RequestedDefinitionKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + DateTime.UtcNow.Ticks;
            state.Editor.RequestedDefinitionDocumentPath = target.DocumentPath ?? string.Empty;
            state.Editor.RequestedDefinitionLine = target.Line;
            state.Editor.RequestedDefinitionColumn = target.Column;
            state.Editor.RequestedDefinitionAbsolutePosition = target.AbsolutePosition;
            state.Editor.RequestedDefinitionTokenText = target.SymbolText ?? string.Empty;
        }

        public string BuildHoverCopyText(LanguageServiceHoverResponse hoverResponse)
        {
            if (hoverResponse == null || !hoverResponse.Success)
            {
                return string.Empty;
            }

            return ((hoverResponse.SymbolDisplay ?? string.Empty) +
                Environment.NewLine + Environment.NewLine +
                (hoverResponse.DocumentationText ?? string.Empty)).Trim();
        }

        private bool TryCreateTarget(
            DocumentSession session,
            int start,
            int length,
            LanguageServiceHoverResponse hoverResponse,
            out EditorCommandTarget target)
        {
            target = null;
            if (session == null || string.IsNullOrEmpty(session.Text) || length <= 0)
            {
                return false;
            }

            var text = session.Text;
            start = Math.Max(0, Math.Min(start, text.Length));
            length = Math.Max(0, Math.Min(length, text.Length - start));
            if (length <= 0)
            {
                return false;
            }

            var symbolText = text.Substring(start, length).Trim();
            if (string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            var caret = _editorService.GetCaretPosition(session, start);
            target = new EditorCommandTarget
            {
                ContextId = EditorSymbolContextId,
                DocumentPath = session.FilePath ?? string.Empty,
                SymbolText = symbolText,
                HoverText = BuildHoverCopyText(hoverResponse),
                Line = caret.Line + 1,
                Column = caret.Column + 1,
                AbsolutePosition = start,
                SupportsEditing = session.SupportsEditing,
                CanGoToDefinition = true
            };
            return true;
        }

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }
    }
}

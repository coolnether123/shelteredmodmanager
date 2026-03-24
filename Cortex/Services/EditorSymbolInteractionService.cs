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
                Line = Math.Max(1, line),
                Column = Math.Max(1, column),
                AbsolutePosition = Math.Max(0, absolutePosition),
                CaretIndex = Math.Max(0, absolutePosition),
                DocumentKind = session.Kind,
                SupportsEditing = session.SupportsEditing,
                CanGoToDefinition = canGoToDefinition
            };
            ApplyHoverMetadata(target, hoverResponse);
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
                BuildHoverDetailText(hoverResponse.DocumentationText, hoverResponse.SupplementalSections)).Trim();
        }

        public string BuildHoverDetailText(LanguageServiceHoverResponse hoverResponse)
        {
            return hoverResponse != null
                ? BuildHoverDetailText(hoverResponse.DocumentationText, hoverResponse.SupplementalSections)
                : string.Empty;
        }

        public void ApplySessionContext(EditorCommandTarget target, DocumentSession session, CortexShellState state, bool editingEnabled)
        {
            if (target == null)
            {
                return;
            }

            target.DocumentKind = session != null ? session.Kind : DocumentKind.Unknown;
            target.SupportsEditing = session != null && session.SupportsEditing;
            target.IsEditModeEnabled = editingEnabled;
            target.CanToggleEditMode = state != null &&
                state.Settings != null &&
                state.Settings.EnableFileEditing &&
                session != null &&
                session.SupportsEditing;

            if (session == null || session.EditorState == null)
            {
                return;
            }

            var selection = session.EditorState.PrimarySelection;
            if (target.CaretIndex < 0)
            {
                target.CaretIndex = session.EditorState.CaretIndex;
            }
            if (selection == null)
            {
                return;
            }

            target.SelectionStart = selection.Start;
            target.SelectionEnd = selection.End;
            target.HasSelection = selection.HasSelection;
            target.SelectionText = selection.HasSelection
                ? _editorService.GetSelectedText(session)
                : string.Empty;
        }

        public void ApplyHoverMetadata(EditorCommandTarget target, LanguageServiceHoverResponse hoverResponse)
        {
            if (target == null)
            {
                return;
            }

            target.HoverText = BuildHoverCopyText(hoverResponse);
            target.QualifiedSymbolDisplay = hoverResponse != null ? hoverResponse.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            target.SymbolKind = hoverResponse != null ? hoverResponse.SymbolKind ?? string.Empty : string.Empty;
            target.MetadataName = hoverResponse != null ? hoverResponse.MetadataName ?? string.Empty : string.Empty;
            target.ContainingTypeName = hoverResponse != null ? hoverResponse.ContainingTypeName ?? string.Empty : string.Empty;
            target.ContainingAssemblyName = hoverResponse != null ? hoverResponse.ContainingAssemblyName ?? string.Empty : string.Empty;
            target.DocumentationCommentId = hoverResponse != null ? hoverResponse.DocumentationCommentId ?? string.Empty : string.Empty;
            target.DefinitionDocumentPath = hoverResponse != null ? hoverResponse.DefinitionDocumentPath ?? string.Empty : string.Empty;
            target.DefinitionStart = hoverResponse != null && hoverResponse.DefinitionRange != null ? hoverResponse.DefinitionRange.Start : -1;
            target.DefinitionLength = hoverResponse != null && hoverResponse.DefinitionRange != null ? hoverResponse.DefinitionRange.Length : -1;
            target.DefinitionLine = hoverResponse != null && hoverResponse.DefinitionRange != null ? hoverResponse.DefinitionRange.StartLine : 0;
            target.DefinitionColumn = hoverResponse != null && hoverResponse.DefinitionRange != null ? hoverResponse.DefinitionRange.StartColumn : 0;
        }

        private static string BuildHoverDetailText(string documentationText, LanguageServiceHoverSection[] supplementalSections)
        {
            var supplementalText = FlattenSupplementalSections(supplementalSections);
            if (string.IsNullOrEmpty(documentationText))
            {
                return supplementalText;
            }

            if (string.IsNullOrEmpty(supplementalText))
            {
                return documentationText ?? string.Empty;
            }

            return supplementalText + Environment.NewLine + documentationText;
        }

        private static string FlattenSupplementalSections(LanguageServiceHoverSection[] supplementalSections)
        {
            if (supplementalSections == null || supplementalSections.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < supplementalSections.Length; i++)
            {
                var section = supplementalSections[i];
                if (section == null)
                {
                    continue;
                }

                var sectionText = !string.IsNullOrEmpty(section.Text)
                    ? section.Text
                    : FlattenDisplayParts(section.DisplayParts);
                if (string.IsNullOrEmpty(sectionText))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    text += Environment.NewLine;
                }

                text += !string.IsNullOrEmpty(section.Title)
                    ? section.Title + ": " + sectionText
                    : sectionText;
            }

            return text;
        }

        private static string FlattenDisplayParts(LanguageServiceHoverDisplayPart[] displayParts)
        {
            if (displayParts == null || displayParts.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < displayParts.Length; i++)
            {
                text += displayParts[i] != null ? displayParts[i].Text ?? string.Empty : string.Empty;
            }

            return text.Trim();
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
                Line = caret.Line + 1,
                Column = caret.Column + 1,
                AbsolutePosition = start,
                CaretIndex = start,
                DocumentKind = session.Kind,
                SupportsEditing = session.SupportsEditing,
                CanGoToDefinition = true
            };
            ApplyHoverMetadata(target, hoverResponse);
            return true;
        }

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }
    }
}

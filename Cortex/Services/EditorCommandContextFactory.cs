using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorCommandContextFactory
    {
        private readonly IEditorService _editorService;
        private readonly EditorSymbolInteractionService _symbolInteractionService;
        private readonly EditorDocumentModeService _documentModeService;

        public EditorCommandContextFactory()
            : this(
                new EditorService(),
                new EditorSymbolInteractionService(),
                new EditorDocumentModeService())
        {
        }

        public EditorCommandContextFactory(
            IEditorService editorService,
            EditorSymbolInteractionService symbolInteractionService,
            EditorDocumentModeService documentModeService)
        {
            _editorService = editorService ?? new EditorService();
            _symbolInteractionService = symbolInteractionService ?? new EditorSymbolInteractionService();
            _documentModeService = documentModeService ?? new EditorDocumentModeService();
        }

        public EditorCommandInvocation CreateForTarget(CortexShellState state, EditorCommandTarget target)
        {
            if (target == null)
            {
                return null;
            }

            return new EditorCommandInvocation
            {
                ActiveContainerId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                ActiveDocumentId = !string.IsNullOrEmpty(target.DocumentPath)
                    ? target.DocumentPath
                    : state != null ? state.Documents.ActiveDocumentPath : string.Empty,
                FocusedRegionId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                Target = target
            };
        }

        public CommandExecutionContext Build(CortexShellState state, EditorCommandTarget target)
        {
            return Build(CreateForTarget(state, target));
        }

        public CommandExecutionContext Build(EditorCommandInvocation invocation)
        {
            return new CommandExecutionContext
            {
                ActiveContainerId = invocation != null ? invocation.ActiveContainerId ?? string.Empty : string.Empty,
                ActiveDocumentId = invocation != null ? invocation.ActiveDocumentId ?? string.Empty : string.Empty,
                FocusedRegionId = invocation != null ? invocation.FocusedRegionId ?? string.Empty : string.Empty,
                Parameter = invocation != null ? invocation.Target : null
            };
        }

        public bool TryCreateSourceInteractionInvocation(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            int absolutePosition,
            LanguageServiceHoverResponse hoverResponse,
            out EditorCommandInvocation invocation)
        {
            invocation = null;

            EditorCommandTarget target;
            if (!_symbolInteractionService.TryCreateTargetFromPosition(session, absolutePosition, hoverResponse, out target))
            {
                return false;
            }

            _symbolInteractionService.ApplySessionContext(target, session, state, editingEnabled);
            invocation = CreateForTarget(state, target);
            return invocation != null;
        }

        public bool TryCreateTokenInvocation(
            DocumentSession session,
            CortexShellState state,
            int absolutePosition,
            int line,
            int column,
            string tokenText,
            LanguageServiceHoverResponse hoverResponse,
            bool canGoToDefinition,
            out EditorCommandInvocation invocation)
        {
            invocation = null;

            EditorCommandTarget target;
            if (!_symbolInteractionService.TryCreateTargetFromToken(
                session,
                absolutePosition,
                line,
                column,
                tokenText,
                hoverResponse,
                canGoToDefinition,
                out target))
            {
                return false;
            }

            var editingEnabled = _documentModeService.IsEditingEnabled(state != null ? state.Settings : null, session);
            _symbolInteractionService.ApplySessionContext(target, session, state, editingEnabled);
            invocation = CreateForTarget(state, target);
            return invocation != null;
        }

        public EditorCommandInvocation CreateDocumentInvocation(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            int absolutePosition)
        {
            if (session == null)
            {
                return null;
            }

            _editorService.EnsureDocumentState(session);
            var safeTextLength = session.Text != null ? session.Text.Length : 0;
            var clampedPosition = Math.Max(0, Math.Min(absolutePosition, safeTextLength));
            var caret = _editorService.GetCaretPosition(session, clampedPosition);
            var target = new EditorCommandTarget
            {
                ContextId = EditorContextIds.Document,
                DocumentPath = session.FilePath ?? string.Empty,
                SymbolText = string.Empty,
                HoverText = string.Empty,
                Line = caret.Line + 1,
                Column = caret.Column + 1,
                AbsolutePosition = clampedPosition,
                CanGoToDefinition = false
            };
            _symbolInteractionService.ApplySessionContext(target, session, state, editingEnabled);
            if (target.CaretIndex < 0)
            {
                target.CaretIndex = clampedPosition;
            }

            return CreateForTarget(state, target);
        }
    }
}

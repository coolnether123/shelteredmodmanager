using System;
using Cortex.Core.Models;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Editor.Commands
{
    internal sealed class EditorCommandExecutionStrategyService
    {
        private readonly EditorLogicalDocumentTargetResolutionService _targetResolutionService;
        private readonly IClipboardService _clipboardService;

        public EditorCommandExecutionStrategyService()
            : this(new EditorLogicalDocumentTargetResolutionService(), new MemoryClipboardService())
        {
        }

        public EditorCommandExecutionStrategyService(
            EditorLogicalDocumentTargetResolutionService targetResolutionService,
            IClipboardService clipboardService)
        {
            _targetResolutionService = targetResolutionService ?? new EditorLogicalDocumentTargetResolutionService();
            _clipboardService = clipboardService ?? new MemoryClipboardService();
        }

        public EditorCommandAvailability GetAvailability(string commandId, CortexShellState state, EditorCommandTarget target)
        {
            switch (commandId ?? string.Empty)
            {
                case "cortex.editor.removeAndSortUsings":
                    return EvaluateRemoveAndSortUsings(state, target);
                case "cortex.editor.cut":
                    return EvaluateCut(target);
                case "cortex.editor.paste":
                    return EvaluatePaste(target);
                default:
                    return new EditorCommandAvailability
                    {
                        Visible = true,
                        Enabled = true,
                        ExecutionKind = EditorCommandExecutionKind.Direct
                    };
            }
        }

        private EditorCommandAvailability EvaluateRemoveAndSortUsings(CortexShellState state, EditorCommandTarget target)
        {
            if (target == null)
            {
                return Disabled("Open a document to use this action.");
            }

            if (target.DocumentKind == DocumentKind.SourceCode && !string.IsNullOrEmpty(target.DocumentPath))
            {
                if (!target.SupportsEditing)
                {
                    return Disabled("Editing is disabled for the current document.");
                }

                if (!target.IsEditModeEnabled && !target.CanToggleEditMode)
                {
                    return Disabled("Editing is disabled for the current document.");
                }

                return new EditorCommandAvailability
                {
                    Visible = true,
                    Enabled = true,
                    ExecutionKind = target.IsEditModeEnabled
                        ? EditorCommandExecutionKind.Direct
                        : EditorCommandExecutionKind.PreviewApply,
                    Description = target.IsEditModeEnabled
                        ? "Clean and reorder using directives in the current source document."
                        : "Preview organized using directives before applying them to the source document."
                };
            }

            if (string.Equals(target.ContextId, EditorContextIds.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                EditorLogicalDocumentTarget resolvedTarget;
                string reason;
                if (_targetResolutionService.TryResolveSourceDocument(state, target, out resolvedTarget, out reason) &&
                    resolvedTarget != null &&
                    resolvedTarget.CanApplyEdits)
                {
                    return new EditorCommandAvailability
                    {
                        Visible = true,
                        Enabled = true,
                        ExecutionKind = EditorCommandExecutionKind.PreviewApply,
                        Description = "Preview organized using directives in the resolved source document."
                    };
                }
            }

            return Hidden();
        }

        private EditorCommandAvailability EvaluateCut(EditorCommandTarget target)
        {
            if (target == null)
            {
                return Disabled("Open a document to use this action.");
            }

            if (target.DocumentKind != DocumentKind.SourceCode)
            {
                return Hidden();
            }

            if (!target.SupportsEditing)
            {
                return Disabled("Editing is disabled for the current document.");
            }

            if (!target.HasSelection)
            {
                return Disabled("Select text before cutting.");
            }

            if (!target.IsEditModeEnabled && !target.CanToggleEditMode)
            {
                return Disabled("Editing is disabled for the current document.");
            }

            return new EditorCommandAvailability
            {
                Visible = true,
                Enabled = true,
                ExecutionKind = target.IsEditModeEnabled
                    ? EditorCommandExecutionKind.Direct
                    : EditorCommandExecutionKind.SourceRedirect,
                Description = target.IsEditModeEnabled
                    ? "Cut the current selection to the clipboard."
                    : "Switch into the writable source editor state and cut the current selection."
            };
        }

        private EditorCommandAvailability EvaluatePaste(EditorCommandTarget target)
        {
            if (target == null)
            {
                return Disabled("Open a document to use this action.");
            }

            if (target.DocumentKind != DocumentKind.SourceCode)
            {
                return Hidden();
            }

            if (!target.SupportsEditing)
            {
                return Disabled("Editing is disabled for the current document.");
            }

            var clipboardText = _clipboardService.GetText();
            if (string.IsNullOrEmpty(clipboardText))
            {
                return Disabled("Clipboard is empty.");
            }

            if (!target.IsEditModeEnabled && !target.CanToggleEditMode)
            {
                return Disabled("Editing is disabled for the current document.");
            }

            return new EditorCommandAvailability
            {
                Visible = true,
                Enabled = true,
                ExecutionKind = target.IsEditModeEnabled
                    ? EditorCommandExecutionKind.Direct
                    : EditorCommandExecutionKind.SourceRedirect,
                Description = target.IsEditModeEnabled
                    ? "Paste clipboard contents into the current source document."
                    : "Switch into the writable source editor state and paste clipboard contents."
            };
        }

        private static EditorCommandAvailability Disabled(string reason)
        {
            return new EditorCommandAvailability
            {
                Visible = true,
                Enabled = false,
                DisabledReason = reason ?? string.Empty,
                ExecutionKind = EditorCommandExecutionKind.Unavailable
            };
        }

        private static EditorCommandAvailability Hidden()
        {
            return new EditorCommandAvailability
            {
                Visible = false,
                Enabled = false,
                ExecutionKind = EditorCommandExecutionKind.Unavailable
            };
        }
    }
}

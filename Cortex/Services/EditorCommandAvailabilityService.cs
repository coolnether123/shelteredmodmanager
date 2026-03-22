using System;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorCommandAvailabilityService
    {
        public bool TryGetAvailability(string commandId, CortexShellState state, EditorCommandTarget target, out string disabledReason)
        {
            disabledReason = string.Empty;
            switch (commandId ?? string.Empty)
            {
                case "cortex.editor.quickActions":
                    return RequireSymbol(target, "Quick Actions require a symbol.", ref disabledReason);
                case "cortex.editor.rename":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.Rename, "Rename", ref disabledReason);
                case "cortex.editor.peekDefinition":
                case "cortex.editor.goToDefinition":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.Definition, "Definition navigation", ref disabledReason);
                case "cortex.editor.goToBase":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.BaseSymbol, "Base navigation", ref disabledReason);
                case "cortex.editor.goToImplementation":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.Implementations, "Implementation navigation", ref disabledReason);
                case "cortex.editor.findAllReferences":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.References, "Reference lookup", ref disabledReason);
                case "cortex.editor.viewCallHierarchy":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.CallHierarchy, "Call hierarchy", ref disabledReason);
                case "cortex.editor.trackValueSource":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.ValueSource, "Value source tracking", ref disabledReason);
                case "cortex.editor.createUnitTests":
                    return RequireSymbol(target, "Unit test generation requires a symbol.", ref disabledReason);
                case "cortex.editor.removeAndSortUsings":
                case "cortex.editor.paste":
                case "cortex.editor.snippet":
                    return RequireEditableDocument(state, "Editing is disabled for the active document.", ref disabledReason);
                case "cortex.editor.cut":
                    return RequireEditableSelection(state, ref disabledReason);
                case "cortex.editor.copy":
                    return RequireSelectionOrSymbol(state, target, ref disabledReason);
                case "cortex.editor.viewCode":
                case "cortex.editor.breakpoint":
                case "cortex.editor.runToCursor":
                case "cortex.editor.forceRunToCursor":
                case "cortex.editor.executeInInteractive":
                case "cortex.editor.annotation":
                case "cortex.editor.outlining":
                case "cortex.editor.git":
                    return RequireDocument(state, "Open a document to use this action.", ref disabledReason);
                default:
                    return true;
            }
        }

        public bool HasCapability(CortexShellState state, string capability)
        {
            if (string.IsNullOrEmpty(capability))
            {
                return true;
            }

            var status = state != null ? state.LanguageServiceStatus : null;
            var capabilities = status != null ? status.Capabilities : null;
            if (capabilities == null)
            {
                return false;
            }

            for (var i = 0; i < capabilities.Length; i++)
            {
                if (string.Equals(capabilities[i], capability, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RequireNavigationCapability(EditorCommandTarget target, CortexShellState state, string capability, string actionLabel, ref string disabledReason)
        {
            if (target == null || !target.CanGoToDefinition)
            {
                disabledReason = actionLabel + " requires a navigable symbol.";
                return false;
            }

            if (!HasCapability(state, capability))
            {
                disabledReason = actionLabel + " is not supported by the active language service.";
                return false;
            }

            return true;
        }

        private bool RequireSymbolCapability(EditorCommandTarget target, CortexShellState state, string capability, string actionLabel, ref string disabledReason)
        {
            if (!RequireSymbol(target, actionLabel + " requires a symbol.", ref disabledReason))
            {
                return false;
            }

            if (!HasCapability(state, capability))
            {
                disabledReason = actionLabel + " is not supported by the active language service.";
                return false;
            }

            return true;
        }

        private static bool RequireSymbol(EditorCommandTarget target, string reason, ref string disabledReason)
        {
            if (target == null || string.IsNullOrEmpty(target.SymbolText))
            {
                disabledReason = reason;
                return false;
            }

            return true;
        }

        private static bool RequireDocument(CortexShellState state, string reason, ref string disabledReason)
        {
            if (state == null || state.Documents == null || state.Documents.ActiveDocument == null)
            {
                disabledReason = reason;
                return false;
            }

            return true;
        }

        private static bool RequireEditableDocument(CortexShellState state, string reason, ref string disabledReason)
        {
            if (!RequireDocument(state, reason, ref disabledReason))
            {
                return false;
            }

            if (!state.Documents.ActiveDocument.SupportsEditing)
            {
                disabledReason = reason;
                return false;
            }

            return true;
        }

        private static bool RequireEditableSelection(CortexShellState state, ref string disabledReason)
        {
            if (!RequireEditableDocument(state, "Editing is disabled for the active document.", ref disabledReason))
            {
                return false;
            }

            if (state.Documents.ActiveDocument.EditorState == null || !state.Documents.ActiveDocument.EditorState.HasSelection)
            {
                disabledReason = "Select text before cutting.";
                return false;
            }

            return true;
        }

        private static bool RequireSelectionOrSymbol(CortexShellState state, EditorCommandTarget target, ref string disabledReason)
        {
            if (state != null &&
                state.Documents != null &&
                state.Documents.ActiveDocument != null &&
                state.Documents.ActiveDocument.EditorState != null &&
                state.Documents.ActiveDocument.EditorState.HasSelection)
            {
                return true;
            }

            if (target != null && !string.IsNullOrEmpty(target.SymbolText))
            {
                return true;
            }

            disabledReason = "Select text or place the caret on a symbol before copying.";
            return false;
        }
    }
}

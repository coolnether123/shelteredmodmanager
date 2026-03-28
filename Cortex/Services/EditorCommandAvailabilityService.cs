using System;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorCommandAvailabilityService
    {
        private readonly EditorCommandExecutionStrategyService _strategyService = new EditorCommandExecutionStrategyService();

        public bool TryGetAvailability(string commandId, CortexShellState state, EditorCommandTarget target, out string disabledReason)
        {
            var availability = GetAvailability(commandId, state, target);
            disabledReason = availability != null ? availability.DisabledReason ?? string.Empty : string.Empty;
            return availability != null && availability.Enabled;
        }

        public EditorCommandAvailability GetAvailability(string commandId, CortexShellState state, EditorCommandTarget target)
        {
            switch (commandId ?? string.Empty)
            {
                case "cortex.editor.quickActions":
                    return RequireSymbol(target, "Quick Actions require a symbol.");
                case "cortex.editor.rename":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.Rename, "Rename");
                case "cortex.editor.peekDefinition":
                case "cortex.editor.goToDefinition":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.Definition, "Definition navigation");
                case "cortex.editor.goToBase":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.BaseSymbol, "Base navigation");
                case "cortex.editor.goToImplementation":
                    return RequireNavigationCapability(target, state, SemanticCapabilityNames.Implementations, "Implementation navigation");
                case "cortex.editor.findAllReferences":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.References, "Reference lookup");
                case "cortex.editor.viewCallHierarchy":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.CallHierarchy, "Call hierarchy");
                case "cortex.editor.trackValueSource":
                    return RequireSymbolCapability(target, state, SemanticCapabilityNames.ValueSource, "Value source tracking");
                case "cortex.editor.createUnitTests":
                    return RequireSymbol(target, "Unit test generation requires a symbol.");
                case "cortex.editor.removeAndSortUsings":
                case "cortex.editor.cut":
                case "cortex.editor.paste":
                    return _strategyService.GetAvailability(commandId, state, target);
                case "cortex.editor.snippet":
                    return RequireEditableDocument(state, "Editing is disabled for the active document.");
                case "cortex.editor.copy":
                    return RequireSelectionOrSymbol(state, target);
                case "cortex.editor.viewCode":
                case "cortex.editor.breakpoint":
                case "cortex.editor.runToCursor":
                case "cortex.editor.forceRunToCursor":
                case "cortex.editor.executeInInteractive":
                case "cortex.editor.annotation":
                case "cortex.editor.outlining":
                case "cortex.editor.git":
                    return RequireDocument(state, "Open a document to use this action.");
                default:
                    return Enabled();
            }
        }

        public bool HasCapability(CortexShellState state, string capability)
        {
            if (string.IsNullOrEmpty(capability))
            {
                return true;
            }

            var runtime = state != null ? state.LanguageRuntime : null;
            var capabilities = runtime != null && runtime.Capabilities != null
                ? runtime.Capabilities.CapabilityIds
                : null;
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

        private EditorCommandAvailability RequireNavigationCapability(EditorCommandTarget target, CortexShellState state, string capability, string actionLabel)
        {
            if (target == null || !target.CanGoToDefinition)
            {
                return Disabled(actionLabel + " requires a navigable symbol.");
            }

            if (!HasCapability(state, capability))
            {
                return Disabled(BuildCapabilityDisabledReason(state, actionLabel));
            }

            return Enabled();
        }

        private EditorCommandAvailability RequireSymbolCapability(EditorCommandTarget target, CortexShellState state, string capability, string actionLabel)
        {
            var symbolAvailability = RequireSymbol(target, actionLabel + " requires a symbol.");
            if (!symbolAvailability.Enabled)
            {
                return symbolAvailability;
            }

            if (!HasCapability(state, capability))
            {
                return Disabled(BuildCapabilityDisabledReason(state, actionLabel));
            }

            return Enabled();
        }

        private static EditorCommandAvailability RequireSymbol(EditorCommandTarget target, string reason)
        {
            if (target == null || string.IsNullOrEmpty(target.SymbolText))
            {
                return Disabled(reason);
            }

            return Enabled();
        }

        private static EditorCommandAvailability RequireDocument(CortexShellState state, string reason)
        {
            if (state == null || state.Documents == null || state.Documents.ActiveDocument == null)
            {
                return Disabled(reason);
            }

            return Enabled();
        }

        private static EditorCommandAvailability RequireEditableDocument(CortexShellState state, string reason)
        {
            var documentAvailability = RequireDocument(state, reason);
            if (!documentAvailability.Enabled)
            {
                return documentAvailability;
            }

            if (!state.Documents.ActiveDocument.SupportsEditing)
            {
                return Disabled(reason);
            }

            return Enabled();
        }

        private static EditorCommandAvailability RequireSelectionOrSymbol(CortexShellState state, EditorCommandTarget target)
        {
            if (target != null && target.HasSelection)
            {
                return Enabled();
            }

            if (state != null &&
                state.Documents != null &&
                state.Documents.ActiveDocument != null &&
                state.Documents.ActiveDocument.EditorState != null &&
                state.Documents.ActiveDocument.EditorState.HasSelection)
            {
                return Enabled();
            }

            if (target != null && !string.IsNullOrEmpty(target.SymbolText))
            {
                return Enabled();
            }

            return Disabled("Select text or place the caret on a symbol before copying.");
        }

        private static string BuildCapabilityDisabledReason(CortexShellState state, string actionLabel)
        {
            var runtime = state != null ? state.LanguageRuntime : null;
            if (runtime == null)
            {
                return actionLabel + " is not available because the language runtime is offline.";
            }

            if (IsExplicitlyDisabled(runtime))
            {
                return actionLabel + " is unavailable because the language runtime is disabled.";
            }

            switch (runtime.HealthState)
            {
                case LanguageRuntimeHealthState.NoProviders:
                    return actionLabel + " is unavailable because no language providers are registered.";
                case LanguageRuntimeHealthState.Unavailable:
                    return actionLabel + " is unavailable because the selected language provider could not be created.";
                case LanguageRuntimeHealthState.Faulted:
                    return actionLabel + " is unavailable because the active language provider faulted.";
            }

            return actionLabel + " is not supported by the active language provider.";
        }

        private static bool IsExplicitlyDisabled(LanguageRuntimeSnapshot runtime)
        {
            return runtime != null &&
                runtime.LifecycleState == LanguageRuntimeLifecycleState.Disabled &&
                runtime.HealthState == LanguageRuntimeHealthState.Healthy;
        }

        private static EditorCommandAvailability Enabled()
        {
            return new EditorCommandAvailability
            {
                Visible = true,
                Enabled = true,
                ExecutionKind = EditorCommandExecutionKind.Direct
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
    }
}

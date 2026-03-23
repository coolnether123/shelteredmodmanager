using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Services;

namespace Cortex.Modules.Editor
{
    internal static class EditorCommandContributions
    {
        private static bool _registered;

        public static void EnsureRegistered(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry, CortexShellState state)
        {
            if (_registered || commandRegistry == null || contributionRegistry == null || state == null)
            {
                return;
            }

            _registered = true;
            new EditorContextCommandRegistrar(commandRegistry, contributionRegistry, state).Register();
        }

        private sealed class EditorContextCommandRegistrar
        {
            private readonly ICommandRegistry _commandRegistry;
            private readonly IContributionRegistry _contributionRegistry;
            private readonly CortexShellState _state;
            private readonly IEditorService _editorService = new EditorService();
            private readonly IClipboardService _clipboardService = new EditorClipboardService();
            private readonly EditorCommandAvailabilityService _availabilityService = new EditorCommandAvailabilityService();
            private readonly EditorCommandExecutionStrategyService _executionStrategyService = new EditorCommandExecutionStrategyService();
            private readonly EditorSemanticOperationService _semanticOperationService = new EditorSemanticOperationService();
            private readonly EditorContextActionResolverService _actionResolverService = new EditorContextActionResolverService();
            private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
            private readonly EditorLogicalDocumentTargetResolutionService _targetResolutionService = new EditorLogicalDocumentTargetResolutionService();
            private readonly EditorMutationExecutionService _mutationExecutionService = new EditorMutationExecutionService();
            private readonly UsingDirectiveOrganizationService _usingDirectiveOrganizationService = new UsingDirectiveOrganizationService();

            public EditorContextCommandRegistrar(
                ICommandRegistry commandRegistry,
                IContributionRegistry contributionRegistry,
                CortexShellState state)
            {
                _commandRegistry = commandRegistry;
                _contributionRegistry = contributionRegistry;
                _state = state;
            }

            public void Register()
            {
                RegisterCommands();
                RegisterActions();
            }

            private void RegisterCommands()
            {
                RegisterCommand(
                    "cortex.editor.quickActions",
                    "Quick Actions and Refactorings...",
                    "Editor",
                    "Show editor quick actions for the current symbol.",
                    "Ctrl+.",
                    0,
                    ExecuteQuickActions);

                RegisterCommand(
                    "cortex.editor.rename",
                    "Rename...",
                    "Editor",
                    "Preview and apply a semantic rename for the current symbol.",
                    "F2",
                    10,
                    BeginRename);

                RegisterCommand(
                    "cortex.editor.removeAndSortUsings",
                    "Remove and Sort Usings",
                    "Editor",
                    "Clean and reorder using directives.",
                    "Ctrl+R, Ctrl+G",
                    20,
                    RemoveAndSortUsings);

                RegisterCommand(
                    "cortex.editor.peekDefinition",
                    "Peek Definition",
                    "Editor",
                    "Preview the current symbol definition.",
                    "Alt+F12",
                    30,
                    PeekDefinition);

                RegisterCommand(
                    "cortex.editor.goToDefinition",
                    "Go To Definition",
                    "Editor",
                    "Navigate to the current symbol definition.",
                    "F12",
                    40,
                    GoToDefinition);

                RegisterCommand(
                    "cortex.editor.goToBase",
                    "Go To Base",
                    "Editor",
                    "Navigate to the current symbol's base declaration.",
                    "Alt+Home",
                    50,
                    GoToBase);

                RegisterCommand(
                    "cortex.editor.goToImplementation",
                    "Go To Implementation",
                    "Editor",
                    "Navigate to the current symbol implementation targets.",
                    "Ctrl+F12",
                    60,
                    GoToImplementation);

                RegisterCommand(
                    "cortex.editor.findAllReferences",
                    "Find All References",
                    "Editor",
                    "Find semantic references for the current symbol.",
                    "Shift+F12",
                    70,
                    FindAllReferences);

                RegisterCommand(
                    "cortex.editor.viewCallHierarchy",
                    "View Call Hierarchy",
                    "Editor",
                    "View semantic incoming and outgoing calls for the current symbol.",
                    "Ctrl+K, Ctrl+T",
                    80,
                    ViewCallHierarchy);

                RegisterCommand(
                    "cortex.editor.trackValueSource",
                    "Track Value Source",
                    "Editor",
                    "View semantic value-source writes for the current symbol.",
                    string.Empty,
                    85,
                    TrackValueSource);

                RegisterCommand(
                    "cortex.editor.createUnitTests",
                    "Create Unit Tests",
                    "Editor",
                    "Generate a reusable unit test scaffold for the current symbol.",
                    string.Empty,
                    90,
                    CreateUnitTests);

                RegisterCommand(
                    "cortex.editor.breakpoint",
                    "Breakpoint...",
                    "Editor",
                    "Debugger breakpoint actions are not available in the current runtime.",
                    string.Empty,
                    100,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.runToCursor",
                    "Run To Cursor",
                    "Editor",
                    "Run-to-cursor is not available in the current runtime.",
                    "Ctrl+F10",
                    110,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.forceRunToCursor",
                    "Force Run To Cursor",
                    "Editor",
                    "Force-run-to-cursor is not available in the current runtime.",
                    string.Empty,
                    120,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.executeInInteractive",
                    "Execute In Interactive",
                    "Editor",
                    "Interactive execution is not available in the current runtime.",
                    "Ctrl+E, Ctrl+E",
                    130,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.snippet",
                    "Snippet...",
                    "Editor",
                    "Snippet insertion is not available in the current runtime.",
                    string.Empty,
                    140,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.cut",
                    "Cut",
                    "Editor",
                    "Cut the active selection to the clipboard.",
                    "Ctrl+X",
                    150,
                    Cut);

                RegisterCommand(
                    "cortex.editor.copy",
                    "Copy",
                    "Editor",
                    "Copy the active selection or symbol to the clipboard.",
                    "Ctrl+C",
                    160,
                    Copy);

                RegisterCommand(
                    "cortex.editor.paste",
                    "Paste",
                    "Editor",
                    "Paste clipboard contents into the active document.",
                    "Ctrl+V",
                    170,
                    Paste);

                RegisterCommand(
                    "cortex.editor.annotation",
                    "Annotation...",
                    "Editor",
                    "Annotations are not available in the current runtime.",
                    string.Empty,
                    180,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.outlining",
                    "Outlining...",
                    "Editor",
                    "Outlining commands are not available in the current runtime.",
                    string.Empty,
                    190,
                    ShowUnavailable);

                RegisterCommand(
                    "cortex.editor.git",
                    "Git...",
                    "Editor",
                    "Git actions are not available in the current runtime.",
                    string.Empty,
                    200,
                    ShowUnavailable);
            }

            private void RegisterActions()
            {
                RegisterAction("cortex.editor.quickActions", EditorContextIds.Symbol, "01_actions", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, false, true);
                RegisterAction("cortex.editor.rename", EditorContextIds.Symbol, "01_actions", 10, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.Rename, false, true);
                RegisterAction("cortex.editor.removeAndSortUsings", EditorContextIds.Document, "01_actions", 20, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.peekDefinition", EditorContextIds.Symbol, "02_navigation", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.Definition, false, true);
                RegisterAction("cortex.editor.goToDefinition", EditorContextIds.Symbol, "02_navigation", 10, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.Definition, false, true);
                RegisterAction("cortex.editor.goToBase", EditorContextIds.Symbol, "02_navigation", 20, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.BaseSymbol, false, true);
                RegisterAction("cortex.editor.goToImplementation", EditorContextIds.Symbol, "02_navigation", 30, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.Implementations, false, true);
                RegisterAction("cortex.editor.findAllReferences", EditorContextIds.Symbol, "02_navigation", 40, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.References, false, true);
                RegisterAction("cortex.editor.viewCallHierarchy", EditorContextIds.Symbol, "02_navigation", 50, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.CallHierarchy, false, true);
                RegisterAction("cortex.editor.trackValueSource", EditorContextIds.Symbol, "02_navigation", 60, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, SemanticCapabilityNames.ValueSource, false, true);
                RegisterAction("cortex.editor.createUnitTests", EditorContextIds.Symbol, "03_generation", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions, string.Empty, false, true);
                RegisterAction("cortex.editor.cut", EditorContextIds.Document, "04_clipboard", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.copy", EditorContextIds.Document, "04_clipboard", 10, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.paste", EditorContextIds.Document, "04_clipboard", 20, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.breakpoint", EditorContextIds.Document, "05_runtime", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.runToCursor", EditorContextIds.Document, "05_runtime", 10, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.forceRunToCursor", EditorContextIds.Document, "05_runtime", 20, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.executeInInteractive", EditorContextIds.Document, "05_runtime", 30, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.snippet", EditorContextIds.Document, "06_metadata", 0, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.annotation", EditorContextIds.Document, "06_metadata", 10, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.outlining", EditorContextIds.Document, "06_metadata", 20, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
                RegisterAction("cortex.editor.git", EditorContextIds.Document, "06_metadata", 30, EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar, string.Empty, true, true);
            }

            private void RegisterCommand(string commandId, string displayName, string category, string description, string gesture, int sortOrder, CommandHandler handler)
            {
                if (_commandRegistry.Get(commandId) == null)
                {
                    _commandRegistry.Register(new CommandDefinition
                    {
                        CommandId = commandId,
                        DisplayName = displayName,
                        Category = category,
                        Description = description,
                        DefaultGesture = gesture,
                        SortOrder = sortOrder,
                        ShowInPalette = true
                    });
                }

                _commandRegistry.RegisterHandler(
                    commandId,
                    handler,
                    delegate(CommandExecutionContext context)
                    {
                        var target = GetTarget(context);
                        string disabledReason;
                        return _availabilityService.TryGetAvailability(commandId, _state, target, out disabledReason);
                    });
            }

            private void RegisterAction(
                string commandId,
                string contextId,
                string group,
                int sortOrder,
                EditorContextActionPlacement placements,
                string requiredCapability,
                bool includeWhenNoSymbol,
                bool showWhenDisabled)
            {
                _contributionRegistry.RegisterEditorContextAction(new EditorContextActionContribution
                {
                    ActionId = commandId,
                    CommandId = commandId,
                    ContextId = contextId,
                    Group = group,
                    SortOrder = sortOrder,
                    Placements = placements,
                    RequiredCapability = requiredCapability,
                    IncludeWhenNoSymbol = includeWhenNoSymbol,
                    ShowWhenDisabled = showWhenDisabled
                });
            }

            private void ExecuteQuickActions(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                var actions = _actionResolverService.ResolveActions(
                    _state,
                    _commandRegistry,
                    _contributionRegistry,
                    target,
                    EditorContextActionPlacement.QuickActions);
                _semanticOperationService.OpenQuickActions(_state, target, ToArray(actions));
                _state.StatusMessage = actions.Count > 0
                    ? "Quick Actions opened for " + (target.SymbolText ?? string.Empty) + "."
                    : "No quick actions are available for the current location.";
            }

            private void BeginRename(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                if (target == null)
                {
                    return;
                }

                _state.Editor.ActiveRenameTarget = target;
                _state.Editor.ActiveRenameText = target.SymbolText ?? string.Empty;
                _state.StatusMessage = "Rename preview ready for " + (target.SymbolText ?? string.Empty) + ".";
            }

            private void PeekDefinition(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                _state.Editor.ActivePeekTarget = target;
                QueueSemantic(context, SemanticRequestKind.PeekDefinition, SemanticWorkbenchViewKind.PeekDefinition, "Peek Definition");
            }

            private void GoToDefinition(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                if (target == null)
                {
                    return;
                }

                _symbolInteractionService.RequestDefinition(_state, target);
                _state.StatusMessage = "Go To Definition: " + (target.SymbolText ?? string.Empty);
            }

            private void GoToBase(CommandExecutionContext context)
            {
                QueueSemantic(context, SemanticRequestKind.BaseSymbol, SemanticWorkbenchViewKind.BaseSymbols, "Go To Base");
            }

            private void GoToImplementation(CommandExecutionContext context)
            {
                QueueSemantic(context, SemanticRequestKind.Implementations, SemanticWorkbenchViewKind.Implementations, "Go To Implementation");
            }

            private void FindAllReferences(CommandExecutionContext context)
            {
                QueueSemantic(context, SemanticRequestKind.References, SemanticWorkbenchViewKind.References, "Find All References");
            }

            private void ViewCallHierarchy(CommandExecutionContext context)
            {
                QueueSemantic(context, SemanticRequestKind.CallHierarchy, SemanticWorkbenchViewKind.CallHierarchy, "View Call Hierarchy");
            }

            private void TrackValueSource(CommandExecutionContext context)
            {
                QueueSemantic(context, SemanticRequestKind.ValueSource, SemanticWorkbenchViewKind.ValueSource, "Track Value Source");
            }

            private void CreateUnitTests(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                var plan = _semanticOperationService.BuildUnitTestPlan(_state, target);
                _state.Semantic.UnitTestGeneration = plan;
                _state.Semantic.ActiveView = SemanticWorkbenchViewKind.UnitTestGeneration;
                OpenSearchContainer();
                _state.StatusMessage = plan != null ? plan.StatusMessage ?? string.Empty : "Unit test generation was not available.";
            }

            private void RemoveAndSortUsings(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                var availability = _executionStrategyService.GetAvailability("cortex.editor.removeAndSortUsings", _state, target);
                if (availability == null || !availability.Enabled)
                {
                    _state.StatusMessage = availability != null ? availability.DisabledReason ?? string.Empty : "Remove and Sort Usings is not available.";
                    return;
                }

                EditorLogicalDocumentTarget resolvedTarget;
                string resolutionReason;
                if (!_targetResolutionService.TryResolveSourceDocument(_state, target, out resolvedTarget, out resolutionReason) || resolvedTarget == null)
                {
                    _state.StatusMessage = resolutionReason;
                    return;
                }

                string updatedText;
                DocumentEditPreviewPlan previewPlan;
                string statusMessage;
                if (!_usingDirectiveOrganizationService.TryBuildPreviewPlan(_state, resolvedTarget.DocumentPath, out previewPlan, out updatedText, out statusMessage))
                {
                    _state.StatusMessage = statusMessage;
                    return;
                }

                if (availability.ExecutionKind == EditorCommandExecutionKind.Direct)
                {
                    var session = resolvedTarget.Session ?? GetActiveDocument(true);
                    if (session == null)
                    {
                        _state.StatusMessage = "Open the source document before organizing using directives.";
                        return;
                    }

                    if (_editorService.SetText(session, updatedText))
                    {
                        _state.StatusMessage = "Removed duplicates and sorted using directives.";
                    }

                    return;
                }

                _semanticOperationService.OpenDocumentEditPreview(_state, previewPlan);
                OpenSearchContainer();

                if (_availabilityService.HasCapability(_state, "document-transforms"))
                {
                    var previewTarget = new EditorCommandTarget
                    {
                        ContextId = EditorContextIds.Document,
                        DocumentPath = resolvedTarget.DocumentPath ?? string.Empty,
                        Line = target != null ? target.Line : 1,
                        Column = target != null ? target.Column : 1,
                        AbsolutePosition = 0,
                        SupportsEditing = resolvedTarget.SupportsEditing,
                        CanGoToDefinition = false
                    };
                    _semanticOperationService.QueueDocumentTransformRequest(
                        _state,
                        previewTarget,
                        "cortex.editor.removeAndSortUsings",
                        "Remove and Sort Usings",
                        "Apply Changes",
                        true,
                        false,
                        true);
                    _state.StatusMessage = "Previewing Roslyn cleanup changes for " + System.IO.Path.GetFileName(resolvedTarget.DocumentPath) + ".";
                    return;
                }

                _state.StatusMessage = previewPlan != null ? previewPlan.StatusMessage ?? string.Empty : "Using directive preview was not available.";
            }

            private void Cut(CommandExecutionContext context)
            {
                string statusMessage;
                if (_mutationExecutionService.TryExecuteClipboardCommand("cortex.editor.cut", _state, GetTarget(context), out statusMessage))
                {
                    _state.StatusMessage = statusMessage;
                    return;
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    _state.StatusMessage = statusMessage;
                }
            }

            private void Copy(CommandExecutionContext context)
            {
                var session = GetActiveDocument(false);
                var target = GetTarget(context);
                var text = target != null && target.HasSelection
                    ? target.SelectionText ?? string.Empty
                    : session != null
                        ? _editorService.GetSelectedText(session)
                        : string.Empty;
                if (string.IsNullOrEmpty(text) && target != null)
                {
                    text = target.SymbolText ?? string.Empty;
                }

                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                _clipboardService.SetText(text);
                _state.StatusMessage = "Copied selection.";
            }

            private void Paste(CommandExecutionContext context)
            {
                string statusMessage;
                if (_mutationExecutionService.TryExecuteClipboardCommand("cortex.editor.paste", _state, GetTarget(context), out statusMessage))
                {
                    _state.StatusMessage = statusMessage;
                    return;
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    _state.StatusMessage = statusMessage;
                }
            }

            private void ShowUnavailable(CommandExecutionContext context)
            {
                _state.StatusMessage = "This action is not available in the current runtime.";
            }

            private void QueueSemantic(CommandExecutionContext context, SemanticRequestKind requestKind, SemanticWorkbenchViewKind viewKind, string actionName)
            {
                var target = GetTarget(context);
                if (target == null)
                {
                    return;
                }

                _semanticOperationService.QueueRequest(_state, target, requestKind);
                _state.Semantic.ActiveView = viewKind;
                OpenSearchContainer();
                _state.StatusMessage = actionName + " requested for " + (target.SymbolText ?? string.Empty) + ".";
            }

            private void OpenSearchContainer()
            {
                _commandRegistry.Execute("cortex.window.search", new CommandExecutionContext
                {
                    ActiveContainerId = _state.Workbench.FocusedContainerId,
                    ActiveDocumentId = _state.Documents.ActiveDocumentPath,
                    FocusedRegionId = _state.Workbench.FocusedContainerId
                });
            }

            private DocumentSession GetActiveDocument(bool requireEditable)
            {
                if (_state == null || _state.Documents == null)
                {
                    return null;
                }

                var session = _state.Documents.ActiveDocument;
                if (session == null)
                {
                    return null;
                }

                if (requireEditable && !session.SupportsEditing)
                {
                    return null;
                }

                return session;
            }

            private static EditorCommandTarget GetTarget(CommandExecutionContext context)
            {
                return context != null ? context.Parameter as EditorCommandTarget : null;
            }

            private static EditorResolvedContextAction[] ToArray(System.Collections.Generic.IList<EditorResolvedContextAction> actions)
            {
                if (actions == null || actions.Count == 0)
                {
                    return new EditorResolvedContextAction[0];
                }

                var results = new EditorResolvedContextAction[actions.Count];
                for (var i = 0; i < actions.Count; i++)
                {
                    results[i] = actions[i];
                }

                return results;
            }
        }
    }
}

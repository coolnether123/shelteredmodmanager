using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Services;
using UnityEngine;

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
            new EditorContextCommandRegistrar(commandRegistry, contributionRegistry, state).RegisterBuiltIns();
        }

        private sealed class EditorContextCommandRegistrar
        {
            private readonly ICommandRegistry _commandRegistry;
            private readonly IContributionRegistry _contributionRegistry;
            private readonly CortexShellState _state;
            private readonly EditorContextActionService _actionService = new EditorContextActionService();

            public EditorContextCommandRegistrar(
                ICommandRegistry commandRegistry,
                IContributionRegistry contributionRegistry,
                CortexShellState state)
            {
                _commandRegistry = commandRegistry;
                _contributionRegistry = contributionRegistry;
                _state = state;
            }

            public void RegisterBuiltIns()
            {
                var descriptors = BuildDescriptors();
                for (var i = 0; i < descriptors.Count; i++)
                {
                    RegisterDescriptor(descriptors[i]);
                }
            }

            private IList<EditorContextCommandDescriptor> BuildDescriptors()
            {
                var descriptors = new List<EditorContextCommandDescriptor>();

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.quickActions",
                    "Quick Actions and Refactorings...",
                    "Refactoring",
                    "Show the primary refactoring action for the current symbol.",
                    "Ctrl+.",
                    "1_Refactor",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ExecuteQuickActions(_state, _commandRegistry, context, GetTarget(context));
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.rename",
                    "Rename...",
                    "Refactoring",
                    "Rename the current symbol.",
                    "F2",
                    "1_Refactor",
                    20,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.BeginRename(_state, _commandRegistry, context, GetTarget(context));
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasRenameTarget(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.removeAndSortUsings",
                    "Remove and Sort Usings",
                    "Refactoring",
                    "Remove duplicate using directives and sort the remaining directives.",
                    "Ctrl+R, Ctrl+G",
                    "1_Refactor",
                    30,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.RemoveAndSortUsings(_state);
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanEditActiveDocument(_state);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.viewCode",
                    "View Code",
                    "View",
                    "Keep focus in the active source editor.",
                    "F7",
                    "2_View",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ViewCode(_state);
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.peekDefinition",
                    "Peek Definition",
                    "Navigation",
                    "Open the inline peek definition popup.",
                    "Alt+F12",
                    "3_Navigation",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.PeekDefinition(_state, GetTarget(context));
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanNavigate(context);
                    }));

                descriptors.Add(CreateDefinitionOnlyDescriptor(
                    "cortex.editor.goToDefinition",
                    "Go To Definition",
                    "Navigation",
                    "Navigate to the symbol definition.",
                    "F12",
                    "3_Navigation",
                    20));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.goToBase",
                    "Go To Base",
                    "Navigation",
                    "Navigate to the nearest available base target.",
                    "Alt+Home",
                    "3_Navigation",
                    30,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.GoToDefinitionLike(_state, GetTarget(context), "Go To Base");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanNavigate(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.goToImplementation",
                    "Go To Implementation",
                    "Navigation",
                    "Navigate to the nearest available implementation target.",
                    "Ctrl+F12",
                    "3_Navigation",
                    40,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.GoToDefinitionLike(_state, GetTarget(context), "Go To Implementation");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanNavigate(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.findAllReferences",
                    "Find All References",
                    "Navigation",
                    "Search for all references to the current symbol.",
                    "Shift+F12",
                    "3_Navigation",
                    50,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.FindAllReferences(_state, _commandRegistry, context, GetTarget(context), "Finding references for: ");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.viewCallHierarchy",
                    "View Call Hierarchy",
                    "Navigation",
                    "Seed the search panel with the current symbol for hierarchy review.",
                    "Ctrl+K, Ctrl+T",
                    "3_Navigation",
                    60,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.FindAllReferences(_state, _commandRegistry, context, GetTarget(context), "Call hierarchy seeded for: ");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.trackValueSource",
                    "Track Value Source",
                    "Navigation",
                    "Seed the search panel with the current symbol for value-source tracking.",
                    string.Empty,
                    "3_Navigation",
                    70,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.FindAllReferences(_state, _commandRegistry, context, GetTarget(context), "Tracking value source for: ");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.createUnitTests",
                    "Create Unit Tests",
                    "Generation",
                    "Seed the search panel for a related test target.",
                    string.Empty,
                    "4_Generation",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.CreateUnitTests(_state, _commandRegistry, context, GetTarget(context));
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.breakpoint",
                    "Breakpoint...",
                    "Debugging",
                    "Breakpoint actions are not available in the current runtime.",
                    string.Empty,
                    "5_Debug",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Breakpoint commands");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.runToCursor",
                    "Run To Cursor",
                    "Debugging",
                    "Run-to-cursor is not available in the current runtime.",
                    "Ctrl+F10",
                    "5_Debug",
                    20,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Run To Cursor");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.forceRunToCursor",
                    "Force Run To Cursor",
                    "Debugging",
                    "Force-run-to-cursor is not available in the current runtime.",
                    string.Empty,
                    "5_Debug",
                    30,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Force Run To Cursor");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.executeInInteractive",
                    "Execute In Interactive",
                    "Debugging",
                    "Interactive execution is not available in the current runtime.",
                    "Ctrl+E, Ctrl+E",
                    "5_Debug",
                    40,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Execute In Interactive");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.snippet",
                    "Snippet...",
                    "Editing",
                    "Snippet insertion is not available in the current runtime.",
                    string.Empty,
                    "5_Debug",
                    50,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Snippet insertion");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanEditActiveDocument(_state);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.cut",
                    "Cut",
                    "Editing",
                    "Cut the active selection to the clipboard.",
                    "Ctrl+X",
                    "6_Edit",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.Cut(_state);
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSelection(_state) && CanEditActiveDocument(_state);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.copy",
                    "Copy",
                    "Editing",
                    "Copy the active selection or symbol to the clipboard.",
                    "Ctrl+C",
                    "6_Edit",
                    20,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.Copy(_state, GetTarget(context));
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return HasSelection(_state) || HasSymbol(context);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.paste",
                    "Paste",
                    "Editing",
                    "Paste clipboard contents into the active document.",
                    "Ctrl+V",
                    "6_Edit",
                    30,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.Paste(_state);
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return CanEditActiveDocument(_state);
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.annotation",
                    "Annotation...",
                    "Metadata",
                    "Annotations are not available in the current runtime.",
                    string.Empty,
                    "7_Metadata",
                    10,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Annotation");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.outlining",
                    "Outlining...",
                    "Metadata",
                    "Outlining commands are not available in the editable surface yet.",
                    string.Empty,
                    "7_Metadata",
                    20,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Outlining");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                descriptors.Add(CreateDescriptor(
                    "cortex.editor.git",
                    "Git...",
                    "Metadata",
                    "Git integration is not available from the editor context menu yet.",
                    string.Empty,
                    "7_Metadata",
                    30,
                    delegate(CommandExecutionContext context)
                    {
                        _actionService.ShowNotAvailable(_state, "Git actions");
                    },
                    delegate(CommandExecutionContext context)
                    {
                        return _state.Documents.ActiveDocument != null;
                    }));

                return descriptors;
            }

            private void RegisterDescriptor(EditorContextCommandDescriptor descriptor)
            {
                if (descriptor == null || descriptor.Definition == null || string.IsNullOrEmpty(descriptor.Definition.CommandId))
                {
                    return;
                }

                _commandRegistry.Register(descriptor.Definition);
                if (descriptor.Handler != null)
                {
                    _commandRegistry.RegisterHandler(descriptor.Definition.CommandId, descriptor.Handler, descriptor.CanExecute);
                }

                if (descriptor.Menu != null)
                {
                    _contributionRegistry.RegisterMenu(descriptor.Menu);
                }
            }

            private static EditorContextCommandDescriptor CreateDescriptor(
                string commandId,
                string displayName,
                string category,
                string description,
                string gesture,
                string group,
                int sortOrder,
                CommandHandler handler,
                CommandEnablement canExecute)
            {
                return new EditorContextCommandDescriptor
                {
                    Definition = new CommandDefinition
                    {
                        CommandId = commandId,
                        DisplayName = displayName,
                        Category = category,
                        Description = description,
                        DefaultGesture = gesture,
                        SortOrder = sortOrder
                    },
                    Menu = new MenuContribution
                    {
                        Location = MenuProjectionLocation.ContextMenu,
                        ContextId = EditorContextIds.Symbol,
                        CommandId = commandId,
                        Group = group,
                        SortOrder = sortOrder
                    },
                    Handler = handler,
                    CanExecute = canExecute
                };
            }

            private static EditorContextCommandDescriptor CreateDefinitionOnlyDescriptor(
                string commandId,
                string displayName,
                string category,
                string description,
                string gesture,
                string group,
                int sortOrder)
            {
                return CreateDescriptor(commandId, displayName, category, description, gesture, group, sortOrder, null, null);
            }
        }

        private sealed class EditorContextCommandDescriptor
        {
            public CommandDefinition Definition;
            public MenuContribution Menu;
            public CommandHandler Handler;
            public CommandEnablement CanExecute;
        }

        private sealed class EditorContextActionService
        {
            private readonly IEditorService _editorService = new EditorService();
            private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();

            public void ExecuteQuickActions(
                CortexShellState state,
                ICommandRegistry commandRegistry,
                CommandExecutionContext context,
                EditorCommandTarget target)
            {
                if (state == null)
                {
                    return;
                }

                if (target == null || string.IsNullOrEmpty(target.SymbolText))
                {
                    state.StatusMessage = "No quick actions are available for the current location.";
                    return;
                }

                BeginRename(state, commandRegistry, context, target);
                state.StatusMessage = "Quick Actions opened Rename for '" + (target.SymbolText ?? string.Empty) + "'.";
            }

            public void BeginRename(
                CortexShellState state,
                ICommandRegistry commandRegistry,
                CommandExecutionContext context,
                EditorCommandTarget target)
            {
                if (state == null || state.Editor == null || target == null || string.IsNullOrEmpty(target.SymbolText))
                {
                    return;
                }

                state.Editor.ActiveRenameTarget = null;
                state.Editor.ActiveRenameText = string.Empty;
                OpenSearchWorkflow(
                    state,
                    commandRegistry,
                    context,
                    TextSearchWorkflowKind.Rename,
                    target.SymbolText ?? string.Empty,
                    "Rename preview opened for '" + (target.SymbolText ?? string.Empty) + "'.",
                    true);
            }

            public void PeekDefinition(CortexShellState state, EditorCommandTarget target)
            {
                if (state == null || state.Editor == null || target == null || !target.CanGoToDefinition)
                {
                    return;
                }

                state.Editor.ActivePeekTarget = target;
                state.StatusMessage = "Peek Definition: " + (target.SymbolText ?? string.Empty);
            }

            public void GoToDefinitionLike(CortexShellState state, EditorCommandTarget target, string actionName)
            {
                if (state == null || target == null || !target.CanGoToDefinition)
                {
                    return;
                }

                _symbolInteractionService.RequestDefinition(state, target);
                state.StatusMessage = (actionName ?? "Navigate") + ": " + (target.SymbolText ?? string.Empty);
            }

            public void FindAllReferences(
                CortexShellState state,
                ICommandRegistry commandRegistry,
                CommandExecutionContext context,
                EditorCommandTarget target,
                string statusPrefix)
            {
                if (state == null || state.Search == null || target == null || string.IsNullOrEmpty(target.SymbolText))
                {
                    return;
                }

                var workflowKind = TextSearchWorkflowKind.References;
                var caption = "Review the current text matches for this symbol.";
                var loweredPrefix = statusPrefix ?? string.Empty;
                if (loweredPrefix.IndexOf("call hierarchy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    workflowKind = TextSearchWorkflowKind.CallHierarchy;
                    caption = "Call hierarchy is seeded from the current text matches.";
                }
                else if (loweredPrefix.IndexOf("value source", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    workflowKind = TextSearchWorkflowKind.ValueSource;
                    caption = "Value-source tracking is seeded from the current text matches.";
                }

                OpenSearchWorkflow(
                    state,
                    commandRegistry,
                    context,
                    workflowKind,
                    target.SymbolText ?? string.Empty,
                    caption,
                    false);
                state.StatusMessage = (statusPrefix ?? string.Empty) + state.Search.QueryText;
            }

            public void CreateUnitTests(
                CortexShellState state,
                ICommandRegistry commandRegistry,
                CommandExecutionContext context,
                EditorCommandTarget target)
            {
                if (state == null || state.Search == null || target == null || string.IsNullOrEmpty(target.SymbolText))
                {
                    return;
                }

                OpenSearchWorkflow(
                    state,
                    commandRegistry,
                    context,
                    TextSearchWorkflowKind.UnitTests,
                    (target.SymbolText ?? string.Empty) + "Tests",
                    "Test search seeded from the current symbol.",
                    false);
                state.StatusMessage = "Test search seeded for: " + (target.SymbolText ?? string.Empty);
            }

            public void ViewCode(CortexShellState state)
            {
                if (state == null)
                {
                    return;
                }

                state.StatusMessage = "Code view is already active.";
            }

            public void ShowNotAvailable(CortexShellState state, string actionName)
            {
                if (state == null)
                {
                    return;
                }

                state.StatusMessage = (actionName ?? "This command") + " is not available in the current runtime.";
            }

            public void Copy(CortexShellState state, EditorCommandTarget target)
            {
                var session = GetActiveDocument(state, false);
                if (session == null)
                {
                    return;
                }

                var text = _editorService.GetSelectedText(session);
                if (string.IsNullOrEmpty(text) && target != null)
                {
                    text = target.SymbolText ?? string.Empty;
                }

                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                GUIUtility.systemCopyBuffer = text;
                state.StatusMessage = "Copied selection.";
            }

            public void Cut(CortexShellState state)
            {
                var session = GetActiveDocument(state, true);
                if (session == null)
                {
                    return;
                }

                var text = _editorService.GetSelectedText(session);
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                GUIUtility.systemCopyBuffer = text;
                if (_editorService.Backspace(session))
                {
                    state.StatusMessage = "Cut selection.";
                }
            }

            public void Paste(CortexShellState state)
            {
                var session = GetActiveDocument(state, true);
                if (session == null)
                {
                    return;
                }

                var text = GUIUtility.systemCopyBuffer ?? string.Empty;
                if (_editorService.InsertText(session, text))
                {
                    state.StatusMessage = "Pasted clipboard contents.";
                }
            }

            public void RemoveAndSortUsings(CortexShellState state)
            {
                var session = GetActiveDocument(state, true);
                if (session == null)
                {
                    return;
                }

                string updatedText;
                if (!TryOrganizeTopLevelUsings(session.Text, out updatedText))
                {
                    state.StatusMessage = "No using directives were found to organize.";
                    return;
                }

                if (string.Equals(session.Text ?? string.Empty, updatedText ?? string.Empty, StringComparison.Ordinal))
                {
                    state.StatusMessage = "Using directives are already organized.";
                    return;
                }

                if (_editorService.SetText(session, updatedText))
                {
                    state.StatusMessage = "Removed duplicates and sorted using directives.";
                }
            }

            private static DocumentSession GetActiveDocument(CortexShellState state, bool requireEditable)
            {
                if (state == null || state.Documents == null)
                {
                    return null;
                }

                var session = state.Documents.ActiveDocument;
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

            private static bool TryOrganizeTopLevelUsings(string text, out string updatedText)
            {
                var original = text ?? string.Empty;
                updatedText = original;
                if (original.Length == 0)
                {
                    return false;
                }

                var usesCrLf = original.IndexOf("\r\n", StringComparison.Ordinal) >= 0;
                var normalized = original.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                var blockStart = -1;
                var blockEnd = -1;

                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = (lines[i] ?? string.Empty).Trim();
                    if (blockStart < 0)
                    {
                        if (trimmed.Length == 0 || IsDirectivePreambleLine(trimmed))
                        {
                            continue;
                        }

                        if (!IsUsingDirective(trimmed))
                        {
                            break;
                        }

                        blockStart = i;
                        blockEnd = i + 1;
                        continue;
                    }

                    if (trimmed.Length == 0 || IsUsingDirective(trimmed))
                    {
                        blockEnd = i + 1;
                        continue;
                    }

                    break;
                }

                if (blockStart < 0 || blockEnd <= blockStart)
                {
                    return false;
                }

                var usingLines = new List<string>();
                for (var i = blockStart; i < blockEnd; i++)
                {
                    var trimmed = (lines[i] ?? string.Empty).Trim();
                    if (IsUsingDirective(trimmed))
                    {
                        usingLines.Add(trimmed);
                    }
                }

                if (usingLines.Count == 0)
                {
                    return false;
                }

                usingLines.Sort(StringComparer.OrdinalIgnoreCase);
                var distinctUsings = new List<string>();
                for (var i = 0; i < usingLines.Count; i++)
                {
                    if (distinctUsings.Count == 0 ||
                        !string.Equals(distinctUsings[distinctUsings.Count - 1], usingLines[i], StringComparison.OrdinalIgnoreCase))
                    {
                        distinctUsings.Add(usingLines[i]);
                    }
                }

                var rebuilt = new List<string>();
                for (var i = 0; i < blockStart; i++)
                {
                    rebuilt.Add(lines[i]);
                }

                for (var i = 0; i < distinctUsings.Count; i++)
                {
                    rebuilt.Add(distinctUsings[i]);
                }

                var firstTrailingIndex = blockEnd;
                while (firstTrailingIndex < lines.Length && string.IsNullOrEmpty(lines[firstTrailingIndex]))
                {
                    firstTrailingIndex++;
                }

                if (distinctUsings.Count > 0 &&
                    firstTrailingIndex < lines.Length &&
                    rebuilt.Count > 0 &&
                    !string.IsNullOrEmpty(rebuilt[rebuilt.Count - 1]))
                {
                    rebuilt.Add(string.Empty);
                }

                for (var i = firstTrailingIndex; i < lines.Length; i++)
                {
                    rebuilt.Add(lines[i]);
                }

                updatedText = string.Join("\n", rebuilt.ToArray());
                if (usesCrLf)
                {
                    updatedText = updatedText.Replace("\n", "\r\n");
                }

                return true;
            }

            private static bool IsDirectivePreambleLine(string trimmed)
            {
                return trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("#", StringComparison.Ordinal);
            }

            private static bool IsUsingDirective(string trimmed)
            {
                return !string.IsNullOrEmpty(trimmed) &&
                    trimmed.StartsWith("using ", StringComparison.Ordinal) &&
                    trimmed.EndsWith(";", StringComparison.Ordinal);
            }

            private static void OpenSearchWorkflow(
                CortexShellState state,
                ICommandRegistry commandRegistry,
                CommandExecutionContext context,
                TextSearchWorkflowKind workflowKind,
                string queryText,
                string caption,
                bool expandRenamePanel)
            {
                if (state == null || state.Search == null)
                {
                    return;
                }

                state.Search.IsVisible = true;
                state.Search.FocusQueryRequested = false;
                state.Search.ScopeMenuOpen = false;
                state.Search.PendingRefresh = true;
                state.Search.ActiveMatchIndex = -1;
                state.Search.QueryText = queryText ?? string.Empty;
                state.Search.WorkflowKind = workflowKind;
                state.Search.WorkflowTargetText = queryText ?? string.Empty;
                state.Search.WorkflowCaption = caption ?? string.Empty;
                state.Search.RenamePanelExpanded = expandRenamePanel;
                if (workflowKind == TextSearchWorkflowKind.Rename)
                {
                    state.Search.RenameReplacementText = queryText ?? string.Empty;
                }

                if (commandRegistry != null)
                {
                    commandRegistry.Execute("cortex.window.search", context);
                }
            }
        }

        private static EditorCommandTarget GetTarget(CommandExecutionContext context)
        {
            return context != null ? context.Parameter as EditorCommandTarget : null;
        }

        private static bool HasSymbol(CommandExecutionContext context)
        {
            var target = GetTarget(context);
            return target != null && !string.IsNullOrEmpty(target.SymbolText);
        }

        private static bool CanNavigate(CommandExecutionContext context)
        {
            var target = GetTarget(context);
            return target != null && target.CanGoToDefinition;
        }

        private static bool HasRenameTarget(CommandExecutionContext context)
        {
            var target = GetTarget(context);
            return target != null &&
                !string.IsNullOrEmpty(target.SymbolText);
        }

        private static bool HasSelection(CortexShellState state)
        {
            return state != null &&
                state.Documents != null &&
                state.Documents.ActiveDocument != null &&
                state.Documents.ActiveDocument.EditorState != null &&
                state.Documents.ActiveDocument.EditorState.HasSelection;
        }

        private static bool CanEditActiveDocument(CortexShellState state)
        {
            return state != null &&
                state.Documents != null &&
                state.Documents.ActiveDocument != null &&
                state.Documents.ActiveDocument.SupportsEditing;
        }
    }
}

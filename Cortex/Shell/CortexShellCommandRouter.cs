using System;
using Cortex.Core.Models;
using Cortex.Modules.Editor;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex
{
    internal sealed class CortexShellCommandRouter
    {
        public void RegisterCommandHandlers(CortexShellCommandContext commandContext)
        {
            if (commandContext == null || commandContext.WorkbenchRuntime == null || commandContext.WorkbenchRuntime.CommandRegistry == null)
            {
                return;
            }

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.shell.toggle",
                delegate(CommandExecutionContext context)
                {
                    commandContext.Visible = !commandContext.Visible;
                    if (!commandContext.Visible)
                    {
                        commandContext.PersistWorkbenchSession();
                        commandContext.PersistWindowSettings();
                    }

                    commandContext.State.StatusMessage = commandContext.Visible ? "Cortex opened." : "Cortex closed.";
                },
                delegate(CommandExecutionContext context) { return true; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.logs.toggleWindow",
                delegate(CommandExecutionContext context)
                {
                    commandContext.State.Logs.ShowDetachedWindow = !commandContext.State.Logs.ShowDetachedWindow;
                    commandContext.State.StatusMessage = commandContext.State.Logs.ShowDetachedWindow ? "Detached logs opened." : "Detached logs hidden.";
                },
                delegate(CommandExecutionContext context) { return true; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.shell.fitWindow",
                delegate(CommandExecutionContext context)
                {
                    commandContext.FitMainWindowToScreen();
                    commandContext.State.StatusMessage = "Workbench fitted to screen.";
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.build.execute",
                delegate(CommandExecutionContext context)
                {
                    commandContext.ActivateContainer(CortexWorkbenchIds.BuildContainer);
                    commandContext.State.StatusMessage = "Build panel focused.";
                },
                delegate(CommandExecutionContext context)
                {
                    return commandContext.Visible && commandContext.State.SelectedProject != null;
                });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.saveAll",
                delegate(CommandExecutionContext context)
                {
                    if (commandContext.State.Settings == null || !commandContext.State.Settings.EnableFileSaving)
                    {
                        commandContext.State.StatusMessage = "Enable file saving in Settings before saving source files.";
                        return;
                    }

                    var saved = 0;
                    var blocked = 0;
                    for (var i = 0; i < commandContext.State.Documents.OpenDocuments.Count; i++)
                    {
                        var doc = commandContext.State.Documents.OpenDocuments[i];
                        if (doc == null || !doc.IsDirty || !doc.SupportsSaving || commandContext.DocumentService == null)
                        {
                            continue;
                        }

                        if (commandContext.DocumentService.Save(doc))
                        {
                            saved++;
                        }
                        else
                        {
                            blocked++;
                        }
                    }

                    commandContext.State.StatusMessage = blocked > 0
                        ? "Saved " + saved + " file(s); " + blocked + " blocked by snapshot conflicts."
                        : "Saved " + saved + " file(s).";
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.closeActive",
                delegate(CommandExecutionContext context)
                {
                    if (commandContext.State.Documents.ActiveDocument != null)
                    {
                        var path = commandContext.State.Documents.ActiveDocument.FilePath;
                        CortexModuleUtil.CloseDocument(commandContext.State, path);
                        commandContext.State.StatusMessage = "Closed " + System.IO.Path.GetFileName(path);
                    }
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible && commandContext.State.Documents.ActiveDocument != null; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.settings",
                delegate(CommandExecutionContext context)
                {
                    commandContext.OpenSettingsWindow();
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.fileExplorer",
                delegate(CommandExecutionContext context)
                {
                    var isVisible = !commandContext.State.Workbench.IsHidden(CortexWorkbenchIds.FileExplorerContainer) &&
                        commandContext.ResolveHostLocation(CortexWorkbenchIds.FileExplorerContainer) == WorkbenchHostLocation.SecondarySideHost;
                    if (isVisible)
                    {
                        commandContext.HideContainer(CortexWorkbenchIds.FileExplorerContainer);
                        commandContext.State.StatusMessage = "File Explorer hidden.";
                    }
                    else
                    {
                        commandContext.ActivateContainer(CortexWorkbenchIds.FileExplorerContainer);
                        commandContext.State.StatusMessage = "File Explorer shown.";
                    }
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            RegisterWindowCommand(commandContext, "cortex.window.explorer", CortexWorkbenchIds.FileExplorerContainer, "Explorer window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.projects", CortexWorkbenchIds.ProjectsContainer, "Projects window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.references", CortexWorkbenchIds.ReferenceContainer, "References window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.search", CortexWorkbenchIds.SearchContainer, "Search window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.logs", CortexWorkbenchIds.LogsContainer, "Logs window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.build", CortexWorkbenchIds.BuildContainer, "Build window shown.");
            RegisterWindowCommand(commandContext, "cortex.window.runtime", CortexWorkbenchIds.RuntimeContainer, "Runtime window shown.");

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.settings",
                delegate(CommandExecutionContext context)
                {
                    commandContext.OpenSettingsWindow();
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.zoomIn",
                delegate(CommandExecutionContext context)
                {
                    commandContext.State.StatusMessage = "Font size increase (apply via Settings).";
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.zoomOut",
                delegate(CommandExecutionContext context)
                {
                    commandContext.State.StatusMessage = "Font size decrease (apply via Settings).";
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.win.theme",
                delegate(CommandExecutionContext context)
                {
                    var themes = commandContext.WorkbenchRuntime.ContributionRegistry.GetThemes();
                    if (themes == null || themes.Count == 0)
                    {
                        return;
                    }

                    var current = commandContext.WorkbenchRuntime.ThemeState.ThemeId;
                    var nextIndex = 0;
                    for (var i = 0; i < themes.Count; i++)
                    {
                        if (string.Equals(themes[i].ThemeId, current, StringComparison.OrdinalIgnoreCase))
                        {
                            nextIndex = (i + 1) % themes.Count;
                            break;
                        }
                    }

                    commandContext.WorkbenchRuntime.ThemeState.ThemeId = themes[nextIndex].ThemeId;
                    if (commandContext.State.Settings != null)
                    {
                        commandContext.State.Settings.ThemeId = commandContext.WorkbenchRuntime.ThemeState.ThemeId;
                    }

                    commandContext.State.StatusMessage = "Theme: " + themes[nextIndex].DisplayName;
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.editor.find",
                delegate(CommandExecutionContext context)
                {
                    commandContext.OpenFind();
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible && commandContext.State.Documents.ActiveDocument != null; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.search.next",
                delegate(CommandExecutionContext context)
                {
                    commandContext.ExecuteSearchOrAdvance(1);
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible && commandContext.State.Search != null && commandContext.State.Search.IsVisible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.search.previous",
                delegate(CommandExecutionContext context)
                {
                    commandContext.ExecuteSearchOrAdvance(-1);
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible && commandContext.State.Search != null && commandContext.State.Search.IsVisible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.search.close",
                delegate(CommandExecutionContext context)
                {
                    commandContext.CloseFind();
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible && commandContext.State.Search != null && commandContext.State.Search.IsVisible; });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.editor.goToDefinition",
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    if (target == null)
                    {
                        return;
                    }

                    commandContext.RequestDefinition(target);
                    commandContext.State.StatusMessage = "Go To Definition: " + (target.SymbolText ?? string.Empty);
                },
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    return commandContext.Visible && target != null && target.CanGoToDefinition;
                });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.editor.copySymbol",
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    if (target == null)
                    {
                        return;
                    }

                    GUIUtility.systemCopyBuffer = target.SymbolText ?? string.Empty;
                    commandContext.State.StatusMessage = "Copied symbol.";
                },
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    return commandContext.Visible && target != null && !string.IsNullOrEmpty(target.SymbolText);
                });

            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.editor.copyHoverInfo",
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    if (target == null)
                    {
                        return;
                    }

                    GUIUtility.systemCopyBuffer = target.HoverText ?? string.Empty;
                    commandContext.State.StatusMessage = "Copied hover info.";
                },
                delegate(CommandExecutionContext context)
                {
                    var target = GetEditorCommandTarget(context);
                    return commandContext.Visible && target != null && !string.IsNullOrEmpty(target.HoverText);
                });
        }

        private static EditorCommandTarget GetEditorCommandTarget(CommandExecutionContext context)
        {
            return context != null ? context.Parameter as EditorCommandTarget : null;
        }

        private static void RegisterWindowCommand(CortexShellCommandContext commandContext, string commandId, string containerId, string statusMessage)
        {
            commandContext.WorkbenchRuntime.CommandRegistry.RegisterHandler(
                commandId,
                delegate(CommandExecutionContext context)
                {
                    commandContext.ActivateContainer(containerId);
                    commandContext.State.StatusMessage = statusMessage;
                },
                delegate(CommandExecutionContext context) { return commandContext.Visible; });
        }
    }
}

using System;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using UnityEngine;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyCommandRegistrar
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyWorkflowController _workflowController;

        public HarmonyCommandRegistrar(HarmonyModuleStateStore stateStore, HarmonyWorkflowController workflowController)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _workflowController = workflowController ?? new HarmonyWorkflowController(_stateStore);
        }

        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            RegisterWindowCommand(context);
            RegisterTargetCommand(context, HarmonyPluginIds.ViewPatchesCommandId, "View Harmony Patches", "Inspect Harmony data for the current symbol.", 230, false, string.Empty);
            RegisterTargetCommand(context, HarmonyPluginIds.GeneratePrefixCommandId, "Generate Harmony Prefix", "Prepare a Prefix Harmony patch workflow for the current symbol.", 240, true, "Prefix");
            RegisterTargetCommand(context, HarmonyPluginIds.GeneratePostfixCommandId, "Generate Harmony Postfix", "Prepare a Postfix Harmony patch workflow for the current symbol.", 250, true, "Postfix");

            context.RegisterCommand(HarmonyPluginIds.RefreshCommandId, "Refresh Harmony", "Harmony", "Refresh the Harmony workspace state.", string.Empty, 260, true, false);
            context.RegisterCommandHandler(
                HarmonyPluginIds.RefreshCommandId,
                delegate(CommandExecutionContext executionContext)
                {
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    var workflow = _stateStore.GetWorkflow(runtime);
                    if (workflow == null)
                    {
                        return;
                    }

                    string statusMessage;
                    _workflowController.Refresh(runtime, out statusMessage);
                    workflow.LastUpdatedUtc = DateTime.UtcNow;
                    SetStatus(context.Runtime, statusMessage);
                },
                delegate(CommandExecutionContext executionContext)
                {
                    return CanUsePlugin(context.Runtime);
                });

            context.RegisterCommand(HarmonyPluginIds.CopySummaryCommandId, "Copy Harmony Summary", "Harmony", "Copy the current Harmony summary.", string.Empty, 270, true, false);
            context.RegisterCommandHandler(
                HarmonyPluginIds.CopySummaryCommandId,
                delegate(CommandExecutionContext executionContext)
                {
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    var summaryText = _workflowController.BuildSummaryText(runtime);
                    GUIUtility.systemCopyBuffer = summaryText;
                    SetStatus(context.Runtime, string.IsNullOrEmpty(summaryText) ? "Harmony summary is not available." : "Copied Harmony summary.");
                },
                delegate(CommandExecutionContext executionContext)
                {
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    var workflow = _stateStore.GetWorkflow(runtime);
                    return CanUsePlugin(context.Runtime) && workflow != null && !string.IsNullOrEmpty(workflow.ActiveSymbolDisplay);
                });

            RegisterEditorAction(context, HarmonyPluginIds.ViewPatchesCommandId, "View Harmony Patches", "Inspect Harmony data for the current symbol.", 0);
            RegisterEditorAction(context, HarmonyPluginIds.GeneratePrefixCommandId, "Generate Harmony Prefix", "Prepare a Prefix Harmony patch workflow for the current symbol.", 10);
            RegisterEditorAction(context, HarmonyPluginIds.GeneratePostfixCommandId, "Generate Harmony Postfix", "Prepare a Postfix Harmony patch workflow for the current symbol.", 20);
        }

        private void RegisterWindowCommand(WorkbenchPluginContext context)
        {
            context.RegisterCommand(
                HarmonyPluginIds.OpenWindowCommandId,
                "Show Harmony Window",
                "Workbench",
                "Open the Harmony workbench container.",
                string.Empty,
                225,
                true,
                true);
            context.RegisterCommandHandler(
                HarmonyPluginIds.OpenWindowCommandId,
                delegate(CommandExecutionContext executionContext)
                {
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    if (runtime != null && runtime.Lifecycle != null)
                    {
                        runtime.Lifecycle.RequestContainer(HarmonyPluginIds.ContainerId, WorkbenchHostLocation.SecondarySideHost);
                    }

                    SetStatus(context.Runtime, "Harmony window shown.");
                },
                delegate(CommandExecutionContext executionContext)
                {
                    return CanUsePlugin(context.Runtime);
                });
        }

        private void RegisterTargetCommand(
            WorkbenchPluginContext context,
            string commandId,
            string displayName,
            string description,
            int sortOrder,
            bool activateInsertion,
            string generationKind)
        {
            context.RegisterCommand(commandId, displayName, "Harmony", description, string.Empty, sortOrder, true, false);
            context.RegisterCommandHandler(
                commandId,
                delegate(CommandExecutionContext executionContext)
                {
                    var target = executionContext != null ? executionContext.Parameter as EditorCommandTarget : null;
                    ExecuteTargetCommand(context.Runtime, target, activateInsertion, generationKind);
                    var runtime = HarmonyModuleRuntimeLocator.Get(context.Runtime);
                    if (runtime != null && runtime.Lifecycle != null)
                    {
                        runtime.Lifecycle.RequestContainer(HarmonyPluginIds.ContainerId, WorkbenchHostLocation.SecondarySideHost);
                    }
                },
                delegate(CommandExecutionContext executionContext)
                {
                    return CanUsePlugin(context.Runtime) && executionContext != null && executionContext.Parameter is EditorCommandTarget;
                });
        }

        private void RegisterEditorAction(
            WorkbenchPluginContext context,
            string commandId,
            string title,
            string description,
            int sortOrder)
        {
            context.RegisterEditorContextAction(
                commandId,
                commandId,
                EditorContextIds.Symbol,
                "02_harmony",
                sortOrder,
                EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions,
                string.Empty,
                false,
                false,
                title,
                description);
            context.RegisterMenu(
                commandId,
                MenuProjectionLocation.ContextMenu,
                "02_harmony",
                sortOrder,
                EditorContextIds.Symbol);
        }

        private void ExecuteTargetCommand(
            IWorkbenchRuntimeAccess runtimeAccess,
            EditorCommandTarget target,
            bool activateInsertion,
            string generationKind)
        {
            var runtime = HarmonyModuleRuntimeLocator.Get(runtimeAccess);
            if (runtime == null || target == null)
            {
                SetStatus(runtimeAccess, _workflowController.IsRuntimeAvailable ? "Select a resolvable method before using Harmony actions." : _workflowController.GetUnavailableMessage());
                return;
            }

            string statusMessage;
            var handled = activateInsertion
                ? _workflowController.PrepareGeneration(
                    runtime,
                    target,
                    string.Equals(generationKind, "Postfix", StringComparison.OrdinalIgnoreCase)
                        ? HarmonyPatchGenerationKind.Postfix
                        : HarmonyPatchGenerationKind.Prefix,
                    out statusMessage)
                : _workflowController.ViewPatches(runtime, target, false, out statusMessage);
            if (!handled)
            {
                SetStatus(runtimeAccess, statusMessage);
                return;
            }

            var persistent = _stateStore.ReadPersistent(runtime);
            var workflow = _stateStore.GetWorkflow(runtime);
            var editorContext = ResolveEditorContext(runtime, target);
            var documentState = _stateStore.GetDocument(runtime, editorContext, true);
            var editorState = activateInsertion ? _stateStore.GetEditor(runtime, editorContext, true) : null;
            var targetDisplay = BuildTargetDisplay(target);

            workflow.ActiveSymbolDisplay = targetDisplay;
            workflow.ActiveDocumentPath = target.DocumentPath ?? string.Empty;
            workflow.ActiveContainingTypeName = target.ContainingTypeName ?? string.Empty;
            workflow.ActiveAssemblyName = target.ContainingAssemblyName ?? string.Empty;
            workflow.ActiveMetadataName = target.MetadataName ?? string.Empty;
            workflow.IsInsertionSelectionActive = activateInsertion;
            workflow.LastUpdatedUtc = DateTime.UtcNow;

            persistent.LastInspectedSymbol = targetDisplay;
            persistent.LastDocumentPath = target.DocumentPath ?? string.Empty;
            if (!string.IsNullOrEmpty(generationKind))
            {
                persistent.PreferredGenerationKind = generationKind;
            }

            if (documentState != null)
            {
                documentState.LastInspectedSymbol = targetDisplay;
                documentState.LastDocumentPath = target.DocumentPath ?? string.Empty;
                documentState.LastUpdatedUtc = DateTime.UtcNow;
            }

            if (editorState != null)
            {
                editorState.SelectedLineNumber = -1;
                editorState.SelectedAbsolutePosition = -1;
                editorState.SelectionLabel = string.Empty;
                editorState.LastUpdatedUtc = DateTime.UtcNow;
            }

            _stateStore.WritePersistent(runtime, persistent);
            SetStatus(runtimeAccess, statusMessage);
        }

        private bool CanUsePlugin(IWorkbenchRuntimeAccess runtimeAccess)
        {
            return runtimeAccess != null &&
                HarmonyModuleRuntimeLocator.Get(runtimeAccess) != null &&
                _workflowController.IsRuntimeAvailable;
        }

        private static EditorContextSnapshot ResolveEditorContext(IWorkbenchModuleRuntime runtime, EditorCommandTarget target)
        {
            if (runtime == null || runtime.Editor == null || target == null)
            {
                return null;
            }

            var context = !string.IsNullOrEmpty(target.ContextKey)
                ? runtime.Editor.GetContext(target.ContextKey)
                : null;
            if (context != null)
            {
                return context;
            }

            context = !string.IsNullOrEmpty(target.SurfaceId)
                ? runtime.Editor.GetSurfaceContext(target.SurfaceId)
                : null;
            return context ?? runtime.Editor.GetActiveContext();
        }

        private static void SetStatus(IWorkbenchRuntimeAccess runtimeAccess, string message)
        {
            if (runtimeAccess != null && runtimeAccess.Feedback != null)
            {
                runtimeAccess.Feedback.SetStatusMessage(message ?? string.Empty);
            }
        }
        private static string BuildTargetDisplay(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(target.QualifiedSymbolDisplay))
            {
                return target.QualifiedSymbolDisplay;
            }

            if (!string.IsNullOrEmpty(target.SymbolText))
            {
                return target.SymbolText;
            }

            return !string.IsNullOrEmpty(target.MetadataName)
                ? target.MetadataName
                : "<unknown>";
        }
    }
}

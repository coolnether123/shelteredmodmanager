using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Harmony
{
    internal static class HarmonyCommandContributions
    {
        private static bool _registered;

        public static void EnsureRegistered(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry, IHarmonyFeatureServices services)
        {
            if (_registered || commandRegistry == null || contributionRegistry == null || services == null || services.State == null)
            {
                return;
            }

            _registered = true;
            new Registrar(commandRegistry, contributionRegistry, services).Register();
        }

        private sealed class Registrar
        {
            private readonly ICommandRegistry _commandRegistry;
            private readonly IContributionRegistry _contributionRegistry;
            private readonly IHarmonyFeatureServices _services;

            public Registrar(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry, IHarmonyFeatureServices services)
            {
                _commandRegistry = commandRegistry;
                _contributionRegistry = contributionRegistry;
                _services = services;
            }

            public void Register()
            {
                MMLog.WriteInfo("[Cortex.Harmony] Registering Harmony commands, menus, and editor actions.");
                RegisterCommand(
                    "cortex.harmony.viewPatches",
                    "View Harmony Patches",
                    "Harmony",
                    "Inspect live Harmony patch metadata for the current method.",
                    230,
                    ExecuteViewPatches,
                    CanExecuteForResolvedMethod);

                RegisterCommand(
                    "cortex.harmony.generatePrefix",
                    "Generate Harmony Prefix",
                    "Harmony",
                    "Generate a Prefix Harmony patch scaffold for the current method.",
                    240,
                    ExecuteGeneratePrefix,
                    CanExecuteForGeneration);

                RegisterCommand(
                    "cortex.harmony.generatePostfix",
                    "Generate Harmony Postfix",
                    "Harmony",
                    "Generate a Postfix Harmony patch scaffold for the current method.",
                    250,
                    ExecuteGeneratePostfix,
                    CanExecuteForGeneration);

                RegisterCommand(
                    "cortex.harmony.refresh",
                    "Refresh Harmony Info",
                    "Harmony",
                    "Refresh cached Harmony runtime inspection data.",
                    260,
                    ExecuteRefresh,
                    CanExecuteForRefresh);

                RegisterCommand(
                    "cortex.harmony.copySummary",
                    "Copy Harmony Patch Summary",
                    "Harmony",
                    "Copy the active Harmony patch summary to the clipboard.",
                    270,
                    ExecuteCopySummary,
                    CanExecuteForCopySummary);

                RegisterMenuAction("cortex.harmony.viewPatches", 0);
                RegisterMenuAction("cortex.harmony.generatePrefix", 10);
                RegisterMenuAction("cortex.harmony.generatePostfix", 20);

                RegisterEditorAction("cortex.harmony.viewPatches", "View Harmony Patches", "Inspect live Harmony patch metadata for the current method.", 0);
                RegisterEditorAction("cortex.harmony.generatePrefix", "Generate Harmony Prefix", "Create a Prefix Harmony patch scaffold for the current method.", 10);
                RegisterEditorAction("cortex.harmony.generatePostfix", "Generate Harmony Postfix", "Create a Postfix Harmony patch scaffold for the current method.", 20);
                MMLog.WriteInfo("[Cortex.Harmony] Harmony command registration complete. Commands=5, ContextMenuActions=3, EditorActions=3.");
            }

            private void RegisterCommand(
                string commandId,
                string displayName,
                string category,
                string description,
                int sortOrder,
                CommandHandler handler,
                CommandEnablement canExecute)
            {
                if (_commandRegistry.Get(commandId) == null)
                {
                    _commandRegistry.Register(new CommandDefinition
                    {
                        CommandId = commandId,
                        DisplayName = displayName,
                        Category = category,
                        Description = description,
                        SortOrder = sortOrder,
                        ShowInPalette = true
                    });
                }

                _commandRegistry.RegisterHandler(commandId, handler, canExecute);
            }

            private void RegisterMenuAction(string commandId, int sortOrder)
            {
                _contributionRegistry.RegisterMenu(new MenuContribution
                {
                    CommandId = commandId,
                    Location = MenuProjectionLocation.ContextMenu,
                    ContextId = EditorContextIds.Symbol,
                    Group = "02_harmony",
                    ShowWhenDisabled = false,
                    SortOrder = sortOrder
                });
            }

            private void RegisterEditorAction(string commandId, string title, string description, int sortOrder)
            {
                _contributionRegistry.RegisterEditorContextAction(new EditorContextActionContribution
                {
                    ActionId = commandId,
                    CommandId = commandId,
                    ContextId = EditorContextIds.Symbol,
                    Group = "02_harmony",
                    SortOrder = sortOrder,
                    Placements = EditorContextActionPlacement.ActionBar | EditorContextActionPlacement.QuickActions,
                    RequiredCapability = string.Empty,
                    IncludeWhenNoSymbol = false,
                    ShowWhenDisabled = false,
                    Title = title,
                    Description = description
                });
            }

            private bool CanExecuteForResolvedMethod(CommandExecutionContext context)
            {
                HarmonyResolvedMethodTarget resolvedTarget;
                string reason;
                if (TryResolveTarget(context, out resolvedTarget, out reason))
                {
                    return true;
                }

                HarmonyResolvedTypeTarget resolvedTypeTarget;
                return TryResolveTypeTarget(context, out resolvedTypeTarget, out reason);
            }

            private bool CanExecuteForGeneration(CommandExecutionContext context)
            {
                HarmonyResolvedMethodTarget resolvedTarget;
                string reason;
                if (!TryResolveTarget(context, out resolvedTarget, out reason))
                {
                    return false;
                }

                return _services.HarmonyPatchGenerationService != null &&
                    _services.HarmonyPatchGenerationService.TryValidateGenerationTarget(_services.State, resolvedTarget, out reason);
            }

            private bool CanExecuteForRefresh(CommandExecutionContext context)
            {
                HarmonyResolvedMethodTarget resolvedTarget;
                string reason;
                return TryResolveTarget(context, out resolvedTarget, out reason) ||
                    (_services.State != null && _services.State.Harmony != null && _services.State.Harmony.ActiveInspectionRequest != null);
            }

            private bool CanExecuteForCopySummary(CommandExecutionContext context)
            {
                return _services.State != null &&
                    _services.State.Harmony != null &&
                    _services.State.Harmony.ActiveSummary != null;
            }

            private void ExecuteViewPatches(CommandExecutionContext context)
            {
                MMLog.WriteInfo("[Cortex.Harmony] View Harmony Patches invoked. Target=" + BuildContextTargetLabel(context) + ".");
                LoadSummary(context, false, false, HarmonyPatchGenerationKind.Prefix);
            }

            private void ExecuteGeneratePrefix(CommandExecutionContext context)
            {
                LoadSummary(context, true, false, HarmonyPatchGenerationKind.Prefix);
            }

            private void ExecuteGeneratePostfix(CommandExecutionContext context)
            {
                LoadSummary(context, true, false, HarmonyPatchGenerationKind.Postfix);
            }

            private void ExecuteRefresh(CommandExecutionContext context)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null)
                {
                    return;
                }

                state.Harmony.RefreshRequested = true;
                if (state.Harmony.ActiveInspectionRequest != null)
                {
                    string statusMessage;
                    state.Harmony.ActiveSummary = _services.HarmonyPatchInspectionService.GetSummary(
                        state,
                        state.Harmony.ActiveInspectionRequest,
                        _services.LoadedModCatalog,
                        _services.ProjectCatalog,
                        true,
                        out statusMessage);
                    state.StatusMessage = statusMessage;
                    OpenHarmonyWindow();
                    return;
                }

                LoadSummary(context, false, true, HarmonyPatchGenerationKind.Prefix);
            }

            private void ExecuteCopySummary(CommandExecutionContext context)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null)
                {
                    return;
                }

                if (state.Harmony.ActiveSummary == null)
                {
                    LoadSummary(context, false, false, HarmonyPatchGenerationKind.Prefix);
                }

                if (state.Harmony.ActiveSummary == null)
                {
                    state.StatusMessage = "Harmony patch summary is not available.";
                    return;
                }

                GUIUtility.systemCopyBuffer = _services.HarmonyPatchDisplayService.BuildPatchSummaryClipboardText(state.Harmony.ActiveSummary);
                state.StatusMessage = "Copied Harmony patch summary.";
            }

            private void LoadSummary(CommandExecutionContext context, bool prepareGeneration, bool forceRefresh, HarmonyPatchGenerationKind generationKind)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null)
                {
                    return;
                }

                HarmonyResolvedMethodTarget resolvedTarget;
                string reason;
                if (TryResolveTarget(context, out resolvedTarget, out reason))
                {
                    LoadMethodSummary(resolvedTarget, prepareGeneration, forceRefresh, generationKind);
                    OpenHarmonyWindow();
                    return;
                }

                if (!prepareGeneration)
                {
                    HarmonyResolvedTypeTarget resolvedTypeTarget;
                    if (TryResolveTypeTarget(context, out resolvedTypeTarget, out reason))
                    {
                        LoadTypeSummary(resolvedTypeTarget, forceRefresh);
                        OpenHarmonyWindow();
                        return;
                    }
                }

                ClearTypeScope();
                state.Harmony.ResolutionFailureReason = reason;
                state.StatusMessage = reason;
                MMLog.WriteWarning("[Cortex.Harmony] Command target resolution failed. Reason=" + (reason ?? string.Empty) + ".");
                OpenHarmonyWindow();
            }

            private void LoadMethodSummary(HarmonyResolvedMethodTarget resolvedTarget, bool prepareGeneration, bool forceRefresh, HarmonyPatchGenerationKind generationKind)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null || resolvedTarget == null)
                {
                    return;
                }

                string statusMessage;
                var summary = _services.HarmonyPatchInspectionService.GetSummary(
                    state,
                    resolvedTarget.InspectionRequest,
                    _services.LoadedModCatalog,
                    _services.ProjectCatalog,
                    forceRefresh,
                    out statusMessage);

                ClearTypeScope();
                state.Harmony.ActiveInspectionRequest = resolvedTarget.InspectionRequest;
                state.Harmony.ActiveSummaryKey = _services.HarmonyPatchInspectionService.BuildKey(resolvedTarget.InspectionRequest);
                state.Harmony.ActiveSummary = summary;
                state.Harmony.ResolutionFailureReason = string.Empty;
                state.StatusMessage = statusMessage;
                MMLog.WriteInfo("[Cortex.Harmony] Opened Harmony details for '" +
                    (resolvedTarget.DisplayName ?? string.Empty) +
                    "'. PrepareGeneration=" + prepareGeneration +
                    ", ForceRefresh=" + forceRefresh + ".");

                if (prepareGeneration)
                {
                    PrepareGeneration(resolvedTarget, generationKind);
                }
            }

            private void LoadTypeSummary(HarmonyResolvedTypeTarget resolvedTypeTarget, bool forceRefresh)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null || resolvedTypeTarget == null || resolvedTypeTarget.DeclaringType == null)
                {
                    return;
                }

                ClearGenerationState();
                string statusMessage;
                var summaries = _services.HarmonyPatchInspectionService.GetTypeSummaries(
                    state,
                    resolvedTypeTarget.AssemblyPath,
                    resolvedTypeTarget.DeclaringType.FullName ?? resolvedTypeTarget.DeclaringType.Name ?? string.Empty,
                    _services.LoadedModCatalog,
                    _services.ProjectCatalog,
                    forceRefresh,
                    out statusMessage);

                state.Harmony.ActiveInspectionRequest = null;
                state.Harmony.ActiveSummaryKey = string.Empty;
                state.Harmony.ActiveSummary = null;
                state.Harmony.ActiveTypeAssemblyPath = resolvedTypeTarget.AssemblyPath ?? string.Empty;
                state.Harmony.ActiveTypeName = resolvedTypeTarget.DeclaringType.FullName ?? resolvedTypeTarget.DeclaringType.Name ?? string.Empty;
                state.Harmony.ActiveTypeDisplayName = resolvedTypeTarget.DisplayName ?? state.Harmony.ActiveTypeName;
                state.Harmony.ActiveTypeSummaries = summaries ?? new HarmonyMethodPatchSummary[0];
                state.Harmony.ResolutionFailureReason = string.Empty;
                state.StatusMessage = statusMessage;
                MMLog.WriteInfo("[Cortex.Harmony] Opened Harmony type details for '" +
                    (state.Harmony.ActiveTypeDisplayName ?? string.Empty) +
                    "'. PatchedMethods=" + state.Harmony.ActiveTypeSummaries.Length +
                    ", ForceRefresh=" + forceRefresh + ".");
            }

            private void PrepareGeneration(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
            {
                var state = _services.State;
                if (state == null || state.Harmony == null || resolvedTarget == null)
                {
                    return;
                }

                var reason = string.Empty;
                if (_services.HarmonyPatchGenerationService == null ||
                    !_services.HarmonyPatchGenerationService.TryValidateGenerationTarget(_services.State, resolvedTarget, out reason))
                {
                    ClearGenerationState();
                    state.Harmony.GenerationStatusMessage = !string.IsNullOrEmpty(reason)
                        ? reason
                        : "Harmony patch generation is not available for the selected method.";
                    state.StatusMessage = state.Harmony.GenerationStatusMessage;
                    MMLog.WriteWarning("[Cortex.Harmony] Rejected " + generationKind +
                        " generation for '" + (resolvedTarget.DisplayName ?? string.Empty) +
                        "'. Reason=" + (state.Harmony.GenerationStatusMessage ?? string.Empty) + ".");
                    return;
                }

                var request = _services.HarmonyPatchGenerationService.CreateDefaultRequest(resolvedTarget, generationKind);
                var targets = _services.HarmonyPatchGenerationService.BuildInsertionTargets(state, _services.ProjectCatalog, resolvedTarget, request);
                state.Harmony.InsertionTargets.Clear();
                for (var i = 0; i < targets.Length; i++)
                {
                    state.Harmony.InsertionTargets.Add(targets[i]);
                }

                if (targets.Length > 0)
                {
                    request.DestinationFilePath = targets[0].FilePath;
                    request.InsertionAnchorKind = targets[0].DefaultAnchorKind;
                    request.InsertionLine = targets[0].SuggestedLine;
                }

                state.Harmony.GenerationRequest = request;
                state.Harmony.GenerationPreview = _services.HarmonyPatchGenerationService.BuildPreview(state, resolvedTarget, request);
                if (state.Harmony.GenerationPreview != null)
                {
                    request.InsertionContextLabel = state.Harmony.GenerationPreview.InsertionContextLabel ?? request.InsertionContextLabel;
                }
                state.Harmony.GenerationStatusMessage = state.Harmony.GenerationPreview != null
                    ? state.Harmony.GenerationPreview.StatusMessage ?? string.Empty
                    : string.Empty;
                _services.HarmonyPatchGenerationService.ArmEditorInsertionPick(state);
                MMLog.WriteInfo("[Cortex.Harmony] Prepared " + generationKind +
                    " generation preview for '" + (resolvedTarget.DisplayName ?? string.Empty) +
                    "'. Destination='" + (request.DestinationFilePath ?? string.Empty) + "'. EditorInsertionPickArmed=True.");
            }

            private void ClearGenerationState()
            {
                var state = _services.State;
                if (state == null || state.Harmony == null)
                {
                    return;
                }

                state.Harmony.GenerationRequest = null;
                state.Harmony.GenerationPreview = null;
                state.Harmony.IsInsertionPickActive = false;
                state.Harmony.InsertionTargets.Clear();
            }

            private void ClearTypeScope()
            {
                var state = _services.State;
                if (state == null || state.Harmony == null)
                {
                    return;
                }

                state.Harmony.ActiveTypeAssemblyPath = string.Empty;
                state.Harmony.ActiveTypeName = string.Empty;
                state.Harmony.ActiveTypeDisplayName = string.Empty;
                state.Harmony.ActiveTypeSummaries = new HarmonyMethodPatchSummary[0];
            }

            private bool TryResolveTarget(CommandExecutionContext context, out HarmonyResolvedMethodTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;

                var target = GetTarget(context);
                if (target != null)
                {
                    var resolvedFromEditorTarget = _services.HarmonyPatchResolutionService.TryResolveFromEditorTarget(
                        _services.State,
                        _services.SourceLookupIndex,
                        _services.ProjectCatalog,
                        target,
                        out resolvedTarget,
                        out reason);
                    MMLog.WriteInfo("[Cortex.Harmony] Resolve target from editor context. Success=" + resolvedFromEditorTarget +
                        ", Document='" + (target.DocumentPath ?? string.Empty) +
                        "', Symbol='" + (target.SymbolText ?? string.Empty) +
                        "', Position=" + target.AbsolutePosition +
                        ", Reason='" + (reason ?? string.Empty) + "'.");
                    return resolvedFromEditorTarget;
                }

                var activeRequest = _services.State != null && _services.State.Harmony != null
                    ? _services.State.Harmony.ActiveInspectionRequest
                    : null;
                if (activeRequest != null)
                {
                    var resolvedFromActiveRequest = _services.HarmonyPatchResolutionService.TryResolveFromInspectionRequest(
                        _services.ProjectCatalog,
                        activeRequest,
                        out resolvedTarget,
                        out reason);
                    MMLog.WriteInfo("[Cortex.Harmony] Resolve target from active inspection request. Success=" + resolvedFromActiveRequest +
                        ", Assembly='" + (activeRequest.AssemblyPath ?? string.Empty) +
                        "', MetadataToken=0x" + activeRequest.MetadataToken.ToString("X8") +
                        ", Display='" + (activeRequest.DisplayName ?? string.Empty) +
                        "', Reason='" + (reason ?? string.Empty) + "'.");
                    return resolvedFromActiveRequest;
                }

                reason = "Select a resolvable method before using Harmony actions.";
                MMLog.WriteWarning("[Cortex.Harmony] Resolve target failed because no editor target or active Harmony request was available.");
                return false;
            }

            private bool TryResolveTypeTarget(CommandExecutionContext context, out HarmonyResolvedTypeTarget resolvedTarget, out string reason)
            {
                resolvedTarget = null;
                reason = string.Empty;
                var target = GetTarget(context);
                if (target == null)
                {
                    reason = "Select a decompiled type before viewing Harmony patches in that area.";
                    return false;
                }

                var resolvedFromEditorTarget = _services.HarmonyPatchResolutionService.TryResolveTypeFromEditorTarget(
                    _services.State,
                    _services.SourceLookupIndex,
                    _services.ProjectCatalog,
                    target,
                    out resolvedTarget,
                    out reason);
                MMLog.WriteInfo("[Cortex.Harmony] Resolve type target from editor context. Success=" + resolvedFromEditorTarget +
                    ", Document='" + (target.DocumentPath ?? string.Empty) +
                    "', Symbol='" + (target.SymbolText ?? string.Empty) +
                    "', Position=" + target.AbsolutePosition +
                    ", Reason='" + (reason ?? string.Empty) + "'.");
                return resolvedFromEditorTarget;
            }

            private void OpenHarmonyWindow()
            {
                var state = _services.State;
                if (state == null)
                {
                    return;
                }

                state.Workbench.AssignHost(CortexWorkbenchIds.HarmonyContainer, WorkbenchHostLocation.SecondarySideHost);
                var opened = _commandRegistry.Execute("cortex.window.harmony", new CommandExecutionContext
                {
                    ActiveContainerId = state.Workbench.FocusedContainerId,
                    ActiveDocumentId = state.Documents.ActiveDocumentPath,
                    FocusedRegionId = state.Workbench.FocusedContainerId
                });
                MMLog.WriteInfo("[Cortex.Harmony] Request to open Harmony window. RoutedCommand=" + opened + ".");
                if (!opened)
                {
                    state.Workbench.RequestedContainerId = CortexWorkbenchIds.HarmonyContainer;
                    MMLog.WriteInfo("[Cortex.Harmony] Falling back to requested Harmony container '" + CortexWorkbenchIds.HarmonyContainer + "'.");
                }
            }

            private static EditorCommandTarget GetTarget(CommandExecutionContext context)
            {
                return context != null ? context.Parameter as EditorCommandTarget : null;
            }

            private static string BuildContextTargetLabel(CommandExecutionContext context)
            {
                var target = GetTarget(context);
                if (target == null)
                {
                    return "<none>";
                }

                return "Document='" + (target.DocumentPath ?? string.Empty) +
                    "', Symbol='" + (target.SymbolText ?? string.Empty) +
                    "', Line=" + target.Line +
                    ", Column=" + target.Column +
                    ", Position=" + target.AbsolutePosition;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Plugin.Harmony.Services;
using Cortex.Plugin.Harmony.Services.Editor;
using Cortex.Plugin.Harmony.Services.Generation;
using Cortex.Plugin.Harmony.Services.Presentation;
using Cortex.Plugin.Harmony.Services.Resolution;
using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyWorkflowController
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly HarmonyMethodResolver _resolver;
        private readonly Runtime.HarmonyRuntimeInspectionService _runtimeInspectionService;
        private readonly HarmonyPatchDisplayService _displayService;
        private readonly HarmonyPatchTemplateService _templateService;
        private readonly HarmonyPatchInsertionService _insertionService;
        private readonly HarmonyTemplateNavigationService _templateNavigationService;
        private readonly HarmonyMethodInspectorNavigationActionFactory _navigationActionFactory;

        public HarmonyWorkflowController(HarmonyModuleStateStore stateStore)
            : this(
                stateStore,
                new HarmonyMethodResolver(),
                new Runtime.HarmonyRuntimeInspectionService(),
                new HarmonyPatchDisplayService(),
                new HarmonyPatchTemplateService(),
                new HarmonyPatchInsertionService(stateStore),
                new HarmonyTemplateNavigationService(stateStore),
                new HarmonyMethodInspectorNavigationActionFactory())
        {
        }

        internal HarmonyWorkflowController(
            HarmonyModuleStateStore stateStore,
            HarmonyMethodResolver resolver,
            Runtime.HarmonyRuntimeInspectionService runtimeInspectionService,
            HarmonyPatchDisplayService displayService,
            HarmonyPatchTemplateService templateService,
            HarmonyPatchInsertionService insertionService,
            HarmonyTemplateNavigationService templateNavigationService,
            HarmonyMethodInspectorNavigationActionFactory navigationActionFactory)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _resolver = resolver ?? new HarmonyMethodResolver();
            _runtimeInspectionService = runtimeInspectionService ?? new Runtime.HarmonyRuntimeInspectionService();
            _displayService = displayService ?? new HarmonyPatchDisplayService();
            _templateService = templateService ?? new HarmonyPatchTemplateService();
            _insertionService = insertionService ?? new HarmonyPatchInsertionService(_stateStore);
            _templateNavigationService = templateNavigationService ?? new HarmonyTemplateNavigationService(_stateStore);
            _navigationActionFactory = navigationActionFactory ?? new HarmonyMethodInspectorNavigationActionFactory();
        }

        public HarmonyPatchDisplayService DisplayService
        {
            get { return _displayService; }
        }

        public bool IsRuntimeAvailable
        {
            get { return _runtimeInspectionService != null && _runtimeInspectionService.IsAvailable; }
        }

        public string GetUnavailableMessage()
        {
            return IsRuntimeAvailable
                ? string.Empty
                : (_runtimeInspectionService != null ? _runtimeInspectionService.UnavailableReason : "Harmony module is unavailable.");
        }

        public bool ViewPatches(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, bool forceRefresh, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            if (!IsRuntimeAvailable || runtime == null)
            {
                return false;
            }

            statusMessage = "Select a resolvable method before using Harmony actions.";

            HarmonyResolvedMethodTarget resolvedMethod;
            string reason;
            if (_resolver.TryResolveMethod(runtime, target, out resolvedMethod, out reason) && resolvedMethod != null)
            {
                LoadMethodSummary(runtime, resolvedMethod, forceRefresh, out statusMessage);
                return true;
            }

            HarmonyResolvedTypeTarget resolvedType;
            if (_resolver.TryResolveType(runtime, target, out resolvedType, out reason) && resolvedType != null)
            {
                LoadTypeSummary(runtime, resolvedType, forceRefresh, out statusMessage);
                return true;
            }

            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow != null)
            {
                workflow.ResolutionFailureReason = reason ?? statusMessage;
            }

            statusMessage = reason ?? statusMessage;
            return false;
        }

        public bool PrepareGeneration(
            IWorkbenchModuleRuntime runtime,
            EditorCommandTarget target,
            HarmonyPatchGenerationKind generationKind,
            out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            if (!IsRuntimeAvailable || runtime == null)
            {
                return false;
            }

            statusMessage = "Harmony patch generation is not available for the selected method.";

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!_resolver.TryResolveMethod(runtime, target, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                !TryValidateGenerationTarget(resolvedTarget, out reason))
            {
                statusMessage = reason ?? statusMessage;
                return false;
            }

            LoadMethodSummary(runtime, resolvedTarget, false, out reason);
            return BeginGeneration(runtime, resolvedTarget, generationKind, out statusMessage);
        }

        public bool Refresh(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null)
            {
                return false;
            }

            statusMessage = "Harmony state refreshed.";

            workflow.RefreshRequested = true;
            if (workflow.ActiveInspectionRequest != null)
            {
                HarmonyResolvedMethodTarget resolvedTarget;
                string reason;
                if (_resolver.TryResolveMethod(runtime, workflow.ActiveInspectionRequest, out resolvedTarget, out reason) && resolvedTarget != null)
                {
                    LoadMethodSummary(runtime, resolvedTarget, true, out statusMessage);
                    return true;
                }
            }

            RefreshSnapshot(runtime);
            statusMessage = workflow.SnapshotStatusMessage ?? statusMessage;
            return true;
        }

        public string BuildSummaryText(IWorkbenchModuleRuntime runtime)
        {
            if (!IsRuntimeAvailable)
            {
                return GetUnavailableMessage();
            }

            var workflow = _stateStore.GetWorkflow(runtime);
            var persistent = _stateStore.ReadPersistent(runtime);
            var summary = workflow != null ? workflow.ActiveSummary : null;
            if (summary != null)
            {
                return _displayService.BuildPatchSummaryClipboardText(summary);
            }

            return "Harmony Target: " + (workflow != null ? workflow.ActiveSymbolDisplay ?? string.Empty : string.Empty) + Environment.NewLine +
                "Preferred Generation: " + (persistent != null ? persistent.PreferredGenerationKind ?? string.Empty : string.Empty) + Environment.NewLine +
                "Document: " + (persistent != null ? persistent.LastDocumentPath ?? string.Empty : string.Empty);
        }

        public bool NavigateToTarget(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            if (!IsRuntimeAvailable)
            {
                return false;
            }

            statusMessage = "Could not open the Harmony target method.";
            var workflow = _stateStore.GetWorkflow(runtime);
            var summary = workflow != null ? workflow.ActiveSummary : null;
            return summary != null && TryNavigate(runtime, summary.Target, "Opened Harmony target method.", out statusMessage);
        }

        public bool NavigateToPatch(IWorkbenchModuleRuntime runtime, HarmonyPatchNavigationTarget target, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            if (!IsRuntimeAvailable)
            {
                return false;
            }

            statusMessage = "Could not open the Harmony patch method.";
            return TryNavigate(runtime, target, "Opened Harmony patch method.", out statusMessage);
        }

        public bool BeginGeneration(IWorkbenchModuleRuntime runtime, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || resolvedTarget == null)
            {
                return false;
            }

            statusMessage = "Harmony patch generation is not available for the selected method.";

            var request = CreateDefaultRequest(resolvedTarget, generationKind);
            var insertionTargets = _insertionService.BuildInsertionTargets(runtime, resolvedTarget, request);
            workflow.GenerationRequest = request;
            workflow.GenerationPreview = null;
            workflow.GenerationStatusMessage = string.Empty;
            workflow.InsertionTargets.Clear();
            for (var i = 0; i < insertionTargets.Length; i++)
            {
                workflow.InsertionTargets.Add(insertionTargets[i]);
            }

            if (insertionTargets.Length > 0)
            {
                request.DestinationFilePath = insertionTargets[0].FilePath ?? string.Empty;
                request.InsertionAnchorKind = insertionTargets[0].DefaultAnchorKind;
                request.InsertionLine = insertionTargets[0].SuggestedLine;
                request.InsertionAbsolutePosition = insertionTargets[0].SuggestedAbsolutePosition;
                request.InsertionContextLabel = insertionTargets[0].SuggestedContextLabel ?? string.Empty;
            }

            workflow.IsInsertionSelectionActive = true;
            return RefreshGenerationPreview(runtime, out statusMessage);
        }

        public bool RefreshGenerationPreview(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || workflow.GenerationRequest == null)
            {
                return false;
            }

            statusMessage = "Harmony patch preview is not available.";

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!_resolver.TryResolveMethod(runtime, workflow.ActiveInspectionRequest, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                !TryValidateGenerationTarget(resolvedTarget, out reason))
            {
                workflow.GenerationStatusMessage = reason ?? statusMessage;
                statusMessage = workflow.GenerationStatusMessage;
                return false;
            }

            var snippetPreview = _templateService.BuildSnippet(resolvedTarget, workflow.GenerationRequest);
            workflow.GenerationPreview = _insertionService.BuildPreview(runtime, workflow.GenerationRequest, snippetPreview);
            workflow.GenerationStatusMessage = workflow.GenerationPreview != null
                ? workflow.GenerationPreview.StatusMessage ?? string.Empty
                : statusMessage;
            statusMessage = workflow.GenerationStatusMessage;
            return workflow.GenerationPreview != null && workflow.GenerationPreview.CanApply;
        }

        public bool ApplyGeneration(IWorkbenchModuleRuntime runtime, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || runtime == null || workflow == null || workflow.GenerationRequest == null)
            {
                return false;
            }

            statusMessage = "Harmony patch preview is not ready to apply.";

            RefreshGenerationPreview(runtime, out statusMessage);
            if (workflow.GenerationPreview == null || !workflow.GenerationPreview.CanApply)
            {
                return false;
            }

            DocumentSession session;
            if (!_insertionService.ApplyPreview(runtime, workflow.GenerationRequest, workflow.GenerationPreview, out session, out statusMessage))
            {
                return false;
            }

            if (session != null)
            {
                _templateNavigationService.StartSession(
                    runtime,
                    session,
                    workflow.GenerationPreview.Placeholders,
                    workflow.GenerationPreview.InsertionOffset,
                    workflow.GenerationPreview.InsertionOffset + ((workflow.GenerationPreview.SnippetText ?? string.Empty).Length));
            }

            workflow.IsInsertionSelectionActive = false;
            return true;
        }

        public bool IsInsertionSelectionActive(IWorkbenchModuleRuntime runtime)
        {
            if (!IsRuntimeAvailable)
            {
                return false;
            }

            var workflow = _stateStore.GetWorkflow(runtime);
            return workflow != null && workflow.IsInsertionSelectionActive;
        }

        public bool HasTemplateSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            return IsRuntimeAvailable && _templateNavigationService.HasActiveSession(runtime, session);
        }

        public void SynchronizeInsertionSelection(WorkbenchEditorWorkflowContext context)
        {
            if (context != null && context.Runtime != null && context.Runtime.Feedback != null)
            {
                context.Runtime.Feedback.SetStatusMessage(
                    IsRuntimeAvailable
                        ? "Click a writable editor line to place the Harmony patch. Press Escape to cancel."
                        : GetUnavailableMessage());
            }
        }

        public void SynchronizeTemplateSession(WorkbenchEditorWorkflowContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return;
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (runtime != null)
            {
                _templateNavigationService.SyncSession(runtime, context != null ? context.Session : null);
            }
        }

        public WorkbenchEditorWorkflowResult HandleTemplateKeyboard(WorkbenchEditorKeyboardContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (runtime == null ||
                context == null ||
                context.Session == null ||
                context.Key != WorkbenchEditorInteractionKey.Tab ||
                context.Control ||
                context.Alt ||
                !_templateNavigationService.TryHandleNavigation(runtime, context.Session, context.Shift))
            {
                return new WorkbenchEditorWorkflowResult();
            }

            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        public WorkbenchEditorWorkflowResult HandleInsertionPointer(WorkbenchEditorPointerContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || workflow == null || !workflow.IsInsertionSelectionActive)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            string statusMessage;
            if (!_insertionService.TryApplyEditorInsertionSelection(
                runtime,
                context.EditorContext,
                context.Session,
                context.LineNumber,
                context.AbsolutePosition,
                out statusMessage))
            {
                SetStatus(context.Runtime, statusMessage);
            }
            else
            {
                RefreshGenerationPreview(runtime, out statusMessage);
                SetStatus(context.Runtime, statusMessage);
            }

            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        public WorkbenchEditorWorkflowResult HandleInsertionKeyboard(WorkbenchEditorKeyboardContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || workflow == null || !workflow.IsInsertionSelectionActive)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            if (context.Key == WorkbenchEditorInteractionKey.Escape)
            {
                workflow.IsInsertionSelectionActive = false;
                workflow.GenerationStatusMessage = "Harmony insertion selection cancelled.";
                SetStatus(context.Runtime, workflow.GenerationStatusMessage);
                return new WorkbenchEditorWorkflowResult
                {
                    Handled = true,
                    ConsumeInput = true
                };
            }

            if (context.Key != WorkbenchEditorInteractionKey.Tab || context.Shift || context.Control || context.Alt || !context.EditingEnabled || context.Session == null)
            {
                return new WorkbenchEditorWorkflowResult();
            }

            var caretIndex = context.Session.EditorState != null ? context.Session.EditorState.CaretIndex : 0;
            var lineNumber = context.Session.HighlightedLine > 0 ? context.Session.HighlightedLine : 1;
            string statusMessage;
            if (!_insertionService.TryApplyEditorInsertionSelection(runtime, context.EditorContext, context.Session, lineNumber, caretIndex, out statusMessage))
            {
                SetStatus(context.Runtime, statusMessage);
                return new WorkbenchEditorWorkflowResult
                {
                    Handled = true,
                    ConsumeInput = true
                };
            }

            ApplyGeneration(runtime, out statusMessage);
            SetStatus(context.Runtime, statusMessage);
            return new WorkbenchEditorWorkflowResult
            {
                Handled = true,
                ConsumeInput = true
            };
        }

        public WorkbenchEditorAdornment[] BuildAdornments(WorkbenchEditorAdornmentContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorAdornment[0];
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || context.Target == null)
            {
                return new WorkbenchEditorAdornment[0];
            }

            var label = "HM";
            var toolTip = "View Harmony context for " + BuildTargetDisplay(context.Target) + ".";
            if (workflow != null && workflow.IsInsertionSelectionActive)
            {
                label = "Pick Insert";
                toolTip = "Select a writable editor location for the pending Harmony patch.";
            }
            else
            {
                string statusMessage;
                var summary = TryResolveSummary(runtime, context.Target, false, out statusMessage);
                if (summary != null && summary.IsPatched)
                {
                    var badge = _displayService.BuildBadgeText(summary);
                    if (!string.IsNullOrEmpty(badge))
                    {
                        label = badge;
                    }

                    toolTip = _displayService.BuildCountBreakdown(summary.Counts);
                }
            }

            return new[]
            {
                new WorkbenchEditorAdornment
                {
                    AdornmentId = HarmonyPluginIds.EditorAdornmentId,
                    Label = label,
                    ToolTip = toolTip,
                    CommandId = HarmonyPluginIds.ViewPatchesCommandId,
                    CommandParameter = context.Target,
                    Placement = WorkbenchEditorAdornmentPlacement.TopRight,
                    Enabled = true,
                    SortOrder = 100
                }
            };
        }

        public MethodInspectorSectionViewModel BuildInspectorSection(WorkbenchMethodInspectorContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return null;
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (context == null || runtime == null || context.Target == null)
            {
                return null;
            }

            string statusMessage;
            var summary = TryResolveSummary(runtime, context.Target, false, out statusMessage);
            HarmonySourcePatchContext sourcePatchContext;
            string sourceReason;
            _resolver.TryResolveSourcePatchContext(runtime, context.Target, out sourcePatchContext, out sourceReason);

            var elements = new List<MethodInspectorElementViewModel>();
            if (sourcePatchContext != null)
            {
                elements.Add(CreateMetadata("Current Method", "Harmony " + (sourcePatchContext.PatchKind ?? string.Empty) + " patch"));
                elements.Add(CreateMetadata("Patches Into", sourcePatchContext.Target != null ? sourcePatchContext.Target.DisplayName ?? string.Empty : string.Empty));
                elements.Add(CreateMetadata("Resolved Via", sourcePatchContext.ResolutionSource ?? string.Empty));
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
            }

            if (summary != null)
            {
                elements.Add(CreateMetadata("Target", _displayService.BuildTargetDisplayName(summary)));
                elements.Add(CreateMetadata("Counts", _displayService.BuildCountBreakdown(summary.Counts)));
                elements.Add(CreateMetadata("Owners", _displayService.BuildOwnerSummary(summary)));
                elements.Add(CreateMetadata("Status", !string.IsNullOrEmpty(summary.ConflictHint) ? summary.ConflictHint : "Patched"));
                AppendPatchCards(elements, summary);
            }
            else
            {
                elements.Add(CreateText(string.Empty, !string.IsNullOrEmpty(statusMessage) ? statusMessage : (sourceReason ?? "No live Harmony patches are registered for this method."), false));
            }

            AppendIndirectRelationships(runtime, context, elements);
            AppendGenerationActions(runtime, elements);

            return new MethodInspectorSectionViewModel
            {
                Id = HarmonyPluginIds.InspectorSectionId,
                Title = "Harmony",
                Expanded = true,
                Elements = elements.ToArray()
            };
        }

        public MethodInspectorActionViewModel[] BuildRelationshipActions(WorkbenchMethodRelationshipActionContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new MethodInspectorActionViewModel[0];
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var relationship = context != null ? context.Relationship : null;
            if (runtime == null || relationship == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            var target = ToCommandTarget(relationship);
            if (target == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            string statusMessage;
            var summary = TryResolveSummary(runtime, target, false, out statusMessage);
            if (summary == null || !summary.IsPatched || summary.Entries == null || summary.Entries.Length == 0)
            {
                return new MethodInspectorActionViewModel[0];
            }

            return _navigationActionFactory.CreatePatchNavigationActions(
                summary.Entries[0].NavigationTarget,
                "Open Patch Method",
                "Open the matching Harmony patch method.");
        }

        public WorkbenchMethodInspectorActionResult HandleInspectorAction(WorkbenchMethodInspectorActionContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchMethodInspectorActionResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (context == null || runtime == null || string.IsNullOrEmpty(context.ActionId))
            {
                return new WorkbenchMethodInspectorActionResult();
            }

            HarmonyPatchNavigationTarget target;
            if (HarmonyMethodInspectorNavigationActionCodec.TryParse(context.ActionId, out target))
            {
                string statusMessage;
                return new WorkbenchMethodInspectorActionResult
                {
                    Handled = NavigateToPatch(runtime, target, out statusMessage)
                };
            }

            string actionStatus;
            bool executed;
            switch (context.ActionId)
            {
                case HarmonyPluginIds.ViewPatchesCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.ViewPatchesCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = executed
                    };
                case HarmonyPluginIds.GeneratePrefixCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.GeneratePrefixCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = executed,
                        CloseInspector = executed
                    };
                case HarmonyPluginIds.GeneratePostfixCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.GeneratePostfixCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = executed,
                        CloseInspector = executed
                    };
                case HarmonyPluginIds.CopySummaryCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.CopySummaryCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = executed
                    };
                case "cortex.harmony.navigateTarget":
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = NavigateToTarget(runtime, out actionStatus)
                    };
            }

            if (context.ActionId.StartsWith("cortex.harmony.openInsertion.", StringComparison.Ordinal))
            {
                var indexText = context.ActionId.Substring("cortex.harmony.openInsertion.".Length);
                int index;
                if (int.TryParse(indexText, out index))
                {
                    var workflow = _stateStore.GetWorkflow(runtime);
                    DocumentSession session;
                    if (workflow != null &&
                        index >= 0 &&
                        index < workflow.InsertionTargets.Count &&
                        _insertionService.TryOpenInsertionTarget(runtime, workflow.InsertionTargets[index], out session, out actionStatus))
                    {
                        return new WorkbenchMethodInspectorActionResult
                        {
                            Handled = true,
                            CloseInspector = true
                        };
                    }
                }
            }

            return new WorkbenchMethodInspectorActionResult();
        }

        public ExplorerNodeMatcher BuildExplorerMatcher(IWorkbenchModuleRuntime runtime, ExplorerFilterRuntimeContext context)
        {
            if (!IsRuntimeAvailable || runtime == null || context == null || context.Scope != ExplorerFilterScope.Decompiler)
            {
                return null;
            }

            EnsureSnapshot(runtime, false);
            var workflow = _stateStore.GetWorkflow(runtime);
            var snapshotMethods = workflow != null ? workflow.SnapshotMethods ?? new HarmonyMethodPatchSummary[0] : new HarmonyMethodPatchSummary[0];
            var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < snapshotMethods.Length; i++)
            {
                var summary = snapshotMethods[i];
                if (summary == null ||
                    !summary.IsPatched ||
                    string.IsNullOrEmpty(summary.AssemblyPath) ||
                    !ShouldIncludeSummary(summary, context))
                {
                    continue;
                }

                assemblies.Add(summary.AssemblyPath);
                var normalizedType = NormalizeTypeName(summary.DeclaringType);
                if (!string.IsNullOrEmpty(normalizedType))
                {
                    types.Add(summary.AssemblyPath + "|" + normalizedType);
                    AddNamespacePrefixes(namespaces, summary.AssemblyPath, normalizedType);
                }

                if (summary.Target != null && summary.Target.MetadataToken > 0)
                {
                    members.Add(summary.AssemblyPath + "|" + summary.Target.MetadataToken.ToString());
                }
            }

            return delegate(WorkspaceTreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                switch (node.NodeKind)
                {
                    case WorkspaceTreeNodeKind.DecompilerRoot:
                        return assemblies.Count > 0;
                    case WorkspaceTreeNodeKind.Assembly:
                        return assemblies.Contains(node.AssemblyPath ?? string.Empty);
                    case WorkspaceTreeNodeKind.Folder:
                        return namespaces.Contains((node.AssemblyPath ?? string.Empty) + "|" + (node.RelativePath ?? string.Empty));
                    case WorkspaceTreeNodeKind.Type:
                        return types.Contains((node.AssemblyPath ?? string.Empty) + "|" + NormalizeTypeName(node.TypeName));
                    case WorkspaceTreeNodeKind.Member:
                        return members.Contains((node.AssemblyPath ?? string.Empty) + "|" + node.MetadataToken.ToString());
                    default:
                        return false;
                }
            };
        }

        private void LoadMethodSummary(IWorkbenchModuleRuntime runtime, HarmonyResolvedMethodTarget resolvedTarget, bool forceRefresh, out string statusMessage)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null || resolvedTarget == null)
            {
                statusMessage = "Harmony state is not available.";
                return;
            }

            workflow.ActiveInspectionRequest = resolvedTarget.InspectionRequest;
            workflow.ActiveTypeAssemblyPath = string.Empty;
            workflow.ActiveTypeName = string.Empty;
            workflow.ActiveTypeDisplayName = string.Empty;
            workflow.ActiveTypeSummaries = new HarmonyMethodPatchSummary[0];
            workflow.ActiveSummary = GetSummary(runtime, resolvedTarget.InspectionRequest, forceRefresh, out statusMessage);
            workflow.ActiveSummaryKey = BuildSummaryKey(resolvedTarget.InspectionRequest);
            workflow.ActiveSymbolDisplay = resolvedTarget.DisplayName ?? string.Empty;
            workflow.ActiveDocumentPath = resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.DocumentPath ?? string.Empty : string.Empty;
            workflow.ActiveContainingTypeName = resolvedTarget.Method != null && resolvedTarget.Method.DeclaringType != null ? resolvedTarget.Method.DeclaringType.FullName ?? string.Empty : string.Empty;
            workflow.ActiveAssemblyName = resolvedTarget.InspectionRequest != null ? Path.GetFileNameWithoutExtension(resolvedTarget.InspectionRequest.AssemblyPath ?? string.Empty) ?? string.Empty : string.Empty;
            workflow.ActiveMetadataName = resolvedTarget.Method != null ? resolvedTarget.Method.Name ?? string.Empty : string.Empty;
            workflow.ResolutionFailureReason = string.Empty;

            var persistent = _stateStore.ReadPersistent(runtime);
            persistent.LastInspectedSymbol = workflow.ActiveSymbolDisplay;
            persistent.LastDocumentPath = workflow.ActiveDocumentPath;
            _stateStore.WritePersistent(runtime, persistent);
        }

        private void LoadTypeSummary(IWorkbenchModuleRuntime runtime, HarmonyResolvedTypeTarget resolvedType, bool forceRefresh, out string statusMessage)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null || resolvedType == null)
            {
                statusMessage = "Harmony state is not available.";
                return;
            }

            EnsureSnapshot(runtime, forceRefresh);
            workflow.ActiveInspectionRequest = null;
            workflow.ActiveSummary = null;
            workflow.ActiveSummaryKey = string.Empty;
            workflow.ActiveTypeAssemblyPath = resolvedType.AssemblyPath ?? string.Empty;
            workflow.ActiveTypeName = resolvedType.DeclaringType != null ? resolvedType.DeclaringType.FullName ?? string.Empty : string.Empty;
            workflow.ActiveTypeDisplayName = resolvedType.DisplayName ?? workflow.ActiveTypeName;

            var matches = new List<HarmonyMethodPatchSummary>();
            var snapshotMethods = workflow.SnapshotMethods ?? new HarmonyMethodPatchSummary[0];
            for (var i = 0; i < snapshotMethods.Length; i++)
            {
                var current = snapshotMethods[i];
                if (current != null &&
                    string.Equals(current.AssemblyPath ?? string.Empty, workflow.ActiveTypeAssemblyPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.DeclaringType ?? string.Empty, workflow.ActiveTypeName, StringComparison.Ordinal))
                {
                    matches.Add(current);
                }
            }

            workflow.ActiveTypeSummaries = matches.ToArray();
            statusMessage = matches.Count > 0
                ? "Loaded Harmony patch details for " + workflow.ActiveTypeName + "."
                : "No live Harmony patches are registered for " + workflow.ActiveTypeName + ".";
        }

        private HarmonyMethodPatchSummary TryResolveSummary(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, bool forceRefresh, out string statusMessage)
        {
            statusMessage = "No live Harmony patches are registered for this method.";
            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!_resolver.TryResolveMethod(runtime, target, out resolvedTarget, out reason) || resolvedTarget == null)
            {
                statusMessage = reason ?? statusMessage;
                return null;
            }

            var summary = GetSummary(runtime, resolvedTarget.InspectionRequest, forceRefresh, out statusMessage);
            return summary;
        }

        private HarmonyMethodPatchSummary GetSummary(IWorkbenchModuleRuntime runtime, HarmonyPatchInspectionRequest request, bool forceRefresh, out string statusMessage)
        {
            statusMessage = GetUnavailableMessage();
            var workflow = _stateStore.GetWorkflow(runtime);
            if (!IsRuntimeAvailable || workflow == null || request == null)
            {
                return null;
            }

            statusMessage = "Harmony patch data has not been loaded for this method yet.";

            EnsureSnapshot(runtime, forceRefresh);
            var key = BuildSummaryKey(request);
            HarmonyMethodPatchSummary summary;
            if (!forceRefresh && !string.IsNullOrEmpty(key) && workflow.SummaryCache.TryGetValue(key, out summary) && summary != null)
            {
                statusMessage = workflow.SnapshotStatusMessage ?? string.Empty;
                return summary;
            }

            summary = NormalizeSummary(runtime, _runtimeInspectionService.Inspect(request));
            if (summary == null)
            {
                statusMessage = "No Harmony metadata was returned for the selected method.";
                return null;
            }

            if (!string.IsNullOrEmpty(key))
            {
                workflow.SummaryCache[key] = summary;
            }

            statusMessage = summary.IsPatched
                ? "Loaded Harmony patch details for " + (summary.MethodName ?? string.Empty) + "."
                : "No live Harmony patches are registered for " + (summary.MethodName ?? string.Empty) + ".";
            return summary;
        }

        private void EnsureSnapshot(IWorkbenchModuleRuntime runtime, bool forceRefresh)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null)
            {
                return;
            }

            if (!forceRefresh &&
                !workflow.RefreshRequested &&
                workflow.SnapshotUtc != DateTime.MinValue &&
                DateTime.UtcNow - workflow.SnapshotUtc < TimeSpan.FromSeconds(4d))
            {
                return;
            }

            RefreshSnapshot(runtime);
        }

        private void RefreshSnapshot(IWorkbenchModuleRuntime runtime)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null)
            {
                return;
            }

            workflow.RuntimeAvailable = _runtimeInspectionService.IsAvailable;
            workflow.RefreshRequested = false;
            if (!_runtimeInspectionService.IsAvailable)
            {
                workflow.SnapshotMethods = new HarmonyMethodPatchSummary[0];
                workflow.SummaryCache.Clear();
                workflow.SnapshotUtc = DateTime.UtcNow;
                workflow.SnapshotStatusMessage = GetUnavailableMessage();
                return;
            }

            var snapshot = _runtimeInspectionService.CaptureSnapshot() ?? new HarmonyPatchSnapshot();
            var methods = snapshot.Methods ?? new HarmonyMethodPatchSummary[0];
            for (var i = 0; i < methods.Length; i++)
            {
                methods[i] = NormalizeSummary(runtime, methods[i]);
            }

            workflow.SnapshotMethods = methods;
            workflow.SnapshotUtc = snapshot.GeneratedUtc != DateTime.MinValue ? snapshot.GeneratedUtc : DateTime.UtcNow;
            workflow.SnapshotStatusMessage = snapshot.StatusMessage ?? string.Empty;
            workflow.SummaryCache.Clear();
            for (var i = 0; i < methods.Length; i++)
            {
                var summary = methods[i];
                if (summary == null)
                {
                    continue;
                }

                var key = BuildSummaryKey(new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = summary.AssemblyPath,
                    MetadataToken = summary.Target != null ? summary.Target.MetadataToken : 0,
                    DeclaringTypeName = summary.DeclaringType,
                    MethodName = summary.MethodName,
                    Signature = summary.Signature
                });
                if (!string.IsNullOrEmpty(key))
                {
                    workflow.SummaryCache[key] = summary;
                }
            }
        }

        private HarmonyMethodPatchSummary NormalizeSummary(IWorkbenchModuleRuntime runtime, HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return null;
            }

            summary.Counts = summary.Counts ?? new HarmonyPatchCounts();
            summary.Entries = summary.Entries ?? new HarmonyPatchEntry[0];
            summary.Order = summary.Order ?? new HarmonyPatchOrderExplanation[0];
            summary.Owners = summary.Owners ?? new string[0];
            summary.Target = summary.Target ?? new HarmonyPatchNavigationTarget();
            if (string.IsNullOrEmpty(summary.Target.AssemblyPath))
            {
                summary.Target.AssemblyPath = summary.AssemblyPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.Target.DisplayName))
            {
                summary.Target.DisplayName = _displayService.BuildTargetDisplayName(summary);
            }

            var targetProject = FindProject(runtime, summary.AssemblyPath, summary.DocumentPath);
            if (targetProject != null)
            {
                summary.ProjectModId = targetProject.ModId ?? string.Empty;
                summary.ProjectSourceRootPath = targetProject.SourceRootPath ?? string.Empty;
            }

            var targetMod = FindLoadedMod(runtime, summary.AssemblyPath);
            if (targetMod != null)
            {
                summary.LoadedModId = targetMod.ModId ?? string.Empty;
                summary.LoadedModRootPath = targetMod.RootPath ?? string.Empty;
            }

            for (var i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.Before = entry.Before ?? new string[0];
                entry.After = entry.After ?? new string[0];
                if (string.IsNullOrEmpty(entry.OwnerDisplayName))
                {
                    entry.OwnerDisplayName = entry.OwnerId ?? string.Empty;
                }

                entry.OwnerAssociation = ResolveOwnerAssociation(runtime, entry);
                if (entry.OwnerAssociation != null && !string.IsNullOrEmpty(entry.OwnerAssociation.DisplayName))
                {
                    entry.OwnerDisplayName = entry.OwnerAssociation.DisplayName;
                }
            }

            if (summary.CapturedUtc == DateTime.MinValue)
            {
                summary.CapturedUtc = DateTime.UtcNow;
            }

            return summary;
        }

        private bool TryValidateGenerationTarget(HarmonyResolvedMethodTarget resolvedTarget, out string reason)
        {
            reason = string.Empty;
            if (resolvedTarget == null || resolvedTarget.Method == null || resolvedTarget.InspectionRequest == null)
            {
                reason = "Select a resolvable external runtime method before generating a Harmony patch.";
                return false;
            }

            var request = resolvedTarget.InspectionRequest;
            if (string.IsNullOrEmpty(request.AssemblyPath) || request.MetadataToken <= 0)
            {
                reason = "Harmony patch generation requires a resolved external runtime method.";
                return false;
            }

            var project = resolvedTarget.Project;
            if (project == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(request.DocumentPath) && PathStartsWith(request.DocumentPath, project.SourceRootPath))
            {
                reason = "Harmony patch generation is only available for external patch targets, not methods from your own source project.";
                return false;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) &&
                !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                PathsEqual(request.AssemblyPath, project.OutputAssemblyPath))
            {
                reason = "Harmony patch generation is only available for external patch targets, not methods from your own built assembly.";
                return false;
            }

            return true;
        }

        private HarmonyPatchGenerationRequest CreateDefaultRequest(HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationKind generationKind)
        {
            var method = resolvedTarget != null ? resolvedTarget.Method : null;
            var request = new HarmonyPatchGenerationRequest();
            request.GenerationKind = generationKind;
            request.TargetAssemblyPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.AssemblyPath : string.Empty;
            request.TargetMetadataToken = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.MetadataToken : 0;
            request.TargetDeclaringTypeName = method != null && method.DeclaringType != null ? method.DeclaringType.FullName ?? string.Empty : string.Empty;
            request.TargetMethodName = method != null ? method.Name : string.Empty;
            request.TargetSignature = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.Signature ?? string.Empty : string.Empty;
            request.TargetDocumentPath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.DocumentPath ?? string.Empty : string.Empty;
            request.TargetCachePath = resolvedTarget != null && resolvedTarget.InspectionRequest != null ? resolvedTarget.InspectionRequest.CachePath ?? string.Empty : string.Empty;
            request.NamespaceName = resolvedTarget != null && resolvedTarget.Project != null && !string.IsNullOrEmpty(resolvedTarget.Project.ModId)
                ? SanitizeIdentifier(resolvedTarget.Project.ModId) + ".Harmony"
                : "GeneratedHarmonyPatches";
            request.PatchClassName = BuildPatchClassName(method, generationKind);
            request.PatchMethodName = generationKind == HarmonyPatchGenerationKind.Prefix ? "Prefix" : "Postfix";
            request.InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.EndOfFile;
            request.IncludeInstanceParameter = method != null && !method.IsStatic && !method.IsConstructor;
            request.IncludeArgumentParameters = method != null && method.GetParameters().Length > 0;
            request.IncludeStateParameter = generationKind == HarmonyPatchGenerationKind.Prefix;
            request.IncludeResultParameter = generationKind == HarmonyPatchGenerationKind.Postfix && method is MethodInfo && ((MethodInfo)method).ReturnType != typeof(void);
            request.UseSkipOriginalPattern = false;
            return request;
        }

        private static string BuildPatchClassName(MethodBase method, HarmonyPatchGenerationKind generationKind)
        {
            var typeName = method != null && method.DeclaringType != null ? method.DeclaringType.Name ?? "Target" : "Target";
            var methodName = method != null ? method.Name ?? "Method" : "Method";
            return SanitizeIdentifier(typeName) + "_" + SanitizeIdentifier(methodName) + "_" + (generationKind == HarmonyPatchGenerationKind.Prefix ? "PrefixPatch" : "PostfixPatch");
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Generated";
            }

            var characters = value.ToCharArray();
            for (var i = 0; i < characters.Length; i++)
            {
                if (!char.IsLetterOrDigit(characters[i]) && characters[i] != '_')
                {
                    characters[i] = '_';
                }
            }

            var result = new string(characters).Trim('_');
            if (string.IsNullOrEmpty(result))
            {
                result = "Generated";
            }

            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "_" + result;
            }

            return result;
        }

        private void AppendPatchCards(List<MethodInspectorElementViewModel> elements, HarmonyMethodPatchSummary summary)
        {
            var entries = summary != null ? summary.Entries ?? new HarmonyPatchEntry[0] : new HarmonyPatchEntry[0];
            for (var i = 0; i < entries.Length && i < 6; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = _displayService.GetPatchKindLabel(entry.PatchKind) + ": " + (entry.PatchMethodName ?? entry.OwnerDisplayName ?? entry.OwnerId ?? string.Empty),
                    Rows = new[]
                    {
                        CreateMetadata("Owner", !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId),
                        CreateMetadata("Priority", entry.Priority.ToString()),
                        CreateMetadata("Patch Method", (entry.PatchMethodDeclaringType ?? string.Empty) + "." + (entry.PatchMethodName ?? string.Empty) + (entry.PatchMethodSignature ?? string.Empty)),
                        CreateMetadata("Order", "Before: " + Join(entry.Before) + " | After: " + Join(entry.After))
                    },
                    Actions = _navigationActionFactory.CreatePatchNavigationActions(entry.NavigationTarget, "Open Patch Method", "Open this patch method.")
                });
            }
        }

        private void AppendIndirectRelationships(IWorkbenchModuleRuntime runtime, WorkbenchMethodInspectorContext context, List<MethodInspectorElementViewModel> elements)
        {
            var incoming = context != null && context.Relationships != null ? context.Relationships.IncomingCalls ?? new WorkbenchMethodRelationship[0] : new WorkbenchMethodRelationship[0];
            var patched = 0;
            for (var i = 0; i < incoming.Length && patched < 4; i++)
            {
                var caller = incoming[i];
                var target = caller != null ? ToCommandTarget(caller) : null;
                if (target == null)
                {
                    continue;
                }

                string statusMessage;
                var summary = TryResolveSummary(runtime, target, false, out statusMessage);
                if (summary == null || !summary.IsPatched)
                {
                    continue;
                }

                if (patched == 0)
                {
                    elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                    elements.Add(CreateText("Indirect Harmony", "Incoming callers with live Harmony patches.", false));
                }

                patched++;
                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = caller.Title ?? "Patched Caller",
                    Rows = new[]
                    {
                        CreateMetadata("Relationship", (caller.Relationship ?? "Call") + " (" + caller.CallCount + ")"),
                        CreateMetadata("Type", caller.ContainingTypeName ?? string.Empty),
                        CreateMetadata("Counts", _displayService.BuildCountBreakdown(summary.Counts))
                    },
                    Actions = summary.Entries != null && summary.Entries.Length > 0
                        ? _navigationActionFactory.CreatePatchNavigationActions(summary.Entries[0].NavigationTarget, "Open Patch Method", "Open the matching patch method.")
                        : new MethodInspectorActionViewModel[0]
                });
            }
        }

        private void AppendGenerationActions(IWorkbenchModuleRuntime runtime, List<MethodInspectorElementViewModel> elements)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null)
            {
                return;
            }

            elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel
                {
                    Id = HarmonyPluginIds.ViewPatchesCommandId,
                    Label = "View Harmony Patches",
                    Enabled = true
                },
                Hint = "Open the Harmony tool window for this target."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel
                {
                    Id = "cortex.harmony.navigateTarget",
                    Label = "Navigate To Target",
                    Enabled = workflow.ActiveSummary != null && workflow.ActiveSummary.Target != null
                },
                Hint = "Open the resolved runtime target."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel
                {
                    Id = HarmonyPluginIds.GeneratePrefixCommandId,
                    Label = "Generate Prefix",
                    Enabled = true
                },
                Hint = "Prepare a Prefix patch workflow."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel
                {
                    Id = HarmonyPluginIds.GeneratePostfixCommandId,
                    Label = "Generate Postfix",
                    Enabled = true
                },
                Hint = "Prepare a Postfix patch workflow."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel
                {
                    Id = HarmonyPluginIds.CopySummaryCommandId,
                    Label = "Copy Summary",
                    Enabled = true
                },
                Hint = "Copy the current Harmony summary."
            });

            for (var i = 0; i < workflow.InsertionTargets.Count && i < 4; i++)
            {
                var current = workflow.InsertionTargets[i];
                if (current == null)
                {
                    continue;
                }

                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = current.DisplayName ?? current.FilePath ?? string.Empty,
                    Body = current.Reason ?? string.Empty,
                    Actions = new[]
                    {
                        new MethodInspectorActionViewModel
                        {
                            Id = "cortex.harmony.openInsertion." + i.ToString(),
                            Label = "Open And Place Here",
                            Enabled = true
                        }
                    }
                });
            }
        }

        private static MethodInspectorMetadataViewModel CreateMetadata(string label, string value)
        {
            return new MethodInspectorMetadataViewModel
            {
                Label = label ?? string.Empty,
                Value = !string.IsNullOrEmpty(value) ? value : "Unknown"
            };
        }

        private static MethodInspectorTextViewModel CreateText(string label, string value, bool monospace)
        {
            return new MethodInspectorTextViewModel
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Monospace = monospace
            };
        }

        private static string BuildSummaryKey(HarmonyPatchInspectionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) && request.MetadataToken > 0)
            {
                return request.AssemblyPath + "|0x" + request.MetadataToken.ToString("X8");
            }

            return (request.AssemblyPath ?? string.Empty) + "|" +
                (request.DeclaringTypeName ?? string.Empty) + "|" +
                (request.MethodName ?? string.Empty) + "|" +
                (request.Signature ?? string.Empty);
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

            return !string.IsNullOrEmpty(target.SymbolText) ? target.SymbolText : target.MetadataName ?? string.Empty;
        }

        private static string Join(string[] values)
        {
            return values != null && values.Length > 0 ? string.Join(", ", values) : "-";
        }

        private bool TryNavigate(IWorkbenchModuleRuntime runtime, HarmonyPatchNavigationTarget target, string successMessage, out string statusMessage)
        {
            statusMessage = "Could not open the requested Harmony target.";
            if (runtime == null || target == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath) && !IsDecompilerPath(target.DocumentPath))
            {
                var session = runtime.Documents != null ? runtime.Documents.Open(target.DocumentPath, target.Line > 0 ? target.Line : 1) : null;
                if (session != null)
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.AssemblyPath) && target.MetadataToken > 0)
            {
                var response = runtime.Navigation != null
                    ? runtime.Navigation.RequestDecompilerSource(target.AssemblyPath, target.MetadataToken, DecompilerEntityKind.Method, false)
                    : null;
                if (response != null && runtime.Navigation.OpenDecompilerResult(response, target.Line > 0 ? target.Line : 1))
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.CachePath) && File.Exists(target.CachePath))
            {
                var cacheSession = runtime.Documents != null ? runtime.Documents.Open(target.CachePath, target.Line > 0 ? target.Line : 1) : null;
                if (cacheSession != null)
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            return false;
        }

        private static CortexProjectDefinition FindProject(IWorkbenchModuleRuntime runtime, string assemblyPath, string documentPath)
        {
            var projects = runtime != null && runtime.Projects != null ? runtime.Projects.GetProjects() : null;
            if (projects == null)
            {
                return null;
            }

            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(documentPath) && PathStartsWith(documentPath, project.SourceRootPath))
                {
                    return project;
                }

                if (!string.IsNullOrEmpty(assemblyPath) &&
                    !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                    PathsEqual(assemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private static LoadedModInfo FindLoadedMod(IWorkbenchModuleRuntime runtime, string assemblyPath)
        {
            var mods = runtime != null && runtime.Projects != null ? runtime.Projects.GetLoadedMods() : null;
            if (mods == null || string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod != null && PathStartsWith(assemblyPath, mod.RootPath))
                {
                    return mod;
                }
            }

            return null;
        }

        private static EditorCommandTarget ToCommandTarget(WorkbenchMethodRelationship relationship)
        {
            if (relationship == null)
            {
                return null;
            }

            return new EditorCommandTarget
            {
                SymbolText = relationship.Title ?? string.Empty,
                MetadataName = relationship.MetadataName ?? string.Empty,
                SymbolKind = relationship.SymbolKind ?? string.Empty,
                ContainingTypeName = relationship.ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = relationship.ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = relationship.DocumentationCommentId ?? string.Empty,
                DefinitionDocumentPath = relationship.DefinitionDocumentPath ?? string.Empty,
                DocumentPath = relationship.DefinitionDocumentPath ?? string.Empty,
                QualifiedSymbolDisplay = relationship.Detail ?? relationship.Title ?? string.Empty
            };
        }

        private static void AddNamespacePrefixes(HashSet<string> namespaces, string assemblyPath, string declaringTypeName)
        {
            var normalizedType = NormalizeTypeName(declaringTypeName);
            var lastDot = normalizedType.LastIndexOf('.');
            if (lastDot <= 0)
            {
                return;
            }

            var namespacePath = normalizedType.Substring(0, lastDot);
            var segments = namespacePath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                current = string.IsNullOrEmpty(current) ? segments[i] : current + "." + segments[i];
                namespaces.Add((assemblyPath ?? string.Empty) + "|" + current);
            }
        }

        private static string NormalizeTypeName(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('+', '.').Trim();
        }

        private static bool ShouldIncludeSummary(HarmonyMethodPatchSummary summary, ExplorerFilterRuntimeContext context)
        {
            if (summary == null)
            {
                return false;
            }

            if (context == null || !context.RestrictToSelectedProject || context.SelectedProject == null)
            {
                return true;
            }

            return SummaryMatchesSelectedProject(summary, context.SelectedProject);
        }

        private static bool SummaryMatchesSelectedProject(HarmonyMethodPatchSummary summary, CortexProjectDefinition selectedProject)
        {
            if (summary == null || selectedProject == null)
            {
                return false;
            }

            var entries = summary.Entries ?? new HarmonyPatchEntry[0];
            for (var i = 0; i < entries.Length; i++)
            {
                if (MatchesSelectedProject(entries[i] != null ? entries[i].OwnerAssociation : null, selectedProject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesSelectedProject(HarmonyPatchOwnerAssociation association, CortexProjectDefinition selectedProject)
        {
            if (association == null || selectedProject == null)
            {
                return false;
            }

            var selectedModId = selectedProject.ModId ?? string.Empty;
            var selectedSourceRoot = NormalizePath(selectedProject.SourceRootPath);
            return MatchesValue(association.ProjectModId, selectedModId) ||
                MatchesValue(association.LoadedModId, selectedModId) ||
                MatchesValue(NormalizePath(association.ProjectSourceRootPath), selectedSourceRoot);
        }

        private static HarmonyPatchOwnerAssociation ResolveOwnerAssociation(IWorkbenchModuleRuntime runtime, HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var patchAssemblyPath = !string.IsNullOrEmpty(entry.AssemblyPath)
                ? entry.AssemblyPath
                : (entry.NavigationTarget != null ? entry.NavigationTarget.AssemblyPath ?? string.Empty : string.Empty);
            var association = new HarmonyPatchOwnerAssociation
            {
                OwnerId = entry.OwnerId ?? string.Empty,
                DisplayName = !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId ?? string.Empty,
                AssemblyPath = patchAssemblyPath ?? string.Empty
            };

            var loadedMod = ResolveLoadedMod(runtime, entry.OwnerId, patchAssemblyPath);
            if (loadedMod != null)
            {
                association.LoadedModId = loadedMod.ModId ?? string.Empty;
                association.LoadedModRootPath = loadedMod.RootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) ||
                    string.Equals(association.DisplayName, entry.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    association.DisplayName = !string.IsNullOrEmpty(loadedMod.DisplayName)
                        ? loadedMod.DisplayName
                        : loadedMod.ModId ?? string.Empty;
                }
            }

            var project = ResolveProject(runtime, entry.OwnerId, patchAssemblyPath, association.LoadedModId);
            if (project != null)
            {
                association.ProjectModId = project.ModId ?? string.Empty;
                association.ProjectSourceRootPath = project.SourceRootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) ||
                    string.Equals(association.DisplayName, entry.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    association.DisplayName = project.GetDisplayName();
                }
            }

            association.HasMatch = !string.IsNullOrEmpty(association.LoadedModId) || !string.IsNullOrEmpty(association.ProjectModId);
            return association;
        }

        private static bool IsDecompilerPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(filePath);
                return fullPath.IndexOf(Path.DirectorySeparatorChar + "cortex_cache" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fullPath.IndexOf(Path.AltDirectorySeparatorChar + "cortex_cache" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static LoadedModInfo ResolveLoadedMod(IWorkbenchModuleRuntime runtime, string ownerId, string patchAssemblyPath)
        {
            var mods = runtime != null && runtime.Projects != null ? runtime.Projects.GetLoadedMods() : null;
            if (mods == null)
            {
                return null;
            }

            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null)
                {
                    continue;
                }

                if (MatchesValue(ownerId, mod.ModId) ||
                    MatchesValue(ownerId, mod.DisplayName) ||
                    PathStartsWith(patchAssemblyPath, mod.RootPath))
                {
                    return mod;
                }
            }

            return null;
        }

        private static CortexProjectDefinition ResolveProject(IWorkbenchModuleRuntime runtime, string ownerId, string patchAssemblyPath, string preferredModId)
        {
            var projects = runtime != null && runtime.Projects != null ? runtime.Projects.GetProjects() : null;
            if (projects == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferredModId))
            {
                for (var i = 0; i < projects.Count; i++)
                {
                    var preferred = projects[i];
                    if (preferred != null && MatchesValue(preferred.ModId, preferredModId))
                    {
                        return preferred;
                    }
                }
            }

            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (MatchesValue(ownerId, project.ModId) ||
                    MatchesValue(ownerId, project.GetDisplayName()) ||
                    PathsEqual(patchAssemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private static bool MatchesValue(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.TrimEnd('\\', '/');
        }
    }
}

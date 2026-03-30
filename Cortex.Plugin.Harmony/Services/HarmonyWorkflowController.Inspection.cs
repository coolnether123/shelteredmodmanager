using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Plugin.Harmony.Services.Resolution;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
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

            return GetSummary(runtime, resolvedTarget.InspectionRequest, forceRefresh, out statusMessage);
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
    }
}

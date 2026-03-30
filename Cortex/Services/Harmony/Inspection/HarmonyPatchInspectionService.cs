using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Policy;

namespace Cortex.Services.Harmony.Inspection
{
    internal sealed class HarmonyPatchInspectionService
    {
        private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(4d);
        private readonly IHarmonyRuntimeInspectionService _runtimeInspectionService;
        private readonly IHarmonyPatchInspectionKeyService _keyService;
        private readonly IHarmonyPatchSummaryNormalizer _summaryNormalizer;

        public HarmonyPatchInspectionService(
            IHarmonyRuntimeInspectionService runtimeInspectionService,
            HarmonyPatchOwnershipService ownershipService,
            HarmonyPatchOrderService orderService)
            : this(
                runtimeInspectionService,
                new HarmonyPatchInspectionKeyService(),
                new HarmonyPatchSummaryNormalizer(ownershipService, orderService))
        {
        }

        internal HarmonyPatchInspectionService(
            IHarmonyRuntimeInspectionService runtimeInspectionService,
            IHarmonyPatchInspectionKeyService keyService,
            IHarmonyPatchSummaryNormalizer summaryNormalizer)
        {
            _runtimeInspectionService = runtimeInspectionService;
            _keyService = keyService;
            _summaryNormalizer = summaryNormalizer;
        }

        public bool IsAvailable
        {
            get { return _runtimeInspectionService != null && _runtimeInspectionService.IsAvailable; }
        }

        public string BuildKey(HarmonyPatchInspectionRequest request)
        {
            return _keyService.BuildKey(request);
        }

        public void RefreshSnapshot(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.RuntimeAvailable = IsAvailable;
            state.Harmony.RefreshRequested = false;
            if (!IsAvailable)
            {
                state.Harmony.SnapshotMethods = new HarmonyMethodPatchSummary[0];
                state.Harmony.SummaryCache.Clear();
                state.Harmony.SnapshotUtc = DateTime.UtcNow;
                state.Harmony.SnapshotStatusMessage = "Harmony runtime inspection is not available in the active host.";
                MMLog.WriteInfo("[Cortex.Harmony] Runtime inspection unavailable for the active host platform.");
                return;
            }

            var snapshot = _runtimeInspectionService.CaptureSnapshot() ?? new HarmonyPatchSnapshot();
            var methods = snapshot.Methods ?? new HarmonyMethodPatchSummary[0];
            state.Harmony.SnapshotMethods = methods;
            state.Harmony.SnapshotUtc = snapshot.GeneratedUtc != DateTime.MinValue ? snapshot.GeneratedUtc : DateTime.UtcNow;
            state.Harmony.SnapshotStatusMessage = snapshot.StatusMessage ?? string.Empty;
            state.Harmony.SummaryCache.Clear();

            for (var i = 0; i < methods.Length; i++)
            {
                var normalized = _summaryNormalizer.Normalize(methods[i], loadedModCatalog, projectCatalog);
                if (normalized == null)
                {
                    continue;
                }

                methods[i] = normalized;
                var key = BuildKey(new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = normalized.AssemblyPath,
                    MetadataToken = normalized.Target != null ? normalized.Target.MetadataToken : 0,
                    DeclaringTypeName = normalized.DeclaringType,
                    MethodName = normalized.MethodName,
                    Signature = normalized.Signature
                });
                if (!string.IsNullOrEmpty(key))
                {
                    state.Harmony.SummaryCache[key] = normalized;
                }
            }
        }

        public HarmonyMethodPatchSummary GetSummary(CortexShellState state, HarmonyPatchInspectionRequest request, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, bool forceRefresh, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || request == null)
            {
                statusMessage = "Harmony request was incomplete.";
                return null;
            }

            var key = BuildKey(request);
            if (forceRefresh || ShouldRefreshSnapshot(state))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            HarmonyMethodPatchSummary cached;
            if (!forceRefresh &&
                !string.IsNullOrEmpty(key) &&
                state.Harmony.SummaryCache.TryGetValue(key, out cached) &&
                cached != null)
            {
                statusMessage = state.Harmony.SnapshotStatusMessage ?? string.Empty;
                return cached;
            }

            if (!IsAvailable)
            {
                statusMessage = "Harmony runtime inspection is not available in the active host.";
                return null;
            }

            var summary = _runtimeInspectionService.Inspect(request);
            if (summary == null)
            {
                statusMessage = "No Harmony metadata was returned for the selected method.";
                return null;
            }

            summary = _summaryNormalizer.Normalize(summary, loadedModCatalog, projectCatalog);
            if (!string.IsNullOrEmpty(key))
            {
                state.Harmony.SummaryCache[key] = summary;
            }

            statusMessage = summary.IsPatched
                ? "Loaded Harmony patch details for " + (summary.MethodName ?? string.Empty) + "."
                : "No live Harmony patches are registered for " + (summary.MethodName ?? string.Empty) + ".";
            return summary;
        }

        public HarmonyMethodPatchSummary GetCachedSummary(CortexShellState state, HarmonyPatchInspectionRequest request)
        {
            if (state == null || state.Harmony == null || request == null)
            {
                return null;
            }

            HarmonyMethodPatchSummary summary;
            return state.Harmony.SummaryCache.TryGetValue(BuildKey(request), out summary)
                ? summary
                : null;
        }

        public HarmonyMethodPatchSummary GetCachedSummary(CortexShellState state, HarmonyPatchInspectionRequest request, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, bool initializeSnapshot, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || request == null)
            {
                return null;
            }

            if (initializeSnapshot && (state.Harmony.RefreshRequested || state.Harmony.SnapshotUtc == DateTime.MinValue))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            var cached = GetCachedSummary(state, request);
            statusMessage = cached != null
                ? state.Harmony.SnapshotStatusMessage ?? string.Empty
                : "Harmony patch data has not been loaded for this method yet.";
            return cached;
        }

        public HarmonyMethodPatchSummary[] GetTypeSummaries(CortexShellState state, string assemblyPath, string declaringTypeName, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, bool forceRefresh, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || string.IsNullOrEmpty(assemblyPath) || string.IsNullOrEmpty(declaringTypeName))
            {
                statusMessage = "Harmony type request was incomplete.";
                return new HarmonyMethodPatchSummary[0];
            }

            if (forceRefresh || ShouldRefreshSnapshot(state))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            if (!IsAvailable)
            {
                statusMessage = "Harmony runtime inspection is not available in the active host.";
                return new HarmonyMethodPatchSummary[0];
            }

            var snapshotMethods = state.Harmony.SnapshotMethods ?? new HarmonyMethodPatchSummary[0];
            var matches = new System.Collections.Generic.List<HarmonyMethodPatchSummary>();
            for (var i = 0; i < snapshotMethods.Length; i++)
            {
                var current = snapshotMethods[i];
                if (current == null ||
                    !string.Equals(current.AssemblyPath ?? string.Empty, assemblyPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(current.DeclaringType ?? string.Empty, declaringTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add(current);
            }

            statusMessage = matches.Count > 0
                ? "Loaded Harmony patch details for " + declaringTypeName + "."
                : "No live Harmony patches are registered for " + declaringTypeName + ".";
            return matches.ToArray();
        }

        private static bool ShouldRefreshSnapshot(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return false;
            }

            return state.Harmony.RefreshRequested ||
                state.Harmony.SnapshotUtc == DateTime.MinValue ||
                DateTime.UtcNow - state.Harmony.SnapshotUtc >= SnapshotTtl;
        }
    }
}

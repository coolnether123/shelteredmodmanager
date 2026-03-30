using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Policy;

namespace Cortex.Services.Harmony.Inspection
{
    internal interface IHarmonyPatchSummaryNormalizer
    {
        HarmonyMethodPatchSummary Normalize(HarmonyMethodPatchSummary summary, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog);
    }

    internal sealed class HarmonyPatchSummaryNormalizer : IHarmonyPatchSummaryNormalizer
    {
        private readonly HarmonyPatchOwnershipService _ownershipService;
        private readonly HarmonyPatchOrderService _orderService;

        public HarmonyPatchSummaryNormalizer(HarmonyPatchOwnershipService ownershipService, HarmonyPatchOrderService orderService)
        {
            _ownershipService = ownershipService;
            _orderService = orderService;
        }

        public HarmonyMethodPatchSummary Normalize(HarmonyMethodPatchSummary summary, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog)
        {
            if (summary == null)
            {
                return null;
            }

            summary.Counts = summary.Counts ?? new HarmonyPatchCounts();
            summary.Entries = summary.Entries ?? new HarmonyPatchEntry[0];
            summary.Order = summary.Order ?? new HarmonyPatchOrderExplanation[0];
            summary.Owners = summary.Owners ?? new string[0];
            if (summary.Target == null)
            {
                summary.Target = new HarmonyPatchNavigationTarget();
            }

            if (string.IsNullOrEmpty(summary.Target.DisplayName))
            {
                summary.Target.DisplayName = !string.IsNullOrEmpty(summary.ResolvedMemberDisplayName)
                    ? summary.ResolvedMemberDisplayName
                    : (summary.DeclaringType ?? string.Empty) + "." + (summary.MethodName ?? string.Empty) + (summary.Signature ?? string.Empty);
            }

            if (string.IsNullOrEmpty(summary.Target.AssemblyPath))
            {
                summary.Target.AssemblyPath = summary.AssemblyPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.DocumentPath))
            {
                summary.DocumentPath = summary.Target.DocumentPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.CachePath))
            {
                summary.CachePath = summary.Target.CachePath ?? string.Empty;
            }

            var targetProject = _ownershipService != null
                ? _ownershipService.ResolveProjectForAssembly(summary.AssemblyPath, projectCatalog)
                : null;
            if (targetProject != null)
            {
                summary.ProjectModId = targetProject.ModId ?? string.Empty;
                summary.ProjectSourceRootPath = targetProject.SourceRootPath ?? string.Empty;
            }

            var targetMod = _ownershipService != null
                ? _ownershipService.ResolveLoadedModForAssembly(summary.AssemblyPath, loadedModCatalog)
                : null;
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
                entry.OwnerAssociation = _ownershipService != null
                    ? _ownershipService.Resolve(entry.OwnerId, entry.AssemblyPath, loadedModCatalog, projectCatalog)
                    : entry.OwnerAssociation;
                if (entry.OwnerAssociation != null && entry.OwnerAssociation.HasMatch)
                {
                    entry.OwnerDisplayName = entry.OwnerAssociation.DisplayName ?? entry.OwnerDisplayName;
                }
            }

            if (_orderService != null)
            {
                summary.Order = _orderService.BuildOrder(summary);
                summary.ConflictHint = _orderService.BuildConflictHint(summary);
            }

            if (summary.CapturedUtc == DateTime.MinValue)
            {
                summary.CapturedUtc = DateTime.UtcNow;
            }

            return summary;
        }
    }
}

using System;
using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Policy
{
    internal sealed class HarmonyPatchOrderService
    {
        public string BuildConflictHint(HarmonyMethodPatchSummary summary)
        {
            if (summary == null || summary.Counts == null || summary.Counts.TotalCount <= 0)
            {
                return "No active Harmony patches are registered for this method.";
            }

            var ownerCount = summary.OwnerCount > 0 ? summary.OwnerCount : 1;
            if (HasExplicitConstraints(summary))
            {
                return "Order is inferred from Harmony priority, before/after constraints, and Harmony patch indexes.";
            }

            if (summary.Counts.TotalCount >= 4 || ownerCount >= 3)
            {
                return "Patch density is high. Review ordering and owner overlap before changing behavior assumptions.";
            }

            if (ownerCount > 1)
            {
                return "Patched by multiple owners. Priority can change effective execution order independently of mod load order.";
            }

            return "Single-owner Harmony patch set detected.";
        }

        public HarmonyPatchOrderExplanation[] BuildOrder(HarmonyMethodPatchSummary summary)
        {
            if (summary == null || summary.Entries == null || summary.Entries.Length == 0)
            {
                return new HarmonyPatchOrderExplanation[0];
            }

            var results = new List<HarmonyPatchOrderExplanation>();
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.Prefix);
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.Postfix);
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.Transpiler);
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.Finalizer);
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.InnerPrefix);
            AddPatchGroup(results, summary.Entries, HarmonyPatchKind.InnerPostfix);
            return results.ToArray();
        }

        private static bool HasExplicitConstraints(HarmonyMethodPatchSummary summary)
        {
            if (summary == null || summary.Entries == null)
            {
                return false;
            }

            for (var i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                if ((entry.Before != null && entry.Before.Length > 0) ||
                    (entry.After != null && entry.After.Length > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddPatchGroup(List<HarmonyPatchOrderExplanation> results, HarmonyPatchEntry[] entries, HarmonyPatchKind patchKind)
        {
            var filtered = new List<HarmonyPatchEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.PatchKind == patchKind)
                {
                    filtered.Add(entry);
                }
            }

            if (filtered.Count == 0)
            {
                return;
            }

            filtered.Sort(CompareEntries);
            var items = new HarmonyPatchOrderExplanationItem[filtered.Count];
            for (var i = 0; i < filtered.Count; i++)
            {
                var entry = filtered[i];
                items[i] = new HarmonyPatchOrderExplanationItem
                {
                    Position = i + 1,
                    OwnerId = entry.OwnerDisplayName ?? entry.OwnerId ?? string.Empty,
                    PatchMethodName = BuildPatchMethodLabel(entry),
                    Priority = entry.Priority,
                    Index = entry.Index,
                    Before = entry.Before ?? new string[0],
                    After = entry.After ?? new string[0],
                    Explanation = BuildExplanation(entry)
                };
            }

            results.Add(new HarmonyPatchOrderExplanation
            {
                PatchKind = patchKind,
                Disclaimer = BuildDisclaimer(filtered),
                Items = items
            });
        }

        private static int CompareEntries(HarmonyPatchEntry left, HarmonyPatchEntry right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var priorityOrder = right.Priority.CompareTo(left.Priority);
            if (priorityOrder != 0)
            {
                return priorityOrder;
            }

            var constrainedOrder = CompareConstraintDensity(left, right);
            if (constrainedOrder != 0)
            {
                return constrainedOrder;
            }

            var indexOrder = left.Index.CompareTo(right.Index);
            if (indexOrder != 0)
            {
                return indexOrder;
            }

            return string.Compare(BuildPatchMethodLabel(left), BuildPatchMethodLabel(right), StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareConstraintDensity(HarmonyPatchEntry left, HarmonyPatchEntry right)
        {
            var leftCount = CountConstraints(left);
            var rightCount = CountConstraints(right);
            return rightCount.CompareTo(leftCount);
        }

        private static int CountConstraints(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            var beforeCount = entry.Before != null ? entry.Before.Length : 0;
            var afterCount = entry.After != null ? entry.After.Length : 0;
            return beforeCount + afterCount;
        }

        private static string BuildDisclaimer(List<HarmonyPatchEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return string.Empty;
            }

            var hasConstraints = false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (CountConstraints(entries[i]) > 0)
                {
                    hasConstraints = true;
                    break;
                }
            }

            return hasConstraints
                ? "Estimated execution order. Sorted primarily by Harmony priority and patch index; before/after constraints may still reorder runtime execution. This is not mod load order."
                : "Estimated execution order from Harmony priority and patch index. This is not mod load order.";
        }

        private static string BuildExplanation(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var text = "Priority " + entry.Priority + ", index " + entry.Index + ".";
            if ((entry.Before != null && entry.Before.Length > 0) || (entry.After != null && entry.After.Length > 0))
            {
                text += " Constraints shown separately may move effective runtime order.";
            }

            return text;
        }

        private static string BuildPatchMethodLabel(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return (entry.PatchMethodDeclaringType ?? string.Empty) +
                "." +
                (entry.PatchMethodName ?? string.Empty) +
                (entry.PatchMethodSignature ?? string.Empty);
        }
    }
}
